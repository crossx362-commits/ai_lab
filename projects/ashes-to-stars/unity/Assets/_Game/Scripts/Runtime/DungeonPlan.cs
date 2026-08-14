using System.Collections.Generic;
using System.Text;

namespace AshesToStars
{
    // ─────────────────────────────────────────────────────────────
    // 던전 계획 (명세: docs/GAME_SPEC_DUNGEON_AND_PARTY.md §3-1)
    //
    // 이 파일에는 **UnityEngine 참조가 하나도 없다.** 의도적이다 —
    // 순수 C#이라야 씬·플레이어 없이 생성기를 1만 시드로 돌려볼 수 있다.
    // (이 저장소는 "돌렸다 ≠ 실행됐다"로 여러 번 데였다. 검증이 싸야 실제로 돌린다.)
    //
    // 계획은 생성 후 **불변**이다. 런 도중 변하는 것은 RunState에만 쌓는다.
    // 둘이 섞이면 "이 죽음을 시드로 재현할 수 있는가"가 깨진다 — 영구사망 게임에서 가장 비싼 버그다.
    // ─────────────────────────────────────────────────────────────

    public enum DungeonKind { 일반, 레이드급 }

    public enum NodeKind { 입구, 전투, 정예, 강화, 보상분기, 보스 }

    /// <summary>아레나 템플릿(§3-4 v1 5종). 아트 신규 0장 — 바닥·프랍은 기존 것을 쓴다.</summary>
    public enum ArenaTemplate { open_ring, pillars, choke, pockets, arena_wide }

    /// <summary>한 노드의 웨이브 편성. §18-11·몬스터문서 §5에서 유도된 값만 담는다.</summary>
    public sealed class WavePlan
    {
        public int StartCount;
        public int TargetCount;
        public float DurationSec;
        public float ElitePercent;
        public float RangedPercent;
        public MobAi[] UnlockedAi = new MobAi[0];
        public EliteKind[] EliteKinds = new EliteKind[0];
    }

    public sealed class DungeonNode
    {
        public int Index;
        public NodeKind Kind;
        public ArenaTemplate Template;
        public int[] ChunkIds = new int[0];
        public WavePlan Wave;              // null이면 전투 없는 노드
        public uint TerrainSeed;
        public bool Optional;              // 막다른 보상 분기(안 들러도 클리어 가능)
    }

    public sealed class DungeonPlan
    {
        public uint RunSeed;
        public int Tier;                   // 0~9 (= GameState.Tier)
        public MobFamily Family;
        public DungeonKind Kind;
        public DungeonNode[] Nodes = new DungeonNode[0];
        public int[][] Edges = new int[0][];   // 인접 리스트. Nodes[0]=입구, 보스는 BossIndex
        public int BossCount = 1;

        /// <summary>
        /// 보스 노드의 인덱스.
        ///
        /// ⚠️ "마지막 노드가 보스"로 가정하면 안 된다 — 선택 분기(G4)를 뒤에 덧붙이는 순간
        /// 마지막 노드는 보상 분기가 된다. 처음에 `Nodes[^1]`로 도달성을 검사했더니
        /// **보스가 아니라 보상 분기의 도달성**을 재고 있었다(테스트는 통과했다. 통과 이유가 틀렸을 뿐).
        /// </summary>
        public int BossIndex = -1;
        /// <summary>폴백을 탄 횟수(§3-6 R4). 0이 아니면 조용한 품질 저하가 있었다는 뜻이다.</summary>
        public int FallbackCount;

        /// <summary>입구에서 보스까지 실제로 갈 수 있는가 — 테스트가 1만 시드에 거는 단언(§3-6 R5).</summary>
        public bool BossReachable()
        {
            if (Nodes.Length == 0 || BossIndex < 0 || BossIndex >= Nodes.Length) return false;
            var seen = new bool[Nodes.Length];
            var stack = new Stack<int>();
            stack.Push(0); seen[0] = true;
            while (stack.Count > 0)
            {
                int n = stack.Pop();
                if (n < 0 || n >= Edges.Length) continue;
                foreach (int to in Edges[n])
                    if (to >= 0 && to < Nodes.Length && !seen[to]) { seen[to] = true; stack.Push(to); }
            }
            return seen[BossIndex];
        }

        /// <summary>
        /// 계획을 한 줄 문자열로. 네거티브 컨트롤 N1(같은 시드 → 같은 계획)·N2(다른 시드 → 다른 계획)가
        /// 이걸 비교한다. **사람이 읽을 수 있게** 두는 이유는 런 감사 로그에도 그대로 쓰기 때문이다.
        /// </summary>
        public string Signature()
        {
            var sb = new StringBuilder();
            sb.Append(RunSeed).Append('|').Append(Tier).Append('|').Append(Family)
              .Append('|').Append(Kind).Append('|').Append("boss").Append(BossCount).Append('|');
            foreach (var n in Nodes)
            {
                sb.Append(n.Kind).Append(':').Append(n.Template).Append(':');
                sb.Append(string.Join(".", n.ChunkIds));
                if (n.Optional) sb.Append("*");
                if (n.Wave != null) sb.Append('(').Append(n.Wave.TargetCount).Append(')');
                sb.Append(',');
            }
            sb.Append('|');
            for (int i = 0; i < Edges.Length; i++)
                foreach (int to in Edges[i]) sb.Append(i).Append('>').Append(to).Append(' ');
            return sb.ToString();
        }
    }

    /// <summary>런 진행 중 변하는 것. 계획과 분리한다(§3-1).</summary>
    public sealed class RunState
    {
        public int CurrentNode;
        public readonly List<int> Boons = new List<int>();   // ✅ §7 던전을 나가면 초기화 — 저장하지 않는다
        public int ElitesKilled;
        public readonly HashSet<int> Cleared = new HashSet<int>();
    }
}
