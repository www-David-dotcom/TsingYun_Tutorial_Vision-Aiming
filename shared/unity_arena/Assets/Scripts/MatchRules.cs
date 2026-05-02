using System.Collections.Generic;
using UnityEngine;

namespace TsingYun.UnityArena
{
    public enum MatchOutcome
    {
        InProgress,
        RedWin,
        BlueWin,
        Draw,
    }

    public enum BoostPointHolder
    {
        Unheld,
        Red,
        Blue,
        Contested,
    }

    public struct MatchScoreState
    {
        public float RedBoostScore;
        public float BlueBoostScore;
        public int RedDamageDealt;
        public int BlueDamageDealt;
    }

    public readonly struct TeamPosition
    {
        public readonly string Team;
        public readonly Vector3 Position;
        public readonly bool Active;

        public TeamPosition(string team, Vector3 position, bool active)
        {
            Team = team;
            Position = position;
            Active = active;
        }
    }

    public readonly struct RespawnPoint
    {
        public readonly Vector3 Position;
        public readonly float Yaw;

        public RespawnPoint(Vector3 position, float yaw)
        {
            Position = position;
            Yaw = yaw;
        }
    }

    public class MatchWorldState
    {
        private readonly Vector3[] _boostPoints = new Vector3[GameConstants.BoostPointCount];
        private readonly BoostPointHolder[] _boostPointHolders = new BoostPointHolder[GameConstants.BoostPointCount];
        private readonly Dictionary<string, RespawnState> _respawns = new Dictionary<string, RespawnState>();

        public IReadOnlyList<Vector3> BoostPoints => _boostPoints;
        public IReadOnlyList<BoostPointHolder> BoostPointHolders => _boostPointHolders;
        public int BoostPointCount => _boostPoints.Length;

        private struct RespawnState
        {
            public bool Waiting;
            public Vector3 Position;
            public float Yaw;
            public float DeathElapsedSeconds;
        }

        public void Reset()
        {
            for (int i = 0; i < _boostPointHolders.Length; i++)
            {
                _boostPointHolders[i] = BoostPointHolder.Unheld;
            }
            _respawns.Clear();
        }

        public void ResetBoostPoints(System.Func<float> random01X, System.Func<float> random01Z)
        {
            Reset();
            var min = new Vector3(
                GameConstants.BoostPointArenaMinX,
                0f,
                GameConstants.BoostPointArenaMinZ);
            var max = new Vector3(
                GameConstants.BoostPointArenaMaxX,
                0f,
                GameConstants.BoostPointArenaMaxZ);
            for (int i = 0; i < _boostPoints.Length; i++)
            {
                _boostPoints[i] = MatchRules.GenerateBoostPoint(
                    random01X(),
                    random01Z(),
                    min,
                    max);
                _boostPointHolders[i] = BoostPointHolder.Unheld;
            }
        }

        public void SetBoostPointForTest(int index, Vector3 point)
        {
            _boostPoints[index] = point;
            _boostPointHolders[index] = BoostPointHolder.Unheld;
        }

        public void UpdateBoostHolders(
            IReadOnlyList<TeamPosition> redPositions,
            IReadOnlyList<TeamPosition> bluePositions,
            float holdRadius)
        {
            List<Vector3> activeRed = ActivePositions(redPositions);
            List<Vector3> activeBlue = ActivePositions(bluePositions);
            for (int i = 0; i < _boostPoints.Length; i++)
            {
                _boostPointHolders[i] = MatchRules.DetermineBoostHolder(
                    _boostPoints[i],
                    activeRed,
                    activeBlue,
                    holdRadius);
            }
        }

        public void CountHeldBoostPoints(out int redHeldPoints, out int blueHeldPoints)
            => MatchRules.CountHeldBoostPoints(_boostPointHolders, out redHeldPoints, out blueHeldPoints);

        public bool RegisterDeath(string key, Vector3 position, float yaw, float elapsedSeconds)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (_respawns.TryGetValue(key, out var state) && state.Waiting) return false;
            _respawns[key] = new RespawnState
            {
                Waiting = true,
                Position = position,
                Yaw = yaw,
                DeathElapsedSeconds = elapsedSeconds,
            };
            return true;
        }

        public bool TryConsumeReadyRespawn(string key, float elapsedSeconds, out RespawnPoint respawn)
        {
            respawn = default;
            if (string.IsNullOrEmpty(key) || !_respawns.TryGetValue(key, out var state) || !state.Waiting) return false;
            if (!MatchRules.IsRespawnReady(elapsedSeconds - state.DeathElapsedSeconds)) return false;

            respawn = new RespawnPoint(state.Position, state.Yaw);
            _respawns[key] = new RespawnState();
            return true;
        }

        private static List<Vector3> ActivePositions(IReadOnlyList<TeamPosition> source)
        {
            var positions = new List<Vector3>();
            if (source == null) return positions;
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].Active) positions.Add(source[i].Position);
            }
            return positions;
        }
    }

    public class MatchRuntimeState
    {
        public MatchScoreState Score { get; private set; }
        public float LastRuleTickSeconds { get; private set; }
        public int PlayerProjectilesFired { get; private set; }
        public int TotalArmorHits { get; private set; }
        public int PlayerArmorHits { get; private set; }
        public int TeamProjectilesFired { get; private set; }
        public int TeamArmorHits { get; private set; }
        public float PlayerHitRate => MatchRules.HitRate(PlayerArmorHits, PlayerProjectilesFired);
        public float TeamHitRate => MatchRules.HitRate(TeamArmorHits, TeamProjectilesFired);

        public void Reset()
        {
            Score = new MatchScoreState();
            LastRuleTickSeconds = 0f;
            PlayerProjectilesFired = 0;
            TotalArmorHits = 0;
            PlayerArmorHits = 0;
            TeamProjectilesFired = 0;
            TeamArmorHits = 0;
        }

        public float BeginRuleTick(float elapsedSeconds)
        {
            float dt = Mathf.Max(0f, elapsedSeconds - LastRuleTickSeconds);
            LastRuleTickSeconds = Mathf.Max(LastRuleTickSeconds, elapsedSeconds);
            return dt;
        }

        public void RecordPlayerProjectileFired()
        {
            PlayerProjectilesFired++;
            TeamProjectilesFired++;
        }

        public void RecordArmorHit(string sourceTeam, string playerTeam, int damage)
        {
            if (damage <= 0) return;
            TotalArmorHits++;
            if (sourceTeam == playerTeam)
            {
                PlayerArmorHits++;
                TeamArmorHits++;
            }
            MatchScoreState score = Score;
            MatchRules.RecordArmorHit(ref score, sourceTeam, damage);
            Score = score;
        }

        public void ApplyBoostScoring(int redHeldPoints, int blueHeldPoints, float seconds)
        {
            MatchScoreState score = Score;
            MatchRules.ApplyBoostScoring(ref score, redHeldPoints, blueHeldPoints, seconds);
            Score = score;
        }

        public int DamageDealtByTeam(string team)
            => MatchRules.DamageDealtByTeam(Score, team);
    }

    public class FireHeatState
    {
        public float Heat { get; private set; }
        public bool IsLocked { get; private set; }
        public bool CanFire => !IsLocked;

        public void Reset()
        {
            Heat = 0f;
            IsLocked = false;
        }

        public void RecordShot()
        {
            Heat += GameConstants.FireHeatPerShot;
            if (Heat >= GameConstants.FireHeatLockThreshold)
            {
                IsLocked = true;
            }
        }

        public void Cool(float seconds)
        {
            if (seconds <= 0f) return;

            Heat = Mathf.Max(0f, Heat - GameConstants.FireHeatCooldownPerSecond * seconds);
            if (IsLocked && Heat <= GameConstants.FireHeatSafeThreshold)
            {
                IsLocked = false;
            }
        }
    }

    public static class MatchRules
    {
        public static void ApplyHealing(ChassisHpState hp, float seconds)
        {
            if (hp == null || seconds <= 0f || hp.IsDestroyed) return;
            int amount = Mathf.FloorToInt(GameConstants.HealingHpPerSecond * seconds);
            hp.Heal(amount);
        }

        public static void ApplyBoostScoring(
            ref MatchScoreState score,
            int redHeldPoints,
            int blueHeldPoints,
            float seconds)
        {
            if (seconds <= 0f) return;
            score.RedBoostScore += Mathf.Max(0, redHeldPoints)
                * GameConstants.BoostScorePointsPerSecond
                * seconds;
            score.BlueBoostScore += Mathf.Max(0, blueHeldPoints)
                * GameConstants.BoostScorePointsPerSecond
                * seconds;
        }

        public static BoostPointHolder DetermineBoostHolder(
            Vector3 point,
            IReadOnlyList<Vector3> redPositions,
            IReadOnlyList<Vector3> bluePositions,
            float holdRadius)
        {
            bool redPresent = AnyWithinRadius(point, redPositions, holdRadius);
            bool bluePresent = AnyWithinRadius(point, bluePositions, holdRadius);
            if (redPresent && bluePresent) return BoostPointHolder.Contested;
            if (redPresent) return BoostPointHolder.Red;
            if (bluePresent) return BoostPointHolder.Blue;
            return BoostPointHolder.Unheld;
        }

        public static void CountHeldBoostPoints(
            IReadOnlyList<BoostPointHolder> holders,
            out int redHeldPoints,
            out int blueHeldPoints)
        {
            redHeldPoints = 0;
            blueHeldPoints = 0;
            if (holders == null) return;
            for (int i = 0; i < holders.Count; i++)
            {
                if (holders[i] == BoostPointHolder.Red) redHeldPoints++;
                else if (holders[i] == BoostPointHolder.Blue) blueHeldPoints++;
            }
        }

        public static Vector3 GenerateBoostPoint(
            float random01X,
            float random01Z,
            Vector3 min,
            Vector3 max)
        {
            float x = Mathf.Lerp(min.x, max.x, Mathf.Clamp01(random01X));
            float z = Mathf.Lerp(min.z, max.z, Mathf.Clamp01(random01Z));
            return new Vector3(x, 0f, z);
        }

        public static void RecordArmorHit(ref MatchScoreState score, string sourceTeam, int damage)
        {
            if (damage <= 0) return;
            if (IsRed(sourceTeam))
            {
                score.RedDamageDealt += damage;
            }
            else if (IsBlue(sourceTeam))
            {
                score.BlueDamageDealt += damage;
            }
        }

        public static int DamageDealtByTeam(MatchScoreState score, string team)
        {
            if (IsRed(team)) return score.RedDamageDealt;
            if (IsBlue(team)) return score.BlueDamageDealt;
            return 0;
        }

        public static MatchOutcome ResolveOutcome(MatchScoreState score, float elapsedSeconds)
        {
            bool redThreshold = score.RedBoostScore >= GameConstants.BoostScoreWinThreshold;
            bool blueThreshold = score.BlueBoostScore >= GameConstants.BoostScoreWinThreshold;
            if (redThreshold || blueThreshold)
            {
                return CompareFloatScores(score.RedBoostScore, score.BlueBoostScore);
            }

            if (elapsedSeconds < GameConstants.MatchDurationSeconds)
            {
                return MatchOutcome.InProgress;
            }

            MatchOutcome boostOutcome = CompareFloatScores(score.RedBoostScore, score.BlueBoostScore);
            if (boostOutcome != MatchOutcome.Draw) return boostOutcome;

            if (score.RedDamageDealt > score.BlueDamageDealt) return MatchOutcome.RedWin;
            if (score.BlueDamageDealt > score.RedDamageDealt) return MatchOutcome.BlueWin;
            return MatchOutcome.Draw;
        }

        public static bool IsRespawnReady(float elapsedSinceDeathSeconds)
            => elapsedSinceDeathSeconds >= GameConstants.RespawnDelaySeconds;

        public static float HitRate(int hits, int shots)
        {
            if (shots <= 0) return 0f;
            return Mathf.Clamp01((float)Mathf.Max(0, hits) / shots);
        }

        public static string ToEpisodeOutcome(MatchOutcome outcome, string playerTeam)
        {
            switch (outcome)
            {
                case MatchOutcome.RedWin:
                    return IsRed(playerTeam) ? "OUTCOME_WIN" : "OUTCOME_LOSS";
                case MatchOutcome.BlueWin:
                    return IsBlue(playerTeam) ? "OUTCOME_WIN" : "OUTCOME_LOSS";
                case MatchOutcome.Draw:
                    return "OUTCOME_DRAW";
                default:
                    return "OUTCOME_TIMEOUT";
            }
        }

        private static MatchOutcome CompareFloatScores(float red, float blue)
        {
            if (red > blue) return MatchOutcome.RedWin;
            if (blue > red) return MatchOutcome.BlueWin;
            return MatchOutcome.Draw;
        }

        private static bool AnyWithinRadius(
            Vector3 point,
            IReadOnlyList<Vector3> positions,
            float radius)
        {
            if (positions == null || radius <= 0f) return false;
            float radiusSq = radius * radius;
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 delta = positions[i] - point;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSq) return true;
            }
            return false;
        }

        private static bool IsRed(string team) => team == "red";

        private static bool IsBlue(string team) => team == "blue";
    }

    public class MatchRuleRuntime
    {
        public MatchRuntimeState State { get; } = new MatchRuntimeState();
        public MatchWorldState World { get; } = new MatchWorldState();
        public FireHeatState FireHeat { get; } = new FireHeatState();
        public bool CanFire => FireHeat.CanFire;

        public void Reset(System.Func<float> random01X, System.Func<float> random01Z)
        {
            State.Reset();
            FireHeat.Reset();
            World.ResetBoostPoints(random01X, random01Z);
        }

        public float BeginRuleTick(float elapsedSeconds)
        {
            return State.BeginRuleTick(elapsedSeconds);
        }

        public void CoolFireHeat(float seconds)
        {
            FireHeat.Cool(seconds);
        }

        public void RecordShotHeat()
        {
            FireHeat.RecordShot();
        }

        public void RecordPlayerProjectileFired()
        {
            State.RecordPlayerProjectileFired();
        }

        public void RecordArmorHit(string sourceTeam, string playerTeam, int damage)
        {
            State.RecordArmorHit(sourceTeam, playerTeam, damage);
        }

        public void ApplyBoostScoring(int redHeldPoints, int blueHeldPoints, float seconds)
        {
            State.ApplyBoostScoring(redHeldPoints, blueHeldPoints, seconds);
        }

        public MatchOutcome ResolveOutcome(float elapsedSeconds)
        {
            return MatchRules.ResolveOutcome(State.Score, elapsedSeconds);
        }
    }
}
