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

    // MonoBehaviour wrapping GimbalKinematics. Drives YawPivot.localRotation
    // and PitchPivot.localRotation in FixedUpdate. Mirrors gimbal.gd.
    public class Gimbal : MonoBehaviour
    {
        public const float MuzzleVelocity = 27.0f;

        public Transform YawPivot;
        public Transform PitchPivot;
        public Transform Muzzle;

        private GimbalKinematics _k = new GimbalKinematics();

        private void Awake()
        {
            if (YawPivot == null) YawPivot = transform.Find("YawPivot");
            if (PitchPivot == null) PitchPivot = YawPivot != null ? YawPivot.Find("PitchPivot") : null;
            if (Muzzle == null && PitchPivot != null) Muzzle = PitchPivot.Find("Muzzle");
        }

        public void SetTarget(float yaw, float pitch, float yawFf, float pitchFf)
            => _k.SetTarget(yaw, pitch, yawFf, pitchFf);

        public void Reset() => _k.Reset();

        public GimbalState GetState() => _k.GetState();

        private void FixedUpdate()
        {
            _k.IntegrateStep(Time.fixedDeltaTime);
            if (YawPivot != null) YawPivot.localRotation = Quaternion.Euler(0f, _k.YawRad * Mathf.Rad2Deg, 0f);
            if (PitchPivot != null) PitchPivot.localRotation = Quaternion.Euler(_k.PitchRad * Mathf.Rad2Deg, 0f, 0f);
        }

        public Matrix4x4 MuzzleWorldTransform()
            => Muzzle != null ? Muzzle.localToWorldMatrix : transform.localToWorldMatrix;

        public ShotSpec ComputeShot()
        {
            Matrix4x4 m = Muzzle != null ? Muzzle.localToWorldMatrix : transform.localToWorldMatrix;
            Vector3 fwd = -((Vector3)m.GetColumn(2)).normalized;
            float jitterYaw = SeedRng.NextRange(-0.002f, 0.002f);
            float jitterPitch = SeedRng.NextRange(-0.002f, 0.002f);
            fwd = Quaternion.AngleAxis(jitterYaw * Mathf.Rad2Deg, Vector3.up) * fwd;
            Vector3 right = ((Vector3)m.GetColumn(0)).normalized;
            fwd = Quaternion.AngleAxis(jitterPitch * Mathf.Rad2Deg, right) * fwd;
            return new ShotSpec
            {
                Position = m.GetColumn(3),
                Rotation = m.rotation,
                Velocity = fwd * MuzzleVelocity,
            };
        }
    }

    public struct ShotSpec
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
    }
}
