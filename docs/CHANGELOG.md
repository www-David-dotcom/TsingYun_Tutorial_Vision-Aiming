# Changelog

Conventional Commits + per-stage tags. This file tracks the reverse
chronological view of what landed when, separately from the live
implementation plan in `IMPLEMENTATION_PLAN.md`.

## v1.3-hw7-strategy — Stage 9 (2026-04-29)

Last active stage. HW7 strategy bonus — behaviour-tree DSL +
4 leaf actions + optional PPO scaffold + DSL → C++ codegen.
~1100 LOC across 3 headers + 3 sources + 2 GTests + 2 Python
modules + bilingual README.

* **HW7 directory:** `HW7_strategy/` — bilingual README, opt-in
  `hw7` uv group (torch, tqdm; sample-factory is manual install
  for the stretch goal), CMakeLists with no extra deps beyond
  GTest.
* **filled (C++):** `behavior_tree.hpp` — minimal Sequence /
  Selector / Action runtime + typed Blackboard (variant-based,
  string keys); factory functions `sequence/selector/action` so
  the codegen emits readable C++.
  `leaf_actions.{hpp,cpp}` — four leaves (engage, retreat_to_cover,
  patrol, reload) reading from + writing to the Blackboard.
* **two C++ TODO sites in `source/strategy.cpp`:**
  * `pick_target` — highest-priority enemy track. Floor pinned by
    tests: closest enemy wins at equal HP; allies skipped.
  * `should_retreat` — switch from engage to retreat. Floors:
    `self.hp <= 30` → retreat, `self.ammo <= 20` → retreat.
* **Python (filled):**
  `src/dsl_to_cpp.py` — YAML → C++ codegen. Reads
  `configs/example_bt.yaml`, emits a `build_tree()` entry point.
  Three node kinds (sequence, selector, action), six leaf names
  (`engage`, `retreat_to_cover`, `patrol`, `reload`,
  `should_retreat_check`, `engage_or_patrol`).
  `src/train_ppo.py` — vanilla PPO scaffold (clipped objective,
  GAE-Lambda, AdamW, single-process rollout) over a stub env that
  emits canned observations. Candidate's first sub-skill task is
  to swap the stub for a real gRPC env; stretch goal is plugging
  in sample-factory's parallel rollouts.
* **public tests:**
  `hw7_priority_distance_test` — 4 cases on pick_target (empty,
  all-ally, closest-equal-HP, ally-near + enemy-far).
  `hw7_retreat_trigger_test` — 4 cases on should_retreat (full +
  full → no retreat, low HP → retreat, low ammo → retreat,
  exact-threshold → retreat).
  Each detects unfilled TODOs and `GTEST_SKIP`s cleanly.
* **manifest:** `gold-opponent-policy` row in the private models
  bucket (placeholder digest until 3-day self-play training
  completes).
* **CMake:** root project bumped to **1.3.0**; HW7 wired in behind
  the same EXISTS guard pattern.
* **uv workspace:** registers `HW7_strategy` as a member.

Out of scope: multi-agent communication beyond the simple ally-NPC
channel, full game-theoretic equilibrium analysis, the gold policy
training itself (team-side), hidden grading (deferred per Stage 10).

**With this tag, HW1–HW7 are all landed.** Stage 10 (grading
workflow + launch) remains deferred per the v0.4 schema decision.
Once the team has reviewed the seven HWs end-to-end, write
`design: grading workflow v1` against `schema.md` §7 and reopen
Stage 10.

## v1.2-hw6-integration — Stage 8 (2026-04-29)

Integration runner — the program that wires HW1 → HW3 → HW4 → HW5
into one real-time loop talking to the Godot arena. ~900 LOC across
ring buffer + watchdog + runner + main + two GTests + bilingual
README.

* **HW6 directory:** `HW6_integration/` — bilingual README,
  CMakeLists with optional `-DAIMING_HW6_TSAN=ON` for the
  ThreadSanitizer build, opt-in HW1 / HW5-MPC links (commented in
  the CMakeLists; uncomment once those targets are configured).
* **filled:** `pipeline::SpscRingBuffer` (lock-free, header-only,
  cache-line-aligned head/tail; capacity rounded to next power of 2;
  move-only payloads supported); `pipeline::Watchdog` (cooperative
  timer; pet from the control thread, expiry callback fires once on
  starvation, recovers after the next pet; thread-safe stop()).
* **two candidate TODOs:**
  * `source/runner.cpp::Runner::next_frame` — stale-frame drop
    policy. Drop frames whose ID is older than
    `latest_frame_id - max_stale_frames`. Without this the EKF
    ingests stale frames whenever the control loop hiccups.
  * `source/main.cpp::run_episode` — thread layout. At least three
    threads (frame subscriber, gRPC client, control loop) with the
    control loop pinned to a single core for the p95 ≤ 25 ms
    target. The current placeholder only runs stats + watchdog.
* **public tests:**
  `hw6_ring_buffer_test` — capacity rounding, fill/drain, move-only
  payload, 100k-element concurrent producer/consumer race.
  `hw6_watchdog_test` — pet/expire/recover/stop behaviour with
  fake-time backed by `std::chrono::steady_clock`.
  Both build under `-DAIMING_HW6_TSAN=ON` for the ThreadSanitizer
  pass on every commit.
* **stats surface:** `Runner::stats()` returns frames_received /
  dropped / consumed, loop iteration count, and rolling p95 latency
  over a 256-element reservoir. HW6 acceptance bar is **p95 ≤ 25 ms**;
  the system-level "no races over 5 episodes" bar runs against a
  live arena, not in unit tests.
* **CMake:** root project bumped to **1.2.0**; HW6 wired in behind
  the same EXISTS guard. The runner CLI auto-links HW3 + HW4 +
  HW5 PID baseline when those targets are configured (HW1 ONNX +
  HW5 MPC opt-in via uncommented lines because their toolchains
  are heavier).

Out of scope (in `HW6_integration/README.md`): HW7 strategy /
behaviour-tree wiring (Stage 9), gRPC client implementation
(candidates use generated stubs), hidden grading (Stage 10).

## v1.1-hw5-mpc — Stage 7 (2026-04-29)

MPC gimbal controller + the engine quality gate. Multi-language
(CasADi → acados-codegened C → C++ runtime), with a filled PID
baseline as the floor the candidate's MPC must beat.

* **HW5 directory:** `HW5_mpc_gimbal/` — bilingual README, opt-in
  `hw5` uv group (casadi, acados-template, matplotlib), CMakeLists
  with two build modes (PID-only vs PID+MPC, depending on whether
  `generated_solver/acados_aiming_mpc/CMakeLists.txt` exists).
* **Python (filled):**
  `src/cost.py` — quadratic stage + terminal cost from
  `configs/mpc_weights.yaml`.
  `src/generate_acados.py` — team-side codegen with a `--check` mode
  that validates the model + cost without invoking acados (useful
  for candidates without acados installed).
  `src/tune.py` — closed-loop PD-baseline sweep so candidates can
  iterate on gain values without waiting for codegen.
* **Python TODO sites in `src/model.py`:**
  * `motor_torque_lag` — first-order lag from torque cmd to applied
    torque, two CasADi expressions.
  * `state_dot` — assemble the full 6-vector dx/dt; kinematic terms
    are filled, the candidate plugs in the lag dynamics.
* **C++ (filled):** `pid_baseline.{hpp,cpp}` — two-axis PD with
  velocity feedforward, hard rate + torque limits. Default gains
  settle a 30° step in ~120 ms with no overshoot.
* **C++ TODO site in `source/controller.cpp`:** wire the acados
  capsule through the `step` API. Header file is filled. While the
  TODO is unfilled, `MpcController::step` falls back to a tiny-gain
  proportional command so the binary still compiles and the
  controller's interface tests pass even without codegen.
* **Public tests (PID path):**
  `hw5_step_response_test` — settling < 200 ms, overshoot < 5%,
  torque respects hard limit.
  `hw5_sinusoid_tracking_test` — 1 Hz / 0.5 rad reference, RMSE
  in steady state < 0.05 rad.
* **Engine quality gate template:**
  `docs/visual_review_2026-04-29.md` is the meeting agenda for the
  Godot-vs-Unity decision called for in `schema.md` §10 decision 1.
  Decision is TBD; document captures attendees, criteria, action-
  item slots.
* **Manifest:** `acados-solver-hw5-v1.1` row in the private models
  bucket — the team's codegen tarball goes here so candidates pull
  instead of install acados locally.
* **CMake:** root project bumped to **1.1.0**; HW5 wired in behind
  the same EXISTS guard pattern.
* **uv workspace:** registers `HW5_mpc_gimbal` as a member.

Out of scope (in `HW5_mpc_gimbal/README.md`): online weight tuning,
hardware-in-the-loop, CUDA EP. Hidden grading deferred per Stage 10.

## v1.0-hw4-ballistic — Stage 6 (2026-04-29)

Ballistic + iterative aim-prediction solver. Closes the
detection → tracking → shooting math chain (HW1 → HW3 → HW4); HW6
will wire all three behind a runtime in Stage 8.

* **HW4 directory:** `HW4_ballistic/` — bilingual README, CMakeLists
  with Eigen3 skip-guard, ~700 LOC across 2 headers + 2 sources +
  3 tests.
* **filled:** `ProjectileParams` (RM 17 mm defaults: 3.2 g pellet,
  sphere drag, ρ = 1.225); `projectile_acceleration` (gravity +
  quadratic drag); `projectile_position_at` /
  `projectile_velocity_at` via RK4 substepping (1 ms default → 1 mm
  precision over 30 m flight). Convention: Z-up world,
  g = (0, 0, -9.81).
* **two candidate TODOs in `source/solver.cpp`:**
  * `solve_flight_time` — closest-approach time-of-flight along a
    given aim direction. Coarse 10 ms scan + bracketed refinement.
    Closed-form short-circuit when both drag and gravity are zero.
  * `plan_shot` — iterative lead computation. Pick a t guess from
    range/speed, predict the target's future position, solve for the
    aim direction, re-solve flight time under drag, iterate until
    miss distance < tolerance.
* **public tests:** three GTest binaries —
  * `hw4_1d_no_drag_test` (range/speed, static-target aim, moving-target lead)
  * `hw4_2d_with_gravity_test` (aim lifts above target; farther → more lift)
  * `hw4_3d_with_drag_test` (converges ≤ 8 iter; lead match within 1 cm; ≥ 95% hit rate on a 32-target sweep)
  Each detects unfilled TODOs and `GTEST_SKIP`s cleanly.
* **CMake:** root project bumped to **1.0.0**; HW4 wired in behind
  the same EXISTS guard.

Out of scope (in `HW4_ballistic/README.md`): Magnus / spin, wind,
projectile-projectile collisions, heat / barrel limits (those live
in HW6's runner). The IMPLEMENTATION_PLAN hit-rate-at-5/10/15 m bar
is a system-level measurement that needs HW3 + HW4 + a real Godot
arena — HW4 in isolation only pins the math.

## v0.9-hw3-ekf — Stage 5 (2026-04-29)

The state estimator that consumes HW1's detections and feeds HW4 +
HW6. EKF + two-mode IMM (CV + CT) + Hungarian-based multi-target
data association. ~1700 LOC across C++ + Python reference + tests +
fixtures.

* **HW3 directory:** `HW3_ekf_tracker/` — bilingual README, CMakeLists
  with Eigen3 skip-guard, opt-in `hw3` uv group for the Python
  reference's scipy dep.
* **Python reference + fixtures:**
  `reference/ekf_python.py` is the math spec — full EKF predict +
  Joseph update, two-mode IMM mixing/blending, mahalanobis_cost +
  Hungarian (delegated to scipy).
  `reference/generate_fixtures.py` produces three deterministic
  1800-sample CSV trajectories (low / med / high maneuver, 60 Hz
  for 30 s each) under `tests/fixtures/`. Same `--seed` →
  byte-identical CSV output.
* **C++ filled:** `motion_models.{hpp,cpp}` (CV/CT transitions,
  process noise covariances), `tracker.{hpp,cpp}` (per-target IMM
  ownership, coast/spawn lifecycle, gated cost-matrix construction),
  `gaussian_likelihood` in `kalman_step.cpp`.
* **Four C++ TODO sites** for the candidate:
  * `kalman_step.cpp::predict` — F·x; F·P·Fᵀ + Q.
  * `kalman_step.cpp::update`  — Joseph-form posterior. Naive form
    drifts to non-symmetric P after ~600 steps.
  * `imm.cpp::Imm::step`       — three labelled blocks: mixing,
    mode-probability update, combination.
  * `data_association.cpp`     — `mahalanobis_cost` +
    `hungarian_assign` (Munkres from scratch — no third-party LAP
    library on the dep list).
* **Public tests:** three GTest binaries — `hw3_cv_predict_test`
  (analytic CV step + covariance growth + Joseph symmetry over 100
  steps), `hw3_imm_mode_probabilities_test` (sums to 1, straight
  line favours CV, ω=4 rad/s curve favours CT), `hw3_da_simple_test`
  (1×1, 2×2 off-diagonal optimum, χ²-gate filter). Each detects the
  unfilled-TODO state via a sentinel call and `GTEST_SKIP`s
  cleanly.
* **Math reference:** `docs/ekf_derivation.md` — predict/update
  equations, IMM mixing/blending, why Joseph form, Hungarian + gating,
  performance signals.
* **Manifest:** `silver-opponent-policy` row (private bucket,
  placeholder digest until RL side ships).
* **CMake:** root project bumped to 0.9.0; HW3 wired in behind the
  same EXISTS guard pattern as HW1/HW2.
* **uv workspace:** registers `HW3_ekf_tracker` as a member.

Out of scope (and explicit in `HW3_ekf_tracker/README.md`):
UKF / particle filter / GP-UKF, nonlinear measurement models,
JPDA / MHT data association. Hidden grading (NEES coverage, RMSE
bars) deferred per `IMPLEMENTATION_PLAN.md` Stage 10.

## v0.8-hw2-tf — Stage 4 (2026-04-29)

Smaller assignment — the TF (transform) graph that HW6's runner uses
to project detections from the camera frame back into world
coordinates. Header-mostly C++ over Eigen 3.4, no ROS dep.

* **HW2 directory:** `HW2_tf_graph/` — bilingual README, CMakeLists
  that skips itself when Eigen3 isn't available, three `.hpp` +
  two `.cpp` files (~600 LOC).
* **filled:** `Transform` struct (translation + unit quaternion) with
  `operator*` for points and `inverse()`; `Buffer` class storing
  per-edge chronological time series with `set_transform`,
  `lookup_direct`, `lookup_chain`, `prune_older_than`. Monotonic
  inserts; out-of-range and unknown-edge lookups throw `LookupError`.
* **two candidate TODOs in `source/interpolate.cpp`:**
  * `tf::interpolate(a, b, alpha)` — translation lerp + quaternion
    SLERP with the antipodal short-arc fix-up.
  * `tf::compose(parent_to_middle, middle_to_child)` — chain two
    rigid transforms (three lines once you've thought about it).
* **public tests:** three GoogleTest binaries — `hw2_basic_lookup_test`,
  `hw2_interpolation_corners_test`, `hw2_chain_compose_test`. Each
  test detects the unfilled-TODO state via a sentinel call and
  `GTEST_SKIP`s with a clear pointer at the file to edit, so the
  rest of the project's CI stays green during stage close.
* **CMake:** root project bumped to 0.8.0; HW2 subdir added behind
  the same EXISTS guard pattern as HW1.

Acceptance posture: per the Stage 4 plan, the public tests are
expected to pass on the candidate's filled implementation. With the
TODOs unfilled, they self-skip with a clear message — one of the
GTEST_SKIP messages is what the candidate sees first when they run
`ctest -R hw2`.

Out of scope (and explicit in `HW2_tf_graph/README.md`): ROS 2 TF2
integration, frame-graph BFS, thread safety. Hidden grading episodes
deferred per `IMPLEMENTATION_PLAN.md` Stage 10.

## v0.7-hw1-detector — Stage 3 (2026-04-29)

First candidate-facing assignment. HW1 is the lightweight armor +
icon detector — train a small CNN on synthetic frames, export to
ONNX, run inference from C++ via ONNX Runtime.

* **HW1 directory:** `HW1_armor_detector/` — bilingual README
  (Chinese primary, English summary), per-HW pyproject with torch in
  an opt-in dependency group (`uv sync --group hw1`), CMakeLists that
  skips itself cleanly when ONNX Runtime isn't installed, and a
  `proto -> ../shared/proto` symlink so the runner pulls the
  cross-stage protos.
* **dataset:** `data/dataset_dumper.py` ships two backends. The
  synthetic backend (PIL only) draws procedural plates on noisy
  backgrounds for offline iteration; the godot backend connects to
  the Stage-2 arena over TCP, reads oracle target poses each tick,
  and re-projects them via the camera intrinsics in
  `data/camera_intrinsics.yaml`. Domain-randomization knobs in
  `data/domain_randomization.yaml`.
* **training:** `src/model.py` is MobileNetV3-Small at stride 16
  with a four-headed multi-task head (4 box + 8 kpt + 4 cls + 1 obj).
  `src/losses.py` carries the primitives candidates compose
  (`giou_loss`, `focal_loss`, `keypoint_l1`, `softmax_focal_loss`,
  `assign_targets`); `src/train.py` exposes four `# TODO(HW1):`
  sites — `loss_box`, `loss_kpt`, `loss_cls`, `mixup`.
  `src/export_onnx.py` is fully filled (no TODOs) and validates the
  graph through `onnx.checker.check_model`.
* **C++ inferer:** `include/aiming_hw/detector/{post_process,inferer}.hpp`
  + matching sources. Session set-up, IO binding, and the
  uint8-BGR-to-float-RGB normalisation are filled; the candidate
  writes `decode_head` and `non_max_suppression` (post-NMS class-aware
  dedup). A `hw1_inferer_smoke` CLI loads either raw RGB888 or PPM
  P6 and prints detections.
* **public tests:** `tests/public/test_loss_shapes.py` (loss-call
  smoke + xfail wrap when TODOs aren't filled),
  `tests/public/test_export_roundtrip.py` (ONNX export + checker),
  `tests/public/test_post_process.cpp` (GoogleTest, NMS dedup +
  cross-class-stack edge cases). Each test self-skips when its heavy
  dependency (torch, onnxruntime) is missing.
* **manifest:** two new placeholder rows — `real-holdout-frames-v1`
  (anonymous-public, the labeled real-world holdout) and
  `bronze-opponent-policy` (private, the frozen RL bot for the red
  chassis). Both with zero digests until the team uploads.
* **CMake:** root project bumped to 0.7.0; HW1 subdir added with a
  guard so candidates without ONNX Runtime still get a green
  configure pass.
* **uv workspace:** registers `HW1_armor_detector` as a member so
  `uv sync` resolves its tree alongside the existing stub servers.

Out of scope for v0.7 (and explicit in `HW1_armor_detector/README.md`):
TensorRT engines, distillation/quantization, the CUDA EP, hidden
grading episodes (deferred per `IMPLEMENTATION_PLAN.md` Stage 10),
and the actual bronze-policy training run (Stage 3's RL side ships
later — the manifest placeholder unblocks the wiring without blocking
on the model).

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
