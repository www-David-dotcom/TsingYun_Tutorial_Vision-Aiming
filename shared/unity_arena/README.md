# Aiming Arena — Unity 6 LTS HDRP project

Unity is the only active arena runtime. This project implements the game
specified by [`schema.md`](../../schema.md): RoboMaster-inspired vehicles,
armor plates with MNIST number tags, gimbal-mounted camera and barrel,
team-based match rules, and a polished sci-fi maze environment.

## Quickstart (team)

```bash
# 1. Install Unity 6 LTS via Unity Hub (project pinned at 6000.3.14f1).
# 2. Open the project from Unity Hub: Open → <repo>/shared/unity_arena
# 3. Open Assets/Scenes/MapA_MazeHybrid.unity and click Play.
# 4. In another terminal, drive the smoke:
UV_CACHE_DIR=.uv-cache uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 10
```

## Runtime Wire Layout

| Port | Role | Encoding |
|------|------|----------|
| 7654 | Control RPC | length-prefixed (4-byte BE) JSON |
| 7655 | Frame stream | 16-byte LE header (`<QQ frame_id stamp_ns>`) + RGB888 |

JSON field names match `shared/proto/*.proto` exactly. The wire-format
conformance test (`tests/test_arena_wire_format.py`) validates the Unity
payload shape without launching the Editor. Candidate runners should treat
[`docs/unity-wire-contract.md`](../../docs/unity-wire-contract.md) as the
stable control/frame contract.

## Synty pack

The maze + chassis art uses the Synty POLYGON Sci-Fi pack (paid,
~$40 USD). Maintainer downloads from synty.com once and drops the
`.unitypackage` (or extracted `.fbx` tree) into
`Assets/Synty/POLYGON_SciFi/`. This directory is gitignored —
**never commit Synty source files**. A CI guard
(`tools/scripts/check_synty_redistribution.py`, added in 12d) fails
the build if any `.fbx` is found in committed paths.

## Build (12d)

Release build automation is not part of the current cleanup priority. Local
Editor Play mode is the supported runtime path until the game rules and visual
design are repaired against `schema.md`.

## Tests

EditMode tests (Unity Test Runner): `Assets/Tests/EditMode/`
- `SeedRngTests`, `MecanumChassisControllerTests`, `GimbalKinematicsTests`,
  `ProjectileDragTests`, `ArmorPlateTests`, `ReplayRecorderTests`

PlayMode tests: `Assets/Tests/PlayMode/`
- `TcpProtoServerTests`, `TcpFramePubTests`, `ArenaMainEpisodeTests`

Python wire conformance (no Unity needed): `tests/test_arena_wire_format.py`
Local Unity smoke: `tools/scripts/smoke_arena.py`

## Project layout

```
shared/unity_arena/
├── Assets/
│   ├── Scenes/      ArenaMain.unity, MapA_MazeHybrid.unity
│   ├── Scripts/     gameplay, wire protocol, replay, and frame streaming
│   ├── Prefabs/     Chassis, Gimbal, ArmorPlate, Projectile, HoloProjector
│   ├── Materials/   current Unity materials
│   ├── Rendering/   HDRP asset
│   ├── Tests/       EditMode + PlayMode
│   └── Synty/       gitignored
├── Packages/manifest.json
├── ProjectSettings/
└── README.md
```

## Rendering Caveat

Unity HDRP cannot render without a GPU. The `linux-headless` build target
disables DXR, drops to baked GI only, and uses low-LOD geometry; it still
needs any GPU (Intel UHD / Apple Silicon / NVIDIA / AMD).
Live arena testing happens locally with Unity Editor or a future Unity build.
