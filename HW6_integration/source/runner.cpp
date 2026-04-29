#include "aiming_hw/pipeline/runner.hpp"

#include <algorithm>
#include <chrono>

namespace aiming_hw {
namespace pipeline {

namespace {

constexpr std::size_t kLatencyReservoir = 256;

}  // namespace

Runner::Runner(const RunnerConfig& cfg)
    : cfg_(cfg),
      frame_q_(cfg.frame_buffer_capacity) {
    using namespace std::chrono;
    const auto timeout = duration_cast<Watchdog::Duration>(
        duration<double, std::milli>(cfg.watchdog_timeout_ms));
    const auto tick = duration_cast<Watchdog::Duration>(
        duration<double, std::milli>(cfg.watchdog_tick_ms));
    watchdog_ = std::make_unique<Watchdog>(
        timeout, tick,
        [] {
            // Default expiry callback is a no-op; the runtime in
            // main.cpp wires its own (zero torque commands, log).
        });
    recent_latencies_.reserve(kLatencyReservoir);
}

Runner::~Runner() = default;

bool Runner::push_frame(Frame&& frame) {
    frames_received_.fetch_add(1, std::memory_order_relaxed);
    latest_frame_id_.store(frame.frame_id, std::memory_order_release);
    if (!frame_q_.push(std::move(frame))) {
        frames_dropped_.fetch_add(1, std::memory_order_relaxed);
        return false;
    }
    return true;
}

void Runner::publish_gimbal(const GimbalSnapshot& snap) {
    std::lock_guard<std::mutex> lock(gimbal_mu_);
    latest_gimbal_ = snap;
}

GimbalSnapshot Runner::latest_gimbal() const {
    std::lock_guard<std::mutex> lock(gimbal_mu_);
    return latest_gimbal_;
}

bool Runner::next_frame(Frame& out) {
    // TODO(HW6): stale-frame drop policy.
    //
    // The pipeline's tail latency budget is dominated by ONNX
    // inference; if the inference thread falls behind the producer,
    // frames pile up and the EKF starts ingesting old data. The
    // policy here is to skip-ahead to the freshest queued frame
    // when the queue is more than `cfg_.max_stale_frames` deep.
    //
    //   1. Read latest_frame_id_ (the most recent ID the producer
    //      has pushed — atomic, no synchronisation needed).
    //   2. Loop:
    //        if !frame_q_.pop(out): return false
    //        if (latest_frame_id - out.frame_id) <= cfg_.max_stale_frames:
    //            frames_consumed_.fetch_add(1)
    //            return true
    //        // otherwise out is too old; drop it and keep looping
    //        frames_dropped_.fetch_add(1)
    //
    // The current placeholder always returns the oldest frame —
    // i.e. no drop policy. Tests assert this is replaced before the
    // candidate ships HW6.
    if (!frame_q_.pop(out)) {
        return false;
    }
    frames_consumed_.fetch_add(1, std::memory_order_relaxed);
    return true;
}

void Runner::pet_watchdog() {
    if (watchdog_) watchdog_->pet();
}

bool Runner::watchdog_expired() const noexcept {
    return watchdog_ ? watchdog_->expired() : false;
}

void Runner::on_loop_complete(double latency_ns) {
    loop_iterations_.fetch_add(1, std::memory_order_relaxed);
    std::lock_guard<std::mutex> lock(latency_mu_);
    if (recent_latencies_.size() < kLatencyReservoir) {
        recent_latencies_.push_back(latency_ns);
    } else {
        // Reservoir replacement — keeps a fixed-size sliding window.
        const std::size_t idx = loop_iterations_.load(std::memory_order_relaxed)
                                % kLatencyReservoir;
        recent_latencies_[idx] = latency_ns;
    }
}

Runner::Stats Runner::stats() const {
    Stats s;
    s.frames_received = frames_received_.load(std::memory_order_acquire);
    s.frames_dropped  = frames_dropped_.load(std::memory_order_acquire);
    s.frames_consumed = frames_consumed_.load(std::memory_order_acquire);
    s.loop_iterations = loop_iterations_.load(std::memory_order_acquire);

    std::lock_guard<std::mutex> lock(latency_mu_);
    if (!recent_latencies_.empty()) {
        std::vector<double> sorted = recent_latencies_;
        std::sort(sorted.begin(), sorted.end());
        const std::size_t idx = static_cast<std::size_t>(
            std::round(0.95 * (sorted.size() - 1)));
        s.loop_latency_p95_ns = sorted[idx];
    }
    return s;
}

}  // namespace pipeline
}  // namespace aiming_hw
