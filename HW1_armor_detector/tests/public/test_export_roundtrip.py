"""ONNX export → onnx.checker round-trip.

Exercises export_onnx.py end-to-end on a tiny model: train a 1-step
no-op pass, dump a checkpoint, export to ONNX, validate the graph.
The whole module skips when torch / onnx aren't installed.
"""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

import pytest

torch = pytest.importorskip("torch")
pytest.importorskip("onnx")
pytest.importorskip("torchvision")

REPO_ROOT = Path(__file__).resolve().parents[3]
HW1_ROOT = REPO_ROOT / "HW1_armor_detector"
sys.path.insert(0, str(HW1_ROOT / "src"))

from model import ArmorDetector  # noqa: E402


def test_export_produces_valid_onnx(tmp_path: Path) -> None:
    weights_path = tmp_path / "ckpt.pt"
    onnx_path = tmp_path / "model.onnx"

    # Tiny model — no training, just save initialised weights so
    # export_onnx can load them back.
    model = ArmorDetector(pretrained_backbone=False)
    torch.save({"model": model.state_dict(), "args": {}}, weights_path)

    cmd = [
        sys.executable, str(HW1_ROOT / "src" / "export_onnx.py"),
        "--weights", str(weights_path),
        "--out", str(onnx_path),
        "--height", "320",
        "--width", "320",
    ]
    result = subprocess.run(cmd, capture_output=True, text=True)
    assert result.returncode == 0, f"export_onnx failed: {result.stderr}"
    assert onnx_path.exists()

    import onnx
    proto = onnx.load(str(onnx_path))
    onnx.checker.check_model(proto)
    # The flat output must have 17 channels (4 box + 8 kpt + 4 cls + 1 obj).
    out_dims = [
        d.dim_value or d.dim_param
        for d in proto.graph.output[0].type.tensor_type.shape.dim
    ]
    assert out_dims[1] == 17, out_dims
