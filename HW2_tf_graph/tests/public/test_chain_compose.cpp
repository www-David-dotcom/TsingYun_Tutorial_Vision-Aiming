// Three-frame chain: world → chassis → gimbal. Tests that
// `Buffer::lookup_chain` (which calls `tf::compose`) produces the
// same world-frame point as composing manually via `Transform::operator*`.

#include <gtest/gtest.h>

#include <cmath>

#include "aiming_hw/tf/buffer.hpp"
#include "aiming_hw/tf/interpolate.hpp"

namespace {

bool compose_is_stub() {
    using namespace aiming_hw::tf;
    Transform a{Eigen::Vector3d(1.0, 0.0, 0.0), Eigen::Quaterniond::Identity()};
    Transform b{Eigen::Vector3d(0.0, 2.0, 0.0), Eigen::Quaterniond::Identity()};
    auto c = compose(a, b);
    // True compose: t = (1, 2, 0). Stub: identity = (0, 0, 0).
    return c.translation.norm() < 0.5;
}

}  // namespace

TEST(HW2ChainCompose, IdentityIsTheNeutralElement) {
    if (compose_is_stub()) GTEST_SKIP() << "compose unimplemented";
    using namespace aiming_hw::tf;
    Transform t{Eigen::Vector3d(1.0, 2.0, 3.0),
                Eigen::Quaterniond(Eigen::AngleAxisd(0.5, Eigen::Vector3d::UnitZ()))};
    auto left  = compose(Transform::identity(), t);
    auto right = compose(t, Transform::identity());

    EXPECT_NEAR((left.translation  - t.translation).norm(), 0.0, 1e-9);
    EXPECT_NEAR((right.translation - t.translation).norm(), 0.0, 1e-9);
    EXPECT_NEAR(std::abs(left.rotation.dot(t.rotation)),  1.0, 1e-9);
    EXPECT_NEAR(std::abs(right.rotation.dot(t.rotation)), 1.0, 1e-9);
}

TEST(HW2ChainCompose, TwoLinkChainAgreesWithOperatorStarOnPoints) {
    if (compose_is_stub()) GTEST_SKIP() << "compose unimplemented";
    using namespace aiming_hw::tf;

    Transform world_to_chassis{
        Eigen::Vector3d(2.0, 0.0, 0.0),
        Eigen::Quaterniond(Eigen::AngleAxisd(M_PI / 2, Eigen::Vector3d::UnitZ())),
    };
    Transform chassis_to_gimbal{
        Eigen::Vector3d(0.5, 0.0, 0.3),
        Eigen::Quaterniond::Identity(),
    };

    Transform world_to_gimbal = compose(world_to_chassis, chassis_to_gimbal);

    // Pick a point in the gimbal frame, push it through the chain,
    // and compare to the manual two-step.
    Eigen::Vector3d p_gimbal(0.1, 0.2, 0.4);
    Eigen::Vector3d p_via_chain  = world_to_gimbal * p_gimbal;
    Eigen::Vector3d p_manual     = world_to_chassis * (chassis_to_gimbal * p_gimbal);

    EXPECT_NEAR((p_via_chain - p_manual).norm(), 0.0, 1e-9);
}

TEST(HW2ChainCompose, BufferLookupChainAgreesWithManualPointApply) {
    if (compose_is_stub()) GTEST_SKIP() << "compose unimplemented";
    using namespace aiming_hw::tf;
    Buffer buf;

    Transform world_to_chassis{
        Eigen::Vector3d(2.0, 0.0, 0.0),
        Eigen::Quaterniond(Eigen::AngleAxisd(0.3, Eigen::Vector3d::UnitZ())),
    };
    Transform chassis_to_gimbal{
        Eigen::Vector3d(0.0, 0.0, 0.4),
        Eigen::Quaterniond(Eigen::AngleAxisd(-0.2, Eigen::Vector3d::UnitY())),
    };
    buf.set_transform("world", "chassis", 1'000'000'000ULL, world_to_chassis);
    buf.set_transform("chassis", "gimbal", 1'000'000'000ULL, chassis_to_gimbal);

    Transform chained = buf.lookup_chain({"world", "chassis", "gimbal"},
                                         1'000'000'000ULL);

    // Ground truth that avoids tf::compose entirely: push a point
    // through Transform::operator* (filled, in transform.hpp) twice.
    Eigen::Vector3d p_gimbal(0.05, -0.10, 0.20);
    Eigen::Vector3d expected = world_to_chassis * (chassis_to_gimbal * p_gimbal);
    Eigen::Vector3d via_chain = chained * p_gimbal;

    EXPECT_NEAR((via_chain - expected).norm(), 0.0, 1e-9);
}
