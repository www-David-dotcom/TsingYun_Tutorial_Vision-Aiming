using NUnit.Framework;
using UnityEngine;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class BaselineAimSolverTests
    {
        [Test]
        public void Solve_AimsAtTargetCenterInYaw()
        {
            BaselineAimCommand cmd = BaselineAimSolver.Solve(
                shooterPosition: Vector3.zero,
                targetCenterWorld: new Vector3(3f, 0f, 3f));

            Assert.AreEqual(Mathf.PI / 4f, cmd.TargetYawRad, 1e-5f);
            Assert.AreEqual(0f, cmd.TargetPitchRad, 1e-5f);
            Assert.IsTrue(cmd.TargetVisible);
        }

        [Test]
        public void Solve_ComputesPositivePitchForHigherTarget()
        {
            BaselineAimCommand cmd = BaselineAimSolver.Solve(
                shooterPosition: Vector3.zero,
                targetCenterWorld: new Vector3(0f, 2f, 4f));

            Assert.AreEqual(0f, cmd.TargetYawRad, 1e-5f);
            Assert.Greater(cmd.TargetPitchRad, 0f);
            Assert.LessOrEqual(cmd.TargetPitchRad, GameConstants.GimbalPitchMaxRadians);
        }

        [Test]
        public void Solve_ClampsPitchToGimbalLimits()
        {
            BaselineAimCommand cmd = BaselineAimSolver.Solve(
                shooterPosition: Vector3.zero,
                targetCenterWorld: new Vector3(0f, 100f, 1f));

            Assert.AreEqual(GameConstants.GimbalPitchMaxRadians, cmd.TargetPitchRad, 1e-5f);
        }

        [Test]
        public void Solve_ReportsInvisibleWhenTargetOverlapsShooter()
        {
            BaselineAimCommand cmd = BaselineAimSolver.Solve(Vector3.zero, Vector3.zero);

            Assert.IsFalse(cmd.TargetVisible);
            Assert.AreEqual(0f, cmd.TargetYawRad, 1e-6f);
            Assert.AreEqual(0f, cmd.TargetPitchRad, 1e-6f);
        }
    }
}
