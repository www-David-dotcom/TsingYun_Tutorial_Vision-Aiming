using System.Collections.Generic;
using NUnit.Framework;
using TsingYun.UnityArena;
using UnityEngine;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class ArenaWirePayloadBuilderTests
    {
        [Test]
        public void BuildSensorBundle_UsesStableV1FrameAndMotionFields()
        {
            var gimbal = new Dictionary<string, object>
            {
                { "yaw", 0.25 },
                { "pitch", -0.1 },
                { "yaw_rate", 0.02 },
                { "pitch_rate", -0.03 },
            };
            var odom = new Dictionary<string, object>
            {
                { "position_world", Vec3Dict(new Vector3(-3f, 0f, 1f)) },
                { "linear_velocity", Vec3Dict(new Vector3(0.5f, 0f, 0.25f)) },
                { "yaw_world", 0.125 },
            };

            Dictionary<string, object> bundle = ArenaWirePayloadBuilder.BuildSensorBundle(
                frameId: 7,
                frameTopic: "frames.42",
                stampNs: 123_000_000L,
                gimbalState: gimbal,
                odomState: odom,
                oracleHintsEnabled: true,
                targetPositionWorld: new Vector3(3f, 0f, 2f),
                targetVelocityWorld: new Vector3(-0.5f, 0f, 0.1f));

            var frame = (Dictionary<string, object>)bundle["frame"];
            Assert.AreEqual(7L, frame["frame_id"]);
            Assert.AreEqual("frames.42", frame["zmq_topic"]);
            Assert.AreEqual(123_000_000L, frame["stamp_ns"]);
            Assert.AreEqual(1280L, frame["width"]);
            Assert.AreEqual(720L, frame["height"]);
            Assert.AreEqual("PIXEL_FORMAT_RGB888", frame["pixel_format"]);

            Assert.AreEqual(123_000_000L, gimbal["stamp_ns"]);
            Assert.AreEqual(123_000_000L, odom["stamp_ns"]);
            Assert.IsTrue(bundle.ContainsKey("oracle"));
        }

        [Test]
        public void BuildEpisodeStats_MapsRuntimeTelemetryIntoStableFields()
        {
            var score = new MatchRuntimeState();
            score.RecordPlayerProjectileFired();
            score.RecordArmorHit(sourceTeam: "blue", playerTeam: "blue", damage: GameConstants.BulletDamage);
            var events = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    { "stamp_ns", 1_000_000L },
                    { "kind", "KIND_HIT_ARMOR" },
                    { "armor_id", "red.front" },
                    { "damage", GameConstants.BulletDamage },
                },
            };

            Dictionary<string, object> stats = ArenaWirePayloadBuilder.BuildEpisodeStats(
                episodeId: "ep-000000000000002a",
                seed: 42L,
                durationNs: 10_000_000L,
                simulatorBuildSha256: "test-sha",
                opponentTier: "bronze",
                outcome: "OUTCOME_WIN",
                damageTaken: 40,
                runtimeState: score,
                playerTeam: "blue",
                events: events);

            Assert.AreEqual("ep-000000000000002a", stats["episode_id"]);
            Assert.AreEqual(42L, stats["seed"]);
            Assert.AreEqual("test-sha", stats["simulator_build_sha256"]);
            Assert.AreEqual(GameConstants.BulletDamage, stats["damage_dealt"]);
            Assert.AreEqual(40, stats["damage_taken"]);
            Assert.AreEqual(1, stats["projectiles_fired"]);
            Assert.AreEqual(1, stats["armor_hits"]);
            Assert.AreEqual(1.0, (double)stats["player_hit_rate"], 1e-6);
            Assert.AreEqual(1.0, (double)stats["team_hit_rate"], 1e-6);
            Assert.AreEqual(1, ((List<object>)stats["events"]).Count);
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
