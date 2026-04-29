# Arena art assets

Stage 2 ships an art-light arena: the chassis, gimbal, plates, and
projectiles all use Godot primitive meshes (`BoxMesh`, `CylinderMesh`,
`SphereMesh`) with `StandardMaterial3D`. The map is a single 20x20
floor plane plus a procedural sky.

The plan calls for a richer pass at the visual-review milestone
(`IMPLEMENTATION_PLAN.md` Stage 7); when that lands, this directory
will hold:

* `kenney_scifi/` — CC0 sci-fi prop pack from
  [Kenney Sci-Fi](https://kenney.nl/assets/sci-fi-rts), used for
  walls, crates, and arena dressing.
* `shaders/` — armor-glow, muzzle-flash, and impact-decal shaders.
* `icons/` — Hero / Engineer / Standard / Sentry SVGs rendered onto
  each armor plate in classification mode.

For Stage 2 these subdirectories exist as empty placeholders; the
visual pass picks them up without further restructuring.

## Asset hosting

Per `IMPLEMENTATION_PLAN.md` resolved decision 2, none of this lands
in git. When the visual pass ships, the assets are uploaded to
`oss://tsingyun-aiming-hw-public/assets/<name>/` via
`shared/scripts/push_assets.py`, and `shared/assets/manifest.toml`
gets a row per asset bundle. The arena project then references the
files via `res://assets/...`, with `fetch_assets.py` materializing
the on-disk copy before Godot is launched.
