using System;
using UnityEngine;

namespace TsingYun.UnityArena
{
    public sealed class ArenaEpisodeClock
    {
        private readonly Func<long> _elapsedMilliseconds;
        private long _startedTicksMs;

        public ArenaEpisodeClock(Func<long> elapsedMilliseconds)
        {
            _elapsedMilliseconds = elapsedMilliseconds;
        }

        public void Reset()
        {
            _startedTicksMs = _elapsedMilliseconds();
        }

        public long NowNs => (_elapsedMilliseconds() - _startedTicksMs) * 1_000_000L;

        internal void AdvanceForTest(float seconds)
        {
            _startedTicksMs -= Mathf.CeilToInt(seconds * 1000f);
        }
    }
}
