using NUnit.Framework;
using UnityEngine;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class TrainingTargetMotionTests
    {
        [Test]
        public void Motion_AdvancesPositionAndYawDeterministically()
        {
            var motion = new TrainingTargetMotion(
                origin: new Vector3(3f, 0f, 0f),
                halfExtentMeters: 2f,
                translationSpeedMps: 1f,
                yawRateRadPerSecond: 2f);

            TrainingTargetSample first = motion.Step(0.5f);
            TrainingTargetSample second = motion.Step(0.5f);

            Assert.AreEqual(new Vector3(3.5f, 0f, 0f), first.Position);
            Assert.AreEqual(new Vector3(4.0f, 0f, 0f), second.Position);
            Assert.AreEqual(1.0f, first.YawRad, 1e-6f);
            Assert.AreEqual(2.0f, second.YawRad, 1e-6f);
            Assert.AreEqual(new Vector3(1f, 0f, 0f), second.VelocityWorld);
            Assert.AreEqual(2.0f, second.YawRateRadPerSecond, 1e-6f);
        }

        [Test]
        public void Motion_BouncesAtConfiguredExtent()
        {
            var motion = new TrainingTargetMotion(
                origin: Vector3.zero,
                halfExtentMeters: 1f,
                translationSpeedMps: 2f,
                yawRateRadPerSecond: 0f);

            TrainingTargetSample first = motion.Step(0.75f);
            TrainingTargetSample second = motion.Step(0.5f);

            Assert.AreEqual(new Vector3(0.5f, 0f, 0f), first.Position);
            Assert.AreEqual(new Vector3(-0.5f, 0f, 0f), second.Position);
            Assert.AreEqual(new Vector3(-2f, 0f, 0f), second.VelocityWorld);
        }

        [Test]
        public void Motion_ClampsNegativeSpeeds()
        {
            var motion = new TrainingTargetMotion(
                origin: Vector3.zero,
                halfExtentMeters: -5f,
                translationSpeedMps: -3f,
                yawRateRadPerSecond: -4f);

            TrainingTargetSample sample = motion.Step(1f);

            Assert.AreEqual(Vector3.zero, sample.Position);
            Assert.AreEqual(Vector3.zero, sample.VelocityWorld);
            Assert.AreEqual(0f, sample.YawRad, 1e-6f);
            Assert.AreEqual(0f, sample.YawRateRadPerSecond, 1e-6f);
        }
    }
}
