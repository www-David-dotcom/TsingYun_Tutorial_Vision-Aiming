# Aiming_HW — Recruitment Coding-Assignment Design Schema

> Plan version 0.4 — **grading workflow and leaderboard policy are deferred** to a separate design pass once HW1–HW7 scaffolds exist (Stages 1–9 in [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md)). This document is now scoped to the homework design itself: scenario, simulator, per-HW deep-dives, toolchain, and roadmap. §7 is a placeholder. We'll redesign grading once we know what we're actually grading.
> Audience: TsingYun RoboMaster Vision Group leads. Schema language: English. Per-HW READMEs: **Chinese primary with an English summary block** (per §10 decision 5).
> Companion repository: [`RM_Vision_Aiming/`](../RM_Vision_Aiming) (the production self-aiming pipeline this assignment is derived from). Candidate-facing repo: [`www-David-dotcom/TsingYun_Tutorial_Vision-Aiming`](https://github.com/www-David-dotcom/TsingYun_Tutorial_Vision-Aiming).

---

## 0. TL;DR — what we propose

A **single-scenario, multi-stage coding gauntlet** called **"Aiming Arena"**: a 3D first-person adversarial shooter in which the candidate's C++ aiming stack faces off against a frozen population of RL-trained "ghost" robots inside a custom simulator. The candidate progresses through **six required sub-projects (HW1–HW6) plus one bonus (HW7)** — each a separate ROS2-style C++ package or Python module, each with prebuilt scaffolding and `TODO` slots. HW7 is genuinely optional. Grading workflow and leaderboard mechanics are **deferred** (see §7); this schema is scoped to the homework design itself.

Recommended stack:

| Layer | Choice | Reason |
|---|---|---|
| **Game engine / simulator** | **Godot 4 + [Godot RL Agents](https://github.com/edbeeching/godot_rl_agents)** as primary; **Unity 2023 LTS + ML-Agents** as visual-fidelity fallback | Godot is MIT, cross-platform, ships an FPS RL example out of the box, builds standalone executables for Win/macOS/Linux from a single `.tscn`. Unity is the contingency if visuals fall short. |
| **Engine ⇄ Python comm** | **gRPC + Protobuf** for typed RPC; **ZeroMQ (PUB/SUB)** for high-rate sensor streams | gRPC matches the pattern Unity ML-Agents uses; ZMQ avoids serialization overhead for image frames. |
| **Engine ⇄ C++ comm** | Same gRPC service compiled with the C++ codegen; image stream over shared memory on Linux/macOS, named pipes on Windows | Lets the candidate's C++ binary talk to the simulator with the same contract as Python tooling. |
| **Frozen opponent agents** | Trained offline with **[Sample Factory 2](https://github.com/alex-petrenko/sample-factory)** + **[PettingZoo](https://pettingzoo.farama.org/)** parallel API | Highest known single-machine throughput on VizDoom/Megaverse-class envs; PettingZoo gives us self-play and adversarial training for free. |
| **ML training** | Python ≥ 3.11, **`uv`** for env/lock management, PyTorch ≥ 2.4, ONNX export | `uv` is fast, deterministic, cross-platform; ONNX makes C++ inference engine-agnostic. |
| **C++ build** | **CMake ≥ 3.22** + **[basis-robotics/uvtarget](https://github.com/basis-robotics/uvtarget)** to glue Python venvs into CMake builds, **[Eigen 3](https://eigen.tuxfamily.org/)**, **[ONNX Runtime](https://onnxruntime.ai/)**, **[CasADi](https://web.casadi.org/) / [acados](https://docs.acados.org/)** for MPC | All three are MIT/BSD/LGPL and run on Win/macOS/Linux; `uvtarget` solves the Python-in-CMake friction. |
| **Submission & grading** | *Deferred — see §7* | We will design grading after the HW scaffolds (Stages 1–9) are built. Until then, every HW description focuses on what the homework teaches and measures, not on how the team converts those measurements into a recruiting decision. |

We strongly recommend the **single-scenario** option (Option B in the README) because (a) the visual feedback loop — watching your robot win or die in a 3D arena — is what makes the assignment "scenically attractive"; (b) every HW remains discoverable in one shared mental model; (c) the same simulator binary serves both interactive play and any future grading harness. Building it is real work, but **roughly 70% of the engine effort is one-time and shared across all HWs**, so the marginal cost per HW is small.

---

## 1. Scenario design — "Aiming Arena"

### 1.1 World

* **Setting**: a near-future / sci-fi indoor industrial complex (think *Titanfall* training arena meets a RoboMaster venue — neon-lit catwalks, blinking holo-panels, low-poly cargo crates).
* **Map**: one shipped at v1, three by v3. Each map is a **bounded arena** ≈ 20 m × 20 m with multi-level catwalks, breakable cover, and a few "speed strips" that boost robot translational velocity. Lighting transitions between bright/strobe/blackout at scripted intervals to stress vision robustness.
* **Robots**: 4-wheel mecanum chassis with an independent yaw/pitch gimbal — **kinematics are a faithful simplification of a RoboMaster Standard robot** so the assignment has direct skill transfer.
* **Armor plates**: every robot carries 4 armor plates (front/back/left/right) bearing a class icon (Hero / Engineer / Standard / Sentry). Armor lights glow blue or red to identify team. **Hits register only on an armor plate**, mirroring the real RoboMaster rule and giving the detection task a clear semantic target — see [DeanFrancis 2025 RoboMaster auto-target paper](https://www.deanfrancispress.com/index.php/te/article/view/3583) and [arXiv:2312.05055](https://arxiv.org/html/2312.05055v1).
* **Projectiles**: simulated 17 mm pellets with realistic muzzle velocity (~25 m/s), gravity, and air-drag (linear-quadratic). Firing rate, projectile spread, and "barrel heat" cap are configurable per HW so simpler tasks can disable nuisance physics.
* **Camera**: every robot streams a 1280×720 RGB feed at 60 fps from its gimbal; this is the *only* perception input the candidate's stack receives. Optionally a depth channel can be unlocked for the easiest HW to sidestep PnP for warm-up purposes.

### 1.2 Adversaries — the "Ghost" RL bots

Each map ships with three opponent skill tiers, each a **frozen PPO policy** trained in Sample Factory:

| Tier | Behaviour | Training time | Used in |
|---|---|---|---|
| `bronze` | Patrols, slow turret swing, fires only inside a short range | 4 h on 1 GPU | HW1–HW3 |
| `silver` | Strafes, takes cover, mid-range fire | 24 h on 1 GPU | HW4–HW6 |
| `gold` | Aggressive, swap-targets, juke maneuvers, predictive aim | self-play 3 days × 4 GPUs | HW7 |

Bots receive the same observations a human would (pixel feed + IMU + chassis odometry), so the candidate's stack is competing on equal sensory footing. Their policies are frozen `.pt` files hosted in the private OSS bucket `tsingyun-aiming-hw-models` (`cn-beijing`) and pulled into `out/assets/` on demand by `shared/scripts/fetch_assets.py`. Policies are not retrained during the recruitment cycle, so episode outcomes are reproducible across runs of the same code against the same seed.

### 1.3 Episode shape

Each match is a **90-second deathmatch** (1 vs 1 by default; HW7 unlocks 2 vs 2). Within an episode, both robots spawn at randomized positions, the timer counts down, and the match ends when either side is destroyed or the clock runs out. Per-episode telemetry the simulator emits — `win_flag`, damage dealt/taken, armor-hit accuracy, aim latency, projectile-fly time — is recorded to a structured `episode.json` so that any later scoring policy has a stable input.

The exact mapping from telemetry to a numeric score, the seed list used for grading, and how scores feed a leaderboard are **deferred to §7**. The episode shape itself (90 s, 1v1 or 2v2, the telemetry schema) is part of the homework design and stays here.

### 1.4 Why this scenario satisfies all requirements

* **Visualizable** — candidates spectate replays in the Godot client locally; the simulator can record short MP4 clips of any episode for review.
* **Tech-stack coverage** — vision (HW1), TF transforms (HW2), EKF (HW3), ballistic + delay model (HW4), MPC gimbal control (HW5), full integration (HW6), self-play strategy (HW7).
* **Cross-platform** — Godot exports Win/macOS/Linux from one `.tscn`; ROS2 Humble is not required (we drop the ROS2 dependency from the production repo for the assignment to flatten the install slope).
* **Difficulty gradient per HW** — every HW splits into `easy / medium / hard / bonus` sections, satisfying the README's "very simple to very difficult" requirement.

---

## 2. Engine & simulator selection

### 2.1 Comparison

| Platform | Visual quality | Multi-agent FPS | Cross-platform | Python API | C++ API | License | Time-to-prototype | Verdict |
|---|---|---|---|---|---|---|---|---|
| **Godot 4 + Godot RL Agents** | Decent low-poly + post-FX | Yes (parallel agents) | Win/macOS/Linux/Web | TCP-Sync via `gdrl` | TCP client trivial to write | MIT | **2 weeks** for an FPS prototype (FPS example exists upstream) | **Recommended primary** |
| **Unity 2023 LTS + ML-Agents** | High (Asset Store, HDRP) | Yes | Win/macOS/Linux | gRPC (built-in) | gRPC C++ codegen | Personal/student free, Pro paid above $200k | 4–6 weeks | **Fallback if visuals fall short** |
| **Unreal Engine 5 + Learning Agents** | Highest (Lumen/Nanite) | Yes (newer plugin) | Win/macOS/Linux | New, partial | Painful | EULA restrictive | 8+ weeks; LA inference still maturing per [Unreal LA docs](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/PluginIndex/LearningAgents) and [UnrealMLAgents port](https://github.com/AlanLaboratory/UnrealMLAgents) | Reject — too heavy for one season's recruiting cycle |
| **VizDoom** | Retro Doom | Excellent (mature deathmatch) | Yes | Native | Native | Permissive | 3 days | Reject — fails the "scenically attractive" bar |
| **DeepMind Lab** | Quake-III ioquake3 | Yes | **Linux only** | Lua/Bazel | C | GPL | 1 week | Reject — Linux only |
| **Megaverse** | Stylized but spartan | Excellent (1M FPS) | Yes | Yes | Yes | MIT | 2 weeks | Reject for primary; **keep as headless training env** for RL bots if Godot training is too slow (see [Megaverse paper](https://wijmans.xyz/publication/megaverse/)) |
| **NVIDIA Isaac Lab** | Photoreal | Robotics-first, not FPS | Yes (CUDA-only) | Yes | Yes | NVIDIA | 6 weeks | Reject — CUDA-only and built for arms/manipulation rather than FPS, per [Isaac Lab paper](https://research.nvidia.com/publication/2025-09_isaac-lab-gpu-accelerated-simulation-framework-multi-modal-robot-learning) |

### 2.2 Decision

Build the arena in **Godot 4** as a `.tscn` scene driving a `CharacterBody3D` chassis + skeletal yaw/pitch gimbal. Wrap it with the [`godot_rl_agents`](https://github.com/edbeeching/godot_rl_agents) `Sync` node so we get a Gym-style PettingZoo env for free, integrating directly with [Sample Factory](https://github.com/alex-petrenko/sample-factory).

If the visual bar isn't met by milestone M3 (see §8), we port the same `.tscn` semantic to Unity HDRP — engine swap is a known cost since the geometry, physics, and gameplay logic are described in our own protocol-buffer schema, not in engine-specific code.

### 2.3 Why we deliberately are not "AI-generating" the engine

The README note about previously trying AI-generated engines is on point. Generative tools can scaffold a prototype but they cannot deliver the polish bar the team wants. We use Godot's existing demo assets ([Kenney's Sci-Fi Kit](https://kenney.nl), CC0) plus a custom shader pack for muzzle flashes / armor glow / impact decals, all of which have permissive licenses.

---

## 3. System architecture

```
┌────────────────────────────────────┐         gRPC (control)         ┌────────────────────────────────────┐
│ Aiming Arena (Godot/Unity binary)  │ <───────────────────────────── │ Candidate's C++ stack (`hw_runner`) │
│ - physics, rendering, scoring      │                                │ - detector (HW1)                   │
│ - frozen RL opponents              │ ─── ZMQ PUB (image frames) ──> │ - PnP (provided)                   │
│ - replay recorder                  │                                │ - tf graph (HW2)                   │
│                                    │ ─── ZMQ PUB (chassis IMU) ───> │ - EKF tracker (HW3)                │
│                                    │ <── ZMQ SUB (gimbal cmd) ───── │ - ballistic + delay (HW4)          │
│                                    │ <── ZMQ SUB (fire/ack)  ────── │ - MPC controller (HW5)             │
└────────────────────────────────────┘                                │ - integration / strategy (HW7)     │
              ▲                                                       └────────────────────────────────────┘
              │ same gRPC contract                                                          ▲
              │ (used during training only)                                                 │ shared protobuf
              │                                                                             │
              │                          ┌───────────────────────────────────────────────────┘
              │                          │
              │                          ▼
┌────────────────────────────────────────────────────┐
│ Python tooling (`hw_pyrunner`)                     │
│ - dataset generation (random arena, label dump)    │
│ - model training & ONNX export (PyTorch + uv)      │
│ - RL bot training (Sample Factory + PettingZoo)    │
│ - episode driver (subprocess wraps both the         │
│   candidate binary and the simulator)              │
└────────────────────────────────────────────────────┘
```

### 3.1 Protocol contract

A single `aiming.proto` defines:

* `EnvReset(Seed) → InitialState`
* `EnvStep(GimbalCmd) → SensorBundle`
* `EnvPushFire(FireCmd) → AckOrPenalty`
* `Episode → ScoreBundle` (terminal)

`SensorBundle` carries (a) RGB frame ID + ZMQ topic name (frames travel over ZMQ to bypass gRPC's 4 MB ceiling), (b) chassis IMU, (c) ground-truth timestamps for telemetry only, (d) HW-specific oracle hints — for example, for HW3 the simulator can optionally publish ground-truth target velocity for sanity-check unit tests, with the simulator's `oracle_hints_enabled` flag controlling exposure. Whether and when oracle hints are masked during evaluation is a §7 concern.

### 3.2 Why dual transport

* **gRPC** is convenient for typed step/reset RPCs and works identically in C++/Python/Godot (via [`mr-grpc-unity`](https://github.com/OpenAvikom/mr-grpc-unity)-equivalent ports for Godot and a hand-written GDExtension we open-source).
* **ZMQ PUB/SUB** carries 60 fps × 720p frames at 1.3 Gbps best case — out of gRPC's comfort zone but trivial for ZMQ + LZ4 (cf. the [2026 Python↔Unity protocol guide](https://copyprogramming.com/howto/protocol-to-communicate-between-python-and-unity)). On Linux/macOS we use the `ipc://` transport (Unix domain sockets) for ~25% throughput gain.

### 3.3 Determinism and reproducibility

* Simulator advances on a fixed 200 Hz physics tick; rendering at 60 Hz.
* All RNG paths (map seed, opponent action sampling, projectile spread) take a single 64-bit seed exposed via `EnvReset(seed=N)`. Episodes are deterministic given the seed, opponent `.pt`, and simulator binary hash. The actual seed list used for any later grading workflow is a §7 concern.
* `episode.json` includes the seed used, opponent `.pt` SHA, simulator binary hash, and the candidate's commit SHA so any episode is reproducible end-to-end.

---

## 4. Sub-project breakdown

Each HW is a self-contained sub-folder under `Aiming_HW/`. Every folder contains:

```
HW{N}_{slug}/
├── README.md                # task statement, math, pseudocode, performance signals
├── pyproject.toml           # if Python; managed by uv
├── CMakeLists.txt           # if C++; uses uvtarget for Python deps if needed
├── proto/                   # symlinked from /shared/proto
├── include/, src/           # provided code with TODO-marked holes
├── tests/                   # public unit + smoke tests (visible to candidate)
└── docs/figures/            # generated plots, math diagrams
```

The README of each HW MUST contain (per the team's spec):

0. **Language**: Chinese primary, with a short English summary block at the top (per §10 decision 4). Math, code, pseudocode, and identifiers stay language-neutral.
1. Environment setup for Win/macOS/Linux (one fenced block per OS).
2. Task description with motivation tied back to the Aiming Arena scenario.
3. Mathematical principles (LaTeX-rendered) where applicable.
4. Pseudocode preview at the level the team described — neither English narration nor copy-pasteable code, e.g.

   ```python
   for k in range(0, N):
       x_pred = F @ x[k] + B @ u[k]                  # TODO: derive F for CV+yaw model
       P_pred = F @ P[k] @ F.T + Q                   # TODO: tune Q from data
       y     = z[k] - h(x_pred)
       S     = H @ P_pred @ H.T + R
       K     = P_pred @ H.T @ inv(S)
       x[k+1] = x_pred + K @ y
       P[k+1] = (I - K @ H) @ P_pred
   ```

5. Public unit-test contract — which functions the candidate must implement and what `pytest` / `ctest` will check.
6. Performance signal (what good behaviour looks like; see §5). How that feeds a grade is a §7 concern.

### Difficulty roadmap

| HW | Title | Lang | Avg time (good candidate) | Pre-req | Outputs of interest | Frozen opponent tier |
|---|---|---|---|---|---|---|
| HW1 | Lightweight armor + icon detector | Python (train) + C++ (infer) | 12 h | none | `model.onnx` + `infer.cpp` filling 4 `TODO`s | bronze |
| HW2 | TF graph: pixel → world | C++ | 4 h | HW1 | `Tf::lookup` returns 6-DoF transform between any pair of frames | bronze |
| HW3 | EKF target tracker (single + multi-target) | C++ | 12 h | HW2 | `EKF::predict / update` for CV → CT model + IMM mode-switch (bonus) | silver |
| HW4 | Ballistic predictor + firing-delay compensator | C++ | 8 h | HW3 | `Ballistic::aim()` returns gimbal pose + scheduled fire time | silver |
| HW5 | MPC gimbal controller | C++ | 16 h | HW4 | `Mpc::step()` returns torque-rate cmd over 0.4 s horizon | silver |
| HW6 | Full integration & latency tuning | C++ | 6 h | HW1–HW5 | `aiming_node` ties graph together inside `hw_runner` | silver |
| HW7 *(bonus)* | Strategy / target prioritization vs `gold` bot | C++ + Python | 12 h | HW6 | `Strategy::pick_target()` + optional behaviour-tree DSL | gold |

Total: ≈ 58 h for the required HW1–HW6, plus ≈ 12 h for the HW7 bonus, for a strong candidate. We communicate the assignment as a 2-week timebox so weak candidates surface honestly; the HW7 bonus is clearly labeled as optional in the candidate-facing README.

---

## 5. HW deep-dives

### HW1 — Lightweight armor & icon detector

* **Why a lightweight model, not YOLO**: per the team's note, latency dominates on the embedded Orin. We replace YOLO with **anchor-free MobileNetV3-Small + a multi-task head** (bbox + 4 keypoint corners + 7-way icon classifier), modeled on [PicoDet](https://arxiv.org/abs/2111.00902) / [NanoDet](https://github.com/RangiLyu/nanodet). That makes the assignment substantively different from the production stack while still being deployable.
* **Provided**:
  * `train.py` skeleton with backbone instantiated, loss hooks empty.
  * `dataset.py` that pulls 5k synthetic armor frames generated by a Godot data-dumping mode (publishes label.json next to each frame).
  * `export_onnx.py` validated round-trip.
  * C++ `inferer.cpp` with ONNX Runtime session set up, candidate fills `decode_outputs()` and NMS.
* **`TODO` previews**:
  ```python
  # train.py
  loss_box  = TODO_l1_or_giou(pred_box,  target_box)
  loss_kpt  = TODO_keypoint_smooth_l1(pred_kpt, target_kpt, mask=visible)
  loss_cls  = TODO_focal_or_ce(pred_cls,   target_cls)
  loss      = w1*loss_box + w2*loss_kpt + w3*loss_cls
  ```
  ```cpp
  // inferer.cpp
  for k in range(0, num_proposals):
      score = sigmoid(raw_score[k])
      if score < score_thresh: continue
      decoded_box = decode_box(raw_box[k], anchor[k])      // TODO: implement DFL or LTRB decode
      decoded_kpt = decode_keypoints(raw_kpt[k], anchor)   // TODO
      proposals.push_back({score, decoded_box, decoded_kpt, argmax(raw_cls[k])})
  apply_class_aware_nms(&proposals, iou_thresh)            // TODO
  ```
* **Performance signal**: mAP@0.5 on a held-out set of 1000 simulator frames + per-frame inference latency. Stretch target: CPU-only run within 8 ms/frame.
* **Reference**: [DeanFrancis YOLOv11n+DeepSORT](https://www.deanfrancispress.com/index.php/te/article/view/3583), [arXiv:2312.05055](https://arxiv.org/html/2312.05055v1), [illini-robomaster/irmv_detection](https://github.com/illini-robomaster/irmv_detection) for ONNX-on-edge patterns.

### HW2 — TF graph

* **Why split this off**: most candidates underestimate timestamp synchronisation, frame chains, and gimbal-IMU offsets. A small dedicated HW forces them to think.
* **Provided**: a pure-header `tf::Buffer` skeleton; the candidate must fill `lookup_transform(target, source, t)` and `set_transform(parent, child, T, t)`.
* **`TODO` previews**:
  ```cpp
  Quaterniond slerp_interpolate(stamp_a, stamp_b, t):
      alpha = (t - stamp_a) / (stamp_b - stamp_a)
      // TODO: short-arc slerp, fall back to nlerp if dot < eps
  Affine3d compose(parent_to_world, child_to_parent):
      // TODO: matrix multiply, double-check homogeneous order
  ```
* **Performance signal**: replay a 60-second logged trajectory, compare reconstructed `armor_in_world` against ground truth — RMSE bound.
* **Reference**: ROS2 TF2 docs (we deliberately do *not* use ROS2 here so candidates write the math themselves).

### HW3 — EKF tracker

* **Story arc**: the candidate first builds a constant-velocity (CV) EKF on a single target (easy), then extends to a CV+CT (constant-turn) IMM filter (hard), then handles target dropout / multi-target data association via Hungarian assignment (bonus). This mirrors the maneuvering-target literature, e.g. [trackingIMM (MathWorks)](https://www.mathworks.com/help/fusion/ref/trackingimm.html), [MDPI 2024 IMM survey](https://www.mdpi.com/1999-4893/17/9/399).
* **Math we'll print in the README** (LaTeX): predict step, update step, Joseph-form covariance update, IMM mixing equations, NIS gating threshold derivation.
* **`TODO` previews**: same EKF skeleton shown in §4 (the "for k in range(0,N)" snippet).
* **Performance signal**: NEES / NIS chi-squared coverage, position RMSE under (low / medium / high) target maneuver intensity, plus end-to-end aim error when fed to a fixed downstream HW4 baseline.
* **Why we de-emphasize off-the-shelf libraries**: candidates may not import ROS robot_localization or filterpy; they must implement the matrix arithmetic in Eigen. We provide a unit-test fixture with reference outputs from a Python prototype so they can compare numerically.

### HW4 — Ballistic predictor & firing-delay compensator

* **Model**: 2D-projectile-with-drag ODE integrated to first impact with a moving target plane, then an iterative scheme for the firing-delay loop:
  ```python
  for iter in range(K):
      t_flight   = solve_quadratic_or_rk4(distance, gravity, drag)
      t_fire_total = t_flight + t_actuator_lag + t_pipeline_lag
      target_at_impact = ekf.predict_forward(t_fire_total)
      distance = ‖target_at_impact - barrel‖
      if converged(distance): break
  ```
* **Provided**: a deterministic 1-D test harness (target moves in a straight line, no drag), a 2-D harness with gravity, a 3-D harness with drag, plus the production team's `BallisticSolver` interface header to plug into.
* **`TODO` preview**:
  ```cpp
  for it in range(0, max_iter):
      t_flight = TODO_solve_flight_time(barrel, target_pos, muzzle_v, drag, g)
      t_total  = t_flight + total_actuator_delay
      target_pos = ekf_state.predict(target_pos, target_vel, t_total)
      err = norm(target_pos - last_pos)
      if err < tol: break
      last_pos = target_pos
  ```
* **Reference reading we cite in the HW README** (without giving away the answer): [Predictive Aim Mathematics for AI Targeting](https://www.gamedeveloper.com/programming/predictive-aim-mathematics-for-ai-targeting), [Kevin Brennan — Projectile Prediction Math](https://kmgb.github.io/blog/projectile-prediction).
* **Performance signal**: hit-rate vs target speed, maximum effective range, and end-to-end aim accuracy in a closed-loop episode.

### HW5 — MPC gimbal controller

* **Why MPC, not LQR/PID**: we want to assess optimization-based control. We use **acados** (open-source, embedded-friendly C, with CasADi front-end). The candidate writes the model and cost in CasADi (Python), runs `acados`'s code-gen, then links the generated C library into a small C++ shim — exactly the workflow used in [uzh-rpg/rpg_mpc](https://github.com/uzh-rpg/rpg_mpc) for quadrotors, and described in the [acados docs](https://docs.acados.org/) and [CasADi paper](https://web.casadi.org/).
* **Provided**: a fully working **PID controller** as the lower-bound baseline + a "naive double integrator" MPC so candidates only need to extend the model.
* **`TODO` preview**:
  ```python
  # mpc/model.py — CasADi expressions
  yaw, pitch, yaw_dot, pitch_dot = state
  yaw_ddot   = TODO_dynamics_with_motor_torque_lag(...)
  pitch_ddot = TODO_dynamics_with_gravity_compensation(...)
  ```
  ```cpp
  // mpc/wrap.cpp — fill the cost weight matrices
  W = block_diag( w_yaw_err, w_pitch_err,
                  w_yaw_rate, w_pitch_rate,
                  w_torque, w_torque_rate );
  // TODO: tune horizon N=20, dt=20ms; stage cost vs terminal cost
  ```
* **Performance signal**: tracking error on a sinusoidal reference + actuator-saturation rate + closed-loop step response within spec. We deliberately publish a ground-truth recorded trajectory so candidates can iterate offline.
* **Reference**: [Rawlings, Mayne, Diehl — *Model Predictive Control: Theory, Computation, and Design*](https://sites.engineering.ucsb.edu/~jbraw/mpc/MPC-book-2nd-edition-3rd-printing.pdf), [Mehrez MPC/MHE workshop](https://github.com/MMehrez/MPC-and-MHE-implementation-in-MATLAB-using-Casadi).

### HW6 — Integration

* Wire HW1..HW5 together inside the `hw_runner` C++ binary that talks gRPC to the simulator.
* Tasks: thread management, dropping stale frames, lock-free ring buffer between detector and EKF, `std::chrono` watchdog for stale gimbal feedback.
* **`TODO` preview**:
  ```cpp
  // pipeline/main.cpp
  while (running):
      auto frame = camera_buf.try_pop();
      if (!frame.has_value()): continue
      auto dets   = detector.run(frame->image);
      tf.update(frame->stamp, gimbal_state.latest());
      auto track  = ekf.update(dets, tf);
      auto aim    = ballistic.aim(track, gimbal_state.latest());
      auto cmd    = mpc.step(aim, gimbal_state.latest());
      grpc_send(cmd);
      if (aim.fire_now): grpc_fire();
  ```
* **Performance signal**: closed-loop episode performance against the `silver` bot; latency budget compliance for the full pipeline.

### HW7 — Strategy & game-theoretic decision *(bonus)*

* **Status**: Bonus per §10 decision 2. A candidate who submits nothing for HW7 is not disadvantaged on HW1–HW6.
* The candidate writes a tiny behaviour-tree or rule-based DSL that decides target priority among multiple visible enemies, when to break engagement, when to push for cover. Optionally trains a small policy with PPO using the same simulator (this is the part we frame as "model-based + RL hybrid").
* **`TODO` preview**:
  ```cpp
  Action choose(GameState s):
      visible = detector.targets()
      if any(plate.health < threshold for plate in visible):
          return engage(weakest_plate)        // TODO: weight by distance, line-of-sight
      if take_damage_recently(s, dt=1.0):
          return retreat_to_cover(s)          // TODO: pick nearest cover via tf graph
      return patrol(s)
  ```
* **Performance signal**: best-of-5 vs the `gold` bot. Stretch: a 2v2 mode where the candidate's bot also commands an ally NPC.
* **Reference**: [WILD-SCAV / PMCA / Arena Breakout](https://arxiv.org/html/2410.04936v1) for FPS-RL prior art; [PettingZoo self-play tutorial](https://pettingzoo.farama.org/main/tutorials/agilerl/curriculums_and_self_play/).

---

## 6. Toolchain plan

### 6.1 Python — `uv`

Each Python project ships a `pyproject.toml` and `uv.lock`. Standard ergonomics:

```bash
# one-time
curl -LsSf https://astral.sh/uv/install.sh | sh

# per-project
uv sync             # creates .venv, installs from lockfile
uv run train.py     # runs inside the venv without activation
uv add torch        # adds + locks
```

* `uv` is cross-platform (handles Windows path edge cases properly) per [Astral docs](https://docs.astral.sh/uv/guides/projects/) and [Real Python's uv guide](https://realpython.com/python-uv/).
* For HWs whose C++ build must call into Python, we adopt [`basis-robotics/uvtarget`](https://github.com/basis-robotics/uvtarget) so CMake `find_package(Python)` is replaced by `uv_initialize(...)`. This avoids the classic "wrong Python on the path" pitfall on macOS.

### 6.2 C++ — CMake

```cmake
cmake_minimum_required(VERSION 3.22)
project(aiming_hw LANGUAGES CXX)
set(CMAKE_CXX_STANDARD 20)

include(FetchContent)
FetchContent_Declare(uvtarget
    GIT_REPOSITORY https://github.com/basis-robotics/uvtarget GIT_TAG v0.1.0)
FetchContent_MakeAvailable(uvtarget)
include(${uvtarget_SOURCE_DIR}/Uv.cmake)
uv_initialize(...)

find_package(Eigen3 3.4 REQUIRED)
find_package(Protobuf CONFIG REQUIRED)
find_package(gRPC CONFIG REQUIRED)
find_package(onnxruntime CONFIG REQUIRED)

add_subdirectory(detector)
add_subdirectory(tracker)
add_subdirectory(ballistic)
add_subdirectory(mpc)
add_subdirectory(pipeline)

enable_testing()
add_subdirectory(tests)
```

We document **`vcpkg`** (Win/macOS/Linux) as the canonical dependency manager so first-time candidates don't fight Eigen/gRPC installs. As a fallback we provide a **prebuilt Docker image** with the full toolchain; candidates can `docker compose up dev` to develop in the same image locally.

### 6.3 Reference Docker image (for reproducibility)

```
aiming-hw-toolchain:2026.04
   └── Ubuntu 22.04 + CUDA 12.2 (optional GPU)
       + Python 3.11 (uv-managed)
       + g++-12, clang-15, cmake 3.27
       + Eigen 3.4, gRPC 1.60, ONNX Runtime 1.18, acados latest
       + Godot 4 headless arena binary
```

This image is published once per HW release and pinned by digest. Its purpose is **toolchain reproducibility** — anyone (candidate or team) building the codebase inside this image gets a byte-identical environment, so behaviour doesn't depend on whichever local OS/CUDA the developer happens to have. How this image is later used inside a grading workflow is a §7 concern.

---

## 7. Grading & leaderboard *(deferred)*

This section is intentionally a stub. We will design the grading workflow, the score formula, the leaderboard, and the anti-cheat posture **after the HW1–HW7 scaffolds exist** (Stages 1–9 of [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md)). Designing grading first risks shaping the homework around a guessed scoring scheme; designing it after lets us pick a policy that fits what we actually built and how candidates actually behave during the pilot.

What we *have* committed to and that downstream design will inherit:

* **Episode telemetry schema** (§1.3) — every match emits a structured `episode.json` with `win_flag`, damage dealt/taken, hit accuracy, aim latency, projectile metadata. Whatever score formula we eventually pick will be a function over this object.
* **Engine and protocol** (§§2–3) — gRPC + ZMQ contract is fixed; any grading harness, wherever it runs, talks the same wire format.
* **Reference Docker image** (§6.3) — provides toolchain reproducibility for both candidate dev and any future grading run.
* **Per-HW "performance signal" definitions** (§5) — these describe what each HW *measures*. The grading score is a weighted combination of these signals, weights TBD.

Open questions intentionally left unanswered until Stage 10:

* Where grading runs (candidate's machine vs the team's Orin NX vs a TA's laptop vs a cloud GPU).
* How candidates submit (committed `submissions/`, PR-driven, OSS upload, email).
* What the leaderboard looks like (internal vs public, real-time vs nightly, web vs CSV).
* Anti-cheat posture (honor system, signed score JSONs, server-authoritative regrade).
* Whether HW7 contributes to the recruiting ranking or sits in a separate stretch column.

Track the grading discussion in a future PR titled `design: grading workflow v1`.

---

## 8. Phased delivery roadmap

| Milestone | Date (proposed) | Deliverables | Owner |
|---|---|---|---|
| **M0** — Engine PoC | Week 1–2 | Godot FPS scene with one chassis, gimbal, plate physics; gRPC echo server; ZMQ frame stream; one shooting demo replay | Engine lead |
| **M1** — HW1 + HW2 ready | Week 3–4 | Detector training pipeline + ONNX export; TF buffer; bronze-tier bot trained; reference Docker image v0.1 | Vision + bot leads |
| **M2** — HW3 + HW4 ready | Week 5–6 | EKF + ballistic; silver-tier bot; episode-driver harness with auto-replay attachment | Tracker + ballistic leads |
| **M3** — HW5 ready, visual review | Week 7–8 | MPC; lighting / VFX pass; **decision gate** (per §10 decision 1): commit to whichever engine has the higher visual quality at this milestone. First wave can ship on Godot; if the visual bar isn't met, the second wave swaps to Unity. | Control lead + art |
| **M4** — HW6 + HW7 ready | Week 9–11 | Full integration; gold-tier bot via 3-day self-play training; behaviour tree DSL | All |
| **M5** — Grading workflow design | TBD | Once HW scaffolds exist, design the grading workflow, score formula, leaderboard, and anti-cheat posture (see §7). Calendar slot intentionally not committed yet. | TBD |
| **M6** — Pilot + launch | TBD | Pilot through internal volunteers, then open the recruitment cycle. Calendar slot depends on M5 outcome. | TBD |

Sub-milestones are intentionally split so HW1–HW4 can land as a **"first wave"** in week 7 if we fall behind on HW5–HW7; that still covers vision/EKF/ballistic, the bulk of the technical signal.

**v2 stretch goals (deferred from v1)**

| Stretch | Notes |
|---|---|
| **Real-camera replay bridge** | Feed pre-recorded RoboMaster RGB streams through the same gRPC/ZMQ contract the simulator uses, so the candidate's stack can be exercised on real-hardware footage in addition to simulator footage. Held back to keep v1 surface area manageable; revisit after M4. |

---

## 9. Risks & mitigations

| Risk | Probability | Impact | Mitigation |
|---|---|---|---|
| Godot visuals fail "scenically attractive" bar at M3 | Medium | High | Prebuild Unity HDRP port in parallel from week 5; keep gameplay logic in our protocol so the swap is data-only. |
| Gold-tier RL bot doesn't converge to interesting behaviour | Medium | Medium | Cap training at 3 days; if it stalls, hand-script the gold bot from a behaviour tree augmented by silver's PPO policy as a sub-skill. |
| Cross-platform pain on Windows (especially gRPC + acados) | High | Medium | Provide a WSL2 escape hatch in the README; the reference Docker image always provides a known-good Linux toolchain. |
| Candidate exploits the simulator (e.g., walks through walls) | Low | Low | Server-authoritative physics; client commands are clamped; replays let us audit. |
| Detection task collapses to memorising synthetic data | Medium | Medium | Domain-randomize lighting, colour, camera intrinsics, projectile occlusion in HW1 dataset; include 100 real-world RoboMaster frames in the test split. |
| `uv` / `uvtarget` regressions on Windows | Low | Low | Pin `uv` and `uvtarget` versions in `cmake/UvFetch.cmake`. |
| Detector / EKF / MPC are too slow on a candidate's CPU | Medium | Medium | The detector and EKF/MPC are designed to run on CPU within the latency budget; reference image carries CUDA-optional ONNX-RT and acados. Profile + tune during M2/M3. |

---

## 10. Resolved decisions

Decisions whose impact lives entirely inside the **homework design** (this schema). Decisions whose impact is grading-specific have been removed from this table and will be re-introduced when §7 is designed.

| # | Question | Decision | Reflected in |
|---|---|---|---|
| 1 | **Engine choice gate** — Godot primary with Unity fallback at M3, or commit to Unity from the start? | Commit at M3 to **whichever engine has the higher visual quality**. First wave ships on Godot; if the visual bar isn't met, the second wave swaps to Unity. The protocol-buffer schema keeps gameplay logic engine-independent so the swap is a data-only port. | §0 TL;DR · §2.2 · §8 (M3) · §9 (engine risk) |
| 2 | **Difficulty cap** — is HW7 required, or stretch only? | **HW7 is a bonus.** A candidate who skips HW7 entirely is not disadvantaged on HW1–HW6. How (or whether) HW7 contributes to a final ranking is a §7-level question. | §0 TL;DR · §4 (table) · §5 (HW7 deep-dive) |
| 3 | **Real-camera replay bridge** — expose a "real camera replay" mode for candidates with hardware access? | **Not in v1.** Tracked as a v2 stretch goal once the core pipeline is stable; same gRPC/ZMQ contract will be reusable. | §8 (v2 stretch goals) |
| 4 | **Language for HW READMEs** — English-primary or Chinese-primary? | **Chinese primary** with a short English summary block at the top of each README. Math, code, and pseudocode stay language-neutral. The schema and team-facing docs (this file) remain in English. | Front-matter · §4 (item 0 of HW README spec) |
| 5 | **Generative-AI policy** — explicit ban + detection, or none? | **No explicit policy and no automated detection.** The code-completion shape of the assignment — small, well-bounded `TODO` holes inside a working scaffold — is itself the deterrent: a candidate who fills holes by hand finishes nearly as fast as one prompting an LLM. | §7 (deferred) |
| 6 | **Candidate-facing repo path** — where does the assignment live? | **[`www-David-dotcom/TsingYun_Tutorial_Vision-Aiming`](https://github.com/www-David-dotcom/TsingYun_Tutorial_Vision-Aiming).** Candidates fork it; submission flow (PR vs commit-to-fork vs other) is part of §7. | Front-matter |
| 7 | **OSS asset hosting** — where do Godot binaries, datasets, opponent weights, and the reference Docker image live? | **Aliyun OSS in `cn-beijing`**, three buckets: `tsingyun-aiming-hw-public` (anonymous-read for binaries and datasets), `tsingyun-aiming-hw-models` (private, SSE-OSS encryption, holds opponent weights and reference detector ONNX), `tsingyun-aiming-hw-cache` (private, holds the reference Docker image and build caches). Access pattern for the private buckets — shared AccessKey vs RAM role vs presigned URL — is a §7 concern, decided once we know whether grading runs candidate-side or team-side. | IMPLEMENTATION_PLAN.md §Resolved decisions |

Future decisions or revisions should be appended below this table with a date and a short note on what changed. Grading-specific decisions (where grading runs, leaderboard topology, anti-cheat posture, score formula) will be added when §7 is filled in.

---

## 11. References

### FPS / multi-agent RL platforms
* Edward Beeching et al., **Godot RL Agents** — [arXiv:2112.03636](https://arxiv.org/abs/2112.03636) · [GitHub](https://github.com/edbeeching/godot_rl_agents) · [Hugging Face DeepRL course unit](https://huggingface.co/learn/deep-rl-course/en/unitbonus3/godotrl)
* Unity Technologies, **ML-Agents** — [GitHub](https://github.com/Unity-Technologies/ml-agents) · [ML-Agents gRPC fork](https://github.com/Unity-Technologies/ml-agents-grpc) · Custom gRPC sample [mr-grpc-unity](https://github.com/OpenAvikom/mr-grpc-unity)
* Epic Games, **Learning Agents** plugin — [Unreal docs](https://dev.epicgames.com/documentation/en-us/unreal-engine/API/PluginIndex/LearningAgents) · port: [UnrealMLAgents](https://github.com/AlanLaboratory/UnrealMLAgents)
* Microsoft, **AirSim** — [home](https://microsoft.github.io/AirSim/) · [RL examples](https://github.com/Microsoft/AirSim/tree/main/PythonClient/reinforcement_learning) (note: archived in favour of Project AirSim)
* **MindMaker**, [GitHub](https://github.com/krumiaa/MindMaker)
* Petrenko et al., **Sample Factory: Egocentric 3D Control from Pixels at 100000 FPS** — [arXiv:2006.11751](https://arxiv.org/abs/2006.11751) · [docs](https://www.samplefactory.dev/) · [GitHub](https://github.com/alex-petrenko/sample-factory)
* Petrenko et al., **Megaverse: Simulating Embodied Agents at One Million Experiences/sec** — [paper](https://wijmans.xyz/publication/megaverse/) · [arXiv:2107.08170](https://arxiv.org/pdf/2107.08170)
* NVIDIA, **Isaac Lab** — [paper](https://research.nvidia.com/publication/2025-09_isaac-lab-gpu-accelerated-simulation-framework-multi-modal-robot-learning) · [GitHub](https://github.com/isaac-sim/IsaacLab)
* **VizDoom** retrospective and [WILD-SCAV / Arena Breakout PMCA](https://arxiv.org/html/2410.04936v1)
* Farama Foundation, **PettingZoo** — [docs](https://pettingzoo.farama.org/) · [paper](https://arxiv.org/abs/2009.14471)

### RoboMaster aiming literature & opensource
* arXiv:2312.05055, [*Design and Implementation of Automatic Assisted Aiming System for RoboMaster EP*](https://arxiv.org/html/2312.05055v1)
* Dean Francis Press 2025, [*RoboMaster Robot Automatic Targeting System Based on YOLOv11n and DeepSORT*](https://www.deanfrancispress.com/index.php/te/article/view/3583)
* [SEU-SuperNova-CVRA Robomaster2018](https://github.com/SEU-SuperNova-CVRA/Robomaster2018-SEU-OpenSource)
* [illini-robomaster/irmv_detection](https://github.com/illini-robomaster/irmv_detection)
* [RoboMaster/RoboRTS](https://github.com/RoboMaster/RoboRTS), [RoboMaster OSS organisation](https://github.com/robomaster-oss)
* [zhuodannychen/Robomaster-ComputerVision](https://github.com/zhuodannychen/Robomaster-ComputerVision)
* [GOFIRST-Robotics/RoboMaster-2024](https://github.com/GOFIRST-Robotics/RoboMaster-2024)

### Estimation, control, math
* Bar-Shalom, Li, Kirubarajan — *Estimation with Applications to Tracking and Navigation* (textbook reference for EKF/IMM)
* MathWorks Sensor Fusion Toolbox — [Tracking Maneuvering Targets](https://www.mathworks.com/help/fusion/ug/tracking-maneuvering-targets.html), [trackingIMM](https://www.mathworks.com/help/fusion/ref/trackingimm.html)
* MDPI 2024, [IMM Filtering Algorithms for a Highly Maneuvering Fighter Aircraft](https://www.mdpi.com/1999-4893/17/9/399)
* Rawlings, Mayne, Diehl — [*Model Predictive Control: Theory, Computation, and Design*](https://sites.engineering.ucsb.edu/~jbraw/mpc/MPC-book-2nd-edition-3rd-printing.pdf)
* CasADi — [home](https://web.casadi.org/) · [paper](https://optimization-online.org/wp-content/uploads/2018/01/6420.pdf)
* acados — [docs](https://docs.acados.org/)
* uzh-rpg quadrotor MPC — [GitHub](https://github.com/uzh-rpg/rpg_mpc)
* Mehrez MPC/MHE workshop — [GitHub](https://github.com/MMehrez/MPC-and-MHE-implementation-in-MATLAB-using-Casadi)
* PlayTechs blog, [Aiming a projectile at a moving target](http://playtechs.blogspot.com/2007/04/aiming-at-moving-target.html)
* Game Developer, [Predictive Aim Mathematics for AI Targeting](https://www.gamedeveloper.com/programming/predictive-aim-mathematics-for-ai-targeting)
* Kevin Brennan, [Projectile Prediction Math](https://kmgb.github.io/blog/projectile-prediction)

### Tooling and build
* Astral `uv` — [docs](https://docs.astral.sh/uv/) · [Real Python guide](https://realpython.com/python-uv/) · [Complete guide](https://pydevtools.com/handbook/explanation/uv-complete-guide/)
* basis-robotics, [`uvtarget`](https://github.com/basis-robotics/uvtarget) — uv ↔ CMake glue
* ZeroMQ + NetMQ, gRPC: [2026 Python↔Unity Protocol Guide](https://copyprogramming.com/howto/protocol-to-communicate-between-python-and-unity)

(Grading-platform references — GitHub Classroom autograding, Actions Runner Controller, EvalAI / Codabench — will be re-added when §7 is designed.)

---

## Appendix A — Suggested directory layout

```
Aiming_HW/
├── schema.md                   # this document
├── IMPLEMENTATION_PLAN.md      # per-stage delivery plan
├── README.md                   # candidate-facing overview
├── shared/
│   ├── proto/                  # aiming.proto and codegen rules
│   ├── docker/                 # reference toolchain Dockerfile
│   ├── godot_arena/            # the simulator project
│   │   ├── project.godot
│   │   ├── scenes/...
│   │   ├── scripts/...
│   │   └── export_presets.cfg
│   ├── opponents/              # populated by fetch_assets.py from the OSS models bucket
│   └── cmake/                  # FetchContent helpers, UvFetch.cmake
├── HW1_armor_detector/
├── HW2_tf_graph/
├── HW3_ekf_tracker/
├── HW4_ballistic/
├── HW5_mpc_gimbal/
├── HW6_integration/
└── HW7_strategy/

# A `grader/` directory will be added when §7 is designed.
```

## Appendix B — Open-source reuse summary

| Component we reuse | License | Why we can ship it |
|---|---|---|
| Godot 4 engine | MIT | freely redistributable |
| Godot RL Agents | MIT | freely redistributable |
| Sample Factory 2 | MIT | freely redistributable |
| PettingZoo / Gymnasium | MIT | freely redistributable |
| ONNX Runtime | MIT | freely redistributable |
| Eigen 3 | MPL2 | header-only, weak copyleft fine for distribution |
| gRPC | Apache-2.0 | freely redistributable |
| acados | LGPL-3.0 | dynamic-link only — we link as a shared library at runtime |
| CasADi | LGPL-3.0 | same as acados |
| Kenney sci-fi assets | CC0 | freely redistributable |

---

*End of schema v0.4 — homework design only; grading workflow and leaderboard policy live in §7 as a deferred stub until the HW scaffolds (Stages 1–9 in IMPLEMENTATION_PLAN.md) are built. Comments, corrections, and disagreements welcome.*
