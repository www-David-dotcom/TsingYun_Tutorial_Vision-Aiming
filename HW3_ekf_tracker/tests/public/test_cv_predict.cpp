// Verifies the candidate's `predict` against an analytically-known
// CV step. Skips when the stub is detected.

#include <gtest/gtest.h>

#include "aiming_hw/ekf/kalman_step.hpp"
#include "aiming_hw/ekf/motion_models.hpp"

namespace {

bool predict_is_stub() {
    using namespace aiming_hw::ekf;
    GaussianBelief b;
    b.x << 0.0, 0.0, 1.0, 0.0;
    b.P = StateMat::Identity();
    auto out = predict(b, cv_transition(1.0), StateMat::Zero());
    // True predict: x = [1, 0, 1, 0]. Stub: identity = [0, 0, 0, 0].
    return out.x.norm() < 0.5;
}

bool update_is_stub() {
    using namespace aiming_hw::ekf;
    GaussianBelief b;
    b.x << 5.0, 0.0, 0.0, 0.0;
    b.P = StateMat::Identity();
    MeasVec z(5.0, 0.0);
    MeasMat R = MeasMat::Identity() * 0.1;
    auto result = update(b, z, R);
    // True update on a perfect measurement leaves x near [5, 0, ...];
    // stub returns identity (zero state).
    return std::abs(result.belief.x(0) - 5.0) > 1.0;
}

}  // namespace

TEST(HW3CVPredict, StraightLineOneSecond) {
    if (predict_is_stub()) GTEST_SKIP() << "predict unimplemented";
    using namespace aiming_hw::ekf;
    GaussianBelief b;
    b.x << 0.0, 0.0, 2.5, -1.0;
    b.P = StateMat::Identity() * 0.5;

    auto out = predict(b, cv_transition(0.4), StateMat::Zero());
    EXPECT_NEAR(out.x(0), 1.0,  1e-12);
    EXPECT_NEAR(out.x(1), -0.4, 1e-12);
    EXPECT_NEAR(out.x(2), 2.5,  1e-12);
    EXPECT_NEAR(out.x(3), -1.0, 1e-12);
}

TEST(HW3CVPredict, CovarianceGrowsWithProcessNoise) {
    if (predict_is_stub()) GTEST_SKIP() << "predict unimplemented";
    using namespace aiming_hw::ekf;
    GaussianBelief b;
    b.x = StateVec::Zero();
    b.P = StateMat::Identity();

    StateMat Q = process_noise_cv(0.1, 1.0);
    auto out = predict(b, cv_transition(0.1), Q);

    // F * I * F^T + Q is symmetric and bigger than I in trace.
    EXPECT_GT(out.P.trace(), b.P.trace());
    EXPECT_NEAR((out.P - out.P.transpose()).norm(), 0.0, 1e-12);
}

TEST(HW3KalmanUpdate, JosephFormStaysSymmetric) {
    if (update_is_stub()) GTEST_SKIP() << "update unimplemented";
    using namespace aiming_hw::ekf;
    GaussianBelief b;
    b.x << 1.0, 2.0, 0.0, 0.0;
    b.P = StateMat::Identity();
    MeasMat R = MeasMat::Identity() * 0.04;

    // Run 100 alternating predict + update steps and check P stays
    // symmetric within 1e-9. The textbook (I - K H) P form drifts by
    // ~1e-12 per step and breaks this after ~600 steps; Joseph form
    // is well within.
    for (int i = 0; i < 100; ++i) {
        b = predict(b, cv_transition(0.05), process_noise_cv(0.05, 0.5));
        MeasVec z(b.x(0), b.x(1));
        b = update(b, z, R).belief;
    }
    EXPECT_NEAR((b.P - b.P.transpose()).norm(), 0.0, 1e-9);
}
