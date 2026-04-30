using NUnit.Framework;
using UnityEngine;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class GimbalKinematicsTests
    {
        [Test]
        public void IntegrateStep_ConvergesToTargetYaw()
        {
            var k = new GimbalKinematics();
            k.SetTarget(targetYaw: 0.3f, targetPitch: 0.0f, yawFf: 0f, pitchFf: 0f);
            for (int i = 0; i < 100; i++)
            {
                k.IntegrateStep(0.01f);
            }
            Assert.AreEqual(0.3f, k.YawRad, 1e-3f);
        }

        [Test]
        public void IntegrateStep_ClampsPitchToLimits()
        {
            var k = new GimbalKinematics();
            // PITCH_LIMIT_HI = 0.52 rad. Command 1.0 → should clamp.
            k.SetTarget(targetYaw: 0f, targetPitch: 1.0f, yawFf: 0f, pitchFf: 0f);
            for (int i = 0; i < 200; i++)
            {
                k.IntegrateStep(0.01f);
            }
            Assert.LessOrEqual(k.PitchRad, GimbalKinematics.PitchLimitHi + 1e-6f);
        }

        [Test]
        public void IntegrateStep_RateLimitsYaw()
        {
            var k = new GimbalKinematics();
            k.SetTarget(targetYaw: 100f, targetPitch: 0f, yawFf: 0f, pitchFf: 0f);
            k.IntegrateStep(0.01f);
            // YAW_RATE_LIMIT = 12 rad/s, so over 0.01s yaw advances ≤ 0.12.
            Assert.LessOrEqual(Mathf.Abs(k.YawRad), 0.12f + 1e-6f);
        }

        [Test]
        public void SetTarget_ClampsTargetPitchOnInput()
        {
            var k = new GimbalKinematics();
            k.SetTarget(targetYaw: 0f, targetPitch: 99f, yawFf: 0f, pitchFf: 0f);
            // Internal target is clamped before the solver runs (mirrors gimbal.gd).
            Assert.AreEqual(GimbalKinematics.PitchLimitHi, k.TargetPitch, 1e-6f);
        }

        [Test]
        public void GetState_ReturnsCurrentValues()
        {
            var k = new GimbalKinematics();
            k.SetTarget(targetYaw: 0.1f, targetPitch: 0f, yawFf: 0f, pitchFf: 0f);
            k.IntegrateStep(0.01f);
            var s = k.GetState();
            Assert.AreEqual(k.YawRad, s.Yaw, 1e-9f);
            Assert.AreEqual(k.PitchRad, s.Pitch, 1e-9f);
        }
    }
}
