#include "aiming_hw/ekf/tracker.hpp"

#include <algorithm>
#include <vector>

namespace aiming_hw {
namespace ekf {

Tracker::Tracker(const TrackerConfig& cfg) : cfg_(cfg) {}

Track Tracker::make_track(const MeasVec& z, std::uint64_t stamp_ns) {
    Imm imm(cfg_.imm);
    StateVec x0;
    x0 << z(0), z(1), 0.0, 0.0;
    StateMat P0 = StateMat::Zero();
    P0(0, 0) = cfg_.initial_position_var;
    P0(1, 1) = cfg_.initial_position_var;
    P0(2, 2) = cfg_.initial_velocity_var;
    P0(3, 3) = cfg_.initial_velocity_var;
    imm.initialise(x0, P0);
    return Track{next_track_id_++, std::move(imm), 0, stamp_ns};
}

std::vector<PublishedTrack> Tracker::step(double dt_s,
                                          std::uint64_t stamp_ns,
                                          const std::vector<MeasVec>& detections) {
    const MeasMat R =
        MeasMat::Identity() * (cfg_.measurement_noise_m * cfg_.measurement_noise_m);

    // Build the cost matrix (rows = tracks, cols = detections).
    std::vector<std::vector<double>> cost(
        tracks_.size(), std::vector<double>(detections.size(), 0.0));
    for (std::size_t i = 0; i < tracks_.size(); ++i) {
        const auto& belief = tracks_[i].imm.mode_beliefs();
        // Use a quick "predict-only" snapshot of mode 0 for gating —
        // the per-mode predict happens inside Imm::step. We need only
        // a coarse cost for assignment.
        TrackBelief tb{belief[0].x, belief[0].P};
        for (std::size_t j = 0; j < detections.size(); ++j) {
            cost[i][j] = mahalanobis_cost(tb, detections[j], R);
        }
    }

    auto pairs = hungarian_assign(cost, cfg_.gate);

    std::vector<bool> track_matched(tracks_.size(), false);
    std::vector<bool> det_matched(detections.size(), false);

    std::vector<PublishedTrack> out;
    out.reserve(tracks_.size() + detections.size());

    for (const auto& pair : pairs) {
        Track& tr = tracks_[pair.track_index];
        const MeasVec& z = detections[pair.detection_index];
        GaussianBelief b = tr.imm.step(dt_s, z, R);
        tr.misses_in_a_row = 0;
        tr.last_update_ns = stamp_ns;
        track_matched[pair.track_index] = true;
        det_matched[pair.detection_index] = true;
        out.push_back(PublishedTrack{tr.id, b.x, b.P, tr.imm.mode_probabilities()});
    }

    // Coast unmatched tracks: keep predicting but don't update. We
    // do this by stepping with an infinite-variance "phantom"
    // measurement gated out — simpler in practice to skip the IMM
    // step entirely and fall back to the latest belief as the
    // published state.
    for (std::size_t i = 0; i < tracks_.size(); ++i) {
        if (track_matched[i]) continue;
        Track& tr = tracks_[i];
        tr.misses_in_a_row += 1;
        const auto& belief = tr.imm.mode_beliefs()[0];
        out.push_back(PublishedTrack{
            tr.id, belief.x, belief.P, tr.imm.mode_probabilities()});
    }

    // Spawn fresh tracks from unmatched detections.
    for (std::size_t j = 0; j < detections.size(); ++j) {
        if (det_matched[j]) continue;
        tracks_.push_back(make_track(detections[j], stamp_ns));
        const auto& tr = tracks_.back();
        const auto& belief = tr.imm.mode_beliefs()[0];
        out.push_back(PublishedTrack{
            tr.id, belief.x, belief.P, tr.imm.mode_probabilities()});
    }

    // Drop coasted tracks whose miss count crossed the threshold.
    tracks_.erase(
        std::remove_if(tracks_.begin(), tracks_.end(),
                       [this](const Track& tr) {
                           return tr.misses_in_a_row > cfg_.coast_max_steps;
                       }),
        tracks_.end());

    return out;
}

}  // namespace ekf
}  // namespace aiming_hw
