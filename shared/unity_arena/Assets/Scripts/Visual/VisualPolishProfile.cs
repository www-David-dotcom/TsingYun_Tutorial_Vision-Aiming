using UnityEngine;

namespace TsingYun.UnityArena
{
    // Runtime colour profile for visual polish elements. Can be overridden
    // via ScriptableObject asset in the Inspector; falls back to sensible
    // cyberpunk defaults when no asset is assigned.
    [CreateAssetMenu(fileName = "VisualPolishProfile", menuName = "TsingYun/Visual Polish Profile")]
    public class VisualPolishProfile : ScriptableObject
    {
        [Header("Hologram")]
        public Color HologramTextColor = new Color(0f, 0.9f, 1f);
        public Color CyanAccentColor = new Color(0f, 0.9f, 1f);

        [Header("Healing Zones")]
        public Color BlueHealingColor = new Color(0f, 0.25f, 1f, 0.22f);
        public Color RedHealingColor = new Color(1f, 0f, 0f, 0.22f);

        [Header("Boost Points")]
        public Color BoostColor = new Color(0f, 1f, 0.8f, 0.28f);

        public static VisualPolishProfile CreateRuntimeDefault()
        {
            var p = CreateInstance<VisualPolishProfile>();
            p.HologramTextColor = new Color(0f, 0.9f, 1f);
            p.CyanAccentColor = new Color(0f, 0.9f, 1f);
            p.BlueHealingColor = new Color(0f, 0.25f, 1f, 0.22f);
            p.RedHealingColor = new Color(1f, 0f, 0f, 0.22f);
            p.BoostColor = new Color(0f, 1f, 0.8f, 0.28f);
            return p;
        }
    }
}
