"""Training entry point for HW1.

The full skeleton is here — dataloader, optimizer, scheduler, training
loop, validation, checkpoint dump. **Four** functions are stubbed with
`# TODO(HW1):` markers. Fill them and you have a working detector.

Each blank is one or two lines that compose primitives from
`src/losses.py`. Read that module first before implementing.

Usage:
    uv run python src/train.py --epochs 10 --data /tmp/ds --out /tmp/last.pt

The `--check-monotone` flag runs the acceptance "loss decreases over 5
epochs" smoke and exits non-zero if it fails.
"""

from __future__ import annotations

import argparse
import itertools
import json
import sys
from pathlib import Path

import numpy as np
import torch
from PIL import Image
from torch import Tensor, nn, optim
from torch.utils.data import DataLoader, Dataset
from torchvision.transforms.functional import to_tensor
try:
    from tqdm import tqdm
except ImportError:  # pragma: no cover - optional training nicety
    def tqdm(iterable, *args, **kwargs):
        return iterable

# Local imports.
sys.path.insert(0, str(Path(__file__).resolve().parent))
from losses import (
    assign_targets,
)
from model import STRIDE, ArmorDetector, decode_box, make_grid

ICON_TO_INDEX = {"Hero": 0, "Engineer": 1, "Standard": 2, "Sentry": 3}


# --------------------------------------------------------------- dataset

class ArmorDataset(Dataset):
    """Loads (image, label) pairs produced by data/dataset_dumper.py."""

    def __init__(self, root: Path) -> None:
        self.frames = sorted(p for p in root.glob("*.png"))
        self.root = root
        if not self.frames:
            raise RuntimeError(f"no frames under {root}")

    def __len__(self) -> int:
        return len(self.frames)

    def __getitem__(self, idx: int) -> tuple[Tensor, dict]:
        image_path = self.frames[idx]
        label_path = image_path.with_suffix(".json")
        img = Image.open(image_path).convert("RGB")
        tensor = to_tensor(img)  # [3, H, W] in [0, 1]
        label = json.loads(label_path.read_text())
        boxes = []
        corners = []
        icons = []
        for plate in label["plates"]:
            boxes.append(plate["bbox_xyxy"])
            flat = []
            for c in plate["corners"]:
                flat.extend(c)
            corners.append(flat)
            icons.append(ICON_TO_INDEX.get(plate["icon"], 2))
        return tensor, {
            "boxes": torch.tensor(boxes, dtype=torch.float32) if boxes else torch.zeros((0, 4)),
            "corners": torch.tensor(corners, dtype=torch.float32) if corners else torch.zeros((0, 8)),
            "icons": torch.tensor(icons, dtype=torch.long) if icons else torch.zeros((0,), dtype=torch.long),
        }


def collate_fn(batch):
    images = torch.stack([b[0] for b in batch], dim=0)
    targets = [b[1] for b in batch]
    return images, targets


# ------------------------------------------------------------ TODO(HW1): sites

def mixup(images: Tensor, targets: list[dict], alpha: float, rng: np.random.Generator) -> tuple[Tensor, list[dict]]:
    """Pairwise mixup augmentation.

    With probability proportional to `alpha` (a beta distribution
    concentration), shuffle the batch and blend each image with its
    shuffled partner using lambda ~ Beta(alpha, alpha). Targets stay
    pinned to the **dominant** image (lambda > 0.5) so the assignment
    step downstream stays well-defined.

    Returns the mixed images and the (possibly-swapped) targets.
    """
    # TODO(HW1): implement mixup.
    #
    # Hints:
    #   - sample lam = float(rng.beta(alpha, alpha))
    #   - choose perm = torch.randperm(images.shape[0])
    #   - mixed = lam * images + (1 - lam) * images[perm]
    #   - targets follow whichever side of 0.5 lam landed on
    #
    # Setting alpha <= 0 disables mixup (return images, targets
    # unchanged). The dataloader passes the per-batch decision via the
    # `apply` flag below; honour it.
    raise NotImplementedError("HW1 mixup not implemented yet")


def loss_box(pred_box: Tensor, decoded_xyxy: Tensor, target_assigns: dict) -> Tensor:
    """Bounding-box regression loss on positively-assigned cells.

    Combine GIoU on the decoded xyxy boxes with an auxiliary L1 on the
    raw 4-channel head output. The L1 component stabilizes early
    training when GIoU's gradient is poorly conditioned.
    """
    # TODO(HW1): implement loss_box.
    #
    # Hints:
    #   - mask = target_assigns["cell_valid"]                # [B, H, W]
    #   - if mask.sum() == 0: return zero scalar
    #   - select positive cells from pred_box, decoded_xyxy, and the
    #     corresponding cell_box / cell_xyxy targets
    #   - return giou_loss(decoded_pos, target_xyxy_pos) + 0.1 * l1
    #
    # Hint: you can reconstruct target xyxy from cell_box via the same
    # math as model.decode_box; or skip the reconstruction and operate
    # on the raw 4 channels for the L1 term, which is what we do.
    raise NotImplementedError("HW1 loss_box not implemented yet")


def loss_kpt(pred_kpt: Tensor, target_assigns: dict) -> Tensor:
    """8-channel corner-offset L1 on positively-assigned cells."""
    # TODO(HW1): implement loss_kpt.
    #
    # Hints:
    #   - target = target_assigns["cell_kpt"]    # [B, H, W, 8]
    #   - mask   = target_assigns["cell_valid"]  # [B, H, W]
    #   - reshape pred_kpt from [B, 8, H, W] to match target
    #   - return keypoint_l1(pred_flat, target_flat, mask_flat)
    #
    # The keypoints are absolute pixel positions of the four corners
    # in (x0,y0,x1,y1,x2,y2,x3,y3) order — the dataset writes them
    # that way.
    raise NotImplementedError("HW1 loss_kpt not implemented yet")


def loss_cls(pred_cls: Tensor, pred_obj: Tensor, target_assigns: dict) -> Tensor:
    """Icon-classification softmax-focal + objectness binary-focal."""
    # TODO(HW1): implement loss_cls.
    #
    # Hints:
    #   - softmax_focal_loss handles the 4-way icon head (use
    #     target_assigns["cell_icon"], with -1 entries masked out).
    #   - focal_loss handles the binary objectness head against
    #     target_assigns["cell_obj"].
    #   - Sum the two with equal weight; the per-cell mean inside each
    #     primitive already balances them.
    raise NotImplementedError("HW1 loss_cls not implemented yet")


# --------------------------------------------------------- training loop

def train_one_epoch(
    model: ArmorDetector,
    loader: DataLoader,
    optimizer: optim.Optimizer,
    device: torch.device,
    rng: np.random.Generator,
    mixup_alpha: float,
    mixup_prob: float,
) -> float:
    model.train()
    running = 0.0
    n = 0
    for images, targets in tqdm(loader, desc="train", leave=False):
        images = images.to(device, non_blocking=True)
        if mixup_alpha > 0.0 and float(rng.uniform()) < mixup_prob:
            images, targets = mixup(images, targets, alpha=mixup_alpha, rng=rng)

        out = model(images)
        decoded = decode_box(out.box)
        # Build per-image assignments at the feature-map resolution.
        _, _, h, w = out.box.shape
        grid = make_grid(h, w, device=device)
        cell_boxes = []
        cell_kpts = []
        cell_icons = []
        cell_objs = []
        cell_valids = []
        for t in targets:
            assigns = assign_targets(
                grid_centres=grid,
                gt_boxes_xyxy=t["boxes"].to(device),
                gt_corners=t["corners"].to(device),
                gt_icons=t["icons"].to(device),
                stride=STRIDE,
            )
            cell_boxes.append(assigns["cell_box"])
            cell_kpts.append(assigns["cell_kpt"])
            cell_icons.append(assigns["cell_icon"])
            cell_objs.append(assigns["cell_obj"])
            cell_valids.append(assigns["cell_valid"])
        target_assigns = {
            "cell_box": torch.stack(cell_boxes, dim=0),
            "cell_kpt": torch.stack(cell_kpts, dim=0),
            "cell_icon": torch.stack(cell_icons, dim=0),
            "cell_obj": torch.stack(cell_objs, dim=0),
            "cell_valid": torch.stack(cell_valids, dim=0),
        }

        l_box = loss_box(out.box, decoded, target_assigns)
        l_kpt = loss_kpt(out.kpt, target_assigns)
        l_cls = loss_cls(out.cls, out.obj, target_assigns)
        loss = l_box + l_kpt * 0.05 + l_cls

        optimizer.zero_grad(set_to_none=True)
        loss.backward()
        nn.utils.clip_grad_norm_(model.parameters(), max_norm=10.0)
        optimizer.step()

        running += float(loss.detach().item()) * images.shape[0]
        n += images.shape[0]
    return running / max(n, 1)


def main() -> int:
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("--data", type=Path, required=True)
    parser.add_argument("--out", type=Path, default=Path("/tmp/last.pt"))
    parser.add_argument("--epochs", type=int, default=10)
    parser.add_argument("--batch-size", type=int, default=8)
    parser.add_argument("--lr", type=float, default=2e-3)
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--mixup-alpha", type=float, default=0.4)
    parser.add_argument("--mixup-prob", type=float, default=0.15)
    parser.add_argument("--no-pretrained", action="store_true")
    parser.add_argument("--check-monotone", action="store_true",
                        help="exit non-zero if the loss isn't monotonic over 5 epochs")
    args = parser.parse_args()

    torch.manual_seed(args.seed)
    rng = np.random.default_rng(args.seed)
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

    dataset = ArmorDataset(args.data)
    loader = DataLoader(
        dataset,
        batch_size=args.batch_size,
        shuffle=True,
        num_workers=2,
        collate_fn=collate_fn,
        pin_memory=device.type == "cuda",
    )

    model = ArmorDetector(pretrained_backbone=not args.no_pretrained).to(device)
    optimizer = optim.AdamW(model.parameters(), lr=args.lr, weight_decay=1e-4)
    scheduler = optim.lr_scheduler.CosineAnnealingLR(optimizer, T_max=args.epochs)

    losses: list[float] = []
    for epoch in range(args.epochs):
        epoch_loss = train_one_epoch(
            model, loader, optimizer, device, rng,
            mixup_alpha=args.mixup_alpha,
            mixup_prob=args.mixup_prob,
        )
        scheduler.step()
        losses.append(epoch_loss)
        print(f"[epoch {epoch + 1}/{args.epochs}] loss={epoch_loss:.4f}")

    args.out.parent.mkdir(parents=True, exist_ok=True)
    torch.save({"model": model.state_dict(), "args": vars(args)}, args.out)
    print(f"saved {args.out}")

    if args.check_monotone:
        if len(losses) < 5:
            print("--check-monotone needs >= 5 epochs", file=sys.stderr)
            return 2
        is_monotone = all(b <= a + 1e-3 for a, b in itertools.pairwise(losses[:5]))
        if not is_monotone:
            print(f"loss not monotonic over first 5 epochs: {losses[:5]}", file=sys.stderr)
            return 1
        print("monotonicity check passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
