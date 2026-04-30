using NUnit.Framework;
using UnityEngine;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class ProjectileDragTests
    {
        [Test]
        public void DragForce_ZeroVelocity_ReturnsZero()
        {
            Vector3 force = ProjectileDragSolver.QuadraticDragForce(Vector3.zero);
            Assert.AreEqual(Vector3.zero, force);
        }

        [Test]
        public void DragForce_OpposesVelocity()
        {
            Vector3 v = new Vector3(10f, 0f, 0f);
            Vector3 force = ProjectileDragSolver.QuadraticDragForce(v);
            Assert.Less(force.x, 0f);
            Assert.AreEqual(0f, force.y, 1e-6f);
            Assert.AreEqual(0f, force.z, 1e-6f);
        }

        [Test]
        public void DragForce_MagnitudeMatchesFormula()
        {
            // F = 0.5 * rho * Cd * A * |v|^2  (along -v)
            // rho = 1.225, Cd = 0.47, A = 0.000227 (matches projectile.gd)
            float speed = 27.0f;  // muzzle velocity
            Vector3 v = new Vector3(speed, 0f, 0f);
            Vector3 force = ProjectileDragSolver.QuadraticDragForce(v);
            float expected = 0.5f * 1.225f * 0.47f * 0.000227f * speed * speed;
            Assert.AreEqual(expected, Mathf.Abs(force.x), 1e-6f);
        }
    }
}
