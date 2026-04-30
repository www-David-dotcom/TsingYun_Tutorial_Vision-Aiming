# Aiming Arena — Unity 6 LTS HDRP project

Stage 12 reform of `shared/godot_arena/`. Implements the simulator side
of the contract from `shared/proto/aiming.proto` on Unity 6 LTS +
HDRP, with a coordinated visual reform (multi-tier maze hybrid map,
stylized chassis, neon palette, holographic UI). Companion to the
spec at [`docs/superpowers/specs/2026-04-30-arena-art-vision-reform-design.md`](../../docs/superpowers/specs/2026-04-30-arena-art-vision-reform-design.md).

## Quickstart (team)

```bash
# 1. Install Unity 6 LTS via Unity Hub (project pinned at 6000.3.14f1).
# 2. Open the project from Unity Hub: Open → <repo>/shared/unity_arena
# 3. Open Assets/Scenes/ArenaMain.unity and click Play.
# 4. In another terminal, drive the smoke:
uv run python tools/scripts/smoke_arena.py --engine unity --seed 42 --ticks 10
```

## Wire layout (identical to Godot)

| Port | Role | Encoding |
|------|------|----------|
| 7654 | Control RPC | length-prefixed (4-byte BE) JSON |
| 7655 | Frame stream | 16-byte LE header (`<QQ frame_id stamp_ns>`) + RGB888 |

JSON field names match `shared/proto/*.proto` exactly. The wire-format
conformance test (`tests/test_arena_wire_format.py`) is parametrized
over `[godot, unity]` and asserts both engines emit the same dict
shapes.

## Synty pack

The maze + chassis art uses the Synty POLYGON Sci-Fi pack (paid,
~$40 USD). Maintainer downloads from synty.com once and drops the
`.unitypackage` (or extracted `.fbx` tree) into
`Assets/Synty/POLYGON_SciFi/`. This directory is gitignored —
**never commit Synty source files**. A CI guard
(`tools/scripts/check_synty_redistribution.py`, added in 12d) fails
the build if any `.fbx` is found in committed paths.

## Build (12d)

Builds run via `tools/unity/build.sh --target {win-showcase,
macos-showcase, linux-showcase, linux-headless}`, output to
`shared/unity_arena/builds/`. Locally validate against the Tier 1–5
regression suite before pushing to OSS.

## Tests

EditMode tests (Unity Test Runner): `Assets/Tests/EditMode/`
- `SeedRngTests`, `MecanumChassisControllerTests`, `GimbalKinematicsTests`,
  `ProjectileDragTests`, `ArmorPlateTests`, `ReplayRecorderTests`

PlayMode tests: `Assets/Tests/PlayMode/`
- `TcpProtoServerTests`, `TcpFramePubTests`, `ArenaMainEpisodeTests`

Python wire conformance (no Unity needed): `tests/test_arena_wire_format.py`

## Project layout

```
shared/unity_arena/
├── Assets/
│   ├── Scenes/      ArenaMain.unity (placeholder), MapA_MazeHybrid.unity (12b)
│   ├── Scripts/     10 C# files (port from GDScript + new MecanumChassisController)
│   ├── Prefabs/     Chassis, Gimbal, ArmorPlate, Projectile, HoloProjector (12b)
│   ├── Materials/   PBR materials (12c)
│   ├── Shaders/     Shader Graph (12c)
│   ├── VFX/         VFX Graph (12c)
│   ├── Settings/    HDRPAsset_Showcase / _Headless, Volume profiles (12c)
│   ├── UI/          HUD prefabs (12c)
│   ├── Tests/       EditMode + PlayMode
│   └── Synty/       gitignored
├── Packages/manifest.json
├── ProjectSettings/
└── README.md
```

## Headless rendering caveat

Unity HDRP cannot render without a GPU. The `linux-headless` build target
disables DXR, drops to baked GI only, and uses low-LOD geometry; it still
needs any GPU (Intel UHD / Apple Silicon / NVIDIA / AMD).
`ubuntu-latest` GitHub-hosted runners have no GPU, so live-arena
episodes never run in CI; CI only runs unit tests
(`test_arena_wire_format.py`, `test_fetch_assets.py`, etc.).
Live arena testing happens on the maintainer's GPU-equipped box.
