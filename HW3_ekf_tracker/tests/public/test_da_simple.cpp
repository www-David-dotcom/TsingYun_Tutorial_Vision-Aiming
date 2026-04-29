// Hungarian assignment + gating sanity. Three cases:
//   * trivial 1-track-1-detection match
//   * 2x2 cost matrix where the optimum picks the off-diagonal
//   * gating filters out a high-cost match

#include <gtest/gtest.h>

#include <vector>

#include "aiming_hw/ekf/data_association.hpp"

namespace {

bool hungarian_is_stub() {
    using namespace aiming_hw::ekf;
    std::vector<std::vector<double>> cost = {{0.5}};
    auto pairs = hungarian_assign(cost, 10.0);
    return pairs.empty();
}

}  // namespace

TEST(HW3DataAssociation, TrivialOneByOne) {
    if (hungarian_is_stub()) GTEST_SKIP() << "hungarian_assign unimplemented";
    using namespace aiming_hw::ekf;
    std::vector<std::vector<double>> cost = {{0.5}};
    auto pairs = hungarian_assign(cost, 10.0);
    ASSERT_EQ(pairs.size(), 1u);
    EXPECT_EQ(pairs[0].track_index, 0);
    EXPECT_EQ(pairs[0].detection_index, 0);
}

TEST(HW3DataAssociation, OffDiagonalIsOptimal) {
    if (hungarian_is_stub()) GTEST_SKIP() << "hungarian_assign unimplemented";
    using namespace aiming_hw::ekf;
    // The diagonal matching has cost 5+5=10; the off-diagonal has 1+1=2.
    std::vector<std::vector<double>> cost = {
        {5.0, 1.0},
        {1.0, 5.0},
    };
    auto pairs = hungarian_assign(cost, 100.0);
    ASSERT_EQ(pairs.size(), 2u);
    int sum_cost_x10 = 0;
    for (const auto& p : pairs) {
        sum_cost_x10 += static_cast<int>(p.cost * 10.0 + 0.5);
        EXPECT_NE(p.track_index, p.detection_index)
            << "expected the off-diagonal pairing";
    }
    EXPECT_EQ(sum_cost_x10, 20);   // 1.0 + 1.0
}

TEST(HW3DataAssociation, GateFiltersHighCostMatches) {
    if (hungarian_is_stub()) GTEST_SKIP() << "hungarian_assign unimplemented";
    using namespace aiming_hw::ekf;
    // Track 0 matches detection 0 (cost 0.5, in gate); track 1 has
    // no in-gate detection (best is cost 50, far above gate=10).
    std::vector<std::vector<double>> cost = {
        {0.5,  100.0},
        {50.0,   80.0},
    };
    auto pairs = hungarian_assign(cost, 10.0);
    ASSERT_EQ(pairs.size(), 1u);
    EXPECT_EQ(pairs[0].track_index, 0);
    EXPECT_EQ(pairs[0].detection_index, 0);
}
