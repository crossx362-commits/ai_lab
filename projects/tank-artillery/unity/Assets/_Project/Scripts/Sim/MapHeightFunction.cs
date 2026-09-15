// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §맵 3종
// 파괴 전 지형 h(x,z) + ∇h. 경사 계수는 유한차분 5샘플이 아니라 이 기울기로 굵는다.

using System;

namespace Tankfall.Sim
{
    public enum MapKind { TwinHills = 0, Crater = 1, Terrace = 2 }

    public delegate void HeightGrad(float x, float z, out float h, out float dhx, out float dhz);

    public static class MapHeightFunction
    {
        public const float MapSize = 200f;
        public const float TwinWestHillX = 100f, TwinWestHillZ = 75f;
        public const float TwinEastHillX = 100f, TwinEastHillZ = 125f;

        public static string Name(MapKind k) => k switch
        {
            MapKind.TwinHills => "TwinHills",
            MapKind.Crater => "Crater",
            MapKind.Terrace => "Terrace",
            _ => k.ToString()
        };

        public static bool TryParse(string s, out MapKind k)
        {
            k = MapKind.TwinHills;
            return !string.IsNullOrEmpty(s) && Enum.TryParse(s.Trim(), true, out k);
        }

        public static void Spawn(MapKind map, int team, int slot, out float x, out float z)
        {
            int i = slot < 0 ? 0 : (slot > 2 ? 2 : slot);
            z = 50f + i * 50f;
            x = team == 0 ? 42f : MapSize - 42f;
            _ = map;
        }

        public static float Height(MapKind map, float x, float z)
        {
            Evaluate(map, x, z, out float h, out _, out _);
            return h;
        }

        public static float TwinHills(float x, float z)
        {
            Evaluate(MapKind.TwinHills, x, z, out float h, out _, out _);
            return h;
        }

        public static Func<float, float, float> Fn(MapKind map)
            => (x, z) => Height(map, x, z);

        public static HeightGrad Grad(MapKind map)
            => (float x, float z, out float h, out float hx, out float hz) => Evaluate(map, x, z, out h, out hx, out hz);

        public static void Evaluate(MapKind map, float x, float z, out float h, out float dhx, out float dhz)
        {
            switch (map)
            {
                case MapKind.Crater: EvalCrater(x, z, out h, out dhx, out dhz); break;
                case MapKind.Terrace: EvalTerrace(x, z, out h, out dhx, out dhz); break;
                default: EvalTwin(x, z, out h, out dhx, out dhz); break;
            }
        }

        static void EvalTwin(float x, float z, out float h, out float hx, out float hz)
        {
            h = 5f; hx = 0f; hz = 0f;
            Bump(x, z, TwinWestHillX, TwinWestHillZ, 14f, 1600f, ref h, ref hx, ref hz);
            Bump(x, z, TwinEastHillX, TwinEastHillZ, 14f, 1600f, ref h, ref hx, ref hz);
            float sx = MathF.Sin(x * 0.05f), cx = MathF.Cos(x * 0.05f);
            float sz = MathF.Sin(z * 0.05f), cz = MathF.Cos(z * 0.05f);
            h += 1.2f * sx * cz;
            hx += 1.2f * 0.05f * cx * cz;
            hz += 1.2f * sx * (-0.05f * sz);
        }

        static void Bump(float x, float z, float cx, float cz, float amp, float s,
                         ref float h, ref float hx, ref float hz)
        {
            float dx = x - cx, dz = z - cz;
            float u = 1f - (dx * dx + dz * dz) / s;
            if (u <= 0f) return;
            h += amp * u * u;
            float k = amp * 2f * u * (-2f / s);
            hx += k * dx;
            hz += k * dz;
        }

        static void EvalCrater(float x, float z, out float h, out float hx, out float hz)
        {
            float dx = x - 100f, dz = z - 100f;
            float r2 = dx * dx + dz * dz;
            float r = MathF.Sqrt(r2);
            float t = (r - 25f) / 70f;
            Smooth01(t, out float s, out float ds);
            h = 4f + 10f * s;
            if (r > 1e-4f && ds != 0f)
            {
                float dr = 10f * ds / 70f / r;
                hx = dr * dx;
                hz = dr * dz;
            }
            else { hx = 0f; hz = 0f; }
            float sx = MathF.Sin(x * 0.04f), cx = MathF.Cos(x * 0.04f);
            float sz = MathF.Sin(z * 0.04f), cz = MathF.Cos(z * 0.04f);
            h += 0.8f * sx * cz;
            hx += 0.8f * 0.04f * cx * cz;
            hz += 0.8f * sx * (-0.04f * sz);
        }

        static void EvalTerrace(float x, float z, out float h, out float hx, out float hz)
        {
            float u = MathF.Abs(x - 100f);
            float sign = x >= 100f ? 1f : -1f;
            float step, dstep;
            if (u < 28f) { step = 3.5f; dstep = 0f; }
            else if (u < 48f)
            {
                Smooth01((u - 28f) / 20f, out float s, out float ds);
                step = 3.5f + 6.5f * s;
                dstep = 6.5f * ds / 20f;
            }
            else { step = 10f; dstep = 0f; }
            float ripple = 0.6f * MathF.Sin(z * 0.07f);
            h = step + ripple;
            hx = sign * dstep;
            hz = 0.6f * 0.07f * MathF.Cos(z * 0.07f);
        }

        static void Smooth01(float t, out float s, out float ds)
        {
            if (t <= 0f) { s = 0f; ds = 0f; return; }
            if (t >= 1f) { s = 1f; ds = 0f; return; }
            s = t * t * (3f - 2f * t);
            ds = 6f * t * (1f - t);
        }
    }
}
