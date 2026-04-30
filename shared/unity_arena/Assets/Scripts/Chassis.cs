using System;
using System.Collections.Generic;
using UnityEngine;

namespace TsingYun.UnityArena
{
    // Mecanum chassis with a custom velocity solver (NOT Rigidbody-driven).
    // Movement is via CharacterController.Move so PhysX integration drift
    // cannot creep into chassis kinematics over a 90-second episode (R3
    // mitigation per the design spec). Ports chassis.gd line-by-line.
    [RequireComponent(typeof(CharacterController))]
    public class Chassis : MonoBehaviour
    {
        public string Team = "blue";
        public int ChassisId = 0;
        [Range(0f, 4f)] public float MaxLinearSpeed = 3.5f;
        [Range(0f, 8f)] public float MaxAngularSpeed = 4.0f;

        public int DamageTaken { get; private set; }
        public Gimbal Gimbal { get; private set; }
        public Vector3 LinearVelocity { get; private set; }
        public float ChassisYaw => _solver?.ChassisYaw ?? 0f;

        public event Action<string, int, int> ArmorHit;  // plateId, damage, sourceId

        private MecanumChassisController _solver;
        private CharacterController _controller;
        private ArmorPlate[] _plates;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _solver = new MecanumChassisController
            {
                MaxLinearSpeed = MaxLinearSpeed,
                MaxAngularSpeed = MaxAngularSpeed,
            };
            Gimbal = GetComponentInChildren<Gimbal>();
            AssignArmorMetadata();
        }

        private void AssignArmorMetadata()
        {
            var faces = new (string ChildName, string Face, string Icon)[]
            {
                ("ArmorPlateFront", "front", "Hero"),
                ("ArmorPlateBack",  "back",  "Engineer"),
                ("ArmorPlateLeft",  "left",  "Standard"),
                ("ArmorPlateRight", "right", "Sentry"),
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
                plate.Icon = f.Icon;
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
            LinearVelocity = Vector3.zero;
            if (_plates != null) foreach (var p in _plates) p.ResetForNewEpisode();
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
            => ArmorHit?.Invoke($"{Team}.{plateId.Split('.')[1]}", damage, sourceId);

        private static Dictionary<string, object> Vec3Dict(Vector3 v) => new Dictionary<string, object>
        {
            { "x", (double)v.x }, { "y", (double)v.y }, { "z", (double)v.z },
        };
    }
}
