# TsingYun Tutorial — Vision Aiming HW

> 青云战队视觉组招新作业模板。Recruitment-cycle assignment template for the
> TsingYun RoboMaster Vision Group.
>
> **Stage 1 baseline.** Per-HW directories (HW1–HW7) land in later stages
> per [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md).

---

## 如何完成作业 (For candidates)

1. **Fork** 本仓库到自己的 GitHub 账号下。
2. **Clone** 你 Fork 的仓库到本地。
3. 在本地完成作业（HW1 起，按顺序），提交并推送到你 Fork 的仓库。
4. 在 GitHub 上向原仓库提交 Pull Request，等待回复。

### 作业要求

1. Pull Request 标题请改为 `姓名 - 学号`，需要在 description 中提交以下信息：
   - 代码功能展示
   - 遇到的问题与反馈（如果有）
2. 请注意 Git 提交规范，保持提交记录清晰。
3. **不要上传敏感信息**，如 API 密钥、密码等（OSS 凭据通过环境变量提供，
   见 [`docs/oss_assets.md`](docs/oss_assets.md)）。

> 评分流程与排行榜机制目前**暂未确定**——见 [`schema.md`](schema.md) §7。
> 当前阶段专注于把作业本身设计好；评分细则将在 HW 脚手架完成之后另行设计。

---

## What's here (English overview)

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
[`schema.md`](schema.md) §7. This repo currently focuses on getting the
homework scaffolds right.

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
    bash -c "cmake --preset linux-debug && cmake --build --preset linux-debug && ctest --preset linux-debug"
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

---

<div align="center"><b>👋 欢迎线下线上的交流讨论</b></div>

## License

MIT.
