using System.Collections.Generic;

namespace AshesToStars
{
    // ─────────────────────────────────────────────────────────────
    // 던전 계획 생성기 (명세 §3-3 G0~G9)
    //
    // 채택안: **미션 그래프 + 손으로 만든 아레나 템플릿**.
    // 생성기가 만드는 것은 "어떤 아레나를 어떤 순서로 지나는가"이고,
    // 아레나 자체(스폰 링·카이팅 레인·쿼터뷰 가독성)는 사람이 만든다 —
    // Enter the Gungeon 개발사가 절차 생성 방을 실측 후 손 제작으로 되돌린 이유가 그것이다.
    //
    // 설계의 핵심 한 줄: **연결성은 검사로 얻지 않고 구성으로 보장한다.**
    // G2에서 입구→전투들→보스를 일직선으로 먼저 깔아버리므로 그 시점에 이미 완주 가능하고,
    // G3 이후는 전부 '덧붙이기'라 무엇이 실패해도 던전은 클리어 가능한 채로 남는다.
    // 그래서 "생성 실패 → 전체 재생성" 루프가 아예 존재하지 않는다(영구사망 게임에서 이건 필수다).
    //
    // 생성 알고리즘은 UnityEngine을 안 쓴다 — 씬 없이 1만 시드를 돌려볼 수 있어야 한다.
    // 상한만 PerfCap(BalanceConfig.잡몹상한)을 읽는다. 값은 캐시라 시드 루프가 SO를 매 웨이브마다 안 만든다.
    // ─────────────────────────────────────────────────────────────
    public static class DungeonGenerator
    {
        /// <summary>노드 하나를 만들 때 허용하는 재시도(§3-6 R2). 소진하면 실패가 아니라 폴백.</summary>
        public const int MaxNodeAttempts = 10;

        /// <summary>어떤 노드 종류든 유효한 기본 템플릿(§3-6 R3).</summary>
        public const ArenaTemplate FallbackTemplate = ArenaTemplate.open_ring;

        /// <summary>동시 몹 수 상한. BalanceConfig.잡몹상한을 읽는다(§10-9·§3-6 R7). QA_NO면 옛 500.</summary>
        public static int MobHardCap => PerfCap.MobLimit();

        // §3-5 티어별 밀도·비율 표(몬스터문서 §5). 인덱스 = Tier(0~9) = T1~T10
        static readonly int[] StartCount = { 30, 30, 30, 40, 40, 40, 50, 50, 50, 60 };
        static readonly int[] EndCount = { 200, 240, 280, 300, 320, 340, 360, 400, 440, 500 };
        static readonly float[] ElitePct = { 5, 6, 7, 8, 9, 9, 10, 11, 11, 12 };
        static readonly float[] RangedPct = { 0, 15, 17, 20, 20, 22, 22, 25, 25, 25 };

        /// <summary>티어별 해금 AI(§3-5). T1 추적·포위 → T2 원거리 → T3 돌진.</summary>
        static MobAi[] UnlockedAi(int tier)
        {
            var list = new List<MobAi> { MobAi.추적, MobAi.포위 };
            if (tier >= 1) list.Add(MobAi.원거리);
            if (tier >= 2) list.Add(MobAi.돌진);
            return list.ToArray();
        }

        /// <summary>티어별 해금 계열(§3-5). T1 야수 → T2 언데드 → T4 마족 → T6 기계 → T8 정령.</summary>
        static MobFamily[] UnlockedFamilies(int tier)
        {
            var list = new List<MobFamily> { MobFamily.야수 };
            if (tier >= 1) list.Add(MobFamily.언데드);
            if (tier >= 3) list.Add(MobFamily.마족);
            if (tier >= 5) list.Add(MobFamily.기계);
            if (tier >= 7) list.Add(MobFamily.정령);
            return list.ToArray();
        }

        static readonly EliteKind[] EliteRoster =
        {
            EliteKind.수호자, EliteKind.처형자, EliteKind.주술사,
            EliteKind.군단장, EliteKind.저주술사, EliteKind.소환술사,
        };

        /// <summary>
        /// 던전 계획을 만든다. 같은 (seed, tier, kind)면 **항상 같은 계획**이 나온다.
        /// </summary>
        public static DungeonPlan Generate(uint runSeed, int tier, DungeonKind kind = DungeonKind.일반)
        {
            // ── G0. 시드 확정 (0은 xorshift 고정점 — R6)
            if (runSeed == 0u) runSeed = 1u;
            tier = tier < 0 ? 0 : (tier > 9 ? 9 : tier);

            var plan = new DungeonPlan { RunSeed = runSeed, Tier = tier, Kind = kind };

            // ── G1. 계열 결정 (✅ §10-3 진입 전 표시 — 그래서 계획에 들어간다)
            var famRng = Rng.Stream(runSeed, 0, SeedChannel.Wave);
            var families = UnlockedFamilies(tier);
            plan.Family = families[famRng.Next(families.Length)];

            // ── G2. 척추 부설 — 이 시점에 이미 클리어 가능하다
            var layout = Rng.Stream(runSeed, 0, SeedChannel.Layout);
            bool raid = kind == DungeonKind.레이드급;
            int combat = raid ? layout.Range(5, 7) : layout.Range(4, 6);   // 전투/정예 노드 수
            int boon = raid ? 1 : layout.Range(1, 3);                      // 강화 노드 1~2

            var nodes = new List<DungeonNode> { new DungeonNode { Kind = NodeKind.입구 } };

            // 전투·정예·강화를 척추 위에 섞는다. 강화는 **연속으로 오지 않게** 한다 —
            // 전투 없이 강화만 두 번 고르는 구간은 판이 늘어지고, 강화의 가치도 흐려진다.
            var spine = new List<NodeKind>();
            for (int i = 0; i < combat; i++) spine.Add(NodeKind.전투);
            for (int i = 0; i < boon; i++) spine.Add(NodeKind.강화);
            PlaceBoons(spine, ref layout);

            // 정예 노드 1~2개는 전투 노드 중에서 승격시킨다(§3-4 배합). 첫 전투는 남겨둔다 —
            // 들어가자마자 정예를 만나면 파티 상태를 볼 새가 없다.
            int elites = raid ? 2 : layout.Range(1, 3);
            PromoteElites(spine, elites, ref layout);

            foreach (var k in spine) nodes.Add(new DungeonNode { Kind = k });
            nodes.Add(new DungeonNode { Kind = NodeKind.보스 });
            // 보스 인덱스를 여기서 못박는다. G4가 뒤에 보상 분기를 덧붙이므로
            // "마지막 노드 = 보스"는 이 줄 이후로 성립하지 않는다.
            plan.BossIndex = nodes.Count - 1;

            var edges = new List<List<int>>();
            for (int i = 0; i < nodes.Count; i++) edges.Add(new List<int>());
            for (int i = 0; i < nodes.Count - 1; i++) edges[i].Add(i + 1);   // ← 완주 보장

            // ── G3. 사이클(지름길) 1개 — 위험한 구간을 건너뛰되 보상도 건너뛴다
            if (nodes.Count >= 5)
            {
                int from = layout.Range(1, nodes.Count - 3);
                int to = from + 2;
                if (to < nodes.Count - 1 && !edges[from].Contains(to)) edges[from].Add(to);
            }

            // ── G4. 선택 분기 0~1개 — 막다른 보상 노드
            if (layout.Chance(raid ? 0.8f : 0.55f) && nodes.Count >= 4)
            {
                int host = layout.Range(1, nodes.Count - 2);
                var bonus = new DungeonNode { Kind = NodeKind.보상분기, Optional = true };
                nodes.Add(bonus);
                edges.Add(new List<int>());
                edges[host].Add(nodes.Count - 1);        // 들어가면 되돌아 나온다(막다른 길)
            }

            // ── G5~G7. 노드별 템플릿·청크·웨이브
            var combatOrder = new List<int>();
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i].Kind == NodeKind.전투 || nodes[i].Kind == NodeKind.정예) combatOrder.Add(i);

            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                n.Index = i;
                var tpl = Rng.Stream(runSeed, i, SeedChannel.Template);
                n.TerrainSeed = Rng.Stream(runSeed, i, SeedChannel.Terrain).NextUInt();
                AssignTemplate(n, ref tpl, plan);

                if (n.Kind == NodeKind.전투 || n.Kind == NodeKind.정예 || n.Kind == NodeKind.보상분기)
                {
                    int step = combatOrder.IndexOf(i);
                    if (step < 0) step = combatOrder.Count - 1;      // 보상분기는 마지막 전투 밀도를 따른다
                    var wr = Rng.Stream(runSeed, i, SeedChannel.Wave);
                    n.Wave = BuildWave(tier, step + 1, System.Math.Max(1, combatOrder.Count),
                                       n.Kind, raid, ref wr);
                }
            }

            // ── G8. 보스 마릿수 (✅ §10-7 60/30/10)
            var bossRng = Rng.Stream(runSeed, nodes.Count, SeedChannel.Boss);
            float roll = bossRng.Value01();
            plan.BossCount = roll < 0.60f ? 1 : (roll < 0.90f ? 2 : 3);

            // ── G9. 동결
            plan.Nodes = nodes.ToArray();
            plan.Edges = new int[edges.Count][];
            for (int i = 0; i < edges.Count; i++) plan.Edges[i] = edges[i].ToArray();
            return plan;
        }

        /// <summary>강화 노드가 서로 붙지 않게 섞는다. 못 지키면 그냥 둔다 — 재시도는 이 정도가 상한이다.</summary>
        static void PlaceBoons(List<NodeKind> spine, ref Rng rng)
        {
            for (int attempt = 0; attempt < MaxNodeAttempts; attempt++)
            {
                rng.Shuffle(spine);
                bool bad = false;
                for (int i = 1; i < spine.Count; i++)
                    if (spine[i] == NodeKind.강화 && spine[i - 1] == NodeKind.강화) { bad = true; break; }
                // 첫 노드가 강화면 입구 직후에 전투 없이 강화를 고르게 된다 — 고를 근거가 없다
                if (spine.Count > 0 && spine[0] == NodeKind.강화) bad = true;
                if (!bad) return;
            }
        }

        /// <summary>전투 노드 일부를 정예로 승격. 첫 전투 노드는 제외한다.</summary>
        static void PromoteElites(List<NodeKind> spine, int count, ref Rng rng)
        {
            var candidates = new List<int>();
            bool first = true;
            for (int i = 0; i < spine.Count; i++)
            {
                if (spine[i] != NodeKind.전투) continue;
                if (first) { first = false; continue; }
                candidates.Add(i);
            }
            rng.Shuffle(candidates);
            for (int i = 0; i < count && i < candidates.Count; i++)
                spine[candidates[i]] = NodeKind.정예;
        }

        static void AssignTemplate(DungeonNode n, ref Rng rng, DungeonPlan plan)
        {
            if (n.Kind == NodeKind.보스) { n.Template = ArenaTemplate.arena_wide; }
            else if (n.Kind == NodeKind.입구 || n.Kind == NodeKind.강화)
            {
                n.Template = FallbackTemplate;              // 전투가 없으니 형태를 고를 이유가 없다
                n.ChunkIds = new int[0];
                return;
            }
            else
            {
                // arena_wide는 보스 전용이다 — 잡몹 노드가 뽑으면 밀도가 흩어져 판이 심심해진다.
                // 뽑히면 다시 뽑되, 상한을 소진하면 폴백으로 내려가고 **그 사실을 센다**(R4).
                int attempt = 0;
                do
                {
                    n.Template = (ArenaTemplate)rng.Next(5);
                    attempt++;
                } while (n.Template == ArenaTemplate.arena_wide && attempt < MaxNodeAttempts);

                if (n.Template == ArenaTemplate.arena_wide)
                {
                    n.Template = FallbackTemplate;
                    plan.FallbackCount++;
                }
            }

            // 템플릿 내부 청크 2~3개 + 회전 — Spelunky가 적은 저작물로 다양성을 내는 방식
            int chunks = rng.Range(2, 4);
            var ids = new int[chunks];
            for (int i = 0; i < chunks; i++) ids[i] = rng.Next(3) * 4 + rng.Next(4);  // 청크 3종 × 회전 4
            n.ChunkIds = ids;
        }

        /// <summary>
        /// §3-5 밀도 곡선을 노드 경계에 이산화한다. 여기서 **새 숫자를 만들지 않는다** —
        /// 티어 표(§18-11·몬스터문서 §5)를 진행률로 보간할 뿐이다.
        /// </summary>
        static WavePlan BuildWave(int tier, int step, int totalSteps, NodeKind kind, bool raid, ref Rng rng)
        {
            float progress = (float)step / totalSteps;
            int start = StartCount[tier];
            int end = EndCount[tier];
            int target = (int)System.Math.Round(start + (end - start) * progress);
            int begin = (int)System.Math.Round(start + (end - start) * ((float)(step - 1) / totalSteps));

            float elite = ElitePct[tier];
            if (kind == NodeKind.정예) elite *= 2.5f;        // 정예 아레나 = 정예가 주역인 판
            if (raid) { target = (int)(target * 1.25f); elite *= 1.3f; }

            // R7 — 성능 예산을 넘기지 않는다. 자를 때는 조용히 자르지 않는다(계획에 그대로 남는다).
            if (target > MobHardCap) target = MobHardCap;
            if (begin > target) begin = target;

            // ✅ §10-2 웨이브당 정예 유형은 1~2종만
            int kinds = kind == NodeKind.정예 ? rng.Range(1, 3) : (rng.Chance(0.5f) ? 1 : 0);
            var picks = new List<EliteKind>();
            var pool = new List<EliteKind>(EliteRoster);
            rng.Shuffle(pool);
            for (int i = 0; i < kinds && i < pool.Count; i++) picks.Add(pool[i]);

            return new WavePlan
            {
                StartCount = begin,
                TargetCount = target,
                DurationSec = kind == NodeKind.정예 ? 75f : 60f + rng.Next(16),
                ElitePercent = elite,
                RangedPercent = RangedPct[tier],
                UnlockedAi = UnlockedAi(tier),
                EliteKinds = picks.ToArray(),
            };
        }
    }
}
