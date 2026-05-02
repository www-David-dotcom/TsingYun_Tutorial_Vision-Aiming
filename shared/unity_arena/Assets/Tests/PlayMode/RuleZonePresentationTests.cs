using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.PlayMode
{
    public class RuleZonePresentationTests
    {
        private GameObject _root;

        [UnityTest]
        public IEnumerator RuleZonePresentation_RendersHealingAndBoostMarkersFromWorldState()
        {
            _root = new GameObject("RuleZonePresentationHost");
            var presentation = _root.AddComponent<RuleZonePresentation>();
            var world = new MatchWorldState();
            world.SetBoostPointForTest(0, new Vector3(1f, 0f, 2f));
            world.SetBoostPointForTest(1, new Vector3(-1f, 0f, -2f));
            var red = new[] { new TeamPosition("red", new Vector3(1f, 0f, 2f), active: true) };
            var blue = new[] { new TeamPosition("blue", new Vector3(10f, 0f, 10f), active: true) };
            world.UpdateBoostHolders(red, blue, holdRadius: 2f);

            presentation.Render(
                blueHealingPosition: Vector3.left,
                redHealingPosition: Vector3.right,
                healingRadius: 2.5f,
                worldState: world,
                boostRadius: 2f);
            yield return null;

            Assert.IsNotNull(_root.transform.Find("RuleMarkers/HealingZone_Blue"));
            Assert.IsNotNull(_root.transform.Find("RuleMarkers/HealingZone_Red"));
            RuleZoneMarker boostOne = _root.transform.Find("RuleMarkers/BoostPoint_1").GetComponent<RuleZoneMarker>();
            Assert.AreEqual(BoostPointHolder.Red, boostOne.Holder);
            Assert.AreEqual(2f, boostOne.Radius, 1e-6f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            yield return null;
        }
    }
}
