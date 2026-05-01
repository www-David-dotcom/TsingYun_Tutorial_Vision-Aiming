# Stage 12c (lean) — Visual Reform Design

Status: design / awaiting user review
Author: planning agent (Claude Code, Opus 4.7) on behalf of David Shi
Implementing agent: TBD (separate session)
Path B per 2026-05-01 brainstorm — minimum-refactoring "lean" cut of the 15 tasks in the original Stage 12c plan

## 0. TL;DR

Three implementing-agent days. Nine sub-stages, layered for minimum refactoring:

1. Retire `shared/godot_arena/` (now Unity is the only engine).
2. Re-enable HDRP, migrate every existing material, set up a global Volume profile with ACES tonemapping.
3. Lighting — sun + neon Tube Lights along corridors + volumetric fog.
4. PlateEmission Shader Graph + blue/red materials.
5. HoloProjector Shader Graph for JCT bollard cones.
6. MuzzleFlash VFX Graph triggered on each fired bullet.
7. ImpactSpark VFX Graph + scorch decal triggered on collision / armor hit.
8. Diegetic LOS-gated `⚠ ENEMY` warning glyph above visible enemy chassis.
9. Re-baseline Tier 3 against the new visual contract; tag `v1.8-unity-art-lean`.

Deferred to a future polish sub-stage (game rules redesign blocks some): EnergyShield Shader Graph, GlassCircuit Shader Graph, DustMote VFX, screen-space HUD prefab, build-time `--ui={full,diegetic}` toggle, HDRPAsset showcase/headless variants, baked lighting, Tier 4 damage regression.

## 1. Project state at 12c entry

The implementing agent should treat this section as authoritative — it captures everything decided in 12a / 12b that 12c must NOT break.

### 1.1 Repo coordinates
- Repo path: `/Users/davidshi/projects/Aiming_HW`
- Branch: `stage12/unity-reform`
- Latest tag: `v1.7-unity-geometry`
- Unity project root: `shared/unity_arena/`
- Unity version: 6000.3.14f1 (Unity 6 LTS)

### 1.2 Render pipeline state
HDRP packages (17.3.0) installed in `Packages/manifest.json`. Pipeline is **disabled** — Built-in is currently active. `Assets/Rendering/HDRPAsset.asset` exists on disk waiting to be re-assigned to Graphics + Quality settings (deferred from 12b per commit `4dd9904`).

### 1.3 Canonical scene & prefabs
- Scene: `Assets/Scenes/MapA_MazeHybrid.unity` (Build Settings index 0)
- Prefabs in `Assets/Prefabs/`:
  - `Chassis.prefab` — pedestal frustum body + 4 wheels + 4 ArmorPlate instances + nested Gimbal + StickerLoader
  - `Gimbal.prefab` — Yoke / PitchBody / Barrel / Muzzle marker / GimbalCamera
  - `Projectile.prefab` — Sphere mesh + Rigidbody (mass 0.0032, kinematic off) + SphereCollider on Layer 8
  - `ArmorPlate.prefab` — Cube body + child Sticker quad + ArmorPlate MB
  - `HoloProjector.prefab` — Cylinder bollard + EmissionCone + LabelCanvas (TextMeshPro)

### 1.4 Conventions locked in 12b — must NOT be undone
- Unity standard `+Z` forward across the whole arena (cameras, muzzles, bullets, ComputeShot, all spawn rotations).
- HP per-robot (`Chassis.MaxHp = 200`); plates are trigger surfaces only — no per-plate HP.
- Number tag `int 0–9` per chassis; `StickerLoader` picks a deterministic MNIST sample at episode reset.
- 5 Hz fire rate limit (`ArenaMain.BulletsPerSecond = 5`); `EnvPushFire` enqueues, `Update.DrainShotQueue` paces.
- GimbalCamera at PitchPivot.local `(-0.01, 0, 0.2)`, no rotation; FOV 60, near 0.05, far 60.
- Layer Collision Matrix: Projectile (8) ✗ Projectile, Projectile ✗ Chassis (9). Other layers default-on.
- `TcpFramePub` Y-flips frames on readback (Apple Silicon default), depth-buffer 24-bit.
- `ArenaMain.EnvReset` reads `SpawnPointBlue.eulerAngles.y` / `SpawnPointRed.eulerAngles.y` for spawn yaws (NOT hardcoded).

### 1.5 Conformance gates active at 12c entry
- **Tier 1** — `tests/test_arena_wire_format.py` (CI-runnable pytest, no simulator)
- **Tier 2** — `tools/scripts/smoke_arena.py` (manual; requires Unity in Play mode)
- **Tier 3** — `tests/intra_unity_determinism.py` + 25 baseline PNGs at `tests/golden_frames_unity_baseline/`. MAD threshold 5.0 / 255. (Manual; requires Unity in Play.)

### 1.6 Visual state at entry
- Built-in pipeline; Synty Polygon Sci-Fi pack imported (gitignored under `Assets/Synty/PolygonSciFiCity/`).
- `Default-Material` everywhere on chassis primitives; `StickerMat.mat` (Unlit/Texture) on plate stickers; placeholder Built-in materials on HoloProjector.
- One Sun directional light in the scene (HDAdditionalLightData component still attached from earlier HDRP authoring; field values approximate).

### 1.7 Memory / preferences
- Unity-only; Godot side has been a frozen reference, no parity edits to `shared/godot_arena/`. **Sub-stage 12c.0 retires it permanently.**
- Game rules being redesigned by user; Tier 4 (damage regression) and screen-space HUD are blocked until rules settle.
- User implementing the Editor-side work via a separate agent — design spec must be self-contained for hand-off.

## 2. Goals

In delivery order:

1. **12c.0 — Godot retirement.** After verifying Tier 1–3 still green, delete `shared/godot_arena/`. Smoke harness, wire-format test, and MNIST extractor become Unity-only.
2. **12c.1 — HDRP foundation (scope B).** Re-enable HDRP pipeline assignment. Convert all current materials to HDRP/Lit. Configure `Volume_MapA.asset` with ACES tonemapping, automatic exposure, default sky.
3. **12c.2 — Lighting setup.** One key directional Sun (5600 K, 100 000 lux). ~30 HDRP Tube Lights along corridors (cyan / magenta alternating). Local Volumetric Fog volumes inside corridor segments.
4. **12c.3 — PlateEmission Shader Graph.** HDRP/Lit Shader Graph with team color + animated 2 Hz scanline + glyph slot + HP-driven brightness. Replaces `ArmorPlate.RefreshGlow` runtime material edits.
5. **12c.4 — HoloProjector Shader Graph.** HDRP/Unlit transparent shader with vertical scrolling noise + edge fresnel. Animates the JCT bollard cones.
6. **12c.5 — MuzzleFlash VFX Graph.** Spark burst + transient point light at the muzzle on each fired bullet. Spawned from `ArenaMain.SpawnSingleProjectile`.
7. **12c.6 — ImpactSpark VFX Graph + scorch decal.** Spark burst at projectile collision; decal projector adds a fading scorch ring on wall hits (no decal on plates).
8. **12c.7 — Diegetic `⚠ ENEMY` warning glyph.** World Space Canvas + LOS-gated controller. Visible only when the enemy is < 12 m and the gimbal camera has clear LoS.
9. **12c.8 — Re-baseline Tier 3 + tag.** Tier 3 baseline regenerated; Tier 1+2 verified; tag `v1.8-unity-art-lean`.

### Acceptance gate for `v1.8-unity-art-lean`
- All Tier 1, Tier 2, Tier 3 green (Tier 3 against the new baseline captured at end of 12c).
- `shared/godot_arena/` no longer exists in the working tree; no `--engine=godot` codepaths in scripts.
- All EditMode tests still green (no `_state` test breakage from runtime ArmorPlate changes).
- MapA scene visibly upgraded:
  - HDRP-rendered Synty buildings (no pink), neon strips lit, volumetric godrays through fog
  - Plates emit team-colored scanlines; glyph slot present (texture can be solid white for now until icon set is sourced)
  - HoloProjector cones animate
  - Muzzle flashes on each fired bullet; sparks at impact; scorch decal on wall hits, fades after 12 s
  - `⚠ ENEMY` glyph appears above RedChassis when BlueChassis has clear LoS within 12 m

## 3. Sub-stage ordering & dependencies

Linear order — each sub-stage commits before the next starts:

```
12c.0  Godot retirement              (no Unity deps)
  ↓
12c.1  HDRP foundation                (pipeline re-enable + materials migration)
  ↓
12c.2  Lighting setup                 (HDRP Tube Lights + volumetric fog)
  ↓
12c.3  PlateEmission Shader Graph     (plate body emission)
  ↓
12c.4  HoloProjector Shader Graph     (JCT bollard cone)
  ↓
12c.5  MuzzleFlash VFX Graph          (fire spark burst)
  ↓
12c.6  ImpactSpark VFX Graph          (collision spark + decal)
  ↓
12c.7  Diegetic warning glyph         (LOS-gated ⚠ ENEMY)
  ↓
12c.8  Re-baseline Tier 3 + tag       (visuals stable)
```

### Rationale
- 12c.0 first — Godot retirement is unrelated to visual work; clean it before piling on. Shrinks the codebase the implementing agent has to navigate.
- 12c.1 before everything HDRP-related — Shader Graphs and VFX Graphs need the HDRP pipeline active.
- 12c.2 before 12c.3-6 — plate / holo emission shaders should be tuned against the *final* lighting.
- 12c.3 before 12c.4 — plates are the HW1 detection target; highest curriculum value.
- 12c.5 before 12c.6 — both VFX share authoring patterns; MuzzleFlash establishes them.
- 12c.7 after 12c.6 — consolidates the script-edit phase (12c.5 + 6 + 7 each touch a few `.cs` files; doing them in sequence avoids ping-ponging back into shader/VFX work after writing C#).
- 12c.8 last — Tier 3 baseline must reflect the *finished* visual contract.

### Tier 3 re-baseline strategy
The baseline at `tests/golden_frames_unity_baseline/` reflects the v1.7 (Built-in pipeline, no shader graphs, no VFX, no fog) state. Every sub-stage 12c.1–7 will fail Tier 3 against this baseline — that's *expected and ignored* during 12c. Tier 1 and Tier 2 stay valid throughout (no wire-format changes); the agent SHOULD run them after each sub-stage as quick sanity checks. Only at 12c.8 does the agent rebuild the Tier 3 baseline.

### Code-change touchpoints

| Sub-stage | Files modified | Scope |
|---|---|---|
| 12c.0 | `tools/scripts/smoke_arena.py`, `tests/test_arena_wire_format.py`, `tools/scripts/extract_mnist_stickers.py`, docs | Remove `--engine=godot`, drop pytest parametrize, prune docs |
| 12c.1 | `ProjectSettings/GraphicsSettings.asset`, `ProjectSettings/QualitySettings.asset`, every `.mat` under `Assets/Materials/` and `Assets/Synty/`, `Assets/Volumes/Volume_MapA.asset` (new), `Assets/Settings/HDRPAsset.asset` (configure values) | HDRP RPA assignment, bulk material conversion, Volume profile creation |
| 12c.2 | `Assets/Scenes/MapA_MazeHybrid.unity` (Tube Lights + fog volumes added), `Volume_MapA.asset` (Fog override values) | Light placement + Volume tuning |
| 12c.3 | `Assets/Shaders/PlateEmission.shadergraph` (new), 2 plate materials (new), `Assets/Prefabs/ArmorPlate.prefab`, `Assets/Scripts/ArmorPlate.cs`, `Assets/Scripts/Chassis.cs` | Author shader, bind materials in `AssignArmorMetadata`, replace `RefreshGlow`'s color-lerp with shader property write |
| 12c.4 | `Assets/Shaders/HoloProjector.shadergraph` (new), `Assets/Materials/HoloProjector_Cone.mat` (new), `Assets/Prefabs/HoloProjector.prefab` | Author shader, replace temp StickerMat reference on EmissionCone |
| 12c.5 | `Assets/VFX/MuzzleFlash.vfx` (new), `Assets/Prefabs/MuzzleFlash.prefab` (new), `Assets/Scripts/ArenaMain.cs` | Author VFX, instantiate prefab in `SpawnSingleProjectile` |
| 12c.6 | `Assets/VFX/ImpactSpark.vfx` (new), `Assets/Prefabs/ImpactSpark.prefab` (new), `Assets/Decals/ImpactScorch.mat` (new HDRP Decal), `Assets/Scripts/Projectile.cs`, `Assets/Scripts/ArmorPlate.cs` | Author VFX + decal, instantiate at `Consume` / `OnTriggerEnter` |
| 12c.7 | `Assets/Prefabs/WarningGlyph.prefab` (new), `Assets/Scripts/WarningGlyphController.cs` (new), `Assets/Scenes/MapA_MazeHybrid.unity` | LOS-gated controller via `Physics.Raycast`, scene drop |
| 12c.8 | `tests/golden_frames_unity_baseline/*.png` (regenerated), `git tag v1.8-unity-art-lean` | Baseline rebuilt; tag created |

### Commit strategy
One commit per sub-stage. Commit prefixes: `chore(stage12c)` for 12c.0, `feat(stage12c)` for 12c.1–7, `test(stage12c)` for 12c.8 (the baseline regeneration). 12c.0 may need 2–3 commits since it touches many files (one per file class — code, tests, docs).

## 4. Per-sub-stage technical specs

This is the inheritance contract for the implementing agent. Acceptance criteria are objective; the agent confirms each before opening the next sub-stage.

### 4.0 — 12c.0: Godot retirement

**Steps**:
- `git rm -rf shared/godot_arena/` (one commit).
- `tools/scripts/smoke_arena.py`: drop `--engine` argparse flag entirely; method calls hard-code Unity host/port. The line printing engine in `[smoke]` becomes Unity-only.
- `tests/test_arena_wire_format.py`: remove `@pytest.mark.parametrize("engine", ["godot", "unity"])` decorator and any `if engine == "godot"` branches; tests run against Unity only.
- `tools/scripts/extract_mnist_stickers.py`: drop the trailing print line that mentions `shared/godot_arena/assets/mnist/`.
- `grep -rn godot_arena docs/ IMPLEMENTATION_PLAN.md README.md schema.md` and update or remove references. Keep historical references in `docs/superpowers/plans/2026-04-30-arena-art-vision-reform-stage12.md` (that's a frozen plan record).
- Append `v1.7.1 — godot_arena removed` line to `docs/CHANGELOG.md`.

**Acceptance**:
- `uv run pytest tests/test_arena_wire_format.py` green.
- `uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 30` produces a `[finish]` line.
- `grep -rn godot_arena .` returns only historical plan files (not active code/scripts).
- Working tree no longer contains `shared/godot_arena/`.

### 4.1 — 12c.1: HDRP foundation

**Steps**:
- Editor: `Edit → Project Settings → Graphics → Default Render Pipeline` → assign `Assets/Rendering/HDRPAsset.asset`.
- Editor: `Edit → Project Settings → Quality` → for each Quality Level, assign the same HDRPAsset.
- Editor: `Window → Rendering → Render Pipeline Converter → Built-in to HDRP` → run all converters (Materials, Animation Clips, ReadOnly Materials, Render Settings, etc.).
- File-edit: configure `HDRPAsset.asset` — Camera FrameSettings: Lit Shader Mode `Both`; Volumetrics On; Tube/Area Lights On; Decal Layers On.
- File-edit: create `Assets/Volumes/Volume_MapA.asset` (Volume Profile) with overrides:
  - `Visual Environment` → Sky Type: HDRI Sky
  - `HDRI Sky` → assign Unity's bundled `Default-DefaultSkyboxHDRI` or any daytime HDRI
  - `Exposure` → Mode: Automatic; Limit Min: 8; Max: 16
  - `Tonemapping` → Mode: ACES
- Editor: in `MapA_MazeHybrid.unity`, add a Global Volume GameObject named `MapAVolume` at origin → Profile = `Volume_MapA.asset`.

**Acceptance**:
- Game view + Scene view render the maze without pink materials.
- ArenaMain TCP startup logs unchanged.
- Tier 1 + Tier 2 still green (frame contents will differ from v1.7 — that's expected and Tier 3 stays *unverified* until 12c.8).

### 4.2 — 12c.2: Lighting

**Steps**:
- Editor: select existing `Sun` GameObject. HDAdditionalLightData fields:
  - Type: Directional
  - Use Color Temperature: on, Temperature 5600 K
  - Intensity: 100 000 lux (HDRP daylight)
  - Shadow Map Resolution: 4096; Shadow Quality: High
- File-edit: `Volume_MapA.asset` add overrides:
  - `Fog` → Enabled: on; State: Enabled; Color Mode: Sky Color; Mean Free Path: 1000 m; Anisotropy: −0.2
- Editor: in MapA scene under `Lighting/`, create empty `NeonStrips` parent. Add ~30 HDRP Tube Lights:
  - Length 2.0 m; Color alternating `#00DDFF` (cyan) and `#FF00DD` (magenta)
  - Intensity 1000 cd; Range 6 m
  - Position along corridor ceilings (Y ≈ 2.4) every ~3 m
  - Cast Shadows: off (perf — fog gives the volumetric illusion)
- Editor: add 3× `Local Volumetric Fog` volumes inside the corridor segments — Volume Bounds matching corridor extents; Density 0.012; Anisotropy −0.2.

**Acceptance**:
- Game view shows neon strips lighting the corridors.
- Volumetric fog catches sun + tube light beams (visible godrays at corridor entrances).
- Sun-from-above casts plausible shadow on chassis.
- Frame rate ≥ 30 fps in the Editor on the maintainer's M2/M3 Mac.

### 4.3 — 12c.3: PlateEmission Shader Graph

**Steps**:
- Author `Assets/Shaders/PlateEmission.shadergraph` (HDRP/Lit, Surface = Opaque). Properties:
  - `_TeamColor` Color (HDR)
  - `_GlyphTexture` Texture2D, default white
  - `_BaseEmissionEnergy` Float, default 1.5
  - `_GlyphEmissionEnergy` Float, default 2.5
  - `_ScanlineSpeed` Float, default 2.0 (Hz)
  - `_ScanlineWidth` Float, default 0.05
  - `_DamageRatio` Float, default 1.0 (driven by `Chassis.Hp / MaxHp`)
- Graph behavior:
  - `glyph_contrib` = `Sample(_GlyphTexture, UV).a * _TeamColor * _GlyphEmissionEnergy`
  - `scanline_mask` = `(Sin(Time * _ScanlineSpeed * 2π + UV.y * 30) > (1 − _ScanlineWidth)) ? 1 : 0`
  - `scanline_contrib` = `scanline_mask * _TeamColor * _BaseEmissionEnergy`
  - Emission = `(_TeamColor * _BaseEmissionEnergy * _DamageRatio) + glyph_contrib + scanline_contrib`
  - Base Color = `_TeamColor * 0.3`; Smoothness 0.4; Metallic 0.2
- Materials:
  - `Assets/Materials/PlateEmission_Blue.mat` `_TeamColor=(0.12, 0.42, 1.0, 1)`
  - `Assets/Materials/PlateEmission_Red.mat` `_TeamColor=(1.0, 0.20, 0.25, 1)`
- File-edit `Chassis.cs:AssignArmorMetadata`: add public Inspector fields `BluePlateMat` / `RedPlateMat`; assign `plate.plateRenderer.material = (Team == "red" ? RedPlateMat : BluePlateMat)`.
- File-edit `ArmorPlate.cs:RefreshGlow(float t)`: replace existing color-lerp body with `plateRenderer.material.SetFloat("_DamageRatio", t)`.
- File-edit `ArmorPlate.prefab`: default plate material `PlateEmission_Blue.mat`.
- Editor: in MapA, on each Chassis instance, drag `PlateEmission_Blue.mat` into `BluePlateMat`, `PlateEmission_Red.mat` into `RedPlateMat`.

**Acceptance**:
- BlueChassis plates glow blue with a 2 Hz scanline; RedChassis plates glow red.
- A bullet hit reduces `_DamageRatio` and the plates dim accordingly.
- 25 EditMode tests still green.

### 4.4 — 12c.4: HoloProjector Shader Graph

**Steps**:
- Author `Assets/Shaders/HoloProjector.shadergraph` (HDRP/Unlit, Surface = Transparent, Render Face = Both). Properties:
  - `_BaseColor` Color (HDR), default `(0.0, 0.7, 1.0, 1)` cyan
  - `_ScrollSpeed` Float, default 0.2 (UV/sec)
  - `_FresnelPower` Float, default 1.5
  - `_NoiseScale` Float, default 8.0
  - `_EmissionEnergy` Float, default 4.0
  - `_AlphaMul` Float, default 0.6
- Graph: `noise = SimpleNoise(UV + Time * (0, _ScrollSpeed)) * _NoiseScale`; `fresnel = Fresnel(View, Normal, _FresnelPower)`; `e = noise * fresnel * _BaseColor * _EmissionEnergy`; Emission = `e`; Alpha = `noise * fresnel * _AlphaMul`.
- Material: `Assets/Materials/HoloProjector_Cone.mat`.
- File-edit `HoloProjector.prefab` EmissionCone material reference: `HoloProjector_Cone.mat`.

**Acceptance**:
- 3× HoloProjectors in MapA show animated cyan emission cones with vertical scroll + edge fresnel.
- No frame-rate hit > 1 fps from the change.

### 4.5 — 12c.5: MuzzleFlash VFX Graph

**Steps**:
- Author `Assets/VFX/MuzzleFlash.vfx` (HDRP VFX Graph). Output config:
  - One-shot 30-particle burst at spawn
  - Lifetime 0.05 s; size 0.01 m fading to 0
  - Initial velocity: cone forward, magnitude 5–15 m/s
  - Color over Lifetime: white → orange → fade
- Add transient Point Light to the prefab: intensity 4 cd, color `#FFE0A0`, range 2 m, lifetime 0.05 s (auto-destroy via `OneShotDestroy.cs`).
- `Assets/Prefabs/MuzzleFlash.prefab`: empty GO with VisualEffect component referencing the .vfx + the Point Light + a `OneShotDestroy.cs` MB that calls `Destroy(gameObject, 0.5f)` in `Start`.
- New `Assets/Scripts/OneShotDestroy.cs`:
  ```csharp
  using UnityEngine;
  namespace TsingYun.UnityArena {
      public class OneShotDestroy : MonoBehaviour {
          public float Lifetime = 0.5f;
          private void Start() => Destroy(gameObject, Lifetime);
      }
  }
  ```
- File-edit `ArenaMain.cs`:
  - Add `public GameObject MuzzleFlashPrefab;` Inspector field
  - Add `public Transform VfxRoot;` Inspector field (parent for short-lived VFX so the Hierarchy stays organized; if unassigned, spawns go to scene root)
  - In `SpawnSingleProjectile`, after `p.Arm(...)`: `if (MuzzleFlashPrefab != null) Instantiate(MuzzleFlashPrefab, spec.Position, spec.Rotation, VfxRoot);`
- Editor: in MapA, create empty `VfxRoot` GameObject at origin (sibling of `ProjectileRoot`). Drag `MuzzleFlash.prefab` into `ArenaMain.MuzzleFlashPrefab` slot, drag the `VfxRoot` GameObject into `ArenaMain.VfxRoot` slot.
- Note for sub-stage 12c.6 (next): `Projectile.cs` and `ArmorPlate.cs` will need access to the same `VfxRoot`. Since prefab Inspector fields can't reference scene objects, those scripts will look up the root by name at runtime: `GameObject.Find("VfxRoot")?.transform`. Cache once in `Start`.

**Acceptance**:
- Each fired bullet spawns a brief muzzle flash visible in the gimbal POV.
- Hierarchy under ProjectileRoot stays clean after the burst (instances self-destruct).

### 4.6 — 12c.6: ImpactSpark VFX Graph + scorch decal

**Steps**:
- Author `Assets/VFX/ImpactSpark.vfx` (HDRP VFX Graph): 20 particles, lifetime 0.3 s, hemisphere outward velocity 2–6 m/s, yellow → orange → black gradient.
- `Assets/Decals/ImpactScorch.mat` (HDRP Decal): black-ring radial gradient 256×256 PNG (or use Unity's bundled scorch sample texture). Decal size 0.2 × 0.2 × 0.05 m.
- New `Assets/Scripts/DecalFader.cs`:
  ```csharp
  using UnityEngine;
  using UnityEngine.Rendering.HighDefinition;
  namespace TsingYun.UnityArena {
      [RequireComponent(typeof(DecalProjector))]
      public class DecalFader : MonoBehaviour {
          public float Lifetime = 12f;
          public float FadeStart = 8f;
          private DecalProjector _proj;
          private float _t;
          private void Awake() => _proj = GetComponent<DecalProjector>();
          private void Update() {
              _t += Time.deltaTime;
              if (_t > FadeStart) _proj.fadeFactor = Mathf.Clamp01(1f - (_t - FadeStart) / (Lifetime - FadeStart));
              if (_t > Lifetime) Destroy(gameObject);
          }
      }
  }
  ```
- Two prefab variants:
  - `Assets/Prefabs/ImpactSpark_Wall.prefab` — empty GO + VisualEffect + child DecalProjector + `OneShotDestroy.cs` (12 s) on the VFX GO + `DecalFader.cs` on the decal GO.
  - `Assets/Prefabs/ImpactSpark_Plate.prefab` — empty GO + VisualEffect + `OneShotDestroy.cs` (1 s). NO decal — plates are too small for a 0.2 m decal and the chassis pedestal would receive ugly Z-fighting.
- File-edit `Projectile.cs`:
  - Add Inspector field `public GameObject WallImpactPrefab;`
  - Cache VfxRoot in `Start` via `_vfxRoot = GameObject.Find("VfxRoot")?.transform;` (prefab can't reference scene objects directly)
  - In `Consume(string reason)`, before `Destroy(gameObject)`: `if (reason.StartsWith("hit_wall") && WallImpactPrefab != null) Instantiate(WallImpactPrefab, transform.position, Quaternion.LookRotation(_rb.linearVelocity.normalized), _vfxRoot);`
- File-edit `ArmorPlate.cs:OnTriggerEnter`:
  - Add Inspector field `public GameObject PlateImpactPrefab;`
  - Cache VfxRoot the same way in `Start`
  - On successful hit (damage > 0): `if (PlateImpactPrefab != null) Instantiate(PlateImpactPrefab, other.transform.position, Quaternion.LookRotation(other.transform.position - transform.position), _vfxRoot);`
- Editor: drag `ImpactSpark_Wall.prefab` into `Projectile.WallImpactPrefab` slot on the Projectile prefab. Drag `ImpactSpark_Plate.prefab` into `ArmorPlate.PlateImpactPrefab` slot on the ArmorPlate prefab.

**Acceptance**:
- Bullets hitting walls show sparks + a scorch ring that fades over 8–12 s.
- Bullets hitting plates show sparks (decal optional based on chosen variant).
- No leaked GameObjects after 30 s of firing.

### 4.7 — 12c.7: Diegetic LOS-gated `⚠ ENEMY` glyph

**Steps**:
- `Assets/Prefabs/WarningGlyph.prefab`: World Space Canvas (RectTransform 60 × 20, scale 0.01); child TextMeshProUGUI "⚠ ENEMY" with color `#FFD966`; pulse animation via shader or `SimpleAlphaPulse.cs` (alpha lerp 0.6 ↔ 1.0 at 1 Hz).
- New `Assets/Scripts/WarningGlyphController.cs`:
  ```csharp
  using UnityEngine;
  namespace TsingYun.UnityArena {
      public class WarningGlyphController : MonoBehaviour {
          public Chassis Self;
          public Chassis Enemy;
          public Camera GimbalCamera;
          public float MaxDistance = 12f;
          public float HoverHeight = 1.4f;
          public Canvas GlyphCanvas;

          private void LateUpdate() {
              if (Self == null || Enemy == null || GimbalCamera == null || GlyphCanvas == null) return;
              transform.position = Enemy.transform.position + Vector3.up * HoverHeight;

              Vector3 dir = Enemy.transform.position - GimbalCamera.transform.position;
              float dist = dir.magnitude;
              bool inRange = dist < MaxDistance;
              bool inLos = false;
              if (inRange && Physics.Raycast(GimbalCamera.transform.position, dir.normalized, out var hit, MaxDistance)) {
                  inLos = hit.collider.GetComponentInParent<Chassis>() == Enemy;
              }
              GlyphCanvas.gameObject.SetActive(inRange && inLos);
          }
      }
  }
  ```
- Editor: in MapA, instantiate `WarningGlyph.prefab` once. Wire its Controller: Self = BlueChassis, Enemy = RedChassis, GimbalCamera = BlueChassis's camera, GlyphCanvas = the prefab's Canvas child.

**Acceptance**:
- When BlueChassis points its gimbal at RedChassis within 12 m through clear LoS, the `⚠ ENEMY` glyph appears above Red.
- Behind a wall or > 12 m → glyph hidden.
- Glyph does not flicker rapidly when LoS is borderline (raycast stability is acceptable; if flicker observed, add a small hysteresis: 11 m show, 13 m hide).

### 4.8 — 12c.8: Re-baseline Tier 3 + tag

**Steps**:
- Run Tier 1: `uv run pytest tests/test_arena_wire_format.py` → expect green
- Run Tier 2: `uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 30` → expect `[finish]` line
- Hit Play in Unity Editor (MapA scene), in another terminal: `uv run python tests/intra_unity_determinism.py --update-baseline`
- Verify: `uv run python tests/intra_unity_determinism.py` → `[OK] all 25 frames within MAD ≤ 5.0`
- Commit: `git add tests/golden_frames_unity_baseline/ && git commit -m "test(stage12c): re-baseline tier 3 to v1.8 visual contract"`
- Tag: `git tag -a v1.8-unity-art-lean -m "Stage 12c lean — HDRP foundation + plate/holo shaders + muzzle/impact VFX + ⚠ ENEMY glyph"`

**Acceptance**: tag exists in `git tag --list`; Tier 1 + 2 + 3 all green at the tag commit.

## 5. Risks & mitigations

- **Synty material conversion gaps.** The auto-converter doesn't handle Synty's vendor-authored Sci-Fi shader (uses a custom emission-mask pattern). After running the converter, scan `Assets/Synty/PolygonSciFiCity/Materials/` for any still-pink mats; manually swap their Shader to `HDRP/Lit` and re-bind `_BaseColorMap` / `_EmissiveColorMap`.
- **Shader Graph node-spec drift across Unity versions.** Unity 6 LTS Shader Graph 17.x changed some node names from older specs. The design specifies *behaviors* (not node names); the implementing agent picks the current node API.
- **Volumetric fog perf drop.** HDRP Local Volumetric Fog can halve framerate at default Mean Free Path. Target 60 fps in the Editor; if fog drops it under 30 fps, reduce density to 0.006 or thin the volumes.
- **AsyncGPUReadback + custom shaders.** The Tier 3 frame capture pipeline (`TcpFramePub`) relies on `Camera.Render()` returning a complete frame. If a shader graph causes the captured frame to differ from Game view, force `EnableInstancing = false` on the shader and re-verify.
- **Decal projector Z-fighting on slanted surfaces.** HDRP Decal Projectors can flicker on slanted Synty roofs. Set Decal Projector `Decal Layer Mask` to only intersect the floor / wall layers, not roofs.
- **Editor-only authoring.** Shader Graphs and VFX Graphs are visual-node assets. The implementing agent's success on 12c.3–6 depends on Unity Editor scripting access (via MCP-Unity or similar) or ability to write the underlying YAML directly. If neither path is available, the user falls back to interactive Editor work guided by this spec — slower but functional.
- **Game-rule churn.** User is redesigning game rules in parallel. Lean cut intentionally avoids HUD telemetry (which depends on rule design). Visual sub-stages 12c.0–8 are rule-independent; ship them first, layer rule-aware UI on top later.

## 6. Implementation handoff notes

- **Branch**: stay on `stage12/unity-reform`. The implementing agent commits each sub-stage; no separate PR — direct push to the branch.
- **Sub-stage isolation**: never start sub-stage N+1 if sub-stage N's acceptance criteria are not all green. If a sub-stage is blocked, leave it half-complete in a WIP commit (`wip(stage12c.N): ...`) and surface the blocker to the user before continuing.
- **No silent scope expansion**: if the implementing agent realizes a deferred non-goal (EnergyShield, GlassCircuit, DustMote, screen-space HUD, build flag, HDRPAsset variants, baked lighting) is required to complete a current goal, STOP and ask the user. Lean cut is lean by intent; don't expand it autonomously.
- **Where to ask**: post questions back to the user as commit messages in WIP form, OR as comments in this design doc. Don't make tradeoff calls alone for anything that affects the visual contract.
- **Timing tests**: run Tier 1 and Tier 2 after every sub-stage as a smoke check; run Tier 3 only at 12c.8 (per the re-baseline strategy in §3).

## 7. Approval

- [ ] User approves design — proceed to implementation plan via `superpowers:writing-plans`
- [ ] User requests revisions — address inline, re-sync

(Design approved → next step is `writing-plans` skill, which converts each sub-stage above into a detailed step-by-step implementation plan with TDD blocks, commit conventions, and per-step verification checklists.)
