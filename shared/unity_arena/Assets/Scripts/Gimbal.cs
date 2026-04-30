using UnityEngine;

namespace TsingYun.UnityArena
{
    // Pure-C# gimbal kinematics: yaw around +Y, pitch around local +X of the
    // YawPivot. First-order motor lag rate-limits the slew so commanded targets
    // don't snap. Mirrors gimbal.gd. Extracted so the math is EditMode-testable
    // without a scene running.
    public class GimbalKinematics
    {
        public const float YawRateLimit = 12.0f;     // rad/s
        public const float PitchRateLimit = 8.0f;    // rad/s
        public const float PitchLimitLo = -0.35f;    // rad (~-20 deg)
        public const float PitchLimitHi = 0.52f;     // rad (~+30 deg)
        public const float MotorLagTc = 0.04f;       // s

        public float YawRad;
        public float PitchRad;
        public float TargetYaw;
        public float TargetPitch;
        public float YawRate;
        public float PitchRate;
        public float YawRateFf;
        public float PitchRateFf;

        public void SetTarget(float targetYaw, float targetPitch, float yawFf, float pitchFf)
        {
            TargetYaw = targetYaw;
            TargetPitch = Mathf.Clamp(targetPitch, PitchLimitLo, PitchLimitHi);
            YawRateFf = yawFf;
            PitchRateFf = pitchFf;
        }

        public void IntegrateStep(float deltaSeconds)
        {
            float yawErr = WrapPi(TargetYaw - YawRad);
            float pitchErr = TargetPitch - PitchRad;

            float yawCmd = yawErr / MotorLagTc + YawRateFf;
            float pitchCmd = pitchErr / MotorLagTc + PitchRateFf;

            YawRate = Mathf.Clamp(yawCmd, -YawRateLimit, YawRateLimit);
            PitchRate = Mathf.Clamp(pitchCmd, -PitchRateLimit, PitchRateLimit);

            YawRad += YawRate * deltaSeconds;
            PitchRad = Mathf.Clamp(PitchRad + PitchRate * deltaSeconds,
                                    PitchLimitLo, PitchLimitHi);
        }

        public GimbalState GetState() => new GimbalState
        {
            Yaw = YawRad,
            Pitch = PitchRad,
            YawRate = YawRate,
            PitchRate = PitchRate,
        };

        public void Reset()
        {
            YawRad = PitchRad = 0f;
            TargetYaw = TargetPitch = 0f;
            YawRate = PitchRate = 0f;
            YawRateFf = PitchRateFf = 0f;
        }

        private static float WrapPi(float angle)
        {
            // Match Godot's wrapf(x, -PI, PI).
            float twoPi = Mathf.PI * 2f;
            angle = (angle + Mathf.PI) % twoPi;
            if (angle < 0f) angle += twoPi;
            return angle - Mathf.PI;
        }
    }

    public struct GimbalState
    {
        public float Yaw;
        public float Pitch;
        public float YawRate;
        public float PitchRate;
    }
}
