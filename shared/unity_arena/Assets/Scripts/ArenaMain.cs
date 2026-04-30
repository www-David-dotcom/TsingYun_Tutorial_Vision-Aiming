using System.Collections.Generic;
using UnityEngine;

namespace TsingYun.UnityArena
{
    public enum EpisodeState { Idle, Running, Finishing }

    // Episode orchestrator. Owns the TCP control server, frame publisher, and
    // replay recorder. Mirrors arena_main.gd one-to-one.
    public class ArenaMain : MonoBehaviour
    {
        public const string SimBuildSha = "stage12a-unity-scaffold-1.6";
        public const long DefaultDurationNs = 90_000_000_000L;

        public int ControlPort = 7654;
        public int FramePort = 7655;

        public Chassis BlueChassis;
        public Chassis RedChassis;
        public Camera GimbalCamera;
        public Transform ProjectileRoot;

        public EpisodeState State { get; private set; } = EpisodeState.Idle;
        public string EpisodeId { get; private set; } = "";
        public long DurationNs { get; private set; } = DefaultDurationNs;
        public string OpponentTier { get; private set; } = "bronze";
        public bool OracleHints { get; private set; }
        public long FrameId { get; private set; }

        private TcpProtoServer _control;
        private TcpFramePub _framePub;
        private ReplayRecorder _replay;

        private long _startedTicksMs;
        private readonly List<Dictionary<string, object>> _events = new List<Dictionary<string, object>>();
        private int _projectilesFired;
        private int _armorHits;
        private int _damageDealt;

        private void Awake()
        {
            BlueChassis.Team = "blue";
            BlueChassis.ChassisId = 0;
            RedChassis.Team = "red";
            RedChassis.ChassisId = 1;
            BlueChassis.ArmorHit += OnBlueArmorHit;
            RedChassis.ArmorHit += OnRedArmorHit;

            var controlObj = new GameObject("TcpProtoServer");
            controlObj.transform.SetParent(transform);
            _control = controlObj.AddComponent<TcpProtoServer>();
            _control.Port = ControlPort;
            _control.SetDispatcher(Dispatch);

            var frameObj = new GameObject("TcpFramePub");
            frameObj.transform.SetParent(transform);
            _framePub = frameObj.AddComponent<TcpFramePub>();
            _framePub.Port = FramePort;
            _framePub.SourceCamera = GimbalCamera;

            _replay = new ReplayRecorder();

            Debug.Log($"[ArenaMain] control on tcp://0.0.0.0:{ControlPort}, frames on tcp://0.0.0.0:{FramePort}");
        }

        private void OnDestroy()
        {
            // Release the replay file handle deterministically — finalizer-based
            // disposal won't fire before the next scene's ReplayRecorder.Start
            // tries to reopen the same path on the same seed (Unity test runner
            // doing LoadSceneAsync between cases).
            _replay?.Close();
        }

        private object Dispatch(string method, Dictionary<string, object> request)
        {
            switch (method)
            {
                case "env_reset": return EnvReset(request);
                case "env_step": return EnvStep(request);
                case "env_push_fire": return EnvPushFire(request);
                case "env_finish": return EnvFinish(request);
                default: return new Dictionary<string, object> { { "_error", $"unknown method: {method}" } };
            }
        }

        public Dictionary<string, object> EnvReset(Dictionary<string, object> request)
        {
            long seedValue = AsLong(request, "seed", 0);
            OpponentTier = AsString(request, "opponent_tier", "bronze");
            OracleHints = AsBool(request, "oracle_hints", false);
            long requestedDuration = AsLong(request, "duration_ns", 0);
            DurationNs = requestedDuration > 0 ? requestedDuration : DefaultDurationNs;

            SeedRng.Reseed(seedValue);
            EpisodeId = $"ep-{seedValue:x16}";
            _startedTicksMs = System.Diagnostics.Stopwatch.GetTimestamp() * 1000L /
                              System.Diagnostics.Stopwatch.Frequency;
            FrameId = 0;
            _events.Clear();
            _projectilesFired = 0;
            _armorHits = 0;
            _damageDealt = 0;

            BlueChassis.ResetForNewEpisode(new Vector3(-3f, 0f, 0f), 0f);
            RedChassis.ResetForNewEpisode(new Vector3(3f, 0f, 0f), Mathf.PI);
            // Despawn projectiles (12b will populate ProjectileRoot)
            if (ProjectileRoot != null)
                foreach (Transform child in ProjectileRoot) Destroy(child.gameObject);

            State = EpisodeState.Running;
            _replay.Start(EpisodeId, seedValue);

            return new Dictionary<string, object>
            {
                { "bundle", BuildSensorBundle() },
                { "zmq_frame_endpoint", $"tcp://127.0.0.1:{FramePort}" },
                { "simulator_build_sha256", SimBuildSha },
            };
        }

        public Dictionary<string, object> EnvStep(Dictionary<string, object> cmd)
        {
            if (State != EpisodeState.Running)
                return new Dictionary<string, object> { { "_error", $"env_step called in state={State}" } };

            FrameId++;
            if (NowNs() > DurationNs) State = EpisodeState.Finishing;
            return BuildSensorBundle();
        }

        public Dictionary<string, object> EnvPushFire(Dictionary<string, object> cmd)
        {
            if (State != EpisodeState.Running)
                return new Dictionary<string, object> { { "accepted", false }, { "reason", "no_episode" }, { "queued_count", 0 } };

            // Stage 12a stub: report acceptance without spawning. Full spawn in 12b.
            int burst = Mathf.Max(0, (int)AsLong(cmd, "burst_count", 1));
            _projectilesFired += burst;
            return new Dictionary<string, object>
            {
                { "accepted", burst > 0 },
                { "reason", "" },
                { "queued_count", burst },
            };
        }

        public Dictionary<string, object> EnvFinish(Dictionary<string, object> request)
        {
            if (State == EpisodeState.Idle)
                return new Dictionary<string, object> { { "_error", "no episode in progress" } };

            State = EpisodeState.Idle;
            var stats = BuildEpisodeStats();
            _replay.Finish(stats);
            return stats;
        }

        private Dictionary<string, object> BuildSensorBundle()
        {
            long stamp = NowNs();
            var gimbal = BlueChassis.GimbalState();
            gimbal["stamp_ns"] = stamp;
            var bundle = new Dictionary<string, object>
            {
                { "frame", new Dictionary<string, object>
                {
                    { "frame_id", FrameId },
                    { "zmq_topic", $"frames.{SeedRng.CurrentSeed()}" },
                    { "stamp_ns", stamp },
                    { "width", 1280L },
                    { "height", 720L },
                    { "pixel_format", "PIXEL_FORMAT_RGB888" },
                }},
                { "imu", new Dictionary<string, object>
                {
                    { "stamp_ns", stamp },
                    { "angular_velocity", new Dictionary<string, object> { { "x", 0.0 }, { "y", 0.0 }, { "z", 0.0 } } },
                    { "linear_accel", new Dictionary<string, object> { { "x", 0.0 }, { "y", -9.81 }, { "z", 0.0 } } },
                    { "orientation", new Dictionary<string, object> { { "w", 1.0 }, { "x", 0.0 }, { "y", 0.0 }, { "z", 0.0 } } },
                }},
                { "gimbal", gimbal },
                { "odom", BuildOdomPayload(stamp) },
            };
            if (OracleHints)
            {
                Vector3 redPos = RedChassis.transform.position;
                bundle["oracle"] = new Dictionary<string, object>
                {
                    { "target_position_world", new Dictionary<string, object> { { "x", (double)redPos.x }, { "y", (double)redPos.y }, { "z", (double)redPos.z } } },
                    { "target_velocity_world", new Dictionary<string, object> { { "x", 0.0 }, { "y", 0.0 }, { "z", 0.0 } } },
                    { "target_visible", true },
                };
            }
            return bundle;
        }

        private Dictionary<string, object> BuildOdomPayload(long stamp)
        {
            var raw = BlueChassis.OdomState();
            raw["stamp_ns"] = stamp;
            return raw;
        }

        private Dictionary<string, object> BuildEpisodeStats() => new Dictionary<string, object>
        {
            { "episode_id", EpisodeId },
            { "seed", SeedRng.CurrentSeed() },
            { "duration_ns", NowNs() },
            { "candidate_commit_sha", "" },
            { "candidate_build_sha256", "" },
            { "simulator_build_sha256", SimBuildSha },
            { "opponent_policy_sha256", "" },
            { "opponent_tier", OpponentTier },
            { "outcome", ResolveOutcome() },
            { "damage_dealt", _damageDealt },
            { "damage_taken", BlueChassis.DamageTaken },
            { "projectiles_fired", _projectilesFired },
            { "armor_hits", _armorHits },
            { "aim_latency_p50_ns", 0L },
            { "aim_latency_p95_ns", 0L },
            { "aim_latency_p99_ns", 0L },
            { "events", new List<object>(_events) },
        };

        private string ResolveOutcome()
        {
            if (BlueChassis.DamageTaken >= 800 && _damageDealt < 800) return "OUTCOME_LOSS";
            if (_damageDealt >= 800 && BlueChassis.DamageTaken < 800) return "OUTCOME_WIN";
            return "OUTCOME_TIMEOUT";
        }

        private void OnBlueArmorHit(string plateId, int damage, int sourceId)
            => _events.Add(new Dictionary<string, object>
            {
                { "stamp_ns", NowNs() }, { "kind", "KIND_HIT_ARMOR" },
                { "armor_id", plateId }, { "damage", damage },
            });

        private void OnRedArmorHit(string plateId, int damage, int sourceId)
        {
            _armorHits++;
            _damageDealt += damage;
            _events.Add(new Dictionary<string, object>
            {
                { "stamp_ns", NowNs() }, { "kind", "KIND_HIT_ARMOR" },
                { "armor_id", plateId }, { "damage", damage },
            });
        }

        private long NowNs()
        {
            long nowMs = System.Diagnostics.Stopwatch.GetTimestamp() * 1000L /
                         System.Diagnostics.Stopwatch.Frequency;
            return (nowMs - _startedTicksMs) * 1_000_000L;
        }

        private static long AsLong(Dictionary<string, object> dict, string key, long fallback)
        {
            if (!dict.TryGetValue(key, out var v) || v == null) return fallback;
            if (v is long l) return l;
            if (v is double d) return (long)d;
            if (v is int i) return i;
            return fallback;
        }

        private static string AsString(Dictionary<string, object> dict, string key, string fallback)
            => dict.TryGetValue(key, out var v) && v is string s ? s : fallback;

        private static bool AsBool(Dictionary<string, object> dict, string key, bool fallback)
            => dict.TryGetValue(key, out var v) && v is bool b ? b : fallback;
    }
}
