using System;

namespace TsingYun.UnityArena
{
    // Single source of randomness for the arena so the same EnvReset.seed gives
    // byte-identical episodes. Mirrors seed_rng.gd. Subsystems that need
    // randomness call NextFloat / NextRange / NextInt; ArenaMain calls Reseed
    // on every new episode.
    public static class SeedRng
    {
        private static Random _rng = new Random(0);
        private static long _currentSeed = 0;

        public static void Reseed(long seedValue)
        {
            _currentSeed = seedValue;
            // System.Random takes int; fold the 64-bit seed into an int so the
            // same input always produces the same RNG state.
            int intSeed = unchecked((int)(seedValue ^ (seedValue >> 32)));
            _rng = new Random(intSeed);
        }

        public static long CurrentSeed() => _currentSeed;

        public static float NextFloat() => (float)_rng.NextDouble();

        public static float NextRange(float lo, float hi)
        {
            return lo + (hi - lo) * (float)_rng.NextDouble();
        }

        public static int NextInt(int lo, int hi)
        {
            // Inclusive on both ends, matching Godot's randi_range.
            return _rng.Next(lo, hi + 1);
        }
    }
}
