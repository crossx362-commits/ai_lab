using System;
using System.IO;
using Tankfall.Sim;

static class MapVerify
{
    const float Voxel = 0.5f, OriginY = -20f;
    const int ChunkN = 16;
    static int Fail;

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine($"=== 맵 {MapHeightFunction.Count}종 검증 ===\n");
        foreach (MapKind map in Enum.GetValues(typeof(MapKind)))
        {
            Console.WriteLine($"[{MapHeightFunction.Name(map)}]");
            CheckSymmetry(map);
            CheckLowAngle(map, expectClear: true);
            Profile(map);
        }
        Console.WriteLine("[네거티브] 옛 언덕(22m @ 60,100) 은 40° 가 막혀야 한다");
        CheckLowAngleLegacy();
        Console.WriteLine(Fail == 0 ? "\n✅ 맵 게이트 통과" : $"\n❌ 실패 {Fail}건");
        Environment.Exit(Fail == 0 ? 0 : 1);
    }

    /// <summary>
    /// 지형 **형태 검수**(오너 지시 2026-09-19 "전체 지형 다 검수해").
    ///
    /// 스크린샷은 카메라가 어디를 보느냐에 따라 같은 맵도 전혀 다르게 보인다 — 실제로 Crater 는
    /// 타이틀 카메라가 구덩이 안쪽 벽을 봐서 "평평한 모래"로만 찍혔다. 그래서 **높이 함수를 직접 격자로
    /// 떠서** 숫자로 본다: 높이 범위·평균 경사·급경사 비율·평탄 비율.
    ///
    /// 읽는 법 — **평탄 비율이 높으면 밋밋한 맵**이고(엄폐가 없다), **급경사 비율이 높으면
    /// 탱크가 갇히고(§7-6) 40° 이륙이 막힌다.** 둘 사이가 좋은 지형이다.
    /// `TANKFALL_HEIGHTDUMP=1` 을 주면 격자를 CSV 로 찍어 그림으로 볼 수 있다.
    /// </summary>
    static void Profile(MapKind map)
    {
        const int N = 96;
        float step = MapHeightFunction.MapSize / (N - 1);
        float lo = float.MaxValue, hi = float.MinValue, slopeSum = 0f;
        int steep = 0, flat = 0, n = 0;
        bool dump = Environment.GetEnvironmentVariable("TANKFALL_HEIGHTDUMP") == "1";
        var sb = dump ? new System.Text.StringBuilder() : null;

        for (int j = 0; j < N; j++)
        {
            for (int i = 0; i < N; i++)
            {
                float x = i * step, z = j * step;
                MapHeightFunction.Evaluate(map, x, z, out float h, out float hx, out float hz);
                float g = MathF.Sqrt(hx * hx + hz * hz);
                lo = MathF.Min(lo, h); hi = MathF.Max(hi, h);
                slopeSum += g; n++;
                if (g > 0.84f) steep++;          // tan40° — 이륙·이동이 막히기 시작하는 선
                if (g < 0.04f) flat++;           // 사실상 평지
                if (dump) sb.Append(h.ToString("F2")).Append(i == N - 1 ? '\n' : ',');
            }
        }
        Console.WriteLine($"    형태  높이 {lo,5:F1}~{hi,5:F1}m (폭 {hi - lo,4:F1})   평균경사 {slopeSum / n:F3}" +
                          $"   급경사 {steep * 100f / n,4:F1}%   평탄 {flat * 100f / n,4:F1}%");
        if (dump) File.WriteAllText($"/tmp/height_{MapHeightFunction.Name(map)}.csv", sb.ToString());
    }

    static void CheckSymmetry(MapKind map)
    {
        float worst = 0f;
        for (int i = 0; i < MapHeightFunction.TeamSize; i++)
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

        // ⚠️ 2026-09-17: 스폰 **6개 점**만 보던 검사는 그 사이 지형이 기울어도 통과한다.
        //    실제로 미러 자기전에서 스폰을 교환하면 A 승률이 52% → 31% 로 떨어졌는데(무풍 대조군에서도),
        //    이 게이트는 Δ0.00m 로 ✅ 였다. 스폰이 대칭이어도 **싸우는 땅**이 대칭이 아니면 판이 기운다.
        //    그래서 격자 전체에서 h(x,z) 와 h(200-x,z) 를 대조한다.
        {
            float gridWorst = 0f; float wx = 0f, wz = 0f;
            for (float z = 10f; z <= 190f; z += 5f)
                for (float x = 10f; x <= 100f; x += 5f)
                {
                    float ha = MapHeightFunction.Height(map, x, z);
                    float hb = MapHeightFunction.Height(map, MapHeightFunction.MapSize - x, z);
                    float d = MathF.Abs(ha - hb);
                    if (d > gridWorst) { gridWorst = d; wx = x; wz = z; }
                }
            if (gridWorst > 0.5f)
            {
                Console.WriteLine($"    ❌ 지형 좌우 비대칭 — 최대 Δ{gridWorst:F2}m @ (x={wx:F0},z={wz:F0}) 와 거울점");
                Fail++;
            }
            else Console.WriteLine($"    ✅ 지형 좌우 대칭 (격자 최대 Δ{gridWorst:F2}m)");
        }
    }

    static void CheckLowAngle(MapKind map, bool expectClear)
    {
        var vol = new SdfVolume(Voxel, ChunkN, OriginY, MapHeightFunction.Fn(map),
                                (int)(MapHeightFunction.MapSize / Voxel), MapHeightFunction.Grad(map));
        int blocked = 0;
        for (int i = 0; i < MapHeightFunction.TeamSize; i++)
        {
            MapHeightFunction.Spawn(map, 0, i, out float ax, out float az);
            MapHeightFunction.Spawn(map, 1, i, out float bx, out float bz);
            float ay = MapHeightFunction.Height(map, ax, az) + 2.4f;
            if (HitsTerrainEarly(vol, ax, ay, az, bx, bz)) blocked++;
        }
        bool clear = blocked == 0;
        if (clear == expectClear)
            Console.WriteLine(expectClear ? $"    ✅ 40° 레인 {MapHeightFunction.TeamSize}/{MapHeightFunction.TeamSize} 이륙 통과"
                                          : "    ✅ 네거티브(막힘) 확인");
        else
        {
            Console.WriteLine(expectClear
                ? $"    ❌ 40° 가 {blocked}/{MapHeightFunction.TeamSize} 레인에서 이륙 직후 지형에 박힘"
                : "    ❌ 네거티브 실패 — 옛 맵에서도 뚫린다(측정 고장)");
            Fail++;
        }
    }

    /// <summary>
    /// 네거티브 컨트롤: 옛 언덕(22m 가우시안, 반경 900)은 40°·**최대파워**로는 절대 못 막는다 —
    /// 발사각(피치)이 파워와 무관하게 고정이라 상승 기울기 tan(40°)=0.839 는 항상 같고,
    /// 이 언덕의 최대 경사(원점 근처 변곡점, ≈0.628)는 그보다 작아 기하학적으로 상승 구간에서
    /// 절대 못 따라잡는다(어떤 좌표를 골라도 동일 — 실측·해석 둘 다로 확인). 최대파워는 정점이
    /// ~108m 밖이라 검사창(8~35m) 안에서 하강도 안 한다. 실제 원작 대조 실험에서 관측된 "막힘"은
    /// 근거리 조준(저파워) 때 정점이 훨씬 가까이(≈10m) 와서 언덕 사면에 내리꽂히는 경우였다 —
    /// 그래서 이 네거티브 컨트롤만 최소파워(0f)로 쏜다(가까운 표적 조준을 흉내).
    /// </summary>
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
        bool hit = HitsTerrainEarly(vol, 55f, old(55f, 40f) + 2.4f, 40f, 150f, 155f, power: 0f);
        if (hit) Console.WriteLine("    ✅ 옛 언덕이 40° 를 막는다");
        else { Console.WriteLine("    ❌ 옛 언덕에서도 40° 가 통과 — 검사가 헐겁다"); Fail++; }
    }

    static bool HitsTerrainEarly(SdfVolume vol, float x0, float y0, float z0, float xt, float zt, float power = 1f)
    {
        float yaw = MathF.Atan2(xt - x0, zt - z0) * 180f / MathF.PI;
        var v0 = Ballistics.VelocityFrom(yaw, 40f, Ballistics.PowerToSpeed(power));
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
