#include "aiming_hw/detector/inferer.hpp"

#include <onnxruntime_cxx_api.h>

#include <array>
#include <cstring>
#include <stdexcept>
#include <vector>

// ONNX-Runtime session set-up + IO binding for the HW1 detector.
//
// The session lifecycle and tensor plumbing is filled — your job is
// the decode + NMS in source/post_process.cpp. The single HW1 blank in
// this file is a one-line dispatch into post_process functions.

namespace aiming_hw {
namespace detector {

struct Inferer::Impl {
    Options options;

    Ort::Env  env;
    Ort::Session session;
    Ort::AllocatorWithDefaultOptions allocator;
    Ort::MemoryInfo memory_info;

    std::vector<std::string> input_names;
    std::vector<std::string> output_names;
    std::vector<const char*> input_name_ptrs;
    std::vector<const char*> output_name_ptrs;

    // Pre-allocated input buffer in NCHW float32, normalized to [0, 1].
    std::vector<float> input_tensor;

    explicit Impl(const Options& opts)
        : options(opts),
          env(ORT_LOGGING_LEVEL_WARNING, "aiming_hw1"),
          session(nullptr),
          memory_info(Ort::MemoryInfo::CreateCpu(OrtArenaAllocator,
                                                 OrtMemTypeDefault)) {
        Ort::SessionOptions session_opts;
        session_opts.SetIntraOpNumThreads(opts.num_threads);
        session_opts.SetGraphOptimizationLevel(
            GraphOptimizationLevel::ORT_ENABLE_BASIC);
        session_opts.SetExecutionMode(ORT_SEQUENTIAL);
        // CUDA EP plumbing intentionally omitted — Stage 3 risk note
        // pins the CPU EP as canonical.
        session = Ort::Session(env, opts.model_path.c_str(), session_opts);

        const std::size_t n_inputs = session.GetInputCount();
        for (std::size_t i = 0; i < n_inputs; ++i) {
            input_names.emplace_back(
                session.GetInputNameAllocated(i, allocator).get());
        }
        const std::size_t n_outputs = session.GetOutputCount();
        for (std::size_t i = 0; i < n_outputs; ++i) {
            output_names.emplace_back(
                session.GetOutputNameAllocated(i, allocator).get());
        }
        for (const auto& name : input_names)  input_name_ptrs.push_back(name.c_str());
        for (const auto& name : output_names) output_name_ptrs.push_back(name.c_str());

        input_tensor.assign(static_cast<std::size_t>(3) *
                            static_cast<std::size_t>(opts.input_height) *
                            static_cast<std::size_t>(opts.input_width),
                            0.0f);
    }
};

Inferer::Inferer(const Options& options)
    : impl_(std::make_unique<Impl>(options)) {}

Inferer::~Inferer() = default;

const Inferer::Options& Inferer::options() const noexcept {
    return impl_->options;
}

namespace {

void bgr_to_chw_normalized(const std::uint8_t* bgr,
                           int height,
                           int width,
                           float* out) {
    // Convert BGR uint8 [H, W, 3] to RGB float32 [3, H, W] / 255.
    const std::size_t plane = static_cast<std::size_t>(height) *
                              static_cast<std::size_t>(width);
    for (int y = 0; y < height; ++y) {
        for (int x = 0; x < width; ++x) {
            const std::size_t src = (y * width + x) * 3;
            const std::size_t pix = static_cast<std::size_t>(y * width + x);
            const float b = static_cast<float>(bgr[src + 0]) / 255.0f;
            const float g = static_cast<float>(bgr[src + 1]) / 255.0f;
            const float r = static_cast<float>(bgr[src + 2]) / 255.0f;
            out[0 * plane + pix] = r;
            out[1 * plane + pix] = g;
            out[2 * plane + pix] = b;
        }
    }
}

}  // namespace

std::vector<Detection> Inferer::run(const std::uint8_t* bgr_frame) {
    if (bgr_frame == nullptr) {
        throw std::invalid_argument("Inferer::run: bgr_frame is null");
    }
    auto& impl = *impl_;
    const auto& opts = impl.options;

    bgr_to_chw_normalized(bgr_frame,
                          opts.input_height,
                          opts.input_width,
                          impl.input_tensor.data());

    const std::array<int64_t, 4> input_shape = {
        1, 3,
        static_cast<int64_t>(opts.input_height),
        static_cast<int64_t>(opts.input_width),
    };
    Ort::Value input_value = Ort::Value::CreateTensor<float>(
        impl.memory_info,
        impl.input_tensor.data(),
        impl.input_tensor.size(),
        input_shape.data(),
        input_shape.size());

    auto outputs = impl.session.Run(
        Ort::RunOptions{nullptr},
        impl.input_name_ptrs.data(), &input_value, 1,
        impl.output_name_ptrs.data(), impl.output_name_ptrs.size());

    if (outputs.empty()) {
        throw std::runtime_error("Inferer::run: ORT session returned no outputs");
    }
    const float* head = outputs[0].GetTensorData<float>();
    auto type_info = outputs[0].GetTensorTypeAndShapeInfo();
    const auto shape = type_info.GetShape();
    if (shape.size() != 4 || shape[0] != 1 || shape[1] != kTotalChan) {
        throw std::runtime_error("Inferer::run: unexpected output shape");
    }
    const int feature_h = static_cast<int>(shape[2]);
    const int feature_w = static_cast<int>(shape[3]);
    const std::size_t head_count = static_cast<std::size_t>(shape[1]) *
                                   static_cast<std::size_t>(shape[2]) *
                                   static_cast<std::size_t>(shape[3]);

    // TODO(HW1): wire the post-processing into the runtime.
    //
    // Once you've implemented decode_head and non_max_suppression in
    // source/post_process.cpp, replace the empty return below with:
    //
    //     auto candidates = decode_head(head, head_count, feature_h,
    //                                   feature_w, opts.score_threshold);
    //     return non_max_suppression(std::move(candidates),
    //                                opts.iou_threshold);
    (void)head;
    (void)head_count;
    (void)feature_h;
    (void)feature_w;
    return {};
}

}  // namespace detector
}  // namespace aiming_hw
