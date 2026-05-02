# TsingYun Aiming Arena

Unity-based RoboMaster self-aiming game and recruitment assignment workspace
for the TsingYun Vision Group.

[`schema.md`](schema.md) is the highest-priority specification. The active
candidate assignment path is the Unity-first A1-A7 sequence in
[`docs/assignment-redesign.md`](docs/assignment-redesign.md).

---

## Current Focus

1. Preserve local Unity runtime functionality.
2. Repair code and docs to match `schema.md`.
3. Keep HW1-HW7 aligned with the Unity-first A1-A7 candidate sequence.
4. Implement the game rules, RL training loop, training-ground scene, and visual
   polish after cleanup.

## Who this is for

This repository is for candidates who are building a self-aiming RoboMaster
runner one stage at a time. You do not need to understand Unity on day one.
Start with the current assignment folder, fill only the `TODO(HWn):` blanks,
run that stage's mini-tests, then move to the next stage.

## Install prerequisites

- Git.
- Python 3.11 or newer.
- `uv` for Python dependencies: <https://docs.astral.sh/uv/getting-started/installation/>.
- Docker Desktop or Docker Engine for the canonical C++ toolchain.
- Unity 6 LTS only when you reach A6 runner integration or want to smoke the
  live arena.

Native C++ builds also need CMake and Ninja. The Docker image is the supported
student path because it already includes Eigen 3.4, gRPC, Protobuf, and the
compiler version used by the mini-tests.

## First-time setup

```bash
git clone <your-fork-url>
cd Aiming_HW

# Python workspace and no-Unity checks.
uv sync
uv run pytest tests/test_assignment_design.py tests/test_assignment_mini_commands.py -q

# Canonical C++ environment.
docker compose -f shared/docker/toolchain.compose.yaml run --rm dev
# Inside the container, from /workspace:
cmake --preset linux-debug
cmake --build --preset linux-debug
ctest --preset linux-debug
```

On macOS you may use `cmake --preset macos-debug` and
`cmake --build --preset macos-debug`, but some C++ stages can be skipped if
your local package manager provides Eigen 5 instead of Eigen 3.4. Docker avoids
that mismatch.

## Assignment workflow

1. Open [`docs/assignment-redesign.md`](docs/assignment-redesign.md) and find
   your active stage A1-A7.
2. Open that HW folder's README and read its `Student Quickstart`.
3. Fill only the `TODO(HWn):` sites named by the README.
4. Run the stage mini-test command before touching the next stage.
5. Use the Unity smoke test only after A6 has a runner that can talk to the
   arena.

## Mini-test quick reference

| Stage | Folder | Mini-test command |
|---|---|---|
| A1 | `HW1_armor_detector` | `uv run pytest HW1_armor_detector/tests/public/test_assign_targets.py HW1_armor_detector/tests/public/test_loss_shapes.py` |
| A2 | `HW2_tf_graph` | `ctest --preset linux-debug -R hw2` |
| A3 | `HW3_ekf_tracker` | `ctest --preset linux-debug -R hw3` |
| A4 | `HW4_ballistic` | `ctest --preset linux-debug -R hw4` |
| A5 | `HW5_mpc_gimbal` | `ctest --preset linux-debug -R hw5` and `uv run pytest HW5_mpc_gimbal/tests/public/test_cost.py` |
| A6 | `HW6_integration` | `ctest --preset linux-debug -R hw6` |
| A7 | `HW7_strategy` | `ctest --preset linux-debug -R hw7` |

For C++ commands, run `cmake --preset linux-debug` and
`cmake --build --preset linux-debug` first inside the Docker toolchain
container.

## Unity Quickstart

```bash
# 1. Open the Unity project in Unity 6 LTS.
#    Project path: shared/unity_arena

# 2. In Unity, open:
#    Assets/Scenes/MapA_MazeHybrid.unity

# 3. Enter Play mode, then smoke the local control surface:
UV_CACHE_DIR=.uv-cache uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 10
```

Unity publishes:

- Control RPC: `tcp://127.0.0.1:7654`
- RGB frame stream: `tcp://127.0.0.1:7655`

## Local Checks

No-Unity checks:

```bash
UV_CACHE_DIR=.uv-cache uv run pytest tests/test_arena_wire_format.py -q
```

Stage mini-test C++ checks:

```bash
cmake --preset linux-debug
cmake --build --preset linux-debug
ctest --preset linux-debug
```

## Troubleshooting

- `ctest --preset linux-debug` says no build directory exists: run
  `cmake --preset linux-debug` and `cmake --build --preset linux-debug` first.
- Eigen, gRPC, or Protobuf is missing on your host: use the Docker toolchain.
- Python tests skip because `torch`, `casadi`, or `scipy` is missing: install
  the stage group, for example `uv sync --group hw1`, `uv sync --group hw3`,
  `uv sync --group hw5`, or `uv sync --group hw7`.
- Unity smoke cannot connect: open `shared/unity_arena` in Unity 6 LTS, load
  `Assets/Scenes/MapA_MazeHybrid.unity`, and enter Play mode before running
  `tools/scripts/smoke_arena.py`.

## Candidate Assignments

The active assignment design is now documented in
[`docs/assignment-redesign.md`](docs/assignment-redesign.md). HW1-HW7 remain
as implementation/reference folders, but candidates should follow the
Unity-first A1-A7 path and use each stage's mini-tests for partial progress.

## Repo Layout


```
Aiming_HW/
├── CMakeLists.txt            # top-level
├── pyproject.toml            # uv workspace root
├── .clang-format / .editorconfig
├── schema.md
├── docs/
│   ├── architecture.md
│   └── oss_assets.md
├── shared/
│   ├── proto/                # aiming.proto, sensor.proto, episode.proto
│   ├── cmake/                # ProtoTargets, UvFetch
│   ├── assets/manifest.toml  # OSS-hosted asset manifest
│   ├── scripts/              # fetch_assets.py, push_assets.py
│   ├── unity_arena/          # Unity 6 LTS game project
│   ├── grpc_stub_server/     # legacy proto tooling support
│   ├── zmq_frame_pub/        # synthetic 720p RGB stream
│   └── docker/               # reference toolchain image
├── HW1_armor_detector/ ... HW7_strategy/
│                              # Unity-first A1-A7 assignment modules
└── tests/
    ├── proto_roundtrip_test.cpp
    ├── test_arena_wire_format.py
    └── test_fetch_assets.py
```

## License

MIT.
