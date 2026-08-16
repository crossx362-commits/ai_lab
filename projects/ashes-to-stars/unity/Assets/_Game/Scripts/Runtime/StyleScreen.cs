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
            int row = 0;

            for (int i = 0; i < Jobs.Length; i++)
            {
                string job = Jobs[i];
                var cur = CombatStylePrefs.Get(job);
                bool open = _sel == i;

                if (Row(r, row++, (open ? "▼ " : "▶ ") + job,
                        $"{cur} — {CombatStylePrefs.Describe(cur)}"))
                    _sel = open ? -1 : i;

                if (!open) continue;

                // 펼친 직업만 4가지 선택지를 보여준다. 고른 것에는 ● 를 붙여
                // **지금 무엇이 켜져 있는지**가 목록 안에서 바로 읽히게 한다.
                for (int s = 0; s < Styles.Length; s++)
                {
                    var id = Styles[s];
                    bool on = id == cur;
                    if (Row(r, row++, (on ? "      ● " : "      ○ ") + id, StatLine(id)))
                    {
                        CombatStylePrefs.Set(job, id);
                        _sel = -1;      // 고르면 접는다 — 목록이 길어지면 아래 버튼이 밀린다
                    }
                }
            }

            if (Row(r, row++, "이펙트 테스트", "생성한 전투 이펙트를 한 화면에서 확인")) GameFlow.Go(GameFlow.VfxTest);
            if (Row(r, row, "돌아가기", "파티 편성으로")) GameFlow.Go(GameFlow.Party);
        }

        /// <summary>
        /// 수치를 에셋에서 읽어 보여준다. 화면이 코드 상수를 따로 들고 있으면
        /// 기획자가 인스펙터에서 값을 고쳤을 때 **화면만 옛 숫자를 말한다** —
        /// 이 저장소가 이미 겪은 「두 곳이 같은 기획을 다르게 구현」 사고다.
        /// </summary>
        static string StatLine(StyleId id)
        {
            var defs = Resources.LoadAll<CombatStyleDef>("styles");
            foreach (var d in defs)
            {
                if (d.Id != id) continue;
                return $"딜 ×{d.딜배율:0.00} · 받는 피해 ×{d.피해배율:0.00} · " +
                       $"후퇴 {d.후퇴체력 * 100f:0}% · 거리 {d.유지거리:0.0}";
            }
            // 에셋을 못 읽으면 숫자를 **지어내지 않는다.** 설명만 보여주고 사실을 말한다.
            return CombatStylePrefs.Describe(id) + " (수치 에셋을 못 읽었다)";
        }
    }
}
