// Round-trip every wire message through SerializeToString / ParseFromString.
// Catches the obvious "I added a field with the wrong tag number" class of
// regressions before they reach a candidate's machine.
//
// Stage 1 acceptance criterion: this test must pass for the build to be
// considered a green stage close.

#include <gtest/gtest.h>

#include "aiming.pb.h"
#include "sensor.pb.h"
#include "episode.pb.h"

namespace tsingyun_v1 = tsingyun::aiming::v1;

namespace {

template <typename T>
T RoundTrip(const T& in) {
    std::string blob;
    EXPECT_TRUE(in.SerializeToString(&blob));
    T out;
    EXPECT_TRUE(out.ParseFromString(blob));
    return out;
}

}  // namespace

TEST(ProtoRoundtrip, FrameRefPreservesEverything) {
    tsingyun_v1::FrameRef f;
    f.set_frame_id(0xDEADBEEFCAFEBABE);
    f.set_zmq_topic("frames.42");
    f.set_stamp_ns(1'234'567'890ULL);
    f.set_width(1280);
    f.set_height(720);
    f.set_pixel_format(tsingyun_v1::FrameRef::PIXEL_FORMAT_RGB888);

    auto out = RoundTrip(f);
    EXPECT_EQ(out.frame_id(), f.frame_id());
    EXPECT_EQ(out.zmq_topic(), f.zmq_topic());
    EXPECT_EQ(out.stamp_ns(), f.stamp_ns());
    EXPECT_EQ(out.width(), f.width());
    EXPECT_EQ(out.height(), f.height());
    EXPECT_EQ(out.pixel_format(), f.pixel_format());
}

TEST(ProtoRoundtrip, SensorBundleWithOracle) {
    tsingyun_v1::SensorBundle b;
    b.mutable_frame()->set_frame_id(7);
    b.mutable_imu()->mutable_orientation()->set_w(1.0);
    b.mutable_gimbal()->set_yaw(0.5);
    b.mutable_gimbal()->set_pitch(-0.25);
    b.mutable_odom()->mutable_position_world()->set_x(3.14);
    b.mutable_oracle()->mutable_target_position_world()->set_z(5.0);
    b.mutable_oracle()->set_target_visible(true);

    auto out = RoundTrip(b);
    EXPECT_EQ(out.frame().frame_id(), 7);
    EXPECT_DOUBLE_EQ(out.imu().orientation().w(), 1.0);
    EXPECT_DOUBLE_EQ(out.gimbal().yaw(), 0.5);
    EXPECT_DOUBLE_EQ(out.gimbal().pitch(), -0.25);
    EXPECT_DOUBLE_EQ(out.odom().position_world().x(), 3.14);
    EXPECT_DOUBLE_EQ(out.oracle().target_position_world().z(), 5.0);
    EXPECT_TRUE(out.oracle().target_visible());
}

TEST(ProtoRoundtrip, EnvResetRequestSeedDoesNotCollapseToInt32) {
    tsingyun_v1::EnvResetRequest r;
    // High bit of a 64-bit seed; if anyone ever drops the type to int32,
    // this round-trip will misbehave.
    r.set_seed(0x8000000000000001ULL);
    r.set_opponent_tier("silver");
    r.set_oracle_hints(true);

    auto out = RoundTrip(r);
    EXPECT_EQ(out.seed(), 0x8000000000000001ULL);
    EXPECT_EQ(out.opponent_tier(), "silver");
    EXPECT_TRUE(out.oracle_hints());
}

TEST(ProtoRoundtrip, EpisodeStatsEvents) {
    tsingyun_v1::EpisodeStats s;
    s.set_episode_id("e-0001");
    s.set_seed(42);
    s.set_outcome(tsingyun_v1::EpisodeStats::OUTCOME_WIN);
    s.set_damage_dealt(800);
    s.set_aim_latency_p95_ns(20'000'000ULL);

    auto* e = s.add_events();
    e->set_kind(tsingyun_v1::ProjectileEvent::KIND_HIT_ARMOR);
    e->set_armor_id("blue.front");
    e->set_damage(50);

    auto out = RoundTrip(s);
    EXPECT_EQ(out.episode_id(), "e-0001");
    EXPECT_EQ(out.seed(), 42u);
    EXPECT_EQ(out.outcome(), tsingyun_v1::EpisodeStats::OUTCOME_WIN);
    EXPECT_EQ(out.damage_dealt(), 800u);
    EXPECT_EQ(out.aim_latency_p95_ns(), 20'000'000ULL);
    ASSERT_EQ(out.events_size(), 1);
    EXPECT_EQ(out.events(0).armor_id(), "blue.front");
    EXPECT_EQ(out.events(0).damage(), 50u);
}
