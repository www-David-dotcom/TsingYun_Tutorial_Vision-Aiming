using UnityEngine;

namespace TsingYun.UnityArena
{
    public class TrainingTargetController : MonoBehaviour
    {
        public Chassis TargetChassis;
        public float TargetTranslationSpeedMps = 1f;
        public float TargetRotationSpeedRadPerSecond = 1f;
        public float TargetPathHalfExtentMeters = 2f;

        public TrainingTargetSample LatestSample { get; private set; }

        private TrainingTargetMotion _motion;

        public void Configure(
            Chassis targetChassis,
            float translationSpeedMps,
            float rotationSpeedRadPerSecond,
            float pathHalfExtentMeters)
        {
            TargetChassis = targetChassis;
            TargetTranslationSpeedMps = Mathf.Max(0f, translationSpeedMps);
            TargetRotationSpeedRadPerSecond = Mathf.Max(0f, rotationSpeedRadPerSecond);
            TargetPathHalfExtentMeters = Mathf.Max(0f, pathHalfExtentMeters);
            ResetMotion();
        }

        public void ResetMotion()
        {
            if (TargetChassis == null) return;
            Vector3 origin = TargetChassis.transform.position;
            _motion = new TrainingTargetMotion(
                origin,
                TargetPathHalfExtentMeters,
                TargetTranslationSpeedMps,
                TargetRotationSpeedRadPerSecond);
            LatestSample = new TrainingTargetSample(origin, Vector3.zero, TargetChassis.ChassisYaw, 0f);
        }

        private void FixedUpdate()
        {
            if (TargetChassis == null) return;
            if (_motion == null) ResetMotion();

            LatestSample = _motion.Step(Time.fixedDeltaTime);
            Transform targetTransform = TargetChassis.transform;
            targetTransform.position = LatestSample.Position;
            targetTransform.rotation = Quaternion.Euler(0f, LatestSample.YawRad * Mathf.Rad2Deg, 0f);
        }
    }
}
