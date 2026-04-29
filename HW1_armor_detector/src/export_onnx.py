"""ONNX export for the HW1 detector.

Filled, no TODOs. The script takes a trained `.pt` and produces a
`.onnx` that the C++ inferer in `source/inferer.cpp` consumes.

Output graph:
    input:  images     [1, 3, H, W]   float32, NCHW, range [0, 1]
    output: detections [1, 17, H/16, W/16] float32
            channel layout: 4 box | 8 kpt | 4 cls (raw) | 1 obj (raw)

The decoder side (sigmoid on box[0:2] and obj, exp on box[2:4],
softmax on cls, NMS) lives in the C++ inferer so the ONNX graph stays
hardware-agnostic.
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

import onnx
import torch

sys.path.insert(0, str(Path(__file__).resolve().parent))
from model import ArmorDetector


class ExportWrapper(torch.nn.Module):
    """Thin wrapper that returns the flat NCHW tensor expected by the
    inferer instead of the structured `DetectorOutput` dataclass."""

    def __init__(self, model: ArmorDetector) -> None:
        super().__init__()
        self.model = model

    def forward(self, images: torch.Tensor) -> torch.Tensor:
        out = self.model(images)
        return out.to_flat()


def main() -> int:
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument("--weights", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--height", type=int, default=720)
    parser.add_argument("--width", type=int, default=1280)
    parser.add_argument("--opset", type=int, default=17)
    args = parser.parse_args()

    state = torch.load(args.weights, map_location="cpu", weights_only=False)
    model = ArmorDetector(pretrained_backbone=False)
    model.load_state_dict(state["model"])
    model.eval()
    wrapped = ExportWrapper(model)

    dummy = torch.zeros((1, 3, args.height, args.width), dtype=torch.float32)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    torch.onnx.export(
        wrapped,
        (dummy,),
        str(args.out),
        input_names=["images"],
        output_names=["detections"],
        dynamic_axes={
            "images": {0: "batch"},
            "detections": {0: "batch"},
        },
        opset_version=args.opset,
        do_constant_folding=True,
    )

    # Sanity: load it back, run shape inference, and check the model
    # validates.
    proto = onnx.load(str(args.out))
    onnx.checker.check_model(proto)
    inferred = onnx.shape_inference.infer_shapes(proto)
    onnx.checker.check_model(inferred)

    print(f"exported {args.out}")
    print(f"  input:  images [1, 3, {args.height}, {args.width}]")
    print(f"  output: detections [1, 17, {args.height // 16}, {args.width // 16}]")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
