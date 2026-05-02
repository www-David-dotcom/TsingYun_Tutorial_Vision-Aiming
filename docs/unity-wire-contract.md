# Unity Wire Contract

This is the stable runtime contract for candidate runners that drive the Unity
arena. Unity is the only active arena runtime; `shared/proto/*.proto` remains
the schema source of truth, while the current transport is length-prefixed JSON
with field names matching those protobuf messages.

## Endpoints

| Port | Direction | Encoding |
|------|-----------|----------|
| `7654` | Candidate runner -> Unity control RPC | TCP stream, one request/response at a time |
| `7655` | Unity -> candidate runner camera frames | TCP stream, raw RGB frames |

Control messages are encoded as:

1. A 4-byte big-endian unsigned payload length.
2. A UTF-8 JSON object with `method` and `request` fields.
3. A response using the same 4-byte length prefix followed by UTF-8 JSON.

Successful control responses are wrapped as:

```json
{ "ok": true, "response": { } }
```

Failures are wrapped as:

```json
{ "ok": false, "error": "message" }
```

The `response` object is the protobuf-compatible payload described below.

The frame stream sends repeated frames as:

1. A 16-byte little-endian header: `<uint64 frame_id, uint64 stamp_ns>`.
2. A `1280 * 720 * 3` byte RGB888 payload.

## Control Methods

`env_reset` maps to `EnvResetRequest` and returns `InitialState`.

Required runner fields:

| Field | Type | Notes |
|-------|------|-------|
| `seed` | uint64 | Same seed should produce the same episode setup. |
| `opponent_tier` | string | Currently `"bronze"`, `"silver"`, or `"gold"`; only bronze is implemented. |
| `oracle_hints` | bool | Enables ground-truth target pose in `SensorBundle.oracle` for tests. |
| `duration_ns` | uint64 | Compatibility only. Unity ignores overrides and uses the fixed 5-minute rule duration. |
| `training_config` | object | Optional team/backend-only controls for non-player opponent policy development. Student runners should omit this field. |

`training_config` is used for the solitary-game backend, where non-player
vehicles are driven by baseline or uploaded team policies. It is not homework
surface area, and students are not expected to train vehicle RL policies.

| `training_config` field | Type | Notes |
|-------------------------|------|-------|
| `enabled` | bool | Enables training-ground helpers. |
| `target_translation_speed_mps` | double | Target ping-pong speed in meters per second; negative values should be clamped by Unity. |
| `target_rotation_speed_rad_s` | double | Target chassis yaw speed in radians per second. |
| `target_path_half_extent_m` | double | Half-width of the target ping-pong path around its spawn point. |
| `baseline_opponent_enabled` | bool | Enables the geometric-center backend baseline opponent. |

`env_step` maps to `GimbalCmd` and returns `SensorBundle`.

| Field | Type | Notes |
|-------|------|-------|
| `target_yaw` | double | Absolute radians. Missing values default to `0.0`. |
| `target_pitch` | double | Absolute radians. Missing values default to `0.0`. |
| `yaw_rate_ff` | double | Optional feed-forward radians/second. |
| `pitch_rate_ff` | double | Optional feed-forward radians/second. |
| `stamp_ns` | uint64 | Accepted for protobuf compatibility; current Unity code does not use it for simulation time. |

`env_push_fire` maps to `FireCmd` and returns `FireResult`.

| Field | Type | Notes |
|-------|------|-------|
| `burst_count` | uint32 | Queued and drained at 5 rounds/second. |
| `stamp_ns` | uint64 | Accepted for protobuf compatibility. |

`FireResult.reason` is an empty string when accepted. Current rejection reasons
are `no_episode`, `destroyed`, `no_prefab`, and `fire_locked`.

`env_finish` maps to `EnvFinishRequest` and returns `EpisodeStats`.

`flush_replay` is accepted for compatibility. Unity finalizes its replay
recorder on every finish call.

## Sensor Bundle

Every `SensorBundle` contains:

| Field | Notes |
|-------|------|
| `frame` | `frame_id`, `zmq_topic`, `stamp_ns`, `width=1280`, `height=720`, `pixel_format=PIXEL_FORMAT_RGB888`. |
| `imu` | Zero angular velocity, gravity in `linear_accel.y`, identity orientation. |
| `gimbal` | Current yaw, pitch, yaw rate, pitch rate, plus `stamp_ns`. |
| `odom` | Blue chassis world position, linear velocity, yaw, plus `stamp_ns`. |
| `oracle` | Present only when `oracle_hints=true`. |
| `training` | Present only in backend training-ground runs; contains target motion, hit metrics, reward, and episode status for non-player policy development. |

Simulation timestamps are nanoseconds since the latest `env_reset`.

`SensorBundle.training` is intentionally backend-only telemetry. It supports
team-side policy training and smoke checks for non-player agents; candidate
assignments should continue to rely on `frame`, `gimbal`, `odom`, and optional
`oracle` test hints.

## Compatibility Gates

The lightweight no-Unity contract test is:

```bash
uv run pytest tests/test_arena_wire_format.py
```

The Unity smoke runner is:

```bash
UV_CACHE_DIR=.uv-cache uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 10
```

Use the protobuf files for generated client types:

- [`shared/proto/aiming.proto`](../shared/proto/aiming.proto)
- [`shared/proto/sensor.proto`](../shared/proto/sensor.proto)
- [`shared/proto/episode.proto`](../shared/proto/episode.proto)
