using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace AshesToStars
{
    /// <summary>
    /// 던전 생성기 자가검사 (명세 §7-1·§7-2 S5).
    ///
    ///   Unity -batchmode -quit -projectPath <프로젝트> -executeMethod AshesToStars.DungeonGenSelfCheck.Run
    ///
    /// 이 검사의 요지는 **"생성기가 아무것도 안 했는데 통과하지 않는가"**다.
    /// 단언을 하나만 걸면 상수를 반환하는 생성기가 그대로 통과한다 — 그래서 네 개를 한 세트로 건다.
    /// (이 저장소는 "빈 화면 700fps"와 "파티원 4명이 몹 스프라이트"를 겪었다.
    ///  수치가 나온다고 내용이 맞는 게 아니다.)
    /// </summary>
    public static class DungeonGenSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        public static void Run()
        {
            _fail = 0; _log.Length = 0;

            // ── N1. 같은 시드 → 완전히 같은 계획
            var a = DungeonGenerator.Generate(12345u, 3);
            var b = DungeonGenerator.Generate(12345u, 3);
            Check(a.Signature() == b.Signature(), "N1 같은 시드 → 같은 계획");

            // ── N1'. 전역 난수를 사이에서 마구 굴려도 흔들리지 않는가
            //        (UnityEngine.Random을 생성기에 쓰면 여기서 깨진다 — §7-2 S4의 네거티브 컨트롤)
            UnityEngine.Random.InitState(999);
            for (int i = 0; i < 1000; i++) { var _ = UnityEngine.Random.value; }
            var c = DungeonGenerator.Generate(12345u, 3);
            Check(a.Signature() == c.Signature(), "N1' 전역 난수 1000회 소비 후에도 동일");

            // ── N2. 다른 시드 1,000개 → 서로 다른 계획이 900개 이상
            //        상수를 반환하는 생성기는 여기서 1개가 되어 잡힌다.
            var sigs = new HashSet<string>();
            var kindSeq = new HashSet<string>();
            var tplSeq = new HashSet<string>();
            for (uint s = 1; s <= 1000u; s++)
            {
                var p = DungeonGenerator.Generate(s, 3);
                sigs.Add(p.Signature());
                var k = new StringBuilder(); var t = new StringBuilder();
                foreach (var n in p.Nodes) { k.Append((int)n.Kind); t.Append((int)n.Template); }
                kindSeq.Add(k.ToString()); tplSeq.Add(t.ToString());
            }
            Check(sigs.Count >= 900, $"N2 서로 다른 계획 {sigs.Count}/1000 (900 이상)");
            // 시드 앞자리만 반영하는 가짜 생성기를 막는다 — 종류·템플릿 시퀀스가 각각 붕괴하지 않아야 한다
            Check(kindSeq.Count >= 20, $"N2' 노드 종류 시퀀스 다양성 {kindSeq.Count}종 (20 이상)");
            Check(tplSeq.Count >= 100, $"N2'' 템플릿 시퀀스 다양성 {tplSeq.Count}종 (100 이상)");

            // ── N3. 노드 종류 배합이 §3-4 범위 안인가
            int minN = 99, maxN = 0, fallbacks = 0;
            bool mixOk = true, reachOk = true, capOk = true, entranceOk = true, bossOk = true;
            var sw = Stopwatch.StartNew();
            var times = new List<double>();
            for (uint s = 1; s <= 10000u; s++)
            {
                var t0 = sw.Elapsed.TotalMilliseconds;
                var p = DungeonGenerator.Generate(s, (int)(s % 10));
                times.Add(sw.Elapsed.TotalMilliseconds - t0);

                int n = p.Nodes.Length;
                if (n < minN) minN = n;
                if (n > maxN) maxN = n;
                fallbacks += p.FallbackCount;

                int combat = 0, elite = 0, boon = 0, bonus = 0, entrance = 0, boss = 0;
                foreach (var nd in p.Nodes)
                {
                    switch (nd.Kind)
                    {
                        case NodeKind.전투: combat++; break;
                        case NodeKind.정예: elite++; break;
                        case NodeKind.강화: boon++; break;
                        case NodeKind.보상분기: bonus++; break;
                        case NodeKind.입구: entrance++; break;
                        case NodeKind.보스: boss++; break;
                    }
                    if (nd.Wave != null && nd.Wave.TargetCount > DungeonGenerator.MobHardCap) capOk = false;
                }
                if (combat < 2 || combat > 5) mixOk = false;
                if (elite < 1 || elite > 2) mixOk = false;
                if (boon < 1 || boon > 2) mixOk = false;
                if (bonus > 1) mixOk = false;
                if (entrance != 1) entranceOk = false;
                if (boss != 1) bossOk = false;
                if (p.Nodes[0].Kind != NodeKind.입구) entranceOk = false;
                if (!p.BossReachable()) reachOk = false;
            }
            times.Sort();
            double p99 = times[(int)(times.Count * 0.99)];

            Check(mixOk, "N3 노드 배합이 §3-4 범위 안 (전투 2~5 · 정예 1~2 · 강화 1~2 · 분기 0~1)");
            Check(minN >= 7 && maxN <= 10, $"N3' 노드 수 {minN}~{maxN} (7~10)");
            Check(entranceOk, "입구는 정확히 1개이며 언제나 0번");
            Check(bossOk, "보스는 정확히 1개");
            Check(reachOk, "R5 시드 10,000개 전부 입구→보스 도달 가능 (flood fill)");
            Check(capOk, $"R7 목표 몹 수가 상한({DungeonGenerator.MobHardCap})을 넘지 않음");
            Check(fallbacks == 0, $"R4 폴백 카운터 {fallbacks} (0이어야 한다 — 조용한 품질 저하 금지)");
            Check(p99 < 16.0, $"생성 시간 p99 {p99:F2}ms (16ms 미만)");

            // ── N4. 난수를 실제로 소비했는가 (상수 생성기는 0이다)
            var rng = Rng.Stream(1u, 0, SeedChannel.Layout);
            for (int i = 0; i < 10; i++) rng.NextUInt();
            Check(rng.Consumed == 10, $"N4 난수 소비 계측 {rng.Consumed} (10)");

            // 스트림 분리 — 채널만 다르면 같은 수열이 나오면 안 된다
            var l = Rng.Stream(7u, 3, SeedChannel.Layout);
            var w = Rng.Stream(7u, 3, SeedChannel.Wave);
            var l2 = Rng.Stream(7u, 4, SeedChannel.Layout);
            Check(l.NextUInt() != w.NextUInt(), "채널이 다르면 수열이 다르다");
            Check(Rng.Stream(7u, 3, SeedChannel.Layout).NextUInt() != l2.NextUInt(),
                  "노드가 다르면 수열이 다르다");

            // 시드 0 보정(R6) — 0은 xorshift의 고정점이라 그대로 두면 난수가 멈춘다
            var z = DungeonGenerator.Generate(0u, 0);
            Check(z.RunSeed == 1u && z.Nodes.Length >= 7, "R6 시드 0 → 1로 보정하고 정상 생성");

            // 레이드급은 일반보다 밀도가 높아야 한다(§3-7)
            int normalMax = 0, raidMax = 0;
            for (uint s = 1; s <= 200u; s++)
            {
                foreach (var nd in DungeonGenerator.Generate(s, 5).Nodes)
                    if (nd.Wave != null && nd.Wave.TargetCount > normalMax) normalMax = nd.Wave.TargetCount;
                foreach (var nd in DungeonGenerator.Generate(s, 5, DungeonKind.레이드급).Nodes)
                    if (nd.Wave != null && nd.Wave.TargetCount > raidMax) raidMax = nd.Wave.TargetCount;
            }
            Check(raidMax > normalMax, $"레이드급 밀도 {raidMax} > 일반 {normalMax}");

            // 보스 마릿수 분포 (✅ §10-7 60/30/10 ±3%p)
            int[] cnt = new int[4];
            for (uint s = 1; s <= 10000u; s++) cnt[DungeonGenerator.Generate(s, 4).BossCount]++;
            float p1 = cnt[1] / 100f, p2 = cnt[2] / 100f, p3 = cnt[3] / 100f;
            Check(System.Math.Abs(p1 - 60f) <= 3f && System.Math.Abs(p2 - 30f) <= 3f &&
                  System.Math.Abs(p3 - 10f) <= 3f,
                  $"§10-7 보스 마릿수 분포 {p1:F1}/{p2:F1}/{p3:F1}% (60/30/10 ±3%p)");

            _log.AppendLine("  참고  계획 예시: " + DungeonGenerator.Generate(20260814u, 3).Signature());

            string head = _fail == 0 ? "[던전자가검사] PASS" : $"[던전자가검사] FAIL {_fail}건";
            Debug.Log(head + "\n" + _log);
            if (_fail > 0) EditorApplication.Exit(1);
        }
    }
}
