#pragma once

// Motion models for the HW3 EKF.
//
// State convention (matches reference/ekf_python.py exactly):
//   x = [px, py, vx, vy]^T   (Vector4d)
//   z = [px, py]^T            (Vector2d)
//
// Two transition matrices are exposed as free functions: a constant-
// velocity (CV) model and a constant-turn (CT) model with a fixed
// angular rate ω. The IMM in imm.hpp blends both.
//
// All math is in double precision — the EKF's covariance update is
// the place numerical noise compounds, and HW3's NEES test catches
// drift below 1e-9 over 1800 steps.

#include <Eigen/Core>

namespace aiming_hw {
namespace ekf {

using StateVec = Eigen::Matrix<double, 4, 1>;
using StateMat = Eigen::Matrix<double, 4, 4>;
using MeasVec  = Eigen::Matrix<double, 2, 1>;
using MeasMat  = Eigen::Matrix<double, 2, 2>;
using ObsMat   = Eigen::Matrix<double, 2, 4>;

// Constant H = [I_2 | 0_2x2] — only position is observed.
inline ObsMat measurement_matrix() {
    ObsMat H = ObsMat::Zero();
    H(0, 0) = 1.0;
    H(1, 1) = 1.0;
    return H;
}

// Constant-velocity transition: F * [px, py, vx, vy] gives next state
// after `dt` assuming straight-line motion.
StateMat cv_transition(double dt);

// Constant-turn transition. ω is the angular rate in rad/s; the
// implementation Taylor-expands around ω = 0 to avoid division-by-
// zero when the curvature is small.
StateMat ct_transition(double dt, double omega);

// Discrete white-noise acceleration covariance matrices. sigma_a is
// the stddev of the per-axis acceleration noise (m/s²).
StateMat process_noise_cv(double dt, double sigma_a);
StateMat process_noise_ct(double dt, double sigma_a);

}  // namespace ekf
}  // namespace aiming_hw
