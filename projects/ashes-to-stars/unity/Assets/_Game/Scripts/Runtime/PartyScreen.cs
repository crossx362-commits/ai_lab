using UnityEngine;

namespace AshesToStars
{
    // Unity는 MonoBehaviour마다 클래스명과 같은 이름의 .cs 파일을 요구한다 — 합치지 마라.

    /// <summary>
    /// 파티 편성 (명세 S2·S3 · 기획서 §3·§9·§16).
    ///
    /// 이 화면이 필요한 이유는 취향이 아니라 측정이다 — §21-1i에서 구성이 생존을 가른다는 것이
    /// 5회 중앙값으로 확정됐다(딜특화 0.72배·힐러없음 0.67배). 그 선택을 플레이어에게 준다.
    ///
    /// 목숨 시스템(§4)과 맞물린다: 회복 중·삭제된 캐릭터는 **넣을 수 없고**,
    /// 마지막 목숨(2회 사망)은 눈에 띄게 표시한다 — 넣는 순간 위험을 알아야 선택이 성립한다.
    /// </summary>
    public class PartyScreen : GameScreen
    {
        protected override string Title => "파티 편성";
        protected override string BackgroundArt => "bg_party";
        protected override string Subtitle =>
            $"최대 {PartyState.MaxSlots}인(§9) · 편성 {PartyState.Slots.Count}명 · " +
            $"1번 자리가 탱 자리다(§10-4 진형) · 부활초 {LifeSystem.GetRevivePotions()}/3";

        string _msg = "";
        int _page;

        protected override void Body(Rect r)
        {
            _page = DrawTabs(r, new[] { "편성", "출전" }, _page);
            var page = UiPages.AfterTabs(r);
            if (_page == 1)
            {
                DrawSortiePage(page);
                return;
            }

            var roster = LifeSystem.GetCharacters();
            if (roster.Count == 0)
            {
                Info(page, 0, "[주의] 캐릭터가 하나도 없다 — 로스터 생성이 실패했다");
                return;
            }

            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster[i];
                bool inParty = PartyState.Contains(i);
                var cell = UiPages.RosterCell(page, i);
                if (cell.yMax > page.yMax) break;
                if (DrawPartyCard(cell, ch, inParty, StatusOf(ch, i, inParty)))
                {
                    if (!PartyState.Toggle(i))
                        _msg = DefenseState.Contains(i)
                            ? "수비 배치 중이다 — 영지 수비대에서 해임해야 출전한다(§13-5)"
                            : LifeSystem.IsAvailable(ch)
                                ? $"자리가 없다 — {PartyState.MaxSlots}인이 상한이다(§9)"
                                : "출전할 수 없는 캐릭터다(회복 중이거나 삭제됐다, §4)";
                    else _msg = "";
                }
            }

            if (!string.IsNullOrEmpty(_msg))
                Info(new Rect(page.x, page.yMax - RowH, page.width, RowH), 0, _msg);
        }

        bool DrawPartyCard(Rect cell, CharacterRecord ch, bool inParty, string status)
        {
            var tint = ch.IsDeleted ? new Color(1f, 1f, 1f, 0.45f) : new Color(1f, 1f, 1f, 0.94f);
            if (!UiAtlas.DrawSliced(cell, "panel", 12f, tint))
                UiAtlas.Draw(cell, "panel", tint);
            float face = Mathf.Min(cell.width - 16f, cell.height - 40f);
            var faceR = new Rect(cell.center.x - face * 0.5f, cell.y + 6f, face, face);
            UiAtlas.DrawRosterFrame(faceR);
            PortraitAtlas.Draw(faceR, PortraitAtlas.KeyForJob(ch.Job),
                ch.IsDeleted ? new Color(1f, 1f, 1f, 0.4f) : (Color?)null);
            UiAtlas.DrawRosterMarks(faceR, new Rect(cell.x + 6f, cell.yMax - 32f, cell.width - 12f, 28f),
                ch.Job, ch.DeathCount, ch.IsDeleted);
            Hint(new Rect(cell.x + 6f, cell.yMax - 22f, cell.width - 12f, 20f),
                (inParty ? "★ " : "") + ch.Name + " · " + status);
            return GUI.Button(cell, GUIContent.none, GUIStyle.none);
        }

        void DrawSortiePage(Rect r)
        {
            var cards = UiPages.Grid(r, 2, 2, 16f);
            if (DrawCard(cards[0], "필드 출전",
                    PartyState.CanSortie ? "이 편성으로 사냥에 나간다" : "한 명도 편성되지 않았다",
                    "field", locked: !PartyState.CanSortie))
                GameFlow.Go(GameFlow.Field);
            if (DrawCard(cards[1], "전투 스타일", "직업별로 공격·방어·생존을 고른다", "damage"))
                GameFlow.Go(GameFlow.Style);
            DrawCard(cards[2], $"편성 {PartyState.Slots.Count}/{PartyState.MaxSlots}",
                "1번 자리가 탱 자리다", "tank", locked: true);
            if (DrawCard(cards[3], "영지로", "허브로 돌아간다", "territory"))
                GameFlow.Go(GameFlow.Estate);
        }

        static string StatusOf(CharacterRecord ch, int rosterIndex, bool inParty)
        {
            if (ch.IsDeleted) return "삭제됨 — 환생석으로만 복구(§4)";
            if (DefenseState.Contains(rosterIndex))
                return "수비 배치 — 출전 불가(§13-5)";

            int left = LifeSystem.GetRecoveryTimeRemaining(ch);
            if (left > 0) return $"회복 중 {LifeSystem.FormatRecoveryTime(left)} — 출전 불가(§4)";

            string mark = inParty ? "편성됨" : "대기";
            if (ch.DeathCount >= 2) return $"{mark} · [주의] 마지막 목숨 — 죽으면 영구 삭제(§4)";
            return mark;
        }
    }
}
