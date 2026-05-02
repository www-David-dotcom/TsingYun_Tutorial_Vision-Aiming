using NUnit.Framework;
using TsingYun.UnityArena;
using UnityEngine;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class MatchRulesTests
    {
        [Test]
        public void ApplyHealing_RestoresHpAtSchemaRateAndClampsAtMax()
        {
            var hp = new ChassisHpState { MaxHp = GameConstants.VehicleHpOneVsOne };
            hp.Reset();
            hp.ApplyDamage(50);

            MatchRules.ApplyHealing(hp, 2f);
            Assert.AreEqual(270, hp.Hp);

            MatchRules.ApplyHealing(hp, 10f);
            Assert.AreEqual(GameConstants.VehicleHpOneVsOne, hp.Hp);
        }

        [Test]
        public void ApplyBoostScoring_AddsThreePointsPerHeldPointPerSecond()
        {
            var score = new MatchScoreState();

            MatchRules.ApplyBoostScoring(ref score, redHeldPoints: 1, blueHeldPoints: 2, seconds: 4f);

            Assert.AreEqual(12f, score.RedBoostScore, 1e-6f);
            Assert.AreEqual(24f, score.BlueBoostScore, 1e-6f);
        }

        [Test]
        public void ResolveOutcome_EndsImmediatelyWhenBoostThresholdReached()
        {
            var score = new MatchScoreState { RedBoostScore = 200f, BlueBoostScore = 199f };

            MatchOutcome outcome = MatchRules.ResolveOutcome(score, elapsedSeconds: 30f);

            Assert.AreEqual(MatchOutcome.RedWin, outcome);
        }

        [Test]
        public void ResolveOutcome_UsesDamageAsTimerTieBreaker()
        {
            var score = new MatchScoreState
            {
                RedBoostScore = 120f,
                BlueBoostScore = 120f,
                RedDamageDealt = 80,
                BlueDamageDealt = 100,
            };

            MatchOutcome outcome = MatchRules.ResolveOutcome(
                score,
                elapsedSeconds: GameConstants.MatchDurationSeconds);

            Assert.AreEqual(MatchOutcome.BlueWin, outcome);
        }

        [Test]
        public void ResolveOutcome_ReturnsDrawWhenBoostAndDamageTieAtTimerEnd()
        {
            var score = new MatchScoreState
            {
                RedBoostScore = 120f,
                BlueBoostScore = 120f,
                RedDamageDealt = 100,
                BlueDamageDealt = 100,
            };

            MatchOutcome outcome = MatchRules.ResolveOutcome(
                score,
                elapsedSeconds: GameConstants.MatchDurationSeconds);

            Assert.AreEqual(MatchOutcome.Draw, outcome);
        }

        [Test]
        public void RecordArmorHit_CreditsDamageToSourceTeam()
        {
            var score = new MatchScoreState();

            MatchRules.RecordArmorHit(ref score, sourceTeam: "blue", damage: GameConstants.BulletDamage);
            MatchRules.RecordArmorHit(ref score, sourceTeam: "blue", damage: GameConstants.BulletDamage);
            MatchRules.RecordArmorHit(ref score, sourceTeam: "red", damage: GameConstants.BulletDamage);

            Assert.AreEqual(GameConstants.BulletDamage * 2, score.BlueDamageDealt);
            Assert.AreEqual(GameConstants.BulletDamage, score.RedDamageDealt);
        }

        [Test]
        public void ToPlayerOutcome_MapsBlueTeamPerspectiveToEpisodeStatsOutcome()
        {
            Assert.AreEqual("OUTCOME_WIN", MatchRules.ToEpisodeOutcome(MatchOutcome.BlueWin, playerTeam: "blue"));
            Assert.AreEqual("OUTCOME_LOSS", MatchRules.ToEpisodeOutcome(MatchOutcome.RedWin, playerTeam: "blue"));
            Assert.AreEqual("OUTCOME_DRAW", MatchRules.ToEpisodeOutcome(MatchOutcome.Draw, playerTeam: "blue"));
            Assert.AreEqual("OUTCOME_TIMEOUT", MatchRules.ToEpisodeOutcome(MatchOutcome.InProgress, playerTeam: "blue"));
        }

        [Test]
        public void RespawnIsReadyAfterTenSeconds()
        {
            Assert.IsFalse(MatchRules.IsRespawnReady(9.99f));
            Assert.IsTrue(MatchRules.IsRespawnReady(10f));
        }

        [Test]
        public void DetermineBoostHolder_AwardsPointToOnlyTeamInRadius()
        {
            Vector3 point = new Vector3(2f, 0f, 2f);
            var red = new[] { new Vector3(2.5f, 0f, 2f) };
            var blue = new[] { new Vector3(-2f, 0f, -2f) };

            BoostPointHolder holder = MatchRules.DetermineBoostHolder(
                point,
                red,
                blue,
                holdRadius: 1f);

            Assert.AreEqual(BoostPointHolder.Red, holder);
        }

        [Test]
        public void DetermineBoostHolder_ContestedWhenBothTeamsInRadius()
        {
            Vector3 point = Vector3.zero;
            var red = new[] { new Vector3(0.5f, 0f, 0f) };
            var blue = new[] { new Vector3(-0.5f, 0f, 0f) };

            BoostPointHolder holder = MatchRules.DetermineBoostHolder(
                point,
                red,
                blue,
                holdRadius: 1f);

            Assert.AreEqual(BoostPointHolder.Contested, holder);
        }

        [Test]
        public void CountHeldBoostPoints_IgnoresContestedPoints()
        {
            var holders = new[]
            {
                BoostPointHolder.Red,
                BoostPointHolder.Blue,
                BoostPointHolder.Contested,
                BoostPointHolder.Unheld,
                BoostPointHolder.Red,
            };

            MatchRules.CountHeldBoostPoints(holders, out int redHeld, out int blueHeld);

            Assert.AreEqual(2, redHeld);
            Assert.AreEqual(1, blueHeld);
        }

        [Test]
        public void GenerateBoostPoint_StaysInsideArenaBounds()
        {
            Vector3 point = MatchRules.GenerateBoostPoint(
                random01X: 0.25f,
                random01Z: 0.75f,
                min: new Vector3(-10f, 0f, -8f),
                max: new Vector3(10f, 0f, 8f));

            Assert.AreEqual(-5f, point.x, 1e-6f);
            Assert.AreEqual(4f, point.z, 1e-6f);
        }

        [Test]
        public void HitRate_ReturnsHitsDividedByShotsAndZeroWhenNoShots()
        {
            Assert.AreEqual(0.4f, MatchRules.HitRate(2, 5), 1e-6f);
            Assert.AreEqual(0f, MatchRules.HitRate(2, 0), 1e-6f);
        }

        [Test]
        public void MatchRuntimeState_RecordsPlayerShotsDamageAndHitRates()
        {
            var state = new MatchRuntimeState();

            state.RecordPlayerProjectileFired();
            state.RecordPlayerProjectileFired();
            state.RecordArmorHit(sourceTeam: "blue", playerTeam: "blue", damage: GameConstants.BulletDamage);
            state.RecordArmorHit(sourceTeam: "red", playerTeam: "blue", damage: GameConstants.BulletDamage);

            Assert.AreEqual(2, state.PlayerProjectilesFired);
            Assert.AreEqual(2, state.TeamProjectilesFired);
            Assert.AreEqual(2, state.TotalArmorHits);
            Assert.AreEqual(1, state.PlayerArmorHits);
            Assert.AreEqual(1, state.TeamArmorHits);
            Assert.AreEqual(GameConstants.BulletDamage, state.DamageDealtByTeam("blue"));
            Assert.AreEqual(GameConstants.BulletDamage, state.DamageDealtByTeam("red"));
            Assert.AreEqual(0.5f, state.PlayerHitRate, 1e-6f);
            Assert.AreEqual(0.5f, state.TeamHitRate, 1e-6f);
        }

        [Test]
        public void MatchRuntimeState_BeginRuleTickDoesNotRollClockBackward()
        {
            var state = new MatchRuntimeState();

            Assert.AreEqual(0.25f, state.BeginRuleTick(0.25f), 1e-6f);
            Assert.AreEqual(0f, state.BeginRuleTick(0.10f), 1e-6f);
            Assert.AreEqual(0.25f, state.BeginRuleTick(0.50f), 1e-6f);
        }

        [Test]
        public void MatchRuntimeState_ResetClearsTelemetryAndScore()
        {
            var state = new MatchRuntimeState();
            state.RecordPlayerProjectileFired();
            state.RecordArmorHit(sourceTeam: "blue", playerTeam: "blue", damage: GameConstants.BulletDamage);
            state.ApplyBoostScoring(redHeldPoints: 1, blueHeldPoints: 1, seconds: 3f);
            state.BeginRuleTick(2f);

            state.Reset();

            Assert.AreEqual(0, state.PlayerProjectilesFired);
            Assert.AreEqual(0, state.TotalArmorHits);
            Assert.AreEqual(0f, state.Score.RedBoostScore, 1e-6f);
            Assert.AreEqual(0f, state.Score.BlueBoostScore, 1e-6f);
            Assert.AreEqual(0f, state.LastRuleTickSeconds, 1e-6f);
        }

        [Test]
        public void MatchWorldState_ResetGeneratesTwoBoostPointsAndClearsHolders()
        {
            var world = new MatchWorldState();

            world.ResetBoostPoints(random01X: () => 0.25f, random01Z: () => 0.75f);

            Assert.AreEqual(GameConstants.BoostPointCount, world.BoostPointCount);
            for (int i = 0; i < world.BoostPointCount; i++)
            {
                Assert.AreEqual(BoostPointHolder.Unheld, world.BoostPointHolders[i]);
                Assert.AreEqual(-4f, world.BoostPoints[i].x, 1e-6f);
                Assert.AreEqual(3f, world.BoostPoints[i].z, 1e-6f);
            }
        }

        [Test]
        public void MatchWorldState_UpdateBoostHoldersIgnoresDestroyedChassis()
        {
            var world = new MatchWorldState();
            world.SetBoostPointForTest(0, Vector3.zero);
            world.SetBoostPointForTest(1, new Vector3(10f, 0f, 10f));
            var red = new[] { new TeamPosition("red", Vector3.zero, active: true) };
            var destroyedBlue = new[] { new TeamPosition("blue", Vector3.zero, active: false) };

            world.UpdateBoostHolders(red, destroyedBlue, holdRadius: 1f);

            Assert.AreEqual(BoostPointHolder.Red, world.BoostPointHolders[0]);
            world.CountHeldBoostPoints(out int redHeld, out int blueHeld);
            Assert.AreEqual(1, redHeld);
            Assert.AreEqual(0, blueHeld);
        }

        [Test]
        public void MatchWorldState_TracksRespawnAtDeathPositionAfterDelay()
        {
            var world = new MatchWorldState();
            var deathPosition = new Vector3(1f, 0f, 2f);

            Assert.IsTrue(world.RegisterDeath("blue", deathPosition, yaw: 0.75f, elapsedSeconds: 3f));
            Assert.IsFalse(world.RegisterDeath("blue", Vector3.zero, yaw: 0f, elapsedSeconds: 4f));
            Assert.IsFalse(world.TryConsumeReadyRespawn("blue", elapsedSeconds: 12.99f, out RespawnPoint _));

            Assert.IsTrue(world.TryConsumeReadyRespawn("blue", elapsedSeconds: 13f, out RespawnPoint respawn));
            Assert.AreEqual(deathPosition, respawn.Position);
            Assert.AreEqual(0.75f, respawn.Yaw, 1e-6f);
            Assert.IsFalse(world.TryConsumeReadyRespawn("blue", elapsedSeconds: 30f, out RespawnPoint _));
        }

        [Test]
        public void FireHeatState_LocksAfterContinuousSchemaFire()
        {
            var heat = new FireHeatState();

            for (int i = 0; i < GameConstants.FireHeatLockShotCount; i++)
            {
                heat.RecordShot();
            }

            Assert.IsTrue(heat.IsLocked);
            Assert.IsFalse(heat.CanFire);
        }

        [Test]
        public void FireHeatState_RemainsLockedUntilHeatDropsToSafeThreshold()
        {
            var heat = new FireHeatState();
            for (int i = 0; i < GameConstants.FireHeatLockShotCount; i++)
            {
                heat.RecordShot();
            }

            float secondsAboveSafeThreshold =
                (GameConstants.FireHeatLockThreshold - GameConstants.FireHeatSafeThreshold)
                / GameConstants.FireHeatCooldownPerSecond
                * 0.5f;
            heat.Cool(secondsAboveSafeThreshold);

            Assert.IsTrue(heat.IsLocked);
            Assert.Greater(heat.Heat, GameConstants.FireHeatSafeThreshold);
        }

        [Test]
        public void FireHeatState_UnlocksAfterCoolingToSafeThreshold()
        {
            var heat = new FireHeatState();
            for (int i = 0; i < GameConstants.FireHeatLockShotCount; i++)
            {
                heat.RecordShot();
            }

            float secondsToSafeThreshold =
                (GameConstants.FireHeatLockThreshold - GameConstants.FireHeatSafeThreshold)
                / GameConstants.FireHeatCooldownPerSecond;
            heat.Cool(secondsToSafeThreshold);

            Assert.IsFalse(heat.IsLocked);
            Assert.IsTrue(heat.CanFire);
            Assert.AreEqual(GameConstants.FireHeatSafeThreshold, heat.Heat, 1e-5f);
        }

        [Test]
        public void FireHeatState_ResetClearsHeatAndLock()
        {
            var heat = new FireHeatState();
            for (int i = 0; i < GameConstants.FireHeatLockShotCount; i++)
            {
                heat.RecordShot();
            }

            heat.Reset();

            Assert.AreEqual(0f, heat.Heat, 1e-6f);
            Assert.IsFalse(heat.IsLocked);
            Assert.IsTrue(heat.CanFire);
        }

        [Test]
        public void MatchRuleRuntime_ResetClearsRuntimeWorldAndFireHeatState()
        {
            var runtime = new MatchRuleRuntime();
            runtime.RecordPlayerProjectileFired();
            for (int i = 0; i < GameConstants.FireHeatLockShotCount; i++)
            {
                runtime.RecordShotHeat();
            }
            Assert.IsFalse(runtime.CanFire);

            runtime.Reset(random01X: () => 0.25f, random01Z: () => 0.75f);

            Assert.AreEqual(0, runtime.State.PlayerProjectilesFired);
            Assert.AreEqual(0f, runtime.State.Score.RedBoostScore, 1e-6f);
            Assert.IsTrue(runtime.CanFire);
            Assert.AreEqual(GameConstants.BoostPointCount, runtime.World.BoostPointCount);
            Assert.AreEqual(BoostPointHolder.Unheld, runtime.World.BoostPointHolders[0]);
        }
    }
}
