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
        protected override string Title => FamilyAdv.Title();
        protected override string BackgroundArt => "bg_dungeon";

        protected override string Subtitle => DungeonRun.Active
            ? (FamilyAdv.ShowQa ? FamilyAdv.Line() + " · " : "") +
              (EliteKinds.ShowQa ? EliteKinds.Line() + " · " : "") +
              (MobSpeed.ShowQa ? MobSpeed.Line() + " · " : "") +
              (MobHp.ShowQa ? MobHp.Line() + " · " : "") +
              (MobDmg.ShowQa ? MobDmg.Line() + " · " : "") +
              $"시드 {DungeonRun.Plan.RunSeed} · T{DungeonRun.Plan.Tier + 1} · " +
              $"노드 {DungeonRun.State.Cleared.Count}/{DungeonRun.Plan.Nodes.Length} · " +
              $"{DungeonRun.Plan.Kind} · 보유 {GameState.WalletText}"
            : "진행 중인 던전이 없다";

        protected override bool ShowBottomBar => false;

        protected override void Body(Rect r)
        {
            FamilyAdv.SeedQaIfRequested();
            EliteKinds.SeedQaIfRequested();
            MobSpeed.SeedQaIfRequested();
            MobHp.SeedQaIfRequested();
            MobDmg.SeedQaIfRequested();
            if (!DungeonRun.Active)
            {
                Info(r, 0, "진행 중인 던전이 없다. 필드에서 던전에 입장할 수 있다(§7).");
                var empty = UiPages.Grid(new Rect(r.x, r.y + 88f, r.width, Mathf.Min(180f, r.height - 96f)), 1, 1, 12f);
                if (DrawCard(empty[0], "필드로", "돌아간다", "field")) GameFlow.Go(GameFlow.Field);
                return;
            }

            // 보스를 잡았으면 런은 끝났다 — ✅ §7 "던전을 나가면 초기화"
            if (DungeonRun.BossCleared)
            {
                Info(r, 0, "종점 보스를 처치했다 — 던전 클리어");
                Info(r, 1, "임시 강화는 여기서 사라진다(§7). 재화·드랍은 이미 지갑에 들어갔다.");
                var done = UiPages.Grid(new Rect(r.x, r.y + 160f, r.width, Mathf.Min(180f, r.height - 168f)), 1, 1, 12f);
                if (DrawCard(done[0], "던전 나가기", "필드로 돌아간다", "field"))
                {
                    DungeonRun.End();
                    GameFlow.Go(DungeonRun.ReturnScene);
                }
                return;
            }

            var plan = DungeonRun.Plan;
            var state = DungeonRun.State;

            Info(r, 0, "현재 위치: " + Label(state.CurrentNode) +
                       (state.ElitesKilled > 0 ? $"   (정예 처치 {state.ElitesKilled})" : "") +
                       (EliteDrop.ShowQa || !string.IsNullOrEmpty(EliteDrop.LastLine)
                           ? " · " + EliteDrop.Line() : ""));

            // 강화 노드에 들어와 있으면 3택을 고르기 전에는 다른 걸 못 한다 —
            // 고르지 않고 지나갈 수 있으면 그건 선택이 아니라 장식이다.
            if (DungeonRun.PendingNode >= 0 &&
                plan.Nodes[DungeonRun.PendingNode].Kind == NodeKind.강화)
            {
                var picks = DungeonRun.DrawBoons(DungeonRun.PendingNode);
                Info(r, 0, $"임시 강화 3택 — 던전을 나가면 사라진다(§7). 보유 {state.Boons.Count}개");
                var pickArea = HuntBoon.PickBand(new Rect(r.x, r.y + 80f, r.width, r.height - 80f));
                if (picks.Count == 0)
                {
                    Info(r, 1, "더 가져갈 강화가 없다 — 이미 전부 보유했다(§18-7 중복 제외)");
                    var skip = UiPages.Grid(pickArea, 1, 1, 12f);
                    if (DrawCard(skip[0], "통과", "다음 노드로", "field")) DungeonRun.TakeBoonSkip();
                    return;
                }
                var cells = UiPages.Grid(pickArea, Mathf.Max(1, picks.Count), 1, HuntBoon.CardGap);
                for (int i = 0; i < picks.Count && i < cells.Length; i++)
                {
                    var d = Boons.Def(picks[i]);
                    if (DrawCard(cells[i], d.Name, d.Desc, HuntBoon.IconOf(picks[i])))
                    {
                        DungeonRun.TakeBoon(picks[i]);
                        return;
                    }
                }
                return;
            }

            var next = DungeonRun.NextNodes();
            if (next.Count == 0)
            {
                Info(r, 1, "이 방향은 끝났다 — 돌아 나간다");
                var leave = UiPages.Grid(new Rect(r.x, r.y + 80f, r.width, r.height - 80f), 1, 1, 12f);
                if (DrawCard(leave[0], "던전 나가기", "재화는 유지, 강화는 초기화(§7)", "field"))
                {
                    DungeonRun.End();
                    GameFlow.Go(DungeonRun.ReturnScene);
                }
                return;
            }

            int cols = Mathf.Min(2, next.Count + 1);
            int rows = Mathf.CeilToInt((next.Count + 1) / (float)cols);
            // 노드가 2장뿐이면 전폭 밴드(r.height-80)가 카드를 본문 전체 높이(~560px)로
            // 늘려, 아이콘·글씨 묶음만 가운데 뜨고 카드 속이 텅 빈 화면이 됐다(폴리싱: 빈 여백).
            // 행당 상한 300px로 밴드를 잡아 본문 가운데에 놓는다 — 카드가 가로형(아이콘 왼쪽·
            // 글씨 오른쪽)으로 서서 게임의 나머지 허브 카드와 같은 비율로 읽힌다.
            float availH = r.height - 80f;
            float bandH = Mathf.Min(availH, rows * 300f + (rows - 1) * 12f);
            var band = new Rect(r.x, r.y + 80f + (availH - bandH) * 0.5f, r.width, bandH);
            var nodes = UiPages.Grid(band, cols, rows, 12f);
            for (int i = 0; i < next.Count && i < nodes.Length; i++)
            {
                int n = next[i];
                if (DrawCard(nodes[i], Label(n), Desc(plan.Nodes[n]), NodeIconKey(plan.Nodes[n].Kind)))
                {
                    DungeonRun.Enter(n);
                    return;
                }
            }
            int last = Mathf.Min(next.Count, nodes.Length - 1);
            if (last >= 0 && DrawCard(nodes[last], "던전 포기", "여기서 나간다 — 강화는 사라진다(§7)", "heart_broken"))
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

        /// <summary>노드 종류에 맞는 아이콘 키를 준다 — 전에는 전부 "tower"라 전투 노드에 등대가 떴다.</summary>
        static string NodeIconKey(NodeKind k)
        {
            switch (k)
            {
                case NodeKind.강화:     return "buffer";      // 임시 강화 3택 = 버프
                case NodeKind.보상분기: return "rarity_rare"; // 막다른 보상
                case NodeKind.보스:     return "tower";       // 종점 보스 = 탑 랜드마크(§5)
                default:                return "damage";      // 전투·정예 = 교전
            }
        }

        /// <summary>격투장 배치 템플릿을 한글로 읽어준다 — enum 이름(pockets 등)이 화면에 새는 것을 막는다.</summary>
        static string TemplateKo(ArenaTemplate t)
        {
            switch (t)
            {
                case ArenaTemplate.pillars:    return "기둥";
                case ArenaTemplate.choke:      return "병목";
                case ArenaTemplate.pockets:    return "엄폐";
                case ArenaTemplate.arena_wide: return "넓은 무대";
                default:                       return "열린 고리";
            }
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
                    return $"막다른 보상 · 동시 {n.Wave?.TargetCount ?? 0}체 · {TemplateKo(n.Template)}";
                case NodeKind.정예:
                    return EliteKinds.Caption(n);
                default:
                    return $"동시 {n.Wave?.TargetCount ?? 0}체 · 원거리 {n.Wave?.RangedPercent ?? 0:F0}% · {TemplateKo(n.Template)}";
            }
        }
    }
}
