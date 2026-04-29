#include "aiming_hw/ekf/data_association.hpp"

#include "aiming_hw/ekf/motion_models.hpp"

namespace aiming_hw {
namespace ekf {

double mahalanobis_cost(const TrackBelief& track,
                        const MeasVec& detection,
                        const MeasMat& R) {
    // TODO(HW3): squared Mahalanobis distance.
    //
    //   y = z - H x
    //   S = H P H^T + R
    //   return y^T * S^{-1} * y     (a scalar)
    //
    // Use measurement_matrix() from motion_models.hpp for H. Pull
    // `.value()` off the resulting 1x1 expression to coerce it to
    // double — Eigen returns Matrix<double,1,1> from `y.transpose() * X * y`.
    (void)track;
    (void)detection;
    (void)R;
    return 0.0;
}

std::vector<AssignmentPair> hungarian_assign(
    const std::vector<std::vector<double>>& cost,
    double gate) {
    // TODO(HW3): minimum-cost bipartite assignment with a gating
    // cutoff.
    //
    // The reference implementation in reference/ekf_python.py
    // delegates to scipy. You write the O(n³) Hungarian algorithm
    // from scratch — it's a textbook exercise and keeps the C++
    // dependency footprint small.
    //
    // Algorithm sketch (Munkres / Kuhn–Munkres, square-matrix form):
    //   1. Pad to square with cost = +inf if rows ≠ cols.
    //   2. Subtract row minima, then column minima, so each row and
    //      column has at least one zero.
    //   3. Cover all zeros with the minimum number of lines; if that
    //      number == n, an optimal assignment exists; else adjust
    //      uncovered entries by the smallest uncovered value and
    //      repeat.
    //   4. Pick a zero per row that doesn't share a column with any
    //      other picked zero — that's the assignment.
    //
    // After the inner matching, walk the pairs and only keep the
    // ones whose ORIGINAL cost (before padding) is ≤ `gate`.
    //
    // Tip: many references work on a working copy of `cost` so you
    // don't lose the gating values when you subtract row/col minima.
    (void)cost;
    (void)gate;
    return {};
}

}  // namespace ekf
}  // namespace aiming_hw
