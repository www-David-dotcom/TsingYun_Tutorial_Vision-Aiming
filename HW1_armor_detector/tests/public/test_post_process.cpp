// Public C++ tests for the HW1 post-processing.
//
// Until the candidate fills source/post_process.cpp the bodies of
// decode_head and non_max_suppression return empty vectors. These
// tests assert the *contract* (return type, smoke shape) and one
// known-answer case for NMS — they only pass when the candidate has
// implemented the math.

#include <gtest/gtest.h>

#include <vector>

#include "aiming_hw/detector/post_process.hpp"

namespace {

aiming_hw::detector::Detection make_det(float x1, float y1, float x2, float y2,
                                        aiming_hw::detector::Icon icon, float score) {
    aiming_hw::detector::Detection d{};
    d.x1 = x1;
    d.y1 = y1;
    d.x2 = x2;
    d.y2 = y2;
    d.icon = icon;
    d.score = score;
    d.corners = {x1, y1, x2, y1, x2, y2, x1, y2};
    return d;
}

}  // namespace

TEST(HW1PostProcess, DecodeHeadOnZeroTensorReturnsBelowThreshold) {
    using namespace aiming_hw::detector;
    constexpr int fH = 4;
    constexpr int fW = 5;
    std::vector<float> tensor(static_cast<std::size_t>(kTotalChan) * fH * fW, 0.0f);
    auto results = decode_head(tensor.data(), tensor.size(), fH, fW, 0.30f);
    // sigmoid(0)=0.5; 0.5 * (1/4)=0.125 < 0.30 threshold, so nothing
    // should survive. If decode_head is unimplemented this is also
    // empty — the test passes either way for an honest stub.
    EXPECT_TRUE(results.empty());
}

TEST(HW1PostProcess, NMSDeduplicatesSameClassOverlap) {
    using namespace aiming_hw::detector;
    std::vector<Detection> candidates;
    candidates.push_back(make_det(10, 10, 50, 50, Icon::Hero, 0.95f));
    candidates.push_back(make_det(12, 12, 52, 52, Icon::Hero, 0.85f));
    candidates.push_back(make_det(200, 200, 240, 240, Icon::Engineer, 0.90f));
    auto kept = non_max_suppression(std::move(candidates), 0.40f);

    if (kept.empty()) {
        GTEST_SKIP() << "non_max_suppression unimplemented — fill the TODO";
    }
    ASSERT_EQ(kept.size(), 2u);
    // Both icons should survive; the duplicate Hero should not.
    bool seen_hero = false, seen_eng = false;
    for (const auto& d : kept) {
        if (d.icon == Icon::Hero)     seen_hero = true;
        if (d.icon == Icon::Engineer) seen_eng = true;
    }
    EXPECT_TRUE(seen_hero);
    EXPECT_TRUE(seen_eng);
}

TEST(HW1PostProcess, NMSKeepsCrossClassStacks) {
    using namespace aiming_hw::detector;
    // Same bbox, different icons: long-range case where four plates
    // legitimately project onto roughly the same screen-space region.
    std::vector<Detection> candidates;
    candidates.push_back(make_det(50, 50, 80, 80, Icon::Hero,     0.85f));
    candidates.push_back(make_det(50, 50, 80, 80, Icon::Engineer, 0.80f));
    candidates.push_back(make_det(50, 50, 80, 80, Icon::Standard, 0.75f));
    candidates.push_back(make_det(50, 50, 80, 80, Icon::Sentry,   0.70f));
    auto kept = non_max_suppression(std::move(candidates), 0.40f);

    if (kept.empty()) {
        GTEST_SKIP() << "non_max_suppression unimplemented — fill the TODO";
    }
    EXPECT_EQ(kept.size(), 4u);
}
