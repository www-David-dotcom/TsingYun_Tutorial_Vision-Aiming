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
    // TODO(HW3): constant-turn transition matrix.
    //
    //  F_CT(dt, ω) = ⎡1  0   sin(ωdt)/ω    -(1-cos(ωdt))/ω ⎤
    //                ⎢0  1   (1-cos(ωdt))/ω    sin(ωdt)/ω  ⎥
    //                ⎢0  0   cos(ωdt)        -sin(ωdt)     ⎥
    //                ⎣0  0   sin(ωdt)         cos(ωdt)     ⎦
    //
    // For ω → 0 the matrix degenerates to F_CV via sin(x)/x → 1.
    // Avoid the division-by-zero by short-circuiting to cv_transition
    // when |ω| is below ~1e-6.
    //
    // The IMM in imm.cpp calls this every step for the CT mode; if
    // it returns identity, the CT mode never propagates motion and
    // the public test `ConstantTurnFavoursCT` skips via the
    // `imm_is_stub` sentinel in test_imm_mode_probabilities.cpp.
    (void)dt;
    (void)omega;
    return StateMat::Identity();
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
