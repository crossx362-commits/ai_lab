using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>가방 60칸. QA_NO면 옛 무한(§11).</summary>
    public static class BagSlotsSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Bag Slots Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(BagSlots.EnvShow);
            string no = Environment.GetEnvironmentVariable(BagSlots.EnvNo);
            Environment.SetEnvironmentVariable(BagSlots.EnvShow, null);
            Environment.SetEnvironmentVariable(BagSlots.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            BagSlots.ResetForTest();
            _ = LifeSystem.GetCharacters();

            Check(BagSlots.Cap == 60, "상한 60");
            Check(!BagSlots.Blocked, "기본은 켜짐");
            Check(BagSlots.CanAddGear() && BagSlots.CanGain(Economy.LifeItem.CraftHide),
                "빈 칸이 있으면 받는다");
            int stacks = BagSlots.ItemStacks();
            bool newHide = GameState.Bag.GetCount(Economy.LifeItem.CraftHide) == 0;
            Check(GameState.Gain(Economy.LifeItem.CraftHide, 5), "첫 가죽은 받는다");
            Check(BagSlots.ItemStacks() == stacks + (newHide ? 1 : 0),
                "새 가죽은 1칸, 있던 가죽은 그대로");
            int afterHide = BagSlots.Used();
            Check(GameState.Gain(Economy.LifeItem.CraftHide, 3), "같은 스택은 칸을 안 늘린다");
            Check(BagSlots.Used() == afterHide
                  && GameState.Bag.GetCount(Economy.LifeItem.CraftHide) >= 8,
                "가죽 추가는 칸을 안 늘린다");

            int gears = BagSlots.GearStacks();
            int beforeGear = BagSlots.Used();
            var first = Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe);
            Check(first != null && BagSlots.GearStacks() == gears + 1
                  && BagSlots.Used() == beforeGear + 1,
                "흉갑 1개가 1칸");

            var roster = LifeSystem.GetCharacters();
            CharacterRecord tank = null;
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].IsDeleted) continue;
                if (EquipJob.LineOf(roster[i]) == "탱") { tank = roster[i]; break; }
            }
            Check(tank != null, "탱이 있다");
            int usedWorn = BagSlots.Used();
            int gearWorn = BagSlots.GearStacks();
            Check(Equipment.TryEquip(tank, first.Id), "입히면 가방에서 빠진다");
            Check(BagSlots.GearStacks() == gearWorn - 1 && BagSlots.Used() == usedWorn - 1,
                "장착은 가방을 안 먹는다");
            Check(Equipment.TryUnequip(tank, EquipSlot.Armor), "벗기면 가방으로");
            Check(BagSlots.GearStacks() == gearWorn && BagSlots.Used() == usedWorn,
                "벗긴 흉갑 1칸");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            BagSlots.ResetForTest();
            _ = LifeSystem.GetCharacters();
            Check(GameState.Gain(Economy.LifeItem.CraftHide, 50), "가죽 먼저");
            int room = BagSlots.Free();
            int filled = 0;
            for (int i = 0; i < BagSlots.Cap; i++)
            {
                if (Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe) == null) break;
                filled++;
            }
            Check(filled == room, $"빈 칸 {room}을 흉갑으로 채움 (실제 {filled})");
            Check(BagSlots.Used() == BagSlots.Cap, $"가득 {BagSlots.Used()}");
            Check(!BagSlots.CanAddGear(), "가득이면 장비 거부");
            Check(Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe) == null,
                "61번째는 null");
            Check(GameState.Gain(Economy.LifeItem.CraftFang, 1) == false,
                "가득이면 새 종류 거부");
            Check(GameState.Gain(Economy.LifeItem.CraftHide, 1),
                "가득이어도 있던 가죽은 받는다");
            Check(BagSlots.Line().IndexOf("60/60", StringComparison.Ordinal) >= 0
                  && BagSlots.Line().IndexOf("§11", StringComparison.Ordinal) >= 0,
                $"가득 줄 (실제 {BagSlots.Line()})");
            Check(BagSlots.WhyFull().IndexOf("가득", StringComparison.Ordinal) >= 0,
                $"가득 사유 (실제 {BagSlots.WhyFull()})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            BagSlots.ResetForTest();
            Environment.SetEnvironmentVariable(BagSlots.EnvNo, "1");
            Check(BagSlots.Blocked, "QA_NO");
            for (int i = 0; i < BagSlots.Cap + 2; i++)
                Check(Equipment.AddUnequippedForTest(Equipment.LeatherArmorRecipe) != null,
                    $"QA_NO 채움 {i + 1}");
            Check(BagSlots.Used() > BagSlots.Cap, "QA_NO면 60을 넘긴다");
            Check(BagSlots.CanAddGear() && GameState.Gain(Economy.LifeItem.CraftFang, 1),
                "QA_NO면 계속 받는다");
            Check(BagSlots.Line().IndexOf("칸 없음", StringComparison.Ordinal) >= 0,
                $"QA_NO 줄 (실제 {BagSlots.Line()})");
            Environment.SetEnvironmentVariable(BagSlots.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            BagSlots.ResetForTest();
            Environment.SetEnvironmentVariable(BagSlots.EnvShow, "1");
            BagSlots.SeedQaIfRequested();
            Check(BagSlots.ShowQa, "시드 ShowQa");
            Check(BagSlots.Used() == BagSlots.Cap, $"시드 60칸 (실제 {BagSlots.Used()})");
            Check(BagSlots.Line().IndexOf("60/60", StringComparison.Ordinal) >= 0,
                $"시드 줄 (실제 {BagSlots.Line()})");
            Environment.SetEnvironmentVariable(BagSlots.EnvShow, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string gainSrc = File.ReadAllText(Path.Combine(runtime, "GameState.cs"));
            string equipSrc = File.ReadAllText(Path.Combine(runtime, "Equipment.cs"));
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            string smithSrc = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            Check(gainSrc.IndexOf("BagSlots.CanGain", StringComparison.Ordinal) >= 0,
                "Gain이 CanGain을 읽는다");
            Check(equipSrc.IndexOf("BagSlots.CanAddGear", StringComparison.Ordinal) >= 0,
                "제작·벗기기·복원이 CanAddGear를 읽는다");
            Check(charSrc.IndexOf("BagSlots.Line", StringComparison.Ordinal) >= 0
                  && charSrc.IndexOf("BagSlots.SeedQaIfRequested", StringComparison.Ordinal) >= 0,
                "캐릭터창이 시드·줄을 읽는다");
            Check(smithSrc.IndexOf("BagSlots.CanAddGear", StringComparison.Ordinal) >= 0
                  && smithSrc.IndexOf("BagSlots.WhyFull", StringComparison.Ordinal) >= 0,
                "대장간이 가득을 읽는다");

            Environment.SetEnvironmentVariable(BagSlots.EnvShow, show);
            Environment.SetEnvironmentVariable(BagSlots.EnvNo, no);
            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            BagSlots.ResetForTest();

            if (_fail == 0) Debug.Log("[BagSlotsSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[BagSlotsSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[BagSlotsSelfCheck] FAIL {_fail}건");
        }
    }
}
