using UnityEngine;

namespace TsingYun.UnityArena
{
    // Pure-C# mecanum velocity solver. Keeps chassis motion out of PhysX drift.
    // Math:
    //
    //   v_x_world = vx_body * cos(yaw) - vy_body * sin(yaw)
    //   v_z_world = vx_body * sin(yaw) + vy_body * cos(yaw)
    //
    // The four-wheel mecanum mixing is not simulated; this remains an
    // ideal-kinematics controller for the legacy HW reference modules.
    public class MecanumChassisController
    {
        public float MaxLinearSpeed { get; set; } = GameConstants.ChassisMaxLinearSpeed;
        public float MaxAngularSpeed { get; set; } = GameConstants.ChassisMaxAngularSpeed;

        public float ChassisYaw;
        public float CmdVx { get; private set; }
        public float CmdVy { get; private set; }
        public float CmdOmega { get; private set; }
        public Vector3 WorldVelocity { get; private set; }

        public void SetCmd(float vxBody, float vyBody, float omega)
        {
            CmdOmega = Mathf.Clamp(omega, -MaxAngularSpeed, MaxAngularSpeed);
            float rotationLoad = MaxAngularSpeed > 0f ? Mathf.Abs(CmdOmega) / MaxAngularSpeed : 0f;
            float linearScale = Mathf.Lerp(
                1f,
                GameConstants.ChassisFullRotationLinearSpeedScale,
                Mathf.Clamp01(rotationLoad));
            float cappedLinearSpeed = MaxLinearSpeed * linearScale;
            Vector2 translation = Vector2.ClampMagnitude(
                new Vector2(vxBody, vyBody),
                cappedLinearSpeed);
            CmdVx = translation.x;
            CmdVy = translation.y;
        }

        // Advance the chassis state by deltaSeconds: integrate yaw, recompute
        // world-space velocity. Caller is responsible for moving the transform
        // (Unity does not provide a CharacterBody3D-equivalent at this layer;
        // callers use a custom CharacterController.Move on FixedUpdate).
        public void IntegrateStep(float deltaSeconds)
        {
            ChassisYaw += CmdOmega * deltaSeconds;
            float cosY = Mathf.Cos(ChassisYaw);
            float sinY = Mathf.Sin(ChassisYaw);
            WorldVelocity = new Vector3(
                CmdVx * cosY - CmdVy * sinY,
                0f,
                CmdVx * sinY + CmdVy * cosY
            );
        }

        public void Reset(float spawnYaw)
        {
            ChassisYaw = spawnYaw;
            CmdVx = 0f;
            CmdVy = 0f;
            CmdOmega = 0f;
            WorldVelocity = Vector3.zero;
        }

        public static float BodyLocalYaw(float fixedMountYaw, float chassisYaw)
        {
            return chassisYaw - fixedMountYaw;
        }
    }
}
