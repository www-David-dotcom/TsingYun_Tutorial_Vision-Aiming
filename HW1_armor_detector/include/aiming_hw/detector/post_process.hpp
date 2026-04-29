#pragma once

// Post-processing for the HW1 detector. Two responsibilities:
//
//   1. Decode the raw [17, H/16, W/16] tensor produced by the ONNX
//      graph into a flat list of `Detection` records (xyxy bboxes,
//      4 corners, icon class, score).
//   2. Run class-aware non-max suppression to deduplicate overlapping
//      detections.
//
// The Detection layout matches what the runner / EKF in HW6 will
// consume; if you change it, downstream contracts break.

#include <array>
#include <cstddef>
#include <cstdint>
#include <vector>

namespace aiming_hw {
namespace detector {

enum class Icon : std::uint8_t {
    Hero = 0,
    Engineer = 1,
    Standard = 2,
    Sentry = 3,
};

struct Detection {
    // xyxy in input-image pixel coordinates.
    float x1, y1, x2, y2;
    // 4 corners in CCW order: top-left, top-right, bottom-right, bottom-left.
    std::array<float, 8> corners;
    Icon  icon;
    float score;          // post-NMS objectness * icon-confidence
};

// Output stride of the ONNX graph (matches src/model.py:STRIDE).
constexpr int kStride   = 16;
// Channel layout: 4 box | 8 kpt | 4 cls | 1 obj.
constexpr int kBoxChan  = 4;
constexpr int kKptChan  = 8;
constexpr int kClsChan  = 4;
constexpr int kObjChan  = 1;
constexpr int kTotalChan = kBoxChan + kKptChan + kClsChan + kObjChan;
// At graph head these many channels — used for raw-tensor strides.

// Decode the raw [kTotalChan, fH, fW] head output into Detections.
//
// `head_data` points at a contiguous float buffer in NCHW layout (C
// fastest-varying after H*W). `feature_h` and `feature_w` are the
// dimensions of the feature map (input_h / kStride and analogous).
// `score_threshold` filters detections whose objectness*icon-conf
// drops below the cutoff.
//
// IMPLEMENT THIS — TODO(HW1).
std::vector<Detection> decode_head(const float* head_data,
                                   std::size_t head_count,
                                   int feature_h,
                                   int feature_w,
                                   float score_threshold);

// Class-aware non-max suppression. Detections of the same icon class
// suppress each other; cross-class overlaps are kept (because four
// plates with different icons can legitimately stack at long range).
//
// `iou_threshold` typically 0.45.
//
// IMPLEMENT THIS — TODO(HW1).
std::vector<Detection> non_max_suppression(std::vector<Detection> candidates,
                                           float iou_threshold);

}  // namespace detector
}  // namespace aiming_hw
