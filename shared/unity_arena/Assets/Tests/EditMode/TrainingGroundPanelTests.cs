using NUnit.Framework;
using TMPro;
using UnityEngine;
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

        private static T CreateChild<T>(Transform parent, string name) where T : Component
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.AddComponent<T>();
        }
    }
}
