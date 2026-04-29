"""Generate the three trajectory fixtures the public tests load.

Each CSV has columns: t_s, true_px, true_py, true_vx, true_vy, meas_x, meas_y.
Same numpy.Generator seed → byte-identical output, so the fixtures
are deterministic and the C++ tests' `1e-9` tolerances hold.

Profiles:
  * low_maneuver: straight-line at 1 m/s for 30 s
  * med_maneuver: gentle turn at 0.6 rad/s
  * high_maneuver: hard turn at 4.0 rad/s for the high-NEES bound

Run from the repo root:

    uv run python HW3_ekf_tracker/reference/generate_fixtures.py
"""

from __future__ import annotations

import csv
from pathlib import Path

import numpy as np

DT = 1.0 / 60.0     # 60 Hz sampling, matches the arena's frame rate
DURATION = 30.0
MEAS_NOISE_STD = np.deg2rad(5.0)     # 5° angular noise translated to position noise via 5 m range
MEAS_NOISE_M = 0.05

OUT_DIR = Path(__file__).resolve().parents[1] / "tests" / "fixtures"


def _simulate(omega: float, seed: int, *, name: str) -> None:
    rng = np.random.default_rng(seed)
    n = int(DURATION / DT)
    rows: list[tuple] = []
    px, py = 0.0, 0.0
    vx, vy = 1.0, 0.0
    for k in range(n):
        # Constant-turn ground truth: rotate the velocity vector by omega*dt
        # and apply over the interval.
        if abs(omega) < 1e-9:
            px += vx * DT
            py += vy * DT
        else:
            s = np.sin(omega * DT)
            c = np.cos(omega * DT)
            dpx = (s / omega) * vx + (-(1.0 - c) / omega) * vy
            dpy = ((1.0 - c) / omega) * vx + (s / omega) * vy
            px += dpx
            py += dpy
            new_vx = c * vx - s * vy
            new_vy = s * vx + c * vy
            vx, vy = new_vx, new_vy
        meas_x = px + rng.normal(0.0, MEAS_NOISE_M)
        meas_y = py + rng.normal(0.0, MEAS_NOISE_M)
        rows.append((k * DT, px, py, vx, vy, meas_x, meas_y))

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    out_path = OUT_DIR / f"traj_{name}.csv"
    with out_path.open("w", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(["t_s", "true_px", "true_py", "true_vx", "true_vy",
                         "meas_x", "meas_y"])
        for row in rows:
            writer.writerow([f"{c:.10f}" for c in row])
    print(f"wrote {n} samples → {out_path}")


def main() -> None:
    _simulate(omega=0.0, seed=42, name="low_maneuver")
    _simulate(omega=0.6, seed=43, name="med_maneuver")
    _simulate(omega=4.0, seed=44, name="high_maneuver")


if __name__ == "__main__":
    main()
