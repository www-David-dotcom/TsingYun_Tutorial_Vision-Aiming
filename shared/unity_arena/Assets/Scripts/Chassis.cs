using System;
using System.Collections.Generic;
using UnityEngine;

namespace TsingYun.UnityArena
{
    // Stage 12a stub. Full implementation (MecanumChassisController-driven
    // CharacterController.Move, armor plate wiring, gimbal child) lands in
    // Stage 12b Task 19. The 12a stub provides just the API ArenaMain calls so
    // the orchestrator + wire-format tests can run end-to-end.
    public class Chassis : MonoBehaviour
    {
        public string Team = "blue";
        public int ChassisId = 0;
        public int DamageTaken = 0;

        public event Action<string, int, int> ArmorHit;  // plateId, damage, sourceId

        public void ResetForNewEpisode(Vector3 spawnPosition, float spawnYaw)
        {
            transform.position = spawnPosition;
            transform.rotation = Quaternion.Euler(0f, spawnYaw * Mathf.Rad2Deg, 0f);
            DamageTaken = 0;
        }

        public void SetChassisCmd(float vxBody, float vyBody, float omega) {}

        public Dictionary<string, object> OdomState() => new Dictionary<string, object>
        {
            { "position_world", Vec3Dict(transform.position) },
            { "linear_velocity", Vec3Dict(Vector3.zero) },
            { "yaw_world", transform.rotation.eulerAngles.y * Mathf.Deg2Rad },
        };

        public Dictionary<string, object> GimbalState() => new Dictionary<string, object>
        {
            { "yaw", 0f }, { "pitch", 0f }, { "yaw_rate", 0f }, { "pitch_rate", 0f },
        };

        protected void RaiseArmorHit(string plateId, int damage, int sourceId)
            => ArmorHit?.Invoke(plateId, damage, sourceId);

        private static Dictionary<string, object> Vec3Dict(Vector3 v) => new Dictionary<string, object>
        {
            { "x", (double)v.x }, { "y", (double)v.y }, { "z", (double)v.z },
        };
    }
}
