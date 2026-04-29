# Aiming Arena — Godot 4 Project

Stage 2 PoC for the `Aiming_HW` recruitment assignment. Implements the
simulator side of the contract from
[`shared/proto/aiming.proto`](../proto/aiming.proto): two chassis (blue
+ red) facing each other across a 20×20 m floor, mecanum-flavoured
kinematics, a 2-axis gimbal with first-order motor lag, projectile
physics with quadratic drag + gravity, and a four-plate armor ring per
chassis with collision-driven damage.

## Quickstart (team / candidates)

```bash
# 1. Install Godot 4.3 stable
#    https://godotengine.org/download/  (or `brew install --cask godot`)

# 2. Open the project (editor)
godot --path shared/godot_arena --editor

# 3. Headless run for protocol smoke-test
godot --path shared/godot_arena --headless --rendering-driver opengl3

# In another terminal:
uv run python -m grpc_stub_server --once          # baseline you can compare against
uv run python tools/scripts/smoke_godot_arena.py  # connects to tcp://127.0.0.1:7654
```

## Wire layout (Stage 2)

The project listens on **two TCP ports** by default:

| Port | Role | Encoding |
|------|------|----------|
| 7654 | Control RPC | length-prefixed (4-byte BE) JSON |
| 7655 | Frame stream | 16-byte LE header (`<QQ`) + RGB888 |

JSON field names match the proto3 wire format exactly — every dict
returned by the arena round-trips through
`google.protobuf.json_format.Parse(text, MessageType())`. This is the
fallback transport authorized by `IMPLEMENTATION_PLAN.md` Stage 2 risk
notes; the binary-proto / gRPC path is deferred to a later stage. See
[`addons/grpc_gd/README.md`](addons/grpc_gd/README.md) for the
rationale.

### Control RPC requests

```
{ "method": "env_reset",     "request": { /* EnvResetRequest */ } }
{ "method": "env_step",      "request": { /* GimbalCmd */ } }
{ "method": "env_push_fire", "request": { /* FireCmd */ } }
{ "method": "env_finish",    "request": { /* EnvFinishRequest */ } }
```

Replies:

```
{ "ok": true,  "response": { /* per-method message */ } }
{ "ok": false, "error":    "..." }
```

## Headless export (team-only)

Used to produce the binaries that get pushed to the public OSS bucket
under `assets/godot/<version>/`:

```bash
godot --path shared/godot_arena --headless \
      --export-release "Linux x86_64 Server" \
      builds/aiming_arena_linux.x86_64

godot --path shared/godot_arena --headless \
      --export-release "Windows x86_64" \
      builds/aiming_arena_win64.exe

godot --path shared/godot_arena --headless \
      --export-release "macOS Universal" \
      builds/aiming_arena_macos.zip

uv run python shared/scripts/push_assets.py \
    --bucket tsingyun-aiming-hw-public \
    --key-prefix assets/godot/0.6.0/ \
    builds/
```

## Headless replay capture

Godot's built-in movie maker writes an MP4 + WAV alongside the JSON
event stream from `replay_recorder.gd`:

```bash
godot --path shared/godot_arena --headless \
      --write-movie /tmp/episode.mp4 \
      --fixed-fps 60 \
      --quit-after 5400          # 90 s × 60 fps
```

Replay JSONs land at `user://replays/<episode_id>.json` (resolved to
`~/.local/share/godot/app_userdata/Aiming Arena/replays/` on Linux).

## Layout

```
shared/godot_arena/
├── project.godot
├── default_env.tres
├── icon.svg
├── export_presets.cfg
├── scenes/
│   ├── arena_main.tscn
│   ├── chassis.tscn
│   ├── gimbal.tscn
│   ├── armor_plate.tscn
│   ├── projectile.tscn
│   └── ui/replay_hud.tscn
├── scripts/
│   ├── arena_main.gd
│   ├── chassis.gd
│   ├── gimbal.gd
│   ├── armor_plate.gd
│   ├── projectile.gd
│   ├── tcp_proto_server.gd
│   ├── tcp_frame_pub.gd
│   ├── replay_recorder.gd
│   └── seed_rng.gd
├── addons/
│   ├── godot_rl_agents/   (placeholder, populated in Stage 3)
│   └── grpc_gd/           (deferred — see README inside)
└── assets/
    ├── icons/             (placeholder)
    └── shaders/           (placeholder)
```

## What Stage 2 does **not** do

* Render the kenney_scifi prop pack — the arena is intentionally art-
  light. Stage 7 (visual review) reopens this.
* Ship a `bronze` opponent policy — that's HW1's deliverable. The red
  chassis sits idle in Stage 2.
* Speak gRPC over HTTP/2 — see
  [`addons/grpc_gd/README.md`](addons/grpc_gd/README.md).
* Build the binaries on CI — Godot headless exports require the
  `export_templates` package, which we don't install on
  `ubuntu-latest`. The team builds locally and pushes to OSS.
