#include "aiming_hw/ballistic/projectile_model.hpp"

#include <algorithm>
#include <cmath>

namespace aiming_hw {
namespace ballistic {

Eigen::Vector3d projectile_acceleration(const ProjectileParams& params,
                                        const Eigen::Vector3d& velocity) {
    Eigen::Vector3d a(0.0, 0.0, params.gravity_z);
    const double speed = velocity.norm();
    if (speed > 1e-9 && params.drag_coefficient > 0.0) {
        const double drag_mag = 0.5 * params.air_density *
                                params.drag_coefficient *
                                params.frontal_area_m2 *
                                speed * speed;
        a += -velocity / speed * (drag_mag / params.mass_kg);
    }
    return a;
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
