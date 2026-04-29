extends Area3D
# One plate of the four-plate armor ring. Listens for projectile-area
# overlaps via Godot's Area3D `area_entered` signal, applies damage, and
# bubbles up to the parent chassis through the `plate_hit` signal.
#
# The `team` and `face` properties are filled in by the chassis at
# spawn-time (see chassis.gd:_assign_armor_metadata). `icon` mirrors
# RoboMaster's armor classification (Hero / Engineer / Standard / Sentry)
# — Stage 2 doesn't render the icon yet; the field exists so HW1's
# detector training can use it as a label.

signal plate_hit(damage: int, source_id: int)

@export var team: String = "blue"
@export var face: String = "front"
@export var icon: String = "Standard"
@export var max_hp: int = 200

var hp: int = 0


func _ready() -> void:
    hp = max_hp
    body_entered.connect(_on_body_entered)


func plate_id() -> String:
    return "%s.%s" % [team, face]


func apply_damage(amount: int, source_id: int) -> void:
    hp = max(0, hp - amount)
    plate_hit.emit(amount, source_id)
    _refresh_glow()


func reset_for_new_episode() -> void:
    hp = max_hp
    _refresh_glow()


func _on_body_entered(other: Node3D) -> void:
    # Projectiles live on collision_layer = 8; the plate's collision_mask
    # is 8 too, so this signal only fires for projectiles entering the
    # plate's bounding box. Chassis (layer 1) and floor (layer 2) are
    # filtered out at the physics layer.
    if other.has_method("on_armor_hit"):
        var dmg: int = other.on_armor_hit(self)
        if dmg > 0:
            apply_damage(dmg, other.get_instance_id())


func _refresh_glow() -> void:
    var mesh := $Mesh as MeshInstance3D
    if mesh == null or mesh.material_override == null:
        return
    var mat := mesh.material_override as StandardMaterial3D
    if mat == null:
        return
    var t: float = float(hp) / float(max_hp)
    # Cool blue when full, hot red as HP drains. Energy ramps up so the
    # plate glows brighter the closer it is to breaking.
    var glow_color: Color = Color(0.2, 0.4, 1.0).lerp(Color(1.0, 0.2, 0.1), 1.0 - t)
    mat.emission = glow_color
    mat.emission_energy_multiplier = lerp(1.5, 4.5, 1.0 - t)
