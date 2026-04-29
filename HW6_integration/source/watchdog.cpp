#include "aiming_hw/pipeline/watchdog.hpp"

#include <utility>

namespace aiming_hw {
namespace pipeline {

namespace {

int64_t now_ns() {
    return std::chrono::duration_cast<std::chrono::nanoseconds>(
        Watchdog::Clock::now().time_since_epoch()).count();
}

}  // namespace

Watchdog::Watchdog(Duration timeout, Duration tick_period, Callback on_expire)
    : timeout_(timeout),
      tick_period_(tick_period),
      on_expire_(std::move(on_expire)) {
    deadline_ns_.store(now_ns() + timeout_.count(), std::memory_order_release);
    thread_ = std::thread(&Watchdog::poll_loop, this);
}

Watchdog::~Watchdog() {
    stop();
}

void Watchdog::pet() {
    deadline_ns_.store(now_ns() + timeout_.count(), std::memory_order_release);
    expired_.store(false, std::memory_order_release);
}

bool Watchdog::expired() const noexcept {
    return expired_.load(std::memory_order_acquire);
}

void Watchdog::stop() {
    bool was_running = false;
    if (stopped_.compare_exchange_strong(was_running, true,
                                          std::memory_order_acq_rel)) {
        if (thread_.joinable()) {
            thread_.join();
        }
    }
}

void Watchdog::poll_loop() {
    while (!stopped_.load(std::memory_order_acquire)) {
        std::this_thread::sleep_for(tick_period_);
        const int64_t now = now_ns();
        const int64_t deadline = deadline_ns_.load(std::memory_order_acquire);
        if (now >= deadline) {
            const bool already = expired_.exchange(true, std::memory_order_acq_rel);
            if (!already && on_expire_) {
                on_expire_();
            }
        }
    }
}

}  // namespace pipeline
}  // namespace aiming_hw
