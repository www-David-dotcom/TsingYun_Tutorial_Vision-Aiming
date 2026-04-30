using NUnit.Framework;
using TsingYun.UnityArena;

namespace TsingYun.UnityArena.Tests.EditMode
{
    public class SeedRngTests
    {
        [Test]
        public void Reseed_ProducesDeterministicSequence()
        {
            SeedRng.Reseed(42);
            float a1 = SeedRng.NextFloat();
            float a2 = SeedRng.NextFloat();

            SeedRng.Reseed(42);
            float b1 = SeedRng.NextFloat();
            float b2 = SeedRng.NextFloat();

            Assert.AreEqual(a1, b1, 1e-9f);
            Assert.AreEqual(a2, b2, 1e-9f);
        }

        [Test]
        public void Reseed_DifferentSeeds_ProduceDifferentSequences()
        {
            SeedRng.Reseed(1);
            float a = SeedRng.NextFloat();
            SeedRng.Reseed(2);
            float b = SeedRng.NextFloat();
            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void NextRange_StaysWithinBounds()
        {
            SeedRng.Reseed(123);
            for (int i = 0; i < 1000; i++)
            {
                float v = SeedRng.NextRange(-2.0f, 5.0f);
                Assert.GreaterOrEqual(v, -2.0f);
                Assert.LessOrEqual(v, 5.0f);
            }
        }

        [Test]
        public void NextInt_StaysWithinBoundsInclusive()
        {
            SeedRng.Reseed(123);
            int lo = -3, hi = 7;
            for (int i = 0; i < 1000; i++)
            {
                int v = SeedRng.NextInt(lo, hi);
                Assert.GreaterOrEqual(v, lo);
                Assert.LessOrEqual(v, hi);
            }
        }

        [Test]
        public void CurrentSeed_ReturnsLastReseedValue()
        {
            SeedRng.Reseed(99);
            Assert.AreEqual(99L, SeedRng.CurrentSeed());
        }
    }
}
