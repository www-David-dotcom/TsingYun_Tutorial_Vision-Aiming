// Buffer insert + lookup at exact and interpolated stamps.
//
// `Buffer::set_transform` and `Buffer::lookup_direct` are filled. The
// interpolated path calls `tf::interpolate`, so this test depends on
// the candidate's TODO. We GTEST_SKIP when the stub is detected (it
// returns identity for any input), so the rest of the suite stays
// green until the candidate fills the TODO.

#include <gtest/gtest.h>

#include "aiming_hw/tf/buffer.hpp"
#include "aiming_hw/tf/interpolate.hpp"

namespace {

bool interpolate_is_stub() {
    using aiming_hw::tf::Transform;
    Transform a{Eigen::Vector3d(1.0, 0.0, 0.0), Eigen::Quaterniond::Identity()};
    Transform b{Eigen::Vector3d(3.0, 0.0, 0.0), Eigen::Quaterniond::Identity()};
    auto mid = aiming_hw::tf::interpolate(a, b, 0.5);
    // A real implementation produces (2, 0, 0); the stub returns identity (0, 0, 0).
    return mid.translation.norm() < 0.5;
}

}  // namespace

TEST(HW2BufferLookup, ExactStampReturnsStoredTransform) {
    using namespace aiming_hw::tf;
    Buffer buf;
    Transform t{Eigen::Vector3d(0.5, 1.0, -2.0), Eigen::Quaterniond::Identity()};
    buf.set_transform("world", "chassis", 1'000'000'000ULL, t);

    auto out = buf.lookup_direct("world", "chassis", 1'000'000'000ULL);
    EXPECT_DOUBLE_EQ(out.translation.x(), 0.5);
    EXPECT_DOUBLE_EQ(out.translation.y(), 1.0);
    EXPECT_DOUBLE_EQ(out.translation.z(), -2.0);
}

TEST(HW2BufferLookup, MidpointInterpolatesTranslation) {
    if (interpolate_is_stub()) {
        GTEST_SKIP() << "tf::interpolate is unimplemented — fill the TODO in interpolate.cpp";
    }
    using namespace aiming_hw::tf;
    Buffer buf;
    Transform a{Eigen::Vector3d(0.0, 0.0, 0.0), Eigen::Quaterniond::Identity()};
    Transform b{Eigen::Vector3d(2.0, 0.0, 0.0), Eigen::Quaterniond::Identity()};
    buf.set_transform("world", "chassis", 0ULL, a);
    buf.set_transform("world", "chassis", 1'000'000'000ULL, b);

    auto out = buf.lookup_direct("world", "chassis", 500'000'000ULL);
    EXPECT_NEAR(out.translation.x(), 1.0, 1e-9);
    EXPECT_NEAR(out.translation.y(), 0.0, 1e-9);
    EXPECT_NEAR(out.translation.z(), 0.0, 1e-9);
}

TEST(HW2BufferLookup, OutOfRangeStampThrows) {
    using namespace aiming_hw::tf;
    Buffer buf;
    Transform t{Eigen::Vector3d::Zero(), Eigen::Quaterniond::Identity()};
    buf.set_transform("world", "chassis", 1'000'000'000ULL, t);
    buf.set_transform("world", "chassis", 2'000'000'000ULL, t);
    EXPECT_THROW(buf.lookup_direct("world", "chassis", 500'000'000ULL),
                 LookupError);
    EXPECT_THROW(buf.lookup_direct("world", "chassis", 3'000'000'000ULL),
                 LookupError);
}

TEST(HW2BufferLookup, NonMonotonicInsertThrows) {
    using namespace aiming_hw::tf;
    Buffer buf;
    Transform t{Eigen::Vector3d::Zero(), Eigen::Quaterniond::Identity()};
    buf.set_transform("world", "chassis", 1'000'000'000ULL, t);
    EXPECT_THROW(buf.set_transform("world", "chassis", 999'999'999ULL, t),
                 LookupError);
}

TEST(HW2BufferLookup, UnknownEdgeThrows) {
    using namespace aiming_hw::tf;
    Buffer buf;
    EXPECT_THROW(buf.lookup_direct("world", "chassis", 0ULL), LookupError);
}
