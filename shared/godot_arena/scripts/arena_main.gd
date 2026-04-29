extends Node3D
# Episode orchestrator. Owns the TCP control server, the frame publisher,
# and the replay recorder. The methods on this node are what the wire
# layer (tcp_proto_server.gd) calls when a request arrives.
#
# Wire contract (Stage 2 fallback per IMPLEMENTATION_PLAN.md Stage 2 risk
# notes): length-prefixed JSON whose field names mirror the proto3
# definitions in shared/proto/aiming.proto + sensor.proto + episode.proto.
# This means the same dicts deserialize via google.protobuf.json_format.Parse
# on the Python side; the proto contract is preserved.

const SIM_BUILD_SHA: String = "stage2-arena-poc-0.6"
const DEFAULT_DURATION_NS: int = 90_000_000_000  # 90 s

enum EpisodeState { IDLE, RUNNING, FINISHING }

@export var control_port: int = 7654
@export var frame_port: int = 7655

@onready var blue_chassis: CharacterBody3D = $BlueChassis
@onready var red_chassis: CharacterBody3D = $RedChassis
@onready var projectile_root: Node3D = $ProjectileRoot
@onready var hud: CanvasLayer = $Hud

var state: int = EpisodeState.IDLE
var episode_id: String = ""
var started_ticks_ms: int = 0
var duration_ns: int = DEFAULT_DURATION_NS
var opponent_tier: String = "bronze"
var oracle_hints: bool = false
var frame_id: int = 0

var control_server: Node
var frame_pub: Node
var replay: Node

var _events: Array = []         # list of ProjectileEvent dicts
var _aim_latencies_ns: Array = []
var _projectiles_fired: int = 0
var _armor_hits: int = 0
var _damage_dealt: int = 0
var _last_cmd_stamp_ns: int = 0


func _ready() -> void:
    _wire_signals()

    var control_scene: GDScript = load("res://scripts/tcp_proto_server.gd")
    control_server = Node.new()
    control_server.set_script(control_scene)
    control_server.arena = self
    control_server.port = control_port
    add_child(control_server)

    var frame_scene: GDScript = load("res://scripts/tcp_frame_pub.gd")
    frame_pub = Node.new()
    frame_pub.set_script(frame_scene)
    frame_pub.port = frame_port
    add_child(frame_pub)

    var replay_scene: GDScript = load("res://scripts/replay_recorder.gd")
    replay = Node.new()
    replay.set_script(replay_scene)
    add_child(replay)

    # Hide HUD when running headless. CanvasLayer is invisible anyway in
    # --headless mode, but skipping the update calls saves CPU.
    if DisplayServer.get_name() == "headless":
        hud.visible = false

    print("[arena_main] control on tcp://0.0.0.0:%d, frames on tcp://0.0.0.0:%d"
          % [control_port, frame_port])


func _wire_signals() -> void:
    blue_chassis.team = "blue"
    blue_chassis.chassis_id = 0
    red_chassis.team = "red"
    red_chassis.chassis_id = 1
    blue_chassis.armor_hit.connect(_on_blue_armor_hit)
    red_chassis.armor_hit.connect(_on_red_armor_hit)


# ----------------------------------------------------------------------- RPCs

func env_reset(request: Dictionary) -> Dictionary:
    var seed_value: int = int(request.get("seed", 0))
    opponent_tier = String(request.get("opponent_tier", "bronze"))
    oracle_hints = bool(request.get("oracle_hints", false))
    duration_ns = int(request.get("duration_ns", 0))
    if duration_ns == 0:
        duration_ns = DEFAULT_DURATION_NS

    SeedRng.reseed(seed_value)
    episode_id = "ep-%016x" % seed_value
    started_ticks_ms = Time.get_ticks_msec()
    frame_id = 0
    _events.clear()
    _aim_latencies_ns.clear()
    _projectiles_fired = 0
    _armor_hits = 0
    _damage_dealt = 0

    blue_chassis.reset_for_new_episode(Vector3(-3.0, 0.0, 0.0), 0.0)
    red_chassis.reset_for_new_episode(Vector3(3.0, 0.0, 0.0), PI)
    for child in projectile_root.get_children():
        child.queue_free()

    state = EpisodeState.RUNNING
    replay.start(episode_id, seed_value)

    return {
        "bundle": _build_sensor_bundle(),
        "zmq_frame_endpoint": "tcp://127.0.0.1:%d" % frame_port,
        "simulator_build_sha256": SIM_BUILD_SHA,
    }


func env_step(cmd: Dictionary) -> Dictionary:
    if state != EpisodeState.RUNNING:
        return _error("env_step called in state=%d" % state)

    var cmd_stamp: int = int(cmd.get("stamp_ns", _now_ns()))
    _last_cmd_stamp_ns = cmd_stamp

    var gimbal: Node = blue_chassis.get_node_or_null("Gimbal")
    if gimbal != null:
        gimbal.set_target(
            float(cmd.get("target_yaw", 0.0)),
            float(cmd.get("target_pitch", 0.0)),
            float(cmd.get("yaw_rate_ff", 0.0)),
            float(cmd.get("pitch_rate_ff", 0.0)),
        )

    frame_id += 1
    # _now_ns is already elapsed-since-start, so compare directly to
    # the configured duration. Don't re-subtract started_ticks_ms.
    if _now_ns() > duration_ns:
        state = EpisodeState.FINISHING
    return _build_sensor_bundle()


func env_push_fire(cmd: Dictionary) -> Dictionary:
    if state != EpisodeState.RUNNING:
        return {"accepted": false, "reason": "no_episode", "queued_count": 0}
    var burst: int = max(0, int(cmd.get("burst_count", 1)))
    var queued: int = _spawn_projectiles(burst)
    return {
        "accepted": queued > 0,
        "reason": "" if queued == burst else "rate_limit",
        "queued_count": queued,
    }


func env_finish(_request: Dictionary) -> Dictionary:
    if state == EpisodeState.IDLE:
        return _error("no episode in progress")

    state = EpisodeState.IDLE
    var stats: Dictionary = _build_episode_stats()
    replay.finish(stats)
    return stats


# --------------------------------------------------------------- internals

func _spawn_projectiles(burst: int) -> int:
    var gimbal: Node = blue_chassis.get_node_or_null("Gimbal")
    if gimbal == null:
        return 0
    var projectile_scene: PackedScene = load("res://scenes/projectile.tscn")
    var queued: int = 0
    for _i in range(burst):
        var spec: Dictionary = gimbal.compute_shot()
        var projectile: RigidBody3D = projectile_scene.instantiate()
        projectile_root.add_child(projectile)
        projectile.global_transform = spec["transform"]
        projectile.arm(spec["velocity"], blue_chassis.team)
        queued += 1
        _projectiles_fired += 1
        _events.append({
            "stamp_ns": _now_ns(),
            "kind": "KIND_FIRED",
            "armor_id": "",
            "damage": 0,
        })
    return queued


func _build_sensor_bundle() -> Dictionary:
    var stamp: int = _now_ns()
    var gimbal_dict: Dictionary = blue_chassis.gimbal_state()
    var bundle: Dictionary = {
        "frame": {
            "frame_id": frame_id,
            "zmq_topic": "frames.%d" % SeedRng.current_seed(),
            "stamp_ns": stamp,
            "width": ProjectSettings.get_setting("display/window/size/viewport_width"),
            "height": ProjectSettings.get_setting("display/window/size/viewport_height"),
            "pixel_format": "PIXEL_FORMAT_RGB888",
        },
        "imu": {
            "stamp_ns": stamp,
            "angular_velocity": {"x": 0.0, "y": 0.0, "z": 0.0},
            "linear_accel": {"x": 0.0, "y": -9.81, "z": 0.0},
            "orientation": {"w": 1.0, "x": 0.0, "y": 0.0, "z": 0.0},
        },
        "gimbal": {
            "stamp_ns": stamp,
            "yaw": gimbal_dict["yaw"],
            "pitch": gimbal_dict["pitch"],
            "yaw_rate": gimbal_dict["yaw_rate"],
            "pitch_rate": gimbal_dict["pitch_rate"],
        },
        "odom": _odom_payload(stamp),
    }
    if oracle_hints:
        var red_pos: Vector3 = red_chassis.global_position
        var red_vel: Vector3 = red_chassis.velocity
        bundle["oracle"] = {
            "target_position_world": {"x": red_pos.x, "y": red_pos.y, "z": red_pos.z},
            "target_velocity_world": {"x": red_vel.x, "y": red_vel.y, "z": red_vel.z},
            "target_visible": true,
        }
    return bundle


func _odom_payload(stamp: int) -> Dictionary:
    var raw: Dictionary = blue_chassis.odom_state()
    raw["stamp_ns"] = stamp
    return raw


func _build_episode_stats() -> Dictionary:
    var p50: int = _percentile(_aim_latencies_ns, 50)
    var p95: int = _percentile(_aim_latencies_ns, 95)
    var p99: int = _percentile(_aim_latencies_ns, 99)
    return {
        "episode_id": episode_id,
        "seed": SeedRng.current_seed(),
        "duration_ns": _now_ns(),
        "candidate_commit_sha": "",
        "candidate_build_sha256": "",
        "simulator_build_sha256": SIM_BUILD_SHA,
        "opponent_policy_sha256": "",
        "opponent_tier": opponent_tier,
        "outcome": _resolve_outcome(),
        "damage_dealt": _damage_dealt,
        "damage_taken": blue_chassis.damage_taken,
        "projectiles_fired": _projectiles_fired,
        "armor_hits": _armor_hits,
        "aim_latency_p50_ns": p50,
        "aim_latency_p95_ns": p95,
        "aim_latency_p99_ns": p99,
        "events": _events.duplicate(),
    }


func _resolve_outcome() -> String:
    if blue_chassis.damage_taken >= 800 and _damage_dealt < 800:
        return "OUTCOME_LOSS"
    if _damage_dealt >= 800 and blue_chassis.damage_taken < 800:
        return "OUTCOME_WIN"
    return "OUTCOME_TIMEOUT"


func _on_blue_armor_hit(plate_id: String, damage: int, _source_id: int) -> void:
    _events.append({
        "stamp_ns": _now_ns(),
        "kind": "KIND_HIT_ARMOR",
        "armor_id": plate_id,
        "damage": damage,
    })


func _on_red_armor_hit(plate_id: String, damage: int, _source_id: int) -> void:
    _armor_hits += 1
    _damage_dealt += damage
    _events.append({
        "stamp_ns": _now_ns(),
        "kind": "KIND_HIT_ARMOR",
        "armor_id": plate_id,
        "damage": damage,
    })


func _percentile(values: Array, pct: int) -> int:
    if values.is_empty():
        return 0
    var sorted: Array = values.duplicate()
    sorted.sort()
    var idx: int = clamp(int(round(pct / 100.0 * (sorted.size() - 1))), 0, sorted.size() - 1)
    return int(sorted[idx])


func _error(msg: String) -> Dictionary:
    return {"_error": msg}


func _now_ns() -> int:
    return (Time.get_ticks_msec() - started_ticks_ms) * 1_000_000
