#pragma once

// ONNX Runtime inferer for the HW1 detector.
//
// Loads a `.onnx` produced by src/export_onnx.py, runs forward on a
// 1280x720 BGR frame (the camera format from HW6's runner), and
// returns Detection records via post_process::decode_head +
// non_max_suppression.
//
// The session is set up with the CPU EP as the canonical target
// (see IMPLEMENTATION_PLAN.md Stage 3 risk note); CUDA EP is left
// as a future extension via `Inferer::Options::use_cuda`.

#include <cstdint>
#include <memory>
#include <string>
#include <vector>

#include "aiming_hw/detector/post_process.hpp"

namespace aiming_hw {
namespace detector {

class Inferer {
public:
    struct Options {
        std::string model_path;
        int  input_height       = 720;
        int  input_width        = 1280;
        float score_threshold   = 0.20f;
        float iou_threshold     = 0.45f;
        int  num_threads        = 2;
        bool use_cuda           = false;       // not wired in Stage 3
    };

    explicit Inferer(const Options& options);
    ~Inferer();

    // Run the model on a contiguous BGR frame of size
    // (Options::input_height, Options::input_width, 3). Returns a
    // post-NMS detection list.
    std::vector<Detection> run(const std::uint8_t* bgr_frame);

    // Convenience accessor for tests.
    const Options& options() const noexcept;

private:
    struct Impl;
    std::unique_ptr<Impl> impl_;
};

}  // namespace detector
}  // namespace aiming_hw
