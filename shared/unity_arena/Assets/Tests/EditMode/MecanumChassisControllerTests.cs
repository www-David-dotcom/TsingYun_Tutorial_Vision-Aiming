using NUnit.Framework;
using UnityEngine;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class MecanumChassisControllerTests
    {
        [Test]
        public void IntegrateYaw_AdvancesByOmegaTimesDelta()
        {
            var c = new MecanumChassisController { ChassisYaw = 1.0f };
            c.SetCmd(0f, 0f, 2.0f);
            c.IntegrateStep(0.5f);
            Assert.AreEqual(2.0f, c.ChassisYaw, 1e-6f);
        }

        [Test]
        public void IntegrateVelocity_NoYaw_BodyXMapsToWorldX()
        {
            var c = new MecanumChassisController { ChassisYaw = 0f };
            c.SetCmd(2.0f, 0f, 0f);
            c.IntegrateStep(0f);
            Assert.AreEqual(2.0f, c.WorldVelocity.x, 1e-6f);
            Assert.AreEqual(0f, c.WorldVelocity.z, 1e-6f);
        }

        [Test]
        public void IntegrateVelocity_NoYaw_BodyYMapsToWorldZ()
        {
            var c = new MecanumChassisController { ChassisYaw = 0f };
            c.SetCmd(0f, 1.5f, 0f);
            c.IntegrateStep(0f);
            Assert.AreEqual(0f, c.WorldVelocity.x, 1e-6f);
            Assert.AreEqual(1.5f, c.WorldVelocity.z, 1e-6f);
        }

        [Test]
        public void IntegrateVelocity_With90DegYaw_BodyXMapsToNegativeWorldZ()
        {
            // Yaw rotation around +Y: v_x_world = vx*cos(y) - vy*sin(y)
            //                                            v_z_world = vx*sin(y) + vy*cos(y)
            // With yaw = +π/2: cos = 0, sin = 1 → world.x = -vy, world.z = +vx
            var c = new MecanumChassisController { ChassisYaw = Mathf.PI / 2f };
            c.SetCmd(1.0f, 0f, 0f);
            c.IntegrateStep(0f);
            Assert.AreEqual(0f, c.WorldVelocity.x, 1e-6f);
            Assert.AreEqual(1.0f, c.WorldVelocity.z, 1e-6f);
        }

        [Test]
        public void SetCmd_ClampsToMaxLinearSpeed()
        {
            var c = new MecanumChassisController { MaxLinearSpeed = GameConstants.ChassisMaxLinearSpeed };
            c.SetCmd(10f, -10f, 0f);
            c.IntegrateStep(0f);

            Assert.AreEqual(GameConstants.ChassisMaxLinearSpeed, c.WorldVelocity.magnitude, 1e-6f);
        }

        [Test]
        public void SetCmd_ClampsToMaxAngularSpeed()
        {
            var c = new MecanumChassisController { MaxAngularSpeed = GameConstants.ChassisMaxAngularSpeed };
            c.SetCmd(0f, 0f, 99f);
            Assert.AreEqual(GameConstants.ChassisMaxAngularSpeed, c.CmdOmega, 1e-6f);
        }

        [Test]
        public void SetCmd_FullSelfRotationReducesMaximumTranslationSpeed()
        {
            var c = new MecanumChassisController
            {
                MaxLinearSpeed = GameConstants.ChassisMaxLinearSpeed,
                MaxAngularSpeed = GameConstants.ChassisMaxAngularSpeed,
            };

            c.SetCmd(
                GameConstants.ChassisMaxLinearSpeed,
                0f,
                GameConstants.ChassisMaxAngularSpeed);
            c.IntegrateStep(0f);

            Assert.AreEqual(
                GameConstants.ChassisMaxLinearSpeed * GameConstants.ChassisFullRotationLinearSpeedScale,
                c.WorldVelocity.magnitude,
                1e-6f);
        }

        [Test]
        public void BodyLocalYaw_ReturnsYawRelativeToFixedGimbalMount()
        {
            float mountYaw = 0.25f;
            float chassisYaw = 0.75f;

            float localYaw = MecanumChassisController.BodyLocalYaw(mountYaw, chassisYaw);

            Assert.AreEqual(0.5f, localYaw, 1e-6f);
        }
    }
}
