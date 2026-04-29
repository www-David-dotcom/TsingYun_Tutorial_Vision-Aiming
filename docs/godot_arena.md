# Godot Arena — Architecture (Stage 2)

Stage 2 of the implementation plan stands up the **simulator** end of
the recruitment-cycle assignment. Until now (Stage 1 / `v0.5-infra`)
the candidate's stack talked to a Python `grpc_stub_server` that
echoed canned responses; from `v0.6-arena-poc` onward, it can talk to
a real Godot 4 scene with chassis, gimbal, projectiles, and four-plate
armor rings.

This document explains the moving parts and the deliberate transport
fallback. For "how do I run this" instructions see
[`shared/godot_arena/README.md`](../shared/godot_arena/README.md).

## Components

```
┌────────────────────────── Godot 4 process ──────────────────────────┐
│                                                                     │
│   arena_main.gd                                                     │
│      ├── BlueChassis  (CharacterBody3D + 4 ArmorPlate Area3D)       │
│      │     └── Gimbal (yaw/pitch + Camera3D + Muzzle Marker3D)      │
│      ├── RedChassis   (mirror layout, idle in Stage 2)              │
│      ├── ProjectileRoot                                             │
│      └── Hud (CanvasLayer; hidden in --headless)                    │
│                                                                     │
│   tcp_proto_server.gd  → :7654  control RPC (length-prefixed JSON)  │
│   tcp_frame_pub.gd     → :7655  raw RGB888 (16-byte header + frame) │
│   replay_recorder.gd   → user://replays/<episode_id>.json           │
│   seed_rng.gd          (autoload; one RNG, seeded by EnvReset)      │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘

         ▲ control                    ▲ frames
         │                            │
         ▼                            ▼
   candidate's stack (HW1+ inferer + EKF + MPC + strategy)
```

## Transport choice (the Stage-2 risk-mitigation)

`IMPLEMENTATION_PLAN.md` Stage 2 named "gRPC inside Godot" as the
single biggest risk and authorized a fallback: "drop to plain TCP +
protobuf framing (length-prefixed frames). The proto contract is
unchanged; only the transport is."

What `v0.6-arena-poc` ships:

| Direction | Port | Encoding |
|-----------|------|----------|
| Control   | 7654 | length-prefixed (4-byte BE) **JSON** |
| Frames    | 7655 | 16-byte LE header (`<QQ frame_id stamp_ns>`) + RGB888 |

JSON instead of binary protobuf because pure-GDScript protobuf does
not exist in the engine's standard library and writing a serializer
would have eaten the rest of the stage. JSON field names match
proto3 exactly, so every dict round-trips through
`google.protobuf.json_format.ParseDict` into a strongly-typed
message — the contract is preserved.

The Python `grpc_stub_server` from Stage 1 keeps speaking real gRPC.
Both arenas implement the same logical contract; the candidate
chooses transport via the simulator they connect to.

When does the binary-proto / gRPC path come back? Likely Stage 7
(visual review milestone) or whenever the candidate's inference loop
needs the binary path's lower latency. The proto schema doesn't
change at that point — only how it's framed on the wire.

See [`shared/godot_arena/addons/grpc_gd/README.md`](../shared/godot_arena/addons/grpc_gd/README.md)
for the deferred GDExtension's design.

## Wire-format conformance

The contract is enforced by [`tests/test_godot_wire_format.py`](../tests/test_godot_wire_format.py).
Each test builds the dict shape that the GDScript actually emits and
parses it through `google.protobuf.json_format.ParseDict`; if the
GDScript shape drifts from the proto schema, the matching test breaks
first.

CI also runs `tools/scripts/smoke_godot_arena.py` (manually, not on
GitHub-hosted runners) end-to-end against a real `godot --headless`
process: env_reset → 10 × env_step → env_push_fire → env_finish.

## What's deferred from the original Stage 2 goals

| Goal | Status |
|------|--------|
| Scenes (chassis / gimbal / armor / projectile / arena) | shipped |
| Mecanum-flavoured chassis kinematics | shipped (no slip model) |
| 2-axis gimbal with motor lag | shipped |
| Quadratic-drag projectile physics | shipped |
| 4 armor plates per chassis with collision damage | shipped |
| TCP control surface speaking proto-shaped JSON | shipped |
| TCP frame stream | shipped |
| JSON replay recorder | shipped |
| MP4 replay (via `--write-movie`) | usable; verified manually only |
| Headless export presets (Linux / Windows / macOS) | shipped (presets); binaries built out-of-band |
| `kenney_scifi` art pack | deferred to Stage 7 |
| Hero/Engineer/Standard/Sentry icon SVGs on plates | deferred to Stage 7 |
| `grpc_gd` GDExtension | deferred (see addon README) |
| Vendored `godot_rl_agents` addon | placeholder; populated in Stage 3 |
| `bronze` opponent policy on the red chassis | deferred to Stage 3 (HW1 deliverable) |

## Smoke-test workflow

```bash
# Terminal 1 — start the arena headless
godot --path shared/godot_arena --headless --rendering-driver opengl3

# Terminal 2 — run the wire-format conformance test (no Godot needed)
uv run pytest tests/test_godot_wire_format.py -v

# Terminal 3 — drive the live arena over TCP
uv run python tools/scripts/smoke_godot_arena.py --seed 42 --ticks 30
```

Expected: smoke client prints monotonically incrementing `frame_id`,
ten gimbal-yaw deltas, a 3-pellet `FireResult` with `accepted=true`,
and a final `EpisodeStats` whose `episode_id` matches `ep-` + the
seed in hex (deterministic per seed).
