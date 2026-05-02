"""Direct unit test for `losses.assign_targets`.

Pins the FCOS-style positive-cell assignment rule the candidate
fills in `src/losses.py`. Skips when the stub is detected (cell_valid
all-zeros) so the rest of the suite stays green during stage close.
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

torch = pytest.importorskip("torch")

REPO_ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(REPO_ROOT / "HW1_armor_detector" / "src"))

from losses import assign_targets  # noqa: E402
from model import STRIDE  # noqa: E402


def _build_grid(h: int, w: int, stride: int = STRIDE) -> torch.Tensor:
    ys, xs = torch.meshgrid(
        torch.arange(h, dtype=torch.float32),
        torch.arange(w, dtype=torch.float32),
        indexing="ij",
    )
    centres = torch.stack([xs, ys], dim=-1) + 0.5
    return centres * stride


def _is_stub(grid: torch.Tensor) -> bool:
    boxes = torch.tensor([[40.0, 40.0, 120.0, 120.0]])
    corners = torch.tensor([[40.0, 40.0, 120.0, 40.0, 120.0, 120.0, 40.0, 120.0]])
    icons = torch.tensor([2])
    out = assign_targets(grid, boxes, corners, icons, STRIDE)
    return float(out["cell_valid"].sum().item()) == 0.0


def test_empty_box_list_returns_zeros() -> None:
    grid = _build_grid(8, 8)
    out = assign_targets(grid, torch.zeros((0, 4)), torch.zeros((0, 8)),
                         torch.zeros((0,), dtype=torch.long), STRIDE)
    assert out["cell_valid"].sum().item() == 0
    assert out["cell_obj"].sum().item() == 0
    assert (out["cell_icon"] == -1).all()


def test_single_box_assigns_inside_cells() -> None:
    grid = _build_grid(20, 20)
    if _is_stub(grid):
        pytest.xfail("assign_targets unimplemented - fill TODO(HW1): in losses.py")
    boxes = torch.tensor([[40.0, 60.0, 120.0, 140.0]])
    corners = torch.tensor([[40.0, 60.0, 120.0, 60.0, 120.0, 140.0, 40.0, 140.0]])
    icons = torch.tensor([2])
    out = assign_targets(grid, boxes, corners, icons, STRIDE)
    n_assigned = int(out["cell_valid"].sum().item())
    # Box is 80x80 px at stride 16 → ~5 cells x 5 cells should be inside.
    assert n_assigned >= 4, f"got {n_assigned} assigned cells"
    # Every assigned cell should carry the same icon class.
    icons_at_valid = out["cell_icon"][out["cell_valid"] > 0]
    assert (icons_at_valid == 2).all()
    # cell_obj is 0/1 and tracks cell_valid exactly.
    assert torch.equal(out["cell_obj"], out["cell_valid"])


def test_smaller_gt_wins_when_overlapping() -> None:
    grid = _build_grid(20, 20)
    if _is_stub(grid):
        pytest.xfail("assign_targets unimplemented")
    # Two boxes: a big one and a small one, both containing cell (5,5)
    # at pixel (88, 88). The smaller box (id=1) must win per FCOS.
    boxes = torch.tensor([
        [16.0,  16.0, 200.0, 200.0],   # large, area = 33856
        [80.0,  80.0,  96.0,  96.0],   # small, area = 256, contains (88,88)
    ])
    corners = torch.zeros((2, 8))
    icons = torch.tensor([0, 3])  # 3 = Sentry
    out = assign_targets(grid, boxes, corners, icons, STRIDE)
    # Cell (5, 5) at pixel (5*16+8=88, 88) is in both boxes; the small
    # one (Sentry, icon=3) wins.
    icon_at_55 = int(out["cell_icon"][5, 5].item())
    assert icon_at_55 == 3, f"expected Sentry (3), got {icon_at_55}"


def test_box_encoding_round_trips_through_decode_box() -> None:
    grid = _build_grid(20, 20)
    if _is_stub(grid):
        pytest.xfail("assign_targets unimplemented")
    boxes = torch.tensor([[64.0, 64.0, 96.0, 96.0]])
    corners = torch.zeros((1, 8))
    icons = torch.tensor([0])
    out = assign_targets(grid, boxes, corners, icons, STRIDE)

    # Reshape cell_box [H, W, 4] → [1, 4, H, W], then decode_box
    # should recover xyxy values close to the GT for the assigned
    # cells.
    from model import decode_box

    pred_box = out["cell_box"].permute(2, 0, 1).unsqueeze(0)
    decoded = decode_box(pred_box)              # [1, 4, H, W]
    valid = (out["cell_valid"] > 0).nonzero()
    assert valid.numel() > 0
    h, w = int(valid[0, 0]), int(valid[0, 1])
    decoded_xyxy = decoded[0, :, h, w]
    # Within a cell of slack — we rounded to a per-cell sigmoid
    # offset, so the decode lands within ~STRIDE/2 of the GT.
    assert abs(float(decoded_xyxy[0]) - 64.0) < STRIDE
    assert abs(float(decoded_xyxy[1]) - 64.0) < STRIDE
    assert abs(float(decoded_xyxy[2]) - 96.0) < STRIDE
    assert abs(float(decoded_xyxy[3]) - 96.0) < STRIDE
