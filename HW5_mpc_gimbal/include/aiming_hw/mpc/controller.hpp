#pragma once

// MPC gimbal controller. Wraps the acados-codegened solver and
// exposes the same `step` API as the PID baseline so HW6's runner can
// swap between them via a config flag.
//
// The candidate fills `MpcController::step` once they've run
// `python src/generate_acados.py` and have the C library under
// generated_solver/. The header itself is filled — only the
// implementation in source/controller.cpp has TODO(HW5):.
//
// If `generated_solver/` doesn't exist at configure time, the HW5
// CMakeLists.txt drops the MPC target entirely and only the PID
// baseline ships. This keeps the project configurable for
// contributors who don't have acados installed.

#include "aiming_hw/mpc/pid_baseline.hpp"

namespace aiming_hw {
namespace mpc {

struct MpcConfig {
    int    horizon_steps     = 20;
    double step_seconds      = 0.01;
    double rate_limit_yaw    = 12.0;
    double rate_limit_pitch  = 8.0;
    double torque_limit_yaw  = 1.6;
    double torque_limit_pitch = 1.2;
    double pitch_limit_lo    = -0.35;
    double pitch_limit_hi    = 0.52;
};

class MpcController {
public:
    explicit MpcController(const MpcConfig& cfg = MpcConfig{});
    ~MpcController();

    // Same signature as PidController::step. The MPC reads the
    // candidate's tuned weights from the codegen step (compiled into
    // the library), so there's no runtime YAML parse on the hot path.
    PidCommand step(const PidState& state,
                    double yaw_ref,
                    double pitch_ref,
                    double yaw_ref_rate,
                    double pitch_ref_rate,
                    double dt);

    // Last QP iteration count + RTI residual — useful for HW6's
    // runtime-stats panel. Both are 0 when the solver isn't
    // configured.
    int    last_qp_iter() const noexcept;
    double last_residual() const noexcept;

private:
    struct Impl;
    Impl* impl_;        // raw pointer so the header doesn't pull in <memory>
    MpcConfig cfg_;
};

}  // namespace mpc
}  // namespace aiming_hw
