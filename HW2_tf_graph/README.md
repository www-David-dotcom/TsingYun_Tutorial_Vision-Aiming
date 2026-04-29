# HW2 — 坐标变换图 / TF Graph

> 第二道作业：写一个简单的位姿缓存（TF buffer），存时间戳化的
> rigid transform，按时间插值返回。两个 TODO：四元数 SLERP 插值
> 和变换链组合。所有数学都用 Eigen，没有 ROS 依赖。
>
> **Your job:** implement two short C++ functions —
> `tf::interpolate` (lerp + SLERP with short-arc fix-up) and
> `tf::compose` (rigid-transform chaining). The buffer that uses them
> is filled. Three public tests drive the work.

---

## 设计 / Design

```
                  +-----------------+
                  |   tf::Buffer    |   per-edge time series
                  +--------+--------+
                           |
                           v   uses
                  +-----------------+
                  | tf::interpolate |   ← TODO(HW2)
                  +-----------------+
                           |
                           v
                  +-----------------+
                  |   tf::compose   |   ← TODO(HW2)
                  +-----------------+
```

* `Transform` (in [`include/aiming_hw/tf/transform.hpp`](include/aiming_hw/tf/transform.hpp))
  is a `Vector3d translation` + `Quaterniond rotation`. It has
  `operator*` for transforming points and `inverse()` for the
  parent ↔ child swap. Both are filled.
* `Buffer` ([`buffer.hpp`](include/aiming_hw/tf/buffer.hpp) +
  [`buffer.cpp`](source/buffer.cpp)) stores per-edge chronological
  time series and exposes `lookup_direct` / `lookup_chain`. Filled.
* [`interpolate.hpp`](include/aiming_hw/tf/interpolate.hpp) declares
  the two TODO functions; [`interpolate.cpp`](source/interpolate.cpp)
  has the stubs.

---

## 你要写什么 / What you write

### 1. `tf::interpolate(a, b, alpha)`

Translation lerps; rotation SLERPs. Three things to get right:

* Clamp `alpha` to `[0, 1]` — callers do `dt / total_dt` and that
  occasionally rounds slightly out of range.
* Compute the quaternion dot. If it's negative, flip one quaternion
  before SLERPing so the slerp takes the **short arc**. Eigen's
  `Quaterniond::slerp` does NOT do this for you.
* Linear lerp on translation: `a + alpha * (b - a)`.

The `AntipodalQuaternionsTakeShortArc` test pins this — an unflipped
SLERP between two same-orientation antipodal quaternions swings the
gimbal the long way around and the test catches it.

### 2. `tf::compose(parent_to_middle, middle_to_child)`

Three lines:

```cpp
return Transform{
    parent_to_middle.rotation * middle_to_child.translation
        + parent_to_middle.translation,
    parent_to_middle.rotation * middle_to_child.rotation,
};
```

Get the order wrong and `TwoLinkChainAgreesWithOperatorStarOnPoints`
fails.

---

## 跑测试 / Running the tests

```bash
docker compose -f shared/docker/toolchain.compose.yaml run --rm dev \
    bash -c "cmake --preset linux-debug && cmake --build --preset linux-debug && ctest --preset linux-debug -R hw2"
```

Three test executables:

| Test | What it pins |
|------|--------------|
| `hw2_basic_lookup_test` | exact / interpolated / out-of-range / non-monotonic-insert |
| `hw2_interpolation_corners_test` | alpha=0/1 endpoints, antipodal short-arc, alpha clamping |
| `hw2_chain_compose_test` | identity is the neutral element, chain agrees with point-by-point composition, `Buffer::lookup_chain` agrees with manual `compose` |

Each test detects the unfilled-TODO state via a sentinel call and
`GTEST_SKIP`s with a clear message, so the rest of the project's CI
stays green until you fill the TODOs. Once filled, all assertions
should pass within `1e-9`.

---

## 已知非范围 / Out of scope

* ROS 2 TF2 integration. The whole point of the assignment is to
  *write* the math; pulling in `tf2_eigen` would obviate it.
* Frame-graph BFS / parent-pointer maintenance. `Buffer` exposes
  `lookup_chain(frames, t)` where the caller passes the chain
  explicitly; HW6's runner uses static chains
  (`world → chassis → gimbal → camera`), so no graph walker is needed.
* Thread safety. `Buffer` is single-threaded; HW6 wraps it with a
  mutex. Don't add locking here.
