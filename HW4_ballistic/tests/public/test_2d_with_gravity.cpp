// Gravity-only (no drag): the bullet arcs. Aim direction must lift
// above the target to compensate for the drop over the flight time.

#include <gtest/gtest.h>

#include <cmath>

#include "aiming_hw/ballistic/projectile_model.hpp"
#include "aiming_hw/ballistic/solver.hpp"

namespace {

bool plan_is_stub() {
    using namespace aiming_hw::ballistic;
    auto params = ProjectileParams::no_drag();
    auto plan = plan_shot(params, Eigen::Vector3d::Zero(), 25.0,
                          Eigen::Vector3d(5.0, 0.0, 0.0),
                          Eigen::Vector3d::Zero());
    return !plan.converged;
}

}  // namespace

TEST(HW42DWithGravity, AimLiftsAboveTargetForDrop) {
    if (plan_is_stub()) GTEST_SKIP() << "plan_shot unimplemented";
    using namespace aiming_hw::ballistic;
    auto params = ProjectileParams::no_drag();
    Eigen::Vector3d muzzle = Eigen::Vector3d::Zero();
    Eigen::Vector3d target(10.0, 0.0, 0.0);
    auto plan = plan_shot(params, muzzle, 25.0, target, Eigen::Vector3d::Zero());

    ASSERT_TRUE(plan.converged);
    // The aim direction must have a positive z component to lift the
    // bullet above the horizontal: gravity drops the bullet during
    // flight.
    EXPECT_GT(plan.aim_direction.z(), 0.0)
        << "aim direction = " << plan.aim_direction.transpose();
    EXPECT_NEAR(plan.aim_direction.norm(), 1.0, 1e-6);

    // Verify the trajectory actually hits the target.
    Eigen::Vector3d hit = projectile_position_at(
        params, muzzle, plan.aim_direction * 25.0, plan.flight_time_s);
    EXPECT_NEAR((hit - target).norm(), 0.0, 5e-3);
}

TEST(HW42DWithGravity, FartherTargetNeedsMoreLift) {
    if (plan_is_stub()) GTEST_SKIP() << "plan_shot unimplemented";
    using namespace aiming_hw::ballistic;
    auto params = ProjectileParams::no_drag();
    auto near_plan = plan_shot(params, Eigen::Vector3d::Zero(), 25.0,
                               Eigen::Vector3d(5.0, 0.0, 0.0),
                               Eigen::Vector3d::Zero());
    auto far_plan  = plan_shot(params, Eigen::Vector3d::Zero(), 25.0,
                               Eigen::Vector3d(15.0, 0.0, 0.0),
                               Eigen::Vector3d::Zero());
    ASSERT_TRUE(near_plan.converged);
    ASSERT_TRUE(far_plan.converged);
    EXPECT_GT(far_plan.aim_direction.z(), near_plan.aim_direction.z());
}

TEST(HW42DWithGravity, FlightTimeMatchesAnalyticForFlatRange) {
    if (plan_is_stub()) GTEST_SKIP() << "plan_shot unimplemented";
    using namespace aiming_hw::ballistic;
    auto params = ProjectileParams::no_drag();
    // For a same-height target, flight time at low elevation is
    // approximately range / horizontal_speed. With elevation θ:
    //   t ≈ R / (v cos θ);  H = R tan(θ)/2 must equal R tan(θ) - 0.5 g t^2/v
    // For a small elevation the linear approximation `t ≈ R / v` is
    // accurate to a few percent; we only assert that.
    auto plan = plan_shot(params, Eigen::Vector3d::Zero(), 25.0,
                          Eigen::Vector3d(5.0, 0.0, 0.0),
                          Eigen::Vector3d::Zero());
    ASSERT_TRUE(plan.converged);
    // Range 5 m at v=25 m/s → ~0.2 s, slightly more for the lift.
    EXPECT_NEAR(plan.flight_time_s, 0.20, 0.02);
}
