using UnityEngine;

namespace TsingYun.UnityArena
{
    // Diegetic intersection marker. Hosts a WorldSpace Canvas displaying the
    // JCT-XX label and grid coordinates. The animated emission cone material
    // is assigned in Stage 12c (HoloProjector.shadergraph).
    public class HoloProjector : MonoBehaviour
    {
        public string JunctionId = "JCT-00";
        public Vector2 GridCoords;

        public TMPro.TextMeshProUGUI LabelText;
        public VisualPolishProfile Profile;
        public Renderer[] EmissiveRenderers;

        private void Start()
        {
            RefreshVisuals();
        }

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
    }
}
