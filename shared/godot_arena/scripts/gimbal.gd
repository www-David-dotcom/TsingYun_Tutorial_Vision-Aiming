extends Node3D
# Two-axis gimbal: yaw is rotation around +Y, pitch is rotation around
# the local +X of the YawPivot. A first-order motor model rate-limits
# the slew so commanded targets don't snap instantaneously — that
# matches the physical RoboMaster gimbal behaviour HW5's MPC has to
# control.
#
# The shipped projectile is constant-velocity at the muzzle; ballistic
# physics happens in projectile.gd from there on.

const YAW_RATE_LIMIT: float = 12.0      # rad/s
const PITCH_RATE_LIMIT: float = 8.0     # rad/s
const PITCH_LIMIT_LO: float = -0.35     # rad (~-20 deg)
const PITCH_LIMIT_HI: float = 0.52      # rad (~+30 deg)
const MUZZLE_VELOCITY: float = 27.0     # m/s; matches RM 17mm spec
const MOTOR_LAG_TC: float = 0.04        # s; first-order torque lag

@onready var yaw_pivot: Node3D = $YawPivot
@onready var pitch_pivot: Node3D = $YawPivot/PitchPivot
@onready var muzzle: Marker3D = $YawPivot/PitchPivot/Muzzle

var target_yaw: float = 0.0
var target_pitch: float = 0.0
var yaw_rate: float = 0.0
var pitch_rate: float = 0.0
var yaw_rate_ff: float = 0.0
var pitch_rate_ff: float = 0.0


func set_target(yaw: float, pitch: float, yaw_ff: float = 0.0, pitch_ff: float = 0.0) -> void:
    target_yaw = yaw
    target_pitch = clamp(pitch, PITCH_LIMIT_LO, PITCH_LIMIT_HI)
    yaw_rate_ff = yaw_ff
    pitch_rate_ff = pitch_ff


func _physics_process(delta: float) -> void:
    var current_yaw: float = yaw_pivot.rotation.y
    var current_pitch: float = pitch_pivot.rotation.x

    var yaw_err: float = wrapf(target_yaw - current_yaw, -PI, PI)
    var pitch_err: float = target_pitch - current_pitch

    # First-order motor lag: rate command tracks (err / tc) + ff, then is
    # rate-limited and integrated.
    var yaw_cmd: float = yaw_err / MOTOR_LAG_TC + yaw_rate_ff
    var pitch_cmd: float = pitch_err / MOTOR_LAG_TC + pitch_rate_ff

    yaw_rate = clamp(yaw_cmd, -YAW_RATE_LIMIT, YAW_RATE_LIMIT)
    pitch_rate = clamp(pitch_cmd, -PITCH_RATE_LIMIT, PITCH_RATE_LIMIT)

    yaw_pivot.rotation.y = current_yaw + yaw_rate * delta
    pitch_pivot.rotation.x = clamp(current_pitch + pitch_rate * delta,
                                    PITCH_LIMIT_LO, PITCH_LIMIT_HI)


func get_state() -> Dictionary:
    return {
        "yaw": yaw_pivot.rotation.y,
        "pitch": pitch_pivot.rotation.x,
        "yaw_rate": yaw_rate,
        "pitch_rate": pitch_rate,
    }


func muzzle_world_transform() -> Transform3D:
    return muzzle.global_transform


# Returns {transform: Transform3D, velocity: Vector3} so the caller can
# instantiate, parent, and arm the projectile in one place. Spread is
# pulled from SeedRng so the same EnvReset.seed produces the same shot
# pattern.
func compute_shot() -> Dictionary:
    var muzzle_xform: Transform3D = muzzle.global_transform
    var fwd: Vector3 = -muzzle_xform.basis.z
    var jitter_yaw: float = SeedRng.next_range(-0.002, 0.002)
    var jitter_pitch: float = SeedRng.next_range(-0.002, 0.002)
    fwd = fwd.rotated(Vector3.UP, jitter_yaw).rotated(muzzle_xform.basis.x, jitter_pitch)
    return {
        "transform": muzzle_xform,
        "velocity": fwd * MUZZLE_VELOCITY,
    }
