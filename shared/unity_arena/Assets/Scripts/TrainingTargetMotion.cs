using UnityEngine;

namespace TsingYun.UnityArena
{
    public readonly struct TrainingTargetSample
    {
        public readonly Vector3 Position;
        public readonly Vector3 VelocityWorld;
        public readonly float YawRad;
        public readonly float YawRateRadPerSecond;

        public TrainingTargetSample(
            Vector3 position,
            Vector3 velocityWorld,
            float yawRad,
            float yawRateRadPerSecond)
        {
            Position = position;
            VelocityWorld = velocityWorld;
            YawRad = yawRad;
            YawRateRadPerSecond = yawRateRadPerSecond;
        }
    }

    public sealed class TrainingTargetMotion
    {
        private readonly Vector3 _origin;
        private readonly float _halfExtentMeters;
        private readonly float _translationSpeedMps;
        private readonly float _yawRateRadPerSecond;
        private float _offsetMeters;
        private float _direction = 1f;
        private float _yawRad;

        public TrainingTargetMotion(
            Vector3 origin,
            float halfExtentMeters,
            float translationSpeedMps,
            float yawRateRadPerSecond)
        {
            _origin = origin;
            _halfExtentMeters = Mathf.Max(0f, halfExtentMeters);
            _translationSpeedMps = Mathf.Max(0f, translationSpeedMps);
            _yawRateRadPerSecond = Mathf.Max(0f, yawRateRadPerSecond);
        }

        public TrainingTargetSample Step(float deltaSeconds)
        {
            float dt = Mathf.Max(0f, deltaSeconds);
            float previousOffset = _offsetMeters;
            float nextOffset = _offsetMeters + _direction * _translationSpeedMps * dt;

            while (_halfExtentMeters > 0f
                && (nextOffset > _halfExtentMeters || nextOffset < -_halfExtentMeters))
            {
                if (nextOffset > _halfExtentMeters)
                {
                    nextOffset = _halfExtentMeters - (nextOffset - _halfExtentMeters);
                    _direction = -1f;
                }
                else
                {
                    nextOffset = -_halfExtentMeters + (-_halfExtentMeters - nextOffset);
                    _direction = 1f;
                }
            }

            if (_halfExtentMeters <= 0f)
            {
                nextOffset = 0f;
            }

            _offsetMeters = nextOffset;
            _yawRad = WrapTwoPi(_yawRad + _yawRateRadPerSecond * dt);

            float velocityX = dt > 0f ? (_offsetMeters - previousOffset) / dt : 0f;
            return new TrainingTargetSample(
                _origin + new Vector3(_offsetMeters, 0f, 0f),
                new Vector3(velocityX, 0f, 0f),
                _yawRad,
                _yawRateRadPerSecond);
        }

        private static float WrapTwoPi(float angle)
        {
            float twoPi = Mathf.PI * 2f;
            angle %= twoPi;
            return angle < 0f ? angle + twoPi : angle;
        }
    }
}
