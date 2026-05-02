using UnityEngine;

namespace TsingYun.UnityArena
{
    public class BaselineOpponentController : MonoBehaviour
    {
        public Chassis Shooter;
        public Chassis Target;
        public bool FireWhenAligned = true;
        public float AlignmentToleranceRad = 0.05f;
        public float BurstIntervalSeconds = 0.4f;

        private float _nextFireTime;

        public BaselineAimCommand LatestCommand { get; private set; }
        public bool WantsFire { get; private set; }

        public void Configure(Chassis shooter, Chassis target, bool fireWhenAligned)
        {
            Shooter = shooter;
            Target = target;
            FireWhenAligned = fireWhenAligned;
            _nextFireTime = 0f;
            WantsFire = false;
        }

        private void FixedUpdate()
        {
            WantsFire = false;
            if (Shooter == null || Target == null || Shooter.Gimbal == null) return;
            if (Shooter.IsDestroyed || Target.IsDestroyed) return;

            LatestCommand = BaselineAimSolver.Solve(
                Shooter.Gimbal.transform.position,
                Target.transform.position + Vector3.up * 0.6f);

            if (!LatestCommand.TargetVisible) return;

            Shooter.Gimbal.SetTarget(LatestCommand.TargetYawRad, LatestCommand.TargetPitchRad, 0f, 0f);
            if (!FireWhenAligned || Time.time < _nextFireTime) return;

            GimbalState state = Shooter.Gimbal.GetState();
            float yawError = Mathf.Abs(Mathf.DeltaAngle(
                state.Yaw * Mathf.Rad2Deg,
                LatestCommand.TargetYawRad * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            float pitchError = Mathf.Abs(state.Pitch - LatestCommand.TargetPitchRad);
            WantsFire = yawError <= AlignmentToleranceRad && pitchError <= AlignmentToleranceRad;
            if (WantsFire) _nextFireTime = Time.time + BurstIntervalSeconds;
        }
    }
}
