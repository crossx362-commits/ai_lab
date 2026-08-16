using UnityEngine;

namespace AshesToStars
{
    // Unity는 MonoBehaviour마다 클래스명과 같은 이름의 .cs 파일을 요구한다 — 합치지 마라.

    /// <summary>
    /// 던전 노드 맵 (명세 S6).
    ///
    /// 걸어다니는 복도는 없다(✅ §7 뱀서류) — 던전은 **아레나 노드의 방향성 그래프**이고
    /// 이 화면이 그 그래프다. 지금 어디에 있고 다음에 어디로 갈지만 고르면 된다.
    ///
    /// 시드를 화면에 그대로 노출한다. 영구사망 게임에서 "그 판이 어땠는지"를 나중에
    /// 재현할 수 있어야 하고, 그러려면 플레이어도 자기 시드를 볼 수 있어야 한다(§3-2 규칙 4).
    /// </summary>
    public class DungeonScreen : GameScreen
    {
        protected override string Title => DungeonRun.Active
            ? $"던전 · {DungeonRun.Plan.Family} 계열" : "던전";
        protected override string BackgroundArt => "bg_dungeon";

        protected override string Subtitle => DungeonRun.Active
            ? $"시드 {DungeonRun.Plan.RunSeed} · T{DungeonRun.Plan.Tier + 1} · " +
              $"노드 {DungeonRun.State.Cleared.Count}/{DungeonRun.Plan.Nodes.Length} · " +
              $"{DungeonRun.Plan.Kind} · 보유 {GameState.WalletText}"
            : "진행 중인 던전이 없다";

        protected override bool ShowBottomBar => false;

        protected override void Body(Rect r)
        {
            if (!DungeonRun.Active)
            {
                Info(r, 0, "진행 중인 던전이 없다. 필드에서 던전에 입장할 수 있다(§7).");
                if (Row(r, 1, "필드로", "돌아간다")) GameFlow.Go(GameFlow.Field);
                return;
            }

            // 보스를 잡았으면 런은 끝났다 — ✅ §7 "던전을 나가면 초기화"
            if (DungeonRun.BossCleared)
            {
                Info(r, 0, "종점 보스를 처치했다 — 던전 클리어");
                Info(r, 1, "임시 강화는 여기서 사라진다(§7). 재화·드랍은 이미 지갑에 들어갔다.");
                if (Row(r, 2, "던전 나가기", "필드로 돌아간다"))
                {
                    DungeonRun.End();
                    GameFlow.Go(DungeonRun.ReturnScene);
                }
                return;
            }

            var plan = DungeonRun.Plan;
            var state = DungeonRun.State;

            Info(r, 0, "현재 위치: " + Label(state.CurrentNode) +
                       (state.ElitesKilled > 0 ? $"   (정예 처치 {state.ElitesKilled})" : ""));

            // 강화 노드에 들어와 있으면 3택을 고르기 전에는 다른 걸 못 한다 —
            // 고르지 않고 지나갈 수 있으면 그건 선택이 아니라 장식이다.
            if (DungeonRun.PendingNode >= 0 &&
                plan.Nodes[DungeonRun.PendingNode].Kind == NodeKind.강화)
            {
                var picks = DungeonRun.DrawBoons(DungeonRun.PendingNode);
                Info(r, 0, $"임시 강화 3택 — 던전을 나가면 사라진다(§7). 보유 {state.Boons.Count}개");
                if (picks.Count == 0)
                {
                    // 8종을 전부 가져간 경우. 없는 걸 지어내지 않고 그대로 통과시킨다.
                    Info(r, 1, "더 가져갈 강화가 없다 — 이미 전부 보유했다(§18-7 중복 제외)");
                    if (Row(r, 2, "통과", "다음 노드로")) DungeonRun.TakeBoonSkip();
                    return;
                }
                for (int i = 0; i < picks.Count; i++)
                {
                    var d = Boons.Def(picks[i]);
                    if (Row(r, i + 1, d.Name, d.Desc)) { DungeonRun.TakeBoon(picks[i]); return; }
                }
                return;
            }

            var next = DungeonRun.NextNodes();
            int row = 1;
            if (next.Count == 0)
            {
                // 구성으로 완주를 보장하므로(G2) 여기 오는 것은 막다른 보상 분기를 다 턴 경우뿐이다.
                Info(r, row++, "이 방향은 끝났다 — 돌아 나간다");
                if (Row(r, row++, "던전 나가기", "재화는 유지, 강화는 초기화(§7)"))
                {
                    DungeonRun.End();
                    GameFlow.Go(DungeonRun.ReturnScene);
                }
                return;
            }

            foreach (int n in next)
            {
                var node = plan.Nodes[n];
                if (Row(r, row++, Label(n), Desc(node)))
                {
                    DungeonRun.Enter(n);
                    // 전투가 없는 노드는 Enter가 그 자리에서 통과시키므로 화면만 다시 그리면 된다
                    return;
                }
            }

            if (Row(r, row, "던전 포기", "여기서 나간다 — 강화는 사라진다(§7)"))
            {
                DungeonRun.End();
                GameFlow.Go(DungeonRun.ReturnScene);
            }
        }

        static string Label(int i)
        {
            if (!DungeonRun.Active || i < 0 || i >= DungeonRun.Plan.Nodes.Length) return "?";
            var n = DungeonRun.Plan.Nodes[i];
            string mark = n.Optional ? " (선택)" : "";
            return $"{i}. {n.Kind}{mark}";
        }

        /// <summary>노드를 고르기 전에 무엇이 기다리는지 보여준다 — 고를 근거가 없으면 선택이 아니다.</summary>
        static string Desc(DungeonNode n)
        {
            switch (n.Kind)
            {
                case NodeKind.보스:
                    return $"종점 보스 {DungeonRun.Plan.BossCount}체 · 기믹 · 수동 지휘가 열린다(§5·§10-1)";
                case NodeKind.강화:
                    return "임시 강화 3택 — 던전을 나가면 사라진다(§7)";
                case NodeKind.보상분기:
                    return $"막다른 보상 · 동시 {n.Wave?.TargetCount ?? 0}체 · {n.Template}";
                case NodeKind.정예:
                    return $"정예 아레나 · 동시 {n.Wave?.TargetCount ?? 0}체 · " +
                           $"정예 {n.Wave?.ElitePercent ?? 0:F0}% · {n.Template}";
                default:
                    return $"동시 {n.Wave?.TargetCount ?? 0}체 · 원거리 {n.Wave?.RangedPercent ?? 0:F0}% · {n.Template}";
            }
        }
    }
}
