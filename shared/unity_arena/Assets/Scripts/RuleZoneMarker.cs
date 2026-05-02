using UnityEngine;

namespace TsingYun.UnityArena
{
    public enum RuleZoneKind
    {
        HealingZone,
        BoostPoint,
    }

    public class RuleZoneMarker : MonoBehaviour
    {
        public RuleZoneKind Kind;
        public string Team = "";
        public float Radius;
        public BoostPointHolder Holder;

        public void ConfigureHealing(string team, float radius)
        {
            Kind = RuleZoneKind.HealingZone;
            Team = team;
            Radius = radius;
            Holder = BoostPointHolder.Unheld;
        }

        public void ConfigureBoost(float radius, BoostPointHolder holder)
        {
            Kind = RuleZoneKind.BoostPoint;
            Team = "";
            Radius = radius;
            Holder = holder;
        }
    }
}
