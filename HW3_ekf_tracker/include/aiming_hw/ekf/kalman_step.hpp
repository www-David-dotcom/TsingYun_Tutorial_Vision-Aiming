#pragma once

// Kalman step primitives. `predict` and `update` are the two TODO(HW3):
// targets - the candidate writes the math, the rest of the
// EKF (motion models, IMM, multi-target) is filled.
//
// Joseph-form covariance update is required for `update` — the IMM's
// 1800-step replay test catches asymmetry in P below 1e-9.

#include "aiming_hw/ekf/motion_models.hpp"

namespace aiming_hw {
namespace ekf {

struct GaussianBelief {
    StateVec x;
    StateMat P;

    static GaussianBelief identity() {
        return GaussianBelief{StateVec::Zero(), StateMat::Identity()};
    }
};

// Innovation + innovation covariance returned alongside the new
// belief so callers can compute IMM mode likelihoods without redoing
// the linear algebra.
struct UpdateResult {
    GaussianBelief belief;
    MeasVec        innovation;        // y = z - H x
    MeasMat        innovation_cov;    // S = H P H^T + R
};

// Predict step: x = F x; P = F P F^T + Q.
//
// TODO(HW3): implement the linear Kalman predict step.
GaussianBelief predict(const GaussianBelief& belief,
                       const StateMat& F,
                       const StateMat& Q);

// Update step. Apply the linear measurement model H = [I_2 | 0_2x2]
// (from motion_models::measurement_matrix) to fuse z under additive
// noise R. Use the Joseph-form covariance update for stability.
//
// TODO(HW3): implement the EKF update step.
UpdateResult update(const GaussianBelief& belief,
                    const MeasVec& z,
                    const MeasMat& R);

// Multivariate-normal pdf of the innovation. Used by IMM to weight
// per-mode likelihoods when updating mode probabilities. This one is
// filled — the candidate doesn't need to write it but should
// understand it (it's three lines of Eigen).
double gaussian_likelihood(const MeasVec& y, const MeasMat& S);

}  // namespace ekf
}  // namespace aiming_hw
