using System;

namespace Tankfall.Sim
{
    /// <summary>
    /// Stylized flight controls, not a real aerodynamic model. Launch-dependent pseudo-random
    /// amplitude, handedness and frequency keep replays deterministic without frame RNG.
    /// Control ends smoothly within 2 seconds (earlier for low angles); later aim solutions retain the base trajectory.
    /// Near obstacles can intercept the actual curved path. Normal and special share controls.
    /// </summary>
    public static class FlightMotion
    {
        public static Vec3 Offset(Vec3 launch, float t, int motion)
        {
            float duration = MathF.Min(2f, MathF.Max(.15f, launch.Y / 30f));
            if (motion == 0 || t <= 0f || t >= duration) return default;
            uint seed = unchecked((uint)(int)(launch.X * 997f) ^
                                  (uint)(int)(launch.Y * 1999f) * 1664525u ^
                                  (uint)(int)(launch.Z * 4093f) * 1013904223u);
            seed ^= seed >> 16; seed *= 2246822519u; seed ^= seed >> 13;
            float r = (seed & 65535) / 65535f;
            float sign = (seed & 65536) == 0 ? -1f : 1f;
            float u = t / duration;
            float e = 16f * u * u * (1f-u) * (1f-u); // zero value and slope at both ends
            float phase = u * MathF.PI * (3.6f + r * 1.4f);
            var side = new Vec3(-launch.Z, 0f, launch.X).Normalized;
            var up = new Vec3(0,1,0);
            float a = (2.5f + r * 2f) * MathF.Min(1f, duration);
            switch (motion)
            {
                case 1: return (side * (sign * MathF.Sin(u*MathF.PI*1.4f)) + up * (.25f*MathF.Sin(u*MathF.PI))) * (e*a*.22f); // heavy rocket: one restrained correction, no repeated tumbling
                case 2: return side * (sign * MathF.Sin(phase) * e*a*1.4f);
                case 3: return up * (e * (6f+r*4f) * MathF.Min(1f,duration));
                case 4: return side * (sign * e*a*1.5f);
                default: return default;
            }
        }

        public static Vec3 Velocity(Vec3 launch, float t, int motion)
        {
            float duration = MathF.Min(2f, MathF.Max(.15f, launch.Y / 30f));
            if (motion == 0 || t <= 0f || t >= duration) return default;
            const float h = .001f;
            return (Offset(launch,t+h,motion)-Offset(launch,t-h,motion))*(.5f/h);
        }
    }
}
