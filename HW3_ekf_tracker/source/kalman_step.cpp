#include "aiming_hw/ekf/kalman_step.hpp"

#include <cmath>

namespace aiming_hw {
namespace ekf {

GaussianBelief predict(const GaussianBelief& belief,
                       const StateMat& F,
                       const StateMat& Q) {
    // TODO(HW3): linear Kalman predict.
    //
    //   x' = F * x
    //   P' = F * P * F^T + Q
    //
    // Two lines once you've thought about it. Eigen's `*` is matrix
    // multiplication; `transpose()` returns the transpose by reference.
    (void)belief;
    (void)F;
    (void)Q;
    return GaussianBelief::identity();
}

UpdateResult update(const GaussianBelief& belief,
                    const MeasVec& z,
                    const MeasMat& R) {
    // TODO(HW3): EKF update with the Joseph-form covariance update.
    //
    //   y = z - H * x
    //   S = H * P * H^T + R
    //   K = P * H^T * S^{-1}
    //   x' = x + K * y
    //   P' = (I - K H) * P * (I - K H)^T + K * R * K^T   // Joseph form
    //
    // Use measurement_matrix() from motion_models.hpp for H.
    //
    // The Joseph form costs ~30% more flops than the textbook
    // P' = (I - K H) P, but it stays numerically symmetric over the
    // 1800-step replay test. The naive form drifts by ~1e-12 per step
    // and breaks the test's 1e-9 tolerance after 600 steps.
    //
    // Return the innovation y and innovation covariance S alongside
    // the new belief — the IMM uses them to weight mode probabilities
    // without redoing the linear algebra.
    (void)belief;
    (void)z;
    (void)R;
    return UpdateResult{
        GaussianBelief::identity(),
        MeasVec::Zero(),
        MeasMat::Identity(),
    };
}

double gaussian_likelihood(const MeasVec& y, const MeasMat& S) {
    constexpr double kTwoPi = 2.0 * 3.141592653589793238462643383279502884;
    const double det_S = S.determinant();
    if (det_S <= 0.0) {
        return 1e-300;
    }
    const double norm = 1.0 / std::sqrt(kTwoPi * kTwoPi * det_S);
    const double expo = -0.5 * (y.transpose() * S.inverse() * y).value();
    return norm * std::exp(expo);
}

}  // namespace ekf
}  // namespace aiming_hw
