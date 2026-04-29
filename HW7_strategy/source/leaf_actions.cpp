#include "aiming_hw/strategy/leaf_actions.hpp"

#include <vector>

#include "aiming_hw/strategy/strategy.hpp"

namespace aiming_hw {
namespace strategy {

namespace {

// HW6's runner stashes a pointer to its current track list in the
// blackboard via Blackboard::set<int>("tracks_ptr", ptr_as_int). We
// undo the cast here. This is a deliberate small evil — it keeps
// the Blackboard's value variant from depending on every project
// type. The runner is the only place the cast happens, so the
// pointer-tag → integer mapping never escapes one translation unit.
const std::vector<TrackInfo>* tracks_from_bb(const Blackboard& bb) {
    if (!bb.has("tracks_ptr")) return nullptr;
    auto raw = bb.get<int>("tracks_ptr", 0);
    if (raw == 0) return nullptr;
    return reinterpret_cast<const std::vector<TrackInfo>*>(
        static_cast<intptr_t>(raw));
}

SelfInfo self_from_bb(const Blackboard& bb) {
    SelfInfo s;
    s.x    = bb.get<double>("self.x");
    s.y    = bb.get<double>("self.y");
    s.hp   = bb.get<double>("self.hp", 100.0);
    s.ammo = bb.get<int>("self.ammo", 200);
    return s;
}

}  // namespace

Status engage(Blackboard& bb) {
    const auto* tracks = tracks_from_bb(bb);
    if (!tracks || tracks->empty()) {
        return Status::Failure;
    }
    const SelfInfo self = self_from_bb(bb);
    const int idx = pick_target(self, *tracks);
    if (idx < 0 || idx >= static_cast<int>(tracks->size())) {
        return Status::Failure;
    }
    const auto& target = (*tracks)[idx];
    bb.set("target.x", target.x);
    bb.set("target.y", target.y);
    bb.set("target.id", target.id);
    return Status::Running;
}

Status retreat_to_cover(Blackboard& bb) {
    // Cover spots in this PoC are hardcoded — Stage 2's arena has a
    // single floor with no obstacles, so "retreat" means moving away
    // from every visible enemy along the unit normal. HW6's runner
    // computes that direction from `target.x/y` (set by `engage`)
    // and the blackboard's self pose; the leaf only flips the flag.
    bb.set("want_retreat", true);
    return Status::Running;
}

Status patrol(Blackboard& bb) {
    bb.set("want_retreat", false);
    bb.set("patrol_active", true);
    return Status::Running;
}

Status reload(Blackboard& bb) {
    const int ammo = bb.get<int>("self.ammo", 200);
    bb.set("want_fire", false);
    return ammo < 20 ? Status::Running : Status::Success;
}

}  // namespace strategy
}  // namespace aiming_hw
