"""Offline tuning helper for the HW5 MPC weights.

Runs the linearised closed-loop step + sinusoid response under a
diagonal-LQR approximation of the MPC (no acados required) so the
candidate can sweep weight values quickly without waiting for codegen.
The actual final tuning still happens with the codegened solver, but
this script gets you in the ballpark.

Usage:
    uv sync --group hw5
    uv run python HW5_mpc_gimbal/src/tune.py \
        --weights HW5_mpc_gimbal/configs/mpc_weights.yaml \
        --plot
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
import yaml


def _load_horizon(path: Path) -> tuple[int, float]:
    cfg = yaml.safe_load(path.read_text())
    h = cfg["horizon"]
    return int(h["length_steps"]), float(h["step_seconds"])


def step_response_pid(
    *,
    target_step_rad: float,
    dt: float,
    duration_s: float,
    kp: float,
    kd: float,
    motor_lag_tc: float,
    inertia: float,
) -> tuple[np.ndarray, np.ndarray]:
    """Simulate a single-axis step response under a PD law plus first-
    order motor lag — a stand-in for the MPC the candidate will tune.

    Returns (t, theta_history)."""
    n = int(duration_s / dt)
    theta = 0.0
    omega = 0.0
    torque = 0.0
    t_hist = np.zeros(n)
    th_hist = np.zeros(n)
    for k in range(n):
        err = target_step_rad - theta
        cmd = kp * err - kd * omega
        # Motor lag.
        torque += dt * (cmd - torque) / motor_lag_tc
        omega  += dt * torque / inertia
        theta  += dt * omega
        t_hist[k] = k * dt
        th_hist[k] = theta
    return t_hist, th_hist


def settling_time_ms(t: np.ndarray, theta: np.ndarray, target: float, band: float = 0.05) -> float:
    """First time after which |theta - target| / target stays < band."""
    threshold = abs(target) * band
    last_outside = 0
    for k in range(len(t)):
        if abs(theta[k] - target) > threshold:
            last_outside = k
    if last_outside == len(t) - 1:
        return float("inf")
    return float(t[last_outside + 1] * 1000.0)


def main() -> int:
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("--weights", type=Path,
                        default=Path(__file__).resolve().parents[1]
                        / "configs" / "mpc_weights.yaml")
    parser.add_argument("--plot", action="store_true",
                        help="render matplotlib plots; off by default for CI")
    args = parser.parse_args()

    cfg = yaml.safe_load(args.weights.read_text())
    physics = cfg["physics"]
    horizon = cfg["horizon"]

    target = np.deg2rad(30.0)
    dt = horizon["step_seconds"]
    duration = 0.5

    sweeps = [
        {"label": "soft (kp=20, kd=2)",   "kp": 20.0, "kd": 2.0},
        {"label": "medium (kp=80, kd=4)", "kp": 80.0, "kd": 4.0},
        {"label": "stiff (kp=200, kd=8)", "kp": 200.0, "kd": 8.0},
    ]

    print(f"step response (target = 30°, motor lag = {physics['motor_lag_tc_s']*1000:.0f} ms):")
    histories = []
    for s in sweeps:
        t, theta = step_response_pid(
            target_step_rad=target,
            dt=dt,
            duration_s=duration,
            kp=s["kp"],
            kd=s["kd"],
            motor_lag_tc=physics["motor_lag_tc_s"],
            inertia=physics["inertia_yaw_kgm2"],
        )
        ts = settling_time_ms(t, theta, target)
        overshoot = (np.max(theta) - target) / target * 100.0
        print(f"  {s['label']:<30s} settling = {ts:7.1f} ms,  overshoot = {overshoot:+5.1f}%")
        histories.append((s["label"], t, theta))

    if args.plot:
        try:
            import matplotlib.pyplot as plt
        except ImportError:
            print("matplotlib not installed; skipping plot", file=sys.stderr)
            return 0
        fig, ax = plt.subplots()
        for label, t, theta in histories:
            ax.plot(t, np.rad2deg(theta), label=label)
        ax.axhline(np.rad2deg(target), linestyle="--", color="k", linewidth=0.5)
        ax.set_xlabel("t (s)")
        ax.set_ylabel("yaw (deg)")
        ax.set_title("HW5 PD-baseline step response (motor lag included)")
        ax.legend()
        out_path = Path("/tmp/hw5_step_response.png")
        fig.savefig(out_path, dpi=120)
        print(f"saved plot → {out_path}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
