// IMM mode probabilities sanity:
//   * sum to 1 each step
//   * stay non-negative
//   * the correct mode dominates after a handful of steps when the
//     true motion clearly matches it
//
// Skips when predict / update / IMM step return identity stubs.

#include <gtest/gtest.h>

#include <cmath>

#include "aiming_hw/ekf/imm.hpp"

namespace {

bool imm_is_stub() {
    using namespace aiming_hw::ekf;
    Imm imm;
    StateVec x0;
    x0 << 0.0, 0.0, 1.0, 0.0;
    imm.initialise(x0, StateMat::Identity());
    MeasMat R = MeasMat::Identity() * 0.04;
    auto out = imm.step(0.1, MeasVec(0.1, 0.0), R);
    // Real IMM moves the position estimate toward the measurement;
    // stub returns identity (zero state).
    return out.x.norm() < 0.05;
}

}  // namespace

TEST(HW3ImmModeProbabilities, SumsToOneEveryStep) {
    if (imm_is_stub()) GTEST_SKIP() << "imm/predict/update unimplemented";
    using namespace aiming_hw::ekf;
    Imm imm;
    StateVec x0;
    x0 << 0.0, 0.0, 1.0, 0.0;
    imm.initialise(x0, StateMat::Identity());
    MeasMat R = MeasMat::Identity() * 0.04;

    double t = 0.0;
    double dt = 1.0 / 60.0;
    for (int i = 0; i < 60; ++i) {
        t += dt;
        // Straight-line ground truth (CV).
        MeasVec z(t, 0.0);
        imm.step(dt, z, R);
        Eigen::Vector2d mu = imm.mode_probabilities();
        EXPECT_GE(mu(0), 0.0);
        EXPECT_GE(mu(1), 0.0);
        EXPECT_NEAR(mu(0) + mu(1), 1.0, 1e-9);
    }
}

TEST(HW3ImmModeProbabilities, StraightLineFavoursCV) {
    if (imm_is_stub()) GTEST_SKIP() << "imm/predict/update unimplemented";
    using namespace aiming_hw::ekf;
    Imm imm;
    StateVec x0;
    x0 << 0.0, 0.0, 1.0, 0.0;
    imm.initialise(x0, StateMat::Identity());
    MeasMat R = MeasMat::Identity() * 0.0025;

    double t = 0.0;
    double dt = 1.0 / 60.0;
    for (int i = 0; i < 200; ++i) {
        t += dt;
        imm.step(dt, MeasVec(t, 0.0), R);
    }
    // After 200 steps of constant-velocity motion the CV mode (index 0)
    // should clearly dominate.
    Eigen::Vector2d mu = imm.mode_probabilities();
    EXPECT_GT(mu(0), 0.7) << "mu = " << mu.transpose();
}

TEST(HW3ImmModeProbabilities, ConstantTurnFavoursCT) {
    if (imm_is_stub()) GTEST_SKIP() << "imm/predict/update unimplemented";
    using namespace aiming_hw::ekf;
    ImmConfig cfg = ImmConfig::default_config();
    cfg.omega_ct = 4.0;
    Imm imm(cfg);
    StateVec x0;
    x0 << 0.0, 0.0, 1.0, 0.0;
    imm.initialise(x0, StateMat::Identity());
    MeasMat R = MeasMat::Identity() * 0.0025;

    double t = 0.0;
    double dt = 1.0 / 60.0;
    double px = 0.0;
    double py = 0.0;
    double vx = 1.0;
    double vy = 0.0;
    const double omega = 4.0;
    for (int i = 0; i < 240; ++i) {
        t += dt;
        // Curved ground truth.
        const double s = std::sin(omega * dt);
        const double c = std::cos(omega * dt);
        const double dpx = (s / omega) * vx + (-(1.0 - c) / omega) * vy;
        const double dpy = ((1.0 - c) / omega) * vx + (s / omega) * vy;
        px += dpx;
        py += dpy;
        const double new_vx = c * vx - s * vy;
        const double new_vy = s * vx + c * vy;
        vx = new_vx;
        vy = new_vy;
        imm.step(dt, MeasVec(px, py), R);
    }
    Eigen::Vector2d mu = imm.mode_probabilities();
    EXPECT_GT(mu(1), 0.5) << "mu = " << mu.transpose();
}
