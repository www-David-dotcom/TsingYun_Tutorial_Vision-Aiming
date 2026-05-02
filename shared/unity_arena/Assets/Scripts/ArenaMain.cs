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
    // replay recorder.
    public class ArenaMain : MonoBehaviour
    {
        public const string SimBuildSha = "stage12a-unity-scaffold-1.6";
        public const long DefaultDurationNs = GameConstants.MatchDurationNanoseconds;
        public const float BulletsPerSecond = GameConstants.FireRateRoundsPerSecond;
        private const float BulletIntervalSeconds = GameConstants.FireIntervalSeconds;

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
        public float HealingZoneRadius = GameConstants.HealingZoneRadiusMeters;
        public float BoostPointHoldRadius = GameConstants.BoostPointHoldRadiusMeters;

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
        private readonly List<Dictionary<string, object>> _events = new List<Dictionary<string, object>>();
        private readonly MatchRuleRuntime _rules = new MatchRuleRuntime();
        private readonly ArenaEpisodeClock _clock = new ArenaEpisodeClock(() => _processClock.ElapsedMilliseconds);
        private RuleZonePresentation _ruleZonePresentation;
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
                if (!_rules.CanFire)
                {
                    _pendingShots = 0;
                    return;
                }
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
            // schema.md fixes match duration at exactly 5 minutes. The
            // duration_ns reset field is kept on the wire for old harnesses,
            // but no longer overrides game rules.
            DurationNs = DefaultDurationNs;

            SeedRng.Reseed(seedValue);
            EpisodeId = $"ep-{seedValue:x16}";
            _clock.Reset();
            FrameId = 0;
            _events.Clear();
            _rules.Reset(SeedRng.NextFloat, SeedRng.NextFloat);
            EnsureRuleMarkers();
            _pendingShots = 0;
            _nextShotTime = 0f;

            // Position AND rotation come from the spawn-point transforms.
            // Convention (Unity-standard, +Z forward): the spawn point's
            // blue arrow gizmo (transform.forward) points at where the
            // chassis should aim — the muzzle, the gimbal camera, and
            // ComputeShot's fwd direction are all along chassis local +Z.
            // Just rotate the SpawnPoint normally; what you see is what
            // the chassis spawns facing.
            Vector3 blueSpawn = SpawnPointBlue != null ? SpawnPointBlue.position : new Vector3(-3f, 0f, 0f);
            Vector3 redSpawn  = SpawnPointRed   != null ? SpawnPointRed.position   : new Vector3(3f, 0f, 0f);
            float blueYaw = SpawnPointBlue != null ? SpawnPointBlue.eulerAngles.y * Mathf.Deg2Rad : 0f;
            float redYaw  = SpawnPointRed   != null ? SpawnPointRed.eulerAngles.y * Mathf.Deg2Rad : Mathf.PI;
            BlueChassis.ResetForNewEpisode(blueSpawn, blueYaw);
            RedChassis.ResetForNewEpisode(redSpawn, redYaw);
            // Despawn projectiles spawned by the previous episode.
            if (ProjectileRoot != null)
                foreach (Transform child in ProjectileRoot) Destroy(child.gameObject);

            State = EpisodeState.Running;
            _replay.Start(EpisodeId, seedValue);

            return ArenaWirePayloadBuilder.BuildInitialState(
                BuildSensorBundle(),
                FramePort,
                SimBuildSha);
        }

        public Dictionary<string, object> EnvStep(Dictionary<string, object> cmd)
        {
            if (State != EpisodeState.Running)
                return new Dictionary<string, object> { { "_error", $"env_step called in state={State}" } };

            // Apply the gimbal cmd from this tick. target_yaw/pitch are
            // absolute targets in radians;
            // *_rate_ff are optional feed-forward rates the agent can use to
            // anticipate motion. Missing keys default to zero.
            if (!BlueChassis.IsDestroyed && BlueChassis.Gimbal != null)
            {
                BlueChassis.Gimbal.SetTarget(
                    (float)AsDouble(cmd, "target_yaw", 0.0),
                    (float)AsDouble(cmd, "target_pitch", 0.0),
                    (float)AsDouble(cmd, "yaw_rate_ff", 0.0),
                    (float)AsDouble(cmd, "pitch_rate_ff", 0.0));
            }

            ApplyTimedMatchRules();
            FrameId++;
            if (ResolveMatchOutcome() != MatchOutcome.InProgress) State = EpisodeState.Finishing;
            return BuildSensorBundle();
        }

        public Dictionary<string, object> EnvPushFire(Dictionary<string, object> cmd)
        {
            if (State != EpisodeState.Running)
                return new Dictionary<string, object> { { "accepted", false }, { "reason", "no_episode" }, { "queued_count", 0 } };
            if (BlueChassis.IsDestroyed)
                return new Dictionary<string, object> { { "accepted", false }, { "reason", "destroyed" }, { "queued_count", 0 } };
            if (ProjectilePrefab == null || BlueChassis.Gimbal == null)
                return new Dictionary<string, object> { { "accepted", false }, { "reason", "no_prefab" }, { "queued_count", 0 } };
            if (!_rules.CanFire)
            {
                _pendingShots = 0;
                return new Dictionary<string, object> { { "accepted", false }, { "reason", "fire_locked" }, { "queued_count", 0 } };
            }

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
            _rules.RecordShotHeat();
            _rules.RecordPlayerProjectileFired();
            _events.Add(new Dictionary<string, object>
            {
                { "stamp_ns", NowNs() }, { "kind", "KIND_FIRED" },
                { "armor_id", "" }, { "damage", 0 },
            });
        }

        private Dictionary<string, object> BuildSensorBundle()
        {
            long stamp = NowNs();
            return ArenaWirePayloadBuilder.BuildSensorBundle(
                FrameId,
                $"frames.{SeedRng.CurrentSeed()}",
                stamp,
                BlueChassis.GimbalState(),
                BlueChassis.OdomState(),
                OracleHints,
                RedChassis.transform.position,
                RedChassis.LinearVelocity);
        }

        private Dictionary<string, object> BuildEpisodeStats()
            => ArenaWirePayloadBuilder.BuildEpisodeStats(
                EpisodeId,
                SeedRng.CurrentSeed(),
                NowNs(),
                SimBuildSha,
                OpponentTier,
                ResolveEpisodeOutcome(),
                BlueChassis.DamageTaken,
                _rules.State,
                BlueChassis.Team,
                _events);

        private MatchOutcome ResolveMatchOutcome()
            => _rules.ResolveOutcome(NowNs() / 1_000_000_000f);

        private string ResolveEpisodeOutcome()
            => MatchRules.ToEpisodeOutcome(ResolveMatchOutcome(), BlueChassis.Team);

        private void OnBlueArmorHit(string plateId, int damage, string sourceTeam)
        {
            RecordArmorHit(plateId, damage, sourceTeam, BlueChassis);
        }

        private void OnRedArmorHit(string plateId, int damage, string sourceTeam)
        {
            RecordArmorHit(plateId, damage, sourceTeam, RedChassis);
        }

        private void RecordArmorHit(string plateId, int damage, string sourceTeam, Chassis target)
        {
            if (damage <= 0) return;
            _rules.RecordArmorHit(sourceTeam, BlueChassis.Team, damage);
            _events.Add(new Dictionary<string, object>
            {
                { "stamp_ns", NowNs() }, { "kind", "KIND_HIT_ARMOR" },
                { "armor_id", plateId }, { "damage", damage },
            });
            RegisterDeathIfNeeded(target);
        }

        private void ApplyTimedMatchRules()
        {
            float elapsedSeconds = NowNs() / 1_000_000_000f;
            float dt = _rules.BeginRuleTick(elapsedSeconds);
            if (dt <= 0f) return;

            ApplyHealingZones(dt);
            ApplyBoostPoints(dt);
            ApplyRespawns(elapsedSeconds);
            _rules.CoolFireHeat(dt);
        }

        private void ApplyHealingZones(float dt)
        {
            if (IsActiveNear(BlueChassis, SpawnPointBlue, HealingZoneRadius)) BlueChassis.Heal(dt);
            if (IsActiveNear(RedChassis, SpawnPointRed, HealingZoneRadius)) RedChassis.Heal(dt);
        }

        private void ApplyBoostPoints(float dt)
        {
            _rules.World.UpdateBoostHolders(
                TeamPositionsFor("red"),
                TeamPositionsFor("blue"),
                BoostPointHoldRadius);
            RenderRuleZones();
            _rules.World.CountHeldBoostPoints(out int redHeld, out int blueHeld);
            _rules.ApplyBoostScoring(redHeld, blueHeld, dt);
        }

        private void ApplyRespawns(float elapsedSeconds)
        {
            ApplyRespawn(BlueChassis, elapsedSeconds);
            ApplyRespawn(RedChassis, elapsedSeconds);
        }

        private void RegisterDeathIfNeeded(Chassis chassis)
        {
            if (chassis == null || !chassis.IsDestroyed) return;
            if (_rules.World.RegisterDeath(RespawnKey(chassis), chassis.transform.position, chassis.ChassisYaw, NowNs() / 1_000_000_000f)
                && chassis == BlueChassis)
            {
                _pendingShots = 0;
            }
        }

        private void ApplyRespawn(Chassis chassis, float elapsedSeconds)
        {
            if (chassis == null) return;
            if (!_rules.World.TryConsumeReadyRespawn(RespawnKey(chassis), elapsedSeconds, out RespawnPoint respawn)) return;
            chassis.RespawnAt(respawn.Position, respawn.Yaw);
        }

        private List<TeamPosition> TeamPositionsFor(string team)
        {
            var positions = new List<TeamPosition>(2);
            AddTeamPosition(positions, BlueChassis, team);
            AddTeamPosition(positions, RedChassis, team);
            return positions;
        }

        private static void AddTeamPosition(List<TeamPosition> positions, Chassis chassis, string team)
        {
            if (chassis == null || chassis.Team != team) return;
            positions.Add(new TeamPosition(team, chassis.transform.position, !chassis.IsDestroyed));
        }

        private static string RespawnKey(Chassis chassis)
            => chassis != null ? $"{chassis.Team}:{chassis.ChassisId}" : "";

        private static bool IsActiveNear(Chassis chassis, Transform point, float radius)
        {
            if (chassis == null || point == null || chassis.IsDestroyed || radius <= 0f) return false;
            Vector3 delta = chassis.transform.position - point.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= radius * radius;
        }

        private void EnsureRuleMarkers()
        {
            RenderRuleZones();
        }

        private void RenderRuleZones()
        {
            Vector3 bluePosition = SpawnPointBlue != null ? SpawnPointBlue.position : BlueChassis.transform.position;
            Vector3 redPosition = SpawnPointRed != null ? SpawnPointRed.position : RedChassis.transform.position;
            RuleZonePresentation.Render(
                bluePosition,
                redPosition,
                HealingZoneRadius,
                _rules.World,
                BoostPointHoldRadius);
        }

        private RuleZonePresentation RuleZonePresentation
        {
            get
            {
                if (_ruleZonePresentation == null)
                {
                    _ruleZonePresentation = GetComponent<RuleZonePresentation>();
                    if (_ruleZonePresentation == null)
                    {
                        _ruleZonePresentation = gameObject.AddComponent<RuleZonePresentation>();
                    }
                }
                return _ruleZonePresentation;
            }
        }

        private long NowNs()
        {
            return _clock.NowNs;
        }

        internal void AdvanceEpisodeClockForTest(float seconds)
        {
            _clock.AdvanceForTest(seconds);
        }

        internal void AllowNextShotForTest()
        {
            _nextShotTime = Time.time;
        }

        internal void DrainShotQueueForTest()
        {
            DrainShotQueue();
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

    }
}
