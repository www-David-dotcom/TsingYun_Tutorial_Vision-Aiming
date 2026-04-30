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
            // chassis.gd: velocity.z = cmd_vx*sin(yaw) + cmd_vy*cos(yaw); yaw=0 => z = cmd_vy
            Assert.AreEqual(1.5f, c.WorldVelocity.z, 1e-6f);
        }

        [Test]
        public void IntegrateVelocity_With90DegYaw_BodyXMapsToNegativeWorldZ()
        {
            // chassis.gd uses yaw rotation around +Y as: v_x_world = vx*cos(y) - vy*sin(y)
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
            var c = new MecanumChassisController { MaxLinearSpeed = 3.5f };
            c.SetCmd(10f, -10f, 0f);
            // Internal cmd values are clamped before the solver runs.
            Assert.AreEqual(3.5f, c.CmdVx, 1e-6f);
            Assert.AreEqual(-3.5f, c.CmdVy, 1e-6f);
        }

        [Test]
        public void SetCmd_ClampsToMaxAngularSpeed()
        {
            var c = new MecanumChassisController { MaxAngularSpeed = 4.0f };
            c.SetCmd(0f, 0f, 99f);
            Assert.AreEqual(4.0f, c.CmdOmega, 1e-6f);
        }
    }
}
