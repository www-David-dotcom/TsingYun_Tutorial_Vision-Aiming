#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace TsingYun.UnityArena.EditorUtilities
{
    public static class TrainingGroundSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/TrainingGround.unity";
        private const string ChassisPrefabPath = "Assets/Prefabs/Chassis.prefab";
        private const string ProjectilePrefabPath = "Assets/Prefabs/Projectile.prefab";
        private const float ChassisSpawnHeightMeters = 0.05f;

        [MenuItem("TsingYun/Build Training Ground Scene")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject arenaObject = new GameObject("ArenaMain");
            var arena = arenaObject.AddComponent<ArenaMain>();
            var trainingTarget = arenaObject.AddComponent<TrainingTargetController>();
            var baseline = arenaObject.AddComponent<BaselineOpponentController>();
            arenaObject.AddComponent<RuleZonePresentation>();

            Chassis blue = CreateChassis("BlueChassis", "blue", new Vector3(-3f, ChassisSpawnHeightMeters, 0f));
            Chassis red = CreateChassis("RedChassis", "red", new Vector3(3f, ChassisSpawnHeightMeters, 0f));
            arena.BlueChassis = blue;
            arena.RedChassis = red;
            arena.SpawnPointBlue = CreateMarker("SpawnPoint_Blue", new Vector3(-3f, ChassisSpawnHeightMeters, 0f), 45f).transform;
            arena.SpawnPointRed = CreateMarker("SpawnPoint_Red", new Vector3(3f, ChassisSpawnHeightMeters, 0f), -135f).transform;
            arena.ProjectileRoot = new GameObject("ProjectileRoot").transform;
            arena.ProjectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);

            var cameraObject = new GameObject("GimbalCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = blue.transform.position + new Vector3(0f, 1.2f, 0f);
            camera.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            arena.GimbalCamera = camera;

            trainingTarget.Configure(red, 1f, 1f, 2f);
            baseline.Configure(red, blue, fireWhenAligned: true);

            CreateLight();
            CreateFloor();
            CreatePanel(trainingTarget);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"[TrainingGroundSceneBuilder] Saved {ScenePath}");
        }

        private static Chassis CreateChassis(string name, string team, Vector3 position)
        {
            GameObject instance = InstantiatePrefab(ChassisPrefabPath, name, position);
            var chassis = instance.GetComponent<Chassis>();
            if (chassis == null)
            {
                chassis = instance.AddComponent<Chassis>();
            }

            chassis.Team = team;
            chassis.MaxHp = GameConstants.VehicleHpOneVsOne;
            return chassis;
        }

        private static GameObject InstantiatePrefab(string path, string name, Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject instance = prefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab)
                : GameObject.CreatePrimitive(PrimitiveType.Capsule);
            instance.name = name;
            instance.transform.position = position;
            return instance;
        }

        private static GameObject CreateMarker(string name, Vector3 position, float yawDegrees)
        {
            var marker = new GameObject(name);
            marker.transform.position = position;
            marker.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            return marker;
        }

        private static void CreateLight()
        {
            GameObject light = new GameObject("KeyLight");
            var directional = light.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.intensity = 2.5f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "TrainingFloor";
            floor.transform.localScale = new Vector3(1.2f, 1f, 0.8f);
        }

        private static void CreatePanel(TrainingTargetController targetController)
        {
            GameObject canvasObject = new GameObject("TrainingGroundCanvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();

            var panel = canvasObject.AddComponent<TrainingGroundPanel>();
            panel.TargetController = targetController;
            panel.TranslationSpeedLabel = CreateLabel(canvasObject.transform, "TranslationSpeedLabel", new Vector2(180f, -18f));
            panel.TranslationSpeedSlider = CreateSlider(
                canvasObject.transform,
                "TranslationSpeedSlider",
                new Vector2(180f, -48f),
                maxValue: 3f);
            panel.RotationSpeedLabel = CreateLabel(canvasObject.transform, "RotationSpeedLabel", new Vector2(180f, -84f));
            panel.RotationSpeedSlider = CreateSlider(
                canvasObject.transform,
                "RotationSpeedSlider",
                new Vector2(180f, -114f),
                maxValue: 8f);
        }

        private static Slider CreateSlider(
            Transform parent,
            string name,
            Vector2 anchoredPosition,
            float maxValue)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(240f, 24f);

            RectTransform background = CreateSliderPart(obj.transform, "Background", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
            background.sizeDelta = new Vector2(0f, 8f);
            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.color = new Color(0.08f, 0.12f, 0.16f, 0.86f);

            RectTransform fillArea = CreateSliderPart(obj.transform, "Fill Area", Vector2.zero, Vector2.one);
            fillArea.offsetMin = new Vector2(8f, 0f);
            fillArea.offsetMax = new Vector2(-8f, 0f);
            RectTransform fill = CreateSliderPart(fillArea, "Fill", Vector2.zero, new Vector2(0.45f, 1f));
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(0.0f, 0.75f, 1.0f, 0.95f);

            RectTransform handleArea = CreateSliderPart(obj.transform, "Handle Slide Area", Vector2.zero, Vector2.one);
            handleArea.offsetMin = new Vector2(8f, 0f);
            handleArea.offsetMax = new Vector2(-8f, 0f);
            RectTransform handle = CreateSliderPart(handleArea, "Handle", new Vector2(0.45f, 0.5f), new Vector2(0.45f, 0.5f));
            handle.sizeDelta = new Vector2(18f, 18f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(1f, 1f, 1f, 0.98f);

            Slider slider = obj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = maxValue;
            slider.value = 1f;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            return slider;
        }

        private static RectTransform CreateSliderPart(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            var rect = part.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static TMP_Text CreateLabel(Transform parent, string name, Vector2 anchoredPosition)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(280f, 24f);
            var text = obj.AddComponent<TextMeshProUGUI>();
            text.fontSize = 16f;
            text.color = Color.white;
            return text;
        }
    }
}
#endif
