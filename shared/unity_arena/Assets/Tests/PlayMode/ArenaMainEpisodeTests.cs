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
        [UnityTest]
        public IEnumerator EnvReset_ReturnsInitialStateWithSimSha()
        {
            yield return SceneManager.LoadSceneAsync("ArenaMain", LoadSceneMode.Single);
            var arena = Object.FindFirstObjectByType<ArenaMain>();
            Assert.IsNotNull(arena);

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
            yield return SceneManager.LoadSceneAsync("ArenaMain", LoadSceneMode.Single);
            var arena = Object.FindFirstObjectByType<ArenaMain>();

            arena.EnvReset(new Dictionary<string, object> { { "seed", 42L } });
            string id1 = arena.EpisodeId;
            arena.EnvFinish(new Dictionary<string, object>());

            arena.EnvReset(new Dictionary<string, object> { { "seed", 42L } });
            string id2 = arena.EpisodeId;

            Assert.AreEqual("ep-000000000000002a", id1);
            Assert.AreEqual(id1, id2);
        }
    }
}
