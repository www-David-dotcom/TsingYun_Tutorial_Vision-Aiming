# Unity-First Assignment Path

`schema.md` is the source of truth. `docs/unity-wire-contract.md` is the
candidate runner wire contract. HW1-HW7 remain in the repository as
implementation/reference folders, but the active assignment path is organized
around the Unity arena and partial mini-tests.

## Candidate Flow

1. Start with mini-tests for the current stage.
2. Implement only the `TODO(HWn):` blanks for that stage.
3. Run the stage mini-test command.
4. Move to the next stage after the local mini-test passes.
5. Use the Unity smoke only after A6 runner integration is present.

## Active Stages

| Stage | Folder | Unity-first assignment path | Mini-test |
|---|---|---|---|
| A1 Perception | `HW1_armor_detector` | Detect armor plates and vehicle IDs from Unity camera frames. | `uv run pytest HW1_armor_detector/tests/public/test_assign_targets.py HW1_armor_detector/tests/public/test_loss_shapes.py` |
| A2 Frame Geometry | `HW2_tf_graph` | Maintain world, chassis, gimbal, and camera transforms for Unity observations. | `ctest --preset linux-debug -R hw2` |
| A3 Tracking | `HW3_ekf_tracker` | Smooth detections into target tracks for aim prediction. | `ctest --preset linux-debug -R hw3` |
| A4 Ballistics | `HW4_ballistic` | Convert target tracks into lead-compensated aim directions. | `ctest --preset linux-debug -R hw4` |
| A5 Gimbal Control | `HW5_mpc_gimbal` | Track yaw and pitch commands with PID baseline and optional MPC. | `ctest --preset linux-debug -R hw5`; `uv run pytest HW5_mpc_gimbal/tests/public/test_cost.py` |
| A6 Runner Integration | `HW6_integration` | Connect frame ingestion, perception, tracking, aiming, and Unity control. | `ctest --preset linux-debug -R hw6` |
| A7 Strategy Bonus | `HW7_strategy` | Choose targets and retreat behavior for teammate automation. | `ctest --preset linux-debug -R hw7` |

## Legacy-Only Material

The old synthetic-only dataset flow, old CI leaderboard process, and non-Unity
arena assumptions are legacy reference. They can help students understand the
math, but they are not the final evaluation path.

## Mini-Test Inventory

### A1 Perception

- `HW1_armor_detector/tests/public/test_assign_targets.py`
- `HW1_armor_detector/tests/public/test_loss_shapes.py`
- `HW1_armor_detector/tests/public/test_post_process.cpp`
- `HW1_armor_detector/tests/public/test_export_roundtrip.py`

### A2 Frame Geometry

- `HW2_tf_graph/tests/public/test_basic_lookup.cpp`
- `HW2_tf_graph/tests/public/test_chain_compose.cpp`
- `HW2_tf_graph/tests/public/test_interpolation_corners.cpp`

### A3 Tracking

- `HW3_ekf_tracker/tests/public/test_cv_predict.cpp`
- `HW3_ekf_tracker/tests/public/test_imm_mode_probabilities.cpp`
- `HW3_ekf_tracker/tests/public/test_da_simple.cpp`

### A4 Ballistics

- `HW4_ballistic/tests/public/test_1d_no_drag.cpp`
- `HW4_ballistic/tests/public/test_2d_with_gravity.cpp`
- `HW4_ballistic/tests/public/test_3d_with_drag.cpp`

### A5 Gimbal Control

- `HW5_mpc_gimbal/tests/public/test_cost.py`
- `HW5_mpc_gimbal/tests/public/test_step_response.cpp`
- `HW5_mpc_gimbal/tests/public/test_sinusoid_tracking.cpp`

### A6 Runner Integration

- `HW6_integration/tests/public/test_ring_buffer.cpp`
- `HW6_integration/tests/public/test_watchdog.cpp`

### A7 Strategy Bonus

- `HW7_strategy/tests/public/test_bt_semantics.cpp`
- `HW7_strategy/tests/public/test_priority_distance.cpp`
- `HW7_strategy/tests/public/test_retreat_trigger.cpp`

## Mini-Test Policy

Milestone 3 does not add new algorithmic graders where a folder already has a
fast public mini-test. The milestone-level tests only guard documentation,
stage mapping, and TODO hygiene. New behavior tests should be added in the
future only when a stage introduces a new candidate-facing blank.

## Full Unity Smoke

After A6, candidates can smoke the local Unity runtime:

```bash
UV_CACHE_DIR=.uv-cache uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 10
```
