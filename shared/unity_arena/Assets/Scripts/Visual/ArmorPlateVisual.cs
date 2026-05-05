using UnityEngine;

namespace TsingYun.UnityArena
{
    // Drives the damage-glow and hit-pulse effects on a single armor plate.
    // The chassis calls RefreshGlow(t) every fixed-update tick; the plate
    // ramps its emission from the team's base colour up to full flare as
    // t (HP ratio) drops toward zero. OnArmorHit triggers a brief pulse.
    public class ArmorPlateVisual : MonoBehaviour
    {
        [SerializeField] private MeshRenderer plateRenderer;
        [SerializeField] private MeshRenderer edgeRenderer;

        private float _pulseRemaining;

        public void RefreshGlow(string team, float t)
        {
            Color teamColor = team == "red" ? Color.red : Color.blue;
            float energy = Mathf.Lerp(1.5f, 8.5f, 1f - t);

            SetRendererGlow(plateRenderer, teamColor, energy);
            SetRendererGlow(edgeRenderer, teamColor, energy * 1.3f);
        }

        public void PulseHit(string team)
        {
            _pulseRemaining = 0.15f;
            Color teamColor = team == "red" ? Color.red : Color.blue;
            SetRendererGlow(plateRenderer, Color.white, 12f);
            SetRendererGlow(edgeRenderer, Color.white, 15f);
        }

        private void Update()
        {
            if (_pulseRemaining > 0f)
            {
                _pulseRemaining -= Time.deltaTime;
                if (_pulseRemaining <= 0f)
                {
                    _pulseRemaining = 0f;
                    // Restore to base — caller will refresh on next tick.
                }
            }
        }

        private static void SetRendererGlow(MeshRenderer r, Color color, float energy)
        {
            if (r == null || r.material == null) return;
            var mat = r.material;
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * energy);
            if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", color * energy);
            mat.EnableKeyword("_EMISSION");
        }
    }
}
