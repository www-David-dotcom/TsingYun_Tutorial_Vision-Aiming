using UnityEngine;

namespace TsingYun.UnityArena
{
    // Pure-C# quadratic-drag force solver for the projectile. Extracted so the
    // math is EditMode-testable.
    public static class ProjectileDragSolver
    {
        public const float DragCoefficient = 0.47f;       // sphere
        public const float AirDensity = 1.225f;            // kg/m^3
        public const float FrontalArea = 0.000227f;        // π * 0.0085^2
        public const float MaxRangeM = 30.0f;
        public const float MaxTtlSeconds = 4.0f;
        public const int Damage = GameConstants.BulletDamage;

        public static Vector3 QuadraticDragForce(Vector3 velocity)
        {
            float speed = velocity.magnitude;
            if (speed < 1e-3f) return Vector3.zero;
            float magnitude = 0.5f * AirDensity * DragCoefficient * FrontalArea * speed * speed;
            return -velocity.normalized * magnitude;
        }
    }

    // 17 mm-style ball projectile. Quadratic drag is applied per FixedUpdate
    // (Rigidbody.linearDamping is exponential decay, which is wrong for a real
    // projectile). Gravity comes from the engine. Lifetime caps: MaxRangeM
    // (30 m) and MaxTtlSeconds (4 s).
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        public string Team = "blue";
        public bool Consumed { get; private set; }

        private Rigidbody _rb;
        private Vector3 _spawnPosition;
        private float _spawnTimeSeconds;

        private void Awake() { _rb = GetComponent<Rigidbody>(); }

        public void Arm(Vector3 initialVelocity, string owningTeam)
        {
            _spawnPosition = transform.position;
            _spawnTimeSeconds = Time.time;
            Team = owningTeam;
            _rb.linearVelocity = initialVelocity;
        }

        private void FixedUpdate()
        {
            if (Consumed) return;
            Vector3 dragForce = ProjectileDragSolver.QuadraticDragForce(_rb.linearVelocity);
            _rb.AddForce(dragForce, ForceMode.Force);

            if ((transform.position - _spawnPosition).magnitude > ProjectileDragSolver.MaxRangeM)
                Consume("miss_range");
            else if (Time.time - _spawnTimeSeconds > ProjectileDragSolver.MaxTtlSeconds)
                Consume("miss_range");
        }

        public int OnArmorHit(ArmorPlate plate)
        {
            if (Consumed) return 0;
            Consume($"hit_armor:{plate.PlateId}");
            return ProjectileDragSolver.Damage;
        }

        private void OnCollisionEnter(Collision other)
        {
            if (Consumed) return;
            // Plates are triggers, not colliders, so this only fires on walls/floor.
            Consume("hit_wall");
        }

        private void Consume(string reason)
        {
            Consumed = true;
            Destroy(gameObject);
        }
    }
}
