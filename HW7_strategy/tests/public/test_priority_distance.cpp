// pick_target priority sanity:
//   * empty track list returns -1
//   * all-ally list returns -1
//   * with two enemies of equal HP at different ranges, the closer
//     one wins (this is the documented FLOOR — the candidate may
//     layer extra logic on top, but this rule must still hold)
//
// Skips when the stub returns -1 even on the trivial single-enemy
// case (i.e. the TODO is unfilled).

#include <gtest/gtest.h>

#include <vector>

#include "aiming_hw/strategy/strategy.hpp"

using aiming_hw::strategy::SelfInfo;
using aiming_hw::strategy::TrackInfo;
using aiming_hw::strategy::pick_target;

namespace {

bool pick_is_stub() {
    SelfInfo self;
    std::vector<TrackInfo> tracks = {
        TrackInfo{1, 5.0, 0.0, 0.0, 0.0, 100.0, false},
    };
    return pick_target(self, tracks) < 0;
}

}  // namespace

TEST(HW7PickTarget, EmptyListReturnsMinusOne) {
    EXPECT_EQ(pick_target(SelfInfo{}, {}), -1);
}

TEST(HW7PickTarget, AllAlliesReturnsMinusOne) {
    if (pick_is_stub()) GTEST_SKIP() << "pick_target unimplemented";
    SelfInfo self;
    std::vector<TrackInfo> tracks = {
        TrackInfo{1, 1.0, 0.0, 0.0, 0.0, 100.0, true},
        TrackInfo{2, 2.0, 0.0, 0.0, 0.0, 100.0, true},
    };
    EXPECT_EQ(pick_target(self, tracks), -1);
}

TEST(HW7PickTarget, ClosestEnemyWinsAtEqualHp) {
    if (pick_is_stub()) GTEST_SKIP() << "pick_target unimplemented";
    SelfInfo self;
    std::vector<TrackInfo> tracks = {
        TrackInfo{10, 10.0, 0.0, 0.0, 0.0, 100.0, false},  // far
        TrackInfo{20,  3.0, 0.0, 0.0, 0.0, 100.0, false},  // near — should win
        TrackInfo{30,  6.0, 0.0, 0.0, 0.0, 100.0, false},
    };
    const int idx = pick_target(self, tracks);
    ASSERT_GE(idx, 0);
    EXPECT_EQ(tracks[idx].id, 20);
}

TEST(HW7PickTarget, AllyAtCloseRangeStillSkipped) {
    if (pick_is_stub()) GTEST_SKIP() << "pick_target unimplemented";
    SelfInfo self;
    std::vector<TrackInfo> tracks = {
        TrackInfo{ 1, 1.0, 0.0, 0.0, 0.0, 100.0, true},   // ally
        TrackInfo{20, 8.0, 0.0, 0.0, 0.0, 100.0, false},  // far enemy, should win
    };
    const int idx = pick_target(self, tracks);
    ASSERT_GE(idx, 0);
    EXPECT_FALSE(tracks[idx].is_ally);
    EXPECT_EQ(tracks[idx].id, 20);
}
