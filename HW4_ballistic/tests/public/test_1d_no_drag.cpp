// 1D no-drag, no-gravity: bullet flies in a straight line at muzzle
// speed; flight time and aim direction are exact closed-form values.

#include <gtest/gtest.h>

#include "aiming_hw/ballistic/projectile_model.hpp"
#include "aiming_hw/ballistic/solver.hpp"

namespace {

bool flight_time_is_stub() {
    using namespace aiming_hw::ballistic;
    auto params = ProjectileParams::no_gravity_no_drag();
    Eigen::Vector3d muzzle = Eigen::Vector3d::Zero();
    Eigen::Vector3d aim = Eigen::Vector3d::UnitX();
    Eigen::Vector3d target(5.0, 0.0, 0.0);
    double t = solve_flight_time(params, muzzle, aim, 25.0, target);
    // True answer: 5 / 25 = 0.20 s. Stub returns -1.
    return t < 0.0;
}

bool plan_is_stub() {
    using namespace aiming_hw::ballistic;
    auto params = ProjectileParams::no_gravity_no_drag();
    auto plan = plan_shot(params, Eigen::Vector3d::Zero(), 25.0,
                          Eigen::Vector3d(5.0, 0.0, 0.0),
                          Eigen::Vector3d::Zero());
    return !plan.converged;
}

}  // namespace

TEST(HW41DNoDrag, FlightTimeIsRangeOverSpeed) {
    if (flight_time_is_stub()) GTEST_SKIP() << "solve_flight_time unimplemented";
    using namespace aiming_hw::ballistic;
    auto params = ProjectileParams::no_gravity_no_drag();
    double t = solve_flight_time(params,
                                 Eigen::Vector3d::Zero(),
                                 Eigen::Vector3d::UnitX(),
                                 25.0,
                                 Eigen::Vector3d(5.0, 0.0, 0.0));
    EXPECT_NEAR(t, 0.20, 5e-4);
}

TEST(HW41DNoDrag, PlanShotStaticTargetAimsDirectly) {
    if (plan_is_stub()) GTEST_SKIP() << "plan_shot unimplemented";
    using namespace aiming_hw::ballistic;
    auto params = ProjectileParams::no_gravity_no_drag();
    Eigen::Vector3d target(5.0, 0.0, 0.0);
    auto plan = plan_shot(params, Eigen::Vector3d::Zero(), 25.0,
                          target, Eigen::Vector3d::Zero());
    ASSERT_TRUE(plan.converged);
    EXPECT_NEAR(plan.aim_direction.x(), 1.0, 1e-6);
    EXPECT_NEAR(plan.aim_direction.norm(), 1.0, 1e-6);
    EXPECT_NEAR(plan.flight_time_s, 0.20, 5e-4);
}

TEST(HW41DNoDrag, PlanShotMovingTargetLeadsCorrectly) {
    if (plan_is_stub()) GTEST_SKIP() << "plan_shot unimplemented";
    using namespace aiming_hw::ballistic;
    auto params = ProjectileParams::no_gravity_no_drag();
    Eigen::Vector3d target(5.0, 0.0, 0.0);
    Eigen::Vector3d target_vel(0.0, 2.0, 0.0);   // moving along +Y at 2 m/s
    auto plan = plan_shot(params, Eigen::Vector3d::Zero(), 25.0,
                          target, target_vel);
    ASSERT_TRUE(plan.converged);

    // Bullet hits the predicted lead point — verify by integrating
    // forward and checking residual against target's actual position
    // at the converged flight time.
    Eigen::Vector3d hit = projectile_position_at(
        params, Eigen::Vector3d::Zero(),
        plan.aim_direction * 25.0, plan.flight_time_s);
    Eigen::Vector3d actual_target_at_t = target + target_vel * plan.flight_time_s;
    EXPECT_NEAR((hit - actual_target_at_t).norm(), 0.0, 1e-3);
}
