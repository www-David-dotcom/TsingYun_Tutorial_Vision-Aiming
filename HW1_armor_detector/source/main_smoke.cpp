// Tiny CLI that wraps Inferer + post_process for a single-frame
// sanity check. Builds as `hw1_inferer_smoke` per the HW1 README.
//
// Usage:
//     hw1_inferer_smoke --model /tmp/model.onnx --frame /tmp/000000.png
//
// The frame loader is intentionally trivial: it expects a raw RGB888
// `.bin` (1280*720*3 bytes) when --raw is passed, otherwise it
// expects a 1280x720 PPM (P6) which `convert frame.png frame.ppm`
// produces. We deliberately skip libpng / opencv dependencies; the
// real consumer of Inferer is HW6's runner which gets frames from the
// arena's TCP stream in raw RGB888 already.

#include "aiming_hw/detector/inferer.hpp"
#include "aiming_hw/detector/post_process.hpp"

#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <iostream>
#include <string>
#include <vector>

namespace {

bool load_raw_rgb(const std::string& path,
                  std::size_t expected_bytes,
                  std::vector<std::uint8_t>& out) {
    std::ifstream f(path, std::ios::binary);
    if (!f) return false;
    out.resize(expected_bytes);
    f.read(reinterpret_cast<char*>(out.data()), expected_bytes);
    return f.gcount() == static_cast<std::streamsize>(expected_bytes);
}

bool load_ppm_p6(const std::string& path,
                 int expected_w,
                 int expected_h,
                 std::vector<std::uint8_t>& bgr_out) {
    std::ifstream f(path, std::ios::binary);
    if (!f) return false;
    std::string magic;
    int w = 0, h = 0, max_val = 0;
    f >> magic >> w >> h >> max_val;
    f.get();  // consume the whitespace before raw bytes.
    if (magic != "P6" || w != expected_w || h != expected_h || max_val != 255) {
        return false;
    }
    std::vector<std::uint8_t> rgb(static_cast<std::size_t>(w * h * 3));
    f.read(reinterpret_cast<char*>(rgb.data()), rgb.size());
    bgr_out.resize(rgb.size());
    for (std::size_t i = 0; i < rgb.size(); i += 3) {
        bgr_out[i + 0] = rgb[i + 2];
        bgr_out[i + 1] = rgb[i + 1];
        bgr_out[i + 2] = rgb[i + 0];
    }
    return true;
}

}  // namespace

int main(int argc, char** argv) {
    using namespace aiming_hw::detector;

    Inferer::Options opts;
    std::string frame_path;
    bool raw = false;
    for (int i = 1; i < argc; ++i) {
        std::string a = argv[i];
        if (a == "--model" && i + 1 < argc) {
            opts.model_path = argv[++i];
        } else if (a == "--frame" && i + 1 < argc) {
            frame_path = argv[++i];
        } else if (a == "--raw") {
            raw = true;
        } else if (a == "--help" || a == "-h") {
            std::printf("usage: %s --model PATH --frame PATH [--raw]\n", argv[0]);
            return 0;
        } else {
            std::fprintf(stderr, "unrecognised argument: %s\n", a.c_str());
            return 2;
        }
    }
    if (opts.model_path.empty() || frame_path.empty()) {
        std::fprintf(stderr, "--model and --frame are required\n");
        return 2;
    }

    std::vector<std::uint8_t> bgr;
    const std::size_t expected = static_cast<std::size_t>(opts.input_height) *
                                  static_cast<std::size_t>(opts.input_width) * 3;
    bool ok = raw
        ? load_raw_rgb(frame_path, expected, bgr)
        : load_ppm_p6(frame_path, opts.input_width, opts.input_height, bgr);
    if (!ok) {
        std::fprintf(stderr, "failed to load frame %s\n", frame_path.c_str());
        return 1;
    }

    Inferer inferer(opts);
    auto detections = inferer.run(bgr.data());
    std::printf("got %zu detections\n", detections.size());
    for (const auto& d : detections) {
        std::printf("  icon=%u score=%.3f bbox=(%.1f,%.1f,%.1f,%.1f)\n",
                    static_cast<unsigned>(d.icon), d.score,
                    d.x1, d.y1, d.x2, d.y2);
    }
    return 0;
}
