"""Synthetic dataset dumper for HW1.

Composites procedural armor plates onto noisy backgrounds using PIL only.
No torch and no live Unity runtime are required, which keeps the legacy HW1
dataloader and loss smoke tests cheap.

Output layout:

    <out>/<frame_id:06d>.png        RGB image at viewport size
    <out>/<frame_id:06d>.json       label payload, see _LabelFile

Determinism: a single numpy.Generator seeded by `--seed` drives every draw.
The same seed produces byte-identical frames.
"""

from __future__ import annotations

import argparse
import json
import math
from dataclasses import asdict, dataclass
from pathlib import Path

import numpy as np
import yaml
from PIL import Image, ImageDraw, ImageFilter

ICONS = ["Hero", "Engineer", "Standard", "Sentry"]
TEAMS = ["blue", "red"]
TEAM_COLORS = {
    "blue": (50, 110, 255),
    "red": (235, 70, 70),
}


@dataclass
class PlateLabel:
    armor_id: str
    icon: str
    team: str
    bbox_xyxy: list[float]
    corners: list[list[float]]


@dataclass
class _LabelFile:
    frame_id: int
    image_size: list[int]
    plates: list[PlateLabel]

    def to_json(self) -> dict:
        return {
            "frame_id": self.frame_id,
            "image_size": self.image_size,
            "plates": [asdict(p) for p in self.plates],
        }


# --------------------------------------------------------------- synthetic

def _draw_background(rng: np.random.Generator, dr_cfg: dict, size: tuple[int, int]) -> Image.Image:
    """Procedural noise + a few rectangles. Looks nothing like an arena
    but has enough texture to keep the detector from collapsing."""
    w, h = size
    bg_cfg = dr_cfg["background"]
    base = rng.integers(0, 256, size=(h, w, 3), dtype=np.uint8)
    img = Image.fromarray(base, mode="RGB")
    img = img.filter(ImageFilter.GaussianBlur(radius=8))

    draw = ImageDraw.Draw(img)
    n_rect = int(rng.integers(bg_cfg["rect_count"]["min"], bg_cfg["rect_count"]["max"] + 1))
    for _ in range(n_rect):
        x0 = int(rng.integers(0, w))
        y0 = int(rng.integers(0, h))
        x1 = x0 + int(rng.integers(40, 240))
        y1 = y0 + int(rng.integers(20, 120))
        color = tuple(int(c) for c in rng.integers(40, 200, size=3))
        draw.rectangle([x0, y0, x1, y1], fill=color)
    return img


def _render_plate(
    img: Image.Image,
    rng: np.random.Generator,
    dr_cfg: dict,
) -> PlateLabel | None:
    plate_cfg = dr_cfg["plate"]
    w, h = img.size
    pw = int(rng.integers(plate_cfg["width_px"]["min"], plate_cfg["width_px"]["max"] + 1))
    ar = float(rng.uniform(plate_cfg["aspect_ratio"]["min"], plate_cfg["aspect_ratio"]["max"]))
    ph = max(8, int(pw / ar))

    cx = int(rng.integers(pw, max(pw + 1, w - pw)))
    cy = int(rng.integers(ph, max(ph + 1, h - ph)))

    yaw = math.radians(float(rng.uniform(plate_cfg["yaw_deg"]["min"], plate_cfg["yaw_deg"]["max"])))
    half_w = pw / 2.0
    half_h = ph / 2.0
    cos_y, sin_y = math.cos(yaw), math.sin(yaw)
    # Top-left, top-right, bottom-right, bottom-left in CCW order.
    local_corners = [
        (-half_w, -half_h),
        (+half_w, -half_h),
        (+half_w, +half_h),
        (-half_w, +half_h),
    ]
    corners: list[list[float]] = []
    for (x, y) in local_corners:
        rx = x * cos_y - y * sin_y
        ry = x * sin_y + y * cos_y
        corners.append([cx + rx, cy + ry])

    team = str(rng.choice(plate_cfg["team_choices"]))
    icon = str(rng.choice(plate_cfg["icon_choices"]))
    color = TEAM_COLORS[team]

    overlay = Image.new("RGBA", img.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    poly = [(round(p[0]), round(p[1])) for p in corners]
    draw.polygon(poly, fill=(*color, 220))
    # Inner rectangle hints at the icon. Actual glyphs are out of scope —
    # the detector's icon head is a 4-class softmax, not a glyph reader.
    inset = 4
    inner = [(p[0] + inset if i in (0, 3) else p[0] - inset,
              p[1] + inset if i in (0, 1) else p[1] - inset)
             for i, p in enumerate(poly)]
    icon_color = (255, 255, 255, 230) if team == "blue" else (240, 230, 70, 230)
    draw.polygon(inner, outline=icon_color, width=2)

    img.alpha_composite(overlay) if img.mode == "RGBA" else img.paste(
        Image.alpha_composite(img.convert("RGBA"), overlay).convert("RGB"))

    xs = [p[0] for p in corners]
    ys = [p[1] for p in corners]
    bbox = [min(xs), min(ys), max(xs), max(ys)]
    return PlateLabel(
        armor_id=f"{team}.synthetic",
        icon=icon,
        team=team,
        bbox_xyxy=bbox,
        corners=corners,
    )


def synthesize_one(
    frame_id: int,
    rng: np.random.Generator,
    dr_cfg: dict,
) -> tuple[Image.Image, _LabelFile]:
    size = (int(dr_cfg["image"]["width"]), int(dr_cfg["image"]["height"]))
    img = _draw_background(rng, dr_cfg, size).convert("RGB")
    plates_cfg = dr_cfg["plates_per_frame"]
    n = int(rng.integers(plates_cfg["min"], plates_cfg["max"] + 1))
    plates: list[PlateLabel] = []
    # Convert to RGBA once so plate compositing has an alpha channel.
    rgba = img.convert("RGBA")
    for _ in range(n):
        label = _render_plate(rgba, rng, dr_cfg)
        if label is not None:
            plates.append(label)
    return rgba.convert("RGB"), _LabelFile(
        frame_id=frame_id,
        image_size=list(size),
        plates=plates,
    )


# --------------------------------------------------------------------- CLI

def main() -> int:
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("--frames", type=int, default=200)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--config",
                        type=Path,
                        default=Path(__file__).parent / "domain_randomization.yaml")
    args = parser.parse_args()

    args.out.mkdir(parents=True, exist_ok=True)
    dr_cfg = yaml.safe_load(args.config.read_text())
    rng = np.random.default_rng(args.seed)
    for i in range(args.frames):
        img, label = synthesize_one(i, rng, dr_cfg)
        img.save(args.out / f"{i:06d}.png")
        (args.out / f"{i:06d}.json").write_text(
            json.dumps(label.to_json(), indent=2))
    print(f"[synthetic] wrote {args.frames} frame/label pairs to {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
