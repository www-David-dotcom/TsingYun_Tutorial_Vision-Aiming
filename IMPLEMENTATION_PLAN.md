# Aiming_HW — Per-Stage Implementation Plan

> Plan version 0.5 — Stage 10 designed (Proposal A: auto-graded public tests + honour system + manual interview of top-N). All `[grading — deferred]` decisions from v0.4 are resolved; the heavier alternatives (hidden tests, simulator-driven episodes, build-artefact hash binding) are recorded as future-cycle targets in `docs/grading.md`.
> Companion to [`schema.md`](schema.md) v0.5. Stops at the level of "what files land in each commit, and how do I know the stage is done." Once approved, each stage is implemented on a short-lived branch, reviewed, fast-forward-merged into `main`, and tagged.

---

## Resolved decisions

(All folded into the relevant sections below. Listed here for review and to make load-bearing assumptions easy to find.)

1. **`godot_rl_agents`** is **vendored** at a pinned commit under `shared/godot_arena/addons/godot_rl_agents/`. We do not fork unless and until we need a patch upstream won't accept.
2. **Asset blobs live on Aliyun OSS in `cn-beijing`** (华北 2 / Beijing). Three buckets exist: `tsingyun-aiming-hw-public` (anonymous-read, candidate-facing static assets), `tsingyun-aiming-hw-models` (private, SSE-OSS, holds opponent policies + reference detector ONNX), `tsingyun-aiming-hw-cache` (private, build/image cache). The repo carries SHA-256 pointers in `shared/assets/manifest.toml` and a `shared/scripts/fetch_assets.py` resolver. We are deliberately **not** using Git LFS (bandwidth pricing, no encryption story).
3. **Grading topology** is **GitHub-hosted CI on `ubuntu-latest`** for the auto-grade pass, plus manual review of the top-N PRs by the team. No candidate-side grader CLI in v1; no team Orin NX in v1. See `docs/grading.md` for the candidate-facing handbook.
4. **The candidate-facing repo is [`www-David-dotcom/TsingYun_Tutorial_Vision-Aiming`](https://github.com/www-David-dotcom/TsingYun_Tutorial_Vision-Aiming).** Candidates fork it, work in their fork, and open a PR back to it for submission. Stage 1+ work lands in this repo's `main` branch.
5. **Models-bucket access pattern** stays as **direct team RAM credentials** for v1 (the candidate's CI never reads the private bucket — only the team does, when manually reviewing top-N PRs). The shared AccessKey + RAM-role surface is recorded as a v2 upgrade in `docs/grading.md` § "future cycles."
6. **Anti-cheat posture** is **honour system, validated by CI**. The score is derived from CI re-running every test on `pull_request.head.sha`; candidates can't edit the `submission_score.json` artefact, and historical commits don't leak in. Suspected cheating is handled at interview time by re-running the candidate's frozen commit on a maintainer's laptop. No signed score JSONs in v1.
7. **GH-hosted runner usage scope** is **the entire grading surface**. `validate_submission.yml` runs the full ctest + pytest matrix on every PR; `regenerate_leaderboard.yml` runs daily as a scheduled cron. ~25 minutes runner time per PR + ~3 minutes for the daily aggregator.
8. **All scheduled work is pinned to `Asia/Shanghai`.** Cron expressions in workflow YAML are documented as Beijing-local; the leaderboard regen wakes at 19:17 Beijing-local (`17 11 * * *` UTC).

Stage 10 v1 is the implementation of decisions 3, 5, 6, 7. The earlier "candidate-side grader CLI + signed score JSONs + build-artefact hash binding" sketch is preserved in `docs/grading.md` § "future cycles" as the v2 upgrade path; it didn't make v1 because Proposal A came in cheaper and is sufficient for a 50-candidate cycle.

---

## 0. Conventions

### Branching
* Trunk: `main`. Always shippable; tagged at the end of every stage.
* Feature branches: `stageN/<slug>` (e.g. `stage3/hw1-detector`). One PR per branch.
* **No binaries in git.** Model weights, datasets, Godot exports, and replays all live on OSS (per resolved decision 2). The repo carries `shared/assets/manifest.toml` with `{path, sha256, bucket, key, size}` rows and a `fetch_assets.py` resolver. Build artefacts and caches remain `.gitignore`-d and are produced by `uv sync` / `cmake --build`.

### Commits
* Conventional Commits: `feat(<scope>): ...`, `fix:`, `docs:`, `test:`, `chore:`, `refactor:`, `build:`, `ci:`.
* The first commit on a stage branch is `chore(stageN): branch open`. The last is `chore(stageN): close stage`.
* Schema changes are atomic: any commit that touches `schema.md` bumps line 3's "Plan version" and uses `docs(schema):` prefix.

### Tags & Releases
* Tags use SemVer-ish: `v0.X-<slug>` for in-progress milestones, `v1.0`+ for the first launchable build.
* Each tag is annotated (`git tag -a`) with a one-paragraph message summarizing the stage outcome.

### Definition of Done (every stage)
1. All listed files exist and pass `clang-format` / `ruff` / `mypy` (whichever applies).
2. `cmake --build build && ctest` passes on Linux x86_64 in the grader Docker image.
3. `uv run pytest` passes for any Python module added.
4. Stage-specific smoke command runs to completion (each stage names its own).
5. The stage's `README.md` (if it adds an HW folder) exists and follows the schema's HW-README contract.
6. CHANGELOG entry added to `docs/CHANGELOG.md`.
7. PR reviewed by at least one other team lead, then merged via fast-forward.

### LOC budgets
LOC ranges are guidance for sizing review effort, not hard caps. Tests count toward the budget. Numbers are **after** removing generated code and vendored third-party.

---

## Stage 1 — Shared Infrastructure (M0a)

* **Branch**: `stage1/shared-infra`
* **End tag**: `v0.3-infra`
* **Maps to schema**: §3 (System architecture), §6 (Toolchain), Appendix A (Layout)
* **Calendar estimate**: 5–7 working days (1 engineer)

### Goals
1. Lay down the directory skeleton from Appendix A.
2. Define the gRPC/Protobuf contract (`shared/proto/aiming.proto`).
3. Establish reproducible Python (`uv`) and C++ (`cmake` + vcpkg) build pipelines.
4. Ship a runnable gRPC echo + ZMQ frame stream so HW1 onward have a stable simulator-side stub to point at while the real Godot scene is being built in parallel.
5. Build the grader Docker image once on the maintainer's workstation and tag it `aiming-hw-grader:0.1`. The image is then **pushed to `tsingyun-aiming-hw-cache/docker/grader/0.1/`** so each candidate's machine pulls it via `tools/grader/pull_image.sh` (using the shared read-only AccessKey). The image is never rebuilt on a candidate's machine and never on a "team grading box" (no such box exists per resolved decision 3). Reproducibility: digest pinned in `docs/CHANGELOG.md` and in `tools/grader/image.lock`.
6. Verify the three OSS buckets in `cn-beijing` (already provisioned by the team), wire `shared/scripts/fetch_assets.py` to the two access patterns (anonymous for `…-public`, AccessKey-auth from `shared/oss/reader_credentials.json` for `…-models`), and seed `shared/assets/manifest.toml` with the empty schema so Stage 2+ can append rows. Provision the RAM user `aiming-hw-public-reader` with GetObject-only on the models bucket and commit its AccessKey JSON.

### Files to create
```
Aiming_HW/
├── CMakeLists.txt                       # top-level, uses FetchContent for uvtarget
├── pyproject.toml                       # uv-managed; minimal deps (grpcio-tools, pyzmq, pytest)
├── uv.lock                              # generated by `uv sync`
├── README.md                            # candidate-facing entry doc (skeleton only)
├── docs/
│   ├── CHANGELOG.md
│   ├── architecture.md                  # rendered from schema §3 with figures
│   └── oss_assets.md                    # where blobs live, how fetch_assets.py works, OSS endpoint config
├── shared/
│   ├── cmake/
│   │   ├── UvFetch.cmake
│   │   ├── ProtoTargets.cmake           # helper to add a proto with C++ + Python codegen
│   │   └── GraderImage.cmake
│   ├── proto/
│   │   ├── aiming.proto                 # EnvReset / EnvStep / EnvPushFire / Episode RPCs
│   │   ├── sensor.proto                 # SensorBundle, IMU, GimbalState, FrameRef
│   │   └── score.proto                  # ScoreBundle, EpisodeStats
│   ├── assets/
│   │   └── manifest.toml                # rows: name, oss region+bucket+key, sha256, size, visibility (public|reader-key)
│   ├── oss/
│   │   ├── reader_credentials.json      # shared RAM AccessKey for the models bucket; rotatable
│   │   └── README.md                    # how to rotate the key, per-key audit log access
│   ├── scripts/
│   │   ├── fetch_assets.py              # resolves manifest entries, downloads to out/assets/
│   │   └── push_assets.py               # team-only; uploads new blobs and rewrites manifest
│   ├── grpc_stub_server/                # Python-only echo server, used in lieu of Godot until Stage 2
│   │   ├── pyproject.toml
│   │   └── src/grpc_stub_server/__init__.py
│   ├── zmq_frame_pub/                   # writes a synthetic 720p RGB feed at 60 fps
│   │   └── src/zmq_frame_pub/main.py
│   └── docker/
│       ├── grader.Dockerfile            # built once on the maintainer's workstation, pushed to tsingyun-aiming-hw-cache/docker/grader/, pulled by every candidate
│       ├── grader.compose.yaml
│       └── README.md
├── tests/
│   ├── proto_roundtrip_test.cpp         # serialize/deserialize sanity for every message
│   └── test_fetch_assets.py             # offline fetch test using a local OSS-emulator (minio)
└── .clang-format                        # copied from RM_Vision_Aiming
```

### LOC budget
~ 700 lines (proto: ~150, CMake: ~200, Python stubs: ~250, tests: ~100).

### Smoke check
```bash
uv sync
docker compose -f shared/docker/grader.compose.yaml up -d
uv run python -m grpc_stub_server &
uv run python -m zmq_frame_pub &
uv run python -m pytest tests/
```
Expected: stub server logs `EnvStep` calls; ZMQ subscriber prints frame numbers 0..N; all tests green.

### Acceptance criteria
* `aiming.proto` round-trips: every message can be encoded and decoded in both Python and C++.
* `cmake --preset linux-debug && cmake --build --preset linux-debug` succeeds on the maintainer's Linux x86_64 workstation and inside the grader Docker image (the canonical build environment for candidates).
* `docker build -t aiming-hw-grader:0.1 -f shared/docker/grader.Dockerfile .` succeeds on the maintainer's workstation; image is reproducible (digest pinned in `docs/CHANGELOG.md` and `tools/grader/image.lock`). Pushed to `oss://tsingyun-aiming-hw-cache/docker/grader/0.1/` and verified pullable from a clean machine via `tools/grader/pull_image.sh`.
* `uv run python shared/scripts/fetch_assets.py --dry-run` parses `manifest.toml`. `uv run python shared/scripts/fetch_assets.py --only sentinel-public` downloads a 1 KB sentinel from the public bucket *anonymously* (no creds set). With `shared/oss/reader_credentials.json` populated, `--only sentinel-models` succeeds against the private bucket and verifies the sha256.
* `pytest tests/test_fetch_assets.py` passes against a local minio container (no real OSS creds needed in CI/dev).
* `clang-format --dry-run` and `ruff check` both clean.
* GitHub-hosted CI workflow `lint_and_build.yml` passes on `ubuntu-latest` (~3 min wall-clock).

### Risks
* **gRPC C++ build pain on macOS** — mitigate by pinning gRPC via vcpkg; document the WSL2 escape hatch in `docker/README.md`.
* **Protobuf field churn later** — mark every field with stable numbering and add a `proto_compat_test` that pins the FileDescriptorProto hash so accidental wire-incompatible edits get caught in CI.

### Out of scope (deferred)
Real Godot client (Stage 2), any HW-specific code (Stages 3–9), GitHub Actions workflows (Stage 10).

---

## Stage 2 — Godot Arena PoC (M0b)

* **Branch**: `stage2/godot-arena`
* **End tag**: `v0.4-arena-poc`
* **Maps to schema**: §1 (Scenario), §2 (Engine selection), §3 (Architecture)
* **Calendar estimate**: 10–14 working days (1 engine engineer + part-time art)

### Goals
1. Stand up `shared/godot_arena/` as a Godot 4 project with one map.
2. Implement chassis (CharacterBody3D, mecanum-flavoured kinematics) and an independent yaw/pitch gimbal.
3. Spawn 4 armor plates per robot with collision shapes, glowing materials, and an icon classification field.
4. Implement projectile physics with drag + gravity, plus a damage-on-armor-hit handler.
5. Connect the arena to the Stage 1 proto contract: implement `EnvReset / EnvStep / EnvPushFire` server-side in GDScript (or C++ via GDExtension if necessary).
6. Add a replay recorder that writes both a JSON event stream and an MP4 (via Godot's built-in movie maker mode).
7. Provide a headless export pipeline that produces Win/macOS/Linux executables under `shared/godot_arena/builds/`.

### Files to create
```
shared/godot_arena/
├── project.godot
├── default_env.tres
├── scenes/
│   ├── arena_main.tscn
│   ├── chassis.tscn
│   ├── gimbal.tscn
│   ├── armor_plate.tscn
│   ├── projectile.tscn
│   └── ui/replay_hud.tscn
├── scripts/
│   ├── arena_main.gd                    # orchestrates EnvReset/Step
│   ├── chassis.gd
│   ├── gimbal.gd
│   ├── armor_plate.gd
│   ├── projectile.gd
│   ├── grpc_server.gd                   # GDExtension-backed gRPC server
│   ├── zmq_frame_pub.gd
│   ├── replay_recorder.gd
│   └── seed_rng.gd
├── addons/
│   ├── godot_rl_agents/                 # vendored from upstream, pinned commit
│   └── grpc_gd/                         # our own gRPC GDExtension wrapper
├── assets/
│   ├── kenney_scifi/                    # CC0 prop pack
│   ├── shaders/                         # muzzle flash, armor glow, impact decals
│   └── icons/                           # Hero/Engineer/Standard/Sentry SVGs
├── export_presets.cfg                   # Linux x86_64 server, Win, macOS
└── README.md                            # how to run, how to edit
```

### LOC budget
~ 2200 lines of GDScript + ~300 lines of GDExtension C++ for the gRPC bridge. Vendored addons not counted.

### Smoke check
```bash
shared/godot_arena/builds/aiming_arena_linux.x86_64 --headless --port 7654 &
uv run python -m grader.smoke_two_step
```
Expected: chassis spawns at origin, gimbal slews 30° in 0.5 s in response to a `GimbalCmd`, projectile fired by `EnvPushFire` arrives at the target plate, score JSON reports 1 hit.

### Acceptance criteria
* Headless export builds for all three OS targets in CI on tag.
* A `gold-bot-stub` policy (random actions) can complete a 90-second episode without crashing.
* Frame stream sustains 60 Hz for 5 minutes on the grader runner.
* Episode JSON conforms to `score.proto`.

### Risks
* **gRPC inside Godot** — gRPC support in GDExtension is immature. Mitigation: if it slips by >3 days, drop to **plain TCP + protobuf framing** (length-prefixed frames). The proto contract is unchanged; only the transport is.
* **Frame throughput** — at 720p × 60 fps × RGB raw the bandwidth is ~190 MB/s. Mitigation: ZMQ over `ipc://` on Linux/macOS (already designed); on Windows fall back to memory-mapped files.

### Out of scope
Lighting/VFX polish (deferred to Stage 7's visual pass), additional maps (Stage 10), HW-specific opponent bots (Stage 3+).

---

## Stage 3 — HW1: Lightweight armor & icon detector (M1a)

* **Branch**: `stage3/hw1-detector`
* **End tag**: `v0.5-hw1-detector`
* **Maps to schema**: §5 HW1
* **Calendar estimate**: 7–9 working days (1 vision lead, 0.5 RL lead in parallel for bronze bot)

### Goals
1. Provide candidates with a working dataset generator that drives the Godot arena in "label dump" mode.
2. Provide a MobileNetV3-Small + multi-task head training script with `TODO`-marked loss formulation.
3. Ship a verified ONNX export pipeline (no `TODO`s here — this is plumbing the candidate doesn't write).
4. Provide a C++ ONNX-Runtime inferer skeleton with `TODO`-marked decode + NMS.
5. Train + commit pointer to the `bronze` opponent policy `.pt` file. (Weights are hosted out-of-tree; the repo carries a SHA-256 hash and a download URL.)
6. Write public unit tests (synthetic 5-frame mAP) and hidden grading episodes.

### Files to create
```
HW1_armor_detector/
├── README.md                            # bilingual per §10 decision 5
├── pyproject.toml                       # depends on torch, torchvision, onnx, onnxruntime
├── CMakeLists.txt
├── proto -> ../shared/proto             # symlink
├── data/
│   ├── dataset_dumper.py                # drives Godot in label-dump mode
│   ├── domain_randomization.yaml
│   └── real_holdout/                    # 100 real RoboMaster frames + labels (fetched from OSS public bucket on demand)
├── src/
│   ├── train.py                         # TODO holes: loss_box / loss_kpt / loss_cls / mixup
│   ├── model.py                         # MobileNetV3-Small backbone (filled), head (filled)
│   ├── export_onnx.py                   # filled
│   └── losses.py                        # provided utilities (giou, focal); the TODOs *call* these
├── include/aiming_hw/detector/
│   ├── inferer.hpp
│   └── post_process.hpp                 # decode + NMS interface
├── source/
│   ├── inferer.cpp                      # ONNX Runtime session set-up filled; decode TODO
│   └── post_process.cpp                 # NMS TODO
├── tests/
│   ├── public/
│   │   ├── test_loss_shapes.py
│   │   ├── test_export_roundtrip.py
│   │   └── test_post_process.cpp
│   └── private/                         # not shipped to candidate repo (lives in grader image)
│       ├── grader_run.py
│       └── seeds.txt
└── grader/
    └── grader.py                        # invoked by GH Actions; not visible to candidate
```

### LOC budget
~ 1400 LOC (Python: 700, C++: 500, tests: 200).

### Smoke check
```bash
cd HW1_armor_detector
uv run python data/dataset_dumper.py --frames 200 --out /tmp/ds
uv run python src/train.py --epochs 1 --data /tmp/ds
uv run python src/export_onnx.py --weights /tmp/last.pt --out /tmp/model.onnx
cmake --build ../build --target hw1_inferer_smoke
../build/HW1_armor_detector/hw1_inferer_smoke --model /tmp/model.onnx --frame /tmp/ds/000000.png
```
Expected: end-to-end pipeline produces a non-empty bbox + 4-corner prediction on a known frame.

### Acceptance criteria
* `train.py` loss_total decreases monotonically over 5 epochs on a 200-frame dataset (smoke level — not a quality bar).
* ONNX file passes `onnx.checker.check_model` and shape-inference.
* `bronze` policy sustains a 90-second match against a "stationary do-nothing" candidate without crashing.
* Public unit tests demonstrate the candidate's expected interface; private tests reproduce mAP on hidden split.

### Risks
* **Synthetic-to-real gap** — partially mitigated by domain randomization; we add a real-image holdout in `data/real_holdout/`. If dom-rand fails to close the gap, allow candidates to optionally augment with the [Roboflow Plates dataset](https://universe.roboflow.com/robomaster-vip/plates-ythwt).
* **ONNX-Runtime CUDA on the grader** — keep the CPU EP as the canonical grading target; CUDA is bonus. Latency budget set with CPU in mind.

### Out of scope
TensorRT engine generation (production team's concern, not the assignment's), distillation, quantization (could become a future bonus).

---

## Stage 4 — HW2: TF graph (M1b)

* **Branch**: `stage4/hw2-tf`
* **End tag**: `v0.6-hw2-tf`
* **Maps to schema**: §5 HW2
* **Calendar estimate**: 3–4 working days (1 engineer)

### Goals
1. Implement a header-only `tf::Buffer` interface with timestamped transform storage.
2. Provide a unit-test fixture that replays a logged trajectory and checks RMSE in world coordinates.
3. Hand the candidate two `TODO`s: `slerp_interpolate` and `compose`.

### Files to create
```
HW2_tf_graph/
├── README.md
├── CMakeLists.txt
├── include/aiming_hw/tf/
│   ├── buffer.hpp                       # data structures provided
│   ├── transform.hpp                    # Affine3d / Quaterniond aliases, helpers
│   └── interpolate.hpp                  # TODO: slerp + nlerp fallback
├── source/
│   ├── buffer.cpp
│   └── interpolate.cpp                  # TODO target
├── tests/
│   ├── public/
│   │   ├── test_basic_lookup.cpp
│   │   ├── test_interpolation_corners.cpp     # singular axis cases
│   │   └── test_chain_compose.cpp
│   ├── private/
│   │   └── test_replay_rmse.cpp
│   └── fixtures/
│       └── replay_60s.bag               # serialized GimbalState + ChassisOdom dump
└── grader/
    └── grader.py
```

### LOC budget
~ 600 LOC (header impls: 250, tests: 250, fixtures + grader: 100).

### Smoke check
```bash
ctest -R hw2_basic_lookup
ctest -R hw2_chain_compose
```
Both green is the bar.

### Acceptance criteria
* All public tests pass on the reference solution committed under `grader/private/reference_impl/`.
* Replay RMSE ≤ 5 mm in position, ≤ 0.1° in attitude on the fixture.
* Header builds clean with `-Wall -Wextra -Werror`.

### Risks
* **Quaternion sign-flip near antipodes** — the reference impl handles short-arc; we add a corner test that hits dot ≈ -1.

### Out of scope
ROS2 TF2 integration (deliberately out — assignment is to *write* the math).

---

## Stage 5 — HW3: EKF tracker (M2a)

* **Branch**: `stage5/hw3-ekf`
* **End tag**: `v0.7-hw3-ekf`
* **Maps to schema**: §5 HW3
* **Calendar estimate**: 7–10 working days (1 estimation lead, 0.5 RL lead training silver in parallel)

### Goals
1. Provide a Python **reference EKF** under `grader/private/reference_impl/ekf_python.py` so that hidden tests can compare numerics.
2. Provide a header-only `ekf::Tracker` C++ skeleton using Eigen with `TODO`s for `predict`, `update`, IMM `mix/blend`, and Hungarian assignment for multi-target.
3. Generate three test fixture trajectories: low-maneuver, medium, high (constant turn at 4 rad/s with 5° measurement noise).
4. Document the math in `docs/ekf_derivation.md` with LaTeX (rendered as PNGs for offline reading).
5. Train + commit pointer to the `silver` opponent policy.

### Files to create
```
HW3_ekf_tracker/
├── README.md
├── CMakeLists.txt
├── include/aiming_hw/ekf/
│   ├── tracker.hpp                      # state, public API
│   ├── kalman_step.hpp                  # TODO: predict/update
│   ├── imm.hpp                          # TODO: mode mixing
│   └── data_association.hpp             # TODO: gating + Hungarian
├── source/
│   ├── tracker.cpp
│   ├── kalman_step.cpp
│   ├── imm.cpp
│   └── data_association.cpp
├── tests/
│   ├── public/
│   │   ├── test_cv_predict.cpp
│   │   ├── test_imm_mode_probabilities.cpp
│   │   └── test_da_simple.cpp
│   ├── private/
│   │   ├── test_nees_coverage.cpp       # 95% NEES bounds
│   │   ├── test_high_maneuver_rmse.cpp
│   │   └── reference_impl/ekf_python.py
│   └── fixtures/
│       ├── traj_low_maneuver.csv
│       ├── traj_med_maneuver.csv
│       └── traj_high_maneuver.csv
├── docs/
│   ├── ekf_derivation.md
│   └── figures/
│       ├── ekf_block_diagram.png
│       ├── imm_mixing.png
│       └── nees_chi2_envelope.png
└── grader/
    └── grader.py
```

### LOC budget
~ 1500 LOC (C++: 700, tests: 400, Python ref: 200, docs/fixtures: 200).

### Smoke check
```bash
ctest -R hw3_cv_predict
ctest -R hw3_imm_mode
uv run python tests/private/reference_impl/ekf_python.py --plot
```
Expected: candidate's CV predict matches Python reference within 1e-9; IMM mode probability sums to 1; reference plot generated for documentation.

### Acceptance criteria
* NEES of the candidate solution lies within the 95% chi-squared envelope on `traj_med_maneuver` for ≥ 90% of timesteps.
* End-to-end position RMSE ≤ 0.10 m on med, ≤ 0.25 m on high.
* `silver` bot completes 5 deathmatch episodes without crashing.

### Risks
* **Numerical conditioning** — Joseph form is required for the covariance update; we add a hidden test that checks symmetry of P after 1000 steps.
* **Hungarian assignment correctness on small batches** — bring in a tiny header-only `lap.hpp` (MIT) or write our own; we lean toward writing our own to keep deps minimal.

### Out of scope
UKF / particle filter / GP-UKF (could be future bonus).

---

## Stage 6 — HW4: Ballistic + firing-delay (M2b)

* **Branch**: `stage6/hw4-ballistic`
* **End tag**: `v0.8-hw4-ballistic`
* **Maps to schema**: §5 HW4
* **Calendar estimate**: 5 working days (1 engineer)

### Goals
1. Provide a `BallisticSolver` C++ class with `TODO`s for the flight-time solver and the iterative aim-prediction loop.
2. Provide three deterministic harnesses (1D, 2D, 3D-with-drag) so candidates can iterate without spinning Godot.
3. Provide an integration test that pipes ground-truth EKF outputs into the solver and measures hit rate against fixed targets.

### Files to create
```
HW4_ballistic/
├── README.md
├── CMakeLists.txt
├── include/aiming_hw/ballistic/
│   ├── solver.hpp
│   └── projectile_model.hpp
├── source/
│   ├── solver.cpp                       # TODO: flight-time + iterative aim
│   └── projectile_model.cpp             # filled (drag, gravity)
├── tests/
│   ├── public/
│   │   ├── test_1d_no_drag.cpp
│   │   ├── test_2d_with_gravity.cpp
│   │   └── test_3d_with_drag.cpp
│   └── private/
│       └── test_hit_rate_vs_speed.cpp
└── grader/
    └── grader.py
```

### LOC budget
~ 700 LOC (C++: 400, tests: 250, grader: 50).

### Smoke check
```bash
ctest -R hw4_1d_no_drag
ctest -R hw4_2d_with_gravity
```
Expected: closed-form solutions match candidate output to 1e-6 m.

### Acceptance criteria
* Iterative loop converges in ≤ 5 iterations for target speed ≤ 6 m/s.
* Hit rate ≥ 80% at 5 m, ≥ 50% at 10 m, ≥ 20% at 15 m for a constant-velocity target.

### Risks
* **Aliasing of two roots in the gravity solver** — make the test case explicit; the README points candidates at the [PlayTechs blog](http://playtechs.blogspot.com/2007/04/aiming-at-moving-target.html) for the lo/hi-arc choice.

### Out of scope
Spin / Magnus effect, wind, projectile-projectile collisions.

---

## Stage 7 — HW5: MPC gimbal + visual review (M3)

* **Branch**: `stage7/hw5-mpc`
* **End tag**: `v0.9-hw5-mpc`
* **Maps to schema**: §5 HW5
* **Calendar estimate**: 10–12 working days (1 control lead + part-time art)

### Goals
1. Provide a CasADi model template with `TODO` slots for the dynamics with motor-torque lag.
2. Generate the acados solver via the team-managed `acados_template` script and ship the **generated** C library to the grader image (so candidates don't need to install acados locally; they only edit the model + cost).
3. Provide a working **PID baseline** as the floor; the MPC must beat it on the leaderboard tracking metric.
4. Run the **engine quality gate** at this milestone: art and product review the Godot arena in a 30-min meeting; if visuals fail the bar, open `stage7b/unity-port` against the same proto and execute a 3-week port. (Schema §10 decision 1.)

### Files to create
```
HW5_mpc_gimbal/
├── README.md
├── CMakeLists.txt
├── pyproject.toml                       # casadi, acados-template
├── src/
│   ├── model.py                         # TODO: dynamics_with_motor_torque_lag, etc.
│   ├── cost.py                          # filled defaults; weights tunable in YAML
│   ├── generate_acados.py               # produces generated_solver/
│   └── tune.py                          # exposed to candidate for offline weight tuning
├── generated_solver/                    # acados codegen artefacts (vendored, so candidate doesn't need acados locally)
│   ├── acados_solver_aiming_mpc.c/.h
│   ├── acados_sim_solver_aiming_mpc.c/.h
│   └── ...
├── include/aiming_hw/mpc/
│   ├── controller.hpp
│   └── pid_baseline.hpp
├── source/
│   ├── controller.cpp
│   └── pid_baseline.cpp                 # provided baseline
├── tests/
│   ├── public/
│   │   ├── test_step_response.cpp
│   │   └── test_sinusoid_tracking.cpp
│   └── private/
│       └── test_actuator_saturation.cpp
└── grader/
    └── grader.py
docs/
└── visual_review_2026-MM-DD.md           # outcome of the engine gate (Godot vs Unity)
```

### LOC budget
~ 1300 LOC excluding generated_solver/ (~Python: 400, C++: 600, tests: 200, docs: 100).

### Smoke check
```bash
uv run python src/generate_acados.py --check
ctest -R hw5_step_response
```
Expected: solver generates without errors; PID baseline tracks a 1° step within 80 ms; candidate-MPC slot exists and is exercised by the test even with a stub.

### Acceptance criteria
* Solver compile time < 2 minutes on the grader runner.
* PID baseline meets minimum tracking spec (settling time < 200 ms for a 30° step with no overshoot > 5°).
* Engine quality gate decision documented in `docs/visual_review_*.md`. If the decision is "port to Unity," Stage 7b is opened; otherwise Stage 8 starts on schedule.

### Risks
* **acados Windows build flakiness** — already known. Mitigation: only the generated C is shipped to candidates; they never run acados itself. The team builds acados once on the grader image.
* **Engine gate slippage** — has a 3-week port budget reserved; if M3 reveals a "just polish Godot for 1 more week" direction we burn that out of the M4 buffer rather than slipping the launch.

### Out of scope (Stage 7 main; reserved for Stage 7b if triggered)
Unity port: separate branch, separate review, separate tag (`v0.9b-unity-port`). Same proto contract, no candidate-visible API change.

---

## Stage 8 — HW6: Integration runner (M4a)

* **Branch**: `stage8/hw6-integration`
* **End tag**: `v1.0-hw6-integration`
* **Maps to schema**: §5 HW6
* **Calendar estimate**: 5–6 working days (1 engineer)

### Goals
1. Provide an `hw_runner` C++ binary that wires HW1..HW5 together using gRPC + ZMQ to talk to the simulator.
2. Provide a lock-free SPSC ring buffer for camera frames, atomic snapshots for gimbal state, and a watchdog timer.
3. Run the first end-to-end leaderboard episode against `silver`.

### Files to create
```
HW6_integration/
├── README.md
├── CMakeLists.txt
├── include/aiming_hw/pipeline/
│   ├── runner.hpp
│   ├── ring_buffer.hpp                  # SPSC, header-only
│   └── watchdog.hpp
├── source/
│   ├── main.cpp                         # TODO: thread layout
│   ├── runner.cpp                       # TODO: stale-frame drop policy
│   └── watchdog.cpp                     # filled
├── tests/
│   ├── public/
│   │   ├── test_ring_buffer.cpp
│   │   └── test_watchdog.cpp
│   └── private/
│       └── test_e2e_silver.cpp
└── grader/
    └── grader.py                        # runs N=20 episodes, aggregates score
```

### LOC budget
~ 900 LOC.

### Smoke check
```bash
./build/HW6_integration/hw_runner --episode-seed 42 --bot silver
```
Expected: 90-second match completes; score JSON written; replay MP4 in `out/`.

### Acceptance criteria
* p95 control loop latency ≤ 25 ms.
* No data races detected by ThreadSanitizer over 5 episodes.
* End-to-end score JSON conforms to `score.proto`.

### Risks
* **Lock-free ring buffer subtle bugs** — handled by ThreadSanitizer in CI; we also vendor a known-good `boost::lockfree::spsc_queue` header as a comparator.
* **gRPC reconnect storms when sim restarts between seeds** — implement exponential backoff with jitter; cap at 5 retries.

### Out of scope
HW7 strategy logic.

---

## Stage 9 — HW7: Strategy bonus (M4b)

* **Branch**: `stage9/hw7-strategy`
* **End tag**: `v1.1-hw7-strategy`
* **Maps to schema**: §5 HW7
* **Calendar estimate**: 7–9 working days (1 engineer + 0.5 RL for gold-bot self-play)

### Goals
1. Provide a tiny behaviour-tree DSL header (or wrap [`BehaviorTree.CPP`](https://github.com/BehaviorTree/BehaviorTree.CPP)) and a fixed set of leaf actions (`engage`, `retreat_to_cover`, `patrol`, `reload`).
2. Provide an optional Python PPO trainer scaffold where the candidate can train their own policy as a sub-skill.
3. Train + commit pointer to the `gold` policy via 3-day self-play.
4. Add 2v2 mode to the arena: candidate's runner commands an ally NPC over a second gRPC stream.

### Files to create
```
HW7_strategy/
├── README.md
├── CMakeLists.txt
├── pyproject.toml                       # sample-factory, torch
├── src/
│   ├── train_ppo.py                     # optional candidate sub-skill
│   └── dsl_to_cpp.py                    # codegen for the BT DSL
├── include/aiming_hw/strategy/
│   ├── behavior_tree.hpp
│   └── leaf_actions.hpp
├── source/
│   ├── strategy.cpp                     # TODO: pick_target, retreat_logic
│   └── leaf_actions.cpp
├── tests/
│   ├── public/
│   │   ├── test_priority_distance.cpp
│   │   └── test_retreat_trigger.cpp
│   └── private/
│       └── test_5_episodes_vs_gold.cpp
└── grader/
    └── grader.py
```

### LOC budget
~ 1100 LOC.

### Smoke check
```bash
./build/HW7_strategy/strategy_smoke --bt-config configs/example.yaml
```
Expected: behaviour tree ticks, target selection prints expected target ID for hand-built scenes.

### Acceptance criteria
* Best-of-5 vs `gold` records at least one tracking-only "draw" with the reference solution (i.e., the bonus is non-trivial but achievable).
* PPO scaffold trains for 1k steps without NaN.

### Risks
* **Gold-bot self-play not converging in 3 days** — fallback: hand-script gold from a behaviour tree using silver's PPO as a sub-skill (already noted in §9 of the schema).

### Out of scope
Multi-agent communication beyond the simple ally-NPC channel; full game-theoretic equilibrium analysis.

---

## Stage 10 — Grading workflow v1 (Proposal A)

* **Branch**: `stage10/grading-v1`
* **End tag**: `v1.4-grading-v1`
* **Maps to schema**: §7 (filled in by this stage)
* **Calendar estimate**: 2 working days

After Stages 1–9 closed, the team reviewed four grading-design proposals (auto-grade only; auto-grade + hidden eval; full Orin-NX leaderboard; manual review). **Proposal A — auto-graded public tests + honour system + manual interview of top-N** was picked as the lightest viable v1; the heavier proposals carry over to a future cycle (see `docs/grading.md` § "future cycles").

This stage is the implementation of Proposal A. The (now superseded) v0.3 draft of Stage 10, which assumed a candidate-side grader CLI + signed score JSONs + build-artefact hash binding, is **not** being shipped — that surface lives in `docs/grading.md`'s last section as a future-cycle target.

### Goals
1. **`validate_submission.yml`** — PR-triggered workflow on `pull_request: opened|synchronize|reopened|ready_for_review`. Checks out `head.sha`, installs the C++ toolchain (cmake + g++-12 + libeigen3 + libgtest), `uv sync`, builds, runs `ctest --output-junit ctest_results.xml`, runs `pytest --junit-xml pytest_results.xml`, scores the result, posts (or updates in place) a single PR comment with the per-HW pass/fail/skip breakdown. Concurrency-cancels on push so candidate iteration is cheap.
2. **`regenerate_leaderboard.yml`** — daily cron at `17 11 * * *` UTC (= 19:17 Beijing-local). Walks every open PR, pulls the latest `submission-score-*` artefact, sorts (total passing tests desc, then HW-breadth desc), writes `leaderboard.{md,csv,json}` to an orphan `leaderboard` branch. Branch is internal — candidates working on `main` don't see it.
3. **`tools/leaderboard/score_pr.py`** — JUnit-XML parser. Buckets test cases by HW directory or `HW{N}` test-class prefix, emits both `submission_score.json` (machine-readable) and `submission_score.md` (PR-comment body).
4. **`tools/leaderboard/aggregate.py`** — driven by `regenerate_leaderboard.yml`. Uses `gh pr list` + `gh run download` to pull every PR's latest score, sorts, writes the three leaderboard files.
5. **Bilingual candidate handbook** at `docs/grading.md` — submission flow, what gets graded, what's known-skipped, troubleshooting, ops notes for the team.

### Files to create
```
.github/workflows/
├── lint_and_build.yml                   # already shipped in Stage 1; unchanged
├── validate_submission.yml              # NEW — PR-triggered grader
└── regenerate_leaderboard.yml           # NEW — daily aggregator cron

tools/leaderboard/
├── score_pr.py                          # JUnit XML → per-HW JSON + Markdown
└── aggregate.py                         # gh-pr-walking aggregator

docs/grading.md                          # bilingual candidate handbook
```

### What is *not* in v1 (future-cycle targets)

* Hidden tests / team-side simulator-driven episode evaluation. The infrastructure in `IMPLEMENTATION_PLAN.md` Stage 10 v0.3's draft (build-artefact hash binding, signed score JSONs, spot-check regrade helper) is recorded in `docs/grading.md` § "future cycles" as the v2 upgrade path.
* Live-arena episodes vs bronze/silver/gold. Out of scope until the bronze + silver + gold policies actually train (manifest carries placeholders today).
* `submissions/` directory pattern, `make grade` / `make submit` candidate UX. The PR diff is the submission; no committed score JSONs.
* Public Pages deploy or web UI for the leaderboard. CSV + Markdown on the orphan branch is enough for 50 candidates.
* Pilot run with internal volunteers (the v0.3 Stage 10b sub-stage). Defer until the team has pushed the workflows to the candidate-facing repo and seen one full cycle.

### LOC budget
~ 700 LOC (workflow YAML: 200, Python: 350, docs: 150).

### Smoke check
```bash
# Local: produce a fake CTest XML + pytest XML from a clean checkout, score it.
cd /tmp/checkout
ctest --preset linux-debug --output-junit ctest_results.xml
uv run pytest --junit-xml pytest_results.xml
uv run python tools/leaderboard/score_pr.py \
    --ctest-xml ctest_results.xml \
    --pytest-xml pytest_results.xml \
    --out /tmp/score.json --markdown /tmp/score.md
cat /tmp/score.md  # eyeball the table

# CI: open a draft PR, mark it ready-for-review, watch validate_submission run.
# Expect a single comment on the PR within 10 minutes.

# Cron: trigger by hand from the Actions tab.
gh workflow run "regenerate leaderboard"
git fetch origin leaderboard
git show origin/leaderboard:leaderboard/leaderboard.md
```

### Acceptance criteria
* `validate_submission.yml` runs to completion on a clean PR in < 12 minutes wall-clock on `ubuntu-latest`.
* The PR comment is posted exactly once per PR — push N times, comment is updated, not duplicated. Verified by the `<!-- aiming-hw-grader -->` marker check.
* `score_pr.py` partitions every test in the project to one of `HW1..HW7` or `shared`. No "unbucketed" cases.
* `regenerate_leaderboard.yml` produces a `leaderboard.csv` whose row count equals the count of open PRs with completed `validate_submission` runs.
* `docs/grading.md` covers submission flow, anti-cheat posture, known-skipped tests, and team-side ops in both Chinese and English.

### Risks
* **GitHub Actions outage on cutoff day** — `validate_submission` won't run, PRs sit unscored. Mitigation: the workflow is idempotent + dispatch-able; team can re-trigger manually with `gh workflow run "validate submission" --ref pr-branch`.
* **CI runner can't apt-install the toolchain** (transient apt repo failures). Mitigation: candidates push an empty commit (`git commit --allow-empty -m "ci: retry"`); workflow re-runs.
* **A candidate's tests time out** (e.g. infinite loop in their TODO impl). Mitigation: `timeout-minutes: 25` cap on the job; the comment shows which test hung.
* **Comment spam from PR re-runs** — addressed by the `<!-- aiming-hw-grader -->` marker that lets `actions/github-script` find and update the existing comment in place.

### Out of scope for Stage 10 (deferred to v2)

* Hidden tests, signed score JSONs, build-artefact hash binding (per `docs/grading.md` § "future cycles").
* Pilot run with internal volunteers — separate `stage10b/pilot` branch when scheduled.
* Migrating the leaderboard to a public Pages site — deliberately not done; `leaderboard` branch stays private to the team.

---

## Master timeline

| Stage | Tag | Cumulative weeks | Maps to milestone |
|-------|-----|-----------------|-------------------|
| 0 | `v0.2-schema` | week 0 | — |
| 1 | `v0.3-infra` | week 1 | M0 |
| 2 | `v0.4-arena-poc` | week 3 | M0 |
| 3 | `v0.5-hw1-detector` | week 5 | M1 |
| 4 | `v0.6-hw2-tf` | week 5.5 | M1 |
| 5 | `v0.7-hw3-ekf` | week 7 | M2 |
| 6 | `v0.8-hw4-ballistic` | week 8 | M2 |
| 7 | `v0.9-hw5-mpc` (+ optional 7b) | week 10 (or 13 if engine swap) | M3 |
| 8 | `v1.0-hw6-integration` | week 11 | M4 |
| 9 | `v1.1-hw7-strategy` | week 12 | M4 |
| 10a | `v1.2-grader` | week 13 | M5 |
| 10b | `v1.3-launch` | week 14 | M6 |

Total: 14 weeks nominal, 17 weeks if Stage 7b (Unity port) triggers.

---

## What is **out of scope** for the entire 10-stage plan

* Hardware drivers (no CAN, no actual gimbal motor control loop). The production repo at [`RM_Vision_Aiming/`](../RM_Vision_Aiming) keeps that responsibility.
* ROS2 integration. We deliberately do not require ROS2 for this assignment so candidates can install on Win/macOS without WSL.
* Real-time deployment to Orin / Jetson. The grader runs on x86_64 with an optional CUDA path.
* Public-internet leaderboard. Held in reserve as a future migration to Codabench (per schema §10 decision 3).
* Grade-based offer logic. Recruiting decisions are made by humans reading the leaderboard, not automated cutoffs.

---

## OSS bucket plan (rolled in from the bucket-sizing discussion)

Per resolved decisions 2, 3, and 5, all three buckets exist and are required. Region: `cn-beijing` (华北 2 Beijing); endpoint: `oss-cn-beijing.aliyuncs.com`.

| Bucket | Contents | ACL | Lifecycle | Versioning | Encryption |
|---|---|---|---|---|---|
| `tsingyun-aiming-hw-public` | Godot binaries × 3 OSes; HW1 eval set + real-world holdout; HW2/3/4 fixtures; candidate-facing docs PDFs | `public-read` (anonymous read; write: team only) | none; cross-region replication if international candidates | on | OSS-managed |
| `tsingyun-aiming-hw-models` | bronze/silver/gold opponent `.pt` + checkpoints; reference detector `.onnx`; replay bag fixtures > 50 MB | `private`; **shared read-only RAM AccessKey `aiming-hw-public-reader`** committed at `shared/oss/reader_credentials.json` in the candidate-facing repo (rotatable) | none; abort old object versions > 90 d | on (rollback path for a bad gold policy) | SSE-OSS |
| `tsingyun-aiming-hw-cache` | grader Docker image (`docker/grader/{tag}/`), vcpkg/uv caches, prebuilt acados artefacts | `private`; same shared AccessKey if candidates need to pull the grader image, otherwise team-only | delete objects > 90 d | off | OSS-managed |

Total expected bucket footprint: ~5–6 GB stored, ~250 GB lifetime egress (50 candidates × 5 GB initial pull). The `cache` bucket is provisioned (per resolved decision 5) and Stage 1 will push the grader Docker image to it so candidates can pull from a fast CN-region endpoint instead of `ghcr.io`. The other two buckets from the original 5-bucket plan (`submissions`, `replays`) are gone because they live inside each candidate's repo at `submissions/hw{N}/`, with the 30 s replay clips committed alongside `score.json`. If replay storage grows beyond what's reasonable to keep in git, we can later migrate replays to a `submissions/` OSS prefix.

### Why OSS instead of Git LFS for the model blobs
* GitHub's free LFS bandwidth quota is 1 GB/month — at 50 candidates × ~580 MB initial pull we'd hit a $25/month bandwidth-pack bill within the first day of launch.
* Aliyun OSS encrypts the opponent policies at rest with SSE-OSS (free, OSS-managed keys, no separate KMS service required), which is a meaningful precaution: the gold policy is the entire point of HW7 and we don't want someone snapshotting it from `git clone` traffic.
* The same `fetch_assets.py` resolver covers Godot binaries, datasets, and policies — one asset story, not two.
* If LFS bandwidth ever stops mattering (smaller candidate cohort, cheaper account tier), we can flip a single config switch in `manifest.toml` to push `vis = "lfs"` instead of `vis = "oss-private"`.

---

## Question log

All eight questions from v0.1 and v0.2 are now resolved (see "Resolved decisions" up top). Future revisions append below this line with a date and a short note.

— end of plan v0.3 —
