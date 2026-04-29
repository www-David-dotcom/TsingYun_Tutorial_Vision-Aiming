// PID tracks a 1 Hz sinusoidal yaw reference. RMSE in steady state
// (after the first second) must stay below the documented bar.

#include <gtest/gtest.h>

#include <cmath>
#include <vector>

#include "aiming_hw/mpc/pid_baseline.hpp"

namespace {

struct AxisSim {
    double angle = 0.0;
    double rate = 0.0;
    double torque = 0.0;
    double inertia = 0.012;
    double motor_lag_tc = 0.04;

    void step(double torque_cmd, double dt) {
        torque += dt * (torque_cmd - torque) / motor_lag_tc;
        rate   += dt * torque / inertia;
        angle  += dt * rate;
    }
};

}  // namespace

TEST(HW5SinusoidTracking, RmseUnder0p05Rad) {
    using aiming_hw::mpc::PidController;
    using aiming_hw::mpc::PidGains;
    using aiming_hw::mpc::PidState;

    PidGains yaw_gains;
    PidGains pitch_gains;
    PidController pid(yaw_gains, pitch_gains);
    AxisSim sim;

    const double dt = 0.001;
    const double duration = 3.0;
    const double freq_hz = 1.0;
    const double amp_rad = 0.5;
    const int n = static_cast<int>(duration / dt);

    double sq_err_sum = 0.0;
    int    sq_err_n   = 0;
    for (int k = 0; k < n; ++k) {
        const double t = k * dt;
        const double ref      = amp_rad * std::sin(2.0 * M_PI * freq_hz * t);
        const double ref_rate = amp_rad * 2.0 * M_PI * freq_hz *
                                std::cos(2.0 * M_PI * freq_hz * t);
        PidState state{sim.angle, 0.0, sim.rate, 0.0};
        auto cmd = pid.step(state, ref, 0.0, ref_rate, 0.0, dt);
        sim.step(cmd.yaw_torque_cmd, dt);
        // Skip the first 1 s; we only score the steady-state phase.
        if (t > 1.0) {
            const double err = sim.angle - ref;
            sq_err_sum += err * err;
            ++sq_err_n;
        }
    }
    const double rmse = std::sqrt(sq_err_sum / sq_err_n);
    EXPECT_LT(rmse, 0.05)
        << "tracking RMSE = " << rmse << " rad";
}
