#pragma once

// Ballistic aim-prediction solver.
//
// Two pieces, both candidate TODO(HW4): blanks:
//
//   1. `solve_flight_time` — given a muzzle direction + speed and a
//      static target position, find the time-of-flight at which the
//      bullet reaches the target's range. This is a 1D root-find
//      along the direction of motion; for 2D-with-gravity it's
//      analytically a quadratic.
//
//   2. `plan_shot` — given a target's *current* position and velocity,
//      iterate to find the muzzle direction that will hit the
//      target's *future* position when the bullet arrives. Lead
//      computation under drag + gravity is necessarily iterative.
//
// HW6's runner calls `plan_shot` at every gimbal control tick with
// the latest EKF estimate of the opponent's pose.

#include <Eigen/Core>

#include "aiming_hw/ballistic/projectile_model.hpp"

namespace aiming_hw {
namespace ballistic {

struct ShotPlan {
    Eigen::Vector3d aim_direction;     // unit vector pointing along the muzzle
    double          flight_time_s = 0.0;
    double          miss_distance_m = 0.0;   // residual after iteration ends
    int             iterations = 0;
    bool            converged = false;
};

// Fly-time of a bullet shot along `aim_direction` at speed
// `muzzle_speed_mps` from `muzzle_pos`, until it reaches the closest
// approach to `target_pos`. The return value is the time-of-flight in
// seconds; if the bullet never gets close (e.g. drag chokes it out
// short of the target), returns -1.
//
// TODO(HW4): implement flight-time solving.
double solve_flight_time(const ProjectileParams& params,
                         const Eigen::Vector3d& muzzle_pos,
                         const Eigen::Vector3d& aim_direction,
                         double muzzle_speed_mps,
                         const Eigen::Vector3d& target_pos);

// Compute the muzzle direction that hits `target_pos + target_vel * t`
// at the time `t` the bullet actually arrives. Iterative: pick an
// initial t guess, predict the lead, recompute t under drag/gravity,
// repeat until either the miss distance falls below `tolerance_m` or
// `max_iterations` is exhausted.
//
// TODO(HW4): implement iterative shot planning.
ShotPlan plan_shot(const ProjectileParams& params,
                   const Eigen::Vector3d& muzzle_pos,
                   double muzzle_speed_mps,
                   const Eigen::Vector3d& target_pos,
                   const Eigen::Vector3d& target_vel,
                   double tolerance_m = 0.01,
                   int max_iterations = 8);

}  // namespace ballistic
}  // namespace aiming_hw
