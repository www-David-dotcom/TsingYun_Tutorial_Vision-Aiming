# Arena Art & Vision Reform — Design Spec

> Design version 1.0 — 2026-04-30. Authored against `Aiming_HW` `v1.5-more-todos`. Proposes **Stage 12** of `IMPLEMENTATION_PLAN.md`: a Godot 4 → Unity 2023 LTS HDRP migration of the Aiming Arena, with a coordinated visual reform to land the high-fidelity sci-fi look from the project brief.

---

## 0. TL;DR

Migrate `shared/godot_arena/` from Godot 4 to **Unity 2023 LTS + HDRP** as `shared/unity_arena/`, preserving the proto wire contract and gameplay 1:1 so candidate HW1–HW7 stacks continue to work without modification. Coordinate the migration with a visual reform that delivers the brief's high-fidelity sci-fi aesthetic: a multi-tier maze hybrid map (Map A), stylized-but-compatible chassis surface treatment, DXR ray tracing on the showcase build with screen-space rasterizer fallback for headless and lower-end hardware, team-identity-first palette, full immersive HUD with a build-time toggle separating diegetic and screen-space layers, Synty POLYGON Sci-Fi as the kit-bash base, 60 fps hard floor on RTX 3060 / M2 Pro. Single hero map (Map A) for v1; B and C deferred. Stage 12 is four sub-stages over ~4 calendar weeks; OSS publishing is the final sub-stage only.

## 1. Decision log

| # | Decision | Choice |
|---|---|---|
| 1 | Engine | Unity 2023 LTS + HDRP, port from Godot 4, gameplay preserved 1:1 |
| 2 | Map shape | Multi-tier maze hybrid; 20 × 20 m × 7 m bounded arena |
| 3 | Chassis aesthetic | Stylized but compatible — silhouette envelope and armor-plate locations bit-identical, surface fully reworked |
| 4 | Render target | DXR showcase + rasterizer fallback for headless and lower-end |
| 5 | Color palette | Team-identity-first — true blue / true red on plates; cyan + magenta in architecture |
| 6 | HUD direction | Full immersive with build-time toggle: showcase = diegetic + screen-space; headless = diegetic only |
| 7 | Asset sourcing | Synty POLYGON Sci-Fi paid pack as kit-bash base |
| 8 | Performance budget | 60 fps hard floor on RTX 3060 / M2 Pro; headless must keep up with 200 Hz physics tick |
| 9 | Map roster scope | Single hero map (Map A) for v1; B and C deferred to Stage 13+ |
| 10 | Build cadence | Local-first iteration; OSS publishing only at Stage 12d closure |

## 2. Background & motivation

`shared/godot_arena/` shipped in Stage 2 (`v0.6-arena-poc`) deliberately art-light: BoxMesh / CylinderMesh primitives, `StandardMaterial3D` with low metallic / high roughness, a single `DirectionalLight3D`, basic glow + SSAO post-FX, a 20 × 20 m flat floor, and an empty `assets/shaders/` and `assets/icons/` directory. Stage 7 was supposed to be the visual review milestone but never landed in detail; the schema's §10 decision 1 explicitly authorizes a swap to Unity HDRP if Godot's visual ceiling is missed at the M3 milestone gate. The project brief ("Visual Rendering Description of Sci-Fi 3D FPS Multi-Agent Maze Map Game") describes a high-fidelity look that exceeds what Godot 4 rasterizers can plausibly hit without disproportionate engineering effort, and the user has signed off on the engine swap.

The recruitment-cycle assignment is fully built out (HW1–HW7 closed, grading workflow shipped at Stage 10) and the wire contract between simulator and candidate stacks is the load-bearing invariant the migration must preserve. This spec is written to make that invariant explicit and to gate every stage on it.

## 3. Goals & non-goals

### 3.1 Goals

1. Replace `shared/godot_arena/` with `shared/unity_arena/` running on Unity 2023 LTS + HDRP.
2. Preserve the proto3 wire contract (TCP control on port 7654, RGB888 frame stream on port 7655) byte-for-byte, including JSON envelope shape, length-prefixing, header layout, and field naming.
3. Preserve gameplay 1:1: chassis kinematics, gimbal first-order lag, projectile physics with quadratic drag, four-armor-plate collision damage, episode determinism per seed.
4. Land the brief's visual targets: cyberpunk industrial maze, PBR materials, volumetric lighting, neon edge strips, holographic UI, post-FX, particles.
5. Keep candidate HW1–HW7 public test suites green against the new arena (Tier 5 conformance gate).
6. Local-first development cadence; OSS publishes are deferred to Stage 12d closure.

### 3.2 Non-goals (Stage 12 explicitly out)

- Maps B and C (deferred to Stage 13)
- Spectator free-cam replay viewer (live = first-person; replay re-uses recorded gimbal camera)
- DXR validation on Linux showcase build (Linux ships rasterizer-only)
- Migration of the opponent training pipeline to ML-Agents (current frozen `bronze.pt` / `silver.pt` / `gold.pt` policies continue to play against either arena per Tier 4 gate)
- Deletion of `shared/godot_arena/` (kept ≥2 release cycles as the GPU-less fallback)
- Re-design of the proto schema, scoring formula, or HW deliverables

## 4. Visual content

### 4.1 Map A — multi-tier maze hybrid

Bounded arena, 20 × 20 m footprint × 7 m vertical, with a 1-cell impassable margin so PnP keypoint synthesis and replay framing stay stable. Three vertical bands of content:

- **Ground tier (0 – 2.5 m).** The maze proper. Six interlocking angular corridor segments form a partial maze with three intersection nodes (`JCT-01`, `JCT-02`, `JCT-03`), two glass-partitioned chokepoints, and four small alcoves for cover. Corridor widths 2.4 m (chassis is 0.55 m wide, gives mecanum strafing room). Two diagonal speed-strip lanes per `schema.md §1.1` boost translational velocity within their footprint.
- **Mid tier (2.5 – 4.5 m).** Open-air voids above the corridors. Light shafts cut through here; volumetric fog catches them.
- **Upper tier (4.5 – 7 m).** Two slim catwalks (1.6 m wide) running along the top and bottom edges of the arena — snipe lanes with line-of-sight breaks at half-height glass panels every 4 m. Two ramp connections between ground and upper tier, opposite corners.

Diegetic holographic projector posts at all three intersection nodes: floor bollards with upward emission cones; on top, a `WorldSpace` Canvas displays `JCT-XX` + `(x, z)` grid coordinates ~1.6 m above the floor.

Spawn cells are 2 × 2 m at opposite corners of the arena, mirrored across the long axis. Both spawns have unobstructed line of sight to the central glass-partitioned chokepoint at episode start, so opening engagements reproduce the existing open-arena geometry the bronze policy was trained on.

### 4.2 Chassis & armor plate

**Silhouette envelope preserved bit-identical:** body 0.45 × 0.18 × 0.55 m, four wheels at the existing 4 × 4 grid, gimbal yoke at 0.30 m above body center, barrel length 0.22 m, plate dimensions 0.14 × 0.13 × 0.012 m at the existing offsets. PnP keypoint corners stay valid; HW1 detector geometry stays valid.

**Surface treatment** (where the brief lands):

- **Body**: angular sci-fi armor panels kit-bashed from Synty POLYGON Sci-Fi panel meshes overlaid on the existing collision box. Matte titanium-aluminum (metallic 0.85, roughness 0.45). Gradient luminous trim along edges in team color (true blue `#1F6BFF` or true red `#FF3340`, emission energy 1.8).
- **Mechanical joints**: exposed at wheel hubs and the gimbal yaw / pitch axes — small Synty mechanical kit pieces, glossy chrome (metallic 1.0, roughness 0.12), tiny specular highlights pop against the matte body.
- **Energy shield halo**: transparent ellipsoid mesh ~5 % larger than the chassis bounding box; custom Shader Graph (rim-lit, fresnel-driven, emission animated by a slow noise pan, alpha blended at ~12 %). Subtle by intent — present, not screaming.
- **Armor plates**: same dimensions, layered emission via Shader Graph: base team color (true blue or red, energy 1.5) + class-icon glyph (Hero / Engineer / Standard / Sentry SVG textures sampled as additive overlay) + animated 2 Hz horizontal scanline. The glyph reads cleanly to HW1's classifier; team color preserves opponent identification.
- **Barrel**: black-anodized metal (metallic 0.75, roughness 0.32), faint cyan power-core line emission running along its length, ramping in intensity 200 ms before each fire.

### 4.3 Materials & lighting

Materials (HDRP Lit shader, all PBR):

- **Maze floors**: dark concrete with subtle wear-and-tear normal map, roughness 0.78, slight metallic grit 0.10. Underfoot neon strips embedded in floor seams (10 cm wide cyan emission lines).
- **Maze walls**: matte aluminum panel kit-bash from Synty, roughness 0.55. Selectively glossy carbon-fiber inserts (anisotropic shader, roughness 0.18) on chokepoint structural pieces.
- **Glass partitions**: tempered-glass shader — refraction enabled, smoothness 0.96, transmission 0.78. Embedded animated circuit-line emission (cyan, scrolling at 0.05 m/s, sampled from a procedural texture). DXR-on builds get real screen-space reflections augmented by RT reflection probes; rasterizer builds get planar reflection probes baked offline.
- **Holographic posts**: emissive polygonal projector base + a billboard quad textured with the JCT label and animated grid lines.

Lighting (the brief's "volumetric ray-traced lighting"):

- **Key directional light**: cool-blue at 5 600 K, low intensity (0.6) — establishes an indoor-lit-from-above feel without competing with neon.
- **Linear neon strips** running along all maze edges and corridor ceilings: HDRP Tube Lights, cyan and magenta alternating, ~80 strips total. DXR builds: real RT shadows from chassis. Rasterizer fallback: contact shadows + screen-space.
- **Volumetric fog**: HDRP Local Volumetric Fog volumes inside each corridor, density 0.012, anisotropy −0.2. Light beams passing through neon become visible godrays.
- **Particles**: dust mote particle system (~80 particles per corridor, drift velocity 0.05 m/s), additive blend, emissive cyan with energy multiplier 0.4.
- **Ambient occlusion**: HDRP screen-space GTAO; DXR builds also get RTAO for tight corners.
- **Tone mapping**: ACES, exposure auto-adapt slow.

### 4.4 Diegetic UI

- **Holographic intersection posts** at JCT-01, JCT-02, JCT-03: floor bollards with upward emission cones; top-mounted `WorldSpace` Canvas displaying `JCT-XX` + `(x, z)` grid coords in amber `#FFD966`. Subtle parallax flicker reads as a hologram.
- **Threat-proximity warning glyph**: red-amber `⚠ ENEMY` marker that spawns above an enemy chassis when distance falls below 12 m and remains LOS-gated (visible only when the gimbal camera can actually see the enemy — prevents wallhack).

### 4.5 Screen-space UI (showcase build only)

- **Reticle**: cyan circle + crosshair in the center, predictive lead arc activated when an EKF track exists with confidence > 0.6.
- **Target-lock countdown glyph**: top-center, `TARGET LOCK · 0.42s` matching the firing-delay solver from HW4.
- **Minimap**: 110 × 110 px upper-right, semi-transparent, grid-aligned, shows team blips and visible enemy blips (LOS-gated).
- **Telemetry status bar**: bottom-left, `HP / HEAT / AMMO / YAW / PITCH`, monospace cyan, dim glow.
- **Post-FX volume**: vignette (intensity 0.3), chromatic aberration (intensity 0.15, edges only), motion blur (intensity 0.4, only on rapid yaw events > 90 °/s), bloom (threshold 1.0, intensity 0.8). All driven by HDRP's Volume system.

The screen-space layer is rooted on a `Canvas` set to Screen Space - Overlay; the diegetic layer is rooted on `Canvas` instances set to World Space anchored to scene anchors. The build-time toggle is `--ui={full,diegetic}` interpreted by `ArenaMain.cs` at startup; in `diegetic` mode the screen-space `Canvas` GameObject is `SetActive(false)` and the post-FX Volume's screen-space-only effects are disabled — clean RGB feeds the candidate's HW1 detector.

### 4.6 VFX

- **Muzzle flash**: HDRP Decal-projected emission disc on the barrel tip + a 6-frame VFX Graph particle burst (sparks + bright flash) + a transient point light (intensity 4, duration 50 ms). DXR-on: the point light casts real RT shadows; rasterizer: contact shadow only.
- **Projectile tracer**: thin emissive line renderer (cyan, fades over flight). Currently the projectile is invisible mid-air; the tracer makes it readable.
- **Impact decal**: HDRP Decal projector on hit surfaces — small scorch ring + 4-frame spark VFX Graph burst. Decals fade after 12 s to keep the world clean.
- **Shell casing**: small Rigidbody mesh ejected per shot, real-time shadow casting on, despawn after 4 s. Tiny touch but matches the brief literally.
- **Ambient particles**: dust motes drifting in light shafts (per §4.3 Lighting).

## 5. Engineering substrate

### 5.1 Engine port

New project lives at `shared/unity_arena/` alongside (not replacing) `shared/godot_arena/`. Folder shape mirrors the Godot project structurally:

```
shared/unity_arena/
├── Assets/
│   ├── Scenes/        ArenaMain.unity, MapA_MazeHybrid.unity
│   ├── Prefabs/       Chassis.prefab, Gimbal.prefab, ArmorPlate.prefab,
│   │                  Projectile.prefab, HoloProjector.prefab
│   ├── Scripts/       ArenaMain.cs, Chassis.cs, Gimbal.cs, ArmorPlate.cs,
│   │                  Projectile.cs, ReplayRecorder.cs, SeedRng.cs,
│   │                  TcpProtoServer.cs, TcpFramePub.cs
│   ├── Materials/     Body, Wall_Aluminum, Glass_Tempered, Floor_Concrete, ...
│   ├── Shaders/       EnergyShield.shadergraph, PlateEmission.shadergraph,
│   │                  GlassCircuit.shadergraph, HoloProjector.shadergraph
│   ├── VFX/           MuzzleFlash.vfx, ImpactSpark.vfx, DustMote.vfx
│   ├── Settings/      HDRPAsset_Showcase.asset, HDRPAsset_Headless.asset,
│   │                  Volume_Showcase.asset, Volume_Headless.asset
│   ├── UI/            HudCanvas_ScreenSpace.prefab, HoloMarker_World.prefab
│   └── Synty/POLYGON_SciFi/    (gitignored; fetched manually from Synty by
│                                maintainer; not committed; not OSS-pushed
│                                until Stage 12d)
├── Packages/manifest.json      HDRP, Input System, VFX Graph, ProBuilder,
│                               Shader Graph
├── ProjectSettings/
└── README.md
```

The 10 GDScript files port to 10 C# `MonoBehaviour`s one-to-one. Method bodies are mechanical translations: Godot lifecycle methods (`_ready`, `_process`, `_physics_process`) → Unity (`Awake` / `Start`, `Update`, `FixedUpdate`). Physics: projectile uses Unity `Rigidbody` (matches Godot `RigidBody3D`); chassis uses a custom `MecanumChassisController` (NOT `Rigidbody`-driven) whose velocity-update math line-by-line matches `chassis.gd`'s integrator — see §7 R3 for the rationale.

**Godot project disposition**: kept side-by-side throughout Stage 12. At 12d closure, renamed to `shared/godot_arena_legacy/` (per §8). Deletion of the legacy directory is out of scope for Stage 12 (per §3.2 non-goals; per §8 the legacy dir is kept ≥ 2 release cycles after migration so a GPU-less fallback exists). Existing OSS keys for Godot binaries remain valid.

### 5.2 Wire contract preservation — non-negotiable

Zero change to anything candidate stacks see. Unity opens the same two TCP ports:

| Port | Role | Encoding | Mirrors |
|---|---|---|---|
| 7654 | Control RPC | length-prefixed (4-byte BE) JSON | `tcp_proto_server.gd` byte-for-byte |
| 7655 | Frame stream | 16-byte LE header `<QQ frame_id stamp_ns>` + RGB888 | `tcp_frame_pub.gd` byte-for-byte |

JSON field names match `shared/proto/*.proto` exactly (snake_case). The existing `tests/test_godot_wire_format.py` is renamed `tests/test_arena_wire_format.py` and parametrized over `[godot, unity]` engines so both implementations are conformance-verified on the same inputs. The smoke harness `tools/scripts/smoke_godot_arena.py` is renamed `tools/scripts/smoke_arena.py` and grows an `--engine={godot,unity}` flag.

`SIM_BUILD_SHA` constant in `ArenaMain.cs` is set at build time from the Unity build's git SHA + Unity version; this propagates into `EpisodeStats.simulator_build_sha256`, preserving the determinism contract from `docs/architecture.md` §"Determinism". Opponent `.pt` SHAs and seed paths are unchanged.

### 5.3 Headless RGB rendering

Unity HDRP cannot do GPU-less software rendering. Godot's `--rendering-driver opengl3 --headless` runs on `ubuntu-latest` CI with no GPU; HDRP cannot match this. Two consequences:

1. **CI grading is unaffected.** `schema.md §7.6` already places live-arena episodes out-of-scope for v1 grading. CI runs unit tests on `pull_request.head.sha`, never spinning up the simulator. The GPU requirement does not break the grading workflow.
2. **Candidate-side live arena requires a GPU.** Running the full HW6 integration loop interactively requires any GPU (integrated Intel UHD / Apple Silicon Metal / NVIDIA / AMD all OK). The candidate handbook's Quickstart grows a "GPU required for live arena; CPU-only candidates use the gRPC stub server" note.

**Implementation**: a `HDRPAsset_Headless` preset disables DXR, drops to baked GI only, halves shadow resolution, kills volumetric fog, uses low-LOD chassis. Built via `tools/unity/build.sh --target linux-headless`. The headless arena renders into an offscreen `RenderTexture` at 1280 × 720 RGB888, reads back via `AsyncGPUReadback.Request`, pushes the bytes over TCP frame port 7655 — same wire as Godot. Tested target: 60 fps headroom on integrated graphics.

`shared/godot_arena/` is kept as the **GPU-less escape hatch** until/unless the team decides to commit fully. This preserves a CPU-only path during the pilot cycle while the Unity headless GPU requirement is validated on candidate hardware in the wild.

### 5.4 Build pipeline (local-first)

Builds go to `shared/unity_arena/builds/` (gitignored) — this is the primary dev artifact path. Local smoke tests, Tier 1 – 4 conformance gates, and Tier 5 HW1–HW7 regression all run against locally-built binaries; **zero OSS round-trip required during iteration**.

Wrapper scripts under `tools/unity/`:

```
tools/unity/
├── build.sh           # --target {win-showcase, macos-showcase, linux-showcase, linux-headless}
├── bake_lighting.sh   # invokes Unity in -batchmode to bake lightmaps for Map A
└── README.md
```

Two bake configs: `test` (low quality, ~5 min) for art iteration, `release` (full quality, ~60 min) for tag-cut builds.

Synty POLYGON Sci-Fi pack: maintainer downloads from synty.com once into `Assets/Synty/POLYGON_SciFi/` (gitignored). **Not pre-uploaded to OSS during development.** Synty's license allows shipping the compiled binary but not redistributing source `.fbx` files; an `tools/scripts/check_synty_redistribution.py` CI guard fails the build if Synty source files are detected in any committed path (R2 mitigation).

### 5.5 OSS distribution (Stage 12d only)

`shared/scripts/push_assets.py` invocations are **deferred to Stage 12d** and gated on Tier 1 – 5 all green. Until that gate, no manifest.toml changes, no OSS uploads, no candidate-facing fetch_assets.py entries. Stage 12d closes by uploading:

- Validated showcase + headless binaries → `oss://tsingyun-aiming-hw-public/assets/unity/v2.0/`
- Synty source pack → `oss://tsingyun-aiming-hw-models/assets/synty/POLYGON_SciFi_v1/` (license-private bucket)
- New rows in `shared/assets/manifest.toml`:
  - `unity_arena_showcase_win64` (~600 MB)
  - `unity_arena_showcase_macos` (~700 MB)
  - `unity_arena_showcase_linux` (~600 MB)
  - `unity_arena_headless_linux` (~250 MB)
  - `synty_polygon_scifi_v1` (private)

The candidate handbook (`docs/grading.md`, candidate-facing repo README) is updated in 12d to point at the new OSS keys. Until 12d closes, only maintainers have working Unity binaries; candidates continue running against the existing Godot arena. The migration is backstage until validation completes.

### 5.6 Determinism & replay

A `SeedRng` C# replica feeds every gameplay random source (chassis spawn jitter, projectile spread, anything that affects outcomes). VFX Graph particles are purely cosmetic, marked non-deterministic with their own RNG seeded from frame counter so screenshots vary but episode outcomes don't. Episode JSON replay format (Godot's `user://replays/<episode_id>.json` → Unity's `Application.persistentDataPath/replays/<episode_id>.json`) is byte-identical schema; existing replay parsers continue to work without modification.

## 6. Migration & testing

Five layered conformance gates from cheapest to costliest. Migration only declared "done" when all five pass on local builds.

### 6.1 Tier 1 — Wire-format conformance (CI, no simulator)

`tests/test_arena_wire_format.py` parametrized over `[godot, unity]`. The test does **not** spin up either simulator; following the existing Godot pattern, it hand-constructs the dict shapes that each engine's source (`arena_main.gd` for Godot, `ArenaMain.cs` for Unity) is documented to emit, then verifies the dict round-trips through `google.protobuf.json_format.ParseDict` into the strongly-typed proto message. Catches shape drift between either engine's source and the proto schema. Runs on every commit on `ubuntu-latest` — no GPU needed.

### 6.2 Tier 2 — Smoke harness parity (maintainer dev box, requires GPU for Unity engine)

`tools/scripts/smoke_arena.py --engine={godot,unity} --seed 42 --ticks 30`. Spins up the named simulator, drives it through 30 ticks. The Unity arm requires a GPU (per §5.3); the Godot arm runs anywhere. Run manually before tag cuts; not part of automated CI. Asserts:

- Monotonic `frame_id`
- Gimbal yaw / pitch deltas applied within tolerance (1e-4 rad)
- Fire returns `accepted=true` for valid burst counts
- `EpisodeStats.episode_id` deterministic per seed (`ep-` + 16-hex-digit seed)

### 6.3 Tier 3 — Golden-frame regression (maintainer dev box, requires GPU)

For 5 seeds × 5 fixed gimbal poses (25 frames total): render one 1280 × 720 frame from each engine and compare to a checked-in PNG via SSIM > 0.95. Catches geometric regressions (plate placement, chassis pivot offset, gimbal kinematic drift). Cosmetic differences (fog density, neon intensity) explicitly excluded by SSIM threshold tuning. Reference frames stored in `tests/golden_frames/<seed>_<pose>.png`. Run on the maintainer's GPU-equipped box, manually triggered before each sub-stage tag cut; not part of automated CI.

### 6.4 Tier 4 — Bronze opponent regression (pre-tag)

Frozen `bronze.pt` plays 50 episodes against each engine. Win-rate distributions compared via 2-sample Kolmogorov-Smirnov test; require `p > 0.10` (statistically indistinguishable) before declaring physics parity. Catches subtle drift between Bullet (Godot) and PhysX (Unity) over a 90-second episode. Run on the maintainer's box; ~20 minutes wall-clock.

### 6.5 Tier 5 — HW1–HW7 contract regression (ship gate, maintainer dev box, requires GPU)

Each HW's public test suite runs once with the simulator pointed at Godot, once at Unity. **Identical pass/fail outcomes are required.** This is the load-bearing migration gate: if any HW test fails, migration is not done. Runs on the maintainer's GPU-equipped box at Stage 12d closure; not part of automated CI. After the migration ships, Unity is the simulator-of-record for HW1–HW7; the Godot path remains executable for GPU-less fallback runs but is not regression-tested past the gate.

Specifically:

- HW1: `pytest HW1_armor_detector/tests/public/` + `ctest --test-dir build` for the C++ inferer
- HW2: `ctest -R hw2_tf_graph`
- HW3: `ctest -R hw3_ekf_tracker`
- HW4: `ctest -R hw4_ballistic`
- HW5: `ctest -R hw5_mpc_gimbal`
- HW6: `ctest -R hw6_integration` + a closed-loop smoke episode against bronze
- HW7: `ctest -R hw7_strategy`

## 7. Risks

| ID | Risk | Probability | Impact | Mitigation |
|---|---|---|---|---|
| R1 | Headless GPU requirement breaks ubuntu-latest CI candidate flow | High | Medium | Keep `shared/godot_arena/` as GPU-less fallback; document in `docs/grading.md` and candidate handbook; live-arena CI is already out-of-scope per `schema.md §7.6` so unit-test grading is unaffected |
| R2 | Synty pack source files leak into git or public OSS | Low | High | `.gitignore` `Assets/Synty/`; `tools/scripts/check_synty_redistribution.py` CI guard fails build on `.fbx` in committed paths; Synty source bucket is private (`…-models`) |
| R3 | Mecanum kinematics drift Bullet → PhysX over 90 s | Medium | High | Custom `MecanumChassisController` (not `Rigidbody`-driven) replicating `chassis.gd` integrator math line-by-line; only projectiles use PhysX. Validated by Tier 4 |
| R4 | DXR limited on macOS / Linux | Medium | Low | Rasterizer fallback is the universal path; brief's "ray-traced lighting" approximated via HDRP screen-space tools on non-DXR platforms; explicit known limitation |
| R5 | OSS bandwidth cost (Unity binaries 5 – 7× larger than Godot) | Low | Low | Showcase builds gated behind candidate handbook (not auto-fetched); only headless build (~250 MB) in the default `fetch_assets.py` manifest |
| R6 | Lightmap bake iteration time slows art workflow | Medium | Low | Two bake configs: `test` (~5 min) for iteration, `release` (~60 min) for tag-cut builds |

### 7.1 Open questions

These do not block the design. They surface during implementation planning.

- **O1** — Synty kit-bash art time budget. Pack contains ~500 modular pieces. Maze + chassis surface kit-bash estimate is 5 – 10 days of dedicated art time. Stage 12c needs a named art owner before kickoff.
- **O2** — Spectator free-cam for replay viewer. Brief mentions spectating replays. Live view is first-person (gimbal camera); replays currently re-use the same recorded camera. A free-cam orbiting the arena would be a nice-to-have. **Defer to Stage 13** unless flagged as blocking.
- **O3** — Opponent training environment swap. `godot_rl_agents` is GDScript-specific. Retraining bronze / silver / gold on Unity would require ML-Agents adoption — separate project, deferred indefinitely. Current frozen policies remain valid against either arena per Tier 4.

## 8. Stage 12 phasing

Project is at `v1.5-more-todos` (Stage 11 closed). Reform = Stage 12 on a single branch `stage12/unity-reform`, four sub-stages with intermediate tags. Single engineer + part-time art owner. Calendar ~4 weeks.

| Sub-stage | Days | Deliverables | OSS push? | End tag |
|---|---|---|---|---|
| **12a** Unity scaffold + wire parity | 5 | `shared/unity_arena/` project shell with HDRP, Synty imported (gitignored). 10 GDScripts → 10 C# MonoBehaviours. `TcpProtoServer.cs` + `TcpFramePub.cs` byte-for-byte parity. Tier 1 + Tier 2 conformance pass | No | `v1.6-unity-scaffold` |
| **12b** Map A geometry + chassis silhouette | 5 | Map A multi-tier maze hybrid built via Synty kit-bash + ProBuilder. Chassis / Gimbal / ArmorPlate / Projectile / HoloProjector prefabs (silhouette-preserving, basic mats only). Tier 3 + Tier 4 conformance pass | No | `v1.7-unity-geometry` |
| **12c** Materials, lighting, VFX, UI | 7 | All PBR materials authored. Shader Graph shaders (energy shield, plate emission, glass circuits, holo projector). VFX Graph (muzzle flash, impact, dust). Screen-space HUD (reticle, minimap, telemetry, post-FX). Diegetic holo projectors at 3 nodes. HDRPAsset_Showcase + HDRPAsset_Headless variants. Map A lightmap bake | No | `v1.8-unity-art` |
| **12d** Build, OSS publish, migration cleanup | 3 | `tools/unity/build.sh` × 4 targets (release-mode binaries, locally validated). **Tier 5 gate: HW1–HW7 all green against Unity arena.** Builds + Synty pack pushed to OSS via `push_assets.py`. `shared/assets/manifest.toml` rows added. Docs updated (`architecture.md`, `arena.md`, `CHANGELOG.md`, candidate handbook). `shared/godot_arena/` → `shared/godot_arena_legacy/` (kept on disk and on existing OSS keys for fallback) | **Yes** | `v1.9-unity-launch`, then `v2.0-arena-reform-complete` |

**Out of scope for Stage 12** (deferred to Stage 13+):

- Maps B and C
- Spectator free-cam replay viewer
- DXR validation on Linux showcase build (Linux ships rasterizer-only)
- ML-Agents migration of opponent training pipeline
- Deletion of `shared/godot_arena_legacy/` (keep ≥2 release cycles)

## 9. Files touched / created (summary)

**Created:**

- `shared/unity_arena/` — full Unity 2023 LTS HDRP project (~50 source files: 10 C# scripts, ~20 prefabs, ~15 materials, 4 shader graphs, 3 VFX assets, 2 HDRPAsset variants, 1 lightmap bake)
- `tools/unity/build.sh`, `tools/unity/bake_lighting.sh`, `tools/unity/README.md`
- `tools/scripts/check_synty_redistribution.py` — CI guard (R2 mitigation)
- `tests/golden_frames/<seed>_<pose>.png` × 25 — Tier 3 reference frames
- `docs/superpowers/specs/2026-04-30-arena-art-vision-reform-design.md` (this file)

**Renamed / moved:**

- `tests/test_godot_wire_format.py` → `tests/test_arena_wire_format.py` (parametrized)
- `tools/scripts/smoke_godot_arena.py` → `tools/scripts/smoke_arena.py` (parametrized via `--engine`)
- `docs/godot_arena.md` → `docs/arena.md` (rewritten to cover both engines)
- `shared/godot_arena/` → `shared/godot_arena_legacy/` (Stage 12d)

**Modified:**

- `shared/assets/manifest.toml` — new rows for Unity binaries and Synty pack (Stage 12d)
- `shared/scripts/push_assets.py` — invoked at Stage 12d closure
- `docs/architecture.md` — engine swap diagram
- `docs/CHANGELOG.md` — Stage 12 entries
- `docs/grading.md` — GPU-required note for live arena
- `.gitignore` — `Assets/Synty/`, `shared/unity_arena/builds/`, Unity-specific patterns
- `IMPLEMENTATION_PLAN.md` — Stage 12 section appended

**Deleted:** none (Stage 12 is purely additive on existing files; `godot_arena/` is renamed not deleted).

## 10. Approval

This design is approved by the user across three review chunks (visual content, engineering substrate, migration / risks / phasing) plus a revision pass on local-first build cadence. Implementation plan to be authored by the `superpowers:writing-plans` skill against this spec.
