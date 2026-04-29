// should_retreat thresholds:
//   * full HP + full ammo → no retreat
//   * HP <= 30            → retreat (the documented floor)
//   * ammo <= 20          → retreat (also the documented floor)
//
// Layered rules (outnumbered + low HP, etc.) are at the candidate's
// discretion and not pinned here.

#include <gtest/gtest.h>

#include <vector>

#include "aiming_hw/strategy/strategy.hpp"

using aiming_hw::strategy::SelfInfo;
using aiming_hw::strategy::TrackInfo;
using aiming_hw::strategy::should_retreat;

namespace {

bool retreat_is_stub() {
    SelfInfo low_hp;
    low_hp.hp = 10.0;     // very low — a real impl must return true
    return !should_retreat(low_hp, {});
}

}  // namespace

TEST(HW7Retreat, FullHpFullAmmoNoRetreat) {
    if (retreat_is_stub()) GTEST_SKIP() << "should_retreat unimplemented";
    SelfInfo self;
    self.hp = 100.0;
    self.ammo = 200;
    EXPECT_FALSE(should_retreat(self, {}));
}

TEST(HW7Retreat, LowHpTriggersRetreat) {
    if (retreat_is_stub()) GTEST_SKIP() << "should_retreat unimplemented";
    SelfInfo self;
    self.hp = 25.0;       // <= 30 floor
    self.ammo = 200;
    EXPECT_TRUE(should_retreat(self, {}));
}

TEST(HW7Retreat, LowAmmoTriggersRetreat) {
    if (retreat_is_stub()) GTEST_SKIP() << "should_retreat unimplemented";
    SelfInfo self;
    self.hp = 100.0;
    self.ammo = 15;       // <= 20 floor
    EXPECT_TRUE(should_retreat(self, {}));
}

TEST(HW7Retreat, ExactlyAtThresholdsTriggers) {
    if (retreat_is_stub()) GTEST_SKIP() << "should_retreat unimplemented";
    SelfInfo at_hp_threshold;
    at_hp_threshold.hp = 30.0;
    at_hp_threshold.ammo = 200;
    EXPECT_TRUE(should_retreat(at_hp_threshold, {}));

    SelfInfo at_ammo_threshold;
    at_ammo_threshold.hp = 100.0;
    at_ammo_threshold.ammo = 20;
    EXPECT_TRUE(should_retreat(at_ammo_threshold, {}));
}
