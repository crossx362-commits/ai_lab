using UnityEngine;
using System.Collections.Generic;

namespace AshesToStars
{
    // Unity는 MonoBehaviour마다 **클래스명과 같은 이름의 .cs 파일**을 요구한다.
    // 한 파일(Screens.cs)에 화면 8종을 넣었더니 Unity가 대표 클래스를 못 찾아
    // 첫 클래스(BattleRewardInfo)로 해석했고, 그것이 MonoBehaviour가 아니라서
    // 씬의 컴포넌트를 통째로 떼어냈다("references runtime script in scene file. Fixing!").
    // 그래서 클래스마다 파일을 나눈다 — 다시 합치지 마라.

    /// <summary>타이틀 — 시작과 종료. 하단바 없음.</summary>
    public class TitleScreen : GameScreen
    {
        protected override void Awake()
        {
            base.Awake();
            // `--auto dungeon`이 붙어 있을 때만 무인 스모크가 붙는다. 평소엔 아무 일도 없다.
            DebugAutoPilot.BootstrapIfRequested();
            GameFlow.RestorePlayRosterIfRequested();
        }

        protected override string Title => "재와 별";
        protected override string Subtitle => "Ashes to Stars — 죽으면 캐릭터가 진짜 사라진다";
        protected override string BackgroundArt => "bg_title";
        protected override bool ShowBottomBar => false;

        protected override void Body(Rect r)
        {
            TowerEnding.SeedQaIfRequested();
            SoloRaidClear.SeedQaIfRequested();
            StarterPick.SeedQaIfRequested();
            if (StarterPick.Open)
            {
                DrawStarterPick(r);
                return;
            }

            var copy = new Rect(r.x, r.y + 8f, r.width * 0.54f, r.height - 16f);
            Hint(new Rect(copy.x, copy.y, copy.width, 36f),
                 TowerEnding.HasTitle ? TowerEnding.TitleName
                 : SoloRaidClear.HasAny ? SoloRaidClear.LastTitle
                 : "죽으면 캐릭터가 진짜 사라진다");
            Hint(new Rect(copy.x, copy.y + 44f, copy.width, 90f),
                 TowerEnding.HasTitle
                     ? $"{TowerEnding.LookName} · 전투력은 그대로 · 100층을 다시 오를 수 있다(§8)"
                     : SoloRaidClear.HasAny
                         ? $"{SoloRaidClear.LookName} · 홀로 깬 레이드 {SoloRaidClear.Count} · 전투력은 그대로(§8)"
                         : "5인 파티를 키워 탑을 오른다. 목숨 3번이면 캐릭터와 장비가 영구 삭제된다.");

            var cells = UiPages.Grid(new Rect(r.x + r.width * 0.56f, r.y + 8f, r.width * 0.44f, r.height - 16f),
                1, 3, 14f);
            if (DrawCard(cells[0], "게임 시작",
                    StarterPick.ShouldOffer()
                        ? "기본직업 4종 중 첫 캐릭터를 고른다(§3)"
                        : "영지에서 모든 콘텐츠가 출발한다",
                    "territory"))
            {
                if (StarterPick.ShouldOffer()) StarterPick.Request();
                else GameFlow.Go(GameFlow.Estate);
            }
            bool hasSave = LifeSystem.HasSavedRoster();
            if (DrawCard(cells[1], "이어하기",
                    hasSave ? "저장된 명부로 영지에 들어간다" : "저장이 없다 — 게임 시작에서 직업을 고른다",
                    "characters", locked: !hasSave)
                && hasSave)
                GameFlow.Go(GameFlow.Estate);
            if (DrawCard(cells[2], "종료", "게임을 닫는다"))
                GameFlow.Quit();
        }

        void DrawStarterPick(Rect r)
        {
            Hint(new Rect(r.x, r.y, r.width, 28f),
                "첫 캐릭터 — 기본직업 4종. 전신과 걷기가 서로 다르다(§3)");
            var cells = UiPages.Grid(new Rect(r.x, r.y + 36f, r.width, r.height - 36f), 2, 2, 14f);
            var jobs = StarterPick.Jobs;
            for (int i = 0; i < jobs.Length && i < cells.Length; i++)
            {
                string job = jobs[i];
                var card = cells[i];
                var tint = new Color(1f, 1f, 1f, 0.94f);
                if (!UiAtlas.DrawSliced(card, "panel", 16f, tint))
                    UiAtlas.Draw(card, "panel", tint);
                float lookH = Mathf.Max(80f, card.height - 88f);
                UiPages.DrawJobLook(new Rect(card.x + 16f, card.y + 10f, card.width - 32f, lookH),
                    job, true);
                Hint(new Rect(card.x + 12f, card.yMax - 70f, card.width - 24f, 28f),
                    LifeSystem.BasicJobLabel(job));
                Hint(new Rect(card.x + 12f, card.yMax - 42f, card.width - 24f, 28f),
                    StarterPick.Blurb(job));
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)
                    && StarterPick.TryChoose(job))
                    GameFlow.Go(GameFlow.Estate);
            }
        }
    }
}
