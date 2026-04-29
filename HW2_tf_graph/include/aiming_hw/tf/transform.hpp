#pragma once

// Rigid transform: rotation (unit quaternion) + translation (3-vec).
// Convention: a Transform represents the rigid map from one frame's
// origin/basis to another's. The translation is the position of the
// child frame's origin expressed in the parent's coordinates; the
// rotation rotates child-frame vectors into the parent.
//
// Equivalently: parent_point = R * child_point + t
//
// All math primitives in this module use double precision because the
// candidate's downstream EKF (HW3) and ballistic solver (HW4) both
// expect doubles for state-covariance numerical stability.

#include <Eigen/Geometry>
#include <cstdint>

namespace aiming_hw {
namespace tf {

struct Transform {
    Eigen::Vector3d    translation;
    Eigen::Quaterniond rotation;

    static Transform identity() {
        return Transform{Eigen::Vector3d::Zero(), Eigen::Quaterniond::Identity()};
    }

    // Apply this transform to a point expressed in the *child* frame,
    // returning the same point expressed in the *parent* frame.
    Eigen::Vector3d operator*(const Eigen::Vector3d& child_point) const {
        return rotation * child_point + translation;
    }

    // Inverse map: parent → child.
    Transform inverse() const {
        const Eigen::Quaterniond q_inv = rotation.conjugate();
        return Transform{q_inv * (-translation), q_inv};
    }
};

// A timestamped sample. The buffer stores these in chronological
// order per (parent, child) edge.
struct Stamped {
    std::uint64_t stamp_ns;
    Transform     transform;
};

}  // namespace tf
}  // namespace aiming_hw
