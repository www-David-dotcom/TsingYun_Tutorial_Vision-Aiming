"""acados-template codegen entry point.

Reads `configs/mpc_weights.yaml`, builds the CasADi model from
src/model.py + src/cost.py, and invokes acados to produce the C
solver under `generated_solver/`. The C++ runtime in
source/controller.cpp links against the result.

This is a **team-side** workflow — candidates don't run it on their
own machines (acados is finicky on Windows; the candidate's machine
might not have the required hpipm + blasfeo build chain). The team
generates once per weight change and pushes the resulting tarball to
oss://tsingyun-aiming-hw-models/assets/HW5/acados_solver_<version>/
via shared/scripts/push_assets.py. Candidates pull it via
shared/scripts/fetch_assets.py.

Usage:
    uv sync --group hw5
    uv run python HW5_mpc_gimbal/src/generate_acados.py \
        --weights HW5_mpc_gimbal/configs/mpc_weights.yaml \
        --out HW5_mpc_gimbal/generated_solver

    # Dry-run mode validates the model + cost without invoking acados:
    uv run python HW5_mpc_gimbal/src/generate_acados.py --check
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import yaml

sys.path.insert(0, str(Path(__file__).resolve().parent))
from cost import CostWeights, stage_cost_expression, terminal_cost_expression
from model import GimbalParams, build_dynamics_function, state_symbols


def _load_horizon(weights_path: Path) -> tuple[int, float]:
    cfg = yaml.safe_load(weights_path.read_text())
    h = cfg["horizon"]
    return int(h["length_steps"]), float(h["step_seconds"])


def check(weights_path: Path) -> int:
    """Validate the model + cost compile under CasADi without invoking
    acados. Returns 0 on success."""
    try:
        import casadi as ca
    except ImportError:
        print("casadi not installed; run `uv sync --group hw5`", file=sys.stderr)
        return 1

    params = GimbalParams.from_yaml(weights_path)
    weights = CostWeights.from_yaml(weights_path)
    dyn = build_dynamics_function(params)

    x, u = state_symbols()
    yaw_ref = ca.SX.sym("yaw_ref")
    pitch_ref = ca.SX.sym("pitch_ref")
    L_stage = stage_cost_expression(x, u, yaw_ref, pitch_ref, weights)
    L_term  = terminal_cost_expression(x, yaw_ref, pitch_ref, weights)

    print(f"dynamics function:  {dyn}")
    print(f"  -> output shape:   {dyn.size_out(0)}")
    print(f"stage cost expr:    shape {L_stage.shape}")
    print(f"terminal cost expr: shape {L_term.shape}")
    n, dt = _load_horizon(weights_path)
    print(f"horizon:            {n} stages x {dt:.4f} s = {n * dt:.3f} s lookahead")
    print("OK")
    return 0


def codegen(weights_path: Path, out_dir: Path) -> int:
    """Run acados codegen. Requires the `acados` Python package + the
    upstream binaries; see acados.org for installation. Pinning to
    v0.3 in pyproject.toml gives a known-good API."""
    try:
        from acados_template import (
            AcadosModel,
            AcadosOcp,
            AcadosOcpSolver,
        )
    except ImportError:
        print("acados-template not installed. Either:\n"
              "  * `uv sync --group hw5` (pulls the Python wheel only — you still\n"
              "    need the upstream acados C library installed; see\n"
              "    https://docs.acados.org/installation/index.html), or\n"
              "  * pull the team-built tarball via\n"
              "    `python shared/scripts/fetch_assets.py --only acados-solver-hw5-v1.1`.",
              file=sys.stderr)
        return 1

    import casadi as ca

    params = GimbalParams.from_yaml(weights_path)
    weights = CostWeights.from_yaml(weights_path)
    n, dt = _load_horizon(weights_path)

    x, u = state_symbols()
    yaw_ref = ca.SX.sym("yaw_ref")
    pitch_ref = ca.SX.sym("pitch_ref")

    model = AcadosModel()
    model.name = "aiming_mpc"
    model.x = x
    model.u = u
    model.p = ca.vertcat(yaw_ref, pitch_ref)
    model.f_expl_expr = build_dynamics_function(params)(x, u)

    ocp = AcadosOcp()
    ocp.model = model
    ocp.dims.N = n
    ocp.cost.cost_type = "EXTERNAL"
    ocp.cost.cost_type_e = "EXTERNAL"
    ocp.model.cost_expr_ext_cost = stage_cost_expression(
        x, u, yaw_ref, pitch_ref, weights)
    ocp.model.cost_expr_ext_cost_e = terminal_cost_expression(
        x, yaw_ref, pitch_ref, weights)

    # Bounds: pitch + torques + rates from the YAML.
    ocp.constraints.idxbx = [1, 2, 3, 4, 5]   # pitch, rates, torques
    ocp.constraints.lbx = [
        params.pitch_limit_lo_rad,
        -params.yaw_rate_limit_rps,
        -params.pitch_rate_limit_rps,
        -params.yaw_torque_limit_nm,
        -params.pitch_torque_limit_nm,
    ]
    ocp.constraints.ubx = [
        params.pitch_limit_hi_rad,
        params.yaw_rate_limit_rps,
        params.pitch_rate_limit_rps,
        params.yaw_torque_limit_nm,
        params.pitch_torque_limit_nm,
    ]
    ocp.constraints.idxbu = [0, 1]
    ocp.constraints.lbu = [-params.yaw_torque_limit_nm, -params.pitch_torque_limit_nm]
    ocp.constraints.ubu = [+params.yaw_torque_limit_nm, +params.pitch_torque_limit_nm]
    # Initial-state constraint (will be set per call at runtime).
    ocp.constraints.x0 = [0.0] * 6

    ocp.solver_options.tf = n * dt
    ocp.solver_options.nlp_solver_type = "SQP_RTI"
    ocp.solver_options.qp_solver = "PARTIAL_CONDENSING_HPIPM"
    ocp.solver_options.integrator_type = "ERK"
    ocp.solver_options.hessian_approx = "GAUSS_NEWTON"

    out_dir.mkdir(parents=True, exist_ok=True)
    ocp.code_export_directory = str(out_dir / "acados_aiming_mpc")
    AcadosOcpSolver.generate(ocp, json_file=str(out_dir / "acados_ocp.json"))

    print(f"codegen complete → {out_dir}")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("--weights", type=Path,
                        default=Path(__file__).resolve().parents[1]
                        / "configs" / "mpc_weights.yaml")
    parser.add_argument("--out", type=Path,
                        default=Path(__file__).resolve().parents[1]
                        / "generated_solver")
    parser.add_argument("--check", action="store_true",
                        help="validate the model + cost without invoking acados")
    args = parser.parse_args()

    if args.check:
        return check(args.weights)
    return codegen(args.weights, args.out)


if __name__ == "__main__":
    raise SystemExit(main())
