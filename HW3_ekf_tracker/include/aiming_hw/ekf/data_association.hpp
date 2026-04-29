#pragma once

// Multi-target data association. Two TODO(HW3) targets:
//   * `mahalanobis_cost` — the per-(track, detection) cost the
//     assignment matrix is built from.
//   * `hungarian_assign` — bipartite minimum-cost matching with a
//     gating cutoff. The candidate writes the O(n³) Hungarian
//     algorithm from scratch (we don't depend on a header-only
//     library to keep the dep footprint low).

#include <vector>

#include "aiming_hw/ekf/motion_models.hpp"

namespace aiming_hw {
namespace ekf {

struct TrackBelief {
    StateVec x;
    StateMat P;
};

struct AssignmentPair {
    int track_index;
    int detection_index;
    double cost;
};

// 99% / dof=2 chi-squared upper bound. Hardcoded so the C++ side
// doesn't need a chi-squared table.
constexpr double kGate99Dof2 = 9.21;

// Mahalanobis distance (squared) between a track's predicted
// measurement and an observation, under additive measurement noise R.
//
// IMPLEMENT THIS — TODO(HW3).
double mahalanobis_cost(const TrackBelief& track,
                        const MeasVec& detection,
                        const MeasMat& R);

// Minimum-cost bipartite matching with a per-pair gating cutoff. The
// `cost` matrix is rows = tracks, cols = detections; cells whose
// cost exceeds `gate` are not eligible for a match. Unmatched tracks
// and detections are simply omitted from the returned list.
//
// IMPLEMENT THIS — TODO(HW3).
std::vector<AssignmentPair> hungarian_assign(
    const std::vector<std::vector<double>>& cost,
    double gate);

}  // namespace ekf
}  // namespace aiming_hw
