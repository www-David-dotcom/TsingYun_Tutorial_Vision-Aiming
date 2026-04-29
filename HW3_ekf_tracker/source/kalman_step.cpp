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
    // TODO(HW3): multivariate-normal pdf at the innovation y under
    // covariance S. Used by IMM to weight each mode's posterior.
    //
    //   p(y) = 1 / sqrt((2π)^d * |S|) * exp(-0.5 * y^T S^{-1} y)
    //
    // Here d = 2 (the measurement dimension) so (2π)^d = 4π² ≈ 39.48.
    // Guard against |S| ≤ 0 (numerical issue with rank-deficient S);
    // return ~1e-300 in that case so the IMM doesn't divide by zero.
    //
    // Hint: pull `.value()` off `(y.transpose() * S.inverse() * y)`
    // to coerce the 1×1 expression to a double.
    //
    // While this stub returns 0.0, the IMM's mode-probability update
    // sees zero likelihoods for both modes, falls back to the
    // uniform-prior recovery branch, and never lets either mode
    // dominate. The `StraightLineFavoursCV` and `ConstantTurnFavoursCT`
    // tests detect this state via the `imm_is_stub` sentinel and
    // skip cleanly.
    (void)y;
    (void)S;
    return 0.0;
}

}  // namespace ekf
}  // namespace aiming_hw
