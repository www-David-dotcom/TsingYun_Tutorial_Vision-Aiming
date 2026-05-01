using System;
using System.Collections.Generic;
using UnityEngine;

namespace TsingYun.UnityArena
{
    // Pure-C# state container for the chassis HP pool. One pool per
    // robot: damage from any of the four armor plates accumulates here,
    // mirroring real RM ("hits on different plates of the same robot all
    // deduct from the same robot HP"). Extracted so EditMode tests can
    // exercise the clamp-at-zero / reset behavior without spinning up a
    // scene.
    public class ChassisHpState
    {
        public int MaxHp = 200;
        public int Hp;

        public void Reset() { Hp = MaxHp; }
        public void ApplyDamage(int amount) { Hp = Mathf.Max(0, Hp - amount); }
        public bool IsDestroyed => Hp <= 0;
    }

    // Mecanum chassis with a custom velocity solver (NOT Rigidbody-driven).
    // Movement is via CharacterController.Move so PhysX integration drift
    // cannot creep into chassis kinematics over a 90-second episode (R3
    // mitigation per the design spec). Ports chassis.gd line-by-line.
    [RequireComponent(typeof(CharacterController))]
    public class Chassis : MonoBehaviour
    {
        public string Team = "blue";
        public int ChassisId = 0;
        // RoboMaster numeric tag (1=Hero, 2=Engineer, 3/4/5=Standard, 7=Sentry).
        // One per robot — every plate of this chassis displays the same
        // number sticker (an MNIST sample of `Number`).
        public int Number = 3;
        // Per-robot HP pool. All four plates feed damage into this; the
        // plates themselves carry no HP.
        public int MaxHp = 200;
        [Range(0f, 4f)] public float MaxLinearSpeed = 3.5f;
        [Range(0f, 8f)] public float MaxAngularSpeed = 4.0f;

        public int DamageTaken { get; private set; }
        public int Hp => _hp.Hp;
        public bool IsDestroyed => _hp.IsDestroyed;
        public Gimbal Gimbal { get; private set; }
        public Vector3 LinearVelocity { get; private set; }
        public float ChassisYaw => _solver?.ChassisYaw ?? 0f;

        public event Action<string, int, int> ArmorHit;  // plateId, damage, sourceId

        private MecanumChassisController _solver;
        private CharacterController _controller;
        private ArmorPlate[] _plates;
        private StickerLoader _stickerLoader;
        private ChassisHpState _hp;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _solver = new MecanumChassisController
            {
                MaxLinearSpeed = MaxLinearSpeed,
                MaxAngularSpeed = MaxAngularSpeed,
            };
            Gimbal = GetComponentInChildren<Gimbal>();
            _stickerLoader = GetComponent<StickerLoader>();
            _hp = new ChassisHpState { MaxHp = MaxHp };
            _hp.Reset();
            AssignArmorMetadata();
        }

        private void AssignArmorMetadata()
        {
            var faces = new (string ChildName, string Face)[]
            {
                ("ArmorPlateFront", "front"),
                ("ArmorPlateBack",  "back"),
                ("ArmorPlateLeft",  "left"),
                ("ArmorPlateRight", "right"),
            };
            var found = new List<ArmorPlate>();
            foreach (var f in faces)
            {
                var child = transform.Find(f.ChildName);
                if (child == null)
                {
                    Debug.LogWarning($"Chassis {Team}: missing armor child {f.ChildName}");
                    continue;
                }
                var plate = child.GetComponent<ArmorPlate>();
                if (plate == null) continue;
                plate.Team = Team;
                plate.Face = f.Face;
                plate.Number = Number;
                plate.PlateHit += (dmg, src) => RaiseArmorHit(plate.PlateId, dmg, src);
                found.Add(plate);
            }
            _plates = found.ToArray();
        }

        public void SetChassisCmd(float vxBody, float vyBody, float omega)
        {
            _solver.SetCmd(vxBody, vyBody, omega);
        }

        public void ResetForNewEpisode(Vector3 spawnPosition, float spawnYaw)
        {
            _controller.enabled = false;
            transform.position = spawnPosition;
            transform.rotation = Quaternion.Euler(0f, spawnYaw * Mathf.Rad2Deg, 0f);
            _controller.enabled = true;
            _solver.Reset(spawnYaw);
            DamageTaken = 0;
            _hp.MaxHp = MaxHp;
            _hp.Reset();
            LinearVelocity = Vector3.zero;
            // Episode N+1 must start from a known gimbal pose, otherwise the
            // intra-Unity determinism test (same seed -> same frame hashes)
            // sees state bleed-through from the prior episode.
            if (Gimbal != null) Gimbal.Reset();
            if (_plates != null)
            {
                foreach (var p in _plates)
                {
                    p.ResetForNewEpisode();
                    p.RefreshGlow(1f);
                }
            }
            if (_stickerLoader != null) _stickerLoader.LoadStickerForCurrentNumber();
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            _solver.IntegrateStep(dt);
            transform.rotation = Quaternion.Euler(0f, _solver.ChassisYaw * Mathf.Rad2Deg, 0f);
            LinearVelocity = _solver.WorldVelocity;
            // Y-component stays zero (flat floor); gravity not applied to chassis here.
            _controller.Move(LinearVelocity * dt);
        }

        public Dictionary<string, object> OdomState() => new Dictionary<string, object>
        {
            { "position_world", Vec3Dict(transform.position) },
            { "linear_velocity", Vec3Dict(LinearVelocity) },
            { "yaw_world", (double)_solver.ChassisYaw },
        };

        public Dictionary<string, object> GimbalState()
        {
            if (Gimbal == null)
                return new Dictionary<string, object> { { "yaw", 0.0 }, { "pitch", 0.0 }, { "yaw_rate", 0.0 }, { "pitch_rate", 0.0 } };
            var s = Gimbal.GetState();
            return new Dictionary<string, object>
            {
                { "yaw", (double)s.Yaw }, { "pitch", (double)s.Pitch },
                { "yaw_rate", (double)s.YawRate }, { "pitch_rate", (double)s.PitchRate },
            };
        }

        protected void RaiseArmorHit(string plateId, int damage, int sourceId)
        {
            DamageTaken += damage;
            _hp.ApplyDamage(damage);
            float t = _hp.MaxHp > 0 ? (float)_hp.Hp / _hp.MaxHp : 0f;
            if (_plates != null)
            {
                foreach (var p in _plates) p.RefreshGlow(t);
            }
            ArmorHit?.Invoke($"{Team}.{plateId.Split('.')[1]}", damage, sourceId);
        }

        private static Dictionary<string, object> Vec3Dict(Vector3 v) => new Dictionary<string, object>
        {
            { "x", (double)v.x }, { "y", (double)v.y }, { "z", (double)v.z },
        };
    }
}
