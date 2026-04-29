#pragma once

// Cooperative watchdog for the control loop. The control thread
// `pet`s the watchdog every iteration; if it ever skips a pet for
// longer than `timeout`, the watchdog fires the registered callback.
// HW6's runner uses it to zero the gimbal torque commands as a
// safety fallback when the inference loop hangs (e.g. the ONNX
// session-run blocks because the GPU was paused).
//
// Single watchdog instance per loop. Internally driven by a
// std::thread that polls every `tick_period`; a coarser tick is fine
// because the timeout we care about is on the order of 50–100 ms.

#include <atomic>
#include <chrono>
#include <functional>
#include <thread>

namespace aiming_hw {
namespace pipeline {

class Watchdog {
public:
    using Clock = std::chrono::steady_clock;
    using Duration = std::chrono::nanoseconds;
    using Callback = std::function<void()>;

    Watchdog(Duration timeout, Duration tick_period, Callback on_expire);
    ~Watchdog();

    Watchdog(const Watchdog&) = delete;
    Watchdog& operator=(const Watchdog&) = delete;

    // Reset the deadline to now + timeout. Called from the control
    // thread on every loop iteration.
    void pet();

    // True iff the watchdog has fired since the last pet. Reset on
    // the next pet.
    bool expired() const noexcept;

    // Stop the polling thread. Safe to call from any thread; called
    // automatically by the dtor.
    void stop();

private:
    void poll_loop();

    Duration              timeout_;
    Duration              tick_period_;
    Callback              on_expire_;
    // Deadline stored as a count of nanoseconds since clock epoch —
    // std::atomic<time_point> is implementation-defined-lock-free on
    // most platforms but not all; the int64_t form is portable.
    std::atomic<int64_t>  deadline_ns_{0};
    std::atomic<bool>     expired_{false};
    std::atomic<bool>     stopped_{false};
    std::thread           thread_;
};

}  // namespace pipeline
}  // namespace aiming_hw
