// SPSC ring buffer correctness — push/pop, fill+drain, and a
// concurrent producer/consumer race. The race test runs under
// ThreadSanitizer in CI; bugs in the memory ordering surface here
// rather than in HW6's runtime where they're hard to reproduce.

#include <gtest/gtest.h>

#include <atomic>
#include <thread>
#include <vector>

#include "aiming_hw/pipeline/ring_buffer.hpp"

using aiming_hw::pipeline::SpscRingBuffer;

TEST(HW6RingBuffer, CapacityRoundsUpToPow2) {
    SpscRingBuffer<int> rb(5);
    // 5 → 8 internally, capacity() returns capacity-1 useful slots = 7.
    EXPECT_EQ(rb.capacity(), 7u);
}

TEST(HW6RingBuffer, RejectsCapacityBelowTwo) {
    EXPECT_THROW(SpscRingBuffer<int>(0), std::invalid_argument);
    EXPECT_THROW(SpscRingBuffer<int>(1), std::invalid_argument);
}

TEST(HW6RingBuffer, FillAndDrain) {
    SpscRingBuffer<int> rb(8);     // 7 useful slots
    EXPECT_TRUE(rb.empty());
    EXPECT_EQ(rb.size(), 0u);
    for (int i = 0; i < 7; ++i) {
        EXPECT_TRUE(rb.push(i)) << "push " << i;
    }
    EXPECT_EQ(rb.size(), 7u);
    EXPECT_FALSE(rb.push(99));     // full
    int out = -1;
    for (int i = 0; i < 7; ++i) {
        ASSERT_TRUE(rb.pop(out));
        EXPECT_EQ(out, i);
    }
    EXPECT_TRUE(rb.empty());
    EXPECT_FALSE(rb.pop(out));      // empty
}

TEST(HW6RingBuffer, MoveOnlyType) {
    struct MoveOnly {
        std::unique_ptr<int> p;
        MoveOnly() = default;
        explicit MoveOnly(int v) : p(std::make_unique<int>(v)) {}
        MoveOnly(MoveOnly&&) = default;
        MoveOnly& operator=(MoveOnly&&) = default;
        MoveOnly(const MoveOnly&) = delete;
        MoveOnly& operator=(const MoveOnly&) = delete;
    };
    SpscRingBuffer<MoveOnly> rb(4);
    EXPECT_TRUE(rb.push(MoveOnly(42)));
    MoveOnly out;
    ASSERT_TRUE(rb.pop(out));
    EXPECT_EQ(*out.p, 42);
}

TEST(HW6RingBuffer, ConcurrentProducerConsumer) {
    SpscRingBuffer<int> rb(64);
    constexpr int N = 100'000;
    std::atomic<bool> producer_done{false};

    std::thread producer([&] {
        for (int i = 0; i < N; ++i) {
            // Spin if the queue is full; we only have one consumer.
            while (!rb.push(i)) {
                std::this_thread::yield();
            }
        }
        producer_done.store(true, std::memory_order_release);
    });

    std::vector<int> received;
    received.reserve(N);
    int out = 0;
    while (received.size() < static_cast<std::size_t>(N)) {
        if (rb.pop(out)) {
            received.push_back(out);
        } else if (producer_done.load(std::memory_order_acquire)) {
            // Drain whatever's left.
            while (rb.pop(out)) received.push_back(out);
        } else {
            std::this_thread::yield();
        }
    }
    producer.join();

    ASSERT_EQ(received.size(), static_cast<std::size_t>(N));
    for (int i = 0; i < N; ++i) {
        EXPECT_EQ(received[i], i);
    }
}
