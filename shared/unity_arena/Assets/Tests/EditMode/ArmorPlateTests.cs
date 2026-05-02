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
            var c = new ChassisHpState { MaxHp = GameConstants.VehicleHpOneVsOne };
            c.Reset();
            c.ApplyDamage(GameConstants.BulletDamage);
            Assert.AreEqual(GameConstants.VehicleHpOneVsOne - GameConstants.BulletDamage, c.Hp);
        }

        [Test]
        public void ApplyDamage_ClampsAtZero()
        {
            var c = new ChassisHpState { MaxHp = GameConstants.VehicleHpOneVsOne };
            c.Reset();
            c.ApplyDamage(GameConstants.VehicleHpOneVsOne + GameConstants.BulletDamage);
            Assert.AreEqual(0, c.Hp);
            Assert.IsTrue(c.IsDestroyed);
        }

        [Test]
        public void Reset_RestoresMaxHp()
        {
            var c = new ChassisHpState { MaxHp = GameConstants.VehicleHpOneVsOne };
            c.Reset();
            c.ApplyDamage(GameConstants.BulletDamage);
            c.Reset();
            Assert.AreEqual(GameConstants.VehicleHpOneVsOne, c.Hp);
        }

        [Test]
        public void DamageFromMultiplePlates_AccumulatesIntoSinglePool()
        {
            // Real-RM model: hits on different plates of one robot all
            // deduct from the same chassis HP pool.
            var c = new ChassisHpState { MaxHp = GameConstants.VehicleHpOneVsOne };
            c.Reset();
            c.ApplyDamage(GameConstants.BulletDamage);  // front plate hit
            c.ApplyDamage(GameConstants.BulletDamage);  // back plate hit
            c.ApplyDamage(GameConstants.BulletDamage);  // left plate hit
            Assert.AreEqual(GameConstants.VehicleHpOneVsOne - 3 * GameConstants.BulletDamage, c.Hp);
        }

        [Test]
        public void PlateId_FormatsTeamDotFace()
        {
            var p = new ArmorPlateState { Team = "blue", Face = "front" };
            Assert.AreEqual("blue.front", p.PlateId);
        }
    }
}
