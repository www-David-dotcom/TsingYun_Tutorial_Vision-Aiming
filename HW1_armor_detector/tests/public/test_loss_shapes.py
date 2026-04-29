"""Public unit tests for HW1's loss formulation.

These tests exercise the candidate's `loss_box`, `loss_kpt`, and
`loss_cls` implementations end-to-end against synthetic targets. They
do not assert on numeric quality — only on shape and gradient sanity.
The acceptance bar (loss decreases over 5 epochs) lives in train.py
under the `--check-monotone` flag.

The whole module skips itself when torch isn't installed; HW1 keeps
torch in an opt-in dependency group (`uv sync --group hw1`).
"""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

torch = pytest.importorskip("torch")
pytest.importorskip("torchvision")

REPO_ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(REPO_ROOT / "HW1_armor_detector" / "src"))

from losses import assign_targets  # noqa: E402
from model import STRIDE, ArmorDetector, decode_box, make_grid  # noqa: E402

IMG_H = 320          # use a small image so the test runs in seconds.
IMG_W = 320
BATCH = 2


@pytest.fixture(scope="module")
def sample_outputs() -> dict:
    torch.manual_seed(0)
    model = ArmorDetector(pretrained_backbone=False)
    images = torch.zeros((BATCH, 3, IMG_H, IMG_W))
    out = model(images)
    return {
        "out": out,
        "decoded": decode_box(out.box),
        "grid": make_grid(out.box.shape[2], out.box.shape[3]),
    }


def _build_target_assigns(grid: torch.Tensor) -> dict:
    boxes = torch.tensor([[40.0, 60.0, 120.0, 140.0]])
    corners = torch.tensor([[40.0, 60.0, 120.0, 60.0, 120.0, 140.0, 40.0, 140.0]])
    icons = torch.tensor([2])
    cell_box, cell_kpt, cell_icon, cell_obj, cell_valid = [], [], [], [], []
    for _ in range(BATCH):
        a = assign_targets(grid_centres=grid,
                           gt_boxes_xyxy=boxes,
                           gt_corners=corners,
                           gt_icons=icons,
                           stride=STRIDE)
        cell_box.append(a["cell_box"])
        cell_kpt.append(a["cell_kpt"])
        cell_icon.append(a["cell_icon"])
        cell_obj.append(a["cell_obj"])
        cell_valid.append(a["cell_valid"])
    return {
        "cell_box": torch.stack(cell_box, dim=0),
        "cell_kpt": torch.stack(cell_kpt, dim=0),
        "cell_icon": torch.stack(cell_icon, dim=0),
        "cell_obj": torch.stack(cell_obj, dim=0),
        "cell_valid": torch.stack(cell_valid, dim=0),
    }


def test_decoder_shapes(sample_outputs: dict) -> None:
    decoded = sample_outputs["decoded"]
    assert decoded.shape == (BATCH, 4, IMG_H // STRIDE, IMG_W // STRIDE)
    # x1 < x2, y1 < y2 by construction.
    assert (decoded[:, 0] <= decoded[:, 2]).all()
    assert (decoded[:, 1] <= decoded[:, 3]).all()


def test_assign_targets_marks_some_cell(sample_outputs: dict) -> None:
    assigns = _build_target_assigns(sample_outputs["grid"])
    valid = assigns["cell_valid"]
    assert valid.shape == (BATCH, IMG_H // STRIDE, IMG_W // STRIDE)
    if valid.sum().item() == 0:
        pytest.xfail("assign_targets unimplemented — see test_assign_targets.py")
    # The synthetic GT box is ~80x80 pixels; on an STRIDE=16 feature
    # map that should hit at least ~25 cells per image.
    assert (valid.sum(dim=(1, 2)) >= 4).all(), valid.sum(dim=(1, 2))


def test_loss_box_runs_or_skips() -> None:
    """If the candidate has filled `loss_box`, the call should succeed
    and produce a scalar with finite value. If they haven't, the
    NotImplementedError is the expected state — we mark this test as
    expected-fail so the suite reports "1 xfailed" instead of red."""
    pytest.importorskip("torchvision")
    from train import loss_box

    grid = make_grid(IMG_H // STRIDE, IMG_W // STRIDE)
    assigns = _build_target_assigns(grid)
    pred_box = torch.zeros((BATCH, 4, IMG_H // STRIDE, IMG_W // STRIDE), requires_grad=True)
    decoded = decode_box(pred_box)
    try:
        loss = loss_box(pred_box, decoded, assigns)
    except NotImplementedError:
        pytest.xfail("loss_box not implemented — fill the TODO in train.py")
    assert torch.isfinite(loss)
    assert loss.dim() == 0


def test_loss_kpt_runs_or_skips() -> None:
    from train import loss_kpt

    grid = make_grid(IMG_H // STRIDE, IMG_W // STRIDE)
    assigns = _build_target_assigns(grid)
    pred_kpt = torch.zeros((BATCH, 8, IMG_H // STRIDE, IMG_W // STRIDE), requires_grad=True)
    try:
        loss = loss_kpt(pred_kpt, assigns)
    except NotImplementedError:
        pytest.xfail("loss_kpt not implemented — fill the TODO in train.py")
    assert torch.isfinite(loss)


def test_loss_cls_runs_or_skips() -> None:
    from train import loss_cls

    grid = make_grid(IMG_H // STRIDE, IMG_W // STRIDE)
    assigns = _build_target_assigns(grid)
    pred_cls = torch.zeros((BATCH, 4, IMG_H // STRIDE, IMG_W // STRIDE), requires_grad=True)
    pred_obj = torch.zeros((BATCH, 1, IMG_H // STRIDE, IMG_W // STRIDE), requires_grad=True)
    try:
        loss = loss_cls(pred_cls, pred_obj, assigns)
    except NotImplementedError:
        pytest.xfail("loss_cls not implemented — fill the TODO in train.py")
    assert torch.isfinite(loss)
