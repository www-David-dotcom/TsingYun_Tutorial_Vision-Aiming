extends RigidBody3D
# 17mm-style ball projectile. Quadratic drag is applied per physics step
# (Godot's built-in `linear_damp` is exponential decay, which is wrong
# for a real projectile). Gravity comes for free from the engine.
#
# Lifetime is capped by both range and ttl so a missed shot disappears
# instead of endlessly tunneling through the floor.

const DRAG_COEFFICIENT: float = 0.47          # sphere
const AIR_DENSITY: float = 1.225              # kg/m^3
const FRONTAL_AREA: float = 0.000227          # pi * 0.0085^2 (radius from .tscn)
const MAX_RANGE_M: float = 30.0
const MAX_TTL_S: float = 4.0
const DAMAGE: int = 50

var spawn_position: Vector3
var spawn_time_ms: int = 0
var team: String = "blue"
var consumed: bool = false


func arm(initial_velocity: Vector3, owning_team: String) -> void:
    spawn_position = global_position
    spawn_time_ms = Time.get_ticks_msec()
    team = owning_team
    linear_velocity = initial_velocity
    body_entered.connect(_on_body_entered)


func _physics_process(_delta: float) -> void:
    if consumed:
        return

    # Quadratic drag: F_drag = -0.5 * rho * Cd * A * |v| * v
    var v: Vector3 = linear_velocity
    var speed: float = v.length()
    if speed > 0.001:
        var drag_magnitude: float = 0.5 * AIR_DENSITY * DRAG_COEFFICIENT * FRONTAL_AREA * speed * speed
        var drag_force: Vector3 = -v.normalized() * drag_magnitude
        apply_central_force(drag_force)

    if (global_position - spawn_position).length() > MAX_RANGE_M:
        _consume("miss_range")
        return
    if Time.get_ticks_msec() - spawn_time_ms > int(MAX_TTL_S * 1000.0):
        _consume("miss_range")


func on_armor_hit(plate: Area3D) -> int:
    # Friendly fire is a no-op: same-team plates don't register damage.
    var plate_team: String = plate.get("team")
    if plate_team == team:
        _consume("friendly")
        return 0
    _consume("hit_armor:%s" % plate.plate_id())
    return DAMAGE


func _on_body_entered(_body: Node) -> void:
    if consumed:
        return
    _consume("hit_wall")


func _consume(_reason: String) -> void:
    consumed = true
    queue_free()
