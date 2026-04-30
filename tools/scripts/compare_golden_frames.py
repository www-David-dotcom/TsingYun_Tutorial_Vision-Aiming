"""Compare per-seed-per-pose frames between two engines via SSIM.

Usage:
  uv run python tools/scripts/compare_golden_frames.py \
      --godot-dir tests/golden_frames/ \
      --unity-dir tests/golden_frames/ \
      --threshold 0.95
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from skimage.metrics import structural_similarity as ssim


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--godot-dir", type=Path, required=True)
    parser.add_argument("--unity-dir", type=Path, required=True)
    parser.add_argument("--threshold", type=float, default=0.95)
    args = parser.parse_args()

    failures = 0
    for godot_path in sorted(args.godot_dir.glob("godot_seed_*.png")):
        unity_name = godot_path.name.replace("godot_", "unity_", 1)
        unity_path = args.unity_dir / unity_name
        if not unity_path.exists():
            print(f"[FAIL] missing Unity counterpart: {unity_path}", file=sys.stderr)
            failures += 1
            continue
        left = np.array(Image.open(godot_path).convert("RGB"))
        right = np.array(Image.open(unity_path).convert("RGB"))
        score = ssim(left, right, channel_axis=2)
        status = "OK" if score >= args.threshold else "FAIL"
        if status == "FAIL":
            failures += 1
        print(f"[{status}] {godot_path.name} vs {unity_name}: SSIM={score:.4f}")

    if failures:
        print(f"\n{failures} pair(s) below threshold {args.threshold}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
