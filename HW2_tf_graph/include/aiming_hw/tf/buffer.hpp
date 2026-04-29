#pragma once

// In-memory transform buffer. Stores per-edge time series of
// (parent → child) transforms and answers lookup queries by
// interpolating the two bracket samples.
//
// The buffer is intentionally simple — no thread safety, no
// frame-graph traversal beyond a single chain pre-built by the
// caller. HW6's runner is the consumer that wraps this with a mutex
// and uses lookup_chain for camera→world resolution.

#include <cstdint>
#include <map>
#include <stdexcept>
#include <string>
#include <vector>

#include "aiming_hw/tf/transform.hpp"

namespace aiming_hw {
namespace tf {

struct EdgeKey {
    std::string parent;
    std::string child;

    bool operator<(const EdgeKey& other) const {
        if (parent != other.parent) return parent < other.parent;
        return child < other.child;
    }
};

class Buffer {
public:
    // Insert a sample. Multiple samples per edge are stored in
    // chronological order (binary-inserted on each set so lookups
    // can binary-search). Inserting a sample with a stamp older than
    // the most recent throws — the buffer is monotonic.
    void set_transform(const std::string& parent,
                       const std::string& child,
                       std::uint64_t stamp_ns,
                       const Transform& transform);

    // Direct edge lookup. Returns the interpolated transform at
    // `stamp_ns`. Throws if the edge isn't known or the stamp falls
    // outside the recorded interval.
    Transform lookup_direct(const std::string& parent,
                            const std::string& child,
                            std::uint64_t stamp_ns) const;

    // Multi-link lookup. The caller passes a sequence of frame names
    // [f_0, f_1, ..., f_n]; the buffer composes the per-edge
    // transforms (f_0 → f_1) ∘ (f_1 → f_2) ∘ ... ∘ (f_{n-1} → f_n)
    // at the given stamp.
    //
    // For Stage 4 we expose the chain explicitly rather than
    // implementing a parent-pointer graph walker — the chain is
    // typically known statically (chassis → gimbal → camera), so the
    // caller's intent is clearer when the chain is named at the call
    // site.
    Transform lookup_chain(const std::vector<std::string>& frames,
                           std::uint64_t stamp_ns) const;

    // Drop samples older than `cutoff_ns` from every edge. Bounded
    // memory under continuous insertion.
    void prune_older_than(std::uint64_t cutoff_ns);

    // Diagnostic — size of every edge's history. Used by the public
    // tests; not expected on the hot path.
    std::map<EdgeKey, std::size_t> edge_sizes() const;

private:
    std::map<EdgeKey, std::vector<Stamped>> edges_;

    // Interpolate inside one edge's chronologically-sorted vector.
    // Throws if `samples` is empty or `stamp_ns` is out of range.
    Transform interpolate_in_series(const std::vector<Stamped>& samples,
                                    std::uint64_t stamp_ns) const;
};

class LookupError : public std::runtime_error {
public:
    using std::runtime_error::runtime_error;
};

}  // namespace tf
}  // namespace aiming_hw
