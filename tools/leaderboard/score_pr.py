"""Score a single PR's CI run.

Reads the JUnit XML produced by `pytest --junit-xml` and the CTest
JUnit-format XML produced by `ctest --output-junit`, partitions
test cases by the HW directory they live in, and emits:

  * `submission_score.json` — machine-readable per-HW scores
  * `submission_score.md`   — markdown comment posted on the PR

Per Stage 10 v1 (Proposal A), the score is the count of passing
public tests per HW. No hidden tests; no simulator-driven evaluation
here. The aggregator (`aggregate.py`) sums these into a leaderboard.

A test that *skipped* counts as zero (not a fail). This is by design:
HW1's torch-dependent tests skip on stock `ubuntu-latest`, and we
don't want to penalise a candidate for that — only the team's
manual re-run with the `hw1` uv group sees the full HW1 score. The
markdown comment says so out loud.
"""

from __future__ import annotations

import argparse
import json
import re
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path

# Mapping of test-name prefixes / fixture file paths to HW labels.
# CTest JUnit names are the GTest "Suite.Case" form; pytest names
# include the path to the test file.
HW_RULES: list[tuple[str, str]] = [
    ("HW1", r"HW1[A-Za-z]"),
    ("HW1", r"HW1_armor_detector"),
    ("HW2", r"HW2[A-Za-z]"),
    ("HW2", r"HW2_tf_graph"),
    ("HW3", r"HW3[A-Za-z]"),
    ("HW3", r"HW3_ekf_tracker"),
    ("HW4", r"HW4[A-Za-z]"),
    ("HW4", r"HW4_ballistic"),
    ("HW5", r"HW5[A-Za-z]"),
    ("HW5", r"HW5_mpc_gimbal"),
    ("HW6", r"HW6[A-Za-z]"),
    ("HW6", r"HW6_integration"),
    ("HW7", r"HW7[A-Za-z]"),
    ("HW7", r"HW7_strategy"),
]
HW_LABELS = ["HW1", "HW2", "HW3", "HW4", "HW5", "HW6", "HW7", "shared"]


@dataclass
class HwBucket:
    label: str
    passed: int = 0
    failed: int = 0
    skipped: int = 0
    failures: list[str] = field(default_factory=list)

    @property
    def total(self) -> int:
        return self.passed + self.failed + self.skipped


def classify(name: str, classname: str = "") -> str:
    full = f"{classname} {name}"
    for label, pattern in HW_RULES:
        if re.search(pattern, full):
            return label
    return "shared"


def parse_junit(path: Path, buckets: dict[str, HwBucket]) -> None:
    if not path.exists():
        return
    tree = ET.parse(path)
    root = tree.getroot()
    suites = root.iter("testsuite")
    for suite in suites:
        for case in suite.iter("testcase"):
            name = case.get("name", "")
            classname = case.get("classname", "")
            label = classify(name, classname)
            bucket = buckets.setdefault(label, HwBucket(label=label))
            failure = case.find("failure")
            error = case.find("error")
            skipped = case.find("skipped")
            if failure is not None or error is not None:
                bucket.failed += 1
                msg = (failure.get("message") if failure is not None
                       else error.get("message", ""))
                bucket.failures.append(f"{classname}::{name} — {msg}")
            elif skipped is not None:
                bucket.skipped += 1
            else:
                bucket.passed += 1


def render_markdown(buckets: dict[str, HwBucket]) -> str:
    lines: list[str] = []
    lines.append("## Aiming HW — public test summary")
    lines.append("")
    lines.append("| HW | passed | failed | skipped | total |")
    lines.append("|----|-------:|-------:|--------:|------:|")
    overall = HwBucket(label="total")
    for label in HW_LABELS:
        b = buckets.get(label)
        if b is None or b.total == 0:
            continue
        lines.append(f"| `{label}` | {b.passed} | {b.failed} | {b.skipped} | {b.total} |")
        overall.passed += b.passed
        overall.failed += b.failed
        overall.skipped += b.skipped
    lines.append(f"| **all** | **{overall.passed}** | **{overall.failed}** "
                 f"| **{overall.skipped}** | **{overall.total}** |")
    lines.append("")
    lines.append(f"**Score:** {overall.passed} passing public tests "
                 f"({overall.failed} failing, {overall.skipped} skipped).")
    lines.append("")
    lines.append("Skipped tests usually mean an optional dependency wasn't "
                 "installed on `ubuntu-latest` (HW1 needs torch + ONNX "
                 "Runtime, HW5 MPC needs the acados-codegened solver). "
                 "They're **not penalised** in the public score — only "
                 "fails are.")
    failures = [f for b in buckets.values() for f in b.failures]
    if failures:
        lines.append("")
        lines.append("<details><summary>First few failures</summary>")
        lines.append("")
        for f in failures[:10]:
            lines.append(f"* `{f}`")
        if len(failures) > 10:
            lines.append(f"* ... ({len(failures) - 10} more in the artefact)")
        lines.append("")
        lines.append("</details>")
    lines.append("")
    lines.append("> Posted by `validate_submission.yml`. Full XML output "
                 "is attached to the workflow run as the "
                 "`submission-score-*` artefact.")
    return "\n".join(lines) + "\n"


def render_json(buckets: dict[str, HwBucket]) -> dict:
    by_hw = {}
    overall = {"passed": 0, "failed": 0, "skipped": 0}
    for label in HW_LABELS:
        b = buckets.get(label)
        if b is None or b.total == 0:
            continue
        by_hw[label] = {
            "passed": b.passed,
            "failed": b.failed,
            "skipped": b.skipped,
            "failures_sample": b.failures[:5],
        }
        overall["passed"] += b.passed
        overall["failed"] += b.failed
        overall["skipped"] += b.skipped
    return {
        "schema_version": 1,
        "by_hw": by_hw,
        "overall": overall,
    }


def main() -> int:
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("--ctest-xml",  type=Path, required=True)
    parser.add_argument("--pytest-xml", type=Path, required=True)
    parser.add_argument("--out",        type=Path, required=True,
                        help="JSON output path")
    parser.add_argument("--markdown",   type=Path, required=True,
                        help="Markdown output path (PR comment body)")
    args = parser.parse_args()

    buckets: dict[str, HwBucket] = {}
    parse_junit(args.ctest_xml, buckets)
    parse_junit(args.pytest_xml, buckets)

    args.out.write_text(json.dumps(render_json(buckets), indent=2))
    args.markdown.write_text(render_markdown(buckets))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
