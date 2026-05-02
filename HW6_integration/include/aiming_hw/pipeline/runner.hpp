#pragma once

// HW6 runtime — wires HW1 (detector) → HW3 (EKF) → HW4 (ballistic) →
// HW5 (controller) into one control loop. The runner owns the
// frame ring buffer, the watchdog, and atomic snapshots of the gimbal
// state shared between the producer (ZMQ subscriber) and the
// consumer (control thread).
//
// One TODO(HW6): site, in source/runner.cpp::Runner::next_frame -
// the stale-frame drop policy.

#include <atomic>
#include <cstdint>
#include <memory>
#include <mutex>
#include <vector>

#include "aiming_hw/pipeline/ring_buffer.hpp"
#include "aiming_hw/pipeline/watchdog.hpp"

namespace aiming_hw {
namespace pipeline {

struct Frame {
    uint64_t          frame_id   = 0;
    uint64_t          stamp_ns   = 0;     // simulator-time ns since EnvReset
    int               width      = 0;
    int               height     = 0;
    std::vector<uint8_t> rgb;             // raw RGB888, length = w*h*3
};

struct GimbalSnapshot {
    double yaw         = 0.0;
    double pitch       = 0.0;
    double yaw_rate    = 0.0;
    double pitch_rate  = 0.0;
    uint64_t stamp_ns  = 0;
};

struct ControlOutput {
    double yaw_target_rad   = 0.0;
    double pitch_target_rad = 0.0;
    bool   fire             = false;
    int    burst_count      = 0;
    uint64_t stamp_ns       = 0;
    double  loop_latency_ns = 0.0;     // for the runtime-stats panel
};

struct RunnerConfig {
    std::size_t frame_buffer_capacity   = 8;
    int         control_rate_hz         = 100;
    int         max_stale_frames        = 2;       // drop frames older than (latest - max_stale)
    double      watchdog_timeout_ms     = 50.0;
    double      watchdog_tick_ms        = 5.0;
    bool        use_mpc                  = false;   // PID baseline by default
};

class Runner {
public:
    explicit Runner(const RunnerConfig& cfg = RunnerConfig{});
    ~Runner();

    // Producer side (ZMQ subscriber thread): push a freshly received
    // frame. Returns false if the ring buffer is full (caller drops).
    bool push_frame(Frame&& frame);

    // Producer side: update the latest gimbal state from the
    // simulator's SensorBundle. Atomic so the control thread sees a
    // consistent (yaw, pitch, rates) snapshot.
    void publish_gimbal(const GimbalSnapshot& snap);

    // Consumer side (control thread): pop the next freshest frame
    // honouring the stale-frame drop policy. Returns false if no
    // usable frame is queued. The blank is in the body.
    bool next_frame(Frame& out);

    // Convenience snapshot of the latest gimbal pose for the EKF.
    GimbalSnapshot latest_gimbal() const;

    // Per-iteration watchdog pet — call once per control loop step.
    void pet_watchdog();

    bool watchdog_expired() const noexcept;

    // Statistics surface — exposed for the runtime-stats HUD in the
    // arena and for the leaderboard JSON.
    struct Stats {
        uint64_t frames_received   = 0;
        uint64_t frames_dropped    = 0;
        uint64_t frames_consumed   = 0;
        uint64_t loop_iterations   = 0;
        double   loop_latency_p95_ns = 0.0;
    };
    Stats stats() const;

    void on_loop_complete(double latency_ns);

private:
    RunnerConfig            cfg_;
    SpscRingBuffer<Frame>   frame_q_;
    std::unique_ptr<Watchdog> watchdog_;

    mutable std::mutex      gimbal_mu_;
    GimbalSnapshot          latest_gimbal_;

    std::atomic<uint64_t>   frames_received_{0};
    std::atomic<uint64_t>   frames_dropped_{0};
    std::atomic<uint64_t>   frames_consumed_{0};
    std::atomic<uint64_t>   loop_iterations_{0};
    std::atomic<uint64_t>   latest_frame_id_{0};

    // Reservoir of recent loop latencies for the p95 rolling stat.
    mutable std::mutex      latency_mu_;
    std::vector<double>     recent_latencies_;
};

}  // namespace pipeline
}  // namespace aiming_hw
