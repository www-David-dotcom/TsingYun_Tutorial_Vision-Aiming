using UnityEngine;

namespace TsingYun.UnityArena
{
    // Pure-C# quadratic-drag force solver for the projectile. Matches
    // projectile.gd:_physics_process (lines 35-40). Extracted so the math is
    // EditMode-testable; the full Projectile MonoBehaviour (with Rigidbody,
    // OnCollisionEnter, lifetime caps) is added in Stage 12b.
    public static class ProjectileDragSolver
    {
        public const float DragCoefficient = 0.47f;       // sphere
        public const float AirDensity = 1.225f;            // kg/m^3
        public const float FrontalArea = 0.000227f;        // π * 0.0085^2
        public const float MaxRangeM = 30.0f;
        public const float MaxTtlSeconds = 4.0f;
        public const int Damage = 50;

        public static Vector3 QuadraticDragForce(Vector3 velocity)
        {
            float speed = velocity.magnitude;
            if (speed < 1e-3f) return Vector3.zero;
            float magnitude = 0.5f * AirDensity * DragCoefficient * FrontalArea * speed * speed;
            return -velocity.normalized * magnitude;
        }
    }
}
