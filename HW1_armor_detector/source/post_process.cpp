#include "aiming_hw/detector/post_process.hpp"

#include <algorithm>
#include <array>
#include <cmath>
#include <stdexcept>

// Stage-3 post-processing: decode the raw head tensor to per-cell
// detections, then class-aware NMS.
//
// Two TODO blocks. Read the comments in each and write the obvious
// implementation; both are short.

namespace aiming_hw {
namespace detector {

namespace {

float sigmoid(float x) {
    return 1.0f / (1.0f + std::exp(-x));
}

float iou_xyxy(const Detection& a, const Detection& b) {
    const float ix1 = std::max(a.x1, b.x1);
    const float iy1 = std::max(a.y1, b.y1);
    const float ix2 = std::min(a.x2, b.x2);
    const float iy2 = std::min(a.y2, b.y2);
    const float iw  = std::max(0.0f, ix2 - ix1);
    const float ih  = std::max(0.0f, iy2 - iy1);
    const float inter = iw * ih;
    const float area_a = std::max(0.0f, a.x2 - a.x1) * std::max(0.0f, a.y2 - a.y1);
    const float area_b = std::max(0.0f, b.x2 - b.x1) * std::max(0.0f, b.y2 - b.y1);
    const float union_ = area_a + area_b - inter + 1e-7f;
    return inter / union_;
}

// Helper used by decode_head when you implement it. Returns the index
// (and softmax-confidence) of the best icon class for the 4-channel
// classification slice at one cell.
struct IconPick {
    Icon  icon;
    float confidence;
};

IconPick best_icon(const float* cls_logits) {
    float max_logit = cls_logits[0];
    int   argmax    = 0;
    for (int i = 1; i < kClsChan; ++i) {
        if (cls_logits[i] > max_logit) {
            max_logit = cls_logits[i];
            argmax = i;
        }
    }
    float denom = 0.0f;
    for (int i = 0; i < kClsChan; ++i) {
        denom += std::exp(cls_logits[i] - max_logit);
    }
    const float confidence = 1.0f / denom;          // softmax of the argmax channel
    return IconPick{static_cast<Icon>(argmax), confidence};
}

}  // namespace

// -------------------------------------------------------- decode_head

std::vector<Detection> decode_head(const float* head_data,
                                   std::size_t head_count,
                                   int feature_h,
                                   int feature_w,
                                   float score_threshold) {
    const std::size_t cell_count = static_cast<std::size_t>(feature_h) *
                                   static_cast<std::size_t>(feature_w);
    const std::size_t expected   = static_cast<std::size_t>(kTotalChan) * cell_count;
    if (head_count < expected) {
        throw std::invalid_argument("decode_head: head_data smaller than expected layout");
    }

    // Channel pointer-shorthand. Layout is C-major: head_data[c * H*W + cell].
    const float* box_chan = head_data + 0 * cell_count;            // 4 channels start here
    const float* kpt_chan = head_data + kBoxChan * cell_count;     // 8 channels
    const float* cls_chan = head_data + (kBoxChan + kKptChan) * cell_count;     // 4 channels
    const float* obj_chan = head_data +
        (kBoxChan + kKptChan + kClsChan) * cell_count;              // 1 channel

    std::vector<Detection> out;
    out.reserve(64);

    // TODO(HW1): for each (y, x) cell in the feature map:
    //   1. Compute the cell index `idx = y * feature_w + x`.
    //   2. Decode the box channels:
    //        cx = (x + 0.5 + (sigmoid(box_chan[0*HW + idx]) - 0.5)) * kStride
    //        cy = (y + 0.5 + (sigmoid(box_chan[1*HW + idx]) - 0.5)) * kStride
    //        w  = std::exp(box_chan[2*HW + idx])  // already in pixels
    //        h  = std::exp(box_chan[3*HW + idx])
    //        x1 = cx - w / 2, x2 = cx + w / 2, etc.
    //   3. Compute the icon pick and the per-cell score:
    //        score = sigmoid(obj_chan[idx]) * best_icon(...).confidence
    //   4. If score >= score_threshold, push a Detection with the
    //      decoded geometry, the 4 raw kpt channels copied into
    //      `corners` (already in pixel space — the loss in Python
    //      regresses them as absolute pixel coordinates), and the
    //      icon + score.
    //
    // Hint: see model.decode_box on the Python side for the exact same
    // math.
    (void)box_chan;
    (void)kpt_chan;
    (void)cls_chan;
    (void)obj_chan;
    (void)score_threshold;
    return out;
}

// -------------------------------------------------------- NMS

std::vector<Detection> non_max_suppression(std::vector<Detection> candidates,
                                           float iou_threshold) {
    std::vector<Detection> kept;
    kept.reserve(candidates.size());

    // TODO(HW1): class-aware NMS.
    //
    //   1. Sort `candidates` by score, descending.
    //   2. For each detection in order, accept it only if it doesn't
    //      overlap (IoU > iou_threshold) with any already-accepted
    //      detection of the SAME icon class. Use iou_xyxy() above.
    //   3. Push accepted detections into `kept`.
    //
    // Cross-class overlaps are allowed — long-range plates of different
    // icons can legitimately stack in screen space.
    (void)iou_threshold;
    (void)candidates;
    return kept;
}

}  // namespace detector
}  // namespace aiming_hw
