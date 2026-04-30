using NUnit.Framework;
using UnityEngine;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class ArmorPlateTests
    {
        [Test]
        public void ApplyDamage_DecrementsHp()
        {
            var p = new ArmorPlateState { MaxHp = 200 };
            p.Reset();
            p.ApplyDamage(50);
            Assert.AreEqual(150, p.Hp);
        }

        [Test]
        public void ApplyDamage_ClampsAtZero()
        {
            var p = new ArmorPlateState { MaxHp = 200 };
            p.Reset();
            p.ApplyDamage(500);
            Assert.AreEqual(0, p.Hp);
        }

        [Test]
        public void Reset_RestoresMaxHp()
        {
            var p = new ArmorPlateState { MaxHp = 200 };
            p.Reset();
            p.ApplyDamage(150);
            p.Reset();
            Assert.AreEqual(200, p.Hp);
        }

        [Test]
        public void PlateId_FormatsTeamDotFace()
        {
            var p = new ArmorPlateState { Team = "blue", Face = "front" };
            Assert.AreEqual("blue.front", p.PlateId);
        }
    }
}
