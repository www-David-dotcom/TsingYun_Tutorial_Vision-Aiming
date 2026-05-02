// HW6 hw_runner CLI entry point.
//
// Usage:
//     hw_runner --episode-seed 42 --bot silver
//
// The candidate fills `run_episode` below — specifically the thread
// layout (which threads do which work, where the ZMQ subscriber
// lives, how detector / EKF / ballistic / controller fit together).
// The Runner class (header + .cpp) provides the shared state and
// stale-frame drop policy.
//
// Until the HW6 blank is filled, `run_episode` only spins up the runner
// + watchdog, prints stats every second, and exits after the episode
// duration. That's enough to verify the binary links, the watchdog
// fires when the loop hangs, and the ring buffer's atomics behave
// under TSan.

#include <atomic>
#include <chrono>
#include <cstdint>
#include <cstdio>
#include <stdexcept>
#include <string>
#include <thread>

#include "aiming_hw/pipeline/runner.hpp"

namespace {

struct CliArgs {
    uint64_t   seed         = 0;
    std::string bot         = "bronze";
    double     duration_s   = 90.0;
    bool       use_mpc      = false;
    std::string control_host = "127.0.0.1";
    int        control_port = 7654;
    int        frame_port   = 7655;
};

CliArgs parse_args(int argc, char** argv) {
    CliArgs args;
    for (int i = 1; i < argc; ++i) {
        const std::string a = argv[i];
        const auto take = [&]() -> std::string {
            if (i + 1 >= argc) {
                throw std::runtime_error("missing value for " + a);
            }
            return argv[++i];
        };
        if      (a == "--episode-seed")  args.seed = std::stoull(take());
        else if (a == "--bot")           args.bot = take();
        else if (a == "--duration")      args.duration_s = std::stod(take());
        else if (a == "--mpc")           args.use_mpc = true;
        else if (a == "--host")          args.control_host = take();
        else if (a == "--control-port")  args.control_port = std::stoi(take());
        else if (a == "--frame-port")    args.frame_port = std::stoi(take());
        else if (a == "-h" || a == "--help") {
            std::printf("usage: hw_runner [--episode-seed N] [--bot bronze|silver|gold]\n"
                        "                 [--duration SECONDS] [--mpc]\n"
                        "                 [--host H] [--control-port P] [--frame-port P]\n");
            std::exit(0);
        } else {
            throw std::runtime_error("unrecognised argument: " + a);
        }
    }
    return args;
}

int run_episode(const CliArgs& args) {
    aiming_hw::pipeline::RunnerConfig cfg;
    cfg.use_mpc = args.use_mpc;
    aiming_hw::pipeline::Runner runner(cfg);

    // TODO(HW6): pick the thread layout.
    //
    // Stage-8 plan calls for at least three threads:
    //   1. Frame subscriber  — reads RGB888 chunks off the arena's
    //      TCP/ZMQ frame port; pushes Frame{} into runner via
    //      push_frame.
    //   2. gRPC client       — calls EnvReset / EnvStep / EnvPushFire
    //      / EnvFinish on the simulator. Owns the SensorBundle stream
    //      and forwards gimbal poses to runner via publish_gimbal.
    //   3. Control loop      — at cfg.control_rate_hz, pops the
    //      freshest frame via runner.next_frame, runs the pipeline
    //      (HW1 detector → HW3 tracker → HW4 ballistic → HW5
    //      controller), and writes a GimbalCmd back to the gRPC
    //      thread's send queue.
    //
    // Trade-offs to weigh:
    //   * Combining threads 1+2 simplifies cancellation but the gRPC
    //     stream's blocking semantics interfere with the ZMQ
    //     subscriber's recv(); separate threads avoid head-of-line
    //     blocking.
    //   * The control loop should pin to a single core (sched_setaffinity)
    //     to keep p95 latency under 25 ms. Other threads pin elsewhere.
    //   * The watchdog's expiry callback must be lock-free (it runs
    //     on the watchdog's polling thread); zeroing torque commands
    //     via atomic stores is safe.
    //
    // Until the HW6 blank is filled, we simulate the loop here in the main
    // thread and report stats — useful for the SPSC + watchdog tests
    // but not a real episode.
    std::atomic<bool> stop{false};
    std::thread stats_thread([&] {
        const auto start = std::chrono::steady_clock::now();
        while (!stop.load(std::memory_order_acquire)) {
            std::this_thread::sleep_for(std::chrono::seconds(1));
            const auto s = runner.stats();
            const auto elapsed_s = std::chrono::duration<double>(
                std::chrono::steady_clock::now() - start).count();
            std::printf("[%5.1fs] received=%llu dropped=%llu consumed=%llu "
                        "iter=%llu p95=%.1fms wd_expired=%d\n",
                        elapsed_s,
                        (unsigned long long)s.frames_received,
                        (unsigned long long)s.frames_dropped,
                        (unsigned long long)s.frames_consumed,
                        (unsigned long long)s.loop_iterations,
                        s.loop_latency_p95_ns / 1e6,
                        runner.watchdog_expired() ? 1 : 0);
        }
    });

    std::this_thread::sleep_for(
        std::chrono::duration<double>(args.duration_s));
    stop.store(true, std::memory_order_release);
    stats_thread.join();

    std::printf("\nepisode finished — seed=%llu bot=%s use_mpc=%d\n",
                (unsigned long long)args.seed,
                args.bot.c_str(),
                args.use_mpc ? 1 : 0);
    return 0;
}

}  // namespace

int main(int argc, char** argv) {
    try {
        return run_episode(parse_args(argc, argv));
    } catch (const std::exception& e) {
        std::fprintf(stderr, "hw_runner: %s\n", e.what());
        return 1;
    }
}
