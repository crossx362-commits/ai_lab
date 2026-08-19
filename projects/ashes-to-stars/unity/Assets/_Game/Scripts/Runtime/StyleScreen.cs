using UnityEngine;

namespace AshesToStars
{
    // Unity는 MonoBehaviour마다 클래스명과 같은 이름의 .cs 파일을 요구한다 — 합치지 마라.

    /// <summary>
    /// 전투 스타일 선택 (§3 전투 스타일 표).
    ///
    /// 왜 이 화면이 필요했나:
    ///   `CombatStyleDef` 에셋을 살리고 W3Party가 그 수치를 읽게 배선까지 끝냈는데,
    ///   정작 **플레이어가 스타일을 고를 화면이 없었다.** W3Party의 `_style`은 파티
    ///   전체 공용 필드에 균형형으로 고정돼 있어서, 네 가지 스타일 중 셋은 게임에서
    ///   한 번도 사용되지 않았다 — 데이터만 있고 선택이 없는 상태였다.
    ///
    /// 이 화면은 **직업별로 따로** 고르게 한다. 파티 전체를 한 값으로 묶으면
    /// "탱커는 방어형, 딜러는 공격형" 같은 조합 자체가 성립하지 않아 선택의 폭이
    /// 4가지로 줄어든다. 직업별이면 §21-1i가 측정한 「구성이 생존을 가른다」가
    /// 스타일 축에서도 성립한다.
    /// </summary>
    public class StyleScreen : GameScreen
    {
        protected override string Title => "전투 스타일";
        protected override string BackgroundArt => "bg_party";
        protected override string Subtitle =>
            "직업마다 따로 고른다(§3) · 선택은 저장되며 다음 전투부터 적용된다";

        /// <summary>W3Party가 편성하는 5직업. 순서는 진형 순(탱 → 딜 → 후열)이다.</summary>
        static readonly string[] Jobs = { "수호기사", "검사", "마법사", "사제", "음유시인" };

        static readonly StyleId[] Styles =
        {
            StyleId.공격형, StyleId.균형형, StyleId.방어형, StyleId.생존형,
        };

        int _sel;      // 지금 펼쳐 놓은 직업. 한 번에 하나만 열어 화면이 넘치지 않게 한다

        protected override void Body(Rect r)
        {
            var jobCells = UiPages.Grid(new Rect(r.x, r.y, r.width, 108f), Jobs.Length, 1, 10f);
            for (int i = 0; i < Jobs.Length; i++)
            {
                string job = Jobs[i];
                var cur = CombatStylePrefs.Get(job);
                bool open = _sel == i;
                if (DrawCard(jobCells[i], job, $"{cur}", UiAtlas.RoleKey(job)))
                    _sel = open ? -1 : i;
            }

            if (_sel >= 0 && _sel < Jobs.Length)
            {
                string job = Jobs[_sel];
                var cur = CombatStylePrefs.Get(job);
                // 하단 DrawChoice(이펙트 테스트·돌아가기)는 r.yMax-168 부터 그린다. 그리드 바닥이
                // 그 위로 오도록 높이를 잡는다 — 예전엔 -240이라 2행 카드 바닥 50px이 선택 바에
                // 깔려, 스타일 수치 한 줄만 있을 땐 안 보이던 겹침이 §3 행동설명 둘째 줄을 잘랐다.
                var styleCells = UiPages.Grid(new Rect(r.x, r.y + 122f, r.width, r.height - 300f), 2, 2, 12f);
                for (int s = 0; s < Styles.Length && s < styleCells.Length; s++)
                {
                    var id = Styles[s];
                    bool on = id == cur;
                    if (DrawCard(styleCells[s], (on ? "● " : "") + id.ToString(), StatLine(id),
                            on ? "heart" : "damage"))
                    {
                        CombatStylePrefs.Set(job, id);
                        _sel = -1;
                    }
                }
            }

            if (DrawChoice(r, "이펙트 테스트", "생성한 전투 이펙트를 한 화면에서 확인", "damage",
                           "돌아가기", "파티 편성으로", "characters", out bool back))
                GameFlow.Go(GameFlow.VfxTest);
            else if (back)
                GameFlow.Go(GameFlow.Party);
        }

        /// <summary>
        /// 수치를 에셋에서 읽어 보여준다. 화면이 코드 상수를 따로 들고 있으면
        /// 기획자가 인스펙터에서 값을 고쳤을 때 **화면만 옛 숫자를 말한다** —
        /// 이 저장소가 이미 겪은 「두 곳이 같은 기획을 다르게 구현」 사고다.
        ///
        /// §3 스타일 표의 「행동」열(공격형=회피 시도 안 함 … 생존형=HP 50%에 즉시 이탈)은
        /// `CombatStyleDef.행동설명`에 스타일마다 authored돼 있으나 **읽는 코드가 0곳**이라
        /// 화면에 한 번도 안 떴다(이 저장소가 반복한 「정의만 있고 부르는 곳 없다」 함정 —
        /// 컨셉·스킬·사망리스크와 동일 계열). 같은 에셋을 이미 로드하는 이 소비처에서
        /// 수치 아래 한 줄로 붙인다. 값이 비면 지어내지 않고 붙이지 않는다.
        /// </summary>
        static string StatLine(StyleId id)
        {
            var defs = Resources.LoadAll<CombatStyleDef>("styles");
            foreach (var d in defs)
            {
                if (d.Id != id) continue;
                string stat = $"딜 ×{d.딜배율:0.00} · 받는 피해 ×{d.피해배율:0.00} · " +
                              $"후퇴 {d.후퇴체력 * 100f:0}% · 거리 {d.유지거리:0.0}";
                return string.IsNullOrEmpty(d.행동설명) ? stat : stat + "\n" + d.행동설명;
            }
            // 에셋을 못 읽으면 숫자를 **지어내지 않는다.** 설명만 보여주고 사실을 말한다.
            return CombatStylePrefs.Describe(id) + " (수치 에셋을 못 읽었다)";
        }
    }
}
