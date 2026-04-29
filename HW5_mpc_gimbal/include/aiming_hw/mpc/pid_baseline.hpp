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
//   * Hard rate + torque limits; clamped at the output.
//
// Settling time on a 30° step: ~120 ms with the default gains.

namespace aiming_hw {
namespace mpc {

struct PidGains {
    double kp = 80.0;
    double kd = 4.0;
    double rate_limit_rps = 12.0;
    double torque_limit_nm = 1.6;
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
    PidGains yaw_gains_;
    PidGains pitch_gains_;
};

}  // namespace mpc
}  // namespace aiming_hw
