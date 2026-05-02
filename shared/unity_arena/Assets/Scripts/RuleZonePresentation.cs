using UnityEngine;

namespace TsingYun.UnityArena
{
    [RequireComponent(typeof(RuleZoneMarkerRenderer))]
    public class RuleZonePresentation : MonoBehaviour
    {
        private RuleZoneMarkerRenderer _renderer;

        public void Render(
            Vector3 blueHealingPosition,
            Vector3 redHealingPosition,
            float healingRadius,
            MatchWorldState worldState,
            float boostRadius)
        {
            Renderer.RenderHealingZones(blueHealingPosition, redHealingPosition, healingRadius);
            Renderer.RenderBoostPoints(worldState.BoostPoints, worldState.BoostPointHolders, boostRadius);
        }

        private RuleZoneMarkerRenderer Renderer
        {
            get
            {
                if (_renderer == null) _renderer = GetComponent<RuleZoneMarkerRenderer>();
                return _renderer;
            }
        }
    }
}
