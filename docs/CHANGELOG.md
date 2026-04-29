# Changelog

Conventional Commits + per-stage tags. This file tracks the reverse
chronological view of what landed when, separately from the live
implementation plan in `IMPLEMENTATION_PLAN.md`.

## v0.6-arena-poc — Stage 2 (2026-04-29)

First runnable simulator. Stand up `shared/godot_arena/` as a Godot 4
project: arena scene with two chassis (blue + red), mecanum-flavoured
chassis kinematics, two-axis gimbal with first-order motor lag,
quadratic-drag projectile physics, and a four-plate armor ring per
chassis with collision-driven damage tracking.

* **godot:** `shared/godot_arena/` is a fully-authored Godot 4.3 project
  — `project.godot`, `default_env.tres`, `export_presets.cfg` for
  Linux/Windows/macOS, six `.tscn` scenes (arena_main, chassis, gimbal,
  armor_plate, projectile, replay_hud), and nine GDScript files
  implementing the physics, RPC server, frame publisher, and replay
  recorder. Geometry uses Godot primitives (no external art); the
  visual pass is deferred to Stage 7 per the implementation plan.
* **transport:** Stage-2 risk mitigation honoured. The arena exposes a
  length-prefixed JSON-over-TCP control surface on port 7654 and a
  raw RGB888 frame stream on port 7655 (16-byte header + payload, same
  layout as `shared/zmq_frame_pub`). gRPC inside Godot via GDExtension
  is deferred — see [`shared/godot_arena/addons/grpc_gd/README.md`](
  ../shared/godot_arena/addons/grpc_gd/README.md). The proto schema is
  unchanged; JSON field names match proto3 exactly so dicts round-trip
  through `google.protobuf.json_format`.
* **wire test:** `tests/test_godot_wire_format.py` builds the same dict
  shapes the GDScript emits and parses each through
  `json_format.ParseDict` into the matching `_pb2` message. 9 test
  cases: every request, every response, framing-prefix sanity. The
  `grpc_stub_server` Stage-1 server keeps speaking real gRPC — both
  arenas implement the same logical contract.
* **manifest:** three placeholder rows in `shared/assets/manifest.toml`
  for the Godot binaries (`godot-arena-{linux,macos,windows}-*`) under
  `oss://tsingyun-aiming-hw-public/assets/godot/0.6.0/`. SHA-256
  digests are zero-stubs until the team performs the first headless
  export + push.
* **smoke client:** `tools/scripts/smoke_godot_arena.py` connects to a
  running headless arena, drives a 4-step episode (reset → step → fire
  → finish), and parses each reply through the proto types as a live
  conformance check.
* **docs:** `docs/godot_arena.md` covers the architecture and the
  transport-fallback rationale. The Godot project's own README
  documents the run / headless-export workflow.

Out of scope for v0.6 and explicit in `docs/godot_arena.md`:
`kenney_scifi` art, plate-icon SVGs, the `grpc_gd` GDExtension, the
vendored `godot_rl_agents` addon (placeholder only), the `bronze`
opponent policy on the red chassis (Stage 3 deliverable), and CI-side
headless export builds (the team builds locally; export templates are
out of `ubuntu-latest`'s comfort zone).

## v0.5-infra — Stage 1 (2026-04-29)

Shared infrastructure baseline.

* **proto:** `aiming.proto` (services), `sensor.proto` (SensorBundle &
  friends), `episode.proto` (EpisodeStats & ProjectileEvent). Tag
  numbers are stable; bumping any to a wire-incompatible value is a
  schema violation.
* **cmake:** top-level `CMakeLists.txt` plus `shared/cmake/{ProtoTargets,
  UvFetch}.cmake`. C++20, `find_package`-based deps with vcpkg as the
  fallback toolchain.
* **python (uv workspace):** root `pyproject.toml` plus two member
  packages: `grpc_stub_server` (synthetic AimingArena server) and
  `zmq_frame_pub` (synthetic 720p RGB stream). Lazy proto codegen
  inside `grpc_stub_server`.
* **assets:** `shared/assets/manifest.toml` schema with one
  `sentinel-public` smoke entry. `fetch_assets.py` resolves anonymous
  + private (env-var-auth) blobs from OSS in `cn-beijing`.
  `push_assets.py` is the team-side uploader.
* **docker:** multi-arch `toolchain.Dockerfile` (linux/amd64 +
  linux/arm64), pushed to
  `oss://tsingyun-aiming-hw-cache/docker/toolchain/0.5.0/`.
* **tests:** GTest-based proto round-trip; pytest fetch_assets coverage
  with optional minio backend.
* **docs:** README, this CHANGELOG, `architecture.md`, `oss_assets.md`.

## v0.4-plan — schema/plan v0.4 (2026-04-29)

* Defer grading workflow and leaderboard policy until HW scaffolds
  exist. Schema §7 became a stub; per-HW "Eval:" lines reframed as
  "Performance signal:". IMPLEMENTATION_PLAN Stage 10 marked
  superseded pending §7 redesign.

## v0.3-plan — schema/plan v0.3 (2026-04-29)

* Folded the second round of decisions: cn-beijing OSS region,
  candidate-facing repo path locked to
  `www-David-dotcom/TsingYun_Tutorial_Vision-Aiming`, models bucket
  uses SSE-OSS, GH-hosted runners allowed for non-grading. Later
  walked back the candidate-side-grading decision in v0.4.

## v0.2-schema — schema baseline (2026-04-29)

* Initial schema and IMPLEMENTATION_PLAN; six v0.1 questions
  resolved (engine gate, HW7-bonus, READMEs language, etc.).
