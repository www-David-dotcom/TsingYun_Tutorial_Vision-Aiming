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
    // TODO(HW6): the watchdog's polling thread.
    //
    // While stopped_ is false:
    //   1. sleep for tick_period_.
    //   2. compare now_ns() to the atomically-stored deadline_ns_.
    //   3. if we're past the deadline, atomically flip expired_ from
    //      false to true; on the FIRST transition (i.e. the call to
    //      compare_exchange or atomic_exchange that actually flipped
    //      it) invoke on_expire_(). Subsequent ticks while still past
    //      the deadline must NOT re-fire — see the public test
    //      `FiresOnceWhenStarved`.
    //
    // Concurrency notes:
    //   * `pet()` resets both deadline_ns_ AND expired_, so a pet
    //     after a fire allows the next starvation to re-fire. The
    //     `RecoversAfterPet` test pins this.
    //   * `stop()` is idempotent and joins this thread; the loop
    //     condition above is the cancellation point. No locks here —
    //     all state is in std::atomic.
    //
    // While this stub is in place, the watchdog never fires. Existing
    // tests (`FiresOnceWhenStarved`, `RecoversAfterPet`) detect that
    // and GTEST_SKIP via the new `poll_loop_is_stub` sentinel.

    // Stub: the thread body just exits, so the watchdog instance
    // sits inert until destruction.
    return;
}

}  // namespace pipeline
}  // namespace aiming_hw
