using UnityEngine;

namespace TsingYun.UnityArena
{
    public readonly struct BaselineAimCommand
    {
        public readonly float TargetYawRad;
        public readonly float TargetPitchRad;
        public readonly bool TargetVisible;

        public BaselineAimCommand(float targetYawRad, float targetPitchRad, bool targetVisible)
        {
            TargetYawRad = targetYawRad;
            TargetPitchRad = targetPitchRad;
            TargetVisible = targetVisible;
        }
    }

    public static class BaselineAimSolver
    {
        public static BaselineAimCommand Solve(Vector3 shooterPosition, Vector3 targetCenterWorld)
        {
            Vector3 delta = targetCenterWorld - shooterPosition;
            Vector2 horizontal = new Vector2(delta.x, delta.z);
            float horizontalDistance = horizontal.magnitude;
            if (horizontalDistance < 1e-4f && Mathf.Abs(delta.y) < 1e-4f)
            {
                return new BaselineAimCommand(0f, 0f, false);
            }

            float yaw = Mathf.Atan2(delta.x, delta.z);
            float pitch = Mathf.Atan2(delta.y, Mathf.Max(horizontalDistance, 1e-4f));
            pitch = Mathf.Clamp(
                pitch,
                GameConstants.GimbalPitchMinRadians,
                GameConstants.GimbalPitchMaxRadians);

            return new BaselineAimCommand(yaw, pitch, true);
        }
    }
}
