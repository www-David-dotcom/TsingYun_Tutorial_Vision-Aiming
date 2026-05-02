using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TsingYun.UnityArena
{
    public class TrainingGroundPanel : MonoBehaviour
    {
        public TrainingTargetController TargetController;
        public Slider TranslationSpeedSlider;
        public Slider RotationSpeedSlider;
        public TMP_Text TranslationSpeedLabel;
        public TMP_Text RotationSpeedLabel;

        private void Start()
        {
            if (TranslationSpeedSlider != null)
            {
                TranslationSpeedSlider.minValue = 0f;
                TranslationSpeedSlider.maxValue = 3f;
                TranslationSpeedSlider.value = TargetController != null
                    ? TargetController.TargetTranslationSpeedMps
                    : TranslationSpeedSlider.value;
                TranslationSpeedSlider.onValueChanged.AddListener(OnTranslationSpeedChanged);
            }

            if (RotationSpeedSlider != null)
            {
                RotationSpeedSlider.minValue = 0f;
                RotationSpeedSlider.maxValue = 8f;
                RotationSpeedSlider.value = TargetController != null
                    ? TargetController.TargetRotationSpeedRadPerSecond
                    : RotationSpeedSlider.value;
                RotationSpeedSlider.onValueChanged.AddListener(OnRotationSpeedChanged);
            }

            RefreshNow();
        }

        private void OnDestroy()
        {
            if (TranslationSpeedSlider != null)
            {
                TranslationSpeedSlider.onValueChanged.RemoveListener(OnTranslationSpeedChanged);
            }

            if (RotationSpeedSlider != null)
            {
                RotationSpeedSlider.onValueChanged.RemoveListener(OnRotationSpeedChanged);
            }
        }

        public void RefreshNow()
        {
            if (TranslationSpeedLabel != null && TranslationSpeedSlider != null)
            {
                TranslationSpeedLabel.text = $"Target speed {TranslationSpeedSlider.value:0.00} m/s";
            }

            if (RotationSpeedLabel != null && RotationSpeedSlider != null)
            {
                RotationSpeedLabel.text = $"Target spin {RotationSpeedSlider.value:0.00} rad/s";
            }
        }

        private void OnTranslationSpeedChanged(float value)
        {
            if (TargetController != null)
            {
                TargetController.TargetTranslationSpeedMps = value;
                TargetController.ResetMotion();
            }

            RefreshNow();
        }

        private void OnRotationSpeedChanged(float value)
        {
            if (TargetController != null)
            {
                TargetController.TargetRotationSpeedRadPerSecond = value;
                TargetController.ResetMotion();
            }

            RefreshNow();
        }
    }
}
