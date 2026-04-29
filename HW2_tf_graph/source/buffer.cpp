#include "aiming_hw/tf/buffer.hpp"

#include <algorithm>
#include <sstream>

#include "aiming_hw/tf/interpolate.hpp"

namespace aiming_hw {
namespace tf {

namespace {

bool stamp_less(const Stamped& a, std::uint64_t t) {
    return a.stamp_ns < t;
}

}  // namespace

void Buffer::set_transform(const std::string& parent,
                           const std::string& child,
                           std::uint64_t stamp_ns,
                           const Transform& transform) {
    auto& series = edges_[EdgeKey{parent, child}];
    if (!series.empty() && series.back().stamp_ns >= stamp_ns) {
        std::ostringstream oss;
        oss << "Buffer::set_transform: stamp " << stamp_ns
            << " not greater than last sample (" << series.back().stamp_ns << ")"
            << " on edge " << parent << "->" << child;
        throw LookupError(oss.str());
    }
    series.push_back(Stamped{stamp_ns, transform});
}

Transform Buffer::lookup_direct(const std::string& parent,
                                const std::string& child,
                                std::uint64_t stamp_ns) const {
    auto it = edges_.find(EdgeKey{parent, child});
    if (it == edges_.end()) {
        std::ostringstream oss;
        oss << "Buffer::lookup_direct: unknown edge " << parent << "->" << child;
        throw LookupError(oss.str());
    }
    return interpolate_in_series(it->second, stamp_ns);
}

Transform Buffer::lookup_chain(const std::vector<std::string>& frames,
                               std::uint64_t stamp_ns) const {
    if (frames.size() < 2) {
        throw LookupError("Buffer::lookup_chain: need at least two frame names");
    }
    Transform result = lookup_direct(frames[0], frames[1], stamp_ns);
    for (std::size_t i = 1; i + 1 < frames.size(); ++i) {
        Transform link = lookup_direct(frames[i], frames[i + 1], stamp_ns);
        result = compose(result, link);
    }
    return result;
}

void Buffer::prune_older_than(std::uint64_t cutoff_ns) {
    for (auto& [key, series] : edges_) {
        auto cut = std::lower_bound(series.begin(), series.end(), cutoff_ns,
                                    stamp_less);
        // Keep at least one sample per edge so out-of-bound lookups
        // surface as LookupError, not as "edge has no history."
        if (cut != series.begin() && cut == series.end()) {
            cut = std::prev(series.end());
        }
        series.erase(series.begin(), cut);
    }
}

std::map<EdgeKey, std::size_t> Buffer::edge_sizes() const {
    std::map<EdgeKey, std::size_t> out;
    for (const auto& [key, series] : edges_) {
        out[key] = series.size();
    }
    return out;
}

Transform Buffer::interpolate_in_series(const std::vector<Stamped>& samples,
                                        std::uint64_t stamp_ns) const {
    // TODO(HW2): bracket-search + interpolate within one edge's
    // chronological time series.
    //
    // Required behaviours (the public tests pin these):
    //   * empty `samples` → throw LookupError("…empty series").
    //   * stamp before samples.front() OR after samples.back() →
    //     throw LookupError with the stamp + interval in the
    //     message. Out-of-range lookups are NOT extrapolated; HW6's
    //     runner is supposed to keep the buffer fresh enough that
    //     this never trips.
    //   * stamp exactly equal to a sample's stamp → return that
    //     sample's transform (no float rounding from interpolate).
    //   * otherwise: binary-search the bracket [lo, hi] s.t.
    //     lo.stamp_ns ≤ stamp_ns ≤ hi.stamp_ns, compute
    //     alpha = (stamp_ns - lo.stamp_ns) / (hi.stamp_ns - lo.stamp_ns),
    //     and return interpolate(lo.transform, hi.transform, alpha).
    //
    // Hint: std::lower_bound + the file-static `stamp_less`
    // comparator (defined at the top of this file) gives you the
    // bracket in O(log N) without rolling your own search.
    //
    // While this stub is in place, lookup_direct returns
    // Transform::identity() for every query. The
    // `MidpointInterpolatesTranslation` test in
    // test_basic_lookup.cpp detects that and GTEST_SKIPs cleanly.
    (void)samples;
    (void)stamp_ns;
    return Transform::identity();
}

}  // namespace tf
}  // namespace aiming_hw
