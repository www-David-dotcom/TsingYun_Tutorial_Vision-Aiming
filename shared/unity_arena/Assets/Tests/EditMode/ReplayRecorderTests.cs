using System.IO;
using NUnit.Framework;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class ReplayRecorderTests
    {
        private string _dir;

        [SetUp]
        public void Setup()
        {
            _dir = Path.Combine(Path.GetTempPath(), "unity_arena_test_" + System.Guid.NewGuid());
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void Start_WritesHeaderLine()
        {
            var r = new ReplayRecorder(_dir);
            r.Start("ep-000000000000002a", 42);
            r.Finish(new System.Collections.Generic.Dictionary<string, object>());

            string path = Path.Combine(_dir, "ep-000000000000002a.json");
            string[] lines = File.ReadAllLines(path);
            Assert.GreaterOrEqual(lines.Length, 2);
            StringAssert.Contains("\"kind\":\"header\"", lines[0]);
            StringAssert.Contains("\"episode_id\":\"ep-000000000000002a\"", lines[0]);
            StringAssert.Contains("\"seed\":42", lines[0]);
        }

        [Test]
        public void Record_WritesEventLineBetweenHeaderAndFooter()
        {
            var r = new ReplayRecorder(_dir);
            r.Start("ep-1", 1);
            r.Record(new System.Collections.Generic.Dictionary<string, object>
            {
                { "kind", "KIND_FIRED" },
                { "stamp_ns", 1000L },
            });
            r.Finish(new System.Collections.Generic.Dictionary<string, object>());

            string[] lines = File.ReadAllLines(Path.Combine(_dir, "ep-1.json"));
            Assert.AreEqual(3, lines.Length);
            StringAssert.Contains("\"kind\":\"KIND_FIRED\"", lines[1]);
            StringAssert.Contains("\"kind\":\"footer\"", lines[2]);
        }
    }
}
