"""Dataset dumper for HW1.

Two backends:

* `--source synthetic` (default) — composites procedural armor plates
  onto noisy backgrounds using PIL only. No torch, no Godot. Good for
  smoke-testing the dataloader and the loss formulation while you fill
  the TODOs in src/train.py.
* `--source godot` — drives the Stage-2 Godot arena over TCP
  (length-prefixed JSON on the control port, raw RGB888 on the frame
  port), reads oracle target poses each tick, and re-projects them
  through the gimbal-camera intrinsics in
  `data/camera_intrinsics.yaml`. Visibility is a facing-dot heuristic;
  see the YAML for the threshold.

Output layout:

    <out>/<frame_id:06d>.png        RGB image at viewport size
    <out>/<frame_id:06d>.json       label payload, see _LabelFile

Determinism: a single numpy.Generator seeded by `--seed` drives every
draw. The same seed produces byte-identical frames on the synthetic
backend; the godot backend depends on Godot's RNG (also seedable via
EnvReset.seed).
"""

from __future__ import annotations

import argparse
import json
import math
import socket
import struct
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


# ------------------------------------------------------------------ godot

def _recv_exact(sock: socket.socket, n: int) -> bytes:
    chunks: list[bytes] = []
    remaining = n
    while remaining:
        chunk = sock.recv(remaining)
        if not chunk:
            raise ConnectionError("godot arena closed connection mid-message")
        chunks.append(chunk)
        remaining -= len(chunk)
    return b"".join(chunks)


def _rpc(sock: socket.socket, method: str, request: dict) -> dict:
    payload = json.dumps({"method": method, "request": request}).encode("utf-8")
    sock.sendall(struct.pack(">I", len(payload)) + payload)
    (n,) = struct.unpack(">I", _recv_exact(sock, 4))
    return json.loads(_recv_exact(sock, n).decode("utf-8"))


def _project_world_to_pixel(
    point_world: tuple[float, float, float],
    cam_intrinsics: dict,
) -> tuple[float, float, float]:
    """Project a Y-up Godot world point into pixel coordinates relative
    to a camera at the world origin looking along -Z (Godot default).

    Returns (u, v, depth). Depth < 0 means the point is behind the
    camera and should be discarded by the caller.
    """
    fx = float(cam_intrinsics["fx"])
    fy = float(cam_intrinsics["fy"])
    cx = float(cam_intrinsics["cx"])
    cy = float(cam_intrinsics["cy"])
    x, y, z = point_world
    depth = -z
    if depth <= 1e-3:
        return (math.nan, math.nan, depth)
    u = fx * (x / depth) + cx
    v = -fy * (y / depth) + cy
    return (u, v, depth)


def dump_from_godot(
    *,
    host: str,
    control_port: int,
    frame_port: int,
    out_dir: Path,
    n_frames: int,
    seed: int,
    cam_intrinsics: dict,
) -> None:
    """Drive the Stage-2 Godot arena and capture frame-label pairs.

    Stage 3 limitation: only the red chassis's centre is projected (we
    use the oracle hint, which is the chassis centroid). To get
    per-plate bboxes we'd need a Godot-side EnvDumpLabels RPC that
    raycasts each plate. That's a Stage-7 follow-up.
    """
    out_dir.mkdir(parents=True, exist_ok=True)
    width = int(cam_intrinsics["width"])
    height = int(cam_intrinsics["height"])

    with socket.create_connection((host, control_port), timeout=5) as ctrl, \
            socket.create_connection((host, frame_port), timeout=5) as frames:
        reset = _rpc(ctrl, "env_reset", {
            "seed": seed,
            "opponent_tier": "bronze",
            "oracle_hints": True,
            "duration_ns": 0,
        })
        if not reset.get("ok"):
            raise RuntimeError(f"env_reset failed: {reset}")

        for frame_idx in range(n_frames):
            step = _rpc(ctrl, "env_step", {
                "stamp_ns": frame_idx * 16_000_000,
                "target_yaw": 0.0,
                "target_pitch": 0.0,
            })
            bundle = step["response"]
            oracle = bundle.get("oracle", {})
            target = oracle.get("target_position_world", {"x": 0.0, "y": 0.0, "z": 0.0})

            # Read one frame from the publisher: 16-byte header + RGB888.
            header = _recv_exact(frames, 16)
            (frame_id, _stamp_ns) = struct.unpack("<QQ", header)
            payload = _recv_exact(frames, width * height * 3)
            arr = np.frombuffer(payload, dtype=np.uint8).reshape(height, width, 3)
            img = Image.fromarray(arr, mode="RGB")

            (u, v, depth) = _project_world_to_pixel(
                (float(target["x"]), float(target["y"]), float(target["z"])),
                cam_intrinsics,
            )
            plates: list[PlateLabel] = []
            if 0 <= u < width and 0 <= v < height and depth > 0:
                # Approximate a plate-sized bbox at the projected point.
                plate_w_px = float(cam_intrinsics["plate_world"]["width_m"]) * float(cam_intrinsics["fx"]) / depth
                plate_h_px = float(cam_intrinsics["plate_world"]["height_m"]) * float(cam_intrinsics["fy"]) / depth
                bbox = [u - plate_w_px / 2, v - plate_h_px / 2,
                        u + plate_w_px / 2, v + plate_h_px / 2]
                corners = [
                    [bbox[0], bbox[1]],
                    [bbox[2], bbox[1]],
                    [bbox[2], bbox[3]],
                    [bbox[0], bbox[3]],
                ]
                plates.append(PlateLabel(
                    armor_id="red.front",
                    icon="Standard",
                    team="red",
                    bbox_xyxy=bbox,
                    corners=corners,
                ))

            label = _LabelFile(
                frame_id=frame_id,
                image_size=[width, height],
                plates=plates,
            )
            img.save(out_dir / f"{frame_id:06d}.png")
            (out_dir / f"{frame_id:06d}.json").write_text(
                json.dumps(label.to_json(), indent=2))

        finish = _rpc(ctrl, "env_finish", {"flush_replay": False})
        if not finish.get("ok"):
            print(f"warning: env_finish returned {finish}")


# --------------------------------------------------------------------- CLI

def main() -> int:
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("--source", choices=("synthetic", "godot"), default="synthetic")
    parser.add_argument("--frames", type=int, default=200)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--control-port", type=int, default=7654)
    parser.add_argument("--frame-port", type=int, default=7655)
    parser.add_argument("--config",
                        type=Path,
                        default=Path(__file__).parent / "domain_randomization.yaml")
    parser.add_argument("--intrinsics",
                        type=Path,
                        default=Path(__file__).parent / "camera_intrinsics.yaml")
    args = parser.parse_args()

    args.out.mkdir(parents=True, exist_ok=True)
    dr_cfg = yaml.safe_load(args.config.read_text())
    cam_intrinsics = yaml.safe_load(args.intrinsics.read_text())

    if args.source == "synthetic":
        rng = np.random.default_rng(args.seed)
        for i in range(args.frames):
            img, label = synthesize_one(i, rng, dr_cfg)
            img.save(args.out / f"{i:06d}.png")
            (args.out / f"{i:06d}.json").write_text(
                json.dumps(label.to_json(), indent=2))
        print(f"[synthetic] wrote {args.frames} frame/label pairs to {args.out}")
    else:
        dump_from_godot(
            host=args.host,
            control_port=args.control_port,
            frame_port=args.frame_port,
            out_dir=args.out,
            n_frames=args.frames,
            seed=args.seed,
            cam_intrinsics=cam_intrinsics,
        )
        print(f"[godot] wrote frame/label pairs to {args.out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
