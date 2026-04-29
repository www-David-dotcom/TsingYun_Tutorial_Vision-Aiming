# Real-world holdout frames

Empty in-tree. The 100-frame holdout the team has labeled lives on the
public OSS bucket as `real-holdout-frames-v1` (see
`shared/assets/manifest.toml`); pull it lazily with:

```bash
uv run python shared/scripts/fetch_assets.py --only real-holdout-frames-v1
```

That populates `out/assets/HW1/real_holdout/` with `frames/*.png` and
`labels/*.json`. Symlink it here if your training script expects the
dataset under `HW1_armor_detector/data/real_holdout/`:

```bash
ln -s ../../../out/assets/HW1/real_holdout HW1_armor_detector/data/real_holdout
```

The placeholder digest in the manifest is zeroed-out — the team has
not uploaded the real holdout yet (Stage 3 ships the wiring; the
content lands during the HW1 calibration pass).
