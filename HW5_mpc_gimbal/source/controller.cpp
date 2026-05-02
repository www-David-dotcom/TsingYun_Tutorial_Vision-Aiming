#include "aiming_hw/mpc/controller.hpp"

// MPC controller — wraps the acados-codegened solver under
// generated_solver/. Build flow:
//
//   1. `uv run python src/generate_acados.py --weights configs/mpc_weights.yaml`
//      (runs once on the team's box, produces acados_aiming_mpc/...).
//   2. CMake adds the generated C library to hw5_mpc when it sees
//      `generated_solver/acados_aiming_mpc/CMakeLists.txt` — the
//      acados generator emits one as part of codegen.
//   3. The runtime here links against that library and dispatches
//      the per-tick QP via acados_solve / acados_get_x_at_stage.
//
// One TODO(HW5): site, in `MpcController::step`. The header / dtor /
// last_qp_iter / last_residual are filled.

namespace aiming_hw {
namespace mpc {

struct MpcController::Impl {
    int    last_qp_iter = 0;
    double last_residual = 0.0;
    // The generated acados solver lives here. We use a void* / opaque
    // handle to avoid pulling acados headers into our public surface.
    // The candidate blank casts this back to `acados_ocp_capsule*`
    // (defined in the generated library) inside step().
    void*  acados_capsule = nullptr;
};

MpcController::MpcController(const MpcConfig& cfg) : cfg_(cfg) {
    impl_ = new Impl();
    // Initialisation of the acados capsule is deferred to the candidate's
    // step() implementation — we don't have the codegen function name
    // until they run generate_acados.py and pin it into the build.
}

MpcController::~MpcController() {
    delete impl_;
}

int MpcController::last_qp_iter() const noexcept {
    return impl_->last_qp_iter;
}

double MpcController::last_residual() const noexcept {
    return impl_->last_residual;
}

PidCommand MpcController::step(const PidState& state,
                               double yaw_ref,
                               double pitch_ref,
                               double yaw_ref_rate,
                               double pitch_ref_rate,
                               double dt) {
    // TODO(HW5): wire the codegened acados solver.
    //
    // After running `python src/generate_acados.py`, the directory
    // `generated_solver/acados_aiming_mpc/` contains:
    //   * acados_solver_aiming_mpc.h / .c
    //   * acados_sim_solver_aiming_mpc.h / .c
    //   * a CMakeLists.txt that builds them as a static library
    //
    // Steps to fill this function:
    //   1. #include "acados_solver_aiming_mpc.h" (top of this file).
    //   2. On first call, allocate the capsule:
    //         capsule = aiming_mpc_acados_create_capsule();
    //         aiming_mpc_acados_create(capsule);
    //      Store capsule in impl_->acados_capsule (cast appropriately).
    //   3. Set the parameter vector p = [yaw_ref, pitch_ref] for every
    //      stage:
    //         double p[2] = { yaw_ref, pitch_ref };
    //         for (int i = 0; i <= cfg_.horizon_steps; ++i)
    //             aiming_mpc_acados_update_params(capsule, i, p, 2);
    //   4. Set the initial state x0 from `state`:
    //         double x0[6] = {state.yaw, state.pitch,
    //                          state.yaw_rate, state.pitch_rate,
    //                          0.0, 0.0};
    //         ocp_nlp_constraints_model_set(..., 0, "lbx", x0);
    //         ocp_nlp_constraints_model_set(..., 0, "ubx", x0);
    //   5. Solve:
    //         status = aiming_mpc_acados_solve(capsule);
    //         impl_->last_qp_iter  = nlp_solver->get_int("nlp_iter");
    //         impl_->last_residual = ...;
    //   6. Read out u_0 and return as torque commands.
    //
    // While the HW5 blank is unfilled, fall back to a clamped feed-forward
    // that matches the reference rate — guarantees the binary
    // compiles and tests for the controller's *interface* still pass,
    // even if performance is dramatically below the MPC.
    (void)yaw_ref_rate;
    (void)pitch_ref_rate;
    (void)dt;

    PidCommand out;
    out.yaw_torque_cmd = (yaw_ref - state.yaw) * 1.0;     // tiny gain, no real control
    out.pitch_torque_cmd = (pitch_ref - state.pitch) * 1.0;
    if (out.yaw_torque_cmd >  cfg_.torque_limit_yaw)  out.yaw_torque_cmd = cfg_.torque_limit_yaw;
    if (out.yaw_torque_cmd < -cfg_.torque_limit_yaw)  out.yaw_torque_cmd = -cfg_.torque_limit_yaw;
    if (out.pitch_torque_cmd >  cfg_.torque_limit_pitch) out.pitch_torque_cmd = cfg_.torque_limit_pitch;
    if (out.pitch_torque_cmd < -cfg_.torque_limit_pitch) out.pitch_torque_cmd = -cfg_.torque_limit_pitch;
    return out;
}

}  // namespace mpc
}  // namespace aiming_hw
