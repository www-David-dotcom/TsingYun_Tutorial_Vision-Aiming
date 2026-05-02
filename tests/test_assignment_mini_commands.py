"""Checks that documented assignment mini-tests point at real files."""

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
