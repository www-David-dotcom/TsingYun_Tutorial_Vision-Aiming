#include "aiming_hw/mpc/pid_baseline.hpp"

#include <algorithm>

namespace aiming_hw {
namespace mpc {

namespace {

double clamp(double x, double lo, double hi) {
    return std::clamp(x, lo, hi);
}

}  // namespace

double PidController::single_axis_step(double q,
                                       double q_ref,
                                       double q_rate,
                                       double q_ref_rate,
                                       const PidGains& gains,
                                       double dt,
                                       double& applied_torque_est,
                                       double& previous_command) {
    if (dt > 0.0 && gains.motor_lag_tc_s > 0.0) {
        applied_torque_est += dt * (previous_command - applied_torque_est) /
                              gains.motor_lag_tc_s;
    } else {
        applied_torque_est = previous_command;
    }

    const double err = q_ref - q;
    const double q_rate_limited = clamp(q_rate, -gains.rate_limit_rps,
                                        gains.rate_limit_rps);
    const double q_ref_rate_limited = clamp(q_ref_rate, -gains.rate_limit_rps,
                                            gains.rate_limit_rps);
    const double derr = q_ref_rate_limited - q_rate_limited;
    const double desired_accel = gains.kp * err + gains.kd * derr;
    const double desired_torque = gains.inertia_kgm2 * desired_accel;
    const double command = applied_torque_est +
                           (desired_torque - applied_torque_est) * 20.0;
    previous_command = clamp(command, -gains.torque_limit_nm,
                             +gains.torque_limit_nm);
    return previous_command;
}

PidCommand PidController::step(const PidState& state,
                               double yaw_ref,
                               double pitch_ref,
                               double yaw_ref_rate,
                               double pitch_ref_rate,
                               double dt) {
    PidCommand out;
    out.yaw_torque_cmd = single_axis_step(state.yaw, yaw_ref,
                                          state.yaw_rate, yaw_ref_rate,
                                          yaw_gains_, dt,
                                          yaw_applied_torque_est_,
                                          previous_yaw_command_);
    out.pitch_torque_cmd = single_axis_step(state.pitch, pitch_ref,
                                            state.pitch_rate, pitch_ref_rate,
                                            pitch_gains_, dt,
                                            pitch_applied_torque_est_,
                                            previous_pitch_command_);
    return out;
}

}  // namespace mpc
}  // namespace aiming_hw
