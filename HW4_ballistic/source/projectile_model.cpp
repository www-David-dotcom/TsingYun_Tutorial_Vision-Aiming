#include "aiming_hw/ballistic/projectile_model.hpp"

#include <algorithm>
#include <cmath>

namespace aiming_hw {
namespace ballistic {

Eigen::Vector3d projectile_acceleration(const ProjectileParams& params,
                                        const Eigen::Vector3d& velocity) {
    // TODO(HW4): the per-step acceleration on the projectile —
    // gravity plus quadratic aerodynamic drag.
    //
    //   gravity    a_g = (0, 0, params.gravity_z)        // Z-up world
    //   drag mag   F_d = 0.5 * ρ * Cd * A * |v|²         // Newtons
    //   drag dir   along -v̂, i.e. opposite the velocity
    //   total      a = a_g + (-v / |v|) * (F_d / mass)
    //
    // Edge cases:
    //   * |v| < 1e-9 → drag is undefined; return only gravity.
    //   * params.drag_coefficient == 0 → no drag (the no-drag preset
    //     short-circuits this; you can also `if (Cd == 0) skip`).
    //
    // The RK4 integrator in this file calls this every substep; if
    // it returns zero, the bullet hovers in place and every shot
    // returns t = 0 → the public tests detect that via the
    // `acceleration_is_stub` sentinel in test_3d_with_drag.cpp.
    (void)params;
    (void)velocity;
    return Eigen::Vector3d::Zero();
}

namespace {

struct State {
    Eigen::Vector3d p;
    Eigen::Vector3d v;
};

State rk4_step(const ProjectileParams& params, const State& s, double dt) {
    auto deriv = [&](const State& y) {
        return State{y.v, projectile_acceleration(params, y.v)};
    };
    State k1 = deriv(s);
    State y2 = {s.p + 0.5 * dt * k1.p, s.v + 0.5 * dt * k1.v};
    State k2 = deriv(y2);
    State y3 = {s.p + 0.5 * dt * k2.p, s.v + 0.5 * dt * k2.v};
    State k3 = deriv(y3);
    State y4 = {s.p + dt * k3.p, s.v + dt * k3.v};
    State k4 = deriv(y4);
    return State{
        s.p + (dt / 6.0) * (k1.p + 2.0 * k2.p + 2.0 * k3.p + k4.p),
        s.v + (dt / 6.0) * (k1.v + 2.0 * k2.v + 2.0 * k3.v + k4.v),
    };
}

State integrate_to(const ProjectileParams& params,
                   const Eigen::Vector3d& muzzle_pos,
                   const Eigen::Vector3d& muzzle_velocity,
                   double t,
                   double dt_substep) {
    State s{muzzle_pos, muzzle_velocity};
    if (t <= 0.0) return s;
    const int n = std::max(1, static_cast<int>(std::ceil(t / dt_substep)));
    const double dt = t / static_cast<double>(n);
    for (int i = 0; i < n; ++i) {
        s = rk4_step(params, s, dt);
    }
    return s;
}

}  // namespace

Eigen::Vector3d projectile_position_at(const ProjectileParams& params,
                                       const Eigen::Vector3d& muzzle_position,
                                       const Eigen::Vector3d& muzzle_velocity,
                                       double t,
                                       double dt_substep) {
    return integrate_to(params, muzzle_position, muzzle_velocity, t, dt_substep).p;
}

Eigen::Vector3d projectile_velocity_at(const ProjectileParams& params,
                                       const Eigen::Vector3d& muzzle_velocity,
                                       double t,
                                       double dt_substep) {
    return integrate_to(params, Eigen::Vector3d::Zero(), muzzle_velocity, t, dt_substep).v;
}

}  // namespace ballistic
}  // namespace aiming_hw
