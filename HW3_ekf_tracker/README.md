# HW3 — EKF + IMM 多目标跟踪器 / EKF + IMM Multi-Target Tracker

> **Status:** Active as part of the Unity-first assignment path; older
> standalone workflows in this folder are legacy reference.
>
> **Unity-first role:** Smooth armor detections into target tracks for aim
> prediction.
>
> **Legacy-only:** CSV fixture replay remains a mini-test harness, not a full
> live-arena tracker evaluation.
>
> **Mini-test:** `ctest --preset linux-debug -R hw3`
>
> **Mini-test files:**
> - `HW3_ekf_tracker/tests/public/test_cv_predict.cpp`
> - `HW3_ekf_tracker/tests/public/test_imm_mode_probabilities.cpp`
> - `HW3_ekf_tracker/tests/public/test_da_simple.cpp`

> 第三道作业：在 HW1 检测器的输出上接一个扩展卡尔曼滤波器（EKF）+
> 交互多模 IMM（CV + CT），再加上多目标 Hungarian 关联。所有的数学
> 都已经在 `reference/ekf_python.py` 里写好了——你的工作是把它在
> C++ 里复刻出来，重点是**Joseph 形式更新**和**IMM 的 mixing/blending**。
>
> **Your job:** four short C++ functions —
> `predict`, `update` (Joseph form),
> the IMM step (mixing → mode probability update → combination), and
> `mahalanobis_cost` + `hungarian_assign` for the multi-target
> association. The Python reference is the math spec, not a copy
> target — read it first, then write the C++.

---

## Student Quickstart

### Prerequisites

- Complete the root [First-time setup](../README.md#first-time-setup).
- Use the Docker toolchain for C++ mini-tests.
- Optional fixture regeneration needs `uv sync --group hw3`.

### What to implement

Fill the `TODO(HW3):` sites in:

- `HW3_ekf_tracker/source/kalman_step.cpp`
- `HW3_ekf_tracker/source/motion_models.cpp`
- `HW3_ekf_tracker/source/imm.cpp`
- `HW3_ekf_tracker/source/data_association.cpp`

Use `HW3_ekf_tracker/reference/ekf_python.py` as the numerical spec.

### Mini-test command

```bash
cmake --preset linux-debug
cmake --build --preset linux-debug
ctest --preset linux-debug -R hw3
```

### Expected first run

Before implementation, tests that depend on a blank can `GTEST_SKIP`. After
each `TODO(HW3):` block is filled, rerun the command and watch the related
skip become a pass.

### Before moving on

Run `ctest --preset linux-debug -R hw3` inside the Docker toolchain. The EKF,
IMM, and association suites should pass before starting ballistics.

## 文件 / Where to look

```
HW3_ekf_tracker/
├── README.md                              ← you are here
├── docs/ekf_derivation.md                 ← math reference (start here)
├── reference/
│   ├── ekf_python.py                      ← spec implementation
│   └── generate_fixtures.py               ← creates tests/fixtures/*.csv
├── tests/
│   ├── public/                            ← 3 GTest suites
│   └── fixtures/                          ← 1800-sample CSV trajectories
├── include/aiming_hw/ekf/
│   ├── motion_models.hpp                  ← filled (CV + CT + Q)
│   ├── kalman_step.hpp                    ← TODO target — predict + update
│   ├── imm.hpp                            ← TODO target — mixing/blending
│   ├── data_association.hpp               ← TODO target — Hungarian + gating
│   └── tracker.hpp                        ← filled — public API
└── source/                                ← matching .cpp for each header
```

---

## TODO 一览 / TODO summary

| File | Function | What |
|------|----------|------|
| [`source/kalman_step.cpp`](source/kalman_step.cpp) | `predict`             | x ← F x;  P ← F P Fᵀ + Q |
| [`source/kalman_step.cpp`](source/kalman_step.cpp) | `update`              | y, S, K, x ← x + K y, P ← Joseph(I - K H, R) |
| [`source/imm.cpp`](source/imm.cpp)                 | mixing / mode update / combination | three labelled blocks inside `Imm::step` |
| [`source/data_association.cpp`](source/data_association.cpp) | `mahalanobis_cost` + `hungarian_assign` | per-pair cost + minimum-cost matching with a χ² gate |

The reference `reference/ekf_python.py` shows the math for all four
above. `docs/ekf_derivation.md` writes out the equations cleanly —
go there first if you'd rather start with paper than code.

---

## 跑测试 / Running the tests

```bash
docker compose -f shared/docker/toolchain.compose.yaml run --rm dev \
    bash -c "cmake --preset linux-debug && cmake --build --preset linux-debug && ctest --preset linux-debug -R hw3"

# Or regenerate the fixtures first if you changed the noise/duration:
uv run python HW3_ekf_tracker/reference/generate_fixtures.py
```

Three test executables:

| Test | What it pins |
|------|--------------|
| `hw3_cv_predict_test` | analytic CV predict, covariance growth + symmetry, Joseph-form symmetry over 100 alternating steps |
| `hw3_imm_mode_probabilities_test` | μ sums to 1 + non-negative, straight line favours CV, ω = 4 rad/s curve favours CT |
| `hw3_da_simple_test` | trivial 1×1, off-diagonal optimum on a 2×2, χ² gate filters high-cost matches |

Each test detects the unfilled-TODO state via a sentinel call and
`GTEST_SKIP`s with a clear message, so the rest of the project's CI
stays green during stage close.

---

## 已知非范围 / Out of scope

* UKF / particle filter / GP-UKF — could become a future bonus.
* Nonlinear measurement models (HW3 uses linear H; HW6's runner
  feeds projected positions, not raw camera angles).
* JPDA, MHT, or other probabilistic data-association schemes —
  Hungarian + gating is what the production stack uses.
* Hidden grading — grading must be redesigned from `schema.md`.
