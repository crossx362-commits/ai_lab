using System;
using Tankfall.Sim;

static class MapVerify
{
    const float Voxel = 0.5f, OriginY = -20f;
    const int ChunkN = 16;
    static int Fail;

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== 맵 3종 검증 ===\n");
        foreach (MapKind map in new[] { MapKind.TwinHills, MapKind.Crater, MapKind.Terrace })
        {
            Console.WriteLine($"[{MapHeightFunction.Name(map)}]");
            CheckSymmetry(map);
            CheckLowAngle(map, expectClear: true);
        }
        Console.WriteLine("[네거티브] 옛 언덕(22m @ 60,100) 은 40° 가 막혀야 한다");
        CheckLowAngleLegacy();
        Console.WriteLine(Fail == 0 ? "\n✅ 맵 게이트 통과" : $"\n❌ 실패 {Fail}건");
        Environment.Exit(Fail == 0 ? 0 : 1);
    }

    static void CheckSymmetry(MapKind map)
    {
        float worst = 0f;
        for (int i = 0; i < 3; i++)
        {
            MapHeightFunction.Spawn(map, 0, i, out float ax, out float az);
            MapHeightFunction.Spawn(map, 1, i, out float bx, out float bz);
            float ha = MapHeightFunction.Height(map, ax, az);
            float hb = MapHeightFunction.Height(map, bx, bz);
            float d = MathF.Abs(ha - hb);
            if (d > worst) worst = d;
            Console.WriteLine($"    슬롯{i}  A({ax:F0},{az:F0})={ha:F2}m  B({bx:F0},{bz:F0})={hb:F2}m  Δ{d:F2}m");
        }
        if (worst > 0.5f) { Console.WriteLine("    ❌ 스폰 높이 비대칭 > 0.5m"); Fail++; }
        else Console.WriteLine("    ✅ 스폰 높이 대칭");
    }

    static void CheckLowAngle(MapKind map, bool expectClear)
    {
        var vol = new SdfVolume(Voxel, ChunkN, OriginY, MapHeightFunction.Fn(map),
                                (int)(MapHeightFunction.MapSize / Voxel), MapHeightFunction.Grad(map));
        int blocked = 0;
        for (int i = 0; i < 3; i++)
        {
            MapHeightFunction.Spawn(map, 0, i, out float ax, out float az);
            MapHeightFunction.Spawn(map, 1, i, out float bx, out float bz);
            float ay = MapHeightFunction.Height(map, ax, az) + 2.4f;
            if (HitsTerrainEarly(vol, ax, ay, az, bx, bz)) blocked++;
        }
        bool clear = blocked == 0;
        if (clear == expectClear)
            Console.WriteLine(expectClear ? "    ✅ 40° 레인 3/3 이륙 통과" : "    ✅ 네거티브(막힘) 확인");
        else
        {
            Console.WriteLine(expectClear
                ? $"    ❌ 40° 가 {blocked}/3 레인에서 이륙 직후 지형에 박힘"
                : "    ❌ 네거티브 실패 — 옛 맵에서도 뚫린다(측정 고장)");
            Fail++;
        }
    }

    static void CheckLowAngleLegacy()
    {
        Func<float, float, float> old = (x, z) =>
        {
            float h = 4f;
            h += 22f * MathF.Exp(-(((x - 60f) * (x - 60f) + (z - 100f) * (z - 100f)) / 900f));
            h += 20f * MathF.Exp(-(((x - 145f) * (x - 145f) + (z - 105f) * (z - 105f)) / 800f));
            h += 2.5f * MathF.Sin(x * 0.06f) * MathF.Cos(z * 0.05f);
            return h;
        };
        var vol = new SdfVolume(Voxel, ChunkN, OriginY, old, (int)(MapHeightFunction.MapSize / Voxel));
        bool hit = HitsTerrainEarly(vol, 69f, old(69f, 48f) + 2.4f, 48f, 150f, 155f);
        if (hit) Console.WriteLine("    ✅ 옛 언덕이 40° 를 막는다");
        else { Console.WriteLine("    ❌ 옛 언덕에서도 40° 가 통과 — 검사가 헐겁다"); Fail++; }
    }

    static bool HitsTerrainEarly(SdfVolume vol, float x0, float y0, float z0, float xt, float zt)
    {
        float yaw = MathF.Atan2(xt - x0, zt - z0) * 180f / MathF.PI;
        var v0 = Ballistics.VelocityFrom(yaw, 40f, Ballistics.PowerToSpeed(1f));
        var a = Ballistics.Accel(0f, 0f);
        const float dt = 0.02f;
        float x = x0, y = y0, z = z0;
        float vx = v0.X, vy = v0.Y, vz = v0.Z;
        for (int i = 0; i < 200; i++)
        {
            x += vx * dt; y += vy * dt; z += vz * dt;
            vx += a.X * dt; vy += a.Y * dt; vz += a.Z * dt;
            float horiz = MathF.Sqrt((x - x0) * (x - x0) + (z - z0) * (z - z0));
            if (horiz < 8f) continue;
            if (horiz > 35f) return false;
            if (vol.SampleWorld(x, y, z) < 0f) return true;
        }
        return false;
    }
}
