namespace TsingYun.UnityArena
{
    public static class GameConstants
    {
        public const float BulletSpeedMetersPerSecond = 20f;
        public const int BulletDamage = 20;

        public const float GimbalPitchMinDegrees = -25f;
        public const float GimbalPitchMaxDegrees = 25f;
        public const float DegreesToRadians = 0.017453292519943295f;
        public const float GimbalPitchMinRadians = GimbalPitchMinDegrees * DegreesToRadians;
        public const float GimbalPitchMaxRadians = GimbalPitchMaxDegrees * DegreesToRadians;
        public const float FireRateRoundsPerSecond = 5f;
        public const float FireIntervalSeconds = 1f / FireRateRoundsPerSecond;
        public const float FireHeatPerShot = 1f;
        public const int FireHeatLockShotCount = 10;
        public const float FireHeatLockThreshold = FireHeatPerShot * FireHeatLockShotCount;
        public const float FireHeatSafeThreshold = 4f;
        public const float FireHeatCooldownPerSecond = 2f;

        public const float ChassisMaxLinearSpeed = 3.5f;
        public const float ChassisMaxAngularSpeed = 4f;
        public const float ChassisFullRotationLinearSpeedScale = 0.5f;

        public const int MatchDurationSeconds = 5 * 60;
        public const long MatchDurationNanoseconds = 300_000_000_000L;
        public const int VehicleHpOneVsOne = 300;
        public const int VehicleHpTwoVsTwo = 500;
        public const int VehicleHpThreeVsThree = 700;
        public const float HealingHpPerSecond = 10f;
        public const float HealingZoneRadiusMeters = 2.5f;
        public const float BoostScorePointsPerSecond = 3f;
        public const int BoostScoreWinThreshold = 200;
        public const int BoostPointCount = 2;
        public const float BoostPointHoldRadiusMeters = 2.0f;
        public const float BoostPointArenaMinX = -8f;
        public const float BoostPointArenaMaxX = 8f;
        public const float BoostPointArenaMinZ = -6f;
        public const float BoostPointArenaMaxZ = 6f;
        public const float RespawnDelaySeconds = 10f;

        public static int VehicleHpForTeamSize(int vehiclesPerTeam)
        {
            switch (vehiclesPerTeam)
            {
                case 1: return VehicleHpOneVsOne;
                case 2: return VehicleHpTwoVsTwo;
                case 3: return VehicleHpThreeVsThree;
                default: return VehicleHpOneVsOne;
            }
        }
    }
}
