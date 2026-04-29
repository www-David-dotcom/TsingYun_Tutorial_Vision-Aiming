#pragma once

// Top-level multi-target tracker. Owns one IMM per active track and
// drives data association on each frame. The candidate doesn't edit
// this header — they only fill the four lower-level TODO targets in
// kalman_step.cpp / imm.cpp / data_association.cpp; this class wires
// the pieces together.

#include <cstdint>
#include <vector>

#include "aiming_hw/ekf/data_association.hpp"
#include "aiming_hw/ekf/imm.hpp"

namespace aiming_hw {
namespace ekf {

struct TrackerConfig {
    ImmConfig imm = ImmConfig::default_config();
    double    measurement_noise_m = 0.05;     // diagonal R, m
    double    initial_position_var = 1.0;
    double    initial_velocity_var = 4.0;
    double    gate = kGate99Dof2;
    int       coast_max_steps = 10;           // drop a track after N missed detections
};

struct Track {
    int            id;
    Imm            imm;
    int            misses_in_a_row = 0;
    std::uint64_t  last_update_ns = 0;
};

struct PublishedTrack {
    int            id;
    StateVec       x;
    StateMat       P;
    Eigen::Vector2d mode_probabilities;
};

class Tracker {
public:
    explicit Tracker(const TrackerConfig& cfg = TrackerConfig{});

    // Drive one frame's worth of detections forward. dt_s is the
    // wall-clock delta from the last call. Returns one entry per
    // currently-tracked target (after gating and association).
    std::vector<PublishedTrack> step(double dt_s,
                                     std::uint64_t stamp_ns,
                                     const std::vector<MeasVec>& detections);

    const TrackerConfig& config() const noexcept { return cfg_; }
    std::size_t track_count() const noexcept { return tracks_.size(); }

private:
    TrackerConfig            cfg_;
    std::vector<Track>       tracks_;
    int                      next_track_id_ = 0;

    Track make_track(const MeasVec& z, std::uint64_t stamp_ns);
};

}  // namespace ekf
}  // namespace aiming_hw
