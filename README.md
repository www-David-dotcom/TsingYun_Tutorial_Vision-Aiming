# TsingYun Tutorial — Vision Aiming HW

> 青云战队视觉组招新作业模板。Recruitment-cycle assignment template for the
> TsingYun RoboMaster Vision Group.
>
> Stage 1 baseline. Per-HW directories (HW1–HW7) land in later stages.

## What's here

* [`schema.md`](schema.md) — assignment design (scenario, simulator,
  per-HW deep-dives, toolchain, roadmap).
* [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) — per-stage delivery
  plan with acceptance criteria.
* [`shared/`](shared/) — common infrastructure that every HW reuses:
  protobuf wire format, CMake helpers, OSS asset tooling, reference
  Docker image, gRPC + ZMQ stub servers.
* [`tests/`](tests/) — round-trip tests on the wire format, smoke tests
  on the asset resolver.
* [`docs/`](docs/) — CHANGELOG, architecture overview, OSS access
  instructions.

Grading workflow and leaderboard are deliberately deferred — see
`schema.md` §7. This repo currently focuses on getting the homework
scaffolds right.

## Quickstart (for the team / TAs)

```bash
# 1. Install uv (once)
curl -LsSf https://astral.sh/uv/install.sh | sh

# 2. Sync the Python workspace
uv sync

# 3. Smoke the gRPC stub server + ZMQ frame publisher
uv run aiming-stub-server --once &
uv run aiming-frame-pub --max-frames 60

# 4. Pull the reference toolchain image (multi-arch amd64+arm64)
docker pull tsingyun-aiming-hw-cache.oss-cn-beijing.aliyuncs.com/docker/toolchain/0.5.0:latest

# 5. Configure + build C++ inside the image
docker compose -f shared/docker/toolchain.compose.yaml run --rm dev \
    bash -c "cmake -B build -G Ninja && cmake --build build && ctest --test-dir build"
```

## Quickstart (for candidates — preview)

Once HW1+ stages land, the candidate flow will be:

```bash
git clone <your-fork-url>
cd TsingYun_Tutorial_Vision-Aiming
uv sync
uv run python shared/scripts/fetch_assets.py        # pull Godot + datasets
docker compose -f shared/docker/toolchain.compose.yaml run --rm dev \
    bash -c "cmake -B build && cmake --build build"

# work on HW1, run the public unit tests
cd HW1_armor_detector && pytest tests/public/
```

## Repo layout (current)

```
Aiming_HW/
├── CMakeLists.txt            # top-level
├── pyproject.toml            # uv workspace root
├── .clang-format / .editorconfig
├── schema.md
├── IMPLEMENTATION_PLAN.md
├── docs/
│   ├── CHANGELOG.md
│   ├── architecture.md
│   └── oss_assets.md
├── shared/
│   ├── proto/                # aiming.proto, sensor.proto, episode.proto
│   ├── cmake/                # ProtoTargets, UvFetch
│   ├── assets/manifest.toml  # OSS-hosted asset manifest
│   ├── scripts/              # fetch_assets.py, push_assets.py
│   ├── grpc_stub_server/     # Python stand-in for the Godot arena
│   ├── zmq_frame_pub/        # synthetic 720p RGB stream
│   └── docker/               # reference toolchain image
└── tests/
    ├── proto_roundtrip_test.cpp
    └── test_fetch_assets.py
```

HW1–HW7 directories appear in later stages per
[`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md).

## License

MIT.
