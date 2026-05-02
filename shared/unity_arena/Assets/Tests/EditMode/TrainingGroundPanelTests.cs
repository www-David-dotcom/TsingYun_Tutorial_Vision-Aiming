using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class TrainingGroundPanelTests
    {
        [Test]
        public void RefreshNow_FormatsSliderLabels()
        {
            var root = new GameObject("TrainingGroundPanelTest");
            try
            {
                var panel = root.AddComponent<TrainingGroundPanel>();
                panel.TranslationSpeedSlider = CreateChild<Slider>(root.transform, "TranslationSpeedSlider");
                panel.RotationSpeedSlider = CreateChild<Slider>(root.transform, "RotationSpeedSlider");
                panel.TranslationSpeedLabel = CreateChild<TextMeshProUGUI>(root.transform, "TranslationSpeedLabel");
                panel.RotationSpeedLabel = CreateChild<TextMeshProUGUI>(root.transform, "RotationSpeedLabel");

                panel.TranslationSpeedSlider.maxValue = 3f;
                panel.TranslationSpeedSlider.value = 1.25f;
                panel.RotationSpeedSlider.maxValue = 8f;
                panel.RotationSpeedSlider.value = 2.5f;
                panel.RefreshNow();

                Assert.AreEqual("Target speed 1.25 m/s", panel.TranslationSpeedLabel.text);
                Assert.AreEqual("Target spin 2.50 rad/s", panel.RotationSpeedLabel.text);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TrainingGroundScene_ChassisRenderersClearFloor()
        {
            Scene previousScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/TrainingGround.unity", OpenSceneMode.Additive);
            try
            {
                GameObject floor = FindInScene(scene, "TrainingFloor");
                Assert.IsNotNull(floor, "TrainingGround scene must contain TrainingFloor.");
                Renderer floorRenderer = floor.GetComponent<Renderer>();
                Assert.IsNotNull(floorRenderer, "TrainingFloor must have a renderer.");
                float floorTopY = floorRenderer.bounds.max.y;

                AssertChassisClearsFloor(scene, "BlueChassis", floorTopY);
                AssertChassisClearsFloor(scene, "RedChassis", floorTopY);
                AssertSpawnClearsFloor(scene, "SpawnPoint_Blue", floorTopY);
                AssertSpawnClearsFloor(scene, "SpawnPoint_Red", floorTopY);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previousScene.IsValid())
                {
                    SceneManager.SetActiveScene(previousScene);
                }
            }
        }

        private static T CreateChild<T>(Transform parent, string name) where T : Component
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.AddComponent<T>();
        }

        private static void AssertChassisClearsFloor(Scene scene, string name, float floorTopY)
        {
            GameObject chassis = FindInScene(scene, name);
            Assert.IsNotNull(chassis, $"TrainingGround scene must contain {name}.");

            Renderer[] renderers = chassis.GetComponentsInChildren<Renderer>();
            Assert.IsNotEmpty(renderers, $"{name} must have visible chassis renderers.");

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Assert.GreaterOrEqual(
                bounds.min.y,
                floorTopY - 0.001f,
                $"{name} render bounds must not sink below the training floor.");
        }

        private static void AssertSpawnClearsFloor(Scene scene, string name, float floorTopY)
        {
            GameObject spawn = FindInScene(scene, name);
            Assert.IsNotNull(spawn, $"TrainingGround scene must contain {name}.");
            Assert.GreaterOrEqual(
                spawn.transform.position.y,
                floorTopY,
                $"{name} must keep episode resets above the training floor.");
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == name)
                    {
                        return transform.gameObject;
                    }
                }
            }

            return null;
        }
    }
}
