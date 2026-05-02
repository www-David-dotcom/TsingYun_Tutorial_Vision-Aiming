using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.PlayMode
{
    public class TrainingGroundEpisodeTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>();
        private int _sceneSerial;
        private int _portSerial;

        [UnityTest]
        public IEnumerator EnvReset_NormalModeDoesNotExposeTrainingTelemetry()
        {
            ArenaMain arena = CreateArena();
            yield return null;

            Dictionary<string, object> reset = arena.EnvReset(new Dictionary<string, object>
            {
                { "seed", 41L },
                { "oracle_hints", true },
            });

            Dictionary<string, object> bundle = (Dictionary<string, object>)reset["bundle"];

            Assert.IsFalse(bundle.ContainsKey("training"));
        }

        [UnityTest]
        public IEnumerator EnvReset_TrainingConfigEnablesBackendTelemetry()
        {
            ArenaMain arena = CreateArena();
            yield return null;

            Dictionary<string, object> reset = arena.EnvReset(TrainingReset(seed: 42L));

            Dictionary<string, object> bundle = (Dictionary<string, object>)reset["bundle"];
            Assert.IsTrue(bundle.ContainsKey("training"));

            Dictionary<string, object> training = (Dictionary<string, object>)bundle["training"];
            Assert.AreEqual(0, training["damage_dealt"]);
            Assert.AreEqual(false, training["episode_done"]);
        }

        [UnityTest]
        public IEnumerator EnvStep_TrainingTargetMovesAndReportsVelocity()
        {
            ArenaMain arena = CreateArena();
            yield return null;

            arena.EnvReset(TrainingReset(seed: 43L));

            yield return new WaitForFixedUpdate();
            Dictionary<string, object> step = arena.EnvStep(new Dictionary<string, object>());
            Dictionary<string, object> training = (Dictionary<string, object>)step["training"];
            Dictionary<string, object> velocity = (Dictionary<string, object>)training["target_velocity_world"];

            Assert.Greater((double)velocity["x"], 0.0);
            Assert.Greater((double)training["target_yaw_world"], 0.0);
        }

        [UnityTest]
        public IEnumerator EnvStep_WhenTrainingDisabledAfterTrainingReset_RemovesTelemetry()
        {
            ArenaMain arena = CreateArena();
            yield return null;

            arena.EnvReset(TrainingReset(seed: 44L));
            Dictionary<string, object> normalReset = arena.EnvReset(new Dictionary<string, object>
            {
                { "seed", 45L },
                { "oracle_hints", true },
            });
            Dictionary<string, object> bundle = (Dictionary<string, object>)normalReset["bundle"];

            Assert.IsFalse(bundle.ContainsKey("training"));

            Dictionary<string, object> step = arena.EnvStep(new Dictionary<string, object>());
            Assert.IsFalse(step.ContainsKey("training"));
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

        private static Dictionary<string, object> TrainingReset(long seed)
        {
            return new Dictionary<string, object>
            {
                { "seed", seed },
                { "oracle_hints", true },
                { "training_config", new Dictionary<string, object>
                {
                    { "enabled", true },
                    { "target_translation_speed_mps", 1.0 },
                    { "target_rotation_speed_rad_s", 2.0 },
                    { "target_path_half_extent_m", 2.0 },
                    { "baseline_opponent_enabled", true },
                }},
            };
        }

        private ArenaMain CreateArena()
        {
            SceneManager.CreateScene($"TrainingGroundEpisodeTest_{_sceneSerial++}");

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
            int basePort = 19654 + (_portSerial++ * 2);
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
    }
}
