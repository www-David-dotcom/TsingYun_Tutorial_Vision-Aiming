#include "aiming_hw/mpc/pid_baseline.hpp"

#include <algorithm>

namespace aiming_hw {
namespace mpc {

namespace {

double clamp(double x, double lo, double hi) {
    return std::clamp(x, lo, hi);
}

double single_axis_step(double q,
                        double q_ref,
                        double q_rate,
                        double q_ref_rate,
                        const PidGains& gains,
                        double dt) {
    (void)dt;  // PD has no per-step state — dt is part of the gain choice.
    const double err  = q_ref - q;
    const double derr = q_ref_rate - q_rate;
    const double cmd  = gains.kp * err + gains.kd * derr;
    return clamp(cmd, -gains.torque_limit_nm, +gains.torque_limit_nm);
}

}  // namespace

PidCommand PidController::step(const PidState& state,
                               double yaw_ref,
                               double pitch_ref,
                               double yaw_ref_rate,
                               double pitch_ref_rate,
                               double dt) {
    PidCommand out;
    out.yaw_torque_cmd = single_axis_step(state.yaw, yaw_ref,
                                          state.yaw_rate, yaw_ref_rate,
                                          yaw_gains_, dt);
    out.pitch_torque_cmd = single_axis_step(state.pitch, pitch_ref,
                                            state.pitch_rate, pitch_ref_rate,
                                            pitch_gains_, dt);
    return out;
}

}  // namespace mpc
}  // namespace aiming_hw
