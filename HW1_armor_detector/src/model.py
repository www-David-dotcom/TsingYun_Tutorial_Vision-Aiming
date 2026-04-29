"""HW1 detector — MobileNetV3-Small backbone + anchor-free multi-task head.

Filled, no TODOs. Candidates use this module unmodified; the
TODO-bearing surface is in src/train.py (loss formulation) and the C++
post-processing in source/inferer.cpp + source/post_process.cpp
(decode + NMS).

Design summary:
- Backbone: torchvision's MobileNetV3-Small at stride 16 (last conv
  block). Pretrained ImageNet weights when available (default), random
  init otherwise.
- Head: shared 3x3 conv neck, four 1x1 prediction heads (box, kpt,
  cls, obj).
- Output stride: 16. For a 1280x720 input the prediction map is 80x45
  with C = 4 (box) + 8 (kpt) + 4 (cls) + 1 (obj) = 17 channels per
  cell.
- Box parameterization: center offsets within the cell + log-scale
  width/height (a-la FCOS). Decoder lives in C++ on the inferer side
  and in losses.py:decode_box for the training loop.
"""

from __future__ import annotations

from dataclasses import dataclass

import torch
from torch import Tensor, nn
from torchvision.models import MobileNet_V3_Small_Weights, mobilenet_v3_small

# Output channel counts. Kept as named constants because the C++ side
# decodes by stride into the same layout.
N_BOX = 4
N_KPT = 8
N_CLS = 4
N_OBJ = 1
TOTAL_C = N_BOX + N_KPT + N_CLS + N_OBJ
STRIDE = 16


@dataclass
class DetectorOutput:
    box: Tensor   # [B, 4, H, W]
    kpt: Tensor   # [B, 8, H, W]
    cls: Tensor   # [B, 4, H, W] — raw logits
    obj: Tensor   # [B, 1, H, W] — raw logits

    def to_flat(self) -> Tensor:
        """Concatenate into the C-major layout the ONNX export uses
        (and the C++ inferer decodes from)."""
        return torch.cat([self.box, self.kpt, self.cls, self.obj], dim=1)


def _build_backbone(pretrained: bool) -> tuple[nn.Module, int]:
    """Returns the MobileNetV3-Small feature extractor up to stride 16
    plus its output channel count.

    The full MobileNetV3-Small ends at stride 32 — we slice the
    `.features` list to stop one stage earlier, where the channel
    count is 96.
    """
    weights = MobileNet_V3_Small_Weights.IMAGENET1K_V1 if pretrained else None
    full = mobilenet_v3_small(weights=weights)
    # Keep features [0..11] which lands at the 96-channel stride-16
    # block; stages [12..15] downsample further to stride 32 and we
    # don't need them.
    backbone = nn.Sequential(*list(full.features[:12]))
    return backbone, 96


class DetectorHead(nn.Module):
    """Shared 3x3 neck + four 1x1 prediction heads."""

    def __init__(self, in_channels: int, hidden: int = 128) -> None:
        super().__init__()
        self.neck = nn.Sequential(
            nn.Conv2d(in_channels, hidden, kernel_size=3, padding=1, bias=False),
            nn.BatchNorm2d(hidden),
            nn.ReLU(inplace=True),
            nn.Conv2d(hidden, hidden, kernel_size=3, padding=1, bias=False),
            nn.BatchNorm2d(hidden),
            nn.ReLU(inplace=True),
        )
        self.head_box = nn.Conv2d(hidden, N_BOX, kernel_size=1)
        self.head_kpt = nn.Conv2d(hidden, N_KPT, kernel_size=1)
        self.head_cls = nn.Conv2d(hidden, N_CLS, kernel_size=1)
        self.head_obj = nn.Conv2d(hidden, N_OBJ, kernel_size=1)
        # Bias the objectness head so initial sigmoid ~ 0.01, the FCOS
        # default for stable training.
        nn.init.constant_(self.head_obj.bias, -4.6)

    def forward(self, feat: Tensor) -> DetectorOutput:
        z = self.neck(feat)
        return DetectorOutput(
            box=self.head_box(z),
            kpt=self.head_kpt(z),
            cls=self.head_cls(z),
            obj=self.head_obj(z),
        )


class ArmorDetector(nn.Module):
    def __init__(self, pretrained_backbone: bool = True) -> None:
        super().__init__()
        self.backbone, c = _build_backbone(pretrained_backbone)
        self.head = DetectorHead(c)

    def forward(self, image: Tensor) -> DetectorOutput:
        feat = self.backbone(image)
        return self.head(feat)


def make_grid(h: int, w: int, device: torch.device | str = "cpu") -> Tensor:
    """[H, W, 2] float tensor of (x, y) cell centres in pixel space."""
    ys, xs = torch.meshgrid(
        torch.arange(h, device=device, dtype=torch.float32),
        torch.arange(w, device=device, dtype=torch.float32),
        indexing="ij",
    )
    centres = torch.stack([xs, ys], dim=-1) + 0.5
    return centres * STRIDE


def decode_box(pred_box: Tensor) -> Tensor:
    """Convert the raw [B, 4, H, W] head output to xyxy boxes in pixel
    coordinates.

    Box format on output of the head:
        - channels 0,1 = center offsets (sigmoid-bound to [0, 1] cell)
        - channels 2,3 = log scale of width/height (in pixels)
    """
    _, _, h, w = pred_box.shape
    grid = make_grid(h, w, device=pred_box.device).reshape(1, h, w, 2).permute(0, 3, 1, 2)
    cx = grid[:, 0:1] + (pred_box[:, 0:1].sigmoid() - 0.5) * STRIDE
    cy = grid[:, 1:2] + (pred_box[:, 1:2].sigmoid() - 0.5) * STRIDE
    w_box = pred_box[:, 2:3].exp().clamp(max=STRIDE * 16)
    h_box = pred_box[:, 3:4].exp().clamp(max=STRIDE * 16)
    x1 = cx - w_box / 2
    y1 = cy - h_box / 2
    x2 = cx + w_box / 2
    y2 = cy + h_box / 2
    return torch.cat([x1, y1, x2, y2], dim=1)
