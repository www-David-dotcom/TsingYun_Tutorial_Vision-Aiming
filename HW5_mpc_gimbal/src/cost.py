"""Quadratic stage + terminal cost for the HW5 MPC.

Filled (no TODOs). The candidate tunes the diagonal weights via
configs/mpc_weights.yaml; this module reads them, builds the CasADi
expressions, and hands them off to generate_acados.py.

State error reference:
    e_x = [yaw - yaw_ref, pitch - pitch_ref, yaw_rate, pitch_rate,
           yaw_torque, pitch_torque]
Control:
    u   = [yaw_torque_cmd, pitch_torque_cmd]

Stage cost:
    L = e_x^T diag(W_state) e_x + u^T diag(W_control) u
Terminal cost (last stage):
    L_T = e_x^T (terminal_scale · diag(W_state)) e_x

The reference yaw/pitch are runtime parameters fed to the solver each
loop tick; this module only builds the *symbolic* cost. The runtime
contract is documented in source/controller.cpp.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

import yaml

try:
    import casadi as ca
except ImportError as e:  # pragma: no cover
    raise ImportError(
        "HW5 cost.py requires casadi; run `uv sync --group hw5` first"
    ) from e


@dataclass
class CostWeights:
    state_diag: list[float]
    control_diag: list[float]
    terminal_scale: float

    @classmethod
    def from_yaml(cls, path: Path) -> CostWeights:
        cfg = yaml.safe_load(path.read_text())
        w = cfg["weights"]
        state_keys = ["yaw_error", "pitch_error", "yaw_rate", "pitch_rate",
                      "yaw_torque", "pitch_torque"]
        control_keys = ["yaw_torque_cmd", "pitch_torque_cmd"]
        return cls(
            state_diag=[float(w["state_diag"][k]) for k in state_keys],
            control_diag=[float(w["control_diag"][k]) for k in control_keys],
            terminal_scale=float(w["terminal_scale"]),
        )


def stage_cost_expression(
    x: ca.SX,
    u: ca.SX,
    yaw_ref: ca.SX,
    pitch_ref: ca.SX,
    weights: CostWeights,
) -> ca.SX:
    """Quadratic stage cost as a scalar CasADi expression."""
    e = ca.vertcat(
        x[0] - yaw_ref,
        x[1] - pitch_ref,
        x[2],
        x[3],
        x[4],
        x[5],
    )
    W_state = ca.diag(ca.SX(weights.state_diag))
    W_control = ca.diag(ca.SX(weights.control_diag))
    return e.T @ W_state @ e + u.T @ W_control @ u


def terminal_cost_expression(
    x: ca.SX,
    yaw_ref: ca.SX,
    pitch_ref: ca.SX,
    weights: CostWeights,
) -> ca.SX:
    """Terminal cost — same state weighting, scaled by `terminal_scale`."""
    e = ca.vertcat(
        x[0] - yaw_ref,
        x[1] - pitch_ref,
        x[2],
        x[3],
        x[4],
        x[5],
    )
    scaled = [w * weights.terminal_scale for w in weights.state_diag]
    W_terminal = ca.diag(ca.SX(scaled))
    return e.T @ W_terminal @ e
