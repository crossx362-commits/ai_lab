using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 진행 중인 던전 런 하나 (명세 S6·S7).
    ///
    /// 계획(`DungeonPlan`)과 진행 상태(`RunState`)를 분리해서 들고 있는다 —
    /// 계획은 시드에서 나오는 불변값이고, 진행은 플레이어가 만드는 변수다.
    /// 둘을 섞으면 "이 죽음을 시드로 재현할 수 있는가"가 깨진다(§3-1).
    ///
    /// ⚠️ **저장하지 않는다.** ✅§7 "던전을 나가면 초기화" — 강화도 진행도도 런 안에서만 산다.
    /// 게임을 껐다 켜면 던전은 사라진다. 남는 것은 이미 지갑에 들어간 보상뿐이다.
    /// </summary>
    public static class DungeonRun
    {
        public static DungeonPlan Plan { get; private set; }
        public static RunState State { get; private set; }
        public static bool Active => Plan != null;

        /// <summary>돌아갈 화면 — 던전은 필드 안에 있다(✅ §7).</summary>
        public static string ReturnScene = GameFlow.Field;

        /// <summary>지금 전투 중인 노드. 전투가 끝나면 이 노드가 클리어된다.</summary>
        public static int PendingNode { get; private set; } = -1;

        public static void Begin(uint seed, int tier, DungeonKind kind, string returnScene)
        {
            Plan = DungeonGenerator.Generate(seed, tier, kind);
            State = new RunState { CurrentNode = 0 };
            State.Cleared.Add(0);                 // 입구는 전투가 없다 — 들어선 순간 지나온 것이다
            PendingNode = -1;
            ReturnScene = returnScene;

            // 감사 로그(§3-2 규칙 4). 캐릭터가 삭제됐다는 신고가 오면 이 한 줄로 판을 다시 만든다.
            Debug.Log($"[던전] 시작 seed={Plan.RunSeed} tier={Plan.Tier} 계열={Plan.Family} " +
                      $"종류={Plan.Kind} 노드={Plan.Nodes.Length} 보스={Plan.BossCount}체 " +
                      $"폴백={Plan.FallbackCount}\n[던전] 계획: {Plan.Signature()}");
            if (Plan.FallbackCount > 0)
                Debug.LogWarning($"[던전] 폴백 {Plan.FallbackCount}회 — 조용한 품질 저하가 있었다(§3-6 R4)");

            WriteRunLog();
        }

        /// <summary>
        /// 런 감사 로그 (S12 · §3-2 규칙 4).
        ///
        /// "이 판에서 캐릭터가 삭제됐다"는 신고가 오면 **시드만으로 판을 다시 만든다.**
        /// 영구사망 게임에서 이건 편의가 아니라 사후 조사 수단이다.
        /// 저장 위치는 `Application.persistentDataPath/runs/` — 플레이어 빌드에서도 쓸 수 있는 유일한 곳이고,
        /// 저장소 경로에 쓰면 배포본에서는 존재하지 않는 경로라 조용히 실패한다.
        /// </summary>
        static void WriteRunLog()
        {
            try
            {
                string dir = System.IO.Path.Combine(Application.persistentDataPath, "runs");
                System.IO.Directory.CreateDirectory(dir);
                string stamp = System.DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string path = System.IO.Path.Combine(dir, $"run_{stamp}_{Plan.RunSeed}.json");

                var kinds = new List<string>();
                foreach (var n in Plan.Nodes) kinds.Add($"\"{n.Kind}\"");

                string json =
                    "{" +
                    $"\"seed\":{Plan.RunSeed}," +
                    $"\"tier\":{Plan.Tier}," +
                    $"\"family\":\"{Plan.Family}\"," +
                    $"\"kind\":\"{Plan.Kind}\"," +
                    $"\"bossCount\":{Plan.BossCount}," +
                    $"\"bossIndex\":{Plan.BossIndex}," +
                    $"\"fallbacks\":{Plan.FallbackCount}," +
                    $"\"nodes\":[{string.Join(",", kinds)}]," +
                    $"\"signature\":\"{Plan.Signature().Replace("\"", "'")}\"" +
                    "}";
                System.IO.File.WriteAllText(path, json);
            }
            catch (System.Exception e)
            {
                // 로그를 못 써도 게임은 계속돼야 한다. 다만 **조용히** 넘어가지는 않는다 —
                // 감사 로그가 없다는 사실 자체가 사후 조사에서 치명적이다.
                Debug.LogWarning($"[던전] 런 로그 기록 실패: {e.Message}");
            }
        }

        public static void End()
        {
            Plan = null; State = null; PendingNode = -1;
            HuntBoon.End();
        }

        /// <summary>지금 위치에서 갈 수 있는 노드들. 이미 클리어한 곳은 다시 안 간다.</summary>
        public static List<int> NextNodes()
        {
            var outs = new List<int>();
            if (!Active) return outs;
            int cur = State.CurrentNode;
            if (cur < 0 || cur >= Plan.Edges.Length) return outs;
            foreach (int to in Plan.Edges[cur])
                if (!State.Cleared.Contains(to)) outs.Add(to);
            return outs;
        }

        /// <summary>노드에 진입한다. 전투 노드면 전투 화면으로, 아니면 즉시 통과.</summary>
        public static void Enter(int node)
        {
            if (!Active || node < 0 || node >= Plan.Nodes.Length) return;
            var n = Plan.Nodes[node];
            PendingNode = node;

            if (n.Kind == NodeKind.보스)
            {
                // ✅ §5 — 보스가 등장하면 지휘 조작이 열린다. 층수는 티어에서 만든다(§10-6 역산)
                GameFlow.GoBattle(GameFlow.Dungeon, GameFlow.BattleKind.보스, Plan.Tier * 10 + 5);
                return;
            }
            if (n.Wave != null)
            {
                GameFlow.GoBattle(GameFlow.Dungeon, GameFlow.BattleKind.던전, Plan.Tier * 10 + 1);
                return;
            }

            // 강화 노드는 화면에서 3택을 고른 뒤에 통과한다(DungeonScreen이 처리).
            if (n.Kind == NodeKind.강화) return;

            // 전투가 없는 노드(입구)는 그 자리에서 통과한다
            Complete(true);
        }

        /// <summary>
        /// 씬 전환 없이 노드만 지정한다 — 자가검사용.
        /// `Enter()`는 씬을 부르므로 배치모드에서 쓸 수 없다.
        /// </summary>
        public static void EnterForTest(int node)
        {
            if (Active && node >= 0 && node < Plan.Nodes.Length) PendingNode = node;
        }

        /// <summary>전투 결과를 반영한다. 이긴 노드만 클리어로 남는다.</summary>
        public static void Complete(bool won)
        {
            if (!Active || PendingNode < 0) return;
            if (won)
            {
                State.Cleared.Add(PendingNode);
                State.CurrentNode = PendingNode;
                if (Plan.Nodes[PendingNode].Kind == NodeKind.정예)
                {
                    var rng = Rng.Stream(Plan.RunSeed, State.ElitesKilled + 1, SeedChannel.Drop);
                    EliteDrop.Apply(NodeKind.정예, ref rng);
                    State.ElitesKilled++;
                }
            }
            PendingNode = -1;
        }

        /// <summary>지금 진입 중인 노드의 웨이브 편성. 전투 화면이 이걸 W3Party에 꽂는다.</summary>
        public static WavePlan PendingWave()
        {
            if (!Active || PendingNode < 0 || PendingNode >= Plan.Nodes.Length) return null;
            return Plan.Nodes[PendingNode].Wave;
        }

        /// <summary>이 노드의 3택 후보. 같은 노드를 다시 봐도 같은 후보가 나온다(시드 고정).</summary>
        public static List<BoonId> DrawBoons(int node) =>
            Active ? Boons.Draw(Plan.RunSeed, node, State.Boons) : new List<BoonId>();

        /// <summary>강화를 하나 가져간다. ✅ §7 — 던전을 나가면 사라진다(저장하지 않는다).</summary>
        public static void TakeBoon(BoonId id)
        {
            if (!Active) return;
            if (!State.Boons.Contains((int)id)) State.Boons.Add((int)id);
            Complete(true);
            Debug.Log($"[던전] 강화 획득: {Boons.Def(id).Name} (보유 {State.Boons.Count})");
        }

        /// <summary>가져갈 강화가 없을 때 그냥 통과.</summary>
        public static void TakeBoonSkip() => Complete(true);

        public static bool BossCleared =>
            Active && Plan.BossIndex >= 0 && State.Cleared.Contains(Plan.BossIndex);
    }
}
