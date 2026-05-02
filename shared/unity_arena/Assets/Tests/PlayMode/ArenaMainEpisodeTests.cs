using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.PlayMode
{
    public class ArenaMainEpisodeTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>();
        private int _sceneSerial;
        private int _portSerial;

        [UnityTest]
        public IEnumerator EnvReset_ReturnsInitialStateWithSimSha()
        {
            var arena = CreateMinimalArena();
            yield return null;

            var resp = arena.EnvReset(new Dictionary<string, object>
            {
                { "seed", 42L }, { "opponent_tier", "bronze" },
                { "oracle_hints", true }, { "duration_ns", 5_000_000_000L },
            });

            Assert.AreEqual(ArenaMain.SimBuildSha, resp["simulator_build_sha256"]);
            Assert.IsTrue(((string)resp["zmq_frame_endpoint"]).Contains("tcp://127.0.0.1:"));
            Assert.IsTrue(resp.ContainsKey("bundle"));
        }

        [UnityTest]
        public IEnumerator EpisodeId_DeterministicPerSeed()
        {
            var arena = CreateMinimalArena();
            yield return null;

            arena.EnvReset(new Dictionary<string, object> { { "seed", 42L } });
            string id1 = arena.EpisodeId;
            arena.EnvFinish(new Dictionary<string, object>());

            arena.EnvReset(new Dictionary<string, object> { { "seed", 42L } });
            string id2 = arena.EpisodeId;

            Assert.AreEqual("ep-000000000000002a", id1);
            Assert.AreEqual(id1, id2);
        }

        [UnityTest]
        public IEnumerator EnvReset_CreatesVisibleRuleMarkers()
        {
            var arena = CreateMinimalArena();
            yield return null;

            arena.EnvReset(new Dictionary<string, object> { { "seed", 7L } });

            RuleZoneMarker[] markers = Object.FindObjectsByType<RuleZoneMarker>(FindObjectsSortMode.None);
            int boostCount = 0;
            int healingCount = 0;
            foreach (RuleZoneMarker marker in markers)
            {
                if (marker.Kind == RuleZoneKind.BoostPoint)
                {
                    boostCount++;
                    Assert.AreEqual(arena.BoostPointHoldRadius, marker.Radius, 1e-6f);
                }
                else if (marker.Kind == RuleZoneKind.HealingZone)
                {
                    healingCount++;
                    Assert.AreEqual(arena.HealingZoneRadius, marker.Radius, 1e-6f);
                }
            }

            Assert.AreEqual(2, boostCount);
            Assert.AreEqual(2, healingCount);
        }

        [UnityTest]
        public IEnumerator RuleZoneMarkerRenderer_CreatesAndUpdatesMarkers()
        {
            var rootObject = new GameObject("RuleMarkerRendererHost");
            _createdObjects.Add(rootObject);
            var renderer = rootObject.AddComponent<RuleZoneMarkerRenderer>();

            Vector3[] boostPoints =
            {
                new Vector3(1f, 0f, 2f),
                new Vector3(-1f, 0f, -2f),
            };
            BoostPointHolder[] holders =
            {
                BoostPointHolder.Blue,
                BoostPointHolder.Unheld,
            };

            renderer.RenderHealingZones(Vector3.left, Vector3.right, healingRadius: 2.5f);
            renderer.RenderBoostPoints(boostPoints, holders, boostRadius: 2f);
            yield return null;

            RuleZoneMarker[] markers = rootObject.GetComponentsInChildren<RuleZoneMarker>();
            Assert.AreEqual(4, markers.Length);
            Assert.IsNotNull(rootObject.transform.Find("RuleMarkers/HealingZone_Blue"));
            Assert.IsNotNull(rootObject.transform.Find("RuleMarkers/HealingZone_Red"));
            Assert.IsNotNull(rootObject.transform.Find("RuleMarkers/BoostPoint_1"));
            Assert.AreEqual(BoostPointHolder.Blue, rootObject.transform.Find("RuleMarkers/BoostPoint_1").GetComponent<RuleZoneMarker>().Holder);

            holders[0] = BoostPointHolder.Red;
            renderer.RenderBoostPoints(boostPoints, holders, boostRadius: 2f);

            Assert.AreEqual(BoostPointHolder.Red, rootObject.transform.Find("RuleMarkers/BoostPoint_1").GetComponent<RuleZoneMarker>().Holder);
        }

        [UnityTest]
        public IEnumerator DestroyedPlayerCannotQueueFire()
        {
            var arena = CreateMinimalArena();
            yield return null;

            arena.EnvReset(new Dictionary<string, object> { { "seed", 11L } });
            arena.BlueChassis.ApplyArmorHitDamage("front", arena.BlueChassis.MaxHp, "red");

            var resp = arena.EnvPushFire(new Dictionary<string, object> { { "burst_count", 1L } });

            Assert.IsFalse((bool)resp["accepted"]);
            Assert.AreEqual("destroyed", resp["reason"]);
        }

        [UnityTest]
        public IEnumerator QueuedShotsAreClearedWhenPlayerIsDestroyed()
        {
            var arena = CreateMinimalArena();
            arena.ProjectilePrefab = CreateProjectilePrefab();
            yield return null;

            arena.EnvReset(new Dictionary<string, object> { { "seed", 12L } });

            var fire = arena.EnvPushFire(new Dictionary<string, object> { { "burst_count", 3L } });
            Assert.IsTrue((bool)fire["accepted"]);

            arena.BlueChassis.ApplyArmorHitDamage("front", arena.BlueChassis.MaxHp, "red");
            yield return new WaitForSeconds(0.3f);

            Assert.AreEqual(0, arena.ProjectileRoot.childCount);
        }

        [UnityTest]
        public IEnumerator ContinuousFireLocksUntilHeatCoolsToSafeThreshold()
        {
            var arena = CreateMinimalArena();
            arena.ProjectilePrefab = CreateProjectilePrefab();
            yield return null;

            arena.EnvReset(new Dictionary<string, object> { { "seed", 15L } });

            var fire = arena.EnvPushFire(new Dictionary<string, object>
            {
                { "burst_count", (long)GameConstants.FireHeatLockShotCount },
            });
            Assert.IsTrue((bool)fire["accepted"]);

            for (int i = 0; i < GameConstants.FireHeatLockShotCount; i++)
            {
                InvokeDrainShotQueue(arena);
                FastForwardNextShotTime(arena);
            }

            var locked = arena.EnvPushFire(new Dictionary<string, object> { { "burst_count", 1L } });

            Assert.IsFalse((bool)locked["accepted"]);
            Assert.AreEqual("fire_locked", locked["reason"]);
            Assert.AreEqual(0, locked["queued_count"]);

            float coolSeconds =
                (GameConstants.FireHeatLockThreshold - GameConstants.FireHeatSafeThreshold)
                / GameConstants.FireHeatCooldownPerSecond
                + 0.1f;
            FastForwardArenaClock(arena, coolSeconds);
            arena.EnvStep(new Dictionary<string, object>());

            var cooled = arena.EnvPushFire(new Dictionary<string, object> { { "burst_count", 1L } });

            Assert.IsTrue((bool)cooled["accepted"]);
        }

        [UnityTest]
        public IEnumerator DestroyedPlayerGimbalHoldsPoseUntilRespawn()
        {
            var arena = CreateMinimalArena();
            yield return null;

            arena.EnvReset(new Dictionary<string, object> { { "seed", 13L } });
            arena.EnvStep(new Dictionary<string, object> { { "target_yaw", 1.0 }, { "target_pitch", 0.0 } });
            yield return new WaitForFixedUpdate();

            GimbalState beforeDeath = arena.BlueChassis.Gimbal.GetState();
            Assert.Greater(Mathf.Abs(beforeDeath.Yaw), 0f);

            arena.BlueChassis.ApplyArmorHitDamage("front", arena.BlueChassis.MaxHp, "red");
            float yawAtDeath = arena.BlueChassis.Gimbal.GetState().Yaw;

            arena.EnvStep(new Dictionary<string, object> { { "target_yaw", 2.0 }, { "target_pitch", 0.2 } });
            yield return new WaitForFixedUpdate();

            GimbalState afterDeath = arena.BlueChassis.Gimbal.GetState();
            Assert.AreEqual(yawAtDeath, afterDeath.Yaw, 1e-4f);
            Assert.AreEqual(0f, afterDeath.YawRate, 1e-4f);
            Assert.AreEqual(0f, afterDeath.PitchRate, 1e-4f);
        }

        [UnityTest]
        public IEnumerator DestroyedPlayerRespawnsAtDeathPositionAfterSchemaDelay()
        {
            var arena = CreateMinimalArena();
            yield return null;

            arena.EnvReset(new Dictionary<string, object> { { "seed", 14L } });
            Vector3 deathPosition = new Vector3(-1.25f, 0f, 2.5f);
            arena.BlueChassis.transform.position = deathPosition;
            arena.BlueChassis.ApplyArmorHitDamage("front", arena.BlueChassis.MaxHp, "red");

            Assert.IsTrue(arena.BlueChassis.IsDestroyed);

            FastForwardArenaClock(arena, GameConstants.RespawnDelaySeconds + 0.1f);
            arena.EnvStep(new Dictionary<string, object>());

            Assert.IsFalse(arena.BlueChassis.IsDestroyed);
            Assert.AreEqual(arena.BlueChassis.MaxHp, arena.BlueChassis.Hp);
            Assert.Less(Vector3.Distance(deathPosition, arena.BlueChassis.transform.position), 0.01f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject obj in _createdObjects)
            {
                if (obj != null) Object.Destroy(obj);
            }
            _createdObjects.Clear();
            yield return null;
        }

        private ArenaMain CreateMinimalArena()
        {
            SceneManager.CreateScene($"ArenaMainEpisodeTest_{_sceneSerial++}");

            Chassis blue = CreateChassis("BlueChassis", "blue", new Vector3(-3f, 0f, 0f));
            Chassis red = CreateChassis("RedChassis", "red", new Vector3(3f, 0f, 0f));
            Transform blueSpawn = CreateTransform("SpawnPoint_Blue", new Vector3(-3f, 0f, 0f), 45f);
            Transform redSpawn = CreateTransform("SpawnPoint_Red", new Vector3(3f, 0f, 0f), -135f);
            Transform projectileRoot = CreateTransform("ProjectileRoot", Vector3.zero, 0f);

            var cameraObject = new GameObject("GimbalCamera");
            _createdObjects.Add(cameraObject);
            Camera camera = cameraObject.AddComponent<Camera>();

            var arenaObject = new GameObject("ArenaMain");
            _createdObjects.Add(arenaObject);
            arenaObject.SetActive(false);
            var arena = arenaObject.AddComponent<ArenaMain>();
            int basePort = 17654 + (_portSerial++ * 2);
            arena.ControlPort = basePort;
            arena.FramePort = basePort + 1;
            arena.BlueChassis = blue;
            arena.RedChassis = red;
            arena.GimbalCamera = camera;
            arena.ProjectileRoot = projectileRoot;
            arena.SpawnPointBlue = blueSpawn;
            arena.SpawnPointRed = redSpawn;
            arenaObject.SetActive(true);
            return arena;
        }

        private Chassis CreateChassis(string name, string team, Vector3 position)
        {
            var chassisObject = new GameObject(name);
            _createdObjects.Add(chassisObject);
            chassisObject.transform.position = position;
            chassisObject.AddComponent<CharacterController>();

            var gimbalObject = new GameObject("Gimbal");
            gimbalObject.transform.SetParent(chassisObject.transform, false);
            gimbalObject.AddComponent<Gimbal>();

            CreateArmorPlate(chassisObject.transform, "ArmorPlateFront");
            CreateArmorPlate(chassisObject.transform, "ArmorPlateBack");
            CreateArmorPlate(chassisObject.transform, "ArmorPlateLeft");
            CreateArmorPlate(chassisObject.transform, "ArmorPlateRight");

            var chassis = chassisObject.AddComponent<Chassis>();
            chassis.Team = team;
            chassis.MaxHp = GameConstants.VehicleHpOneVsOne;
            return chassis;
        }

        private GameObject CreateProjectilePrefab()
        {
            var projectilePrefab = new GameObject("ProjectilePrefab");
            _createdObjects.Add(projectilePrefab);
            projectilePrefab.AddComponent<Rigidbody>();
            projectilePrefab.AddComponent<Projectile>();
            return projectilePrefab;
        }

        private static void CreateArmorPlate(Transform parent, string name)
        {
            var plateObject = new GameObject(name);
            plateObject.transform.SetParent(parent, false);
            plateObject.AddComponent<ArmorPlate>();
        }

        private Transform CreateTransform(string name, Vector3 position, float yawDegrees)
        {
            var obj = new GameObject(name);
            _createdObjects.Add(obj);
            obj.transform.position = position;
            obj.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            return obj.transform;
        }

        private static void FastForwardArenaClock(ArenaMain arena, float seconds)
        {
            arena.AdvanceEpisodeClockForTest(seconds);
        }

        private static void FastForwardNextShotTime(ArenaMain arena)
        {
            arena.AllowNextShotForTest();
        }

        private static void InvokeDrainShotQueue(ArenaMain arena)
        {
            arena.DrainShotQueueForTest();
        }
    }
}
