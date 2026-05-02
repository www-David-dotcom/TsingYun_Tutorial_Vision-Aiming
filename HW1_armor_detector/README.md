# HW1 — 装甲板与图标检测器 / Lightweight Armor & Icon Detector

> **Status:** Active as part of the Unity-first assignment path; older
> standalone workflows in this folder are legacy reference.
>
> **Unity-first role:** Detect armor plates and vehicle IDs from Unity camera
> frames.
>
> **Legacy-only:** Synthetic PIL dataset generation is useful for local
> training smoke tests, but final evaluation must use Unity-frame semantics.
>
> **Mini-test:** `uv run pytest HW1_armor_detector/tests/public/test_assign_targets.py HW1_armor_detector/tests/public/test_loss_shapes.py`
>
> **Mini-test files:**
> - `HW1_armor_detector/tests/public/test_assign_targets.py`
> - `HW1_armor_detector/tests/public/test_loss_shapes.py`

> 第一道作业：在合成数据上训练一个轻量级检测器，导出 ONNX，
> 用 ONNX Runtime（C++）跑推理。重点是**多任务损失的写法**和
> **后处理（解码 + NMS）**，不在于追指标。
>
> **Your job:** train a small detector on synthetic frames, export to
> ONNX, run it from C++ via ONNX Runtime. The focus is on writing the
> multi-task loss and the post-processing (decode + NMS) — not on
> chasing mAP.

---

## Student Quickstart

### Prerequisites

- Complete the root [First-time setup](../README.md#first-time-setup).
- Install HW1 Python dependencies with `uv sync --group hw1`.
- C++ inferer tests additionally need the Docker toolchain or a local CMake
  setup with ONNX Runtime.

### What to implement

Fill the `TODO(HW1):` sites in:

- `HW1_armor_detector/src/losses.py`
- `HW1_armor_detector/src/train.py`
- `HW1_armor_detector/source/inferer.cpp`
- `HW1_armor_detector/source/post_process.cpp`

Start with `assign_targets` and the Python loss functions before the C++
post-processing path.

### Mini-test command

```bash
uv run pytest HW1_armor_detector/tests/public/test_assign_targets.py HW1_armor_detector/tests/public/test_loss_shapes.py
```

### Expected first run

Before you fill the blanks, some tests are expected to `xfail` because the
candidate implementation is intentionally missing. After each `TODO(HW1):`
block is filled, the related `xfail` should turn into a pass.

### Before moving on

Run the mini-test command from the repository root. If you also worked on the
C++ post-processing, build inside the Docker toolchain and run
`ctest --preset linux-debug -R hw1`.

## 目标 / Goals

1. **检测**红/蓝四块装甲板的边界框（bbox）+ 4 个角点（用于后续 PnP 解算）。
2. **分类**每块装甲板上的图标：Hero / Engineer / Standard / Sentry。
3. 把 PyTorch 训练好的权重导出为 ONNX，shape-inference 通过。
4. 在 C++ 里加载 ONNX，给定一张 1280×720 BGR 图像，输出
   `[{bbox_xyxy, corners[4], icon, score}, ...]`。

| Step | What's filled | What you write |
|------|---------------|----------------|
| 模型骨干 / Backbone (`src/model.py`) | MobileNetV3-Small + multi-task head | — |
| 损失工具 / Loss utilities (`src/losses.py`) | `giou_loss`, `focal_loss`, `keypoint_l1` | — |
| 训练 / Training (`src/train.py`) | Dataloader, optimizer, training loop | `loss_box`, `loss_kpt`, `loss_cls`, `mixup` |
| ONNX 导出 / Export (`src/export_onnx.py`) | full pipeline (no TODOs) | — |
| C++ 推理器 / Inferer (`source/inferer.cpp`) | ONNX session set-up, IO binding | tensor decode (raw → bbox + corners + icon) |
| 后处理 / NMS (`source/post_process.cpp`) | Header + I/O contracts | `non_max_suppression` |

---

## 数据 / Data

The legacy HW1 workflow uses a synthetic local dataset:

```bash
python data/dataset_dumper.py --frames 200 --out /tmp/ds
```

It draws procedural armor plates against random backgrounds with PIL only,
with no live engine dependency. This is useful for unit-testing your
dataloader and loss while the Unity-first assignment framework is redesigned.

Domain-randomization knobs live in `data/domain_randomization.yaml`.
Real-world holdout frames are pulled lazily via
`shared/scripts/fetch_assets.py --only real-holdout-frames-v1` once
the team uploads the dataset to the public bucket.

---

## 训练 / Training

```bash
uv sync --group hw1
cd HW1_armor_detector

# 1. Generate synthetic training set (default 2000 frames @ 1280x720).
uv run python data/dataset_dumper.py --frames 2000 --out /tmp/ds

# 2. Fill the TODO holes in src/train.py, then:
uv run python src/train.py --epochs 10 --data /tmp/ds --out /tmp/last.pt

# 3. Export to ONNX:
uv run python src/export_onnx.py --weights /tmp/last.pt --out /tmp/model.onnx
```

`src/train.py` exposes four `# TODO(HW1):` sites. Each one is a single
expression that combines provided utilities from `src/losses.py`. If
your gradients explode or your validation IOU stops improving, that's
where to look first.

---

## 推理 / Inference

```bash
# C++ build (uses the toolchain Docker image from Stage 1):
docker compose -f shared/docker/toolchain.compose.yaml run --rm dev \
    bash -c "cmake --preset linux-debug && cmake --build --preset linux-debug \
             --target hw1_inferer_smoke"

build/HW1_armor_detector/hw1_inferer_smoke \
    --model /tmp/model.onnx --frame /tmp/ds/000000.png
```

`source/inferer.cpp` and `source/post_process.cpp` each have one
`// TODO(HW1):` block. The session set-up, IO binding, and output-tensor
plumbing are filled — your job is the math (raw tensor → boxes; boxes →
deduplicated detections).

---

## 验收 / Acceptance

| Check | How |
|-------|-----|
| `train.py` loss decreases monotonically over 5 epochs (smoke level) | `uv run python src/train.py --epochs 5 --data /tmp/ds --check-monotone` |
| ONNX file passes `onnx.checker.check_model` | included in `src/export_onnx.py` |
| Public unit tests pass | `uv run pytest HW1_armor_detector/tests/public/` |
| C++ inferer round-trips a synthetic frame | `ctest --preset linux-debug -R hw1` |

A "good" submission converges to roughly 0.6 IOU on the synthetic
validation split inside 10 epochs. The exact mAP isn't graded —
correctness of the loss formulation and post-processing is.

---

## 已知非范围 / Out of scope

* TensorRT engine generation (production team's concern).
* Quantization / distillation (could become a future bonus).
* CUDA EP for inferer (CPU EP is canonical; CUDA path is not gated by
  this assignment).
* Hidden grading episodes — the grading workflow must be redesigned from
  `schema.md`.
