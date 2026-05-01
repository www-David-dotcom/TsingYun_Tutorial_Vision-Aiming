using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace TsingYun.UnityArena
{
    public enum EpisodeState { Idle, Running, Finishing }

    // Episode orchestrator. Owns the TCP control server, frame publisher, and
    // replay recorder. Mirrors arena_main.gd one-to-one.
    public class ArenaMain : MonoBehaviour
    {
        public const string SimBuildSha = "stage12a-unity-scaffold-1.6";
        public const long DefaultDurationNs = 90_000_000_000L;
        // RM Standard robots cap fire rate around 10 Hz; 5 Hz here gives a
        // visible cadence and matches the smoke-test viewing window.
        // Burst fires from EnvPushFire are queued and drained at this rate
        // by Update.DrainShotQueue rather than spawned all in one tick.
        public const float BulletsPerSecond = 5f;
        private const float BulletIntervalSeconds = 1f / BulletsPerSecond;

        public int ControlPort = 7654;
        public int FramePort = 7655;

        public Chassis BlueChassis;
        public Chassis RedChassis;
        public Camera GimbalCamera;
        public Transform ProjectileRoot;

        // MapA scene wires these in Task 24 step 2 (Unity Inspector). Falls back
        // to the placeholder hard-coded spawn vectors if not assigned, so the
        // 12a placeholder ArenaMain scene still runs.
        public Transform SpawnPointBlue;
        public Transform SpawnPointRed;
        public GameObject ProjectilePrefab;

        public EpisodeState State { get; private set; } = EpisodeState.Idle;
        public string EpisodeId { get; private set; } = "";
        public long DurationNs { get; private set; } = DefaultDurationNs;
        public string OpponentTier { get; private set; } = "bronze";
        public bool OracleHints { get; private set; }
        public long FrameId { get; private set; }

        private TcpProtoServer _control;
        private TcpFramePub _framePub;
        private ReplayRecorder _replay;

        // Process-lifetime monotonic clock. Stopwatch.StartNew/.ElapsedMilliseconds
        // is overflow-safe (won't wrap for ~292M years) — replacing the previous
        // `Stopwatch.GetTimestamp() * 1000L / Frequency` math which on Apple
        // Silicon (1 GHz mach_absolute_time, value is mac uptime not process
        // uptime) overflowed `long` after 107 days of mac uptime.
        private static readonly Stopwatch _processClock = Stopwatch.StartNew();
        private long _startedTicksMs;
        private readonly List<Dictionary<string, object>> _events = new List<Dictionary<string, object>>();
        private int _projectilesFired;
        private int _armorHits;
        private int _damageDealt;
        // Rate-limited shot queue. EnvPushFire increments _pendingShots; Update
        // drains one shot per BulletIntervalSeconds. Reset on EnvReset so
        // unfired shots from a previous episode don't carry over.
        private int _pendingShots;
        private float _nextShotTime;

        // TcpProtoServer dispatches on its accept/client thread; EnvReset / Step /
        // PushFire / Finish all touch Unity transform/component APIs which are
        // main-thread-only. Network-thread requests get queued here and pumped
        // off the queue by Update().
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

        private void Awake()
        {
            // Editor Play mode otherwise pauses Update when the window loses
            // focus; the Python smoke harness drives the arena from Terminal,
            // so without this the dispatch queue never drains and every TCP
            // request times out.
            Application.runInBackground = true;

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

        private void Update()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            DrainShotQueue();
        }

        private void DrainShotQueue()
        {
            if (State != EpisodeState.Running) return;
            while (_pendingShots > 0 && Time.time >= _nextShotTime)
            {
                SpawnSingleProjectile();
                _pendingShots--;
                _nextShotTime = Time.time + BulletIntervalSeconds;
            }
        }

        // Called by TcpProtoServer from its accept/client thread. Marshals onto
        // the main thread (Update) so the env_* handlers can safely touch
        // transforms and components, then blocks here until the handler signals
        // completion. A 5 s timeout guards against the queue not draining
        // (scene tearing down, Unity paused, etc).
        private object Dispatch(string method, Dictionary<string, object> request)
        {
            object response = null;
            Exception caught = null;
            // No `using`: if Wait times out, the action may still be queued and
            // would throw ObjectDisposedException on done.Set(). Let GC reclaim.
            var done = new ManualResetEventSlim(false);
            _mainThreadQueue.Enqueue(() =>
            {
                try
                {
                    switch (method)
                    {
                        case "env_reset":     response = EnvReset(request); break;
                        case "env_step":      response = EnvStep(request); break;
                        case "env_push_fire": response = EnvPushFire(request); break;
                        case "env_finish":    response = EnvFinish(request); break;
                        default:              response = new Dictionary<string, object> { { "_error", $"unknown method: {method}" } }; break;
                    }
                }
                catch (Exception ex) { caught = ex; }
                finally { try { done.Set(); } catch (ObjectDisposedException) { /* outer Dispatch already returned */ } }
            });
            if (!done.Wait(5000))
            {
                Debug.LogWarning($"[ArenaMain] dispatch timed out (5s) for method={method} — Application.runInBackground={Application.runInBackground}");
                return new Dictionary<string, object> { { "_error", $"dispatch timed out (5s) for method={method}" } };
            }
            if (caught != null)
            {
                Debug.LogWarning($"[ArenaMain] dispatch caught exception in {method}: {caught}");
                return new Dictionary<string, object> { { "_error", caught.Message } };
            }
            return response;
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
            _startedTicksMs = _processClock.ElapsedMilliseconds;
            FrameId = 0;
            _events.Clear();
            _projectilesFired = 0;
            _armorHits = 0;
            _damageDealt = 0;
            _pendingShots = 0;
            _nextShotTime = 0f;

            Vector3 blueSpawn = SpawnPointBlue != null ? SpawnPointBlue.position : new Vector3(-3f, 0f, 0f);
            Vector3 redSpawn  = SpawnPointRed   != null ? SpawnPointRed.position   : new Vector3(3f, 0f, 0f);
            BlueChassis.ResetForNewEpisode(blueSpawn, 0f);
            RedChassis.ResetForNewEpisode(redSpawn, Mathf.PI);
            // Despawn projectiles spawned by the previous episode.
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

            // Apply the gimbal cmd from this tick. Mirrors arena_main.gd
            // env_step: target_yaw/pitch are absolute targets in radians;
            // *_rate_ff are optional feed-forward rates the agent can use to
            // anticipate motion. Missing keys default to zero.
            if (BlueChassis.Gimbal != null)
            {
                BlueChassis.Gimbal.SetTarget(
                    (float)AsDouble(cmd, "target_yaw", 0.0),
                    (float)AsDouble(cmd, "target_pitch", 0.0),
                    (float)AsDouble(cmd, "yaw_rate_ff", 0.0),
                    (float)AsDouble(cmd, "pitch_rate_ff", 0.0));
            }

            FrameId++;
            if (NowNs() > DurationNs) State = EpisodeState.Finishing;
            return BuildSensorBundle();
        }

        public Dictionary<string, object> EnvPushFire(Dictionary<string, object> cmd)
        {
            if (State != EpisodeState.Running)
                return new Dictionary<string, object> { { "accepted", false }, { "reason", "no_episode" }, { "queued_count", 0 } };
            if (ProjectilePrefab == null || BlueChassis.Gimbal == null)
                return new Dictionary<string, object> { { "accepted", false }, { "reason", "no_prefab" }, { "queued_count", 0 } };

            int burst = Mathf.Max(0, (int)AsLong(cmd, "burst_count", 1));
            // Burst is enqueued; Update.DrainShotQueue spawns one bullet per
            // BulletIntervalSeconds so consecutive shots are visible as a
            // staggered stream rather than overlapping at the muzzle.
            _pendingShots += burst;
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

        private void SpawnSingleProjectile()
        {
            ShotSpec spec = BlueChassis.Gimbal.ComputeShot();
            var go = Instantiate(ProjectilePrefab, spec.Position, spec.Rotation, ProjectileRoot);
            var p = go.GetComponent<Projectile>();
            p.Arm(spec.Velocity, BlueChassis.Team);
            _projectilesFired++;
            _events.Add(new Dictionary<string, object>
            {
                { "stamp_ns", NowNs() }, { "kind", "KIND_FIRED" },
                { "armor_id", "" }, { "damage", 0 },
            });
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
                bundle["oracle"] = new Dictionary<string, object>
                {
                    { "target_position_world", Vec3Dict(RedChassis.transform.position) },
                    // Was hard-coded zero; the MPC agent uses this for lead-
                    // compensation when oracle_hints=true and would otherwise
                    // aim at the current position instead of the intercept.
                    { "target_velocity_world", Vec3Dict(RedChassis.LinearVelocity) },
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
            // HP is per-robot (Chassis.MaxHp). IsDestroyed fires when Hp hits
            // 0; mutual destruction in the same tick falls through to TIMEOUT.
            if (BlueChassis.IsDestroyed && !RedChassis.IsDestroyed) return "OUTCOME_LOSS";
            if (RedChassis.IsDestroyed && !BlueChassis.IsDestroyed) return "OUTCOME_WIN";
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
            return (_processClock.ElapsedMilliseconds - _startedTicksMs) * 1_000_000L;
        }

        private static long AsLong(Dictionary<string, object> dict, string key, long fallback)
        {
            if (!dict.TryGetValue(key, out var v) || v == null) return fallback;
            if (v is long l) return l;
            if (v is double d) return (long)d;
            if (v is int i) return i;
            return fallback;
        }

        private static double AsDouble(Dictionary<string, object> dict, string key, double fallback)
        {
            if (!dict.TryGetValue(key, out var v) || v == null) return fallback;
            if (v is double d) return d;
            if (v is long l) return l;
            if (v is int i) return i;
            return fallback;
        }

        private static string AsString(Dictionary<string, object> dict, string key, string fallback)
            => dict.TryGetValue(key, out var v) && v is string s ? s : fallback;

        private static bool AsBool(Dictionary<string, object> dict, string key, bool fallback)
            => dict.TryGetValue(key, out var v) && v is bool b ? b : fallback;

        private static Dictionary<string, object> Vec3Dict(Vector3 v) => new Dictionary<string, object>
        {
            { "x", (double)v.x }, { "y", (double)v.y }, { "z", (double)v.z },
        };
    }
}
