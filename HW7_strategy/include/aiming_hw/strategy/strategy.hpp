#pragma once

// HW7 strategy decision points. Two TODO(HW7) targets that the leaf
// actions in leaf_actions.cpp call. Splitting these out from the
// leaves means the public unit tests can drive them directly without
// instantiating the full BT runtime.

#include <vector>

#include "aiming_hw/strategy/behavior_tree.hpp"

namespace aiming_hw {
namespace strategy {

// One observation row per visible track. The runner builds a
// std::vector<TrackInfo> from HW3's PublishedTrack list and stashes
// the address in the blackboard under `tracks_ptr`. The leaf
// actions cast it back here.
struct TrackInfo {
    int    id;
    double x;
    double y;
    double vx;
    double vy;
    double estimated_hp = 100.0;   // unknown by default; HW6 may infer
    bool   is_ally      = false;
};

struct SelfInfo {
    double x  = 0.0;
    double y  = 0.0;
    double hp = 100.0;
    int    ammo = 200;
};

// Pick the highest-priority track to engage. Returns the index into
// `tracks` or -1 when no track should be engaged (all allies, all
// out-of-range, etc.). The recommended priority is a weighted sum
// of (close, low-HP, not-ally); the tests pin "closest enemy wins"
// as the floor.
//
// IMPLEMENT THIS — TODO(HW7).
int pick_target(const SelfInfo& self, const std::vector<TrackInfo>& tracks);

// Decide whether the BT should switch from engage to retreat. True
// triggers `retreat_to_cover` on the next tick. The recommended
// rule combines low HP, low ammo, and recent damage; tests pin the
// HP threshold (≤ 30) and the ammo threshold (≤ 20) as the floor.
//
// IMPLEMENT THIS — TODO(HW7).
bool should_retreat(const SelfInfo& self, const std::vector<TrackInfo>& tracks);

// Helpers exposed for the TODO bodies (filled).
double squared_distance(double ax, double ay, double bx, double by);

}  // namespace strategy
}  // namespace aiming_hw
