using System;
using UnityEngine;

namespace TsingYun.UnityArena
{
    // Pure-C# identity container for an armor plate. HP no longer lives
    // here — there is one HP pool per robot (see ChassisHpState). A plate
    // is just a trigger surface that bubbles damage up to the parent
    // Chassis.
    public class ArmorPlateState
    {
        public string Team = "blue";
        public string Face = "front";
        public int Number = 3;

        public string PlateId => $"{Team}.{Face}";
    }

    // MonoBehaviour wrapper. Listens for projectile triggers, calls
    // projectile.OnArmorHit (returns damage), and emits PlateHit so the
    // parent Chassis can deduct from its HP pool. The plate itself stores
    // no HP — every plate of one robot shares the chassis HP.
    public class ArmorPlate : MonoBehaviour
    {
        public string Team = "blue";
        public string Face = "front";
        public int Number = 3;

        // Inspector-assigned: a child quad whose material albedo is the
        // MNIST sticker. StickerLoader writes the texture here at episode
        // reset.
        [SerializeField] private MeshRenderer stickerRenderer;
        // Inspector-assigned: the plate body's renderer, for the
        // damage-glow effect driven by chassis HP ratio.
        [SerializeField] private MeshRenderer plateRenderer;

        public event Action<int, int> PlateHit;  // damage, sourceInstanceId

        private ArmorPlateState _state;

        public string PlateId => _state.PlateId;

        private void Awake()
        {
            _state = new ArmorPlateState
            {
                Team = Team,
                Face = Face,
                Number = Number,
            };
        }

        public void ApplySticker(Texture2D tex)
        {
            if (stickerRenderer != null && stickerRenderer.material != null)
            {
                stickerRenderer.material.mainTexture = tex;
            }
        }

        // Called by Chassis after every hit (and at episode reset with
        // t=1). t = chassis.Hp / chassis.MaxHp ∈ [0,1]. All four plates
        // glow identically because they share the chassis HP.
        public void RefreshGlow(float t)
        {
            if (plateRenderer == null || plateRenderer.material == null) return;
            var glowColor = Color.Lerp(
                new Color(0.2f, 0.4f, 1.0f),
                new Color(1.0f, 0.2f, 0.1f),
                1f - t);
            var mat = plateRenderer.material;
            mat.SetColor("_EmissionColor", glowColor * Mathf.Lerp(1.5f, 4.5f, 1f - t));
            mat.EnableKeyword("_EMISSION");
        }

        public void ResetForNewEpisode()
        {
            // No per-plate state to reset — chassis owns HP and calls
            // RefreshGlow(1f) on every plate after restoring Hp = MaxHp.
        }

        private void OnTriggerEnter(Collider other)
        {
            var projectile = other.GetComponent<Projectile>();
            if (projectile == null) return;
            int damage = projectile.OnArmorHit(this);
            if (damage > 0)
            {
                PlateHit?.Invoke(damage, other.GetInstanceID());
            }
        }
    }
}
