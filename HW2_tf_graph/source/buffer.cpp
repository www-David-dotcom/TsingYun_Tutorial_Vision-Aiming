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
    if (samples.empty()) {
        throw LookupError("Buffer::interpolate_in_series: empty series");
    }
    if (stamp_ns < samples.front().stamp_ns ||
        stamp_ns > samples.back().stamp_ns) {
        std::ostringstream oss;
        oss << "Buffer::interpolate_in_series: stamp " << stamp_ns
            << " outside [" << samples.front().stamp_ns
            << ", " << samples.back().stamp_ns << "]";
        throw LookupError(oss.str());
    }
    if (stamp_ns == samples.front().stamp_ns) return samples.front().transform;
    if (stamp_ns == samples.back().stamp_ns)  return samples.back().transform;

    auto upper = std::lower_bound(samples.begin(), samples.end(), stamp_ns,
                                  stamp_less);
    // upper now points at the first sample with stamp >= stamp_ns; we
    // bracket between upper-1 and upper.
    const auto& lo = *(upper - 1);
    const auto& hi = *upper;
    const double dt = static_cast<double>(hi.stamp_ns - lo.stamp_ns);
    const double alpha = static_cast<double>(stamp_ns - lo.stamp_ns) / dt;
    return interpolate(lo.transform, hi.transform, alpha);
}

}  // namespace tf
}  // namespace aiming_hw
