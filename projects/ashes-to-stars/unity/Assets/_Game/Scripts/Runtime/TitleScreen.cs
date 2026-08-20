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
            if (!string.IsNullOrEmpty(LocalPlayKit.Line))
                Hint(new Rect(r.x, r.y + r.height - 22f, r.width, 20f), LocalPlayKit.Line);
            if (StarterPick.Open)
            {
                DrawStarterPick(r);
                return;
            }

            var copy = new Rect(r.x, r.y + 8f, r.width * 0.54f, r.height - 16f);
            // 소개 두 줄이 헤더 바로 아래에 붙어 좌측 열이 통째로 비어 화면이 위로 쏠려
            // 보였다(폴리싱 2026-08-20). 오른쪽 3카드는 열 전체 높이를 채우는데 왼쪽만
            // 상단에 몰려 균형이 깨졌다. 소개 블록을 좌측 열 세로 중앙에 놓아 맞춘다.
            const float titleH = 36f, gap = 8f, descH = 90f;
            float blockH = titleH + gap + descH;
            float by = copy.y + Mathf.Max(0f, (copy.height - blockH) * 0.5f);
            // 이 줄은 획득한 칭호(정상 정복·홀로 레이드)를 보여주는 자리다. 빈 상태
            // 폴백이 헤더 부제("…죽으면 캐릭터가 진짜 사라진다")와 **같은 문장**이라
            // 타이틀 화면에 같은 문구가 두 번 떴다(폴리싱 2026-08-20). 아직 칭호가 없다는
            // 빈 상태 문구로 바꿔 중복을 없앤다.
            Hint(new Rect(copy.x, by, copy.width, titleH),
                 TowerEnding.HasTitle ? TowerEnding.TitleName
                 : SoloRaidClear.HasAny ? SoloRaidClear.LastTitle
                 : "아직 정상에 오른 파티가 없다");
            Hint(new Rect(copy.x, by + titleH + gap, copy.width, descH),
                 TowerEnding.HasTitle
                     ? $"{TowerEnding.LookName} · 전투력은 그대로 · 100층을 다시 오를 수 있다(§8)"
                     : SoloRaidClear.HasAny
                         ? $"{SoloRaidClear.LookName} · 홀로 깬 레이드 {SoloRaidClear.Count} · 전투력은 그대로(§8)"
                         : "5인 파티를 키워 탑을 오른다. 목숨 3번이면 캐릭터와 장비가 영구 삭제된다.");

            var cells = UiPages.Grid(new Rect(r.x + r.width * 0.56f, r.y + 8f, r.width * 0.44f, r.height - 16f),
                1, 3, 14f);
            if (DrawCard(cells[0], "게임 시작",
                    StarterPick.ShouldOffer()
                        ? "기본직업 5종 중 첫 캐릭터를 고른다(오너 21:38)"
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
            // 종료는 아이콘이 없는 유틸리티 카드 — 위 두 카드(아이콘+좌측 글씨)와 좌측 여백이
            // 어긋나 보이지 않게 글씨를 카드 중앙에 둔다(center:true).
            if (DrawCard(cells[2], "종료", "게임을 닫는다", center: true))
                GameFlow.Quit();
        }

        void DrawStarterPick(Rect r)
        {
            Hint(new Rect(r.x, r.y, r.width, 24f),
                "첫 캐릭터 — 기본직업 5종. idle · 늘어남 없이 고른다");
            var cells = UiPages.StarterPickCards(new Rect(r.x, r.y + 28f, r.width, r.height - 28f));
            var jobs = StarterPick.Jobs;
            for (int i = 0; i < jobs.Length && i < cells.Length; i++)
            {
                string job = jobs[i];
                var card = cells[i];
                var tint = new Color(1f, 1f, 1f, 0.94f);
                if (!UiAtlas.DrawSliced(card, "panel", 16f, tint))
                    UiAtlas.Draw(card, "panel", tint);
                var inner = UiAtlas.ContentRect(card, "panel", 2f);
                var look = new Rect(inner.x, inner.y,
                    inner.width, Mathf.Max(24f, inner.height - UiPages.StarterLabelH));
                UiPages.DrawJobLook(look, job, UiPages.StarterLookWalks);
                Hint(new Rect(inner.x, inner.yMax - UiPages.StarterLabelH + 4f,
                        inner.width, UiPages.StarterLabelH - 4f),
                    LifeSystem.BasicJobLabel(job));
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)
                    && StarterPick.TryChoose(job))
                    GameFlow.Go(GameFlow.Estate);
            }
        }
    }
}
