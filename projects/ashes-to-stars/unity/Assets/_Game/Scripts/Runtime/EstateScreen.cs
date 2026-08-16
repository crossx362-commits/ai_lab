using UnityEngine;
using System.Collections.Generic;

namespace AshesToStars
{
    // Unity는 MonoBehaviour마다 **클래스명과 같은 이름의 .cs 파일**을 요구한다.
    // 한 파일(Screens.cs)에 화면 8종을 넣었더니 Unity가 대표 클래스를 못 찾아
    // 첫 클래스(BattleRewardInfo)로 해석했고, 그것이 MonoBehaviour가 아니라서
    // 씬의 컴포넌트를 통째로 떼어냈다("references runtime script in scene file. Fixing!").
    // 그래서 클래스마다 파일을 나눈다 — 다시 합치지 마라.

    /// <summary>
    /// 영지 — 허브. 경매장·대장간·영묘는 **영지 안 건물**로 들어간다(§16).
    /// 씬을 새로 파지 않고 하위 화면으로 두는 것이 기획 의도다 —
    /// "메뉴를 늘리지 않고 영지를 실제로 쓰게 만드는 배치"(§16).
    /// </summary>
    public class EstateScreen : GameScreen
    {
        enum Sub { 없음, 대장간, 경매장, 영묘, 수비대 }
        Sub _sub = Sub.없음;

        protected override string Title => _sub == Sub.없음 ? "영지" : $"영지 · {_sub}";
        protected override string HeaderIcon => "territory";
        protected override string BackgroundArt => "bg_estate";
        protected override string Subtitle => _sub switch
        {
            Sub.대장간 => "사냥해서 얻은 재료로 만든다. 강화는 실패해도 파괴되지 않는다(§11)",
            Sub.경매장 => "탑 30층 달성 시 오픈. 골드는 곧 목숨이라 거래가 성립한다(§12)",
            Sub.영묘 => "환생석으로 삭제된 캐릭터를 되돌린다. 장비는 함께 돌아오지 않는다(§4)",
            Sub.수비대 => "침략에 맞설 캐릭터를 세운다. 수비대도 죽으면 사라진다(§13-5)",
            _ => "모든 콘텐츠의 출발점. 건물을 눌러 들어간다 — 메뉴를 늘리지 않는다(§13·§16)",
        };

        /// <summary>
        /// 시각 QA가 **건물 안쪽까지** 찍을 수 있게 하는 진입점(`GAME_START=estate`).
        /// 이게 없으면 자동 조종은 허브만 찍고 끝나서, 정작 채운 화면은 한 번도 안 보인다 —
        /// 이 프로젝트에서 "렌더는 되는데 아무도 안 본 화면"이 반복해서 나온 이유다.
        /// </summary>
        public static string AutoOpen;

        protected override void Update()
        {
            // 하위 화면에서 ESC는 영지로 — 허브 밖으로 튕겨나가지 않게
            if (_sub != Sub.없음 && Input.GetKeyDown(KeyCode.Escape)) { _sub = Sub.없음; return; }
            base.Update();
        }

        protected override void Body(Rect r)
        {
            if (!string.IsNullOrEmpty(AutoOpen))
            {
                if (System.Enum.TryParse(AutoOpen, out Sub want)) _sub = want;
                AutoOpen = null;                 // 한 번만 — 이후엔 사람이 조작한다
            }

            if (_sub == Sub.영묘) { Mausoleum(r); return; }

            if (_sub != Sub.없음)
            {
                // 제네릭 "아직 내용이 없다"는 "고장난 게임"으로 읽힌다(오너 지시). 영묘가
                // 이미 있는 시스템으로 성립한 것과 달리, 아래 셋은 **소비할 시스템 자체가
                // 없어서** 비어 있다 — 왜 비었는지·무엇이 있어야 채워지는지 건물마다 밝힌다.
                // 채우면서도 소비처가 없으면 그 순간 또 "눌러도 아무 일 없는" 거짓말이 된다.
                string why = _sub switch
                {
                    Sub.대장간 => "장비·재료 시스템이 아직 없다 — 사냥 전리품으로 제작·강화하는 " +
                                  "구조를 먼저 만들어야 채워진다(§11)",
                    Sub.경매장 => $"탑 30층을 달성해야 열린다(현재 {GameState.TowerFloor}층) — " +
                                  "골드가 곧 목숨이라 거래가 성립한다. 온라인 거래 서버가 필요하다(§12)",
                    Sub.수비대 => "침략(월드맵)이 아직 수비 배치를 소비하지 않는다 — 배치를 세워도 " +
                                  "방어에 반영되지 않아, 아무 일 없는 척하지 않고 비워 둔다(§13-5)",
                    _ => "아직 내용이 없다 — 수직 슬라이스에서 채운다(§21-2)",
                };
                Info(r, 0, why);
                if (Row(r, 1, "← 영지로", "건물에서 나온다")) _sub = Sub.없음;
                return;
            }

            if (Row(r, 0, "대장간", "장비 제작·강화 (§11)", UiAtlas.BuildingKey("대장간"))) _sub = Sub.대장간;
            if (Row(r, 1, "경매장", "탑 30층 달성 시 오픈 (§12)", UiAtlas.BuildingKey("경매장"))) _sub = Sub.경매장;
            if (Row(r, 2, "영묘", $"환생 — 삭제된 캐릭터의 귀환 · 환생석 {LifeSystem.GetRebornStones()}개 (§4)",
                    UiAtlas.BuildingKey("영묘")))
                _sub = Sub.영묘;
            if (Row(r, 3, "수비대 배치", "침략 방어 (§13-5)", UiAtlas.BuildingKey("수비대"))) _sub = Sub.수비대;
        }

        string _msg = "";

        /// <summary>
        /// 영묘 — 환생석으로 삭제된 캐릭터를 되돌린다(§4).
        ///
        /// 영지 4건물 중 여기를 먼저 채운 이유: **이미 있는 시스템만으로 성립한다.**
        /// 삭제된 캐릭터(`IsDeleted`)도, 환생석(`Economy.LifeItem.RebornStone`)도,
        /// 소모 API(`GameState.Consume`)도 전부 있었는데 **화면만 없었다** — 이 저장소가
        /// 반복해서 겪는 「정의는 있고 부르는 곳이 없다」의 또 한 사례다.
        /// 대장간(§11)은 장비·재료 시스템이 통째로 없어 한 이터레이션에 끝나지 않는다.
        ///
        /// 삭제가 **되돌릴 수 있는 것**이 되면 목숨 시스템(§4)의 무게가 사라지므로,
        /// 되돌아온 캐릭터는 사망 0에서 다시 시작하고 환생석 자체를 희소하게 둔다.
        /// </summary>
        void Mausoleum(Rect r)
        {
            var dead = LifeSystem.GetDeletedCharacters();
            int stones = LifeSystem.GetRebornStones();
            int row = 0;

            Info(r, row++, $"환생석 {stones}개 · 잠든 캐릭터 {dead.Count}명");

            if (dead.Count == 0)
            {
                // 빈 목록을 조용히 그리지 않는다 — "왜 아무것도 없나"에 답해 준다
                Info(r, row++, "잠든 캐릭터가 없다 — 3회 사망한 캐릭터만 이곳에 온다(§4)");
            }
            else
            {
                foreach (var ch in dead)
                {
                    string desc = stones > 0
                        ? "환생석 1개를 써서 되돌린다 — 사망 0에서 다시 시작한다"
                        : "환생석이 없다 — 10층 보스가 떨어뜨린다";
                    if (Row(r, row++, $"{ch.Name} · {ch.Job} Lv{ch.Level}", desc))
                    {
                        if (stones <= 0) _msg = "환생석이 없다. 10층 보스를 잡아야 한다(§4)";
                        else if (LifeSystem.UseRebornStone(ch))
                        {
                            _msg = $"{ch.Name}이(가) 돌아왔다 — 사망 0에서 다시 시작한다";
                            return;                     // 목록이 바뀌었으니 이번 프레임은 여기서 끝
                        }
                        else _msg = "환생에 실패했다 — 환생석 소모를 확인할 것";
                    }
                }
            }

            if (!string.IsNullOrEmpty(_msg)) Info(r, row++, _msg);
            if (Row(r, row, "← 영지로", "건물에서 나온다")) { _sub = Sub.없음; _msg = ""; }
        }
    }
}
