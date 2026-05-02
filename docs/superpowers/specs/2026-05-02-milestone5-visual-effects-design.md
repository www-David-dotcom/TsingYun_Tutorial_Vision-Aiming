# Milestone 5 Visual Effects Design

## Goal

Milestone 5 upgrades both `Assets/Scenes/MapA_MazeHybrid.unity` and
`Assets/Scenes/TrainingGround.unity` toward the competitive sci-fi industrial
style described in `schema.md`, while preserving the readability required by
the self-aiming assignment.

The approved direction is **Competitive Neon Industrial**: dark matte metal and
carbon surfaces, restrained cyan/magenta accent strips, readable holographic
markers, thin atmosphere, and crisp red/blue team signals.

## Creative Ambition

Readability is a guardrail, not the visual ceiling. The scene should use the
strongest polished look that still preserves the essential assignment signals.
When choosing between sparse and rich visual treatment, prefer rich treatment:
more layered materials, more purposeful accent lighting, more holographic
detail, more projectile and impact energy, and more mechanical surface depth.

The target is not a minimal detector-friendly scene. The target is an
imaginative, high-impact RoboMaster sci-fi arena where the important gameplay
cues remain recoverable. QA should catch destructive failures, such as washed
out stickers or lost red/blue identity, but it should not force the scene back
into a flat prototype look.

The arena should read as a dark cyberpunk environment. Sunlight and broad
directional illumination must be dimmed aggressively; the primary readable
light sources should be local neon strips, armor emitters, holograms, muzzle
flashes, rule markers, and controlled rim lights.

## Hard Constraints

- Armor plate base colors remain exact pure red `#FF0000` and pure blue
  `#0000FF`.
- Armor plates also get internal red/blue glow emitters using the same
  `#FF0000` and `#0000FF` colors to strengthen team identification.
- MNIST stickers remain visible from the gimbal camera and must not be washed
  out by emission, bloom-like effects, fog, or particles.
- Rule-zone colors stay semantically distinct: blue healing, red healing, and
  cyan/green boost markers.
- Projectile trails, muzzle flash, hit flash, fog, lights, and decorative
  particles cannot affect projectile physics, hit logic, match rules,
  telemetry, or training behavior.
- Visual density is encouraged. Dense lighting, particles, glow layers, surface
  details, and holographic accents are acceptable as long as the player can
  still identify teams, targets, armor plates, and rule zones in normal views.
- Visual-only additions must not introduce colliders that change movement,
  projectile impacts, armor hits, or camera placement.
- Completion requires QA evidence, not only scene edits.

## Architecture

Milestone 5 adds a small repeatable visual layer rather than manual-only scene
dressing.

### `VisualPolishProfile`

Create a ScriptableObject at `Assets/Visual/CompetitiveNeonIndustrial.asset`.
It stores approved colors, emission strengths, trail settings, atmosphere
limits, and readability thresholds. Runtime presentation scripts and editor
installers read from this profile so both scenes stay consistent.

### `ArenaReadabilityMetrics`

Add pure-C# helpers for luminance, red/blue color dominance, contrast,
overexposure, and simple profile validation. These helpers are used by EditMode
tests and optional screenshot QA scripts.

### `VisualPolishInstaller`

Add an editor installer exposed by a menu item such as
`TsingYun/Install Visual Polish`. It creates or updates shared materials,
patches prefabs with visual-only children/components, applies scene polish to
both scenes, and saves assets after required references are resolved.

The installer must be rerunnable. Generated GameObjects use predictable names
so subsequent runs update existing objects instead of duplicating them.

### Runtime Presentation Scripts

- `ArmorPlateVisual`: applies exact base color, internal red/blue glow, and a
  brief hit pulse without changing damage behavior.
- `ProjectileTrailVisual`: owns non-physics projectile trail, muzzle flash, and
  impact sparkle hooks.
- `RuleZoneMarkerRenderer`: uses profile colors while keeping marker semantics
  distinct.
- `HoloProjector`: refreshes label/material colors from the visual profile so
  junction labels remain readable under the new lighting.

## Scene Work

### `MapA_MazeHybrid`

The main arena receives the full competitive neon treatment:

- dark industrial floor and wall materials,
- layered metal, carbon, glass, and panel materials,
- cyan/magenta edge strips on important maze boundaries,
- localized glow clusters, warning glyphs, and technical decals,
- dimmed sunlight or disabled directional sun,
- controlled low-intensity key, fill, and rim lighting driven by neon colors,
- visible atmosphere and drifting particles tuned for mood rather than minimalism,
- brighter holographic junction posts,
- projectile, muzzle, impact, and armor-hit energy flashes,
- readable healing and boost zone markers,
- enough restraint to avoid hiding armor plates or target silhouettes.

### `TrainingGround`

The training scene receives a cleaner calibration-lab version of the same style:

- dark test-lab floor and measured aiming lanes,
- controlled but visually rich cyan accents,
- dim ambient/sun exposure so calibration lights and target glow dominate,
- holographic calibration grids and target-range markings,
- strong target silhouette,
- readable training UI,
- same robot, armor, projectile, hologram, and rule-marker polish as the arena.

The training scene should feel like a deliberate auto-aim calibration mode, not
a spare debug scene.

## Prefab Cohesion

The robot must read as one assembled machine after the polish pass. The current
prefab hierarchy has separate wheel meshes, a nested `Gimbal`, yaw/pitch pivots,
`Barrel`, and a transform-only `Muzzle`. Milestone 5 visually bridges these
without changing motion semantics.

Required prefab cohesion work:

- Add dark structural connector plates or struts between the chassis body and
  wheels so wheels look mounted.
- Add a turret base ring or bearing collar where `Chassis/Gimbal` meets the
  chassis, while preserving independent chassis self-rotation and gimbal yaw.
- Add a pitch housing/yoke treatment so `PitchBody`, `Barrel`, and gimbal pivot
  read as one articulated assembly.
- Add a small muzzle shroud or emissive power ring at `Muzzle` so muzzle flash
  appears attached to the barrel.
- Use consistent metal/carbon materials across chassis, wheels, gimbal, barrel,
  and connector pieces.
- Keep all added connector geometry visual-only.

## Installer Flow

The menu installer should:

1. Load or create `Assets/Visual/CompetitiveNeonIndustrial.asset`.
2. Create or update shared materials under `Assets/Materials/VisualPolish/`.
3. Patch prefabs for armor glow, robot connector visuals, projectile trails,
   muzzle flash hooks, and impact flash hooks.
4. Apply scene polish to `MapA_MazeHybrid` and `TrainingGround`.
5. Save prefabs, scenes, and assets only after required references are resolved.
6. Log a clear summary: assets created, scenes updated, warnings, and QA
   reminders.

## Failure Handling

- Missing required prefabs stop the installer with a clear error.
- Missing scene paths stop only the scene portion and report the skipped scene.
- The project currently contains HDRP assets, but Unity MCP reports the active
  rendering pipeline as Built-in. The installer must degrade safely by using
  compatible material/shader settings when HDRP-specific properties are not
  available.
- If a shader property is unavailable, set the safest base color/emission
  fallback and log the skipped advanced property.
- Visual-only generated objects must be named predictably and reused on rerun.

## QA And Acceptance

Milestone 5 is complete only when visuals are better and still usable for the
self-aiming assignment.

Required verification:

- EditMode tests for `ArenaReadabilityMetrics`: red/blue dominance, contrast
  thresholds, overexposure bounds, and profile value validation.
- Prefab/PlayMode checks that chassis have required connector visuals, armor
  plates have glow emitters, projectiles have non-physics trails, and rule
  markers use distinct colors.
- Screenshot QA for both scenes from stable camera viewpoints:
  - frames are nonblank,
  - red and blue armor pixels remain detectable,
  - armor glow does not fully wash out stickers,
  - neon accent coverage is visible but bounded,
  - major robot parts no longer appear detached from the inspected views.
- Console and compile checks after script and editor-tool changes.
- Existing gameplay tests still pass, proving the visual pass did not alter
  rules, hit logic, telemetry, or training controls.

Also create `docs/visual-polish-qa.md` with the intended look, QA commands, and
known limits.

## Non-Goals

- Do not replace the candidate-facing detector/sticker semantics.
- Do not change match rules, projectile physics, damage, heat, hit rate,
  training telemetry, or the TCP frame/control contracts.
- Do not make an HDRP-only implementation that breaks when the active pipeline
  remains Built-in.
