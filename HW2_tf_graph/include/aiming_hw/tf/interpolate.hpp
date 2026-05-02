#pragma once

// Two primitives the buffer composes to answer lookup queries:
//
//   * `interpolate(a, b, alpha)` — blend two transforms by alpha in
//     [0, 1]. Translation lerps; rotation slerps with the antipodal
//     "short-arc" fix-up. The candidate fills this.
//   * `compose(a, b)` — chain two transforms. Given parent→middle and
//     middle→child, returns parent→child. The candidate fills this.
//
// Both are pure functions; the buffer in buffer.cpp calls them at
// each lookup. If you change the signature, update the buffer too.

#include "aiming_hw/tf/transform.hpp"

namespace aiming_hw {
namespace tf {

// Linear interpolation in translation, SLERP in rotation, with the
// short-arc antipodal fix-up. `alpha == 0` returns `a`; `alpha == 1`
// returns `b`. Out-of-range alpha is clamped.
//
// TODO(HW2): implement transform interpolation.
Transform interpolate(const Transform& a, const Transform& b, double alpha);

// Compose two transforms so that `compose(parent_to_middle,
// middle_to_child)` is the transform parent→child.
//
// TODO(HW2): implement transform composition.
Transform compose(const Transform& parent_to_middle,
                  const Transform& middle_to_child);

}  // namespace tf
}  // namespace aiming_hw
