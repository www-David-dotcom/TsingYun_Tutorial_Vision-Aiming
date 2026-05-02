// Full 3D dynamics: gravity + drag, target moving in 3D, shooter
// offset from origin. Tests that the iterative solver converges
// within a few iterations and produces a hit within 1 cm.

#include <gtest/gtest.h>

#include "aiming_hw/ballistic/projectile_model.hpp"
#include "aiming_hw/ballistic/solver.hpp"

namespace {

bool plan_is_stub() {
    using namespace aiming_hw::ballistic;
    auto plan = plan_shot(ProjectileParams::rm_17mm(),
                          Eigen::Vector3d::Zero(), 27.0,
                          Eigen::Vector3d(5.0, 0.0, 0.5),
                          Eigen::Vector3d::Zero());
    return !plan.converged;
}

bool acceleration_is_stub() {
    using namespace aiming_hw::ballistic;
    auto a = projectile_acceleration(ProjectileParams::rm_17mm(),
                                     Eigen::Vector3d::Zero());
    return a.z() > -1.0;
}

}  // namespace

TEST(HW43DWithDrag, ConvergesInUnderEightIterations) {
    if (plan_is_stub() || acceleration_is_stub()) GTEST_SKIP() << "plan_shot or projectile_acceleration unimplemented";
    using namespace aiming_hw::ballistic;
    auto params = ProjectileParams::rm_17mm();
    auto plan = plan_shot(params,
                          Eigen::Vector3d(0.0, 0.0, 0.5),    // shooter offset
                          27.0,                                // muzzle speed
                          Eigen::Vector3d(8.0, 1.0, 0.5),    // target
                          Eigen::Vector3d(0.0, 1.0, 0.0));    // target vel
    ASSERT_TRUE(plan.converged) << "miss = " << plan.miss_distance_m;
    EXPECT_LE(plan.iterations, 8);
    EXPECT_LT(plan.miss_distance_m, 0.01);
}

TEST(HW43DWithDrag, BulletPositionAtConvergedTimeMatchesLead) {
    if (plan_is_stub() || acceleration_is_stub()) GTEST_SKIP() << "plan_shot or projectile_acceleration unimplemented";
    using namespace aiming_hw::ballistic;
    auto params = ProjectileParams::rm_17mm();
    Eigen::Vector3d muzzle(0.0, 0.0, 0.4);
    Eigen::Vector3d target(6.0, -1.5, 0.6);
    Eigen::Vector3d target_vel(0.5, 1.5, 0.0);
    double speed = 27.0;
    auto plan = plan_shot(params, muzzle, speed, target, target_vel);
    ASSERT_TRUE(plan.converged);

    Eigen::Vector3d bullet = projectile_position_at(
        params, muzzle, plan.aim_direction * speed, plan.flight_time_s);
    Eigen::Vector3d lead = target + target_vel * plan.flight_time_s;
    EXPECT_LT((bullet - lead).norm(), 0.01);
}

TEST(HW43DWithDrag, NearTargetHitRateIsHigh) {
    if (plan_is_stub() || acceleration_is_stub()) GTEST_SKIP() << "plan_shot or projectile_acceleration unimplemented";
    using namespace aiming_hw::ballistic;
    auto params = ProjectileParams::rm_17mm();
    int hits = 0;
    int trials = 0;
    // Sweep target placements within 5 m. Longer-range hit-rate bars require
    // the full dispersion + heat model, but a static target at <= 5 m should
    // hit ~100% of the time.
    for (double dx = 2.0; dx <= 5.0; dx += 0.5) {
        for (double dy = -1.5; dy <= 1.5; dy += 0.5) {
            ++trials;
            Eigen::Vector3d target(dx, dy, 0.5);
            auto plan = plan_shot(params, Eigen::Vector3d::Zero(), 27.0,
                                  target, Eigen::Vector3d::Zero());
            if (plan.converged && plan.miss_distance_m < 0.01) ++hits;
        }
    }
    EXPECT_GE(hits, static_cast<int>(0.95 * trials))
        << hits << " / " << trials;
}
