#pragma once

// Two-mode IMM (CV + CT). The mixing / per-mode update / mode
// probability update / state combination are the four steps the
// candidate fills via TODO(HW3): markers in source/imm.cpp.
//
// Mode 0 = CV; mode 1 = CT with the configured turn rate.

#include <array>

#include "aiming_hw/ekf/kalman_step.hpp"
#include "aiming_hw/ekf/motion_models.hpp"

namespace aiming_hw {
namespace ekf {

struct ImmConfig {
    Eigen::Matrix2d transition_matrix;     // π — pi[i,j] = P(mode_{k+1}=j | mode_k=i)
    double          omega_ct      = 4.0;   // rad/s for the CT mode
    double          sigma_a_cv    = 1.0;   // process noise stddev (m/s²) for CV
    double          sigma_a_ct    = 2.5;   // ditto for CT — slightly larger to absorb model error

    static ImmConfig default_config() {
        ImmConfig c;
        c.transition_matrix << 0.95, 0.05,
                               0.10, 0.90;
        return c;
    }
};

class Imm {
public:
    explicit Imm(const ImmConfig& cfg = ImmConfig::default_config());

    void initialise(const StateVec& x0, const StateMat& P0);

    // Run one IMM step: mix → predict+update both modes → reweight →
    // combine. Returns the combined posterior belief.
    GaussianBelief step(double dt, const MeasVec& z, const MeasMat& R);

    Eigen::Vector2d mode_probabilities() const noexcept { return mu_; }

    const std::array<GaussianBelief, 2>& mode_beliefs() const noexcept {
        return beliefs_;
    }

private:
    ImmConfig                       cfg_;
    std::array<GaussianBelief, 2>   beliefs_;
    Eigen::Vector2d                 mu_ = Eigen::Vector2d(0.5, 0.5);
};

}  // namespace ekf
}  // namespace aiming_hw
