#pragma once

// Two-axis PID baseline controller. This is the *floor* the candidate's
// MPC must beat on the leaderboard tracking metric. Filled — no
// TODOs here.
//
// Behaviour:
//   * P + D on each axis (no integral term — the gimbal's natural
//     friction is small enough that integrator wind-up causes more
//     trouble than it solves).
//   * Velocity feedforward from the reference rate.
//   * A small applied-torque observer to compensate the motor lag in
//     the public dynamics model.
//   * Hard torque limits; clamped at the output.
//
// Settling time on a 30° step: ~120 ms with the default gains.

namespace aiming_hw {
namespace mpc {

struct PidGains {
    double kp = 400.0;
    double kd = 30.0;
    double rate_limit_rps = 12.0;
    double torque_limit_nm = 1.6;
    double motor_lag_tc_s = 0.04;
    double inertia_kgm2 = 0.012;
};

struct PidState {
    double yaw         = 0.0;
    double pitch       = 0.0;
    double yaw_rate    = 0.0;
    double pitch_rate  = 0.0;
};

struct PidCommand {
    double yaw_torque_cmd   = 0.0;
    double pitch_torque_cmd = 0.0;
};

class PidController {
public:
    PidController(const PidGains& yaw_gains, const PidGains& pitch_gains)
        : yaw_gains_(yaw_gains), pitch_gains_(pitch_gains) {}

    // Compute the next torque command. dt is the control-loop period
    // in seconds; references are absolute setpoints in radians.
    PidCommand step(const PidState& state,
                    double yaw_ref,
                    double pitch_ref,
                    double yaw_ref_rate,
                    double pitch_ref_rate,
                    double dt);

private:
    double single_axis_step(double q,
                            double q_ref,
                            double q_rate,
                            double q_ref_rate,
                            const PidGains& gains,
                            double dt,
                            double& applied_torque_est,
                            double& previous_command);

    PidGains yaw_gains_;
    PidGains pitch_gains_;
    double yaw_applied_torque_est_ = 0.0;
    double pitch_applied_torque_est_ = 0.0;
    double previous_yaw_command_ = 0.0;
    double previous_pitch_command_ = 0.0;
};

}  // namespace mpc
}  // namespace aiming_hw
