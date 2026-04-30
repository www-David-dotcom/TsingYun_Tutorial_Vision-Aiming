using System;
using UnityEngine;

namespace TsingYun.UnityArena
{
    // Pure-C# state container for an armor plate. The MonoBehaviour wrapper
    // (ArmorPlate) adds the OnTriggerEnter glue and emission update.
    // Mirrors armor_plate.gd.
    public class ArmorPlateState
    {
        public string Team = "blue";
        public string Face = "front";
        public string Icon = "Standard";
        public int MaxHp = 200;
        public int Hp;

        public string PlateId => $"{Team}.{Face}";

        public void Reset()
        {
            Hp = MaxHp;
        }

        public void ApplyDamage(int amount)
        {
            Hp = Mathf.Max(0, Hp - amount);
        }
    }

    // MonoBehaviour wrapper. Listens for projectile triggers, calls
    // projectile.OnArmorHit (returns damage), applies damage. The PlateHit
    // event bubbles up to the parent Chassis.
    public class ArmorPlate : MonoBehaviour
    {
        public string Team = "blue";
        public string Face = "front";
        public string Icon = "Standard";
        public int MaxHp = 200;

        public event Action<int, int> PlateHit;  // damage, sourceInstanceId

        private ArmorPlateState _state;

        public string PlateId => _state.PlateId;
        public int Hp => _state.Hp;

        private void Awake()
        {
            _state = new ArmorPlateState
            {
                Team = Team,
                Face = Face,
                Icon = Icon,
                MaxHp = MaxHp,
            };
            _state.Reset();
        }

        public void ResetForNewEpisode()
        {
            _state.Reset();
        }

        public void ApplyDamage(int amount, int sourceInstanceId)
        {
            _state.ApplyDamage(amount);
            PlateHit?.Invoke(amount, sourceInstanceId);
        }

        private void OnTriggerEnter(Collider other)
        {
            var projectile = other.GetComponent<Projectile>();
            if (projectile == null) return;
            int damage = projectile.OnArmorHit(this);
            if (damage > 0)
            {
                ApplyDamage(damage, other.GetInstanceID());
            }
        }
    }
}
