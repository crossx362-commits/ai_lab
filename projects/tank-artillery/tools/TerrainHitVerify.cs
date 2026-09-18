// 지형 충돌 게이트 — 발사체가 지형을 통과하지 않는가(§5-5).
//
// 2026-09-18 오너 보고 "발사체가 지형에 닿으면 터져야지 그냥 통과한다"로 만든 게이트다.
// 원인: 지형 판정이 **공기→지형 부호 전환**(`sdf<=0 && prevSdf>0`)만 봐서, 포구가 이미 지형 안에 있으면
//       그 전환이 영영 안 왔다 — 탄이 땅속을 그대로 지나갔다(묻힌 포구에서 쏜 432발 전부 관통).
// 수리: ProjectileSimulator 가 포신 길이(MuzzleEscape)만큼 앞을 훑어 공기가 나오면 거기서부터 날리고,
//       못 벗어나면 포구에서 터뜨린다(PhysX 의 "원점이 solid 안이면 거리 0" 관례 + 포격 게임 관례).
//
// ⚠️ 세 항목을 **같이** 재야 한다. 하나만 재면 반대쪽으로 넘어간다:
//    ① 안 재면 → 다시 지형을 통과한다.
//    ② 안 재면 → "묻히면 무조건 폭발"로 과교정해서 평범한 사격이 전부 자폭이 된다.
//    ③ 안 재면 → 포구가 아니라 비행 중간에서 지형을 뚫는 경우를 놓친다.

using System;
using System.Collections.Generic;
using Tankfall.Sim;

static class TerrainHitVerify
{
    const float Voxel = 0.5f, OriginY = -20f, MapSize = MapHeightFunction.MapSize;
    const int ChunkN = 16;
    static int Fail;

    static SdfVolume MakeVol(MapKind m)
        => new SdfVolume(Voxel, ChunkN, OriginY, MapHeightFunction.Fn(m), (int)(MapSize / Voxel),
                         MapHeightFunction.Grad(m));

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var accel = new Vec3(0f, -Ballistics.Gravity, 0f);

        // ── ① 벽에 대고 쏘면 코앞에서 터져야 한다 ───────────────
        // 계약은 "묻히면 무조건 즉시 폭발"이 아니다 — 얕게 걸친 채 **위로** 쏘면 포구가 지면을 벗어나므로
        // 정상 비행이 맞다(그건 ②에서 잰다). 여기서 재는 건 **빠져나갈 수 없는 사격**이다:
        // 깊이 박힌 포구로 수평~저각을 쏘면 앞이 전부 흙이라 그 자리에서 터져야 한다.
        Console.WriteLine("① 벽에 대고 쏘기(깊이 박힌 포구 + 저각) — 코앞에서 터지는가");
        int wallFar = 0, wallTotal = 0;
        foreach (MapKind m in Enum.GetValues(typeof(MapKind)))
        {
            var vol = MakeVol(m);
            // 가운데쯤 한 자리. 인원이 짝수면 정중앙이 없으니 가운데에 가까운 쪽.
            MapHeightFunction.Spawn(m, 0, MapHeightFunction.TeamSize / 2, out float sx, out float sz);
            float gy = MapHeightFunction.Height(m, sx, sz);
            foreach (float bury in new[] { 3.0f, 5.0f })
            {
                var p0 = new Vec3(sx, gy - bury, sz);
                int far = 0, n = 0;
                for (int deg = 0; deg <= 30; deg += 10)
                    for (int pw = 40; pw <= 100; pw += 20)
                    {
                        n++; wallTotal++;
                        var res = ProjectileSimulator.Simulate(vol, p0,
                            Ballistics.VelocityFrom(0f, deg, Ballistics.PowerToSpeed(pw / 100f)), accel, null, 0, MapSize);
                        float dx = res.Hit ? (res.Impact - p0).Length : 9999f;
                        if (dx > ProjectileSimulator.MuzzleEscape + 1f) { far++; wallFar++; }
                    }
                Console.WriteLine($"    [{MapHeightFunction.Name(m)}] {bury:F1}m 매몰·저각 — {n}발 중 멀리 날아감 {far}발");
            }
        }
        if (wallFar > 0) { Console.WriteLine($"    ❌ {wallFar}/{wallTotal} 발이 흙을 뚫고 날아간다"); Fail++; }
        else Console.WriteLine($"    ✅ {wallTotal}발 전부 포구 코앞에서 터진다");

        // ── ② 턱에 살짝 걸린 포구는 자폭하면 안 된다 ────────────
        // 지표 바로 위(공중)에서 쏘는 평범한 사격 + 포구가 아주 얕게(5cm) 걸린 사격.
        Console.WriteLine("\n② 네거티브 대조 — 평범한 사격이 자폭으로 변하지 않았는가");
        int selfBlast = 0, normalTotal = 0;
        foreach (MapKind m in Enum.GetValues(typeof(MapKind)))
        {
            var vol = MakeVol(m);
            for (int slot = 0; slot < MapHeightFunction.TeamSize; slot++)
            {
                MapHeightFunction.Spawn(m, 0, slot, out float sx, out float sz);
                float gy = MapHeightFunction.Height(m, sx, sz);
                foreach (float lift in new[] { 2.5f, 1.0f, -0.05f })   // 마지막은 포구가 5cm 묻힌 경우
                {
                    var p0 = new Vec3(sx, gy + lift, sz);
                    for (int deg = 20; deg <= 60; deg += 10)
                        for (int pw = 50; pw <= 100; pw += 25)
                        {
                            normalTotal++;
                            var res = ProjectileSimulator.Simulate(vol, p0,
                                Ballistics.VelocityFrom(0f, deg, Ballistics.PowerToSpeed(pw / 100f)), accel, null, 0, MapSize);
                            if (res.Hit && (res.Impact - p0).Length < 5f) selfBlast++;
                        }
                }
            }
        }
        if (selfBlast > 0) { Console.WriteLine($"    ❌ 평범한 사격 {selfBlast}/{normalTotal} 발이 코앞에서 터진다(자폭 회귀)"); Fail++; }
        else Console.WriteLine($"    ✅ {normalTotal}발 전부 정상 비행 — 자폭 회귀 없음");

        // ── ③ 관통 회귀 — 야우 스윕 + 파괴 후 ────────────────
        Console.WriteLine("\n③ 관통 회귀 — 궤적이 지표 아래를 지나는가");
        int pierce = 0, sweepTotal = 0;
        foreach (MapKind m in Enum.GetValues(typeof(MapKind)))
        {
            var vol = MakeVol(m);
            var rng = new Rng(12345u);
            for (int i = 0; i < 60; i++)
            {
                float cx = rng.Range(MapSize * 0.15f, MapSize * 0.85f), cz = rng.Range(MapSize * 0.15f, MapSize * 0.85f);
                SdfDeformer.SubtractSphere(vol, new BlastRequest(cx, MapHeightFunction.Height(m, cx, cz), cz, rng.Range(4f, 9f)));
            }
            for (int slot = 0; slot < MapHeightFunction.TeamSize; slot++)
            {
                MapHeightFunction.Spawn(m, 0, slot, out float sx, out float sz);
                var p0 = new Vec3(sx, MapHeightFunction.Height(m, sx, sz) + 2.5f, sz);
                for (int yaw = -40; yaw <= 40; yaw += 10)
                    for (int deg = 5; deg <= 70; deg += 5)
                        for (int pw = 30; pw <= 100; pw += 10)
                        {
                            sweepTotal++;
                            var res = ProjectileSimulator.Simulate(vol, p0,
                                Ballistics.VelocityFrom(yaw, deg, Ballistics.PowerToSpeed(pw / 100f)), accel, null, 0, MapSize);
                            foreach (var q in res.Path)
                            {
                                if (q.X < 0f || q.Z < 0f || q.X > MapSize || q.Z > MapSize) continue;
                                if (vol.SampleWorld(q.X, q.Y, q.Z) < -0.5f) { pierce++; break; }
                            }
                        }
            }
        }
        if (pierce > 0) { Console.WriteLine($"    ❌ {sweepTotal}발 중 관통 {pierce}발"); Fail++; }
        else Console.WriteLine($"    ✅ {sweepTotal}발 전부 지형 위로만 지난다");

        Console.WriteLine(Fail == 0 ? "\n✅ 지형 충돌 수리 확인" : $"\n❌ 실패 {Fail}건");
        Environment.Exit(Fail == 0 ? 0 : 1);
    }
}
