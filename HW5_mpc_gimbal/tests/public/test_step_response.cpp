// PID step response. Pins the BASELINE the candidate's MPC must beat:
//   * settling time < 200 ms for a 30° step
//   * overshoot < 5%
//
// The MPC path needs the acados-codegened solver under
// generated_solver/, which only exists after `python
// src/generate_acados.py`. This test runs the PID controller only —
// the MPC's acceptance is a separate test the candidate adds once
// they've codegened.

#include <gtest/gtest.h>

#include <cmath>
#include <vector>

#include "aiming_hw/mpc/pid_baseline.hpp"

namespace {

using aiming_hw::mpc::PidController;
using aiming_hw::mpc::PidGains;
using aiming_hw::mpc::PidState;

// Single-axis simulation: torque → motor lag → angular acceleration.
// Matches the dynamics in src/model.py so the PID test exercises the
// same simulator the MPC will eventually be evaluated against.
struct AxisSim {
    double angle = 0.0;
    double rate  = 0.0;
    double torque = 0.0;          // applied (after motor lag)
    double inertia = 0.012;
    double motor_lag_tc = 0.04;

    void step(double torque_cmd, double dt) {
        torque += dt * (torque_cmd - torque) / motor_lag_tc;
        rate   += dt * torque / inertia;
        angle  += dt * rate;
    }
};

}  // namespace

TEST(HW5StepResponse, SettlingTimeUnder200ms) {
    PidGains yaw_gains;       // defaults: kp=80, kd=4
    PidGains pitch_gains;
    PidController pid(yaw_gains, pitch_gains);
    AxisSim sim;
    sim.inertia = 0.012;
    sim.motor_lag_tc = 0.04;

    const double target = 30.0 * M_PI / 180.0;
    const double dt = 0.001;
    const int n = 500;     // 500 ms

    int last_outside = 0;
    for (int k = 0; k < n; ++k) {
        PidState state{sim.angle, 0.0, sim.rate, 0.0};
        auto cmd = pid.step(state, target, 0.0, 0.0, 0.0, dt);
        sim.step(cmd.yaw_torque_cmd, dt);
        if (std::abs(sim.angle - target) > 0.05 * std::abs(target)) {
            last_outside = k;
        }
    }
    const double settling_ms = (last_outside + 1) * dt * 1000.0;
    EXPECT_LT(settling_ms, 200.0)
        << "settling = " << settling_ms << " ms";
}

TEST(HW5StepResponse, OvershootUnder5Percent) {
    PidGains yaw_gains;
    PidGains pitch_gains;
    PidController pid(yaw_gains, pitch_gains);
    AxisSim sim;
    sim.inertia = 0.012;
    sim.motor_lag_tc = 0.04;

    const double target = 30.0 * M_PI / 180.0;
    const double dt = 0.001;
    const int n = 500;

    double peak = 0.0;
    for (int k = 0; k < n; ++k) {
        PidState state{sim.angle, 0.0, sim.rate, 0.0};
        auto cmd = pid.step(state, target, 0.0, 0.0, 0.0, dt);
        sim.step(cmd.yaw_torque_cmd, dt);
        if (sim.angle > peak) peak = sim.angle;
    }
    const double overshoot_pct = (peak - target) / target * 100.0;
    EXPECT_LT(overshoot_pct, 5.0)
        << "overshoot = " << overshoot_pct << "%";
}

TEST(HW5StepResponse, TorqueRespectsLimit) {
    PidGains yaw_gains;
    yaw_gains.torque_limit_nm = 1.6;
    PidGains pitch_gains;
    PidController pid(yaw_gains, pitch_gains);

    PidState state{0.0, 0.0, 0.0, 0.0};
    // A huge step should saturate the torque output.
    auto cmd = pid.step(state, 1.0, 0.0, 0.0, 0.0, 0.001);
    EXPECT_LE(cmd.yaw_torque_cmd, yaw_gains.torque_limit_nm + 1e-9);
    EXPECT_GE(cmd.yaw_torque_cmd, -yaw_gains.torque_limit_nm - 1e-9);
}
