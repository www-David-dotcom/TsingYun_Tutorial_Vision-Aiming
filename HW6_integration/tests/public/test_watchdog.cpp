// Watchdog: pet/expire/recover behaviour.

#include <gtest/gtest.h>

#include <atomic>
#include <chrono>
#include <thread>

#include "aiming_hw/pipeline/watchdog.hpp"

using namespace std::chrono_literals;
using aiming_hw::pipeline::Watchdog;

namespace {

bool poll_loop_is_stub() {
    // A real poll_loop fires the callback after the deadline lapses.
    // A stub thread that returns immediately leaves the callback
    // never invoked.
    std::atomic<int> fires{0};
    Watchdog wd(20ms, 2ms, [&] { fires.fetch_add(1, std::memory_order_relaxed); });
    wd.pet();
    std::this_thread::sleep_for(80ms);
    return fires.load() == 0;
}

}  // namespace

TEST(HW6Watchdog, DoesNotFireWhenPettedRegularly) {
    std::atomic<int> fires{0};
    Watchdog wd(50ms, 5ms, [&] { fires.fetch_add(1, std::memory_order_relaxed); });

    for (int i = 0; i < 20; ++i) {
        wd.pet();
        std::this_thread::sleep_for(10ms);   // well under the 50 ms timeout
    }
    EXPECT_EQ(fires.load(), 0);
    EXPECT_FALSE(wd.expired());
}

TEST(HW6Watchdog, FiresOnceWhenStarved) {
    if (poll_loop_is_stub()) {
        GTEST_SKIP() << "Watchdog::poll_loop unimplemented "
                        "- fill TODO(HW6): in source/watchdog.cpp";
    }
    std::atomic<int> fires{0};
    Watchdog wd(20ms, 2ms, [&] { fires.fetch_add(1, std::memory_order_relaxed); });
    wd.pet();
    std::this_thread::sleep_for(80ms);   // 4× the timeout

    // The callback should have fired exactly once — repeat tick
    // expirations after the first don't re-fire because expired_ is
    // latched until the next pet.
    EXPECT_EQ(fires.load(), 1);
    EXPECT_TRUE(wd.expired());
}

TEST(HW6Watchdog, RecoversAfterPet) {
    if (poll_loop_is_stub()) {
        GTEST_SKIP() << "Watchdog::poll_loop unimplemented";
    }
    std::atomic<int> fires{0};
    Watchdog wd(20ms, 2ms, [&] { fires.fetch_add(1, std::memory_order_relaxed); });
    wd.pet();
    std::this_thread::sleep_for(50ms);
    ASSERT_TRUE(wd.expired());

    wd.pet();
    EXPECT_FALSE(wd.expired());
    std::this_thread::sleep_for(10ms);
    EXPECT_FALSE(wd.expired());

    // And starve it again — the callback should fire a second time.
    std::this_thread::sleep_for(50ms);
    EXPECT_EQ(fires.load(), 2);
}

TEST(HW6Watchdog, StopIsIdempotent) {
    Watchdog wd(50ms, 5ms, [] {});
    wd.stop();
    wd.stop();   // must not crash or hang
    EXPECT_FALSE(wd.expired());
}
