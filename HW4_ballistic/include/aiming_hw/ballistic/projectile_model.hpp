#pragma once

// Projectile dynamics for the HW4 ballistic solver. Quadratic drag +
// gravity, integrated with explicit RK4 substepping so the candidate's
// solver can use this as a black-box "where is the bullet at time t"
// query without rolling their own integrator.
//
// Convention: world is Z-up, gravity g = (0, 0, -9.81 m/s²). HW6's
// runner owns any simulator-to-solver convention conversion; HW4 itself
// is unaware of that.

#include <Eigen/Core>

namespace aiming_hw {
namespace ballistic {

struct ProjectileParams {
    // Defaults match the 17 mm RoboMaster pellet: 3.2 g, 8.5 mm radius,
    // sphere drag.
    double mass_kg          = 0.0032;
    double drag_coefficient = 0.47;
    double frontal_area_m2  = 0.000227;
    double air_density      = 1.225;
    double gravity_z        = -9.81;

    static ProjectileParams rm_17mm() { return ProjectileParams{}; }
    static ProjectileParams no_drag() {
        ProjectileParams p;
        p.drag_coefficient = 0.0;
        return p;
    }
    static ProjectileParams no_gravity_no_drag() {
        ProjectileParams p;
        p.drag_coefficient = 0.0;
        p.gravity_z = 0.0;
        return p;
    }
};

// Position of the bullet at time `t` after firing, computed by RK4
// integration. `dt_substep` controls the integrator step (1 ms by
// default — well within the 1 mm precision the public tests require
// over a 30 m flight).
Eigen::Vector3d projectile_position_at(const ProjectileParams& params,
                                       const Eigen::Vector3d& muzzle_position,
                                       const Eigen::Vector3d& muzzle_velocity,
                                       double t,
                                       double dt_substep = 1e-3);

// Velocity at time `t`. Same numerics as projectile_position_at,
// returned for convergence checks.
Eigen::Vector3d projectile_velocity_at(const ProjectileParams& params,
                                       const Eigen::Vector3d& muzzle_velocity,
                                       double t,
                                       double dt_substep = 1e-3);

// Acceleration at the current state. Useful inside the candidate's
// flight-time solver if they want to write their own integrator.
Eigen::Vector3d projectile_acceleration(const ProjectileParams& params,
                                        const Eigen::Vector3d& velocity);

}  // namespace ballistic
}  // namespace aiming_hw
