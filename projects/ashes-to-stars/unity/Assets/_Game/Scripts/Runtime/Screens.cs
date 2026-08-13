using UnityEngine;

namespace AshesToStars
{
    // ─────────────────────────────────────────────────────────────
    // 기획서 §2(코어 루프)·§16(UI 화면 구조)의 골격을 실제로 걸어다닐 수 있게 만든 것.
    // 지금은 각 화면이 "무엇을 하는 곳인가"와 이동만 담는다 — 콘텐츠는 수직 슬라이스에서 채운다.
    // ⚠️ 여기에 전투 로직·수치를 넣지 마라. 수치의 출처는 언제나 §18 기준표와 ScriptableObject다.
    // ─────────────────────────────────────────────────────────────

    /// <summary>타이틀 — 시작과 종료. 하단바 없음.</summary>
    public class TitleScreen : GameScreen
    {
        protected override string Title => "재와 별";
        protected override string Subtitle => "Ashes to Stars — 죽으면 캐릭터가 진짜 사라진다";
        protected override bool ShowBottomBar => false;

        protected override void Body(Rect r)
        {
            if (Row(r, 0, "게임 시작", "영지에서 시작한다 (§16 허브는 영지)")) GameFlow.Go(GameFlow.Estate);
            if (Row(r, 1, "이어하기", "저장 슬롯 — 수직 슬라이스에서 구현")) GameFlow.Go(GameFlow.Estate);
            if (Row(r, 2, "종료", "Application.Quit")) GameFlow.Quit();
        }
    }

    /// <summary>영지 — 허브. 경매장·대장간·영묘는 건물을 눌러 들어간다(§16).</summary>
    public class EstateScreen : GameScreen
    {
        protected override string Title => "영지";
        protected override string Subtitle => "모든 콘텐츠의 출발점. 건물을 눌러 들어간다 — 메뉴를 늘리지 않는다(§13·§16)";

        protected override void Body(Rect r)
        {
            if (Row(r, 0, "대장간", "장비 제작·강화 (§11)")) GameFlow.Go(GameFlow.Character);
            if (Row(r, 1, "경매장", "탑 30층 달성 시 오픈 (§12)")) GameFlow.Go(GameFlow.Character);
            if (Row(r, 2, "영묘", "환생 — 삭제된 캐릭터의 귀환 (§4)")) GameFlow.Go(GameFlow.Character);
            if (Row(r, 3, "수비대 배치", "침략 방어 (§13-5)")) GameFlow.Go(GameFlow.WorldMap);
        }
    }

    /// <summary>필드 — 자동사냥. 코어 루프의 시작점(§2·§6).</summary>
    public class FieldScreen : GameScreen
    {
        protected override string Title => "필드";
        protected override string Subtitle => "자동사냥으로 재화를 번다. 단기 루프의 출발점(§2·§6)";

        protected override void Body(Rect r)
        {
            if (Row(r, 0, "사냥 시작", "잡몹은 자동, 보스는 수동 지휘(§5)"))
                GameFlow.GoBattle(GameFlow.Field);
            if (Row(r, 1, "던전 입장", "랜덤 생성 + 종점 보스 1체(§7)"))
                GameFlow.GoBattle(GameFlow.Field);
            if (Row(r, 2, "자동화 일정", "무엇을 언제 시킬지 예약(§6)")) { }
        }
    }

    /// <summary>탑 — 최대 100층. 10층 돌파마다 필드·던전 난이도가 오른다(§8·§10-6).</summary>
    public class TowerScreen : GameScreen
    {
        protected override string Title => "탑";
        protected override string Subtitle => "최대 100층. 10층 돌파마다 필드·던전 티어 상승(§8·§10-6)";

        protected override void Body(Rect r)
        {
            if (Row(r, 0, "다음 층 도전", "벽 콘텐츠 — 재도전 리듬(§8)"))
                GameFlow.GoBattle(GameFlow.Tower);
            if (Row(r, 1, "레이드 (5층 단위)", "5층마다 보스, 10층 단위는 대보스(§9)"))
                GameFlow.GoBattle(GameFlow.Tower);
        }
    }

    /// <summary>월드맵 — 우주 성계. 침략은 30층 달성 시 해금(§14·§15).</summary>
    public class WorldMapScreen : GameScreen
    {
        protected override string Title => "월드맵";
        protected override string Subtitle => "우주 성계. 침략은 탑 30층 달성 시 해금(§14·§15)";

        protected override void Body(Rect r)
        {
            if (Row(r, 0, "성계 이동", "영지 ↔ 월드맵 연결(§13-6)")) { }
            if (Row(r, 1, "침략", "비동기 PvP — 30층 해금(§15)"))
                GameFlow.GoBattle(GameFlow.WorldMap);
            if (Row(r, 2, "랭킹", "주 단위 장기 루프(§15)")) { }
        }
    }

    /// <summary>캐릭터 — 성장·전직·합성, 그리고 목숨 상태(§3·§4).</summary>
    public class CharacterScreen : GameScreen
    {
        protected override string Title => "캐릭터";
        protected override string Subtitle => "성장·전직·합성. 목숨 카운트가 여기서 보인다(§3·§4)";

        protected override void Body(Rect r)
        {
            if (Row(r, 0, "파티 편성", "탱1·딜2·힐1·버퍼1 — 1인은 불가(§9, W3에서 검증)")) { }
            if (Row(r, 1, "전직", "1차 전직 11종(§3)")) { }
            if (Row(r, 2, "합성", "안 쓰는 캐릭터 → 패시브 흡수(§3)")) { }
            if (Row(r, 3, "전투 스타일", "공격·균형·방어·생존 — 안전을 사면 효율을 잃는다(§3)")) { }
        }
    }

    /// <summary>
    /// 전투 — 지금은 자리표시자다. 실제 전투는 W1~W3 검증 빌드에 있고,
    /// 수직 슬라이스에서 이 화면으로 합류시킨다.
    /// </summary>
    public class BattleScreen : GameScreen
    {
        protected override string Title => "전투";
        protected override string Subtitle => "잡몹 자동 · 보스 수동 지휘(§5). 검증 구현은 W1~W3 빌드에 있다";
        protected override bool ShowBottomBar => false;

        float _t;
        protected override void Update()
        {
            base.Update();
            _t += Time.deltaTime;
        }

        protected override void Body(Rect r)
        {
            Info(r, 0, $"경과 {_t:F1}s");
            if (Row(r, 1, "승리 처리", "결과 화면으로"))
            {
                GameFlow.LastBattleSummary = $"승리 — {_t:F1}초";
                GameFlow.Go(GameFlow.Result);
            }
            if (Row(r, 2, "패배 처리", "사망 카운트 +1 (§4). 3회면 캐릭터 영구 삭제"))
            {
                GameFlow.LastBattleSummary = $"패배 — {_t:F1}초 · 사망 카운트 +1";
                GameFlow.Go(GameFlow.Result);
            }
            if (Row(r, 3, "후퇴", "긴급 탈출 아이템(§4)")) GameFlow.Go(GameFlow.ReturnTo);
        }
    }

    /// <summary>결과 — 전투가 끝나고 들어온 곳으로 돌아간다.</summary>
    public class ResultScreen : GameScreen
    {
        protected override string Title => "결과";
        protected override string Subtitle => "보상 정산 후 원래 화면으로";
        protected override bool ShowBottomBar => false;

        protected override void Body(Rect r)
        {
            Info(r, 0, string.IsNullOrEmpty(GameFlow.LastBattleSummary) ? "전투 기록 없음" : GameFlow.LastBattleSummary);
            if (Row(r, 1, "계속", "들어온 화면으로 복귀")) GameFlow.Go(GameFlow.ReturnTo);
            if (Row(r, 2, "영지로", "허브 복귀(§16)")) GameFlow.Go(GameFlow.Estate);
        }
    }
}
