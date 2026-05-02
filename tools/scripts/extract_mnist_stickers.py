"""Extract N samples per digit from MNIST as PNG stickers.

Unity renders an MNIST digit on each armor plate as the robot's number tag.
This script downloads the raw MNIST IDX files (no torchvision / torch
dependency) and dumps the first N samples of each digit (0-9) into a flat
directory:

  shared/sticker_assets/mnist/{digit}/{idx:03d}.png

Unity loads from:
  shared/unity_arena/Assets/Resources/MNIST/

Required deps: pillow only. Add via `uv add --dev pillow` if missing.

Usage:
  uv run python tools/scripts/extract_mnist_stickers.py
  uv run python tools/scripts/extract_mnist_stickers.py --samples-per-digit 100
"""

from __future__ import annotations

import argparse
import gzip
import struct
import urllib.request
from pathlib import Path

from PIL import Image

REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_OUT = REPO_ROOT / "shared" / "sticker_assets" / "mnist"

# OSSCI mirror — the canonical Yann LeCun host went offline in 2022.
# This is the same mirror torchvision falls back to.
MIRROR = "https://ossci-datasets.s3.amazonaws.com/mnist"
IMAGES_FILE = "train-images-idx3-ubyte.gz"
LABELS_FILE = "train-labels-idx1-ubyte.gz"


def _download(url: str, dest: Path) -> None:
    if dest.exists():
        print(f"[mnist] cached: {dest}")
        return
    dest.parent.mkdir(parents=True, exist_ok=True)
    print(f"[mnist] downloading {url}")
    with urllib.request.urlopen(url) as r, open(dest, "wb") as f:
        f.write(r.read())


def _read_images(gz_path: Path) -> tuple[int, int, int, bytes]:
    with gzip.open(gz_path, "rb") as f:
        data = f.read()
    magic, num, rows, cols = struct.unpack(">IIII", data[:16])
    if magic != 2051:
        raise RuntimeError(f"bad images magic {magic} in {gz_path}")
    return num, rows, cols, data[16:]


def _read_labels(gz_path: Path) -> bytes:
    with gzip.open(gz_path, "rb") as f:
        data = f.read()
    magic, num = struct.unpack(">II", data[:8])
    if magic != 2049:
        raise RuntimeError(f"bad labels magic {magic} in {gz_path}")
    return data[8:]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--samples-per-digit", type=int, default=50)
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--cache-dir", type=Path, default=Path("/tmp/mnist"))
    args = parser.parse_args()

    args.cache_dir.mkdir(parents=True, exist_ok=True)
    images_gz = args.cache_dir / IMAGES_FILE
    labels_gz = args.cache_dir / LABELS_FILE
    _download(f"{MIRROR}/{IMAGES_FILE}", images_gz)
    _download(f"{MIRROR}/{LABELS_FILE}", labels_gz)

    num, rows, cols, pixels = _read_images(images_gz)
    labels = _read_labels(labels_gz)
    if len(labels) != num:
        raise RuntimeError(f"label count {len(labels)} != image count {num}")
    img_size = rows * cols

    by_digit: dict[int, list[bytes]] = {d: [] for d in range(10)}
    for i in range(num):
        d = labels[i]
        if len(by_digit[d]) < args.samples_per_digit:
            by_digit[d].append(pixels[i * img_size:(i + 1) * img_size])
        if all(len(v) >= args.samples_per_digit for v in by_digit.values()):
            break

    args.output_dir.mkdir(parents=True, exist_ok=True)
    total = 0
    for digit, raws in by_digit.items():
        outdir = args.output_dir / str(digit)
        outdir.mkdir(parents=True, exist_ok=True)
        for i, raw in enumerate(raws):
            img = Image.frombytes("L", (cols, rows), raw)
            img.save(outdir / f"{i:03d}.png")
        total += len(raws)
        print(f"[mnist] digit {digit}: {len(raws)} → {outdir}")

    print(f"[mnist] wrote {total} PNGs under {args.output_dir}")
    print("[mnist] copy or symlink this into shared/unity_arena/Assets/Resources/MNIST/")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
