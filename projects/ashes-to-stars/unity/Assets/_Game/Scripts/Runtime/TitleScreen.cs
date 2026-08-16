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
            var copy = new Rect(r.x, r.y + 8f, r.width * 0.54f, r.height - 16f);
            Hint(new Rect(copy.x, copy.y, copy.width, 36f), "죽으면 캐릭터가 진짜 사라진다");
            Hint(new Rect(copy.x, copy.y + 44f, copy.width, 90f),
                 "5인 파티를 키워 탑을 오른다. 목숨 3번이면 캐릭터와 장비가 영구 삭제된다.");

            var cells = UiPages.Grid(new Rect(r.x + r.width * 0.56f, r.y + 8f, r.width * 0.44f, r.height - 16f),
                1, 3, 14f);
            if (DrawCard(cells[0], "게임 시작", "영지에서 모든 콘텐츠가 출발한다", "territory"))
                GameFlow.Go(GameFlow.Estate);
            DrawCard(cells[1], "이어하기", "저장 슬롯 없음 — 시작이 곧 이어하기", "characters", locked: true);
            if (DrawCard(cells[2], "종료", "게임을 닫는다"))
                GameFlow.Quit();
        }
    }
}
