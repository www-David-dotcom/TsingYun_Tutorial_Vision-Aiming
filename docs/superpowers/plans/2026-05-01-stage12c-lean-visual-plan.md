# Stage 12c (lean) — Visual Reform Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Layer a Unity 6 LTS HDRP visual reform on top of the geometry / wiring foundation that landed at `v1.7-unity-geometry`, ending at `v1.8-unity-art-lean`.

**Architecture:** Nine sub-stages, ordered for minimum refactoring (foundation → shaders → VFX → UI → re-baseline). Each sub-stage is its own commit. Tier 1 + Tier 2 verified after each sub-stage; Tier 3 baseline regenerated only at the very end.

**Tech Stack:** Unity 6000.3.14f1, HDRP 17.3.0, Shader Graph 17.3.0, VFX Graph 17.3.0, ProBuilder 6.0.9, TextMeshPro 2.0.0. Python 3.x via uv for tests/scripts.

**Companion design**: `docs/superpowers/specs/2026-05-01-stage12c-lean-visual-design.md` — read it FIRST. The design's §1 (project state at 12c entry) and §1.4 (locked conventions from 12b) are non-negotiable inputs to every task below; do not undo them.

**Repo coordinates:**
- Path: `/Users/davidshi/projects/Aiming_HW`
- Branch: `stage12/unity-reform`
- Latest tag: `v1.7-unity-geometry`
- Unity project: `shared/unity_arena/`

**Implementing-agent assumptions:**
- Unity Editor access (via MCP-Unity, programmatic asset authoring, or interactive Editor work).
- Bash + git + uv available in shell.
- Familiar with HDRP Shader Graph and VFX Graph (or willing to read Unity 6 LTS docs at https://docs.unity3d.com/6000.3/Documentation/Manual/render-pipelines.html).

---

## Sub-stage 12c.0 — Godot retirement

**Sub-stage goal:** Delete `shared/godot_arena/` and every reference in scripts / tests / docs. Smoke harness, wire-format test, and MNIST extractor become Unity-only.

**Acceptance gate** (from design §4.0):
- `uv run pytest tests/test_arena_wire_format.py` green
- `uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 30` produces `[finish]` line
- `grep -rn godot_arena .` returns only historical plan files (not active code/scripts/docs)
- Working tree no longer contains `shared/godot_arena/`

### Task 0.1: Pre-retirement Tier 1 + Tier 2 verification

**Files:** none modified — read-only verification.

- [ ] **Step 1: Run Tier 1 wire-format tests**

```bash
cd /Users/davidshi/projects/Aiming_HW
uv run pytest tests/test_arena_wire_format.py -v
```

Expected: all tests PASS. If they fail, STOP and report the failure to the user before proceeding — Godot must not be removed if the wire format is broken.

- [ ] **Step 2: Open Unity Editor**

Open `/Users/davidshi/projects/Aiming_HW/shared/unity_arena` in Unity 6000.3.14f1. Open scene `Assets/Scenes/MapA_MazeHybrid.unity`. Hit Play. Console should log:
```
[ArenaMain] control on tcp://0.0.0.0:7654, frames on tcp://0.0.0.0:7655
[TcpProtoServer] listening on tcp://0.0.0.0:7654
[TcpFramePub] listening on tcp://0.0.0.0:7655
```

If any of those lines is missing or there are red errors in Console, STOP and report.

- [ ] **Step 3: Run Tier 2 smoke harness against the running Unity arena**

In a separate terminal:
```bash
cd /Users/davidshi/projects/Aiming_HW
uv run python tools/scripts/smoke_arena.py --engine unity --seed 42 --ticks 30
```

Expected output ends with a line like:
```
[finish] episode_id=ep-000000000000002a outcome=4 projectiles_fired=... damage_dealt=...
```

If the script errors or times out, STOP and report.

- [ ] **Step 4: Stop Play in Unity**

Click the Play button again to exit Play mode. Tier 1 + Tier 2 are now verified green at v1.7. Safe to proceed with Godot retirement.

### Task 0.2: Delete `shared/godot_arena/` directory

**Files:**
- Delete: `shared/godot_arena/` (entire directory)

- [ ] **Step 1: Confirm directory exists and is gitted**

```bash
cd /Users/davidshi/projects/Aiming_HW
ls shared/godot_arena/ | head -5
git ls-files shared/godot_arena/ | wc -l
```

Expected: `head -5` shows directory contents; `wc -l` reports a non-zero file count.

- [ ] **Step 2: Remove the directory via git rm**

```bash
git rm -rf shared/godot_arena/
```

Expected: many `rm 'shared/godot_arena/...'` lines in stdout.

- [ ] **Step 3: Verify the removal**

```bash
ls shared/godot_arena/ 2>&1 | head -1
```

Expected: `ls: shared/godot_arena/: No such file or directory` (the directory is gone).

- [ ] **Step 4: Commit**

```bash
git commit -m "chore(stage12c.0): remove shared/godot_arena/ — Unity is the only engine

Per the 12c design (docs/superpowers/specs/2026-05-01-stage12c-lean-visual-design.md
§1.7 / §4.0), the Godot side has been a frozen reference since 12a; Tier 1+2
verified green on Unity at v1.7-unity-geometry, so the safety-net role is
discharged. Subsequent commits in this sub-stage prune script + doc references."
```

### Task 0.3: Update `tools/scripts/smoke_arena.py` to drop `--engine` flag

**Files:**
- Modify: `tools/scripts/smoke_arena.py`

- [ ] **Step 1: Read the current script**

```bash
cat tools/scripts/smoke_arena.py
```

Note the `--engine` argparse flag and any `engine == "godot"` branches.

- [ ] **Step 2: Apply the edit — remove --engine flag**

Open `tools/scripts/smoke_arena.py` and apply these changes:

Remove the line:
```python
parser.add_argument("--engine", default="godot", choices=["godot", "unity"],
                    help="which engine the running arena is. Wire is identical between them.")
```

Update the `print(f"[smoke] engine=...")` line to drop `args.engine`:
```python
print(f"[smoke] host={args.host} port={args.port}")
```

If there are any `if args.engine == "godot":` or `if args.engine == "unity":` branches, keep only the Unity path and unindent.

- [ ] **Step 3: Verify the script still runs against the Unity arena**

Hit Play in Unity (MapA scene), then:
```bash
uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 30
```

Expected: same `[finish]` output as Task 0.1. Stop Play in Unity afterward.

- [ ] **Step 4: Commit**

```bash
git add tools/scripts/smoke_arena.py
git commit -m "chore(stage12c.0): drop --engine flag from smoke_arena.py (Unity-only)"
```

### Task 0.4: Update `tests/test_arena_wire_format.py` to drop pytest parametrize

**Files:**
- Modify: `tests/test_arena_wire_format.py`

- [ ] **Step 1: Read the current test**

```bash
cat tests/test_arena_wire_format.py
```

Look for `@pytest.mark.parametrize("engine", ["godot", "unity"])` decorators or any `if engine == "godot":` branches.

- [ ] **Step 2: Apply the edit — remove engine parametrize**

For each test function decorated with `@pytest.mark.parametrize("engine", ...)`:
- Remove the decorator
- Remove the `engine` parameter from the function signature
- Remove any `if engine == "godot": ...` blocks (keep the `unity` path)
- If a test was Godot-specific, delete it entirely

- [ ] **Step 3: Run the tests to confirm they still pass**

```bash
uv run pytest tests/test_arena_wire_format.py -v
```

Expected: all remaining tests PASS.

- [ ] **Step 4: Commit**

```bash
git add tests/test_arena_wire_format.py
git commit -m "test(stage12c.0): drop engine parametrize from wire format test (Unity-only)"
```

### Task 0.5: Update `tools/scripts/extract_mnist_stickers.py`

**Files:**
- Modify: `tools/scripts/extract_mnist_stickers.py`

- [ ] **Step 1: Read the current script**

```bash
grep -n "godot" tools/scripts/extract_mnist_stickers.py
```

You should see one or two lines mentioning `shared/godot_arena/assets/mnist/` — these are in the trailing print messages.

- [ ] **Step 2: Apply the edit**

Open the file. In the closing print block (near the end of `main()`), remove the line:
```python
print("  - shared/godot_arena/assets/mnist/")
```

Keep the Unity path line:
```python
print("  - shared/unity_arena/Assets/Resources/MNIST/")
```

If the docstring at the top of the file mentions Godot, edit it to be Unity-only.

- [ ] **Step 3: Verify the script still runs**

```bash
uv run python tools/scripts/extract_mnist_stickers.py --help
```

Expected: usage message prints without errors.

- [ ] **Step 4: Commit**

```bash
git add tools/scripts/extract_mnist_stickers.py
git commit -m "chore(stage12c.0): drop godot_arena path mention from MNIST extractor"
```

### Task 0.6: Sweep docs for godot_arena references

**Files:**
- Modify: `docs/godot_arena.md` (likely deletion)
- Modify: `docs/architecture.md`
- Modify: `docs/arena.md`
- Modify: `IMPLEMENTATION_PLAN.md`
- Modify: `README.md`
- Modify: `schema.md`

- [ ] **Step 1: Find all references**

```bash
grep -rln "godot_arena\|godot arena\|Godot arena" docs/ IMPLEMENTATION_PLAN.md README.md schema.md 2>/dev/null
```

Note each file in the output.

- [ ] **Step 2: Delete `docs/godot_arena.md` if it exists**

```bash
[ -f docs/godot_arena.md ] && git rm docs/godot_arena.md
```

- [ ] **Step 3: Edit each remaining file**

For each file in the list from Step 1 (excluding `docs/superpowers/plans/2026-04-30-arena-art-vision-reform-stage12.md` and `docs/superpowers/specs/2026-04-30-arena-art-vision-reform-design.md`, which are frozen historical plan/spec records and may keep references):

- Open the file
- Remove or rewrite paragraphs that explain the Godot side as if it's still alive
- Keep references that are explicitly historical ("originally implemented in Godot at v1.4, ported to Unity in Stage 12")
- For tables / lists that enumerate "engines", remove the Godot row

Save each file.

- [ ] **Step 4: Verify no active references remain**

```bash
grep -rln "godot_arena" docs/ IMPLEMENTATION_PLAN.md README.md schema.md 2>/dev/null
```

Expected: only `docs/superpowers/plans/2026-04-30-arena-art-vision-reform-stage12.md` and `docs/superpowers/specs/2026-04-30-arena-art-vision-reform-design.md` (and possibly this current plan / design) listed.

- [ ] **Step 5: Commit**

```bash
git add docs/ IMPLEMENTATION_PLAN.md README.md schema.md 2>/dev/null
git commit -m "docs(stage12c.0): prune godot_arena references from active docs"
```

### Task 0.7: Append CHANGELOG entry

**Files:**
- Modify: `docs/CHANGELOG.md`

- [ ] **Step 1: Read the current CHANGELOG**

```bash
head -20 docs/CHANGELOG.md
```

Note the format used for prior entries.

- [ ] **Step 2: Append new entry**

Add a new entry near the top of `docs/CHANGELOG.md`, matching the existing format. Example:

```markdown
## v1.7.1 — 2026-05-01

- **Removed:** `shared/godot_arena/` Godot project. Unity is now the only engine. The frozen Godot reference was the safety net during Stage 12a/12b migration; Tier 1 + Tier 2 conformance verified green on Unity at `v1.7-unity-geometry`, so the role is discharged.
- **Updated:** `tools/scripts/smoke_arena.py`, `tests/test_arena_wire_format.py`, `tools/scripts/extract_mnist_stickers.py` to drop `--engine` flag / Godot parameterization.
```

- [ ] **Step 3: Commit**

```bash
git add docs/CHANGELOG.md
git commit -m "docs(stage12c.0): changelog entry for godot_arena removal"
```

### Task 0.8: Final Tier 1 + Tier 2 verification + sub-stage close

**Files:** none modified.

- [ ] **Step 1: Run Tier 1 again**

```bash
uv run pytest tests/test_arena_wire_format.py -v
```

Expected: all tests still PASS.

- [ ] **Step 2: Run Tier 2 again**

Hit Play in Unity (MapA scene), then:
```bash
uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 30
```

Expected: `[finish]` line.

- [ ] **Step 3: Stop Play, declare 12c.0 done**

Click Play again to exit. Sub-stage 12c.0 is closed. Move on to 12c.1.

---

## Sub-stage 12c.1 — HDRP foundation

**Sub-stage goal:** Re-enable HDRP pipeline assignment, convert all current materials to HDRP/Lit, configure the Volume profile.

**Acceptance gate** (from design §4.1):
- Game view + Scene view render the maze without pink materials
- ArenaMain TCP startup logs unchanged
- Tier 1 + Tier 2 still green

### Task 1.1: Note current HDRPAsset values for reference

**Files:** none modified — recording only.

- [ ] **Step 1: Open HDRPAsset.asset**

Locate `shared/unity_arena/Assets/Rendering/HDRPAsset.asset` in the Unity Editor's Project window. Click it.

- [ ] **Step 2: Open the Inspector and note key fields**

In Inspector, expand:
- `Camera FrameSettings`
- `Real-time Lighting`
- `Decals`
- `Lit Shader Mode`

These will be configured properly in Task 1.6. For now, just note any non-default values.

### Task 1.2: Assign HDRPAsset to Graphics Settings

**Files:**
- Modify: `shared/unity_arena/ProjectSettings/GraphicsSettings.asset`

- [ ] **Step 1: Open Project Settings**

In Unity Editor: `Edit → Project Settings → Graphics`.

- [ ] **Step 2: Assign HDRPAsset**

Find the **Default Render Pipeline** field (or **Scriptable Render Pipeline Settings** in older menu naming). Click the small circle/picker on the right. In the picker dialog, select **`HDRPAsset`** (located at `Assets/Rendering/HDRPAsset.asset`).

- [ ] **Step 3: Save the project**

`File → Save Project`. The change is written to `ProjectSettings/GraphicsSettings.asset`.

### Task 1.3: Assign HDRPAsset to all Quality levels

**Files:**
- Modify: `shared/unity_arena/ProjectSettings/QualitySettings.asset`

- [ ] **Step 1: Open Quality settings**

`Edit → Project Settings → Quality`. You see a list of Quality Levels (Low, Medium, High, Very High, Ultra, etc. depending on Unity defaults).

- [ ] **Step 2: For each Quality Level, assign HDRPAsset**

Click each level (one at a time) and find the **Render Pipeline Asset** field. Assign `HDRPAsset` (same as Task 1.2).

- [ ] **Step 3: Save the project**

`File → Save Project`.

### Task 1.4: Run the Render Pipeline Converter (Built-in to HDRP)

**Files:**
- Modify: every `.mat` file under `Assets/Materials/` and `Assets/Synty/`

- [ ] **Step 1: Open the Converter**

In Unity: `Window → Rendering → Render Pipeline Converter`.

- [ ] **Step 2: Select Built-in to HDRP direction**

In the Converter window, choose conversion path **`Built-in to HDRP`**.

- [ ] **Step 3: Initialize and run all converters**

In the converter list (Materials, Animation Clips, ReadOnly Materials, Render Settings, etc.):
- Check ALL converters
- Click **Initialize Converters** — wait for the "ready" status
- Click **Convert Assets** — wait for completion (could take 1–5 minutes depending on Synty asset count)

- [ ] **Step 4: Inspect Console for errors**

The Console window shows progress. Some warnings about specific Synty shaders are expected and OK. **Red errors** require attention — note any and continue to Task 1.5.

### Task 1.5: Manually fix any pink Synty materials that the auto-converter missed

**Files:**
- Modify: any pink `.mat` under `Assets/Synty/PolygonSciFiCity/Materials/`

- [ ] **Step 1: Open the MapA scene and inspect for pink**

Open `Assets/Scenes/MapA_MazeHybrid.unity`. In the Scene view, look for any pink (magenta) surfaces — these indicate a material whose shader doesn't exist in HDRP.

- [ ] **Step 2: For each pink material, swap the shader**

For each affected material asset:
- Click the material in the Project window
- In Inspector, click the **Shader** dropdown at the top
- Change to **`HDRP/Lit`**
- The Inspector now shows HDRP/Lit's properties; the previous textures are usually still bound to `_BaseColorMap` and `_EmissiveColorMap` automatically. If they aren't:
  - Drag the original albedo texture into `Base Map`
  - If it had emission, drag the emission texture into `Emissive Color Map`

- [ ] **Step 3: Re-inspect Scene view**

Move around the scene; verify no pink surfaces remain. If a Synty mesh has multiple submeshes, inspect each material slot.

### Task 1.6: Configure HDRPAsset.asset Camera FrameSettings

**Files:**
- Modify: `shared/unity_arena/Assets/Rendering/HDRPAsset.asset` (or `Settings/`, depending on existing path)

- [ ] **Step 1: Open HDRPAsset in Inspector**

Click `HDRPAsset.asset` in Project window.

- [ ] **Step 2: Configure Camera FrameSettings**

In Inspector, expand **Default Frame Settings** (or **Camera FrameSettings**). Set:
- **Lit Shader Mode**: `Both`
- Under **Real-time Lighting** group: **Volumetrics** ✓ checked
- Under **Lighting** group: **Tube/Area Lights** ✓ checked
- Under **Decals** group: **Decal Layers** ✓ checked

- [ ] **Step 3: Save the project**

`File → Save Project`.

### Task 1.7: Create `Assets/Volumes/Volume_MapA.asset`

**Files:**
- Create: `shared/unity_arena/Assets/Volumes/Volume_MapA.asset`

- [ ] **Step 1: Create the Volumes folder if missing**

In Project window, right-click `Assets` → Create → Folder → name `Volumes`.

- [ ] **Step 2: Create the Volume Profile**

Right-click `Assets/Volumes` → Create → Volume Profile → name `Volume_MapA`.

- [ ] **Step 3: Add overrides**

Click `Volume_MapA.asset`. In Inspector, click **Add Override** and add the following four overrides one at a time:

**Visual Environment**:
- Sky Type: `HDRI Sky`

**HDRI Sky**:
- HDRI Sky: drag `Default-DefaultSkyboxHDRI` from Unity's bundled assets into the slot. (If not findable, leave None — HDRP falls back to a default.)
- Exposure: `0`
- Multiplier: `1`

**Exposure**:
- Mode: `Automatic`
- Limit Min: `8`
- Limit Max: `16`

**Tonemapping**:
- Mode: `ACES`

- [ ] **Step 4: Save the project**

`File → Save Project`.

### Task 1.8: Add a Global Volume to MapA scene

**Files:**
- Modify: `shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity`

- [ ] **Step 1: Open the scene**

Open `Assets/Scenes/MapA_MazeHybrid.unity` if not already open.

- [ ] **Step 2: Create the Volume GameObject**

In Hierarchy, right-click → Volume → **Global Volume**. Name it `MapAVolume`. Position `(0, 0, 0)`.

- [ ] **Step 3: Wire the Profile**

Select `MapAVolume`. In Inspector, on the `Volume` component, drag `Assets/Volumes/Volume_MapA.asset` into the **Profile** slot.

- [ ] **Step 4: Save the scene**

`Cmd+S` (Mac) or `Ctrl+S` (Windows/Linux).

### Task 1.9: Verify scene renders + commit sub-stage

**Files:** none modified.

- [ ] **Step 1: Inspect Scene view + Game view**

Game view (or Scene view) should now show the maze with HDRP rendering: ACES tone mapping applied, exposure auto-adapt, the Synty buildings and chassis visible. No pink surfaces.

- [ ] **Step 2: Hit Play and verify ArenaMain logs**

Hit Play. Console should still show:
```
[ArenaMain] control on tcp://0.0.0.0:7654, frames on tcp://0.0.0.0:7655
[TcpProtoServer] listening on tcp://0.0.0.0:7654
[TcpFramePub] listening on tcp://0.0.0.0:7655
```

No new errors. Stop Play.

- [ ] **Step 3: Run Tier 1 + Tier 2**

```bash
uv run pytest tests/test_arena_wire_format.py -v
```
Expected: green.

Hit Play again, then:
```bash
uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 30
```
Expected: `[finish]` line. Stop Play.

- [ ] **Step 4: Commit**

```bash
git add shared/unity_arena/ProjectSettings/GraphicsSettings.asset \
        shared/unity_arena/ProjectSettings/QualitySettings.asset \
        shared/unity_arena/Assets/Rendering/HDRPAsset.asset \
        shared/unity_arena/Assets/Synty/ \
        shared/unity_arena/Assets/Materials/ \
        shared/unity_arena/Assets/Volumes/ \
        shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity
git commit -m "feat(stage12c.1): re-enable HDRP + migrate materials + Volume_MapA profile

HDRP pipeline assignment in Graphics + all Quality levels. Built-in to HDRP
material conversion via Render Pipeline Converter (with manual fixes for
Synty Sci-Fi shaders the converter doesn't fully handle). Volume_MapA profile
overrides: HDRI Sky, Automatic Exposure (8..16), ACES Tonemapping. Global
Volume added to MapA scene at origin."
```

---

## Sub-stage 12c.2 — Lighting

**Sub-stage goal:** Sun directional light + ~30 HDRP Tube Lights (cyan/magenta) along corridors + 3 Local Volumetric Fog volumes inside corridors.

**Acceptance gate** (from design §4.2):
- Game view shows neon strips lighting the corridors
- Volumetric fog catches sun + tube light beams (visible godrays)
- Sun-from-above casts plausible shadow on chassis
- Frame rate ≥ 30 fps in the Editor on the maintainer's M2/M3 Mac

### Task 2.1: Configure Sun directional light

**Files:**
- Modify: `shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity`

- [ ] **Step 1: Open MapA scene, select Sun**

In Hierarchy, find and select the `Sun` GameObject (typically under `Lighting/`).

- [ ] **Step 2: Configure HDAdditionalLightData**

In Inspector, on the Light component (HDRP version):
- Type: `Directional`
- Mode: `Realtime`
- Use Color Temperature: ✓ checked
- Color Temperature: `5600` K
- Intensity: `100000` lux

- [ ] **Step 3: Configure shadows**

Under `Shadows`:
- Enable Shadows: ✓ checked
- Shadow Map Resolution: `4096`
- Quality: `High`

- [ ] **Step 4: Save the scene**

`Cmd+S`.

### Task 2.2: Add Fog override to Volume_MapA

**Files:**
- Modify: `shared/unity_arena/Assets/Volumes/Volume_MapA.asset`

- [ ] **Step 1: Select Volume_MapA**

Click `Assets/Volumes/Volume_MapA.asset` in Project window.

- [ ] **Step 2: Add Fog override**

In Inspector, click **Add Override** → **Fog**. Configure:
- Enabled: ✓ checked
- State: `Enabled`
- Color Mode: `Sky Color`
- Mean Free Path: `1000` m
- Anisotropy: `-0.2`

- [ ] **Step 3: Save the project**

`File → Save Project`.

### Task 2.3: Place ~30 HDRP Tube Lights along corridors

**Files:**
- Modify: `shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity`

- [ ] **Step 1: Create the parent GameObject**

In MapA scene Hierarchy, under `Lighting/`, right-click → Create Empty. Name `NeonStrips`. Position `(0, 0, 0)`.

- [ ] **Step 2: Create the first Tube Light**

Right-click `NeonStrips` → Light → **Tube Light** (or generic Light if Tube isn't a direct option, then change Type to Tube on the Light component). Name `Strip_001`.

In Inspector, on the Light component:
- Type: `Tube` (under Shape)
- Length: `2.0` m
- Color: `#00DDFF` (cyan) — click the color swatch and enter hex `00DDFF`
- Intensity: `1000` cd
- Range: `6` m
- Cast Shadows: `Off`

- [ ] **Step 3: Position the first strip in a corridor**

Position `Strip_001` along a corridor ceiling (Y ≈ 2.4). Pick any visible corridor in the maze; the placement is stylistic, not deterministic.

- [ ] **Step 4: Duplicate to create 30 strips**

Duplicate `Strip_001` (Cmd+D / Ctrl+D) 29 more times. For each:
- Move along corridor ceilings, every ~3 m
- Alternate the color: even-numbered strips stay `#00DDFF` (cyan), odd-numbered swap to `#FF00DD` (magenta)
- Name them `Strip_002` through `Strip_030`

Distribute them across all six corridor segments roughly evenly. ~5 strips per corridor segment.

- [ ] **Step 5: Save the scene**

`Cmd+S`.

### Task 2.4: Add 3 Local Volumetric Fog volumes inside corridors

**Files:**
- Modify: `shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity`

- [ ] **Step 1: Create first fog volume**

In Hierarchy, under `Lighting/`, right-click → Volume → **Local Volumetric Fog**. Name `Fog_Corridor_1`.

- [ ] **Step 2: Configure**

On the Local Volumetric Fog component:
- Density: `0.012`
- Anisotropy: `-0.2`
- Volume Bounds: pick three numbers that enclose corridor segment 1 (roughly X width, Y height to ceiling, Z corridor length). Example: `(2.4, 2.5, 8)` if the corridor is 2.4 m wide × 2.5 m tall × 8 m long.

Position it at the center of the corridor.

- [ ] **Step 3: Duplicate twice for the other corridors**

Duplicate `Fog_Corridor_1` twice → name `Fog_Corridor_2` and `Fog_Corridor_3`. Move each into one of the remaining corridor segments. Adjust bounds as needed.

- [ ] **Step 4: Save the scene**

`Cmd+S`.

### Task 2.5: Verify Game view + commit sub-stage

**Files:** none modified.

- [ ] **Step 1: Inspect Game view**

In MapA scene, hit Play (or just look at Scene view with full lighting on). Confirm:
- Neon strips lit (cyan + magenta) along corridors
- Volumetric fog visible — godrays at corridor entrances when sun beams pass through
- Sun-from-above casts shadow on chassis
- Frame rate ≥ 30 fps in the Editor (check with `Window → Analysis → Profiler` if unsure)

If frame rate is too low, halve the Tube Light count and/or reduce fog density to `0.006`.

- [ ] **Step 2: Run Tier 1 + Tier 2 sanity check**

```bash
uv run pytest tests/test_arena_wire_format.py -v
```
Hit Play, then:
```bash
uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 30
```
Both green. Stop Play.

- [ ] **Step 3: Commit**

```bash
git add shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity \
        shared/unity_arena/Assets/Volumes/Volume_MapA.asset
git commit -m "feat(stage12c.2): lighting setup — sun + 30 neon tube lights + 3 fog volumes

Sun directional at 5600 K, 100000 lux, 4096 shadow map. NeonStrips parent
holds 30 HDRP Tube Lights alternating cyan (#00DDFF) and magenta (#FF00DD)
along corridor ceilings. Three Local Volumetric Fog volumes (density 0.012,
anisotropy −0.2) inside corridor segments. Volume_MapA gains Fog override
(Sky Color mode, MFP 1000 m). Visible godrays through fog at corridor
entrances when sun beams pass through."
```

---

## Sub-stage 12c.3 — PlateEmission Shader Graph

**Sub-stage goal:** Custom Shader Graph for armor plate body emission with team color + 2 Hz scanline + glyph slot + HP-driven brightness. Replaces `ArmorPlate.RefreshGlow` runtime material edits.

**Acceptance gate** (from design §4.3):
- BlueChassis plates glow blue with a 2 Hz scanline; RedChassis plates glow red
- A bullet hit reduces `_DamageRatio` and the plates dim accordingly
- 25 EditMode tests still green

### Task 3.1: Create `Assets/Shaders/PlateEmission.shadergraph`

**Files:**
- Create: `shared/unity_arena/Assets/Shaders/PlateEmission.shadergraph`

- [ ] **Step 1: Create the Shaders folder if missing**

Project window: right-click `Assets/` → Create → Folder → name `Shaders`.

- [ ] **Step 2: Create the Shader Graph**

Right-click `Assets/Shaders/` → Create → Shader Graph → HDRP → **Lit Shader Graph**. Name `PlateEmission`.

- [ ] **Step 3: Open the Shader Graph editor**

Double-click `PlateEmission.shadergraph` to open the Shader Graph editor window.

### Task 3.2: Configure Graph Inspector

**Files:** modifies the Shader Graph from Task 3.1.

- [ ] **Step 1: Open Graph Inspector**

In the Shader Graph editor, top-right gear icon or `Graph Inspector` panel.

- [ ] **Step 2: Set Graph Settings**

Under **Graph Settings**:
- Material: `Lit`
- Surface Type: `Opaque`
- Alpha Clipping: off
- Receive Decals: on (default)
- Render Face: `Front` (default)

### Task 3.3: Add shader properties

**Files:** modifies the Shader Graph from Task 3.1.

- [ ] **Step 1: Open Blackboard**

Show the **Blackboard** panel in the Shader Graph editor (top-right toggle).

- [ ] **Step 2: Add properties one by one**

Click `+` in Blackboard, add each property:

| Name | Type | Default | Notes |
|---|---|---|---|
| `_TeamColor` | Color | (0.12, 0.42, 1.0, 1) | Mode: HDR |
| `_GlyphTexture` | Texture2D | (white square) | Reference: `_GlyphTexture` |
| `_BaseEmissionEnergy` | Float | 1.5 | |
| `_GlyphEmissionEnergy` | Float | 2.5 | |
| `_ScanlineSpeed` | Float | 2.0 | |
| `_ScanlineWidth` | Float | 0.05 | |
| `_DamageRatio` | Float | 1.0 | |

For each, set the property's **Reference** field to match the name (e.g. `_TeamColor`). This is what `material.SetFloat(...)` will use at runtime.

### Task 3.4: Build the node graph

**Files:** modifies the Shader Graph from Task 3.1.

- [ ] **Step 1: Add the glyph contribution path**

Drag from the Blackboard:
- `_GlyphTexture` property → graph
- Add a **Sample Texture 2D** node, connect `_GlyphTexture` to its Texture input
- Connect the default UV (the node has UV input — leave default)
- Take the `A` (alpha) output of the sampler

Multiply chain:
- alpha × `_TeamColor` × `_GlyphEmissionEnergy` → name this output `glyph_contrib`

- [ ] **Step 2: Add the scanline path**

- Add a **Time** node → `Time` output
- Multiply Time × `_ScanlineSpeed` × `2π` (use `Multiply` nodes; `2π` ≈ `6.28318`)
- Add UV → take `Y` channel only (Split node)
- Multiply UV.Y × 30
- Add the two values
- Apply `Sin` node
- Add **Comparison** node: input1 = sin output, input2 = (1 - `_ScanlineWidth`), Type = Greater
- The boolean output × 1.0 = `scanline_mask`
- Multiply `scanline_mask` × `_TeamColor` × `_BaseEmissionEnergy` → `scanline_contrib`

- [ ] **Step 3: Compute base emission**

- Multiply `_TeamColor` × `_BaseEmissionEnergy` × `_DamageRatio` → `base_emission`

- [ ] **Step 4: Sum and connect to outputs**

- Add `base_emission` + `glyph_contrib` + `scanline_contrib` → final emission
- Connect final emission → **Emission** input on the Lit master fragment
- Multiply `_TeamColor` × `0.3` → connect to **Base Color**
- Set **Smoothness** node = `0.4`, connect to Smoothness output
- Set **Metallic** node = `0.2`, connect to Metallic output

- [ ] **Step 5: Save the Shader Graph**

`Cmd+S` (or `Save Asset` button in Shader Graph editor).

- [ ] **Step 6: Visually verify shader compiles**

The Master Stack node turns green if compilation succeeded. If red errors appear, follow Unity's error message to fix node connections. Common issue: Sample Texture 2D must take a `Texture2D` not a Color — make sure you connected `_GlyphTexture` (the property), not the default texture.

### Task 3.5: Create blue and red plate materials

**Files:**
- Create: `shared/unity_arena/Assets/Materials/PlateEmission_Blue.mat`
- Create: `shared/unity_arena/Assets/Materials/PlateEmission_Red.mat`

- [ ] **Step 1: Create the blue material**

Right-click `Assets/Materials/` → Create → Material → name `PlateEmission_Blue`.

In Inspector, click the Shader dropdown → **Shader Graphs/PlateEmission**. The Inspector now shows the seven properties from Task 3.3.

Set:
- `_TeamColor`: HDR color (0.12, 0.42, 1.0, 1) — pick a vivid blue. HDR intensity slider: 0.
- `_GlyphTexture`: leave at default white (you can set this later when class-icon textures are sourced)
- `_BaseEmissionEnergy`: 1.5
- `_GlyphEmissionEnergy`: 2.5
- `_ScanlineSpeed`: 2.0
- `_ScanlineWidth`: 0.05
- `_DamageRatio`: 1.0

- [ ] **Step 2: Create the red material**

Right-click `Assets/Materials/` → Create → Material → name `PlateEmission_Red`. Same Shader assignment. Set `_TeamColor` to HDR (1.0, 0.20, 0.25, 1) — vivid red. Other properties same as blue.

- [ ] **Step 3: Save the project**

`File → Save Project`.

### Task 3.6: Modify `Chassis.cs` to expose plate-material Inspector slots

**Files:**
- Modify: `shared/unity_arena/Assets/Scripts/Chassis.cs`

- [ ] **Step 1: Read the current `Chassis.cs`**

```bash
cat shared/unity_arena/Assets/Scripts/Chassis.cs
```

Note `AssignArmorMetadata`'s structure.

- [ ] **Step 2: Add Inspector fields**

Open `Chassis.cs`. Near the top of the `Chassis` class (next to other public fields like `Team`, `Number`, `MaxHp`), add:

```csharp
public Material BluePlateMat;
public Material RedPlateMat;
```

- [ ] **Step 3: Add a public accessor for the plate renderer in `ArmorPlate.cs`**

`ArmorPlate.plateRenderer` is currently `[SerializeField] private MeshRenderer plateRenderer;`. To let `Chassis.AssignArmorMetadata` swap the team material at runtime, expose it via a property. In `ArmorPlate.cs`, near the existing `PlateId` property, add:

```csharp
public MeshRenderer PlateRenderer => plateRenderer;
```

- [ ] **Step 4: Update `Chassis.AssignArmorMetadata` to assign the team material**

Find `AssignArmorMetadata`'s loop body (where `plate.Team`, `plate.Face`, `plate.Number` are set). Add right after `plate.Number = Number;`:

```csharp
Material teamMat = (Team == "red") ? RedPlateMat : BluePlateMat;
if (teamMat != null && plate.PlateRenderer != null)
{
    plate.PlateRenderer.material = teamMat;
}
```

- [ ] **Step 5: Save and let Unity recompile**

Save both files. Wait for Unity to finish recompiling (no spinner in bottom-right).

### Task 3.7: Modify `ArmorPlate.cs:RefreshGlow` to use shader property

**Files:**
- Modify: `shared/unity_arena/Assets/Scripts/ArmorPlate.cs`

- [ ] **Step 1: Find the current `RefreshGlow` method**

It currently does `Color.Lerp` and `mat.SetColor("_EmissionColor", ...)`.

- [ ] **Step 2: Replace the body with shader property write**

Replace the entire `RefreshGlow` method body with:

```csharp
public void RefreshGlow(float t)
{
    if (plateRenderer == null || plateRenderer.material == null) return;
    plateRenderer.material.SetFloat("_DamageRatio", t);
}
```

- [ ] **Step 3: Save**

Save the file. Wait for recompile.

### Task 3.8: Update `ArmorPlate.prefab` default plate material

**Files:**
- Modify: `shared/unity_arena/Assets/Prefabs/ArmorPlate.prefab`

- [ ] **Step 1: Open ArmorPlate.prefab in Prefab Mode**

Double-click `Assets/Prefabs/ArmorPlate.prefab`.

- [ ] **Step 2: Assign PlateEmission_Blue.mat**

Select the plate cube child (the body of the plate, not the Sticker quad). In Inspector, on the MeshRenderer's **Materials → Element 0**, drag `Assets/Materials/PlateEmission_Blue.mat`.

- [ ] **Step 3: Save and exit Prefab Mode**

`Cmd+S`. Click `<` to exit Prefab Mode.

### Task 3.9: Wire materials in MapA scene + verify

**Files:**
- Modify: `shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity`

- [ ] **Step 1: Open MapA**

Open `Assets/Scenes/MapA_MazeHybrid.unity`.

- [ ] **Step 2: Wire BlueChassis plate materials**

Select `BlueChassis`. On `Chassis` component:
- `Blue Plate Mat`: drag `PlateEmission_Blue.mat`
- `Red Plate Mat`: drag `PlateEmission_Red.mat`

- [ ] **Step 3: Wire RedChassis plate materials**

Select `RedChassis`. On `Chassis` component:
- `Blue Plate Mat`: drag `PlateEmission_Blue.mat`
- `Red Plate Mat`: drag `PlateEmission_Red.mat`

(Both chassis carry both materials — `AssignArmorMetadata` picks the right one based on `Team`.)

- [ ] **Step 4: Save the scene**

`Cmd+S`.

- [ ] **Step 5: Run EditMode tests**

`Window → General → Test Runner → EditMode → Run All`. Expected: 25 green.

- [ ] **Step 6: Hit Play and verify visuals**

Hit Play. In Game view, BlueChassis plates emit blue with a 2 Hz scanline; RedChassis plates emit red. Stop Play.

- [ ] **Step 7: Verify damage-ramping by smoke**

```bash
uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 30
```
Expected: `[finish]` line. (Plates won't actually take damage in this scenario, but the script verifies the wire path.)

- [ ] **Step 8: Commit**

```bash
git add shared/unity_arena/Assets/Shaders/PlateEmission.shadergraph \
        shared/unity_arena/Assets/Materials/PlateEmission_Blue.mat \
        shared/unity_arena/Assets/Materials/PlateEmission_Red.mat \
        shared/unity_arena/Assets/Prefabs/ArmorPlate.prefab \
        shared/unity_arena/Assets/Prefabs/Chassis.prefab \
        shared/unity_arena/Assets/Scripts/Chassis.cs \
        shared/unity_arena/Assets/Scripts/ArmorPlate.cs \
        shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity
git commit -m "feat(stage12c.3): PlateEmission Shader Graph + blue/red mats + plate glow refactor

Custom HDRP/Lit Shader Graph with seven properties (team color, glyph slot,
base/glyph emission energies, scanline speed/width, HP-driven damage ratio).
Two material instances PlateEmission_Blue and PlateEmission_Red. Chassis.cs
gains BluePlateMat/RedPlateMat Inspector slots; AssignArmorMetadata picks
one by Team and assigns to plate.PlateRenderer. ArmorPlate.RefreshGlow
replaces the runtime color-lerp with mat.SetFloat(_DamageRatio, t)."
```

---

## Sub-stage 12c.4 — HoloProjector Shader Graph

**Sub-stage goal:** Animated emissive cone shader (vertical scrolling noise + edge fresnel) replaces the temp `StickerMat` placeholder on the EmissionCone child of HoloProjector.prefab.

**Acceptance gate** (from design §4.4):
- 3× HoloProjectors in MapA show animated cyan emission cones with vertical scroll + edge fresnel
- No frame-rate hit > 1 fps from the change

### Task 4.1: Create `Assets/Shaders/HoloProjector.shadergraph`

**Files:**
- Create: `shared/unity_arena/Assets/Shaders/HoloProjector.shadergraph`

- [ ] **Step 1: Create the Shader Graph**

Right-click `Assets/Shaders/` → Create → Shader Graph → HDRP → **Unlit Shader Graph**. Name `HoloProjector`.

- [ ] **Step 2: Open and configure Graph Settings**

Open the Shader Graph editor for `HoloProjector`. In Graph Inspector → Graph Settings:
- Material: `Unlit`
- Surface Type: `Transparent`
- Blending Mode: `Alpha`
- Render Face: `Both`

### Task 4.2: Add shader properties

**Files:** modifies the Shader Graph from Task 4.1.

- [ ] **Step 1: Add properties via Blackboard**

| Name | Type | Default | Notes |
|---|---|---|---|
| `_BaseColor` | Color | (0.0, 0.7, 1.0, 1) | Mode: HDR |
| `_ScrollSpeed` | Float | 0.2 | |
| `_FresnelPower` | Float | 1.5 | |
| `_NoiseScale` | Float | 8.0 | |
| `_EmissionEnergy` | Float | 4.0 | |
| `_AlphaMul` | Float | 0.6 | |

Set Reference fields to match the names.

### Task 4.3: Build the node graph

**Files:** modifies the Shader Graph from Task 4.1.

- [ ] **Step 1: Build the noise contribution**

- Add **UV** node → take XY → Split into U and V
- Add **Time** node → `Time` output
- Multiply Time × `_ScrollSpeed` → `t_scroll`
- Add `t_scroll` to V → new V
- Combine U with new V → modified UV
- **Simple Noise** node, take modified UV input, Scale = `_NoiseScale`
- Output → `noise`

- [ ] **Step 2: Build the fresnel contribution**

- Add **Fresnel Effect** node, Power = `_FresnelPower`
- Output → `fresnel`

- [ ] **Step 3: Combine emission and alpha**

- Multiply `noise × fresnel × _BaseColor × _EmissionEnergy` → connect to **Emission** output
- Multiply `noise × fresnel × _AlphaMul` → connect to **Alpha** output

- [ ] **Step 4: Save**

`Cmd+S` in Shader Graph editor.

### Task 4.4: Create `HoloProjector_Cone.mat`

**Files:**
- Create: `shared/unity_arena/Assets/Materials/HoloProjector_Cone.mat`

- [ ] **Step 1: Create the material**

Right-click `Assets/Materials/` → Create → Material → name `HoloProjector_Cone`.

- [ ] **Step 2: Assign shader and defaults**

Inspector → Shader → `Shader Graphs/HoloProjector`. Properties default per Task 4.2.

- [ ] **Step 3: Save**

`File → Save Project`.

### Task 4.5: Update HoloProjector.prefab EmissionCone material

**Files:**
- Modify: `shared/unity_arena/Assets/Prefabs/HoloProjector.prefab`

- [ ] **Step 1: Open the prefab**

Double-click `Assets/Prefabs/HoloProjector.prefab`.

- [ ] **Step 2: Replace EmissionCone material**

Select `EmissionCone` child. In Inspector, on the MeshRenderer's **Materials → Element 0**, drag `HoloProjector_Cone.mat` (replacing the previous `StickerMat.mat` or whatever was there).

- [ ] **Step 3: Save and exit Prefab Mode**

`Cmd+S`, click `<`.

### Task 4.6: Verify and commit

**Files:** none modified.

- [ ] **Step 1: Hit Play in MapA**

Open MapA, hit Play. The 3× HoloProjector instances at the JCT bollards should show:
- Cyan emissive cones above the bollards
- Vertical noise scrolling animation visible
- Edge fresnel (brighter at silhouette edges)

If the cone renders solid black, the Render Face setting was missed in Task 4.1 — re-open the shader and confirm.

Stop Play.

- [ ] **Step 2: Check frame rate**

`Window → Analysis → Profiler` (or note the fps overlay if visible). Frame rate should be within 1 fps of the previous sub-stage's measurement. If lower, the noise-scale texture sampling may be too aggressive — reduce `_NoiseScale` to 4.0 in the material.

- [ ] **Step 3: Run Tier 1 + Tier 2 sanity**

```bash
uv run pytest tests/test_arena_wire_format.py -v
```
Hit Play, then:
```bash
uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 30
```
Both green. Stop Play.

- [ ] **Step 4: Commit**

```bash
git add shared/unity_arena/Assets/Shaders/HoloProjector.shadergraph \
        shared/unity_arena/Assets/Materials/HoloProjector_Cone.mat \
        shared/unity_arena/Assets/Prefabs/HoloProjector.prefab
git commit -m "feat(stage12c.4): HoloProjector Shader Graph — animated emissive cone

HDRP/Unlit transparent shader with vertical scrolling Simple Noise (UV+Time
× ScrollSpeed) modulated by fresnel for soft silhouette glow. Six properties
(_BaseColor HDR cyan, _ScrollSpeed 0.2, _FresnelPower 1.5, _NoiseScale 8.0,
_EmissionEnergy 4.0, _AlphaMul 0.6). HoloProjector_Cone.mat replaces the
temp StickerMat placeholder on the EmissionCone child of HoloProjector.prefab.
All three MapA JCT bollards animate."
```

---

## Sub-stage 12c.5 — MuzzleFlash VFX Graph

**Sub-stage goal:** Spark burst + transient point light at the muzzle on each fired bullet, spawned from `ArenaMain.SpawnSingleProjectile`.

**Acceptance gate** (from design §4.5):
- Each fired bullet spawns a brief muzzle flash visible in the gimbal POV
- Hierarchy under VfxRoot stays clean after the burst (instances self-destruct)

### Task 5.1: Write `OneShotDestroy.cs`

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/OneShotDestroy.cs`

- [ ] **Step 1: Create the file**

```csharp
using UnityEngine;

namespace TsingYun.UnityArena
{
    /// <summary>
    /// Destroys the GameObject after a fixed lifetime. Used by short-lived VFX
    /// prefabs (MuzzleFlash, ImpactSpark) that get instantiated per-event and
    /// must self-clean so the Hierarchy doesn't accumulate stale instances.
    /// </summary>
    public class OneShotDestroy : MonoBehaviour
    {
        public float Lifetime = 0.5f;

        private void Start()
        {
            Destroy(gameObject, Lifetime);
        }
    }
}
```

- [ ] **Step 2: Save and verify compile**

Wait for Unity to recompile (no errors in Console).

### Task 5.2: Create `Assets/VFX/MuzzleFlash.vfx`

**Files:**
- Create: `shared/unity_arena/Assets/VFX/MuzzleFlash.vfx`

- [ ] **Step 1: Create the VFX folder if missing**

Project window: right-click `Assets/` → Create → Folder → `VFX`.

- [ ] **Step 2: Create the VFX Graph**

Right-click `Assets/VFX/` → Create → Visual Effects → **Visual Effect Graph**. Name `MuzzleFlash`.

- [ ] **Step 3: Open the VFX Graph editor**

Double-click `MuzzleFlash.vfx`.

### Task 5.3: Configure MuzzleFlash VFX (one-shot burst)

**Files:** modifies the VFX Graph from Task 5.2.

- [ ] **Step 1: Configure the System**

Default VFX has a `Spawn`, `Initialize`, `Update`, and `Output` block. Adjust each:

**Spawn block:**
- Replace the Constant Spawn Rate with **Single Burst** → Count: `30`, Delay: `0`

**Initialize block:**
- **Set Position (Cone)**: Cone Angle 30°, Cone Height 0.05 m, axis along +Z
- **Set Velocity (Direction & Speed)**: Direction = forward, Speed = Random Range 5–15 m/s
- **Set Lifetime (Constant)**: 0.05 s
- **Set Color (Constant)**: white

**Update block:** (default Linear Drag is fine)

**Output block (Quad):**
- Set Texture: any small spark texture (Unity's default `Default-Particle` or import a small flare PNG; for now use the included `Default-Particle`)
- Set Size over Lifetime: starts 0.01 m, fades to 0
- Set Color over Lifetime: white → orange (`#FFA040`) → fade to (0,0,0,0)
- Blend Mode: Additive

- [ ] **Step 2: Save the VFX**

`Cmd+S` in VFX Graph editor.

### Task 5.4: Create MuzzleFlash.prefab

**Files:**
- Create: `shared/unity_arena/Assets/Prefabs/MuzzleFlash.prefab`

- [ ] **Step 1: Create the prefab root in scene**

In an empty scene or the MapA scene Hierarchy, right-click → Create Empty → name `MuzzleFlash`. Position `(0, 0, 0)`.

- [ ] **Step 2: Add VisualEffect component**

Inspector → Add Component → **Visual Effect**. Drag `Assets/VFX/MuzzleFlash.vfx` into the Asset Template slot.

- [ ] **Step 3: Add Point Light child**

Right-click `MuzzleFlash` → Light → **Point Light**. Name it `Flash_Light`. Configure:
- Intensity: 4 cd
- Color: HDR `#FFE0A0` (warm white)
- Range: 2 m
- Cast Shadows: Off

Add the `OneShotDestroy` component to `Flash_Light`. Set `Lifetime` to `0.05`.

- [ ] **Step 4: Add OneShotDestroy to MuzzleFlash root**

Select the root `MuzzleFlash` GameObject. Add Component → `OneShotDestroy`. Set Lifetime to `0.5` (covers both VFX particle lifetime and the brief light flash).

- [ ] **Step 5: Drag to Prefabs folder**

Drag the `MuzzleFlash` GameObject from Hierarchy into `Assets/Prefabs/`. Choose **Create Original Prefab**. Name `MuzzleFlash`.

- [ ] **Step 6: Delete the scene instance**

If you created the prefab in MapA, delete the in-scene instance after the prefab is saved (we don't want it permanently in the Hierarchy).

### Task 5.5: Modify `ArenaMain.cs` to spawn MuzzleFlash on each fire

**Files:**
- Modify: `shared/unity_arena/Assets/Scripts/ArenaMain.cs`

- [ ] **Step 1: Add Inspector fields**

Open `ArenaMain.cs`. Near other public fields (e.g., `BlueChassis`, `RedChassis`), add:

```csharp
public GameObject MuzzleFlashPrefab;
public Transform VfxRoot;
```

- [ ] **Step 2: Update `SpawnSingleProjectile`**

Find the `SpawnSingleProjectile` method (around line 250 of the current file). After the `p.Arm(spec.Velocity, BlueChassis.Team);` line, add:

```csharp
if (MuzzleFlashPrefab != null)
{
    Instantiate(MuzzleFlashPrefab, spec.Position, spec.Rotation, VfxRoot);
}
```

- [ ] **Step 3: Save and recompile**

Save the file. Wait for Unity to finish compiling.

### Task 5.6: Wire MapA scene — VfxRoot + ArenaMain refs

**Files:**
- Modify: `shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity`

- [ ] **Step 1: Create VfxRoot GameObject**

In MapA Hierarchy, at root level (sibling of `ProjectileRoot`), right-click → Create Empty. Name `VfxRoot`. Position `(0, 0, 0)`.

- [ ] **Step 2: Wire ArenaMain Inspector slots**

Select `ArenaMain` in Hierarchy. On the `ArenaMain` script component:
- `Muzzle Flash Prefab`: drag `Assets/Prefabs/MuzzleFlash.prefab`
- `Vfx Root`: drag the `VfxRoot` GameObject from Hierarchy

- [ ] **Step 3: Save the scene**

`Cmd+S`.

### Task 5.7: Verify and commit

**Files:** none modified.

- [ ] **Step 1: Hit Play in MapA**

In MapA, hit Play. In another terminal:

```bash
uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 30
```

Watch the Game view: when `[fire] accepted=True queued=3` prints, you should see brief muzzle flashes at BlueChassis's muzzle. (Bullets are rate-limited at 5 Hz; first flash on `tick 029` per the smoke harness flow.)

- [ ] **Step 2: Verify VfxRoot stays clean after**

After the smoke harness finishes, switch to Hierarchy panel (still in Play mode). Expand `VfxRoot`. Should be empty (all spawned MuzzleFlash instances self-destructed via OneShotDestroy).

Stop Play.

- [ ] **Step 3: Run Tier 1 + Tier 2**

```bash
uv run pytest tests/test_arena_wire_format.py -v
```
And another smoke run if needed. Both green.

- [ ] **Step 4: Commit**

```bash
git add shared/unity_arena/Assets/Scripts/OneShotDestroy.cs \
        shared/unity_arena/Assets/Scripts/ArenaMain.cs \
        shared/unity_arena/Assets/VFX/MuzzleFlash.vfx \
        shared/unity_arena/Assets/Prefabs/MuzzleFlash.prefab \
        shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity
git commit -m "feat(stage12c.5): MuzzleFlash VFX Graph + spawn on fire

HDRP VFX Graph one-shot burst: 30 white-to-orange spark particles, 0.05 s
lifetime, cone-distributed forward velocity 5-15 m/s, additive blend.
Transient warm-white Point Light (4 cd, 0.05 s lifetime). MuzzleFlash.prefab
wraps both with a OneShotDestroy MB on the root (0.5 s self-clean).
ArenaMain.SpawnSingleProjectile instantiates the prefab at the muzzle
world position right after Projectile.Arm. New VfxRoot GameObject in MapA
parents the spawns so the Hierarchy stays organized."
```

---

## Sub-stage 12c.6 — ImpactSpark VFX Graph + scorch decal

**Sub-stage goal:** Spark burst at projectile collision; HDRP Decal projector adds a fading scorch ring on wall hits (no decal on plates — too small).

**Acceptance gate** (from design §4.6):
- Bullets hitting walls show sparks + a scorch ring that fades over 8–12 s
- Bullets hitting plates show sparks (no decal)
- No leaked GameObjects after 30 s of firing

### Task 6.1: Write `DecalFader.cs`

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/DecalFader.cs`

- [ ] **Step 1: Create the file**

```csharp
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace TsingYun.UnityArena
{
    /// <summary>
    /// Fades an HDRP DecalProjector's fadeFactor from 1 to 0 between FadeStart
    /// and Lifetime, then destroys the GameObject. Used by ImpactSpark_Wall
    /// scorch decals to fade out cleanly.
    /// </summary>
    [RequireComponent(typeof(DecalProjector))]
    public class DecalFader : MonoBehaviour
    {
        public float Lifetime = 12f;
        public float FadeStart = 8f;

        private DecalProjector _proj;
        private float _t;

        private void Awake()
        {
            _proj = GetComponent<DecalProjector>();
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t > FadeStart)
            {
                _proj.fadeFactor = Mathf.Clamp01(1f - (_t - FadeStart) / (Lifetime - FadeStart));
            }
            if (_t > Lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
```

- [ ] **Step 2: Save and verify recompile**

### Task 6.2: Create `Assets/VFX/ImpactSpark.vfx`

**Files:**
- Create: `shared/unity_arena/Assets/VFX/ImpactSpark.vfx`

- [ ] **Step 1: Create the VFX Graph**

Right-click `Assets/VFX/` → Create → Visual Effects → Visual Effect Graph. Name `ImpactSpark`.

- [ ] **Step 2: Configure**

Open the VFX Graph editor. Configure:

**Spawn block:** Single Burst → Count `20`, Delay `0`.

**Initialize block:**
- Set Position (Sphere): Radius `0.01` m
- Set Velocity (Random Direction): hemisphere outward, magnitude Random Range `2–6` m/s
- Set Lifetime: Random Range `0.2–0.4` s
- Set Color: yellow `#FFCC40`

**Update block:**
- Linear Drag: 1.0
- Apply Gravity: enabled (default `(0, -9.81, 0)`)

**Output block (Quad):**
- Texture: `Default-Particle`
- Size over Lifetime: `0.005` → 0
- Color over Lifetime: yellow → orange `#FF6020` → black
- Blend Mode: Additive

- [ ] **Step 3: Save**

### Task 6.3: Create `Assets/Decals/ImpactScorch.mat`

**Files:**
- Create: `shared/unity_arena/Assets/Decals/ImpactScorch.mat`

- [ ] **Step 1: Create the Decals folder if missing**

Project window: right-click `Assets/` → Create → Folder → `Decals`.

- [ ] **Step 2: Find or create a scorch texture**

Either source a small black-ring radial-gradient PNG (256×256) or use Unity's bundled `Default-Particle` as a black-and-white substitute. Place it in `Assets/Decals/` if a custom texture is used. Name it `ImpactScorch.png`.

- [ ] **Step 3: Create the Decal Material**

Right-click `Assets/Decals/` → Create → Material → name `ImpactScorch`. Inspector → Shader → `HDRP/Decal`.

- [ ] **Step 4: Configure the material**

In Inspector:
- **Base Color Map**: drag the scorch PNG (or fallback texture)
- **Base Color**: black tint `(0.05, 0.05, 0.05, 1)`
- **Affect Base Color**: ✓ checked
- **Affect Smoothness**: ✓ checked, Smoothness Multiplier `0.4`
- Other channels: leave defaults

### Task 6.4: Create `ImpactSpark_Wall.prefab`

**Files:**
- Create: `shared/unity_arena/Assets/Prefabs/ImpactSpark_Wall.prefab`

- [ ] **Step 1: Build hierarchy**

In MapA scene (or empty scene), right-click → Create Empty → name `ImpactSpark_Wall`. Position `(0, 0, 0)`.

- [ ] **Step 2: Add VisualEffect to root**

Add Component → Visual Effect. Asset Template = `Assets/VFX/ImpactSpark.vfx`.

Add Component → `OneShotDestroy`. Lifetime: `12`.

- [ ] **Step 3: Add Decal child**

Right-click `ImpactSpark_Wall` → Rendering → **Decal Projector**. Name `Scorch_Decal`. Configure:
- Material: `Assets/Decals/ImpactScorch.mat`
- Size: `(0.2, 0.2, 0.05)` (X = width, Y = height, Z = projection depth)
- Pivot: `(0, 0, 0)`
- Decal Layer: `Decal Layer Default`

Add Component → `DecalFader`. Lifetime: `12`. FadeStart: `8`.

- [ ] **Step 4: Drag to Prefabs**

Drag the `ImpactSpark_Wall` GameObject into `Assets/Prefabs/`. Create Original Prefab. Delete the scene instance.

### Task 6.5: Create `ImpactSpark_Plate.prefab`

**Files:**
- Create: `shared/unity_arena/Assets/Prefabs/ImpactSpark_Plate.prefab`

- [ ] **Step 1: Build hierarchy (no decal)**

In MapA (or empty), right-click → Create Empty → `ImpactSpark_Plate`.

Add Component → Visual Effect. Asset Template = `Assets/VFX/ImpactSpark.vfx`.

Add Component → `OneShotDestroy`. Lifetime: `1.0` (just enough for the spark particles to fade).

- [ ] **Step 2: Drag to Prefabs**

Drag the GameObject into `Assets/Prefabs/`. Create Original Prefab. Delete scene instance.

### Task 6.6: Modify `Projectile.cs` to spawn wall impact on consume

**Files:**
- Modify: `shared/unity_arena/Assets/Scripts/Projectile.cs`

- [ ] **Step 1: Add Inspector field**

Near top of the `Projectile` class:

```csharp
public GameObject WallImpactPrefab;
private Transform _vfxRoot;
```

- [ ] **Step 2: Cache VfxRoot in `Start`**

Add (or extend) `Start`:

```csharp
private void Start()
{
    var rootGo = GameObject.Find("VfxRoot");
    if (rootGo != null) _vfxRoot = rootGo.transform;
}
```

- [ ] **Step 3: Update `Consume(string reason)`**

Find the `Consume` method. Before the existing `Destroy(gameObject)` call, add:

```csharp
if (reason.StartsWith("hit_wall") && WallImpactPrefab != null)
{
    Instantiate(WallImpactPrefab,
                transform.position,
                Quaternion.LookRotation(_rb.linearVelocity.sqrMagnitude > 0.001f
                    ? _rb.linearVelocity.normalized
                    : Vector3.forward),
                _vfxRoot);
}
```

- [ ] **Step 4: Save and recompile**

### Task 6.7: Modify `ArmorPlate.cs` to spawn plate impact on hit

**Files:**
- Modify: `shared/unity_arena/Assets/Scripts/ArmorPlate.cs`

- [ ] **Step 1: Add Inspector field**

Near top of `ArmorPlate` class:

```csharp
public GameObject PlateImpactPrefab;
private Transform _vfxRoot;
```

- [ ] **Step 2: Cache VfxRoot in `Start`**

Add (or extend) `Start`:

```csharp
private void Start()
{
    var rootGo = GameObject.Find("VfxRoot");
    if (rootGo != null) _vfxRoot = rootGo.transform;
}
```

- [ ] **Step 3: Update `OnTriggerEnter`**

Find `OnTriggerEnter`. After the damage calculation but BEFORE the existing trigger flow, add:

```csharp
if (damage > 0 && PlateImpactPrefab != null)
{
    Vector3 impactPos = other.transform.position;
    Vector3 impactNormal = (other.transform.position - transform.position).normalized;
    Instantiate(PlateImpactPrefab,
                impactPos,
                Quaternion.LookRotation(impactNormal),
                _vfxRoot);
}
```

(Place this where the `damage` variable is in scope, immediately before `_state.ApplyDamage(damage)` or equivalent.)

- [ ] **Step 4: Save and recompile**

### Task 6.8: Wire prefab Inspector slots

**Files:**
- Modify: `shared/unity_arena/Assets/Prefabs/Projectile.prefab`
- Modify: `shared/unity_arena/Assets/Prefabs/ArmorPlate.prefab`

- [ ] **Step 1: Wire Projectile.prefab**

Open `Assets/Prefabs/Projectile.prefab` in Prefab Mode. Select the root. On the `Projectile` script component:
- `Wall Impact Prefab`: drag `Assets/Prefabs/ImpactSpark_Wall.prefab`

Save and exit Prefab Mode.

- [ ] **Step 2: Wire ArmorPlate.prefab**

Open `Assets/Prefabs/ArmorPlate.prefab` in Prefab Mode. Select the root. On the `ArmorPlate` script component:
- `Plate Impact Prefab`: drag `Assets/Prefabs/ImpactSpark_Plate.prefab`

Save and exit Prefab Mode.

### Task 6.9: Verify and commit

**Files:** none modified.

- [ ] **Step 1: Hit Play and run smoke**

In MapA, hit Play. Run smoke harness. As the bullets stream out, watch for:
- Spark bursts when bullets hit walls (with a scorch decal that lingers)
- Spark bursts when bullets hit plates (no decal)

If no impacts visible because all bullets miss (depending on chassis aim), that's fine — the prefab wiring is still validated by the absence of console errors.

- [ ] **Step 2: Verify VfxRoot self-cleans after 30 s**

Stay in Play mode. Wait 30 seconds. Inspect Hierarchy → `VfxRoot`. Should be empty or contain only currently-fading instances. If GameObjects accumulate (more than ~10 simultaneously), `OneShotDestroy` and `DecalFader` are not running; check Console for errors and verify both scripts are attached to their respective prefabs.

Stop Play.

- [ ] **Step 3: Run Tier 1 + Tier 2**

```bash
uv run pytest tests/test_arena_wire_format.py -v
```

Hit Play, then smoke. Both green.

- [ ] **Step 4: Commit**

```bash
git add shared/unity_arena/Assets/Scripts/DecalFader.cs \
        shared/unity_arena/Assets/Scripts/Projectile.cs \
        shared/unity_arena/Assets/Scripts/ArmorPlate.cs \
        shared/unity_arena/Assets/VFX/ImpactSpark.vfx \
        shared/unity_arena/Assets/Decals/ImpactScorch.mat \
        shared/unity_arena/Assets/Prefabs/ImpactSpark_Wall.prefab \
        shared/unity_arena/Assets/Prefabs/ImpactSpark_Plate.prefab \
        shared/unity_arena/Assets/Prefabs/Projectile.prefab \
        shared/unity_arena/Assets/Prefabs/ArmorPlate.prefab
git commit -m "feat(stage12c.6): ImpactSpark VFX Graph + scorch decal + collision spawns

HDRP VFX Graph: 20 yellow-to-black spark particles per impact, 0.2-0.4 s
lifetime, hemisphere outward velocity 2-6 m/s, gravity-affected. Two prefab
variants:
  - ImpactSpark_Wall.prefab: VFX + child DecalProjector (HDRP/Decal,
    ImpactScorch.mat) + DecalFader (fade 8-12 s) + OneShotDestroy 12 s
  - ImpactSpark_Plate.prefab: VFX + OneShotDestroy 1 s, no decal (plates
    too small for 0.2 m decal)

Projectile.Consume spawns ImpactSpark_Wall on hit_wall reasons.
ArmorPlate.OnTriggerEnter spawns ImpactSpark_Plate on damage > 0.
Both look up VfxRoot via GameObject.Find at Start (prefabs can't reference
scene objects directly)."
```

---

## Sub-stage 12c.7 — Diegetic LOS-gated `⚠ ENEMY` warning glyph

**Sub-stage goal:** World Space Canvas + LOS-gated controller, visible only when the enemy is < 12 m and the gimbal camera has clear LoS.

**Acceptance gate** (from design §4.7):
- When BlueChassis points its gimbal at RedChassis within 12 m through clear LoS, the `⚠ ENEMY` glyph appears above Red
- Behind a wall or > 12 m → glyph hidden
- Glyph does not flicker rapidly when LoS is borderline (add hysteresis if observed)

### Task 7.1: Write `WarningGlyphController.cs`

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/WarningGlyphController.cs`

- [ ] **Step 1: Create the file**

```csharp
using UnityEngine;

namespace TsingYun.UnityArena
{
    /// <summary>
    /// Diegetic LOS-gated "⚠ ENEMY" warning glyph. Positions the glyph above
    /// the enemy chassis each LateUpdate; reveals it only when the enemy is
    /// within MaxDistance of the gimbal camera AND a Physics.Raycast from the
    /// camera to the enemy hits the enemy chassis (no occluder between).
    /// </summary>
    public class WarningGlyphController : MonoBehaviour
    {
        public Chassis Self;
        public Chassis Enemy;
        public Camera GimbalCamera;
        public float MaxDistance = 12f;
        public float HoverHeight = 1.4f;
        public Canvas GlyphCanvas;

        private void LateUpdate()
        {
            if (Self == null || Enemy == null || GimbalCamera == null || GlyphCanvas == null) return;

            transform.position = Enemy.transform.position + Vector3.up * HoverHeight;

            Vector3 from = GimbalCamera.transform.position;
            Vector3 to = Enemy.transform.position;
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            bool inRange = dist < MaxDistance;
            bool inLos = false;
            if (inRange && Physics.Raycast(from, dir.normalized, out var hit, MaxDistance))
            {
                var hitChassis = hit.collider.GetComponentInParent<Chassis>();
                inLos = hitChassis == Enemy;
            }
            GlyphCanvas.gameObject.SetActive(inRange && inLos);
        }
    }
}
```

- [ ] **Step 2: Save and verify compile**

### Task 7.2: Create `WarningGlyph.prefab`

**Files:**
- Create: `shared/unity_arena/Assets/Prefabs/WarningGlyph.prefab`

- [ ] **Step 1: Build hierarchy in MapA (or empty scene)**

Right-click in Hierarchy → Create Empty → name `WarningGlyph`. Position `(0, 0, 0)`.

Add Component → `WarningGlyphController`. Leave its Inspector slots blank for now (wired in Task 7.3).

- [ ] **Step 2: Add Canvas child**

Right-click `WarningGlyph` → UI → **Canvas**. Name it `GlyphCanvas`. On the Canvas component:
- Render Mode: **World Space**
- RectTransform: width `60`, height `20`, scale `(0.01, 0.01, 0.01)`

- [ ] **Step 3: Add TextMeshPro text child**

Right-click `GlyphCanvas` → UI → **Text - TextMeshPro**. Name `LabelText`. (If TMP Essentials prompt appears, click Import.)

On RectTransform: anchor stretch (Alt-click bottom-right anchor preset). On TextMeshProUGUI:
- Text: `⚠ ENEMY`
- Font Size: `18`
- Color: `#FFD966` (red-amber)
- Alignment: Center / Middle
- Wrapping: Disabled

- [ ] **Step 4: Wire WarningGlyphController.GlyphCanvas slot**

Select `WarningGlyph` root. On the `WarningGlyphController` component, drag the `GlyphCanvas` child into the `Glyph Canvas` slot. (This stays wired in the prefab since both root and Canvas are inside the prefab.)

- [ ] **Step 5: Drag to Prefabs folder**

Drag `WarningGlyph` from Hierarchy into `Assets/Prefabs/`. Create Original Prefab.

### Task 7.3: Add WarningGlyph instance to MapA + wire scene refs

**Files:**
- Modify: `shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity`

- [ ] **Step 1: Open MapA, instantiate the prefab**

Drag `Assets/Prefabs/WarningGlyph.prefab` into MapA Hierarchy (root level). Position `(0, 0, 0)` — the controller will move it each LateUpdate.

- [ ] **Step 2: Wire scene-only references**

Select the WarningGlyph instance. On the `WarningGlyphController` component:
- `Self`: drag `BlueChassis` from Hierarchy
- `Enemy`: drag `RedChassis` from Hierarchy
- `Gimbal Camera`: drag `BlueChassis → Gimbal → YawPivot → PitchPivot → GimbalCamera` from Hierarchy
- `Glyph Canvas`: should already be wired (from prefab); verify

(`MaxDistance` and `HoverHeight` keep their defaults of 12 and 1.4.)

- [ ] **Step 3: Save scene**

`Cmd+S`.

### Task 7.4: Verify LOS gate + commit

**Files:** none modified.

- [ ] **Step 1: Hit Play in MapA**

Sit in Play mode. Don't run smoke harness.

- [ ] **Step 2: Verify glyph visibility**

In Game view (BlueChassis POV):
- If RedChassis is in clear line of sight within 12 m: `⚠ ENEMY` glyph appears above Red.
- If RedChassis is behind a Synty wall: glyph hidden.
- If RedChassis is > 12 m away: glyph hidden.

You may need to manually move BlueChassis or RedChassis in the Editor while in Play mode (Scene view, drag the chassis transform) to test all three cases.

If glyph flickers rapidly when LoS is borderline (e.g., on a wall edge), open `WarningGlyphController.cs` and add hysteresis: visible at < 11 m, hidden at > 13 m. (Optional polish; only if needed.)

Stop Play.

- [ ] **Step 3: Run Tier 1 + Tier 2**

```bash
uv run pytest tests/test_arena_wire_format.py -v
```

Hit Play, run smoke. Both green.

- [ ] **Step 4: Commit**

```bash
git add shared/unity_arena/Assets/Scripts/WarningGlyphController.cs \
        shared/unity_arena/Assets/Prefabs/WarningGlyph.prefab \
        shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity
git commit -m "feat(stage12c.7): diegetic LOS-gated ⚠ ENEMY warning glyph

WarningGlyph.prefab: World Space Canvas (60×20 at 0.01 scale) + TextMeshProUGUI
'⚠ ENEMY' (font 18, color #FFD966 red-amber, wrapping off, center-middle).
WarningGlyphController.cs LateUpdate:
  - Position glyph at Enemy.position + Vector3.up * HoverHeight (1.4 m)
  - LOS gate: Physics.Raycast from GimbalCamera through Enemy direction;
    visible iff hit chassis == Enemy AND distance < MaxDistance (12 m)

Single instance in MapA wired Self=BlueChassis, Enemy=RedChassis,
GimbalCamera=BlueChassis's PitchPivot/GimbalCamera. Glyph hidden when Red
is occluded by Synty wall or > 12 m away."
```

---

## Sub-stage 12c.8 — Re-baseline Tier 3 + tag

**Sub-stage goal:** Re-baseline `tests/golden_frames_unity_baseline/` against the new visual contract; tag `v1.8-unity-art-lean`.

**Acceptance gate** (from design §4.8):
- Tag `v1.8-unity-art-lean` exists in `git tag --list`
- Tier 1 + Tier 2 + Tier 3 all green at the tag commit

### Task 8.1: Run Tier 1 (wire format)

**Files:** none modified.

- [ ] **Step 1: Run pytest**

```bash
uv run pytest tests/test_arena_wire_format.py -v
```

Expected: green. If red, STOP and report.

### Task 8.2: Run Tier 2 (smoke harness)

**Files:** none modified.

- [ ] **Step 1: Hit Play in Unity**

Open MapA, hit Play. Wait for the three startup logs.

- [ ] **Step 2: Run smoke**

```bash
uv run python tools/scripts/smoke_arena.py --seed 42 --ticks 30
```

Expected: `[finish]` line. If error or timeout, STOP and report.

### Task 8.3: Bootstrap Tier 3 baseline against the new visuals

**Files:**
- Modify (regenerate): `tests/golden_frames_unity_baseline/*.png`

- [ ] **Step 1: Delete the old baseline**

```bash
rm -rf tests/golden_frames_unity_baseline/
```

(With Unity still in Play mode.)

- [ ] **Step 2: Capture the new baseline**

```bash
uv run python tests/intra_unity_determinism.py --update-baseline
```

Expected: 25 `[capture] seed_NNNN_pose_N sha256=...` lines, then `[determinism] wrote new baseline: tests/golden_frames_unity_baseline/`.

- [ ] **Step 3: Spot-check a few PNGs visually**

```bash
ls tests/golden_frames_unity_baseline/ | head -5
```

Open one or two PNGs (e.g., `seed_0042_pose_0.png`) in any image viewer. Confirm they show the upgraded MapA: HDRP-rendered Synty buildings, neon strips, plate emission, HoloProjector cones, etc.

### Task 8.4: Verify Tier 3 against new baseline

**Files:** none modified.

- [ ] **Step 1: Run determinism test**

```bash
uv run python tests/intra_unity_determinism.py
```

Expected: `[OK] all 25 frames within MAD ≤ 5.0 of baseline.` plus a top-5 worst-case MAD list.

If FAIL, the visual rendering is non-deterministic across runs (timing noise pushed some pose deltas above 5.0). Workarounds:
- Bump threshold: `uv run python tests/intra_unity_determinism.py --threshold 10`
- If still fails: investigate the specific shader/VFX that's introducing noise (likely the noise-driven HoloProjector or fog), reduce `_NoiseScale` or fog density.

Stop Play in Unity.

### Task 8.5: Commit baseline + tag v1.8-unity-art-lean

**Files:**
- Add: `tests/golden_frames_unity_baseline/*.png`

- [ ] **Step 1: Commit baseline**

```bash
git add tests/golden_frames_unity_baseline/
git commit -m "test(stage12c.8): re-baseline tier 3 to v1.8 visual contract

25 frames captured against MapA after Stage 12c lean visual reform:
HDRP foundation + neon Tube Lights + volumetric fog + PlateEmission +
HoloProjector + MuzzleFlash + ImpactSpark + ⚠ ENEMY glyph. MAD threshold
5.0 / 255; worst-case MAD < 5.0 in the bootstrap+verify pass. Future
regressions in shaders, materials, lighting, or scene wiring will fail
this gate before reaching students."
```

- [ ] **Step 2: Tag the release**

```bash
git tag -a v1.8-unity-art-lean -m "Stage 12c lean — HDRP foundation + plate/holo shaders + muzzle/impact VFX + ⚠ ENEMY glyph

Path B (lean cut) of Stage 12c per design at
docs/superpowers/specs/2026-05-01-stage12c-lean-visual-design.md.

Delivered (sub-stages 12c.0 through 12c.8):
  - Godot retirement: shared/godot_arena/ removed
  - HDRP foundation: pipeline re-enabled, materials migrated, Volume_MapA profile
  - Lighting: sun + 30 neon Tube Lights + 3 volumetric fog volumes
  - PlateEmission Shader Graph + blue/red materials
  - HoloProjector Shader Graph for JCT bollard cones
  - MuzzleFlash VFX Graph + transient point light
  - ImpactSpark VFX Graph + scorch decal (wall variant)
  - Diegetic LOS-gated ⚠ ENEMY warning glyph
  - Tier 3 baseline regenerated (25 frames, MAD ≤ 5.0)

Deferred to a future polish sub-stage: EnergyShield Shader Graph,
GlassCircuit Shader Graph, DustMote VFX, screen-space HUD prefab,
build-time --ui={full,diegetic} toggle, HDRPAsset showcase/headless
variants, baked lighting, Tier 4 damage regression."
```

- [ ] **Step 3: Verify the tag**

```bash
git tag --list "v1.8*"
```

Expected: `v1.8-unity-art-lean` listed.

- [ ] **Step 4: Sub-stage 12c.8 closed; Stage 12c (lean) closed.**

Report the tag to the user. If the user wants to push the branch + tag to origin, they'll do so manually (not part of this plan to avoid surprises).

---

## Self-review note (planning-agent only — implementing agent skips this)

The plan was reviewed against the design's §1–§7 with the following coverage check:

- §1 project state at entry → covered in plan header + Task 0.1 verification
- §2 goals (9 sub-stages) → one plan sub-stage per goal
- §3 ordering & dependencies → plan sub-stage order matches §3 sequence; Tier 3 re-baseline strategy honored (only at 12c.8)
- §4 per-sub-stage technical specs → each plan sub-stage cites the design's §4.N as its acceptance gate
- §5 risks & mitigations → flagged inline at relevant tasks (Synty conversion in Task 1.5, fog perf in Task 2.5, Shader Graph node-spec drift addressed by behavior-not-name property descriptions, AsyncGPUReadback compat in Task 8.4 fallback, Editor-only authoring assumption stated in plan header)
- §6 implementation handoff notes → embedded in commit messages and the "STOP and report" guidance at every verification step
- §7 approval → plan ends with sub-stage 12c.8 commit + tag, which is the design's exit criterion
