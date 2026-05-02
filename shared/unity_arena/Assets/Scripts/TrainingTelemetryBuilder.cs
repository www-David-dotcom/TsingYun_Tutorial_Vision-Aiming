using System.Collections.Generic;
using UnityEngine;

namespace TsingYun.UnityArena
{
    public static class TrainingTelemetryBuilder
    {
        public static Dictionary<string, object> Build(
            long stampNs,
            TrainingTargetSample targetSample,
            MatchRuntimeState runtimeState,
            string playerTeam,
            bool episodeDone)
        {
            int damageDealt = runtimeState.DamageDealtByTeam(playerTeam);
            float hitRate = runtimeState.PlayerHitRate;
            double reward = damageDealt * 0.01
                + runtimeState.PlayerArmorHits * 0.1
                - runtimeState.PlayerProjectilesFired * 0.01;

            return new Dictionary<string, object>
            {
                { "stamp_ns", stampNs },
                { "target_position_world", Vec3Dict(targetSample.Position) },
                { "target_velocity_world", Vec3Dict(targetSample.VelocityWorld) },
                { "target_yaw_world", (double)targetSample.YawRad },
                { "target_yaw_rate", (double)targetSample.YawRateRadPerSecond },
                { "damage_dealt", damageDealt },
                { "projectiles_fired", runtimeState.PlayerProjectilesFired },
                { "armor_hits", runtimeState.PlayerArmorHits },
                { "player_hit_rate", (double)hitRate },
                { "step_reward", reward },
                { "episode_done", episodeDone },
            };
        }

        private static Dictionary<string, object> Vec3Dict(Vector3 v)
            => new Dictionary<string, object>
            {
                { "x", (double)v.x },
                { "y", (double)v.y },
                { "z", (double)v.z },
            };
    }
}
