using System.Collections.Generic;
using UnityEngine;

namespace TsingYun.UnityArena
{
    public static class ArenaWirePayloadBuilder
    {
        public const long FrameWidth = 1280L;
        public const long FrameHeight = 720L;
        public const string FramePixelFormat = "PIXEL_FORMAT_RGB888";

        public static Dictionary<string, object> BuildInitialState(
            Dictionary<string, object> bundle,
            int framePort,
            string simulatorBuildSha256)
            => new Dictionary<string, object>
            {
                { "bundle", bundle },
                { "zmq_frame_endpoint", $"tcp://127.0.0.1:{framePort}" },
                { "simulator_build_sha256", simulatorBuildSha256 },
            };

        public static Dictionary<string, object> BuildSensorBundle(
            long frameId,
            string frameTopic,
            long stampNs,
            Dictionary<string, object> gimbalState,
            Dictionary<string, object> odomState,
            bool oracleHintsEnabled,
            Vector3 targetPositionWorld,
            Vector3 targetVelocityWorld)
        {
            gimbalState["stamp_ns"] = stampNs;
            odomState["stamp_ns"] = stampNs;

            var bundle = new Dictionary<string, object>
            {
                { "frame", new Dictionary<string, object>
                {
                    { "frame_id", frameId },
                    { "zmq_topic", frameTopic },
                    { "stamp_ns", stampNs },
                    { "width", FrameWidth },
                    { "height", FrameHeight },
                    { "pixel_format", FramePixelFormat },
                }},
                { "imu", new Dictionary<string, object>
                {
                    { "stamp_ns", stampNs },
                    { "angular_velocity", Vec3Dict(Vector3.zero) },
                    { "linear_accel", Vec3Dict(new Vector3(0f, -9.81f, 0f)) },
                    { "orientation", new Dictionary<string, object>
                    {
                        { "w", 1.0 },
                        { "x", 0.0 },
                        { "y", 0.0 },
                        { "z", 0.0 },
                    }},
                }},
                { "gimbal", gimbalState },
                { "odom", odomState },
            };

            if (oracleHintsEnabled)
            {
                bundle["oracle"] = new Dictionary<string, object>
                {
                    { "target_position_world", Vec3Dict(targetPositionWorld) },
                    { "target_velocity_world", Vec3Dict(targetVelocityWorld) },
                    { "target_visible", true },
                };
            }

            return bundle;
        }

        public static Dictionary<string, object> BuildEpisodeStats(
            string episodeId,
            long seed,
            long durationNs,
            string simulatorBuildSha256,
            string opponentTier,
            string outcome,
            int damageTaken,
            MatchRuntimeState runtimeState,
            string playerTeam,
            List<Dictionary<string, object>> events)
            => new Dictionary<string, object>
            {
                { "episode_id", episodeId },
                { "seed", seed },
                { "duration_ns", durationNs },
                { "candidate_commit_sha", "" },
                { "candidate_build_sha256", "" },
                { "simulator_build_sha256", simulatorBuildSha256 },
                { "opponent_policy_sha256", "" },
                { "opponent_tier", opponentTier },
                { "outcome", outcome },
                { "damage_dealt", runtimeState.DamageDealtByTeam(playerTeam) },
                { "damage_taken", damageTaken },
                { "projectiles_fired", runtimeState.PlayerProjectilesFired },
                { "armor_hits", runtimeState.TotalArmorHits },
                { "player_hit_rate", (double)runtimeState.PlayerHitRate },
                { "team_hit_rate", (double)runtimeState.TeamHitRate },
                { "aim_latency_p50_ns", 0L },
                { "aim_latency_p95_ns", 0L },
                { "aim_latency_p99_ns", 0L },
                { "events", new List<object>(events) },
            };

        private static Dictionary<string, object> Vec3Dict(Vector3 v)
            => new Dictionary<string, object>
            {
                { "x", (double)v.x },
                { "y", (double)v.y },
                { "z", (double)v.z },
            };
    }
}
