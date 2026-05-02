"""Milestone 3 assignment redesign guards."""

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
    for stage in [
        "A1 Perception",
        "A2 Frame Geometry",
        "A3 Tracking",
        "A4 Ballistics",
        "A5 Gimbal Control",
        "A6 Runner Integration",
        "A7 Strategy Bonus",
    ]:
        assert stage in doc


def test_each_hw_readme_has_status_banner_and_unity_mapping() -> None:
    for hw_dir in HW_DIRS:
        text = read(f"{hw_dir}/README.md")
        assert "Status:" in text, hw_dir
        assert "Unity-first role:" in text, hw_dir
        assert "Legacy-only:" in text, hw_dir
        assert "Mini-test:" in text, hw_dir


def test_readmes_are_student_friendly_for_first_time_setup() -> None:
    root = read("README.md")
    for phrase in [
        "Who this is for",
        "Install prerequisites",
        "First-time setup",
        "Assignment workflow",
        "Mini-test quick reference",
        "Troubleshooting",
        "uv sync",
        "cmake --preset",
        "ctest",
        "Docker",
    ]:
        assert phrase in root, phrase

    for hw_dir in HW_DIRS:
        text = read(f"{hw_dir}/README.md")
        for phrase in [
            "Student Quickstart",
            "Prerequisites",
            "What to implement",
            "Mini-test command",
            "Expected first run",
            "Before moving on",
        ]:
            assert phrase in text, f"{hw_dir} missing {phrase}"
        assert "TODO(HW" in text, f"{hw_dir} missing TODO marker guidance"


def test_docs_no_longer_present_legacy_ci_as_active_grading() -> None:
    grading = read("docs/grading.md")
    assert "Legacy grading workflow" in grading
    assert "Milestone 3 grading stance" in grading
    assert "live Unity evaluation is deferred" in grading


def test_candidate_todo_markers_start_with_todo_hw_prefix() -> None:
    source_roots = [REPO_ROOT / hw for hw in HW_DIRS]
    bad_lines: list[str] = []
    candidate_pattern = re.compile(r"(IMPLEMENT THIS|fill the TODO|candidate'?s TODO|TODO)")
    valid_marker = re.compile(r"TODO\(HW[1-7]\):")

    for root in source_roots:
        for path in root.rglob("*"):
            if path.suffix not in {".py", ".cpp", ".hpp"}:
                continue
            for line_no, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
                if "TODOs here" in line or "no TODOs" in line:
                    continue
                if candidate_pattern.search(line) and not valid_marker.search(line):
                    bad_lines.append(f"{path.relative_to(REPO_ROOT)}:{line_no}: {line.strip()}")

    assert not bad_lines, "\n".join(bad_lines)
