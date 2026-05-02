# Milestone 5 Visual Effects Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an ambitious competitive neon industrial visual pass for both Unity scenes while preserving gameplay contracts and the core red/blue armor cues.

**Architecture:** Add a shared visual profile, pure-C# readability metrics, runtime presentation components, and one rerunnable editor installer. The installer creates shared materials, patches visual-only prefab details, and applies scene dressing to `MapA_MazeHybrid` and `TrainingGround`; tests verify the helper math and critical component wiring before visual QA verifies screenshots.

**Tech Stack:** Unity 6000.3.14f1, C# MonoBehaviours, ScriptableObject profiles, UnityEditor prefab/scene tooling, Unity Test Framework EditMode/PlayMode tests, current Built-in render pipeline with HDRP-compatible fallbacks where available.

---

## Scope Notes

The visual direction is intentionally ambitious. Readability checks are guardrails against destructive failures, not an excuse to keep the scene sparse. External or generated assets are allowed if they are legally safe for the repo, but the first implementation should still create a strong procedural/editor-driven pass so the milestone can be completed without waiting on asset sourcing.

The lighting target is dark cyberpunk, not daylight sci-fi. Sunlight and broad
directional illumination should be dimmed aggressively; local neon strips,
armor emitters, holograms, muzzle flashes, rule markers, and controlled rim
lights should carry the scene.

The Unity editor currently reports the active rendering pipeline as Built-in even though HDRP assets exist. Every material helper must set Built-in-safe color/emission properties first, then opportunistically set HDRP properties when they exist.

## File Structure

- Create `shared/unity_arena/Assets/Scripts/Visual/VisualPolishProfile.cs`
  - Owns approved colors, emission strengths, trail settings, atmosphere settings, and thresholds.
  - Provides `CreateRuntimeDefault()` so runtime scripts have a fallback if no asset reference is assigned.

- Create `shared/unity_arena/Assets/Scripts/Visual/ArenaReadabilityMetrics.cs`
  - Pure helper methods for luminance, contrast, red/blue dominance, overexposure, profile validation, and pixel-ratio checks.

- Create `shared/unity_arena/Assets/Scripts/Visual/ArmorPlateVisual.cs`
  - Applies exact base colors, exact-color internal light, emission, and hit pulse.

- Create `shared/unity_arena/Assets/Scripts/Visual/ProjectileTrailVisual.cs`
  - Configures trail renderer and spawns visual-only muzzle/impact flashes.

- Modify `shared/unity_arena/Assets/Scripts/ArmorPlate.cs`
  - Delegate plate color/glow/pulse to `ArmorPlateVisual`, with a compatible fallback.

- Modify `shared/unity_arena/Assets/Scripts/Projectile.cs`
  - Notify `ProjectileTrailVisual` on arm and consume without changing physics or damage.

- Modify `shared/unity_arena/Assets/Scripts/RuleZoneMarkerRenderer.cs`
  - Use `VisualPolishProfile` colors and material helpers.

- Modify `shared/unity_arena/Assets/Scripts/HoloProjector.cs`
  - Use `VisualPolishProfile` for label and emissive visual refresh.

- Create `shared/unity_arena/Assets/Editor/VisualPolishInstaller.cs`
  - Menu item `TsingYun/Install Visual Polish`.
  - Creates profile/material assets, patches prefabs, applies scene polish, logs summary.

- Create `shared/unity_arena/Assets/Tests/EditMode/ArenaReadabilityMetricsTests.cs`
  - Tests pure helper math and default profile sanity.

- Create `shared/unity_arena/Assets/Tests/EditMode/VisualPolishRuntimeTests.cs`
  - Tests component behavior on temporary GameObjects.

- Create `shared/unity_arena/Assets/Tests/EditMode/VisualPolishPrefabTests.cs`
  - Tests installed prefab/scene-critical visual components after the installer has run.

- Create `docs/visual-polish-qa.md`
  - Describes the final look, QA commands, screenshot checklist, and known limits.

## Task 1: Readability Metrics And Profile Foundation

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/Visual/VisualPolishProfile.cs`
- Create: `shared/unity_arena/Assets/Scripts/Visual/ArenaReadabilityMetrics.cs`
- Create: `shared/unity_arena/Assets/Tests/EditMode/ArenaReadabilityMetricsTests.cs`

- [ ] **Step 1: Write failing EditMode tests**

Create `shared/unity_arena/Assets/Tests/EditMode/ArenaReadabilityMetricsTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class ArenaReadabilityMetricsTests
    {
        [Test]
        public void RedDominance_AcceptsPureRedAndRejectsMagenta()
        {
            Assert.IsTrue(ArenaReadabilityMetrics.IsRedDominant(Color.red));
            Assert.IsFalse(ArenaReadabilityMetrics.IsRedDominant(new Color(1f, 0f, 0.8f, 1f)));
        }

        [Test]
        public void BlueDominance_AcceptsPureBlueAndRejectsCyan()
        {
            Assert.IsTrue(ArenaReadabilityMetrics.IsBlueDominant(Color.blue));
            Assert.IsFalse(ArenaReadabilityMetrics.IsBlueDominant(new Color(0f, 0.8f, 1f, 1f)));
        }

        [Test]
        public void ContrastDifference_SeparatesDarkMetalFromNeon()
        {
            float contrast = ArenaReadabilityMetrics.LuminanceDifference(
                new Color(0.04f, 0.06f, 0.08f, 1f),
                new Color(0.0f, 0.9f, 1.0f, 1f));
            Assert.Greater(contrast, 0.45f);
        }

        [Test]
        public void OverexposureRatio_CountsOnlyNearWhitePixels()
        {
            var pixels = new[]
            {
                new Color32(255, 255, 255, 255),
                new Color32(250, 248, 246, 255),
                new Color32(255, 0, 0, 255),
                new Color32(0, 0, 255, 255),
            };

            Assert.AreEqual(0.5f, ArenaReadabilityMetrics.OverexposureRatio(pixels), 1e-6f);
        }

        [Test]
        public void DefaultProfile_HasExactTeamColorsAndAmbitiousEmission()
        {
            VisualPolishProfile profile = VisualPolishProfile.CreateRuntimeDefault();

            Assert.AreEqual(Color.red, profile.RedArmorColor);
            Assert.AreEqual(Color.blue, profile.BlueArmorColor);
            Assert.GreaterOrEqual(profile.ArmorEmissionMax, 7f);
            Assert.Greater(profile.NeonAccentTargetRatio, 0.03f);
            Assert.IsTrue(ArenaReadabilityMetrics.TryValidateProfile(profile, out string message), message);
        }
    }
}
```

- [ ] **Step 2: Run the failing tests**

Run with Unity MCP:

```text
run_tests(mode: "EditMode", test_names: ["TsingYun.UnityArena.Tests.EditMode.ArenaReadabilityMetricsTests"], include_failed_tests: true)
```

Expected: FAIL because `ArenaReadabilityMetrics` and `VisualPolishProfile` do not exist.

- [ ] **Step 3: Create the visual profile**

Create `shared/unity_arena/Assets/Scripts/Visual/VisualPolishProfile.cs`:

```csharp
using UnityEngine;

namespace TsingYun.UnityArena
{
    [CreateAssetMenu(menuName = "TsingYun/Visual Polish Profile", fileName = "CompetitiveNeonIndustrial")]
    public class VisualPolishProfile : ScriptableObject
    {
        public Color RedArmorColor = Color.red;
        public Color BlueArmorColor = Color.blue;
        public Color CyanAccentColor = new Color(0f, 0.92f, 1f, 1f);
        public Color MagentaAccentColor = new Color(1f, 0.12f, 0.82f, 1f);
        public Color DarkMetalColor = new Color(0.035f, 0.045f, 0.055f, 1f);
        public Color CarbonPanelColor = new Color(0.08f, 0.095f, 0.11f, 1f);
        public Color GlassTintColor = new Color(0.25f, 0.85f, 1f, 0.28f);
        public Color BoostColor = new Color(0f, 1f, 0.78f, 0.36f);
        public Color BlueHealingColor = new Color(0f, 0.18f, 1f, 0.32f);
        public Color RedHealingColor = new Color(1f, 0f, 0f, 0.32f);
        public Color HologramTextColor = new Color(0.72f, 0.98f, 1f, 1f);
        public Color DarkAmbientColor = new Color(0.005f, 0.008f, 0.012f, 1f);

        [Min(0f)] public float ArmorEmissionMin = 3.0f;
        [Min(0f)] public float ArmorEmissionMax = 8.5f;
        [Min(0f)] public float ArmorInternalLightIntensity = 2.2f;
        [Min(0f)] public float HitPulseEmissionBoost = 4.0f;
        [Min(0f)] public float HitPulseSeconds = 0.18f;

        [Min(0f)] public float ProjectileTrailSeconds = 0.35f;
        [Min(0f)] public float ProjectileTrailStartWidth = 0.055f;
        [Min(0f)] public float ProjectileTrailEndWidth = 0.012f;
        [Min(0f)] public float MuzzleFlashSeconds = 0.08f;
        [Min(0f)] public float ImpactFlashSeconds = 0.16f;

        [Range(0f, 1f)] public float MaxOverexposureRatio = 0.14f;
        [Range(0f, 1f)] public float MinTeamPixelRatio = 0.002f;
        [Range(0f, 1f)] public float NeonAccentTargetRatio = 0.055f;
        [Range(0f, 1f)] public float MaxNeonAccentRatio = 0.34f;
        [Range(0f, 2f)] public float MaxDirectionalLightIntensity = 0.28f;
        [Range(0f, 1f)] public float SceneAmbientIntensity = 0.04f;

        public static VisualPolishProfile CreateRuntimeDefault()
        {
            var profile = CreateInstance<VisualPolishProfile>();
            profile.name = "CompetitiveNeonIndustrialRuntimeDefault";
            return profile;
        }

        public Color TeamColor(string team)
        {
            return string.Equals(team, "red", System.StringComparison.OrdinalIgnoreCase)
                ? RedArmorColor
                : BlueArmorColor;
        }
    }
}
```

- [ ] **Step 4: Create readability metrics**

Create `shared/unity_arena/Assets/Scripts/Visual/ArenaReadabilityMetrics.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace TsingYun.UnityArena
{
    public static class ArenaReadabilityMetrics
    {
        public static float RelativeLuminance(Color color)
        {
            return 0.2126f * LinearChannel(color.r)
                + 0.7152f * LinearChannel(color.g)
                + 0.0722f * LinearChannel(color.b);
        }

        public static float LuminanceDifference(Color a, Color b)
        {
            return Mathf.Abs(RelativeLuminance(a) - RelativeLuminance(b));
        }

        public static bool IsRedDominant(Color color, float minRed = 0.86f, float maxOther = 0.28f)
        {
            return color.r >= minRed && color.g <= maxOther && color.b <= maxOther;
        }

        public static bool IsBlueDominant(Color color, float minBlue = 0.86f, float maxOther = 0.28f)
        {
            return color.b >= minBlue && color.r <= maxOther && color.g <= maxOther;
        }

        public static float OverexposureRatio(IReadOnlyList<Color32> pixels, byte threshold = 245)
        {
            if (pixels == null || pixels.Count == 0) return 0f;
            int over = 0;
            for (int i = 0; i < pixels.Count; i++)
            {
                Color32 p = pixels[i];
                if (p.r >= threshold && p.g >= threshold && p.b >= threshold) over++;
            }

            return (float)over / pixels.Count;
        }

        public static float TeamPixelRatio(IReadOnlyList<Color32> pixels, bool redTeam)
        {
            if (pixels == null || pixels.Count == 0) return 0f;
            int count = 0;
            for (int i = 0; i < pixels.Count; i++)
            {
                Color c = pixels[i];
                if (redTeam ? IsRedDominant(c) : IsBlueDominant(c)) count++;
            }

            return (float)count / pixels.Count;
        }

        public static bool TryValidateProfile(VisualPolishProfile profile, out string message)
        {
            if (profile == null)
            {
                message = "VisualPolishProfile is null.";
                return false;
            }

            if (profile.RedArmorColor != Color.red)
            {
                message = "Red armor color must be exact #FF0000.";
                return false;
            }

            if (profile.BlueArmorColor != Color.blue)
            {
                message = "Blue armor color must be exact #0000FF.";
                return false;
            }

            if (profile.ArmorEmissionMax < profile.ArmorEmissionMin)
            {
                message = "ArmorEmissionMax must be >= ArmorEmissionMin.";
                return false;
            }

            if (profile.MaxNeonAccentRatio < profile.NeonAccentTargetRatio)
            {
                message = "MaxNeonAccentRatio must be >= NeonAccentTargetRatio.";
                return false;
            }

            message = "Profile is valid.";
            return true;
        }

        private static float LinearChannel(float value)
        {
            value = Mathf.Clamp01(value);
            return value <= 0.04045f
                ? value / 12.92f
                : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
        }
    }
}
```

- [ ] **Step 5: Run tests and commit**

Run with Unity MCP:

```text
run_tests(mode: "EditMode", test_names: ["TsingYun.UnityArena.Tests.EditMode.ArenaReadabilityMetricsTests"], include_failed_tests: true)
```

Expected: PASS.

Commit:

```bash
git add shared/unity_arena/Assets/Scripts/Visual shared/unity_arena/Assets/Tests/EditMode/ArenaReadabilityMetricsTests.cs
git commit -m "feat: add visual polish profile metrics"
```

## Task 2: Runtime Armor And Projectile Visuals

**Files:**
- Create: `shared/unity_arena/Assets/Scripts/Visual/ArmorPlateVisual.cs`
- Create: `shared/unity_arena/Assets/Scripts/Visual/ProjectileTrailVisual.cs`
- Modify: `shared/unity_arena/Assets/Scripts/ArmorPlate.cs`
- Modify: `shared/unity_arena/Assets/Scripts/Projectile.cs`
- Create: `shared/unity_arena/Assets/Tests/EditMode/VisualPolishRuntimeTests.cs`

- [ ] **Step 1: Write failing runtime component tests**

Create `shared/unity_arena/Assets/Tests/EditMode/VisualPolishRuntimeTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class VisualPolishRuntimeTests
    {
        [Test]
        public void ArmorPlateVisual_CreatesExactColorInternalLight()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var visual = go.AddComponent<ArmorPlateVisual>();
                var profile = VisualPolishProfile.CreateRuntimeDefault();

                visual.RefreshGlow("red", 1f, profile);

                Light light = go.transform.Find("Visual_ArmorInternalLight").GetComponent<Light>();
                Assert.AreEqual(Color.red, light.color);
                Assert.Greater(light.intensity, 0f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ArmorPlate_RefreshGlow_UsesExactTeamBaseColor()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var plate = go.AddComponent<ArmorPlate>();
                go.AddComponent<ArmorPlateVisual>();
                plate.Team = "blue";

                plate.RefreshGlow(1f);

                var renderer = go.GetComponent<MeshRenderer>();
                Assert.AreEqual(Color.blue, renderer.sharedMaterial.GetColor("_BaseColor"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ProjectileTrailVisual_DoesNotCreatePhysicsComponents()
        {
            var go = new GameObject("ProjectileVisual");
            try
            {
                var visual = go.AddComponent<ProjectileTrailVisual>();
                visual.OnArmed(Vector3.forward * 20f, "blue", VisualPolishProfile.CreateRuntimeDefault());

                Assert.IsNull(go.GetComponent<Collider>());
                Assert.IsNull(go.GetComponent<Rigidbody>());
                Assert.IsNotNull(go.GetComponent<TrailRenderer>());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
```

- [ ] **Step 2: Run the failing tests**

Run with Unity MCP:

```text
run_tests(mode: "EditMode", test_names: ["TsingYun.UnityArena.Tests.EditMode.VisualPolishRuntimeTests"], include_failed_tests: true)
```

Expected: FAIL because the runtime visual scripts do not exist.

- [ ] **Step 3: Create `ArmorPlateVisual`**

Create `shared/unity_arena/Assets/Scripts/Visual/ArmorPlateVisual.cs`:

```csharp
using UnityEngine;

namespace TsingYun.UnityArena
{
    [DisallowMultipleComponent]
    public class ArmorPlateVisual : MonoBehaviour
    {
        public MeshRenderer PlateRenderer;
        public Light InternalLight;
        public VisualPolishProfile Profile;

        private float _pulseUntil;

        private void Awake()
        {
            if (PlateRenderer == null) PlateRenderer = GetComponent<MeshRenderer>();
            EnsureInternalLight();
        }

        private void Update()
        {
            if (Time.time <= _pulseUntil && InternalLight != null)
            {
                InternalLight.intensity = Mathf.Max(InternalLight.intensity, ResolveProfile().ArmorInternalLightIntensity * 2.2f);
            }
        }

        public void RefreshGlow(string team, float hpRatio, VisualPolishProfile profile = null)
        {
            Profile = profile != null ? profile : Profile;
            VisualPolishProfile resolved = ResolveProfile();
            Color teamColor = resolved.TeamColor(team);
            float t = Mathf.Clamp01(hpRatio);
            float emission = Mathf.Lerp(resolved.ArmorEmissionMax, resolved.ArmorEmissionMin, t);

            if (PlateRenderer == null) PlateRenderer = GetComponent<MeshRenderer>();
            if (PlateRenderer != null)
            {
                Material mat = Application.isPlaying ? PlateRenderer.material : PlateRenderer.sharedMaterial;
                if (mat != null)
                {
                    SetMaterialColor(mat, "_BaseColor", teamColor);
                    SetMaterialColor(mat, "_Color", teamColor);
                    SetMaterialColor(mat, "_EmissionColor", teamColor * emission);
                    SetMaterialColor(mat, "_EmissiveColor", teamColor * emission);
                    mat.EnableKeyword("_EMISSION");
                }
            }

            EnsureInternalLight();
            if (InternalLight != null)
            {
                InternalLight.color = teamColor;
                InternalLight.intensity = resolved.ArmorInternalLightIntensity + emission * 0.2f;
                InternalLight.range = 1.1f;
            }
        }

        public void PulseHit(string team)
        {
            VisualPolishProfile resolved = ResolveProfile();
            _pulseUntil = Time.time + resolved.HitPulseSeconds;
            if (InternalLight != null)
            {
                InternalLight.color = resolved.TeamColor(team);
                InternalLight.intensity = resolved.ArmorInternalLightIntensity + resolved.HitPulseEmissionBoost;
            }
        }

        private VisualPolishProfile ResolveProfile()
        {
            return Profile != null ? Profile : VisualPolishProfile.CreateRuntimeDefault();
        }

        private void EnsureInternalLight()
        {
            if (InternalLight != null) return;
            Transform existing = transform.Find("Visual_ArmorInternalLight");
            GameObject lightObject = existing != null ? existing.gameObject : new GameObject("Visual_ArmorInternalLight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = Vector3.zero;
            InternalLight = lightObject.GetComponent<Light>();
            if (InternalLight == null) InternalLight = lightObject.AddComponent<Light>();
            InternalLight.type = LightType.Point;
            InternalLight.shadows = LightShadows.None;
            InternalLight.range = 1.1f;
        }

        private static void SetMaterialColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }
    }
}
```

- [ ] **Step 4: Create `ProjectileTrailVisual`**

Create `shared/unity_arena/Assets/Scripts/Visual/ProjectileTrailVisual.cs`:

```csharp
using UnityEngine;

namespace TsingYun.UnityArena
{
    [DisallowMultipleComponent]
    public class ProjectileTrailVisual : MonoBehaviour
    {
        public VisualPolishProfile Profile;
        public TrailRenderer Trail;

        public void OnArmed(Vector3 initialVelocity, string team, VisualPolishProfile profile = null)
        {
            Profile = profile != null ? profile : Profile;
            VisualPolishProfile resolved = ResolveProfile();
            Color color = resolved.TeamColor(team);

            if (Trail == null) Trail = GetComponent<TrailRenderer>();
            if (Trail == null) Trail = gameObject.AddComponent<TrailRenderer>();

            Trail.time = resolved.ProjectileTrailSeconds;
            Trail.startWidth = resolved.ProjectileTrailStartWidth;
            Trail.endWidth = resolved.ProjectileTrailEndWidth;
            Trail.numCornerVertices = 4;
            Trail.numCapVertices = 4;
            Trail.material = BuildAdditiveMaterial(color, "Visual_ProjectileTrail_Runtime");
            Trail.startColor = color;
            Trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }

        public void PlayImpact(string reason, string team)
        {
            SpawnFlash($"Visual_ImpactFlash_{reason}", transform.position, ResolveProfile().TeamColor(team), ResolveProfile().ImpactFlashSeconds, 1.4f);
        }

        public static void PlayMuzzleFlash(Transform muzzle, string team, VisualPolishProfile profile = null)
        {
            if (muzzle == null) return;
            VisualPolishProfile resolved = profile != null ? profile : VisualPolishProfile.CreateRuntimeDefault();
            SpawnFlash("Visual_MuzzleFlash", muzzle.position, resolved.TeamColor(team), resolved.MuzzleFlashSeconds, 0.8f);
        }

        private VisualPolishProfile ResolveProfile()
        {
            return Profile != null ? Profile : VisualPolishProfile.CreateRuntimeDefault();
        }

        private static void SpawnFlash(string name, Vector3 position, Color color, float lifetime, float range)
        {
            var flash = new GameObject(name);
            flash.transform.position = position;
            var light = flash.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = 2.8f;
            light.range = range;
            light.shadows = LightShadows.None;
            Object.Destroy(flash, Mathf.Max(0.02f, lifetime));
        }

        private static Material BuildAdditiveMaterial(Color color, string name)
        {
            Shader shader = Shader.Find("Sprites/Default");
            var material = new Material(shader != null ? shader : Shader.Find("Standard"));
            material.name = name;
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color * 3f);
                material.EnableKeyword("_EMISSION");
            }
            return material;
        }
    }
}
```

- [ ] **Step 5: Modify `ArmorPlate`**

In `shared/unity_arena/Assets/Scripts/ArmorPlate.cs`, add a serialized visual field near the renderers:

```csharp
[SerializeField] private ArmorPlateVisual plateVisual;
```

Replace `RefreshGlow(float t)` with:

```csharp
public void RefreshGlow(float t)
{
    if (plateVisual == null) plateVisual = GetComponent<ArmorPlateVisual>();
    if (plateVisual != null)
    {
        plateVisual.RefreshGlow(Team, t);
        return;
    }

    if (plateRenderer == null || plateRenderer.material == null) return;
    Color teamColor = Team == "red" ? Color.red : Color.blue;
    float energy = Mathf.Lerp(3.0f, 8.5f, 1f - t);
    var mat = plateRenderer.material;
    mat.color = teamColor;
    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", teamColor);
    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", teamColor * energy);
    mat.EnableKeyword("_EMISSION");
}
```

Inside `OnTriggerEnter`, before `PlateHit?.Invoke(...)`, add:

```csharp
if (plateVisual == null) plateVisual = GetComponent<ArmorPlateVisual>();
if (plateVisual != null) plateVisual.PulseHit(Team);
```

- [ ] **Step 6: Modify `Projectile`**

In `shared/unity_arena/Assets/Scripts/Projectile.cs`, add:

```csharp
private ProjectileTrailVisual _visual;
```

Replace `Awake()` with:

```csharp
private void Awake()
{
    _rb = GetComponent<Rigidbody>();
    _visual = GetComponent<ProjectileTrailVisual>();
}
```

At the end of `Arm(...)`, add:

```csharp
if (_visual == null) _visual = GetComponent<ProjectileTrailVisual>();
if (_visual != null) _visual.OnArmed(initialVelocity, owningTeam);
```

At the start of `Consume(string reason)`, before `Consumed = true;`, add:

```csharp
if (_visual == null) _visual = GetComponent<ProjectileTrailVisual>();
if (_visual != null) _visual.PlayImpact(reason, Team);
```

- [ ] **Step 7: Run tests and commit**

Run:

```text
run_tests(mode: "EditMode", test_names: ["TsingYun.UnityArena.Tests.EditMode.VisualPolishRuntimeTests"], include_failed_tests: true)
```

Expected: PASS.

Run existing focused tests:

```text
run_tests(mode: "EditMode", test_names: ["TsingYun.UnityArena.Tests.EditMode.ArmorPlateTests", "TsingYun.UnityArena.Tests.EditMode.ProjectileDragTests"], include_failed_tests: true)
```

Expected: PASS.

Commit:

```bash
git add shared/unity_arena/Assets/Scripts/Visual shared/unity_arena/Assets/Scripts/ArmorPlate.cs shared/unity_arena/Assets/Scripts/Projectile.cs shared/unity_arena/Assets/Tests/EditMode/VisualPolishRuntimeTests.cs
git commit -m "feat: add runtime arena visual effects"
```

## Task 3: Profile-Driven Rule Markers And Holograms

**Files:**
- Modify: `shared/unity_arena/Assets/Scripts/RuleZoneMarkerRenderer.cs`
- Modify: `shared/unity_arena/Assets/Scripts/HoloProjector.cs`
- Modify: `shared/unity_arena/Assets/Tests/PlayMode/RuleZonePresentationTests.cs`

- [ ] **Step 1: Extend PlayMode marker test**

Append this test to `RuleZonePresentationTests` before `UnityTearDown`:

```csharp
[UnityTest]
public IEnumerator RuleZoneMarkerRenderer_UsesProfileBoostColor()
{
    _root = new GameObject("RuleZoneProfileHost");
    var renderer = _root.AddComponent<RuleZoneMarkerRenderer>();
    var profile = VisualPolishProfile.CreateRuntimeDefault();
    profile.BoostColor = new Color(0.1f, 1f, 0.7f, 0.42f);
    renderer.Profile = profile;

    renderer.RenderBoostPoints(
        new[] { Vector3.zero },
        new[] { BoostPointHolder.Unheld },
        boostRadius: 1.5f);
    yield return null;

    var markerRenderer = _root.transform.Find("RuleMarkers/BoostPoint_1").GetComponent<MeshRenderer>();
    Assert.AreEqual(profile.BoostColor, markerRenderer.sharedMaterial.GetColor("_BaseColor"));
}
```

- [ ] **Step 2: Run failing PlayMode test**

Run:

```text
run_tests(mode: "PlayMode", test_names: ["TsingYun.UnityArena.Tests.PlayMode.RuleZonePresentationTests.RuleZoneMarkerRenderer_UsesProfileBoostColor"], include_failed_tests: true)
```

Expected: FAIL because `RuleZoneMarkerRenderer.Profile` does not exist.

- [ ] **Step 3: Modify `RuleZoneMarkerRenderer`**

Add a public profile field:

```csharp
public VisualPolishProfile Profile;
```

Replace the static color fields with fallback-only values:

```csharp
private static readonly Color DefaultBlueHealingColor = new Color(0f, 0.25f, 1f, 0.22f);
private static readonly Color DefaultRedHealingColor = new Color(1f, 0f, 0f, 0.22f);
private static readonly Color DefaultBoostPointColor = new Color(0f, 1f, 0.8f, 0.28f);
```

In `RenderHealingZones`, resolve profile colors:

```csharp
VisualPolishProfile profile = ResolveProfile();
Color blue = profile != null ? profile.BlueHealingColor : DefaultBlueHealingColor;
Color red = profile != null ? profile.RedHealingColor : DefaultRedHealingColor;
_blueHealingMarker = EnsureMarker("HealingZone_Blue", bluePosition, healingRadius, blue);
_redHealingMarker = EnsureMarker("HealingZone_Red", redPosition, healingRadius, red);
```

In `EnsureBoostMarker`, use:

```csharp
Color boostColor = ResolveProfile() != null ? ResolveProfile().BoostColor : DefaultBoostPointColor;
marker = EnsureMarker($"BoostPoint_{index + 1}", position, radius, boostColor);
```

Add:

```csharp
private VisualPolishProfile ResolveProfile()
{
    return Profile != null ? Profile : VisualPolishProfile.CreateRuntimeDefault();
}
```

Update `BuildMarkerMaterial` so it sets `_BaseColor`, `_Color`, `_EmissionColor`, and `_EmissiveColor` when present.

- [ ] **Step 4: Modify `HoloProjector`**

Add profile-driven refresh:

```csharp
public VisualPolishProfile Profile;
public Renderer[] EmissiveRenderers;

public void RefreshVisuals()
{
    VisualPolishProfile profile = Profile != null ? Profile : VisualPolishProfile.CreateRuntimeDefault();
    if (LabelText != null)
    {
        LabelText.color = profile.HologramTextColor;
        LabelText.text = $"{JunctionId}\n({GridCoords.x:+0.0;-0.0}, {GridCoords.y:+0.0;-0.0})";
    }

    if (EmissiveRenderers == null) return;
    foreach (Renderer r in EmissiveRenderers)
    {
        if (r == null || r.sharedMaterial == null) continue;
        Material mat = Application.isPlaying ? r.material : r.sharedMaterial;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", profile.CyanAccentColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", profile.CyanAccentColor);
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", profile.CyanAccentColor * 3f);
        if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", profile.CyanAccentColor * 3f);
        mat.EnableKeyword("_EMISSION");
    }
}
```

Call `RefreshVisuals()` from `Start()`.

- [ ] **Step 5: Run tests and commit**

Run:

```text
run_tests(mode: "PlayMode", test_names: ["TsingYun.UnityArena.Tests.PlayMode.RuleZonePresentationTests"], include_failed_tests: true)
```

Expected: PASS.

Commit:

```bash
git add shared/unity_arena/Assets/Scripts/RuleZoneMarkerRenderer.cs shared/unity_arena/Assets/Scripts/HoloProjector.cs shared/unity_arena/Assets/Tests/PlayMode/RuleZonePresentationTests.cs
git commit -m "feat: profile rule markers and holograms"
```

## Task 4: Visual Polish Installer And Prefab Cohesion

**Files:**
- Create: `shared/unity_arena/Assets/Editor/VisualPolishInstaller.cs`
- Create or modify through installer: `shared/unity_arena/Assets/Visual/CompetitiveNeonIndustrial.asset`
- Create or modify through installer: `shared/unity_arena/Assets/Materials/VisualPolish/*.mat`
- Modify through installer: `shared/unity_arena/Assets/Prefabs/ArmorPlate.prefab`
- Modify through installer: `shared/unity_arena/Assets/Prefabs/Chassis.prefab`
- Modify through installer: `shared/unity_arena/Assets/Prefabs/Gimbal.prefab`
- Modify through installer: `shared/unity_arena/Assets/Prefabs/Projectile.prefab`
- Create: `shared/unity_arena/Assets/Tests/EditMode/VisualPolishPrefabTests.cs`

- [ ] **Step 1: Write prefab verification tests**

Create `shared/unity_arena/Assets/Tests/EditMode/VisualPolishPrefabTests.cs`:

```csharp
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class VisualPolishPrefabTests
    {
        [Test]
        public void ChassisPrefab_HasVisualCohesionChildren()
        {
            GameObject chassis = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Chassis.prefab");
            try
            {
                Assert.IsNotNull(chassis.transform.Find("Pedestal/Visual_WheelMount_FL"));
                Assert.IsNotNull(chassis.transform.Find("Pedestal/Visual_WheelMount_FR"));
                Assert.IsNotNull(chassis.transform.Find("Pedestal/Visual_WheelMount_RL"));
                Assert.IsNotNull(chassis.transform.Find("Pedestal/Visual_WheelMount_RR"));
                Assert.IsNotNull(chassis.transform.Find("Gimbal/Visual_TurretBearing"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(chassis);
            }
        }

        [Test]
        public void ArmorPlatePrefab_HasInternalGlowVisual()
        {
            GameObject plate = PrefabUtility.LoadPrefabContents("Assets/Prefabs/ArmorPlate.prefab");
            try
            {
                Assert.IsNotNull(plate.GetComponent<ArmorPlateVisual>());
                Assert.IsNotNull(plate.transform.Find("Visual_ArmorInternalLight"));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(plate);
            }
        }

        [Test]
        public void ProjectilePrefab_HasTrailVisualWithoutExtraCollider()
        {
            GameObject projectile = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Projectile.prefab");
            try
            {
                Assert.IsNotNull(projectile.GetComponent<ProjectileTrailVisual>());
                Assert.IsNotNull(projectile.GetComponent<TrailRenderer>());
                Assert.AreEqual(1, projectile.GetComponents<Collider>().Length);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(projectile);
            }
        }
    }
}
```

- [ ] **Step 2: Run tests before installer**

Run:

```text
run_tests(mode: "EditMode", test_names: ["TsingYun.UnityArena.Tests.EditMode.VisualPolishPrefabTests"], include_failed_tests: true)
```

Expected: FAIL because the prefabs have not been patched.

- [ ] **Step 3: Create `VisualPolishInstaller`**

Create `shared/unity_arena/Assets/Editor/VisualPolishInstaller.cs` with these concrete responsibilities:

```csharp
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TsingYun.UnityArena.EditorUtilities
{
    public static class VisualPolishInstaller
    {
        private const string ProfilePath = "Assets/Visual/CompetitiveNeonIndustrial.asset";
        private const string MaterialRoot = "Assets/Materials/VisualPolish";
        private const string ArmorPlatePrefabPath = "Assets/Prefabs/ArmorPlate.prefab";
        private const string ChassisPrefabPath = "Assets/Prefabs/Chassis.prefab";
        private const string GimbalPrefabPath = "Assets/Prefabs/Gimbal.prefab";
        private const string ProjectilePrefabPath = "Assets/Prefabs/Projectile.prefab";

        [MenuItem("TsingYun/Install Visual Polish")]
        public static void Install()
        {
            Directory.CreateDirectory("Assets/Visual");
            Directory.CreateDirectory(MaterialRoot);

            VisualPolishProfile profile = EnsureProfile();
            Material darkMetal = EnsureMaterial("DarkMetal.mat", profile.DarkMetalColor, 0.2f, 0.75f);
            Material carbon = EnsureMaterial("CarbonPanel.mat", profile.CarbonPanelColor, 0.0f, 0.55f);
            Material cyan = EnsureMaterial("CyanNeon.mat", profile.CyanAccentColor, 0.0f, 0.15f, profile.CyanAccentColor * 4f);
            Material magenta = EnsureMaterial("MagentaNeon.mat", profile.MagentaAccentColor, 0.0f, 0.15f, profile.MagentaAccentColor * 4f);

            PatchArmorPlatePrefab(profile);
            PatchProjectilePrefab(profile);
            PatchGimbalPrefab(darkMetal, cyan, magenta);
            PatchChassisPrefab(darkMetal, carbon, cyan, magenta);
            ApplyScenePolish("Assets/Scenes/MapA_MazeHybrid.unity", profile, darkMetal, carbon, cyan, magenta, training: false);
            ApplyScenePolish("Assets/Scenes/TrainingGround.unity", profile, darkMetal, carbon, cyan, magenta, training: true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[VisualPolishInstaller] Installed Competitive Neon Industrial visual polish.");
        }

        private static VisualPolishProfile EnsureProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VisualPolishProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VisualPolishProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            profile.RedArmorColor = Color.red;
            profile.BlueArmorColor = Color.blue;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Material EnsureMaterial(string fileName, Color baseColor, float metallic, float smoothness, Color? emission = null)
        {
            string path = $"{MaterialRoot}/{fileName}";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = baseColor;
            SetColor(material, "_BaseColor", baseColor);
            SetColor(material, "_Color", baseColor);
            SetFloat(material, "_Metallic", metallic);
            SetFloat(material, "_Glossiness", smoothness);
            SetFloat(material, "_Smoothness", smoothness);
            if (emission.HasValue)
            {
                SetColor(material, "_EmissionColor", emission.Value);
                SetColor(material, "_EmissiveColor", emission.Value);
                material.EnableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void PatchArmorPlatePrefab(VisualPolishProfile profile)
        {
            GameObject root = LoadRequiredPrefab(ArmorPlatePrefabPath);
            var visual = root.GetComponent<ArmorPlateVisual>();
            if (visual == null) visual = root.AddComponent<ArmorPlateVisual>();
            visual.Profile = profile;
            visual.PlateRenderer = root.GetComponent<MeshRenderer>();
            visual.RefreshGlow("blue", 1f, profile);
            SavePrefab(root, ArmorPlatePrefabPath);
        }

        private static void PatchProjectilePrefab(VisualPolishProfile profile)
        {
            GameObject root = LoadRequiredPrefab(ProjectilePrefabPath);
            var visual = root.GetComponent<ProjectileTrailVisual>();
            if (visual == null) visual = root.AddComponent<ProjectileTrailVisual>();
            visual.Profile = profile;
            if (root.GetComponent<TrailRenderer>() == null) root.AddComponent<TrailRenderer>();
            SavePrefab(root, ProjectilePrefabPath);
        }

        private static void PatchGimbalPrefab(Material darkMetal, Material cyan, Material magenta)
        {
            GameObject root = LoadRequiredPrefab(GimbalPrefabPath);
            ApplyMaterial(root.transform.Find("YawPivot/Yoke"), darkMetal);
            ApplyMaterial(root.transform.Find("YawPivot/PitchPivot/PitchBody"), darkMetal);
            ApplyMaterial(root.transform.Find("YawPivot/PitchPivot/Barrel"), darkMetal);
            EnsurePrimitive(root.transform, "Visual_TurretBearing", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.78f, 0.08f, 0.78f), cyan);
            Transform pitch = root.transform.Find("YawPivot/PitchPivot");
            if (pitch != null)
            {
                EnsurePrimitive(pitch, "Visual_PitchHousing", PrimitiveType.Cube, new Vector3(0f, 0f, -0.02f), new Vector3(0.42f, 0.28f, 0.34f), darkMetal);
                EnsurePrimitive(pitch, "Visual_MuzzlePowerRing", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.34f), new Vector3(0.18f, 0.035f, 0.18f), magenta);
            }
            SavePrefab(root, GimbalPrefabPath);
        }

        private static void PatchChassisPrefab(Material darkMetal, Material carbon, Material cyan, Material magenta)
        {
            GameObject root = LoadRequiredPrefab(ChassisPrefabPath);
            Transform pedestal = root.transform.Find("Pedestal");
            ApplyMaterial(pedestal, carbon);
            ApplyMaterial(root.transform.Find("WheelFL"), darkMetal);
            ApplyMaterial(root.transform.Find("WheelFR"), darkMetal);
            ApplyMaterial(root.transform.Find("WheelRL"), darkMetal);
            ApplyMaterial(root.transform.Find("WheelRR"), darkMetal);
            if (pedestal != null)
            {
                EnsurePrimitive(pedestal, "Visual_WheelMount_FL", PrimitiveType.Cube, new Vector3(-0.55f, -0.05f, 0.42f), new Vector3(0.36f, 0.14f, 0.18f), darkMetal);
                EnsurePrimitive(pedestal, "Visual_WheelMount_FR", PrimitiveType.Cube, new Vector3(0.55f, -0.05f, 0.42f), new Vector3(0.36f, 0.14f, 0.18f), darkMetal);
                EnsurePrimitive(pedestal, "Visual_WheelMount_RL", PrimitiveType.Cube, new Vector3(-0.55f, -0.05f, -0.42f), new Vector3(0.36f, 0.14f, 0.18f), darkMetal);
                EnsurePrimitive(pedestal, "Visual_WheelMount_RR", PrimitiveType.Cube, new Vector3(0.55f, -0.05f, -0.42f), new Vector3(0.36f, 0.14f, 0.18f), darkMetal);
                EnsurePrimitive(pedestal, "Visual_CyanPowerSpine", PrimitiveType.Cube, new Vector3(0f, 0.18f, 0f), new Vector3(0.08f, 0.035f, 0.95f), cyan);
                EnsurePrimitive(pedestal, "Visual_MagentaDiagnosticStrip", PrimitiveType.Cube, new Vector3(0.25f, 0.17f, 0f), new Vector3(0.04f, 0.03f, 0.75f), magenta);
            }
            Transform nestedGimbal = root.transform.Find("Gimbal");
            if (nestedGimbal != null) EnsurePrimitive(nestedGimbal, "Visual_TurretBearing", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.78f, 0.08f, 0.78f), cyan);
            SavePrefab(root, ChassisPrefabPath);
        }

        private static void ApplyScenePolish(string scenePath, VisualPolishProfile profile, Material darkMetal, Material carbon, Material cyan, Material magenta, bool training)
        {
            if (!File.Exists(scenePath))
            {
                Debug.LogWarning($"[VisualPolishInstaller] Missing scene {scenePath}; skipped.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(scenePath);
            GameObject root = GameObject.Find("VisualPolishRoot");
            if (root == null) root = new GameObject("VisualPolishRoot");
            DimSceneSun(profile);
            EnsureSceneLight(root.transform, "Visual_KeyCyan", new Vector3(-3f, 6f, -4f), profile.CyanAccentColor, 1.25f);
            EnsureSceneLight(root.transform, "Visual_RimMagenta", new Vector3(4f, 4f, 3f), profile.MagentaAccentColor, 0.9f);
            EnsurePrimitive(root.transform, "Visual_CyanFloorSpine", PrimitiveType.Cube, Vector3.zero, training ? new Vector3(0.08f, 0.012f, 8f) : new Vector3(0.08f, 0.012f, 18f), cyan);
            EnsurePrimitive(root.transform, "Visual_MagentaFloorSpine", PrimitiveType.Cube, new Vector3(1.5f, 0.015f, 0f), training ? new Vector3(0.05f, 0.012f, 8f) : new Vector3(0.05f, 0.012f, 18f), magenta);
            EnsureAtmosphere(root.transform, training ? 24 : 48, profile);

            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (renderer.name.Contains("TrainingFloor") || renderer.name.Contains("Ground"))
                {
                    renderer.sharedMaterial = carbon;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void DimSceneSun(VisualPolishProfile profile)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = profile.DarkAmbientColor;
            RenderSettings.ambientIntensity = profile.SceneAmbientIntensity;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    light.intensity = Mathf.Min(light.intensity, profile.MaxDirectionalLightIntensity);
                    light.color = new Color(0.45f, 0.55f, 0.68f, 1f);
                }
            }
        }

        private static GameObject LoadRequiredPrefab(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                throw new FileNotFoundException($"Required prefab missing: {path}");
            }
            return PrefabUtility.LoadPrefabContents(path);
        }

        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static GameObject EnsurePrimitive(Transform parent, string name, PrimitiveType type, Vector3 localPosition, Vector3 localScale, Material material)
        {
            Transform existing = parent.Find(name);
            GameObject obj = existing != null ? existing.gameObject : GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localScale = localScale;
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            ApplyMaterial(obj.transform, material);
            return obj;
        }

        private static void EnsureSceneLight(Transform parent, string name, Vector3 position, Color color, float intensity)
        {
            Transform existing = parent.Find(name);
            GameObject obj = existing != null ? existing.gameObject : new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            Light light = obj.GetComponent<Light>();
            if (light == null) light = obj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = 10f;
            light.shadows = LightShadows.Soft;
        }

        private static void EnsureAtmosphere(Transform parent, int count, VisualPolishProfile profile)
        {
            Transform existing = parent.Find("Visual_DriftingParticles");
            GameObject obj = existing != null ? existing.gameObject : new GameObject("Visual_DriftingParticles");
            obj.transform.SetParent(parent, false);
            ParticleSystem particles = obj.GetComponent<ParticleSystem>();
            if (particles == null) particles = obj.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.startLifetime = 8f;
            main.startSpeed = 0.08f;
            main.startSize = 0.04f;
            main.maxParticles = count;
            main.startColor = new ParticleSystem.MinMaxGradient(profile.CyanAccentColor, profile.MagentaAccentColor);
            var emission = particles.emission;
            emission.rateOverTime = count / 8f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(16f, 3f, 16f);
        }

        private static void ApplyMaterial(Transform target, Material material)
        {
            if (target == null || material == null) return;
            Renderer renderer = target.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        private static void SetColor(Material material, string property, Color color)
        {
            if (material.HasProperty(property)) material.SetColor(property, color);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }
    }
}
#endif
```

- [ ] **Step 4: Run installer**

Run with Unity MCP:

```text
execute_menu_item(menu_path: "TsingYun/Install Visual Polish")
```

Expected: console log contains `[VisualPolishInstaller] Installed Competitive Neon Industrial visual polish.`

- [ ] **Step 5: Run prefab tests and inspect console**

Run:

```text
run_tests(mode: "EditMode", test_names: ["TsingYun.UnityArena.Tests.EditMode.VisualPolishPrefabTests"], include_failed_tests: true)
read_console(action: "get", types: ["error", "warning"], count: "20", format: "detailed")
```

Expected: tests PASS and no compile errors. Warnings about renderer pipeline fallbacks are acceptable if explicit and nonfatal.

- [ ] **Step 6: Commit**

Commit installer, generated assets, and patched prefabs:

```bash
git add shared/unity_arena/Assets/Editor/VisualPolishInstaller.cs shared/unity_arena/Assets/Visual shared/unity_arena/Assets/Materials/VisualPolish shared/unity_arena/Assets/Prefabs/ArmorPlate.prefab shared/unity_arena/Assets/Prefabs/Chassis.prefab shared/unity_arena/Assets/Prefabs/Gimbal.prefab shared/unity_arena/Assets/Prefabs/Projectile.prefab shared/unity_arena/Assets/Tests/EditMode/VisualPolishPrefabTests.cs
git commit -m "feat: install visual polish prefabs"
```

## Task 5: Scene Polish, Screenshot QA, And Documentation

**Files:**
- Modify through installer: `shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity`
- Modify through installer: `shared/unity_arena/Assets/Scenes/TrainingGround.unity`
- Create: `docs/visual-polish-qa.md`
- Update: `docs/cleanup-roadmap.md`

- [ ] **Step 1: Run installer again to prove rerunnable scene updates**

Run:

```text
execute_menu_item(menu_path: "TsingYun/Install Visual Polish")
```

Expected: no duplicate `VisualPolishRoot` objects in either scene and a successful installer log.

- [ ] **Step 2: Capture visual QA screenshots**

Use Unity MCP camera captures:

```text
manage_scene(action: "load", path: "Assets/Scenes/MapA_MazeHybrid.unity")
manage_camera(action: "screenshot", capture_source: "scene_view", view_target: "BlueChassis", include_image: true, max_resolution: 900, screenshot_file_name: "milestone5_mapa_blue.png")
manage_camera(action: "screenshot", capture_source: "scene_view", view_target: "RedChassis", include_image: true, max_resolution: 900, screenshot_file_name: "milestone5_mapa_red.png")
manage_scene(action: "load", path: "Assets/Scenes/TrainingGround.unity")
manage_camera(action: "screenshot", capture_source: "scene_view", view_target: "BlueChassis", include_image: true, max_resolution: 900, screenshot_file_name: "milestone5_training_blue.png")
manage_camera(action: "screenshot", capture_source: "scene_view", view_target: "RedChassis", include_image: true, max_resolution: 900, screenshot_file_name: "milestone5_training_red.png")
```

Expected: screenshots show richer neon/industrial treatment, internal armor glow, and no detached-looking wheel/gimbal/muzzle assemblies from the inspected views.

- [ ] **Step 3: Create QA documentation**

Create `docs/visual-polish-qa.md`:

```markdown
# Visual Polish QA

## Visual Target

Milestone 5 uses the Competitive Neon Industrial direction: dark metal and
carbon surfaces, cyan/magenta glow systems, visible atmosphere, holographic
arena markers, exact red/blue armor identity, and cohesive robot assemblies.

Readability is a guardrail, not the ceiling. Dense glow, particles, decals,
and visual energy are encouraged when team, armor, target, and rule-zone cues
remain recoverable.

## Installer

Run the visual pass from Unity:

```text
TsingYun/Install Visual Polish
```

The installer creates or updates:

- `Assets/Visual/CompetitiveNeonIndustrial.asset`
- `Assets/Materials/VisualPolish/`
- `Assets/Prefabs/ArmorPlate.prefab`
- `Assets/Prefabs/Chassis.prefab`
- `Assets/Prefabs/Gimbal.prefab`
- `Assets/Prefabs/Projectile.prefab`
- `Assets/Scenes/MapA_MazeHybrid.unity`
- `Assets/Scenes/TrainingGround.unity`

## Required Checks

Run Unity tests:

```text
EditMode: TsingYun.UnityArena.Tests.EditMode.ArenaReadabilityMetricsTests
EditMode: TsingYun.UnityArena.Tests.EditMode.VisualPolishRuntimeTests
EditMode: TsingYun.UnityArena.Tests.EditMode.VisualPolishPrefabTests
PlayMode: TsingYun.UnityArena.Tests.PlayMode.RuleZonePresentationTests
```

Run existing gameplay smoke tests that cover armor, projectile, rule-zone, and
training behavior.

## Screenshot Checklist

Capture both scenes from blue and red chassis viewpoints.

Pass criteria:

- scene is nonblank and visibly richer than the previous flat prototype,
- red armor uses exact red base plus red glow,
- blue armor uses exact blue base plus blue glow,
- MNIST sticker remains visible enough to recognize the printed digit,
- wheels appear mounted to chassis,
- gimbal appears seated into a turret bearing,
- barrel and muzzle flash appear attached to the gimbal,
- healing and boost markers remain semantically distinct,
- glow and particles add mood without erasing target silhouettes.

## Known Limits

The project currently reports the active renderer as Built-in even though HDRP
assets are present. The visual pass uses Built-in-safe material properties first
and sets HDRP-style properties only when they exist.
```

- [ ] **Step 4: Mark roadmap complete**

In `docs/cleanup-roadmap.md`, change Milestone 5 checkboxes to complete after screenshots and tests pass:

```markdown
## Milestone 5: Visual Effects And Readability Polish

- [x] Upgrade scene visuals toward the sci-fi industrial style in `schema.md`.
- [x] Preserve armor, team color, target, rule-zone, and frame-stream readability while adding lighting, material, and VFX polish.
- [x] Add screenshot or PlayMode visual QA checks before treating the visual pass as complete.
```

- [ ] **Step 5: Run final verification**

Run:

```text
run_tests(mode: "EditMode", include_failed_tests: true)
run_tests(mode: "PlayMode", include_failed_tests: true)
read_console(action: "get", types: ["error", "warning"], count: "30", format: "detailed")
```

Also run no-Unity docs check if available:

```bash
uv run pytest tests/test_assignment_design.py tests/test_assignment_mini_commands.py tests/test_arena_wire_format.py tests/test_rl_training_client.py -q
```

Expected: Unity tests pass, Python docs/wire tests pass, and console has no compile errors.

- [ ] **Step 6: Commit final scene polish**

Commit:

```bash
git add shared/unity_arena/Assets/Scenes/MapA_MazeHybrid.unity shared/unity_arena/Assets/Scenes/TrainingGround.unity docs/visual-polish-qa.md docs/cleanup-roadmap.md
git commit -m "feat: polish milestone 5 scenes"
```

## Self-Review Checklist

- Spec coverage: Tasks 1-5 cover visual profile, readability metrics, armor glow, projectile visuals, rule markers, holograms, robot cohesion, scene polish, screenshot QA, and docs.
- Asset scope: The plan does not ban external assets. It completes a procedural/editor-driven pass first, then leaves room to add safe external assets during implementation if they improve the result.
- Gameplay safety: All visual geometry is named `Visual_*`, removes colliders, and is attached under visual parents or existing non-physics transforms.
- Renderer fallback: Material helpers set Built-in-safe properties first and HDRP-style properties only when present.
