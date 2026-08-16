using UnityEngine;
using System.Collections.Generic;

namespace AshesToStars
{
    // Unity는 MonoBehaviour마다 **클래스명과 같은 이름의 .cs 파일**을 요구한다.
    // 한 파일(Screens.cs)에 화면 8종을 넣었더니 Unity가 대표 클래스를 못 찾아
    // 첫 클래스(BattleRewardInfo)로 해석했고, 그것이 MonoBehaviour가 아니라서
    // 씬의 컴포넌트를 통째로 떼어냈다("references runtime script in scene file. Fixing!").
    // 그래서 클래스마다 파일을 나눈다 — 다시 합치지 마라.

    /// <summary>월드맵 — 우주 성계. 침략은 30층 달성 시 해금(§14·§15).</summary>
    public class WorldMapScreen : GameScreen
    {
        protected override string Title => "월드맵";
        protected override string HeaderIcon => UiAtlas.HeaderKey(GameFlow.WorldMap);
        protected override string BackgroundArt => "bg_worldmap";
        protected override string Subtitle => "우주 성계. 침략은 탑 30층 달성 시 해금(§14·§15)";

        /// <summary>침략 해금 층(§15 ✅ "탑 30층 이상 등반 시 해금"). 경매장과 **동시** 해금이다
        /// (SceneStructureBuilder "30층 돌파 → 침략·경매장 동시 해금").</summary>
        public const int InvasionUnlockFloor = 30;

        protected override void Body(Rect r)
        {
            Locked(r, 0, "성계 이동", "성계 시스템 미구현 — 지금은 영지·필드·탑만 오간다(§13-6)");

            // §15 ✅ 침략은 탑 30층 달성 시 해금 — "30층 미만은 공격도 수비도 없음 = 초보 보호".
            // 경매장(EstateScreen)은 이미 30층 게이트가 있는데(현재 층 표시) 침략만 무게이트라
            // 둘이 "동시 해금"이어야 하는데도 대칭이 깨져 있었다("고장난 게임" 계열의 거짓말).
            if (GameState.TowerFloor >= InvasionUnlockFloor)
            {
                if (Row(r, 1, "침략", "비동기 PvP — 상대 별을 침공한다(§15)"))
                    GameFlow.GoBattle(GameFlow.WorldMap);
            }
            else
            {
                Locked(r, 1, "침략",
                    $"탑 {InvasionUnlockFloor}층 달성 시 해금(현재 {GameState.TowerFloor}층) — 30층 미만은 초보 보호(§15)");
            }

            Locked(r, 2, "랭킹", "랭킹 서버 없음 — 온라인 기능이다(§15)");
        }
    }
}
