# HW6 — 集成 / Integration Runner

> **Status:** Active as part of the Unity-first assignment path; older
> standalone workflows in this folder are legacy reference.
>
> **Unity-first role:** Connect frame ingestion, perception, tracking, aiming,
> and Unity control.
>
> **Legacy-only:** Threading and stale-frame tests are partial gates; full
> Unity smoke requires the running arena.
>
> **Mini-test:** `ctest --preset linux-debug -R hw6`
>
> **Mini-test files:**
> - `HW6_integration/tests/public/test_ring_buffer.cpp`
> - `HW6_integration/tests/public/test_watchdog.cpp`

> 第六道作业：把 HW1（检测）→ HW3（跟踪）→ HW4（弹道）→ HW5（控制）
> 串成一个实时跑的程序。HW6 自己只写 ring buffer + watchdog +
> 线程编排——具体的算法都已经在前几道作业里写过了。
> 两个 TODO：线程布局（main.cpp）和过期帧丢弃策略（runner.cpp）。
>
> **Your job:** decide the thread layout for `hw_runner` (one TODO in
> `source/main.cpp`) and implement the stale-frame drop policy (one
> TODO in `source/runner.cpp::Runner::next_frame`). The SPSC ring
> buffer, the watchdog, and the per-HW math libraries are all
> filled — HW6 is the assembly job.

---

## Student Quickstart

### Prerequisites

- Complete the root [First-time setup](../README.md#first-time-setup).
- Complete or intentionally stub A1, A3, A4, and A5 before expecting a useful
  live runner.
- Use the Docker toolchain for C++ mini-tests.

### What to implement

Fill the `TODO(HW6):` sites in:

- `HW6_integration/source/runner.cpp`
- `HW6_integration/source/main.cpp`

Start with `Runner::next_frame`; it has a direct unit test. The thread layout
in `run_episode` is the system integration step.

### Mini-test command

```bash
cmake --preset linux-debug
cmake --build --preset linux-debug
ctest --preset linux-debug -R hw6
```

### Expected first run

The ring-buffer tests should pass on a fresh checkout. Watchdog tests that
touch unfilled polling behavior can `GTEST_SKIP` until the relevant
`TODO(HW6):` body is complete.

### Before moving on

Run `ctest --preset linux-debug -R hw6`. For live smoke, open the Unity scene,
enter Play mode, and run `UV_CACHE_DIR=.uv-cache uv run python
tools/scripts/smoke_arena.py --seed 42 --ticks 10`.

## 设计 / Design

```
                ZMQ subscriber thread
                      ↓ push_frame()
                 ┌────────────────────┐
                 │  SpscRingBuffer    │  (filled, header-only)
                 │     <Frame>        │
                 └─────────┬──────────┘
                           │ next_frame()    ← TODO: stale-drop
                           ▼
        ┌──────────────────────────────────────────┐
        │  Control loop (hw_runner main thread)    │
        │  HW1 detect → HW3 track → HW4 ballistic  │
        │           → HW5 PID/MPC                  │
        │  pet_watchdog() once per iteration       │
        └─────────────────────┬────────────────────┘
                              │ GimbalCmd
                              ▼
                       gRPC client thread
                              │
                              ▼
                       Unity arena control server
```

* `Frame{frame_id, stamp_ns, w, h, rgb}` is what the ZMQ thread
  pushes. Layout matches `shared/proto/sensor.proto::FrameRef`.
* `GimbalSnapshot{yaw, pitch, yaw_rate, pitch_rate, stamp_ns}` is
  the latest gimbal pose, published from the gRPC thread and read
  by the control loop. Stored under one mutex (rare contention; one
  small struct).
* `Watchdog` fires its callback when the control loop hasn't pet
  it for `timeout_ms`. The candidate's expiry callback should zero
  the gimbal torque commands as a safety fallback.
* `Runner::stats()` exposes `frames_received / dropped / consumed`,
  loop iteration count, and rolling p95 loop latency. HW6's
  acceptance criterion is **p95 ≤ 25 ms**.

---

## 你要写什么 / What you write

### 1. `Runner::next_frame` — stale-frame drop policy (`source/runner.cpp`)

The control loop is slower than the producer (~100 Hz vs ~60 Hz
frames; spikes can stretch the loop further). Without a drop
policy the queue fills with old frames and the EKF ingests stale
data. The TODO walks you through:

```
if (latest_frame_id - out.frame_id) > cfg_.max_stale_frames:
    drop and continue
else:
    return out
```

### 2. `run_episode` — thread layout (`source/main.cpp`)

The Stage 8 plan calls for at least three threads (frame subscriber,
gRPC client, control loop). Trade-offs:

* Combining the gRPC + ZMQ thread makes cancellation easier but the
  gRPC stream's blocking semantics interfere with `recv()`; separate
  threads avoid head-of-line blocking.
* The control loop should pin to a single core via
  `pthread_setaffinity_np` to keep p95 latency under 25 ms.
* The watchdog's expiry callback runs on the watchdog's polling
  thread — it must be lock-free (atomic stores / a small wake-up
  flag the control loop checks).

The current placeholder in `main.cpp` only spins the runner +
watchdog and prints stats every second. Replace it.

---

## 跑测试 / Running the tests

```bash
docker compose -f shared/docker/toolchain.compose.yaml run --rm dev \
    bash -c "cmake --preset linux-debug && cmake --build --preset linux-debug && ctest --preset linux-debug -R hw6"
```

Two GTest binaries:

| Test | What it pins |
|------|--------------|
| `hw6_ring_buffer_test` | capacity rounding, fill/drain, move-only types, 100k-element concurrent producer/consumer race |
| `hw6_watchdog_test` | doesn't fire when petted, fires once when starved (no double-fires from later ticks), recovers after pet, stop is idempotent |

Build once with `-DAIMING_HW6_TSAN=ON` to run the suite under
ThreadSanitizer:

```bash
cmake -B build_tsan -DAIMING_HW6_TSAN=ON
cmake --build build_tsan
ctest --test-dir build_tsan -R hw6
```

The Stage 8 acceptance bar is "no data races over 5 episodes" —
that's a system-level test against a live arena, not the unit
tests; HW6's CI runs under TSan on every commit instead.

---

## 已知非范围 / Out of scope

* HW7 strategy / behaviour-tree wiring — that's Stage 9.
* gRPC client implementation — the candidate uses the generated
  stubs from `shared/proto`; HW6 doesn't re-implement them.
* Hidden grading episodes — grading must be redesigned from `schema.md`.
