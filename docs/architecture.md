# Architecture overview

This is a Unity-only render of `schema.md` with implementation pointers
attached. `schema.md` remains the source of truth; this file is only a map
from the spec to code.

## High-level diagram

```
┌────────────────────────────────────┐         gRPC (control)         ┌────────────────────────────────────┐
│ Aiming Arena (Unity runtime)       │ <───────────────────────────── │ Candidate's C++ stack (`hw_runner`) │
│ - physics, rendering, scoring      │                                │ - detector (HW1)                   │
│ - frozen RL opponents              │ ─── ZMQ PUB (image frames) ──> │ - PnP (provided)                   │
│ - replay recorder                  │                                │ - tf graph (HW2)                   │
│                                    │ ─── ZMQ PUB (chassis IMU) ───> │ - EKF tracker (HW3)                │
│                                    │ <── ZMQ SUB (gimbal cmd) ───── │ - ballistic + delay (HW4)          │
│                                    │ <── ZMQ SUB (fire/ack)  ────── │ - MPC controller (HW5)             │
└────────────────────────────────────┘                                │ - integration / strategy (HW7)     │
              ▲                                                       └────────────────────────────────────┘
              │ same gRPC contract                                                          ▲
              │ (used during training only)                                                 │ shared protobuf
              │                                                                             │
              │                          ┌───────────────────────────────────────────────────┘
              │                          │
              │                          ▼
┌────────────────────────────────────────────────────┐
│ Python tooling (`hw_pyrunner`)                     │
│ - dataset generation (random arena, label dump)    │
│ - model training & ONNX export (PyTorch + uv)      │
│ - RL bot training (Sample Factory + PettingZoo)    │
│ - episode driver                                   │
└────────────────────────────────────────────────────┘
```

## Active Unity transport

The current Unity runtime exposes length-prefixed TCP JSON control on port
`7654` and raw RGB frame TCP on port `7655`. See
[`docs/unity-wire-contract.md`](unity-wire-contract.md). The older gRPC/ZMQ
language below remains architecture intent and generated-proto context, not the
current local Unity transport implementation.

## What stage 1 ships of this picture

| Component | Status | Code |
|---|---|---|
| gRPC service definition | ✅ | [`shared/proto/aiming.proto`](../shared/proto/aiming.proto) |
| Sensor / cmd types | ✅ | [`shared/proto/sensor.proto`](../shared/proto/sensor.proto) |
| Episode telemetry | ✅ | [`shared/proto/episode.proto`](../shared/proto/episode.proto) |
| Stand-in gRPC server | legacy support | [`shared/grpc_stub_server/`](../shared/grpc_stub_server/) |
| Stand-in ZMQ frame stream | ✅ | [`shared/zmq_frame_pub/`](../shared/zmq_frame_pub/) |
| Unity arena | active | [`shared/unity_arena/`](../shared/unity_arena/) |
| Candidate C++ pipeline (`hw_runner`) | Stage 3+ | one HW at a time |
| Reference toolchain image | ✅ | [`shared/docker/toolchain.Dockerfile`](../shared/docker/toolchain.Dockerfile) |
| OSS asset distribution | ✅ | [`shared/scripts/fetch_assets.py`](../shared/scripts/fetch_assets.py) |

## Wire-format crash course

A typical episode's data flow looks like:

```
candidate:                 simulator:
   ──> EnvReset(seed=42, opponent_tier="silver")
                                ──> InitialState{ bundle, zmq_frame_endpoint }
   <subscribes to ZMQ endpoint>

   <── frame.0 (header + 1280×720 RGB) ── (60 Hz)
       ── gimbal_cmd ──>            (per tick)
   ──> EnvStep(stream of GimbalCmd)
                                ──> stream of SensorBundle

   ──> EnvPushFire(burst_count=1)
                                ──> FireResult{accepted, queued_count}

   ... 5 minutes of episode ...

   ──> EnvFinish(flush_replay=True)
                                ──> EpisodeStats{ outcome, damage, events, ... }
```

Two transports run in parallel because their cost profiles differ:

* **gRPC** for typed RPCs (reset / step / fire / finish). Streaming
  bidirectional `EnvStep` lets the candidate hand the server one
  command per simulator tick. Easy to reason about, comfortable with
  ~100 RPCs/sec; bad fit for raw 720p frames at 60 fps (~190 MB/s).
* **ZMQ PUB/SUB** for image frames. 16-byte in-band header (frame_id,
  stamp_ns) keeps PUB/SUB drops aligned. On Linux/macOS we use
  `ipc://` for ~25% throughput gain; on Windows we fall back to
  `tcp://127.0.0.1:port`.

## Determinism

Every episode is deterministic given:

1. The 64-bit seed passed to `EnvReset`
2. The opponent policy `.pt` SHA (manifest entry)
3. The simulator binary SHA (returned in `InitialState`)
4. The candidate's compiled binary SHA-256

`EpisodeStats` carries all four so any episode is reproducible end-to-end.
This matters for whatever grading policy we eventually ship (§7) but is
useful even now: any test failure can be reproduced bit-for-bit by replay
of the seed.

## Deferred (stage 2+ concerns)

* Unity game-rule completion
* Frozen RL opponent training (Sample Factory + PettingZoo)
* Per-HW grading episodes
