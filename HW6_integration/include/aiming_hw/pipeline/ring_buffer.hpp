#pragma once

// Lock-free single-producer / single-consumer ring buffer.
// Header-only — the runner instantiates one for raw-frame staging
// between the ZMQ subscriber thread (producer) and the inference
// thread (consumer).
//
// Capacity is fixed at construction (not a template arg, so the size
// can come from a YAML config) and is bumped to the next power of two
// internally — the masked indexing is the reason this is one cache
// line of atomics rather than a per-element mutex.
//
// Invariants:
//   * Exactly one writer thread calls `push`.
//   * Exactly one reader thread calls `pop` / `try_peek`.
//   * Capacity is at least 2 (capacity-1 useful slots so head==tail
//     means empty without wraparound ambiguity).
//
// Validated by tests/public/test_ring_buffer.cpp under TSan.

#include <atomic>
#include <cstddef>
#include <stdexcept>
#include <utility>
#include <vector>

namespace aiming_hw {
namespace pipeline {

template <typename T>
class SpscRingBuffer {
public:
    explicit SpscRingBuffer(std::size_t requested_capacity)
        : capacity_(round_up_pow2(requested_capacity)),
          mask_(capacity_ - 1),
          slots_(capacity_),
          head_(0),
          tail_(0) {
        if (requested_capacity < 2) {
            throw std::invalid_argument(
                "SpscRingBuffer: capacity must be >= 2");
        }
    }

    std::size_t capacity() const noexcept { return capacity_ - 1; }

    // Producer: enqueue `value`. Returns false (without blocking) if
    // the buffer is full — caller decides drop-vs-overwrite policy.
    bool push(T value) {
        const std::size_t head = head_.load(std::memory_order_relaxed);
        const std::size_t next = (head + 1) & mask_;
        if (next == tail_.load(std::memory_order_acquire)) {
            return false;
        }
        slots_[head] = std::move(value);
        head_.store(next, std::memory_order_release);
        return true;
    }

    // Consumer: dequeue into `out`. Returns false if empty.
    bool pop(T& out) {
        const std::size_t tail = tail_.load(std::memory_order_relaxed);
        if (tail == head_.load(std::memory_order_acquire)) {
            return false;
        }
        out = std::move(slots_[tail]);
        tail_.store((tail + 1) & mask_, std::memory_order_release);
        return true;
    }

    // Consumer: peek the next element without consuming. Returns
    // false if empty. The pointer is valid until the next pop.
    bool try_peek(T*& out) {
        const std::size_t tail = tail_.load(std::memory_order_relaxed);
        if (tail == head_.load(std::memory_order_acquire)) {
            return false;
        }
        out = &slots_[tail];
        return true;
    }

    std::size_t size() const noexcept {
        const std::size_t head = head_.load(std::memory_order_acquire);
        const std::size_t tail = tail_.load(std::memory_order_acquire);
        return (head - tail) & mask_;
    }

    bool empty() const noexcept {
        return head_.load(std::memory_order_acquire) ==
               tail_.load(std::memory_order_acquire);
    }

private:
    static std::size_t round_up_pow2(std::size_t n) {
        if (n < 2) return 2;
        std::size_t p = 1;
        while (p < n) p <<= 1;
        return p;
    }

    const std::size_t capacity_;
    const std::size_t mask_;
    std::vector<T>    slots_;

    // alignas keeps the producer's hot path (head_) and the consumer's
    // hot path (tail_) on separate cache lines.
    alignas(64) std::atomic<std::size_t> head_;
    alignas(64) std::atomic<std::size_t> tail_;
};

}  // namespace pipeline
}  // namespace aiming_hw
