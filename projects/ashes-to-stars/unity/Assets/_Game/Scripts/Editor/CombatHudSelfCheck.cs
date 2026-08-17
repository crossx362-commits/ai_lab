using UnityEngine;

namespace AshesToStars
{
    /// <summary>전투 HUD가 전장과 서로의 안전 영역을 침범하지 않게 하는 계약 검사다.</summary>
    public static class CombatHudSelfCheck
    {
        public static void Run()
        {
            Debug.Assert(W3Party.CombatHudTopHeight <= 72f,
                "[전투 HUD] 상단 정보는 72px를 넘기면 전장을 가린다");
            Debug.Assert(W3Party.CombatHudBottomHeight >= W3Party.CombatHudCardH,
                "[전투 HUD] 하단 지휘 영역은 카드와 스킬을 모두 수용해야 한다");
            Debug.Assert(W3Party.CombatHudCardH >= 160f,
                "[전투 HUD] 카드가 낮으면 스킬·체력바가 깨알이 된다");
            Debug.Assert(W3Party.CombatHudHpH >= 20f,
                "[전투 HUD] 체력바는 숫자와 함께 읽힐 두께여야 한다");
            Debug.Assert(W3Party.CombatHudSkillMin >= 52f,
                "[전투 HUD] 스킬 버튼은 초상 옆을 채울 크기여야 한다");
            Debug.Assert(W3Party.CombatHudRewardMaxEntries == 3,
                "[전투 HUD] 보상 레인은 최대 세 항목만 보여야 한다");
            Debug.Assert(Mathf.Approximately(W3Party.CombatHudRewardLifetime, 2.2f),
                "[전투 HUD] 보상 문구는 오래 남아 전장을 가리면 안 된다");
            Debug.Assert(W3Party.CombatHudRailW >= 220f,
                "[전투 HUD] 오른쪽 칸이 좁으면 획이 잘린다");
            Debug.Assert(W3Party.CombatHudStreakLine(7) == "연속 7"
                         && !W3Party.CombatHudStreakLine(7).Contains("KILL"),
                "[전투 HUD] 연속 처치는 짧은 한국어여야 한다");
            Debug.Assert(W3Party.CombatHudRateLine(10, 10f) == "분당 60"
                         && !W3Party.CombatHudRateLine(10, 10f).Contains("획득"),
                "[전투 HUD] 분당은 '획득 속도'처럼 길면 잘린다");
            Debug.Assert(W3Party.CombatHudRewardLine(12, 4, 3) == "골드 +12  경험 +4  ×3",
                "[전투 HUD] 같은 보상은 한 줄로 합친다");
            var rail = new Rect(0f, 0f, W3Party.CombatHudRailW, 30f);
            var well = UiAtlas.ContentRect(rail, "panel", 2f);
            Debug.Assert(well.width >= 140f,
                "[전투 HUD] 보상 칸 안쪽이 너무 좁으면 글씨가 잘린다");
            Debug.Assert(!W3Party.CombatHudUsesFullWidthPanels,
                "[전투 HUD] 상·하단 전체 폭 패널은 전장을 가린다");
            Debug.Assert(W3Party.CombatSkillFxScale >= 1.35f,
                "[전투 HUD] 스킬 이펙트는 몬스터 무리에서도 읽힐 크기여야 한다");
            Debug.Log("[CombatHudSelfCheck] PASS");
        }
    }
}
