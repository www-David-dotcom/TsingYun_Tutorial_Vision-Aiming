using NUnit.Framework;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class ArmorPlateTests
    {
        // HP lives on the chassis, not the plate. These cases verify the
        // chassis HP state behaves like the old per-plate state did.
        [Test]
        public void ApplyDamage_DecrementsHp()
        {
            var c = new ChassisHpState { MaxHp = 200 };
            c.Reset();
            c.ApplyDamage(50);
            Assert.AreEqual(150, c.Hp);
        }

        [Test]
        public void ApplyDamage_ClampsAtZero()
        {
            var c = new ChassisHpState { MaxHp = 200 };
            c.Reset();
            c.ApplyDamage(500);
            Assert.AreEqual(0, c.Hp);
            Assert.IsTrue(c.IsDestroyed);
        }

        [Test]
        public void Reset_RestoresMaxHp()
        {
            var c = new ChassisHpState { MaxHp = 200 };
            c.Reset();
            c.ApplyDamage(150);
            c.Reset();
            Assert.AreEqual(200, c.Hp);
        }

        [Test]
        public void DamageFromMultiplePlates_AccumulatesIntoSinglePool()
        {
            // Real-RM model: hits on different plates of one robot all
            // deduct from the same chassis HP pool.
            var c = new ChassisHpState { MaxHp = 200 };
            c.Reset();
            c.ApplyDamage(40);  // front plate hit
            c.ApplyDamage(40);  // back plate hit
            c.ApplyDamage(40);  // left plate hit
            Assert.AreEqual(80, c.Hp);
        }

        [Test]
        public void PlateId_FormatsTeamDotFace()
        {
            var p = new ArmorPlateState { Team = "blue", Face = "front" };
            Assert.AreEqual("blue.front", p.PlateId);
        }
    }
}
