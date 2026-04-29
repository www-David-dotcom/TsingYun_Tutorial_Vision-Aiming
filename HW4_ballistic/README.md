# HW4 — 弹道补偿与超前射击 / Ballistic + Firing-Delay Solver

> 第四道作业：写一个弹道求解器。已知 HW3 给出的目标位置 + 速度，
> 计算应该往哪个方向射击，使得子弹（受重力 + 空气阻力影响）抵达
> 目标未来位置时刚好命中。两个 TODO：飞行时间求解和迭代超前求解。
>
> **Your job:** two short C++ functions —
> `solve_flight_time` (closest-approach time-of-flight along a given
> aim direction) and `plan_shot` (iterative lead computation under
> drag + gravity). The projectile dynamics are filled, so you can
> query bullet position at any time without writing your own
> integrator.

---

## 设计 / Design

```
                  +--------------------------+
                  |  ProjectileParams        |   filled (RM 17mm defaults)
                  +--------------------------+
                              |
                              v
                  +--------------------------+
                  | projectile_position_at   |   filled (RK4 substepping)
                  | projectile_velocity_at   |
                  +--------------------------+
                              ^
                              |   queried by
                  +--------------------------+
                  |   solve_flight_time      |   ← TODO(HW4)
                  +--------------------------+
                              ^
                              |
                  +--------------------------+
                  |   plan_shot              |   ← TODO(HW4)
                  +--------------------------+
```

* **Convention:** Z-up world. Gravity = (0, 0, -9.81 m/s²). HW6's
  runner does the convention swap when feeding shooter / target poses
  from Y-up Godot; HW4 itself is unaware of it.
* **Defaults:** `ProjectileParams::rm_17mm()` matches the simulator
  pellet (3.2 g, 8.5 mm radius, sphere drag, ρ = 1.225 kg/m³). The
  no-drag and no-gravity-no-drag presets are used by the public
  tests for the closed-form sanity checks.

---

## 你要写什么 / What you write

### 1. `solve_flight_time(params, muzzle, aim_dir, speed, target)`

Closest-approach time-of-flight: given a fixed aim direction and
muzzle speed, when does the bullet pass closest to `target`? Returns
the time in seconds, or `-1` if the bullet never reasonably reaches
the target (drag chokes it out, target above max range).

* Coarse scan first: sample t at 10 ms steps from 0 to ~3 s, query
  `projectile_position_at` at each step, find the bracket where the
  range residual is minimal.
* Refine inside the bracket via golden-section or bisection. A few
  iterations are enough to hit the 1 mm precision the public tests
  expect over a 30 m flight.
* For the no-drag, no-gravity case there's a closed form
  `t = ((target - muzzle) · aim_dir) / muzzle_speed`. You can
  short-circuit to it when `params.drag_coefficient == 0` and
  `params.gravity_z == 0`.

### 2. `plan_shot(params, muzzle, speed, target_pos, target_vel, ...)`

Iterative lead. Until the residual miss distance falls below
`tolerance_m` or `max_iterations` is exhausted:

```
t_guess = |target_pos - muzzle| / muzzle_speed_mps
for iter in 0..max_iterations:
    lead     = target_pos + target_vel * t_guess
    aim_dir  = (lead - muzzle).normalized()
    t_new    = solve_flight_time(params, muzzle, aim_dir, speed, lead)
    bullet   = projectile_position_at(params, muzzle, aim_dir * speed, t_new)
    miss     = (bullet - lead).norm()
    if miss < tolerance_m:
        converged
    t_guess  = t_new
```

The closed-form Playtechs solution
([blog](http://playtechs.blogspot.com/2007/04/aiming-at-moving-target.html))
applies only to the no-drag, no-gravity case. The iterative form
above converges in 2–4 iterations and works with drag too.

---

## 跑测试 / Running the tests

```bash
docker compose -f shared/docker/toolchain.compose.yaml run --rm dev \
    bash -c "cmake --preset linux-debug && cmake --build --preset linux-debug && ctest --preset linux-debug -R hw4"
```

Three test executables:

| Test | What it pins |
|------|--------------|
| `hw4_1d_no_drag_test` | flight time = range / speed; static-target aim direction; moving-target lead |
| `hw4_2d_with_gravity_test` | aim must lift above target; farther targets need more lift; flight time within 10% of analytic flat-range |
| `hw4_3d_with_drag_test` | converges ≤ 8 iterations; bullet reaches the predicted lead within 1 cm; ≥ 95% hit rate on a static-target sweep ≤ 5 m |

Each test detects the unfilled-TODO state via a sentinel call and
`GTEST_SKIP`s with a clear message.

The IMPLEMENTATION_PLAN.md acceptance bar (≥ 80% hit rate at 5 m,
≥ 50% at 10 m, ≥ 20% at 15 m for a constant-velocity target) needs
the full HW6 runner with HW3's tracker and an actual Godot arena —
HW4 in isolation only pins the math.

---

## 已知非范围 / Out of scope

* Spin / Magnus effect — ignored; HW6's runner handles dispersion
  via an empirical noise model rather than physical spin.
* Wind — not modelled in the simulator.
* Projectile-projectile collisions — never happen in practice.
* Heat / barrel-temperature limits — modelled in HW6, not HW4.
* Hidden grading episodes — deferred per `IMPLEMENTATION_PLAN.md`
  Stage 10.
