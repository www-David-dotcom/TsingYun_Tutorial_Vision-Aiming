#pragma once

// Leaf-action library for HW7's behaviour tree.
//
// Each leaf action is a free function that returns
// `aiming_hw::strategy::Status`. The blackboard convention:
//   * "self.hp"      double, 0..100
//   * "self.ammo"    int
//   * "self.x/y"     double, world frame meters
//   * "ally.x/y"     double  (optional)
//   * "tracks"       opaque — runner stashes the pointer in a
//                    `void*`-equivalent slot via Blackboard.set<int>()
//                    casting; HW6's runner is the only consumer that
//                    bridges the actual pointer.
//   * "target.x/y"   double — set by `engage` after pick_target.
//   * "want_retreat" bool   — written by retreat_logic.

#include "aiming_hw/strategy/behavior_tree.hpp"

namespace aiming_hw {
namespace strategy {

// Pick the highest-priority track and write its position into the
// blackboard's "target.x" / "target.y" slots. Calls `pick_target`
// (TODO in strategy.cpp) under the hood.
//
// Returns Failure if no tracks visible; Running while engaging.
Status engage(Blackboard& bb);

// Pull back to the nearest cover spot. Triggered by `retreat_logic`
// (TODO in strategy.cpp) — the BT typically wraps engage and
// retreat in a Selector with retreat checked first.
Status retreat_to_cover(Blackboard& bb);

// Walk a fixed patrol pattern when no tracks are visible.
Status patrol(Blackboard& bb);

// Stop firing and wait for the ammo cooldown to pass. The runner
// signals ammo via the "self.ammo" key.
Status reload(Blackboard& bb);

}  // namespace strategy
}  // namespace aiming_hw
