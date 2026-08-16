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

        protected override void Body(Rect r)
        {
            var roster = LifeSystem.GetCharacters();
            int row = 0;

            if (roster.Count == 0)
            {
                // 조용히 빈 목록을 그리지 않는다 — 로스터가 비어 있으면 그건 버그다(실제로 그랬다)
                Info(r, row++, "[주의] 캐릭터가 하나도 없다 — 로스터 생성이 실패했다");
                return;
            }

            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster[i];
                bool inParty = PartyState.Contains(i);
                string label = (inParty ? "★ " : "") + $"{ch.Name} · {ch.Job}";
                string sub = StatusOf(ch, i, inParty);

                if (Row(r, row, label, "", leftPad: 56f))
                {
                    if (!PartyState.Toggle(i))
                        _msg = DefenseState.Contains(i)
                            ? "수비 배치 중이다 — 영지 수비대에서 해임해야 출전한다(§13-5)"
                            : LifeSystem.IsAvailable(ch)
                                ? $"자리가 없다 — {PartyState.MaxSlots}인이 상한이다(§9)"
                                : "출전할 수 없는 캐릭터다(회복 중이거나 삭제됐다, §4)";
                    else _msg = "";
                }
                DrawSlotChrome(r, row, ch, sub);
                row++;
            }

            if (!string.IsNullOrEmpty(_msg)) Info(r, row++, _msg);

            if (Row(r, row++, "출전", PartyState.CanSortie
                    ? "이 편성으로 전투에 나간다" : "한 명도 편성되지 않았다"))
            {
                if (!PartyState.CanSortie) _msg = "최소 한 명은 편성해야 한다";
                else GameFlow.Go(GameFlow.Field);
            }

            // ⚠️ 진입점을 여기 두는 이유: 화면을 만들어도 **갈 수 있는 버튼이 없으면**
            //    그 화면은 없는 것과 같다(이 저장소에 실제 전례가 있다 — 렌더 점검은
            //    화면 함수를 직접 불러 확인하므로 도달 불가를 영원히 못 잡는다).
            if (Row(r, row++, "전투 스타일", "직업별로 공격형·방어형 등을 고른다(§3)"))
                GameFlow.Go(GameFlow.Style);

            if (Row(r, row, "돌아가기", "영지로")) GameFlow.Go(GameFlow.Estate);
        }

        void DrawSlotChrome(Rect r, int index, CharacterRecord ch, string sub)
        {
            var br = RowButtonRect(r, index);
            if (br.yMax > r.yMax) return;

            var face = new Rect(br.x + 6, br.y + 5, 48, 48);
            var tint = ch.IsDeleted ? new Color(1f, 1f, 1f, 0.4f) : (Color?)null;
            UiAtlas.DrawRosterFrame(face);
            PortraitAtlas.Draw(face, PortraitAtlas.KeyForJob(ch.Job), tint);
            var desc = RowDescRect(r, index);
            float heartsW = UiAtlas.DrawRosterMarks(face, desc, ch.Job, ch.DeathCount, ch.IsDeleted);
            Hint(new Rect(desc.x + heartsW + 6, desc.y + 6, desc.width - heartsW - 6, 22), sub);
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
