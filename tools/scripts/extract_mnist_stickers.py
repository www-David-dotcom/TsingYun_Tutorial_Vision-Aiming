"""Extract N samples per digit from MNIST as PNG stickers.

Both engines render an MNIST digit on each armor plate as the robot's
"number tag". This script dumps the first N samples of each digit (0-9)
from torchvision's MNIST training set into a flat directory:

  shared/sticker_assets/mnist/{digit}/{idx:03d}.png

Both engines load from there at episode reset:
  - Unity: copy or symlink shared/sticker_assets/mnist/ into
    shared/unity_arena/Assets/Resources/MNIST/ (Resources.Load reads here).
  - Godot: copy or symlink into shared/godot_arena/assets/mnist/ (load()
    via res:// reads here).

Required deps not yet in pyproject.toml: torchvision, pillow. Add via
`uv add --dev torchvision` before running.

Usage:
  uv run python tools/scripts/extract_mnist_stickers.py
  uv run python tools/scripts/extract_mnist_stickers.py --samples-per-digit 100
"""

from __future__ import annotations

import argparse
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
DEFAULT_OUT = REPO_ROOT / "shared" / "sticker_assets" / "mnist"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--samples-per-digit", type=int, default=50)
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--cache-dir", type=Path, default=Path("/tmp/mnist"))
    args = parser.parse_args()

    from torchvision.datasets import MNIST  # type: ignore[import-untyped]

    ds = MNIST(root=str(args.cache_dir), train=True, download=True)

    by_digit: dict[int, list] = {d: [] for d in range(10)}
    for img, label in ds:
        if len(by_digit[label]) < args.samples_per_digit:
            by_digit[label].append(img)
        if all(len(v) >= args.samples_per_digit for v in by_digit.values()):
            break

    args.output_dir.mkdir(parents=True, exist_ok=True)
    total = 0
    for digit, imgs in by_digit.items():
        outdir = args.output_dir / str(digit)
        outdir.mkdir(parents=True, exist_ok=True)
        for i, img in enumerate(imgs):
            (outdir / f"{i:03d}.png").parent.mkdir(parents=True, exist_ok=True)
            img.save(outdir / f"{i:03d}.png")
        total += len(imgs)
        print(f"[mnist] digit {digit}: {len(imgs)} → {outdir}")

    print(f"[mnist] wrote {total} PNGs under {args.output_dir}")
    print("[mnist] copy or symlink this into:")
    print("  - shared/unity_arena/Assets/Resources/MNIST/")
    print("  - shared/godot_arena/assets/mnist/")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
