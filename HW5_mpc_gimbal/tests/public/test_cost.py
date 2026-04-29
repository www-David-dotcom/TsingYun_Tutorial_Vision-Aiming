"""Direct unit test for `cost.stage_cost_expression`.

Pins the quadratic-cost shape the candidate fills in `src/cost.py`.
Skips when CasADi isn't installed (the `hw5` uv group hasn't been
synced) or when the stub zero-return is detected.
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

ca = pytest.importorskip("casadi")

REPO_ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(REPO_ROOT / "HW5_mpc_gimbal" / "src"))

from cost import CostWeights, stage_cost_expression  # noqa: E402


def _evaluate(expr: ca.SX, sym_inputs, num_inputs) -> float:
    f = ca.Function("test", sym_inputs, [expr])
    return float(f(*num_inputs))


def _make_weights() -> CostWeights:
    return CostWeights(
        state_diag=[100.0, 100.0, 1.0, 1.0, 0.0, 0.0],
        control_diag=[0.05, 0.05],
        terminal_scale=5.0,
    )


def test_zero_state_zero_control_zero_ref_is_zero() -> None:
    x = ca.SX.sym("x", 6)
    u = ca.SX.sym("u", 2)
    yaw_ref = ca.SX.sym("yaw_ref")
    pitch_ref = ca.SX.sym("pitch_ref")
    expr = stage_cost_expression(x, u, yaw_ref, pitch_ref, _make_weights())
    val = _evaluate(expr, [x, u, yaw_ref, pitch_ref],
                    [[0]*6, [0]*2, 0.0, 0.0])
    assert val == pytest.approx(0.0)


def test_yaw_error_dominates_quadratically() -> None:
    x = ca.SX.sym("x", 6)
    u = ca.SX.sym("u", 2)
    yaw_ref = ca.SX.sym("yaw_ref")
    pitch_ref = ca.SX.sym("pitch_ref")
    expr = stage_cost_expression(x, u, yaw_ref, pitch_ref, _make_weights())

    # x[0] = 0.1, yaw_ref = 0 → e[0] = 0.1, weight = 100, contribution = 100·0.01 = 1.0
    val = _evaluate(expr, [x, u, yaw_ref, pitch_ref],
                    [[0.1, 0, 0, 0, 0, 0], [0, 0], 0.0, 0.0])
    if val == pytest.approx(0.0):
        pytest.xfail("stage_cost_expression unimplemented "
                     "— fill the TODO in src/cost.py")
    assert val == pytest.approx(1.0, rel=1e-6)


def test_doubled_error_quadruples_cost() -> None:
    x = ca.SX.sym("x", 6)
    u = ca.SX.sym("u", 2)
    yaw_ref = ca.SX.sym("yaw_ref")
    pitch_ref = ca.SX.sym("pitch_ref")
    expr = stage_cost_expression(x, u, yaw_ref, pitch_ref, _make_weights())

    val_1 = _evaluate(expr, [x, u, yaw_ref, pitch_ref],
                      [[0.1, 0, 0, 0, 0, 0], [0, 0], 0.0, 0.0])
    if val_1 == pytest.approx(0.0):
        pytest.xfail("stage_cost_expression unimplemented")
    val_2 = _evaluate(expr, [x, u, yaw_ref, pitch_ref],
                      [[0.2, 0, 0, 0, 0, 0], [0, 0], 0.0, 0.0])
    assert val_2 == pytest.approx(4.0 * val_1, rel=1e-6)


def test_control_effort_costs() -> None:
    x = ca.SX.sym("x", 6)
    u = ca.SX.sym("u", 2)
    yaw_ref = ca.SX.sym("yaw_ref")
    pitch_ref = ca.SX.sym("pitch_ref")
    expr = stage_cost_expression(x, u, yaw_ref, pitch_ref, _make_weights())

    # u[0] = 1.0 → weight 0.05 → contribution 0.05.
    val = _evaluate(expr, [x, u, yaw_ref, pitch_ref],
                    [[0]*6, [1.0, 0.0], 0.0, 0.0])
    if val == pytest.approx(0.0):
        pytest.xfail("stage_cost_expression unimplemented")
    assert val == pytest.approx(0.05, rel=1e-6)
