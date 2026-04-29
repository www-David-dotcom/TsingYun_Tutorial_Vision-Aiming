// SLERP corner cases: short-arc fix-up around antipodal quaternions.
//
// Short-arc selection is the failure mode the README calls out: two
// near-antipodal unit quaternions, naively SLERPed, swing the long
// way around. The candidate's `tf::interpolate` must flip one of the
// quaternion signs before SLERPing.

#include <gtest/gtest.h>

#include <cmath>

#include "aiming_hw/tf/interpolate.hpp"

namespace {

bool interpolate_is_stub() {
    using aiming_hw::tf::Transform;
    Transform a{Eigen::Vector3d(1.0, 0.0, 0.0), Eigen::Quaterniond::Identity()};
    Transform b{Eigen::Vector3d(3.0, 0.0, 0.0), Eigen::Quaterniond::Identity()};
    auto mid = aiming_hw::tf::interpolate(a, b, 0.5);
    return mid.translation.norm() < 0.5;
}

}  // namespace

TEST(HW2InterpolationCorners, AlphaZeroReturnsA) {
    if (interpolate_is_stub()) GTEST_SKIP() << "interpolate unimplemented";
    using namespace aiming_hw::tf;
    Transform a{Eigen::Vector3d(1.0, 2.0, 3.0),
                Eigen::Quaterniond(Eigen::AngleAxisd(0.4, Eigen::Vector3d::UnitZ()))};
    Transform b{Eigen::Vector3d(7.0, 8.0, 9.0),
                Eigen::Quaterniond(Eigen::AngleAxisd(1.7, Eigen::Vector3d::UnitZ()))};

    auto out = interpolate(a, b, 0.0);
    EXPECT_NEAR((out.translation - a.translation).norm(), 0.0, 1e-9);
    EXPECT_NEAR(std::abs(out.rotation.dot(a.rotation)), 1.0, 1e-9);
}

TEST(HW2InterpolationCorners, AlphaOneReturnsB) {
    if (interpolate_is_stub()) GTEST_SKIP() << "interpolate unimplemented";
    using namespace aiming_hw::tf;
    Transform a{Eigen::Vector3d(1.0, 2.0, 3.0), Eigen::Quaterniond::Identity()};
    Transform b{Eigen::Vector3d(7.0, 8.0, 9.0),
                Eigen::Quaterniond(Eigen::AngleAxisd(1.7, Eigen::Vector3d::UnitZ()))};

    auto out = interpolate(a, b, 1.0);
    EXPECT_NEAR((out.translation - b.translation).norm(), 0.0, 1e-9);
    EXPECT_NEAR(std::abs(out.rotation.dot(b.rotation)), 1.0, 1e-9);
}

TEST(HW2InterpolationCorners, AntipodalQuaternionsTakeShortArc) {
    if (interpolate_is_stub()) GTEST_SKIP() << "interpolate unimplemented";
    using namespace aiming_hw::tf;
    // Two quaternions representing the same orientation but with
    // opposite sign — Eigen treats them as antipodal even though
    // `q` and `-q` rotate vectors identically.
    Eigen::Quaterniond q(Eigen::AngleAxisd(0.1, Eigen::Vector3d::UnitY()));
    Eigen::Quaterniond q_neg(-q.w(), -q.x(), -q.y(), -q.z());

    Transform a{Eigen::Vector3d::Zero(), q};
    Transform b{Eigen::Vector3d::Zero(), q_neg};
    auto out = interpolate(a, b, 0.5);

    // Short-arc SLERP between two same-orientation antipodes should
    // not produce a wildly different orientation. The midpoint must
    // still rotate (1, 0, 0) close to where the endpoints do.
    Eigen::Vector3d v = Eigen::Vector3d::UnitX();
    Eigen::Vector3d v_a = q * v;
    Eigen::Vector3d v_mid = out.rotation * v;
    EXPECT_LT((v_mid - v_a).norm(), 0.05)
        << "naive SLERP swung the long way around the sphere";
}

TEST(HW2InterpolationCorners, OutOfRangeAlphaIsClamped) {
    if (interpolate_is_stub()) GTEST_SKIP() << "interpolate unimplemented";
    using namespace aiming_hw::tf;
    Transform a{Eigen::Vector3d(1.0, 0.0, 0.0), Eigen::Quaterniond::Identity()};
    Transform b{Eigen::Vector3d(3.0, 0.0, 0.0), Eigen::Quaterniond::Identity()};
    auto under = interpolate(a, b, -0.5);
    auto over  = interpolate(a, b,  1.5);
    EXPECT_NEAR((under.translation - a.translation).norm(), 0.0, 1e-9);
    EXPECT_NEAR((over.translation  - b.translation).norm(), 0.0, 1e-9);
}
