# HW5 — MPC 云台控制器 / MPC Gimbal Controller

> 第五道作业：用 MPC 控制两轴云台。模型在 CasADi 里写，
> 通过 acados 代码生成成 C 库，然后 C++ 运行时调它。还提供了一个
> PID 基线作为下限——你的 MPC 必须在跟踪指标上击败它。
> 三个 TODO：CasADi 模型里两个（电机扭矩 lag 动力学），
> 加 C++ 控制器里一个（把生成的 acados solver 接到运行时）。
>
> **Your job:** finish the CasADi model with a first-order motor-
> torque-lag dynamics, run the acados codegen once on a workstation
> that has acados installed, and wire the generated C library into
> the C++ runtime. The PID baseline is filled and is the floor your
> MPC must beat on tracking RMSE.

---

## 多语言 / Multi-language structure

```
HW5_mpc_gimbal/
├── pyproject.toml                 ← uv group `hw5`: casadi, acados-template
├── configs/mpc_weights.yaml       ← physics + diagonal cost weights
├── src/
│   ├── model.py                   ← CasADi model, two TODO(HW5) sites
│   ├── cost.py                    ← stage + terminal quadratic cost (filled)
│   ├── generate_acados.py         ← runs the codegen (team-side)
│   └── tune.py                    ← offline PD-baseline sweep (no acados needed)
├── generated_solver/              ← populated by acados; gitignored beyond README
├── include/aiming_hw/mpc/
│   ├── pid_baseline.hpp           ← filled
│   └── controller.hpp             ← filled (header), .cpp has one TODO
├── source/
│   ├── pid_baseline.cpp           ← filled
│   └── controller.cpp             ← TODO(HW5): wire acados solver
├── tests/public/
│   ├── test_step_response.cpp     ← PID baseline: settling < 200ms, overshoot < 5%
│   └── test_sinusoid_tracking.cpp ← PID baseline: RMSE < 0.05 rad on 1 Hz / 0.5 rad
└── docs/visual_review_2026-04-29.md ← engine quality gate template
```

---

## 工作流 / Workflow

### 1. The PID baseline already works

```bash
docker compose -f shared/docker/toolchain.compose.yaml run --rm dev \
    bash -c "cmake --preset linux-debug && cmake --build --preset linux-debug && ctest --preset linux-debug -R hw5"
```

`hw5_step_response_test` and `hw5_sinusoid_tracking_test` should both
pass on a fresh checkout — they test the PID baseline only.

### 2. Tune the model offline

```bash
uv sync --group hw5
uv run python HW5_mpc_gimbal/src/tune.py --plot
```

The tune script runs a closed-loop PD simulation against the same
motor-lag dynamics the MPC will see, so you can sweep gain values
without waiting for acados codegen.

### 3. Fill the model TODOs

Open `src/model.py`. Two TODO sites:

* `motor_torque_lag(...)` — first-order lag from torque command to
  applied torque. Two CasADi expressions, one per axis.
* `state_dot(...)` — call `motor_torque_lag` to fill the last two
  components of the 6-vector return.

Validate without acados:

```bash
uv run python HW5_mpc_gimbal/src/generate_acados.py --check
```

Expected: prints the dynamics function signature, stage + terminal
cost shapes, and `OK`.

### 4. Run acados codegen (team-side workflow)

This step needs the upstream **acados C library** installed (see
[acados.org](https://docs.acados.org/installation/index.html)). The
team does it once per weight change and uploads the resulting tarball
to OSS:

```bash
uv run python HW5_mpc_gimbal/src/generate_acados.py \
    --weights HW5_mpc_gimbal/configs/mpc_weights.yaml
# generated_solver/acados_aiming_mpc/CMakeLists.txt now exists.

uv run python shared/scripts/push_assets.py \
    --bucket tsingyun-aiming-hw-models \
    --key-prefix assets/HW5/acados_solver_v1.1/ \
    HW5_mpc_gimbal/generated_solver/
```

Candidates pull instead of generate:

```bash
uv run python shared/scripts/fetch_assets.py --only acados-solver-hw5-v1.1
# Symlink it into place:
ln -sfn out/assets/HW5/acados_solver_v1.1 HW5_mpc_gimbal/generated_solver
```

### 5. Fill the C++ TODO

`source/controller.cpp::MpcController::step` has a step-by-step
comment block. Once filled, the MPC runs at 100 Hz on the team's
reference workstation; HW6's runner can swap PID for MPC via a
config flag.

---

## 验收 / Acceptance

| Check | How |
|-------|-----|
| Model + cost compile under CasADi | `uv run python src/generate_acados.py --check` |
| PID baseline settles < 200 ms / overshoot < 5% | `ctest -R hw5_step_response` |
| PID baseline tracks 1 Hz sinusoid (RMSE < 0.05 rad) | `ctest -R hw5_sinusoid_tracking` |
| acados codegen produces a buildable C library | `cmake --preset linux-debug` after step 4 succeeds |
| MPC beats PID on the same sinusoid (lower RMSE) | candidate-authored test once their solver is wired |

---

## 已知非范围 / Out of scope

* Online weight tuning (the candidate edits the YAML and re-runs
  codegen; no Bayesian-opt loop).
* Hardware-in-the-loop on a real gimbal — production team's concern.
* CUDA EP — acados is CPU only.
* Hidden grading episodes — deferred per `IMPLEMENTATION_PLAN.md`
  Stage 10.
* Engine quality gate (Godot → Unity port) — separately tracked in
  `docs/visual_review_2026-04-29.md`.
