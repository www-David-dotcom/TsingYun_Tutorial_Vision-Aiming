# Changelog

Conventional Commits + per-stage tags. This file tracks the reverse
chronological view of what landed when, separately from the live
implementation plan in `IMPLEMENTATION_PLAN.md`.

## v0.5-infra — Stage 1 (in-progress)

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
