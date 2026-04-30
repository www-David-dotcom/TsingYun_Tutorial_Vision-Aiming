extends Area3D
# One plate of the four-plate armor ring. Listens for projectile-area
# overlaps via Godot's Area3D `area_entered` signal and bubbles damage
# up to the parent chassis through the `plate_hit` signal. The plate
# itself stores no HP — there is one HP pool per robot, on the chassis,
# and hits on any plate accumulate into it (real-RM convention).
#
# The `team`, `face`, and `number` properties are filled in by the
# chassis at spawn-time (see chassis.gd:_assign_armor_metadata). `number`
# is the RoboMaster numeric tag (1=Hero, 2=Engineer, 3/4/5=Standard,
# 7=Sentry) — one per robot, identical on all 4 plates. An MNIST sample
# of that digit is applied to the plate face as a sticker by
# sticker_loader.gd at episode reset.

signal plate_hit(damage: int, source_id: int)

@export var team: String = "blue"
@export var face: String = "front"
@export var number: int = 3


func _ready() -> void:
    body_entered.connect(_on_body_entered)


func plate_id() -> String:
    return "%s.%s" % [team, face]


func reset_for_new_episode() -> void:
    # No per-plate state to reset — chassis owns HP and calls
    # refresh_glow(1.0) on every plate after restoring hp = max_hp.
    pass


func apply_sticker(tex: Texture2D) -> void:
    # Sticker quad is a child node "Sticker" (MeshInstance3D with a
    # StandardMaterial3D albedo). sticker_loader.gd hands the chassis-
    # wide MNIST texture to every plate, so all four match.
    var sticker := get_node_or_null("Sticker") as MeshInstance3D
    if sticker == null:
        return
    var mat := sticker.material_override as StandardMaterial3D
    if mat == null:
        return
    mat.albedo_texture = tex


# Called by chassis after every hit (and at episode reset with t=1).
# t = chassis.hp / chassis.max_hp ∈ [0,1]. All four plates glow
# identically because they share the chassis HP.
func refresh_glow(t: float) -> void:
    var mesh := $Mesh as MeshInstance3D
    if mesh == null or mesh.material_override == null:
        return
    var mat := mesh.material_override as StandardMaterial3D
    if mat == null:
        return
    var glow_color: Color = Color(0.2, 0.4, 1.0).lerp(Color(1.0, 0.2, 0.1), 1.0 - t)
    mat.emission = glow_color
    mat.emission_energy_multiplier = lerp(1.5, 4.5, 1.0 - t)


func _on_body_entered(other: Node3D) -> void:
    # Projectiles live on collision_layer = 8; the plate's collision_mask
    # is 8 too, so this signal only fires for projectiles entering the
    # plate's bounding box. Chassis (layer 1) and floor (layer 2) are
    # filtered out at the physics layer.
    if other.has_method("on_armor_hit"):
        var dmg: int = other.on_armor_hit(self)
        if dmg > 0:
            plate_hit.emit(dmg, other.get_instance_id())
