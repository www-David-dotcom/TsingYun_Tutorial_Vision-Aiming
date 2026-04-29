#include "aiming_hw/ekf/motion_models.hpp"

#include <cmath>

namespace aiming_hw {
namespace ekf {

StateMat cv_transition(double dt) {
    StateMat F = StateMat::Identity();
    F(0, 2) = dt;
    F(1, 3) = dt;
    return F;
}

StateMat ct_transition(double dt, double omega) {
    if (std::abs(omega) < 1e-6) {
        return cv_transition(dt);
    }
    const double s = std::sin(omega * dt);
    const double c = std::cos(omega * dt);
    StateMat F = StateMat::Identity();
    F(0, 2) =  s / omega;
    F(0, 3) = -(1.0 - c) / omega;
    F(1, 2) =  (1.0 - c) / omega;
    F(1, 3) =  s / omega;
    F(2, 2) =  c;
    F(2, 3) = -s;
    F(3, 2) =  s;
    F(3, 3) =  c;
    return F;
}

StateMat process_noise_cv(double dt, double sigma_a) {
    const double half_dt2 = 0.5 * dt * dt;
    Eigen::Matrix<double, 4, 2> G;
    G << half_dt2, 0.0,
         0.0,      half_dt2,
         dt,       0.0,
         0.0,      dt;
    return (G * G.transpose()) * (sigma_a * sigma_a);
}

StateMat process_noise_ct(double dt, double sigma_a) {
    return process_noise_cv(dt, sigma_a) * 1.5;
}

}  // namespace ekf
}  // namespace aiming_hw
