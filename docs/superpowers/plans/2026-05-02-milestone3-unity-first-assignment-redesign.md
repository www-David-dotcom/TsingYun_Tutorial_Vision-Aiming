# Milestone 3 Unity-First Assignment Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert HW1-HW7 from legacy standalone homework notes into a coherent Unity-first candidate assignment path, with old material clearly marked as legacy reference, all candidate blanks normalized to `TODO`, and lightweight mini-tests for partial progress.

**Architecture:** Keep the existing HW1-HW7 folders as the implementation/reference modules, but introduce an assignment map that defines the active Unity-first sequence and the role of each legacy module. Do not rewrite algorithms in this milestone; this milestone changes assignment framing, blank hygiene, and test entry points so future implementation work has stable surfaces. Add mini-test wrappers where an existing public test can validate partial progress without requiring the full Unity runtime.

**Tech Stack:** Markdown docs, C++/GTest, Python/pytest, CMake, uv, Unity wire contract docs.

---

## Scope And Non-Goals

This milestone is about assignment design, not game mechanics. Do not add the training-ground scene, RL policy, visual polish, or hidden grading. Those are Milestone 4 or later.

Do not delete HW1-HW7. They remain useful as math and scaffold references. The change is to mark which parts are active Unity-first candidate tasks and which are legacy-only.

## File Structure

Create or modify these files:

- Create `docs/assignment-redesign.md`
  - Owns the active Unity-first assignment sequence.
  - Maps HW1-HW7 to Unity gameplay capabilities.
  - Identifies legacy-only pieces and candidate-facing TODO surfaces.
  - Defines mini-test commands for partial task validation.

- Modify `README.md`
  - Replace the current short candidate-assignment note with a pointer to `docs/assignment-redesign.md`.
  - Keep the Unity quickstart intact.

- Modify `docs/grading.md`
  - Mark the old CI/leaderboard workflow as legacy.
  - Add a Milestone 3 grading stance: local mini-tests first, live Unity evaluation deferred.

- Modify `docs/architecture.md`
  - Align transport wording with `docs/unity-wire-contract.md` because the current diagram still describes gRPC/ZMQ while the active Unity transport is TCP JSON plus frame TCP.

- Modify every `HW*/README.md`
  - Add a standard status banner.
  - Explain how that HW contributes to the Unity-first candidate stack.
  - Mark legacy-only workflow parts explicitly.
  - Link to the relevant mini-test command.

- Modify candidate TODO files under HW1-HW7 so every candidate blank starts with an exact `TODO(HWn):` marker:
  - `HW1_armor_detector/src/losses.py`
  - `HW1_armor_detector/src/train.py`
  - `HW1_armor_detector/source/inferer.cpp`
  - `HW1_armor_detector/source/post_process.cpp`
  - `HW1_armor_detector/include/aiming_hw/detector/post_process.hpp`
  - `HW2_tf_graph/source/interpolate.cpp`
  - `HW2_tf_graph/source/buffer.cpp`
  - `HW2_tf_graph/include/aiming_hw/tf/interpolate.hpp`
  - `HW3_ekf_tracker/source/kalman_step.cpp`
  - `HW3_ekf_tracker/source/motion_models.cpp`
  - `HW3_ekf_tracker/source/data_association.cpp`
  - `HW3_ekf_tracker/source/imm.cpp`
  - `HW3_ekf_tracker/include/aiming_hw/ekf/*.hpp`
  - `HW4_ballistic/source/solver.cpp`
  - `HW4_ballistic/source/projectile_model.cpp`
  - `HW4_ballistic/include/aiming_hw/ballistic/solver.hpp`
  - `HW5_mpc_gimbal/src/cost.py`
  - `HW5_mpc_gimbal/src/model.py`
  - `HW5_mpc_gimbal/source/controller.cpp`
  - `HW6_integration/source/main.cpp`
  - `HW6_integration/source/runner.cpp`
  - `HW6_integration/source/watchdog.cpp`
  - `HW6_integration/include/aiming_hw/pipeline/runner.hpp`
  - `HW7_strategy/include/aiming_hw/strategy/behavior_tree.hpp`
  - `HW7_strategy/include/aiming_hw/strategy/strategy.hpp`
  - `HW7_strategy/source/strategy.cpp`

- Create `tests/test_assignment_design.py`
  - Fast no-Unity checks for assignment docs and TODO hygiene.
  - This is the milestone-level guard that prevents drift.

- Create `tests/test_assignment_mini_commands.py`
  - Fast no-Unity checks that README-documented mini-test commands point at existing files and known test names.

## Assignment Design Target

Use this active sequence in `docs/assignment-redesign.md` and HW READMEs:

| Active Stage | Legacy Folder | Unity-First Candidate Skill | Mini-Test Gate |
|---|---|---|---|
| A1 Perception | `HW1_armor_detector` | Detect armor plates and IDs from Unity camera frames. | `uv run pytest HW1_armor_detector/tests/public/test_assign_targets.py HW1_armor_detector/tests/public/test_loss_shapes.py` |
| A2 Frame Geometry | `HW2_tf_graph` | Maintain world/chassis/gimbal/camera transforms for Unity observations. | `ctest --preset linux-debug -R hw2` |
| A3 Tracking | `HW3_ekf_tracker` | Smooth armor detections into target tracks. | `ctest --preset linux-debug -R hw3` |
| A4 Ballistics | `HW4_ballistic` | Convert target tracks into lead-compensated aim directions. | `ctest --preset linux-debug -R hw4` |
| A5 Gimbal Control | `HW5_mpc_gimbal` | Track yaw/pitch commands with PID baseline and optional MPC. | `ctest --preset linux-debug -R hw5` and `uv run pytest HW5_mpc_gimbal/tests/public/test_cost.py` |
| A6 Runner Integration | `HW6_integration` | Connect frame ingestion, perception, tracking, aiming, and Unity control. | `ctest --preset linux-debug -R hw6` |
| A7 Strategy Bonus | `HW7_strategy` | Choose targets and retreat behavior for teammate automation. | `ctest --preset linux-debug -R hw7` |

State clearly that the Unity runtime smoke test is still:

```bash
UV_CACHE_DIR=.uv-cache uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 10
```

State clearly that partial assignments should use mini-tests first; candidates should not need the full Unity simulator until A6 integration.

## Task 1: Add Milestone-Level Assignment Design Tests

**Files:**
- Create: `tests/test_assignment_design.py`
- Create: `tests/test_assignment_mini_commands.py`

- [ ] **Step 1: Write failing doc existence and roadmap tests**

Create `tests/test_assignment_design.py` with:

```python
from __future__ import annotations

import re
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
HW_DIRS = [
    "HW1_armor_detector",
    "HW2_tf_graph",
    "HW3_ekf_tracker",
    "HW4_ballistic",
    "HW5_mpc_gimbal",
    "HW6_integration",
    "HW7_strategy",
]


def read(path: str) -> str:
    return (REPO_ROOT / path).read_text(encoding="utf-8")


def test_assignment_redesign_doc_exists_and_names_active_path() -> None:
    doc = read("docs/assignment-redesign.md")
    assert "Unity-first assignment path" in doc
    assert "schema.md" in doc
    assert "docs/unity-wire-contract.md" in doc
    for stage in ["A1 Perception", "A2 Frame Geometry", "A3 Tracking",
                  "A4 Ballistics", "A5 Gimbal Control",
                  "A6 Runner Integration", "A7 Strategy Bonus"]:
        assert stage in doc


def test_each_hw_readme_has_status_banner_and_unity_mapping() -> None:
    for hw_dir in HW_DIRS:
        text = read(f"{hw_dir}/README.md")
        assert "Status:" in text, hw_dir
        assert "Unity-first role:" in text, hw_dir
        assert "Legacy-only:" in text, hw_dir
        assert "Mini-test:" in text, hw_dir


def test_docs_no_longer_present_legacy_ci_as_active_grading() -> None:
    grading = read("docs/grading.md")
    assert "Legacy grading workflow" in grading
    assert "Milestone 3 grading stance" in grading
    assert "live Unity evaluation is deferred" in grading


def test_candidate_todo_markers_start_with_todo_hw_prefix() -> None:
    source_roots = [REPO_ROOT / hw for hw in HW_DIRS]
    bad_lines: list[str] = []
    candidate_pattern = re.compile(r"(IMPLEMENT THIS|fill the TODO|candidate'?s TODO|TODO)")
    valid_marker = re.compile(r"TODO\\(HW[1-7]\\):")

    for root in source_roots:
        for path in root.rglob("*"):
            if path.suffix not in {".py", ".cpp", ".hpp"}:
                continue
            for line_no, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
                if "TODOs here" in line or "no TODOs" in line:
                    continue
                if candidate_pattern.search(line) and not valid_marker.search(line):
                    bad_lines.append(f"{path.relative_to(REPO_ROOT)}:{line_no}: {line.strip()}")

    assert not bad_lines, "\\n".join(bad_lines)
```

- [ ] **Step 2: Write failing mini-command documentation tests**

Create `tests/test_assignment_mini_commands.py` with:

```python
from __future__ import annotations

from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]

EXPECTED_MINI_TESTS = {
    "HW1_armor_detector": [
        "HW1_armor_detector/tests/public/test_assign_targets.py",
        "HW1_armor_detector/tests/public/test_loss_shapes.py",
    ],
    "HW2_tf_graph": [
        "HW2_tf_graph/tests/public/test_basic_lookup.cpp",
        "HW2_tf_graph/tests/public/test_chain_compose.cpp",
        "HW2_tf_graph/tests/public/test_interpolation_corners.cpp",
    ],
    "HW3_ekf_tracker": [
        "HW3_ekf_tracker/tests/public/test_cv_predict.cpp",
        "HW3_ekf_tracker/tests/public/test_imm_mode_probabilities.cpp",
        "HW3_ekf_tracker/tests/public/test_da_simple.cpp",
    ],
    "HW4_ballistic": [
        "HW4_ballistic/tests/public/test_1d_no_drag.cpp",
        "HW4_ballistic/tests/public/test_2d_with_gravity.cpp",
        "HW4_ballistic/tests/public/test_3d_with_drag.cpp",
    ],
    "HW5_mpc_gimbal": [
        "HW5_mpc_gimbal/tests/public/test_cost.py",
        "HW5_mpc_gimbal/tests/public/test_step_response.cpp",
        "HW5_mpc_gimbal/tests/public/test_sinusoid_tracking.cpp",
    ],
    "HW6_integration": [
        "HW6_integration/tests/public/test_ring_buffer.cpp",
        "HW6_integration/tests/public/test_watchdog.cpp",
    ],
    "HW7_strategy": [
        "HW7_strategy/tests/public/test_bt_semantics.cpp",
        "HW7_strategy/tests/public/test_priority_distance.cpp",
        "HW7_strategy/tests/public/test_retreat_trigger.cpp",
    ],
}


def read(path: str) -> str:
    return (REPO_ROOT / path).read_text(encoding="utf-8")


def test_assignment_doc_lists_existing_mini_test_files() -> None:
    doc = read("docs/assignment-redesign.md")
    for hw_dir, paths in EXPECTED_MINI_TESTS.items():
        assert hw_dir in doc
        for rel_path in paths:
            assert (REPO_ROOT / rel_path).exists(), rel_path
            assert rel_path in doc, rel_path


def test_each_hw_readme_lists_its_mini_test_files() -> None:
    for hw_dir, paths in EXPECTED_MINI_TESTS.items():
        readme = read(f"{hw_dir}/README.md")
        for rel_path in paths:
            assert rel_path in readme, f"{hw_dir} missing {rel_path}"
```

- [ ] **Step 3: Run tests to verify they fail**

Run:

```bash
uv run pytest tests/test_assignment_design.py tests/test_assignment_mini_commands.py -q
```

Expected: FAIL because `docs/assignment-redesign.md` does not exist and HW READMEs do not have the standard banner.

- [ ] **Step 4: Commit the failing tests**

Run:

```bash
git add tests/test_assignment_design.py tests/test_assignment_mini_commands.py
git commit -m "test: add assignment redesign guards"
```

## Task 2: Create The Unity-First Assignment Design Document

**Files:**
- Create: `docs/assignment-redesign.md`
- Modify: `README.md`

- [ ] **Step 1: Write `docs/assignment-redesign.md`**

Create the file with this structure:

```markdown
# Unity-First Assignment Path

`schema.md` is the source of truth. `docs/unity-wire-contract.md` is the
candidate runner wire contract. HW1-HW7 remain in the repository as legacy
reference modules, but the active assignment path is organized around the Unity
arena and partial mini-tests.

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
| A4 Ballistics | `HW4_ballistic` | Convert tracks into lead-compensated aim directions. | `ctest --preset linux-debug -R hw4` |
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

## Full Unity Smoke

After A6, candidates can smoke the local Unity runtime:

```bash
UV_CACHE_DIR=.uv-cache uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 10
```
```

- [ ] **Step 2: Update root README candidate section**

Replace the `## Candidate Assignments` section in `README.md` with:

```markdown
## Candidate Assignments

The active assignment design is now documented in
[`docs/assignment-redesign.md`](docs/assignment-redesign.md). HW1-HW7 remain
as implementation/reference folders, but candidates should follow the
Unity-first A1-A7 path and use each stage's mini-tests for partial progress.
```

- [ ] **Step 3: Run tests to verify progress**

Run:

```bash
uv run pytest tests/test_assignment_design.py::test_assignment_redesign_doc_exists_and_names_active_path tests/test_assignment_mini_commands.py::test_assignment_doc_lists_existing_mini_test_files -q
```

Expected: PASS for assignment document tests, remaining banner tests still fail.

- [ ] **Step 4: Commit**

Run:

```bash
git add docs/assignment-redesign.md README.md
git commit -m "docs: add unity-first assignment path"
```

## Task 3: Mark Legacy Grading And Architecture Boundaries

**Files:**
- Modify: `docs/grading.md`
- Modify: `docs/architecture.md`

- [ ] **Step 1: Update `docs/grading.md` header**

Add this section immediately below the title:

```markdown
## Legacy grading workflow

This document records the old public-test and leaderboard process. It is
legacy-only until the Unity-first assignment path is fully implemented.

## Milestone 3 grading stance

For the Unity-first redesign, grading starts with local mini-tests for partial
completion. A live Unity evaluation is deferred until the A6 runner and
Milestone 4 training-ground/runtime pieces are stable.
```

- [ ] **Step 2: Update `docs/architecture.md` transport note**

In `docs/architecture.md`, add this section after the high-level diagram:

```markdown
## Active Unity transport

The current Unity runtime exposes length-prefixed TCP JSON control on port
`7654` and raw RGB frame TCP on port `7655`. See
[`docs/unity-wire-contract.md`](unity-wire-contract.md). The older gRPC/ZMQ
language below remains architecture intent and generated-proto context, not the
current local Unity transport implementation.
```

- [ ] **Step 3: Run focused tests**

Run:

```bash
uv run pytest tests/test_assignment_design.py::test_docs_no_longer_present_legacy_ci_as_active_grading -q
```

Expected: PASS.

- [ ] **Step 4: Commit**

Run:

```bash
git add docs/grading.md docs/architecture.md
git commit -m "docs: mark legacy grading and active unity transport"
```

## Task 4: Standardize HW README Banners

**Files:**
- Modify: `HW1_armor_detector/README.md`
- Modify: `HW2_tf_graph/README.md`
- Modify: `HW3_ekf_tracker/README.md`
- Modify: `HW4_ballistic/README.md`
- Modify: `HW5_mpc_gimbal/README.md`
- Modify: `HW6_integration/README.md`
- Modify: `HW7_strategy/README.md`

- [ ] **Step 1: Add a standard banner after each title**

Use the exact template below, customized per HW:

```markdown
> **Status:** Active as part of the Unity-first assignment path; older
> standalone workflows in this folder are legacy reference.
>
> **Unity-first role:** [one sentence from the active-stage table].
>
> **Legacy-only:** [one sentence naming old workflow parts that should not be
> treated as the final evaluation path].
>
> **Mini-test:** [exact command].
```

Use these exact role and mini-test values:

- HW1 role: `Detect armor plates and vehicle IDs from Unity camera frames.`
- HW1 legacy-only: `Synthetic PIL dataset generation is useful for local training smoke tests, but final evaluation must use Unity-frame semantics.`
- HW1 mini-test: ``uv run pytest HW1_armor_detector/tests/public/test_assign_targets.py HW1_armor_detector/tests/public/test_loss_shapes.py``

- HW2 role: `Maintain world, chassis, gimbal, and camera transforms for Unity observations.`
- HW2 legacy-only: `The standalone TF buffer remains math reference; it is not a ROS integration task.`
- HW2 mini-test: ``ctest --preset linux-debug -R hw2``

- HW3 role: `Smooth armor detections into target tracks for aim prediction.`
- HW3 legacy-only: `CSV fixture replay remains a mini-test harness, not a full live-arena tracker evaluation.`
- HW3 mini-test: ``ctest --preset linux-debug -R hw3``

- HW4 role: `Convert target tracks into lead-compensated aim directions.`
- HW4 legacy-only: `Standalone projectile math remains the partial-progress harness; live hit-rate evaluation belongs to A6 and later.`
- HW4 mini-test: ``ctest --preset linux-debug -R hw4``

- HW5 role: `Track yaw and pitch commands with PID baseline and optional MPC.`
- HW5 legacy-only: `acados generation is team-side or optional; PID and cost mini-tests are the candidate baseline.`
- HW5 mini-test: ``ctest --preset linux-debug -R hw5`` and ``uv run pytest HW5_mpc_gimbal/tests/public/test_cost.py``

- HW6 role: `Connect frame ingestion, perception, tracking, aiming, and Unity control.`
- HW6 legacy-only: `Threading and stale-frame tests are partial gates; full Unity smoke requires the running arena.`
- HW6 mini-test: ``ctest --preset linux-debug -R hw6``

- HW7 role: `Choose targets and retreat behavior for teammate automation.`
- HW7 legacy-only: `PPO training is optional stretch work, not required for the core assignment.`
- HW7 mini-test: ``ctest --preset linux-debug -R hw7``

- [ ] **Step 2: Ensure each README lists expected mini-test file paths**

Add a short `Mini-test files` list to each README using the paths from `EXPECTED_MINI_TESTS` in `tests/test_assignment_mini_commands.py`.

- [ ] **Step 3: Run README tests**

Run:

```bash
uv run pytest tests/test_assignment_design.py::test_each_hw_readme_has_status_banner_and_unity_mapping tests/test_assignment_mini_commands.py::test_each_hw_readme_lists_its_mini_test_files -q
```

Expected: PASS.

- [ ] **Step 4: Commit**

Run:

```bash
git add HW1_armor_detector/README.md HW2_tf_graph/README.md HW3_ekf_tracker/README.md HW4_ballistic/README.md HW5_mpc_gimbal/README.md HW6_integration/README.md HW7_strategy/README.md
git commit -m "docs: map homework folders to unity-first stages"
```

## Task 5: Normalize Candidate TODO Markers

**Files:**
- Modify candidate TODO files listed in the File Structure section.

- [ ] **Step 1: Run current TODO hygiene test**

Run:

```bash
uv run pytest tests/test_assignment_design.py::test_candidate_todo_markers_start_with_todo_hw_prefix -q
```

Expected: FAIL with a list of non-normalized TODO lines.

- [ ] **Step 2: Normalize source comments without changing behavior**

For each failure line, apply these rules:

- Candidate blanks must start with `TODO(HWn):`.
- Header comments that say `IMPLEMENT THIS` should become `TODO(HWn): implement ...`.
- Test skip strings should mention `TODO(HWn)` if they mention TODO at all.
- Non-candidate notes such as `no TODOs here` may remain only if the test explicitly exempts them.
- Do not implement any TODO body in this task.

Example transformation:

```cpp
// Before:
// IMPLEMENT THIS — TODO(HW4).

// After:
// TODO(HW4): implement this solver.
```

```python
# Before:
# TODO(HW5): build the cost.

# After:
# TODO(HW5): build the cost.
```

- [ ] **Step 3: Re-run TODO hygiene**

Run:

```bash
uv run pytest tests/test_assignment_design.py::test_candidate_todo_markers_start_with_todo_hw_prefix -q
```

Expected: PASS.

- [ ] **Step 4: Run existing fast public tests that should remain unaffected**

Run:

```bash
uv run pytest HW1_armor_detector/tests/public/test_assign_targets.py HW1_armor_detector/tests/public/test_loss_shapes.py HW5_mpc_gimbal/tests/public/test_cost.py tests/test_assignment_design.py tests/test_assignment_mini_commands.py -q
```

Expected: assignment-design tests PASS. HW tests may xfail where candidate TODO bodies are intentionally unimplemented; there should be no unexpected FAIL caused by comment-only edits.

- [ ] **Step 5: Commit**

Run:

```bash
git add HW1_armor_detector HW2_tf_graph HW3_ekf_tracker HW4_ballistic HW5_mpc_gimbal HW6_integration HW7_strategy tests/test_assignment_design.py
git commit -m "chore: normalize candidate todo markers"
```

## Task 6: Add Or Confirm Lightweight Mini-Test Commands

**Files:**
- Modify: `docs/assignment-redesign.md`
- Modify: HW READMEs if needed
- No new algorithm tests unless a stage lacks an independent public test.

- [ ] **Step 1: Audit mini-test coverage**

Run:

```bash
uv run pytest tests/test_assignment_mini_commands.py -q
```

Expected: PASS after Task 4.

- [ ] **Step 2: Decide whether new mini-tests are needed**

Use this checklist:

- HW1 has Python loss/target tests and C++ post-processing tests.
- HW2 has transform interpolation and chain tests.
- HW3 has EKF, IMM, and association tests.
- HW4 has no-drag, gravity, and drag tests.
- HW5 has PID C++ and cost Python tests.
- HW6 has ring buffer and watchdog tests.
- HW7 has behavior-tree, target priority, and retreat tests.

If all entries are true, do not add new algorithm tests in Milestone 3. Record this decision in `docs/assignment-redesign.md` under `Mini-Test Policy`:

```markdown
## Mini-Test Policy

Milestone 3 does not add new algorithmic graders where a folder already has a
fast public mini-test. The milestone-level tests only guard documentation,
stage mapping, and TODO hygiene. New behavior tests should be added in the
future only when a stage introduces a new candidate-facing blank.
```

- [ ] **Step 3: Run assignment tests**

Run:

```bash
uv run pytest tests/test_assignment_design.py tests/test_assignment_mini_commands.py -q
```

Expected: PASS.

- [ ] **Step 4: Commit**

Run:

```bash
git add docs/assignment-redesign.md
git commit -m "docs: define mini-test policy"
```

## Task 7: Final Verification And Roadmap Update

**Files:**
- Modify: `docs/cleanup-roadmap.md`

- [ ] **Step 1: Run no-Unity verification**

Run:

```bash
uv run pytest tests/test_assignment_design.py tests/test_assignment_mini_commands.py tests/test_arena_wire_format.py -q
```

Expected: all PASS.

- [ ] **Step 2: Run C++ configure/build if local dependencies are available**

Run:

```bash
cmake --preset linux-debug
cmake --build --preset linux-debug
```

Expected: build succeeds, or dependency skips are clearly reported by existing CMake guards.

- [ ] **Step 3: Run mini-test ctest sweep**

Run:

```bash
ctest --preset linux-debug -R "hw2|hw3|hw4|hw5|hw6|hw7"
```

Expected: no unexpected failures. Skips from intentionally unimplemented candidate TODOs are acceptable only if existing tests already use `GTEST_SKIP` for the blank state.

- [ ] **Step 4: Run focused Python HW tests**

Run:

```bash
uv run pytest HW1_armor_detector/tests/public/test_assign_targets.py HW1_armor_detector/tests/public/test_loss_shapes.py HW5_mpc_gimbal/tests/public/test_cost.py -q
```

Expected: no unexpected failures; xfails/skips are acceptable where candidate blanks remain unimplemented.

- [ ] **Step 5: Update roadmap**

In `docs/cleanup-roadmap.md`, mark Milestone 3 complete:

```markdown
- [x] Redesign HW1-HW7 into a Unity-first assignment path.
- [x] Remove overlap between old tasks or mark it as legacy-only.
- [x] Ensure every candidate blank starts with `TODO`.
- [x] Add lightweight mini-tests for partial task completion.
```

- [ ] **Step 6: Run final assignment guard**

Run:

```bash
uv run pytest tests/test_assignment_design.py tests/test_assignment_mini_commands.py -q
```

Expected: PASS.

- [ ] **Step 7: Commit**

Run:

```bash
git add docs/cleanup-roadmap.md
git commit -m "docs: complete assignment redesign milestone"
```

## Self-Review Notes

Spec coverage:

- Redesign HW1-HW7 into Unity-first path: Tasks 2 and 4.
- Remove overlap or mark legacy-only: Tasks 3 and 4.
- Ensure every candidate blank starts with `TODO`: Task 5.
- Add lightweight mini-tests: Tasks 1 and 6 use existing tests as mini-test gates and add meta-tests to keep them documented.

Placeholder scan:

- This plan intentionally uses the literal word `TODO` when describing candidate blank markers. It does not leave implementation placeholders for the plan itself.

Risk:

- The TODO hygiene regex may catch explanatory README text or non-candidate examples. If that happens, narrow the test to source files only, as written, and avoid scanning READMEs.
- Some existing public tests intentionally skip on unimplemented blanks. Do not treat those skips as Milestone 3 failures unless the skip is caused by broken test discovery or syntax errors.
