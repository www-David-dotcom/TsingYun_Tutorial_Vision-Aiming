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

        private void Start()
        {
            if (LabelText != null)
                LabelText.text = $"{JunctionId}\n({GridCoords.x:+0.0;-0.0}, {GridCoords.y:+0.0;-0.0})";
        }
    }
}
