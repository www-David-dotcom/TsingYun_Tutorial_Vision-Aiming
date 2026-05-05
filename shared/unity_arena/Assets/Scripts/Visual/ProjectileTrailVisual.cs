using UnityEngine;

namespace TsingYun.UnityArena
{
    // Lightweight projectile trail and impact visual feedback. Attached to
    // the Projectile prefab; does not own gameplay logic — Projectile.cs
    // calls in at arm/impact for purely cosmetic effects.
    public class ProjectileTrailVisual : MonoBehaviour
    {
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private Light pointLight;

        private float _lightDecay;
        private Color _teamColor;

        public void OnArmed(Vector3 velocity, string team)
        {
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }
            _teamColor = team == "red" ? Color.red : Color.cyan;
            if (pointLight != null)
            {
                pointLight.color = _teamColor;
                pointLight.intensity = 1.5f;
                pointLight.enabled = true;
            }
            _lightDecay = 0f;
        }

        public void PlayImpact(string reason, string team)
        {
            if (trail != null) trail.emitting = false;
            if (pointLight != null) pointLight.enabled = false;
        }

        private void Update()
        {
            if (pointLight != null && pointLight.enabled)
            {
                _lightDecay += Time.deltaTime * 0.3f;
                pointLight.intensity = Mathf.Max(0f, 1.5f - _lightDecay);
            }
        }
    }
}
