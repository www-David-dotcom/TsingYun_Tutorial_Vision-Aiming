#include "aiming_hw/strategy/strategy.hpp"

namespace aiming_hw {
namespace strategy {

double squared_distance(double ax, double ay, double bx, double by) {
    const double dx = ax - bx;
    const double dy = ay - by;
    return dx * dx + dy * dy;
}

int pick_target(const SelfInfo& self, const std::vector<TrackInfo>& tracks) {
    // TODO(HW7): pick the highest-priority enemy track.
    //
    // Recommended scoring (you may tune):
    //   priority = -alpha * distance²
    //              -beta  * estimated_hp
    //              +gamma * (closer than 5 m bonus)
    //   skip is_ally tracks entirely.
    //   return the argmax index, or -1 if no eligible track.
    //
    // The test in tests/public/test_priority_distance.cpp pins
    // "closest enemy wins" as the floor — i.e. with all else equal,
    // the closer enemy must win. You can layer extra logic on top
    // (low-HP first, etc.) as long as that floor still holds.
    //
    // Hint: squared_distance(self.x, self.y, t.x, t.y) — you don't
    // need the actual distance, just relative ordering, so skip the
    // sqrt.
    (void)self;
    (void)tracks;
    return -1;
}

bool should_retreat(const SelfInfo& self, const std::vector<TrackInfo>& tracks) {
    // TODO(HW7): decide whether to switch from engage to retreat.
    //
    // Recommended floors (the public tests pin these exactly):
    //   * self.hp   <= 30  → retreat
    //   * self.ammo <= 20  → retreat (need to disengage to reload)
    //
    // Optional layer-on rules:
    //   * outnumbered (more enemies than allies in `tracks`) and
    //     self.hp < 60 → retreat
    //   * any enemy within 1.5 m and self.hp < 50 → retreat
    //
    // Return true to retreat. The BT typically wraps engage and
    // retreat in a Selector with retreat checked first; returning
    // true here makes that branch fire.
    (void)self;
    (void)tracks;
    return false;
}

}  // namespace strategy
}  // namespace aiming_hw
