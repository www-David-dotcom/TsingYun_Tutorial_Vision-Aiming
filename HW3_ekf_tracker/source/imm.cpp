#include "aiming_hw/ekf/imm.hpp"

#include <algorithm>

namespace aiming_hw {
namespace ekf {

Imm::Imm(const ImmConfig& cfg) : cfg_(cfg) {
    beliefs_[0] = GaussianBelief::identity();
    beliefs_[1] = GaussianBelief::identity();
}

void Imm::initialise(const StateVec& x0, const StateMat& P0) {
    beliefs_[0] = GaussianBelief{x0, P0};
    beliefs_[1] = GaussianBelief{x0, P0};
    mu_ = Eigen::Vector2d(0.5, 0.5);
}

GaussianBelief Imm::step(double dt, const MeasVec& z, const MeasMat& R) {
    // ----- TODO(HW3): part A: mixing.
    //
    // For each output mode j ∈ {0, 1}:
    //   c_j = Σ_i π_{ij} * mu_i                 (normaliser)
    //   mu_{i|j} = π_{ij} * mu_i / c_j          (mixing weights)
    //   x_j_mix = Σ_i mu_{i|j} * x_i
    //   P_j_mix = Σ_i mu_{i|j} * (P_i + (x_i - x_j_mix)(x_i - x_j_mix)^T)
    //
    // Then write `mixed[0]` and `mixed[1]` of type GaussianBelief.
    std::array<GaussianBelief, 2> mixed{};
    Eigen::Vector2d c = Eigen::Vector2d::Zero();
    // (void)c, (void)mixed;     // ← remove these once you start.

    // ----- per-mode predict + update (filled).
    const StateMat F_cv = cv_transition(dt);
    const StateMat F_ct = ct_transition(dt, cfg_.omega_ct);
    const StateMat Q_cv = process_noise_cv(dt, cfg_.sigma_a_cv);
    const StateMat Q_ct = process_noise_ct(dt, cfg_.sigma_a_ct);

    std::array<GaussianBelief, 2> updated{};
    Eigen::Vector2d likelihoods = Eigen::Vector2d::Zero();
    for (int j = 0; j < 2; ++j) {
        const StateMat& F = (j == 0) ? F_cv : F_ct;
        const StateMat& Q = (j == 0) ? Q_cv : Q_ct;
        GaussianBelief predicted = predict(mixed[j], F, Q);
        UpdateResult result = update(predicted, z, R);
        updated[j] = result.belief;
        likelihoods(j) = gaussian_likelihood(result.innovation,
                                             result.innovation_cov);
    }

    // ----- TODO(HW3): part B: mode probability update.
    //
    //   mu_j_new = c_j * Λ_j   (un-normalised)
    //   then divide by Σ_k mu_k_new.
    //
    // Numerical safety: if every likelihood is zero (the gate
    // rejected everything), reset to a uniform 0.5/0.5 prior so the
    // tracker recovers gracefully.
    Eigen::Vector2d mu_new = Eigen::Vector2d(0.5, 0.5);
    (void)c;
    (void)likelihoods;

    beliefs_ = updated;
    mu_ = mu_new;

    // ----- TODO(HW3): part C: state combination.
    //
    //   x_comb = Σ_j mu_j * x_j
    //   P_comb = Σ_j mu_j * (P_j + (x_j - x_comb)(x_j - x_comb)^T)
    //
    // This is the belief the public API returns.
    GaussianBelief combined = GaussianBelief::identity();
    return combined;
}

}  // namespace ekf
}  // namespace aiming_hw
