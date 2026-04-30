using UnityEngine;

namespace TsingYun.UnityArena
{
    // Pure-C# mecanum velocity solver. Extracted from chassis.gd:_physics_process
    // so Bullet→PhysX drift cannot affect it. Math is line-by-line equivalent:
    //
    //   v_x_world = vx_body * cos(yaw) - vy_body * sin(yaw)
    //   v_z_world = vx_body * sin(yaw) + vy_body * cos(yaw)
    //
    // The four-wheel mecanum mixing is NOT simulated — Godot's CharacterBody3D
    // doesn't model wheel slip and the RM rules don't punish ideal-kinematics
    // simulators in a way that matters for HW1–HW7.
    public class MecanumChassisController
    {
        public float MaxLinearSpeed { get; set; } = 3.5f;
        public float MaxAngularSpeed { get; set; } = 4.0f;

        public float ChassisYaw;
        public float CmdVx { get; private set; }
        public float CmdVy { get; private set; }
        public float CmdOmega { get; private set; }
        public Vector3 WorldVelocity { get; private set; }

        public void SetCmd(float vxBody, float vyBody, float omega)
        {
            CmdVx = Mathf.Clamp(vxBody, -MaxLinearSpeed, MaxLinearSpeed);
            CmdVy = Mathf.Clamp(vyBody, -MaxLinearSpeed, MaxLinearSpeed);
            CmdOmega = Mathf.Clamp(omega, -MaxAngularSpeed, MaxAngularSpeed);
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
    }
}
