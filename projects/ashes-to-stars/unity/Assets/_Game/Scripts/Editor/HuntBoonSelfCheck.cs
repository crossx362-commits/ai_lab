using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>사냥 중 처치로 3택이 뜨고, 영구 레벨·보스·검증 판은 안 뜬다.</summary>
    public static class HuntBoonSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Hunt Boon Self Check")]
        public static void Run()
        {
            int fail = 0;
            void Check(bool cond, string what)
            {
                if (!cond)
                {
                    fail++;
                    Debug.LogError("[HuntBoonSelfCheck] FAIL  " + what);
                }
            }

            HuntBoon.End();
            Check(HuntBoon.IconOf(BoonId.치유의손) == "healer"
                  && HuntBoon.IconOf(BoonId.예리함) == "damage"
                  && HuntBoon.IconOf(BoonId.강골) == "tank"
                  && HuntBoon.IconOf(BoonId.발놀림) == "buffer",
                "강화 아이콘은 목숨 하트가 아니라 역할 조각");
            Check(HuntBoon.IconOf(BoonId.분노) != "heart",
                "큰 카드에 목숨 하트를 쓰면 잘린다");
            var band = HuntBoon.PickBand(new Rect(0f, 160f, 1280f, 400f));
            var cells = UiPages.Grid(band, 3, 1, HuntBoon.CardGap);
            Check(cells.Length == 3 && UiPages.IsWideCard(cells[0]),
                "3택 카드는 가로여야 금테가 안 늘어난다");
            UiPages.CardLayout(cells[0], true, out var ic, out var tt, out var sb);
            Check(UiAtlas.FitsInContent(cells[0], ic)
                  && UiAtlas.FitsInContent(cells[0], tt)
                  && UiAtlas.FitsInContent(cells[0], sb)
                  && ic.x < tt.x,
                "가로 카드 아이콘·글씨가 금테 안에 있어야 한다");

            Check(HuntBoon.Need(0) == HuntBoon.FirstKills, "첫 강화는 8처치");
            Check(HuntBoon.Need(1) > HuntBoon.Need(0), "다음 강화가 더 비싸다");

            HuntBoon.BeginField(7);
            for (int i = 0; i < HuntBoon.FirstKills - 1; i++) HuntBoon.NoteKill();
            Check(!HuntBoon.Waiting, "7마리면 아직 안 뜬다");
            HuntBoon.NoteKill();
            Check(HuntBoon.Waiting, "8마리째에 3택이 떠야 한다");
            Check(HuntBoon.Offered != null && HuntBoon.Offered.Count == Boons.Choices,
                "후보는 3개");

            var first = HuntBoon.Offered[0];
            Check(HuntBoon.Take(first) && HuntBoon.Owned.Count == 1, "고르면 보유 1");
            Check(!HuntBoon.Waiting, "한 단계면 고른 뒤 닫힌다");

            for (int i = 0; i < HuntBoon.Need(1); i++) HuntBoon.NoteKill();
            Check(HuntBoon.Waiting, "두 번째 단계도 뜬다");
            Check(!HuntBoon.Offered.Contains(first), "이미 고른 것은 다시 안 나온다");

            HuntBoon.End();
            HuntBoon.NoteKill();
            Check(!HuntBoon.Waiting, "검증 판(미시작)은 안 뜬다");

            var shared = new List<int>();
            HuntBoon.BindDungeon(shared, 9);
            for (int i = 0; i < HuntBoon.FirstKills; i++) HuntBoon.NoteKill();
            var pick = HuntBoon.Offered[0];
            HuntBoon.Take(pick);
            Check(shared.Count == 1 && shared[0] == (int)pick, "던전 보유 목록에 쌓인다");
            HuntBoon.LeaveBattle();
            Check(shared.Count == 1, "던전 전투를 나가도 런 강화는 남는다");
            HuntBoon.End();
            Check(shared.Count == 1, "End는 던전 리스트를 지우지 않는다");

            Environment.SetEnvironmentVariable(HuntBoon.EnvNo, "1");
            HuntBoon.BeginField(3);
            for (int i = 0; i < 20; i++) HuntBoon.NoteKill();
            Check(!HuntBoon.Waiting, "QA_NO면 안 뜬다");
            Environment.SetEnvironmentVariable(HuntBoon.EnvNo, null);
            HuntBoon.End();

            HuntBoon.BeginField(11);
            int stacked = HuntBoon.Need(0) + HuntBoon.Need(1);
            for (int i = 0; i < stacked; i++) HuntBoon.NoteKill();
            Check(HuntBoon.Waiting && HuntBoon.Pending >= 1, "두 단계가 쌓이면 목록이 열린다");
            HuntBoon.Take(HuntBoon.Offered[0]);
            Check(HuntBoon.Waiting, "하나 골라도 다음 단계가 남는다");
            HuntBoon.Take(HuntBoon.Offered[0]);
            Check(!HuntBoon.Waiting, "두 번 고르면 닫힌다");

            HuntBoon.End();
            HuntBoon.BeginField(13);
            int guard = 0;
            while (HuntBoon.Owned.Count < 8 && guard++ < 400)
            {
                if (HuntBoon.Waiting) HuntBoon.Take(HuntBoon.Offered[0]);
                else HuntBoon.NoteKill();
            }
            Check(HuntBoon.Owned.Count == 8, "강화는 8개까지");
            for (int i = 0; i < 30; i++) HuntBoon.NoteKill();
            Check(!HuntBoon.Waiting, "8개를 다 먹으면 더 안 뜬다");
            HuntBoon.End();

            if (fail == 0) Debug.Log("[HuntBoonSelfCheck] PASS");
            else Debug.LogError($"[HuntBoonSelfCheck] FAIL {fail}");
        }
    }
}
