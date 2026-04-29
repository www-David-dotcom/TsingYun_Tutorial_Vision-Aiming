#include "aiming_hw/tf/interpolate.hpp"

#include <algorithm>
#include <cmath>

// Two TODOs. Each is a few lines once you've thought about the
// short-arc / antipodal cases for SLERP and the order of operations
// for compose. Read transform.hpp first — `Transform::operator*` and
// `Transform::inverse` may be useful but neither is required.

namespace aiming_hw {
namespace tf {

Transform interpolate(const Transform& a, const Transform& b, double alpha) {
    // TODO(HW2): linear lerp on translation, SLERP on rotation, with
    // the short-arc fix-up for antipodal quaternions.
    //
    // Hints:
    //   * clamp alpha to [0, 1] before doing anything else (callers
    //     occasionally pass slightly out-of-range values from
    //     dt/duration division).
    //   * translation: a.translation + alpha * (b.translation - a.translation).
    //   * rotation: dot = a.rotation.dot(b.rotation); if dot < 0 flip
    //     b's quaternion sign so we slerp along the short arc.
    //     Then call Eigen's `Eigen::Quaterniond::slerp`. SLERP is
    //     well-defined for unit quaternions; if your transforms drift
    //     off-unit (they shouldn't in this assignment), normalize at
    //     the end.
    //
    // Watch out: Eigen's Quaterniond::slerp does NOT do the short-arc
    // flip itself. Two near-antipodal unit quaternions slerped naively
    // will swing the long way around — the matching test in
    // tests/public/test_interpolation_corners.cpp catches this.
    (void)a;
    (void)b;
    (void)alpha;
    return Transform::identity();
}

Transform compose(const Transform& parent_to_middle,
                  const Transform& middle_to_child) {
    // TODO(HW2): chain two rigid transforms.
    //
    // Given:
    //   parent_to_middle: translation t_pm, rotation R_pm
    //   middle_to_child:  translation t_mc, rotation R_mc
    //
    // The composition parent_to_child has:
    //   translation = R_pm * t_mc + t_pm
    //   rotation    = R_pm * R_mc        (quaternion product)
    //
    // Three lines of code; getting the order wrong is a common
    // mistake. Verify against tests/public/test_chain_compose.cpp.
    (void)parent_to_middle;
    (void)middle_to_child;
    return Transform::identity();
}

}  // namespace tf
}  // namespace aiming_hw
