"""CasADi symbolic model for the HW5 gimbal MPC.

State vector (6-dim):
    [yaw, pitch, yaw_rate, pitch_rate, yaw_torque, pitch_torque]

Control vector (2-dim):
    [yaw_torque_cmd, pitch_torque_cmd]

Two TODO sites:
    * `motor_torque_lag` — first-order lag from cmd to applied torque.
    * `state_dot`        — full state derivative composing kinematics
                           + the lag dynamics above.

The kinematic terms (yaw_dot = yaw_rate, etc.) are filled. The
acados codegen in `generate_acados.py` calls `state_dot` to build
the integrator, so getting the order of state components right is
load-bearing.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

import yaml

try:
    import casadi as ca
except ImportError as e:  # pragma: no cover - HW5 dep group not installed
    raise ImportError(
        "HW5 model.py requires casadi; run `uv sync --group hw5` first"
    ) from e


@dataclass
class GimbalParams:
    inertia_yaw_kgm2: float
    inertia_pitch_kgm2: float
    motor_lag_tc_s: float
    yaw_rate_limit_rps: float
    pitch_rate_limit_rps: float
    yaw_torque_limit_nm: float
    pitch_torque_limit_nm: float
    pitch_limit_lo_rad: float
    pitch_limit_hi_rad: float

    @classmethod
    def from_yaml(cls, path: Path) -> GimbalParams:
        cfg = yaml.safe_load(path.read_text())
        return cls(**cfg["physics"])


def state_symbols() -> tuple[ca.SX, ca.SX]:
    """Return (x, u) symbolic variables in the canonical order."""
    x = ca.SX.sym("x", 6)
    u = ca.SX.sym("u", 2)
    return x, u


def motor_torque_lag(
    yaw_torque: ca.SX,
    pitch_torque: ca.SX,
    yaw_torque_cmd: ca.SX,
    pitch_torque_cmd: ca.SX,
    params: GimbalParams,
) -> ca.SX:
    """First-order lag from torque command to applied torque.

    Returns a 2-vector [yaw_torque_dot, pitch_torque_dot].

    Mathematical form:
        torque_dot = (cmd - torque) / motor_lag_tc

    IMPLEMENT THIS — TODO(HW5).
    """
    # TODO(HW5): two lines. The lag dynamics for both axes are
    # symmetric — same time constant, applied independently to each
    # torque state.
    #
    # Hint: ca.vertcat(yaw_dot, pitch_dot) builds the 2-vector.
    del yaw_torque, pitch_torque, yaw_torque_cmd, pitch_torque_cmd, params
    return ca.vertcat(0.0, 0.0)


def state_dot(x: ca.SX, u: ca.SX, params: GimbalParams) -> ca.SX:
    """Full state derivative dx/dt as a CasADi 6-vector.

    State unpacking (filled — do not change order; the codegen and
    the C++ runtime depend on it):
        yaw, pitch, yaw_rate, pitch_rate, yaw_torque, pitch_torque
    Control:
        yaw_torque_cmd, pitch_torque_cmd
    """
    yaw          = x[0]
    pitch        = x[1]
    yaw_rate     = x[2]
    pitch_rate   = x[3]
    yaw_torque   = x[4]
    pitch_torque = x[5]
    yaw_cmd      = u[0]
    pitch_cmd    = u[1]

    # Kinematic terms (filled).
    yaw_dot         = yaw_rate
    pitch_dot       = pitch_rate
    yaw_rate_dot    = yaw_torque   / params.inertia_yaw_kgm2
    pitch_rate_dot  = pitch_torque / params.inertia_pitch_kgm2

    # TODO(HW5): use motor_torque_lag(...) above to compute
    # yaw_torque_dot and pitch_torque_dot, then return the full
    # 6-vector via ca.vertcat in the canonical order.
    del yaw, pitch, yaw_cmd, pitch_cmd
    return ca.vertcat(
        yaw_dot,
        pitch_dot,
        yaw_rate_dot,
        pitch_rate_dot,
        # TODO(HW5): yaw_torque_dot
        0.0,
        # TODO(HW5): pitch_torque_dot
        0.0,
    )


def build_dynamics_function(params: GimbalParams) -> ca.Function:
    """CasADi function `f: (x, u) -> dx/dt`. Used by `generate_acados.py`."""
    x, u = state_symbols()
    return ca.Function("aiming_mpc_dyn", [x, u], [state_dot(x, u, params)],
                       ["x", "u"], ["xdot"])
