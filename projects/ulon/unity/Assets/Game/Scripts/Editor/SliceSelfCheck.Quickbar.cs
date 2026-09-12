using System;
using System.IO;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 기획 §4.2 퀵바 단축키. 원장 1–9·붕대/물약/명상 선행·HUD가 Fill/KeyOf를 쓰는지.
    /// NC: NcDisable 이면 번호 라벨이 빠지고 게이트가 빨간불.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertQuickbarHotkeys()
        {
            if (QuickbarSlots.NcDisable)
                throw new InvalidOperationException("QuickbarSlots.NcDisable 가 켜져 있으면 단축키가 없습니다.");

            string ledgerPath = Path.Combine(Application.dataPath, "Game/Scripts/Shared/QuickbarSlots.cs");
            if (!File.Exists(ledgerPath))
                throw new InvalidOperationException("퀵바 원장이 없습니다: " + ledgerPath);

            if (QuickbarSlots.Label(0, "붕대") != "1 붕대")
                throw new InvalidOperationException("1번 칸 라벨이 원장과 다릅니다: " + QuickbarSlots.Label(0, "붕대"));
            if (QuickbarSlots.KeyOf(0) != KeyCode.Alpha1 || QuickbarSlots.KeyOf(8) != KeyCode.Alpha9)
                throw new InvalidOperationException("1–9 키가 Alpha1–Alpha9 가 아닙니다.");
            if (QuickbarSlots.KeyOf(9) != KeyCode.None)
                throw new InvalidOperationException("9칸을 넘는 키는 없어야 합니다.");

            var dest = new QuickbarSlot[QuickbarSlots.MaxDrawn];
            int empty = QuickbarSlots.Fill(null, dest);
            if (empty != 3)
                throw new InvalidOperationException("주문 없는 퀵바는 붕대·물약·명상 3칸이어야 합니다. 지금 " + empty);
            if (dest[0].Kind != QuickbarKind.Bandage || dest[1].Kind != QuickbarKind.Potion ||
                dest[2].Kind != QuickbarKind.Meditate)
                throw new InvalidOperationException("앞 세 칸이 붕대·물약·명상이 아닙니다.");

            var book = new Spellbook();
            book.Learn(SpellId.Ember);
            int withEmber = QuickbarSlots.Fill(book, dest);
            if (withEmber != 4 || dest[3].Kind != QuickbarKind.Spell || dest[3].Spell != SpellId.Ember)
                throw new InvalidOperationException("불씨를 알면 4번째 칸이 불씨여야 합니다.");

            for (int i = 0; i < QuickbarSlots.Spells.Length; i++)
                book.Learn(QuickbarSlots.Spells[i]);
            int all = QuickbarSlots.Fill(book, dest);
            if (all != 3 + QuickbarSlots.Spells.Length)
                throw new InvalidOperationException("주문 전부면 3+주문 수여야 합니다. 지금 " + all);
            if (QuickbarSlots.Label(3, dest[3].Name) != "4 " + SpellNames.KoreanOf(SpellId.Ember))
                throw new InvalidOperationException("4번 칸 라벨이 불씨가 아닙니다.");

            string hud = HudSourceText();
            if (hud.IndexOf("QuickbarSlots.Fill", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD 퀵바가 QuickbarSlots.Fill 을 안 씁니다.");
            if (hud.IndexOf("QuickbarSlots.KeyOf", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD 가 QuickbarSlots.KeyOf 를 안 읽습니다.");
            if (hud.IndexOf("FireQuickbar", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD 단축키 발사 경로가 없습니다.");

            Debug.Log("[Ulon] 퀵바 단축키 — 1–9·붕대/물약/명상·Fill/KeyOf");
        }

        static void AssertQuickbarHotkeysNegativeControl()
        {
            bool was = QuickbarSlots.NcDisable;
            bool red = false;
            try
            {
                QuickbarSlots.NcDisable = true;
                try { AssertQuickbarHotkeys(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { QuickbarSlots.NcDisable = was; }
            if (!red)
                throw new InvalidOperationException("퀵바 단축키 네거티브 컨트롤 실패 — NcDisable 인데 통과했습니다.");
            Debug.Log("[Ulon] 퀵바 단축키 네거티브 컨트롤 통과 — NcDisable 이면 FAIL");
        }
    }
}
