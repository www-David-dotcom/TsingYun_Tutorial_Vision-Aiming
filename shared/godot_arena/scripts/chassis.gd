extends CharacterBody3D
# Chassis with a mecanum-flavoured kinematic model:
#
#   v_x_world = vx_body * cos(yaw) - vy_body * sin(yaw)
#   v_z_world = vx_body * sin(yaw) + vy_body * cos(yaw)
#
# The four-wheel mecanum mixing is *not* simulated — Godot's
# CharacterBody3D doesn't model wheel slip, and the RM rules don't
# punish ideal-kinematics simulators in a way that matters for HW1–HW7.
# Stage 7's MPC milestone may revisit if the no-slip assumption hurts
# tracking metrics.

signal armor_hit(plate_id: String, damage: int, source_id: int)

@export var team: String = "blue"
@export var chassis_id: int = 0
# RoboMaster numeric tag for this robot. 1=Hero, 2=Engineer, 3/4/5=Standard,
# 7=Sentry. One number per robot — every plate of this chassis displays
# the same number sticker (an MNIST sample of `chassis_number`).
@export var chassis_number: int = 3
@export_range(0.0, 4.0) var max_linear_speed: float = 3.5    # m/s
@export_range(0.0, 8.0) var max_angular_speed: float = 4.0   # rad/s

var chassis_yaw: float = 0.0
var cmd_vx: float = 0.0
var cmd_vy: float = 0.0
var cmd_omega: float = 0.0
var damage_taken: int = 0


func _ready() -> void:
    _assign_armor_metadata()
    chassis_yaw = rotation.y


func _assign_armor_metadata() -> void:
    # Chassis layout: front/back along local -Z/+Z, left/right along
    # local -X/+X. All four plates display the same number sticker
    # (chassis_number) — that's the real-RM convention: one number per
    # robot, four identical stickers.
    var faces := {
        "ArmorPlateFront": "front",
        "ArmorPlateBack":  "back",
        "ArmorPlateLeft":  "left",
        "ArmorPlateRight": "right",
    }
    for child_name in faces:
        var node: Node = get_node_or_null(child_name)
        if node == null:
            push_warning("chassis: missing armor child %s" % child_name)
            continue
        var face: String = faces[child_name]
        node.team = team
        node.face = face
        node.number = chassis_number
        node.plate_hit.connect(_on_plate_hit.bind(face))


func set_chassis_cmd(vx_body: float, vy_body: float, omega: float) -> void:
    cmd_vx = clamp(vx_body, -max_linear_speed, max_linear_speed)
    cmd_vy = clamp(vy_body, -max_linear_speed, max_linear_speed)
    cmd_omega = clamp(omega, -max_angular_speed, max_angular_speed)


func _physics_process(delta: float) -> void:
    chassis_yaw += cmd_omega * delta
    # Lock pitch/roll: the chassis stays upright on the flat floor and
    # only yaws. Going through `rotation =` rather than `rotation.y =`
    # zeroes out any drift on the other two axes.
    rotation = Vector3(0.0, chassis_yaw, 0.0)

    var cos_y: float = cos(chassis_yaw)
    var sin_y: float = sin(chassis_yaw)
    velocity.x = cmd_vx * cos_y - cmd_vy * sin_y
    velocity.z = cmd_vx * sin_y + cmd_vy * cos_y
    # Y-velocity stays under physics control (gravity); we don't fly.
    move_and_slide()


func reset_for_new_episode(spawn_position: Vector3, spawn_yaw: float) -> void:
    chassis_yaw = spawn_yaw
    rotation = Vector3(0.0, spawn_yaw, 0.0)
    global_position = spawn_position
    velocity = Vector3.ZERO
    cmd_vx = 0.0
    cmd_vy = 0.0
    cmd_omega = 0.0
    damage_taken = 0
    for child in [$ArmorPlateFront, $ArmorPlateBack, $ArmorPlateLeft, $ArmorPlateRight]:
        child.reset_for_new_episode()
    var loader: Node = get_node_or_null("StickerLoader")
    if loader != null:
        loader.load_sticker_for_current_number()


func odom_state() -> Dictionary:
    return {
        "position_world": _vec3_to_dict(global_position),
        "linear_velocity": _vec3_to_dict(velocity),
        "yaw_world": chassis_yaw,
    }


func gimbal_state() -> Dictionary:
    var gimbal: Node = get_node_or_null("Gimbal")
    if gimbal == null:
        return {"yaw": 0.0, "pitch": 0.0, "yaw_rate": 0.0, "pitch_rate": 0.0}
    return gimbal.get_state()


func _on_plate_hit(damage: int, source_id: int, face: String) -> void:
    damage_taken += damage
    armor_hit.emit("%s.%s" % [team, face], damage, source_id)


func _vec3_to_dict(v: Vector3) -> Dictionary:
    return {"x": v.x, "y": v.y, "z": v.z}
