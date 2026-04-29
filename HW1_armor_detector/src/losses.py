"""Loss-formulation building blocks.

These are the primitives the candidate's `loss_box`/`loss_kpt`/
`loss_cls` calls in `train.py` should compose. Each function here is
fully implemented; the candidate's job is to **select and combine**
them, not to re-derive the math.

Design rule: every function returns a scalar (mean over the valid
mask) so train.py can sum losses with simple weights.
"""

from __future__ import annotations

import torch
from torch import Tensor
from torch.nn import functional as F  # noqa: N812 — torch convention


def giou_loss(pred_xyxy: Tensor, target_xyxy: Tensor) -> Tensor:
    """Generalized-IoU loss between two [N, 4] tensors of xyxy boxes.

    Returns the mean of (1 - GIoU) over N. GIoU is bounded in [-1, 1],
    so the loss is bounded in [0, 2].
    """
    px1, py1, px2, py2 = pred_xyxy.unbind(dim=-1)
    tx1, ty1, tx2, ty2 = target_xyxy.unbind(dim=-1)

    # Intersection area.
    inter_x1 = torch.maximum(px1, tx1)
    inter_y1 = torch.maximum(py1, ty1)
    inter_x2 = torch.minimum(px2, tx2)
    inter_y2 = torch.minimum(py2, ty2)
    inter_w = (inter_x2 - inter_x1).clamp(min=0.0)
    inter_h = (inter_y2 - inter_y1).clamp(min=0.0)
    inter = inter_w * inter_h

    pred_area = (px2 - px1).clamp(min=0.0) * (py2 - py1).clamp(min=0.0)
    target_area = (tx2 - tx1).clamp(min=0.0) * (ty2 - ty1).clamp(min=0.0)
    union = pred_area + target_area - inter + 1e-7
    iou = inter / union

    # Smallest enclosing axis-aligned box.
    encl_x1 = torch.minimum(px1, tx1)
    encl_y1 = torch.minimum(py1, ty1)
    encl_x2 = torch.maximum(px2, tx2)
    encl_y2 = torch.maximum(py2, ty2)
    encl_area = (encl_x2 - encl_x1).clamp(min=0.0) * (encl_y2 - encl_y1).clamp(min=0.0) + 1e-7

    giou = iou - (encl_area - union) / encl_area
    return (1.0 - giou).mean()


def focal_loss(
    logits: Tensor,
    target: Tensor,
    alpha: float = 0.25,
    gamma: float = 2.0,
) -> Tensor:
    """Sigmoid focal loss for binary or multi-label classification.

    `logits` and `target` are same-shape tensors; target is 0/1.
    Returns the mean over all elements.
    """
    p = logits.sigmoid()
    ce = F.binary_cross_entropy_with_logits(logits, target, reduction="none")
    p_t = p * target + (1.0 - p) * (1.0 - target)
    alpha_t = alpha * target + (1.0 - alpha) * (1.0 - target)
    loss = alpha_t * (1.0 - p_t).pow(gamma) * ce
    return loss.mean()


def keypoint_l1(pred: Tensor, target: Tensor, valid_mask: Tensor) -> Tensor:
    """Masked L1 over a [N, K] tensor of keypoint offsets.

    `valid_mask` is [N] or broadcastable to [N, K]; rows where mask=0
    contribute zero gradient. Returns the mean over the valid entries
    (NOT over all entries — this matters for the loss-weight balance
    train.py is expected to set).
    """
    if valid_mask.dim() == 1:
        valid_mask = valid_mask.unsqueeze(-1).expand_as(pred)
    diff = (pred - target).abs() * valid_mask
    denom = valid_mask.sum().clamp(min=1.0)
    return diff.sum() / denom


def softmax_focal_loss(
    logits: Tensor,
    target_index: Tensor,
    valid_mask: Tensor,
    alpha: float = 0.25,
    gamma: float = 2.0,
) -> Tensor:
    """Softmax focal loss for the icon head.

    `logits` is [N, C]; `target_index` is [N] of class indices in
    [0, C); `valid_mask` is [N] of 0/1.
    """
    if valid_mask.sum() == 0:
        return logits.sum() * 0.0
    log_p = F.log_softmax(logits, dim=-1)
    p = log_p.exp()
    ce = -log_p.gather(1, target_index.unsqueeze(-1)).squeeze(-1)
    p_t = p.gather(1, target_index.unsqueeze(-1)).squeeze(-1)
    loss = alpha * (1.0 - p_t).pow(gamma) * ce
    masked = loss * valid_mask
    denom = valid_mask.sum().clamp(min=1.0)
    return masked.sum() / denom


def assign_targets(
    grid_centres: Tensor,
    gt_boxes_xyxy: Tensor,
    gt_corners: Tensor,
    gt_icons: Tensor,
    stride: int,
) -> dict[str, Tensor]:
    """FCOS-style positive-cell assignment: a feature-map cell is positive
    if its centre falls inside any GT box, in which case it inherits
    that box's regression + keypoint + class targets. If a cell is
    inside multiple GTs, the smallest GT wins (ties broken by index).

    grid_centres: [H, W, 2]  (x, y) cell centres in pixels
    gt_boxes_xyxy: [G, 4]
    gt_corners:    [G, 8]    flattened (x0,y0,...,x3,y3)
    gt_icons:      [G]       int64

    Returns a dict with:
        cell_box:    [H, W, 4]  log-scale + center-offset targets
        cell_kpt:    [H, W, 8]  corner offsets in pixel space
        cell_icon:   [H, W]     int64 icon label (-1 = ignore)
        cell_obj:    [H, W]     0/1 objectness target
        cell_valid:  [H, W]     0/1 whether the cell was assigned
    """
    H, W, _ = grid_centres.shape
    device = grid_centres.device

    cell_box = torch.zeros((H, W, 4), device=device)
    cell_kpt = torch.zeros((H, W, 8), device=device)
    cell_icon = torch.full((H, W), -1, dtype=torch.long, device=device)
    cell_obj = torch.zeros((H, W), device=device)
    cell_valid = torch.zeros((H, W), device=device)

    if gt_boxes_xyxy.numel() == 0:
        return {
            "cell_box": cell_box,
            "cell_kpt": cell_kpt,
            "cell_icon": cell_icon,
            "cell_obj": cell_obj,
            "cell_valid": cell_valid,
        }

    centres = grid_centres.reshape(-1, 2)              # [HW, 2]
    cx = centres[:, 0:1]                                # [HW, 1]
    cy = centres[:, 1:2]
    gx1, gy1, gx2, gy2 = gt_boxes_xyxy.unbind(dim=-1)   # each [G]

    inside = ((cx >= gx1) & (cx <= gx2) & (cy >= gy1) & (cy <= gy2))  # [HW, G]
    areas = (gx2 - gx1).clamp(min=1e-3) * (gy2 - gy1).clamp(min=1e-3)
    areas = areas.unsqueeze(0).expand_as(inside.float())
    areas = torch.where(inside, areas, torch.full_like(areas, float("inf")))
    best_g = areas.argmin(dim=1)                       # [HW]
    has_gt = inside.any(dim=1)                         # [HW]

    for k in range(centres.shape[0]):
        if not has_gt[k]:
            continue
        g = int(best_g[k].item())
        x = float(centres[k, 0].item())
        y = float(centres[k, 1].item())
        cx_box = (gt_boxes_xyxy[g, 0] + gt_boxes_xyxy[g, 2]) / 2
        cy_box = (gt_boxes_xyxy[g, 1] + gt_boxes_xyxy[g, 3]) / 2
        wid = (gt_boxes_xyxy[g, 2] - gt_boxes_xyxy[g, 0]).clamp(min=1.0)
        hei = (gt_boxes_xyxy[g, 3] - gt_boxes_xyxy[g, 1]).clamp(min=1.0)
        # Inverse of model.decode_box: store offsets as (sigmoid_inv,
        # log) so the head's raw prediction matches them after the
        # forward decode.
        ox = torch.tensor((cx_box - x) / stride + 0.5, device=device).clamp(1e-3, 1 - 1e-3)
        oy = torch.tensor((cy_box - y) / stride + 0.5, device=device).clamp(1e-3, 1 - 1e-3)
        cell_box[k // W, k % W, 0] = torch.logit(ox)
        cell_box[k // W, k % W, 1] = torch.logit(oy)
        cell_box[k // W, k % W, 2] = torch.log(wid)
        cell_box[k // W, k % W, 3] = torch.log(hei)
        cell_kpt[k // W, k % W] = gt_corners[g]
        cell_icon[k // W, k % W] = gt_icons[g]
        cell_obj[k // W, k % W] = 1.0
        cell_valid[k // W, k % W] = 1.0

    return {
        "cell_box": cell_box,
        "cell_kpt": cell_kpt,
        "cell_icon": cell_icon,
        "cell_obj": cell_obj,
        "cell_valid": cell_valid,
    }
