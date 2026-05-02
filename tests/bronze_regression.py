"""Legacy 50-episode bronze opponent regression against the Unity arena.

This script is retained as reference for future RL regression design. It drives
the Unity smoke harness for 50 fixed seeds and reports the damage_dealt
distribution. It is not part of the current cleanup verification gate.

Usage:
  uv run python tests/bronze_regression.py

This invokes tools/scripts/smoke_arena.py per episode as a placeholder for a
future candidate runner or RL policy driver.
"""

from __future__ import annotations

import argparse
import subprocess
from pathlib import Path

import numpy as np

REPO_ROOT = Path(__file__).resolve().parents[1]
SEEDS = list(range(1, 51))


def run_episode(seed: int) -> dict:
    """Drive one episode against the running arena. Returns parsed [finish] line."""
    proc = subprocess.run(
        ["uv", "run", "python", "tools/scripts/smoke_arena.py",
         "--seed", str(seed), "--ticks", "5400"],
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
    args = parser.parse_args()

    print("[bronze-regression] running Unity (start MapA_MazeHybrid in Play mode)...")
    input("Press Enter when Unity arena is in Play mode...")
    unity_damage = [int(run_episode(s)["damage_dealt"]) for s in SEEDS]

    print(f"  unity mean damage: {np.mean(unity_damage):.1f}")
    print(f"  unity min/max damage: {min(unity_damage)} / {max(unity_damage)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
