"""Tier 4: 50-episode bronze opponent regression Godot vs Unity.

Drives `bronze.pt` against each engine for 50 fixed seeds. Records each
episode's outcome (WIN/LOSS/TIMEOUT) + damage_dealt + damage_taken.
2-sample Kolmogorov-Smirnov test on damage_dealt distributions; passes
if p > 0.10 (statistically indistinguishable physics).

Usage:
  uv run python tests/bronze_regression.py --threshold 0.10

12b note: this script invokes tools/scripts/smoke_arena.py per episode
as a placeholder for the candidate's hw_runner C++ binary built against
bronze.pt. Once the candidate-side stack is built (HW1-HW7 binaries
linked in shared/opponents/bronze.pt), swap the subprocess call below
for hw_runner.

Required deps not yet in pyproject.toml: scipy. Add via
`uv add --dev scipy` before running.
"""

from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

import numpy as np
from scipy import stats

REPO_ROOT = Path(__file__).resolve().parents[1]
SEEDS = list(range(1, 51))


def run_episode(engine: str, seed: int) -> dict:
    """Drive one episode against the running arena. Returns parsed [finish] line."""
    proc = subprocess.run(
        ["uv", "run", "python", "tools/scripts/smoke_arena.py",
         "--engine", engine, "--seed", str(seed), "--ticks", "5400"],
        capture_output=True, text=True, cwd=REPO_ROOT,
    )
    if proc.returncode != 0:
        raise RuntimeError(f"episode failed seed={seed}: {proc.stderr}")
    for line in proc.stdout.splitlines():
        if line.startswith("[finish]"):
            return parse_finish_line(line)
    raise RuntimeError(f"no [finish] line in stdout for seed={seed}")


def parse_finish_line(line: str) -> dict:
    parts = line.split()
    out = {}
    for p in parts:
        if "=" in p:
            k, v = p.split("=", 1)
            out[k] = v
    return out


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--threshold", type=float, default=0.10)
    args = parser.parse_args()

    print("[bronze-regression] running Godot...")
    godot_damage = [int(run_episode("godot", s)["damage_dealt"]) for s in SEEDS]

    print("[bronze-regression] running Unity (start MapA_MazeHybrid in Play mode)...")
    input("Press Enter when Unity arena is in Play mode...")
    unity_damage = [int(run_episode("unity", s)["damage_dealt"]) for s in SEEDS]

    ks_stat, p_value = stats.ks_2samp(godot_damage, unity_damage)
    print(f"[KS] statistic={ks_stat:.4f} p_value={p_value:.4f}")
    print(f"  godot mean damage: {np.mean(godot_damage):.1f}")
    print(f"  unity mean damage: {np.mean(unity_damage):.1f}")

    if p_value < args.threshold:
        print(f"[FAIL] p={p_value:.4f} < threshold {args.threshold}; physics distributions diverge.")
        return 1
    print(f"[OK] p={p_value:.4f} >= threshold {args.threshold}; engines indistinguishable.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
