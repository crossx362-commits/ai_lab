using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>경매 등록·복원이 드랍 등급·옵션을 싣는다. QA_NO면 옛 recipe|enhance(§11).</summary>
    public static class GearListSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Gear List Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(GearOpt.EnvShowList);
            string no = Environment.GetEnvironmentVariable(GearOpt.EnvNo);
            Environment.SetEnvironmentVariable(GearOpt.EnvShowList, null);
            Environment.SetEnvironmentVariable(GearOpt.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            AuctionState.ResetForTest();
            GearOpt.ResetForTest();
            _ = LifeSystem.GetCharacters();
            if (GameState.Wallet.Copper < 50_000) GameState.Grant(50_000);

            Check(!GearOpt.Blocked, "기본은 켜짐");
            var drop = Equipment.TryGrantDrop(Equipment.LeatherArmorRecipe, GearGrade.Legendary);
            Check(drop != null, "드랍 전설");
            drop.Affixes = new[] { 0, 1, 2, 3 };
            drop.Enhance = 3;
            Equipment.Flush();
            string packed = GearOpt.Pack(drop);
            Check(packed.IndexOf("Legendary", StringComparison.Ordinal) >= 0
                  && packed.IndexOf("0,1,2,3", StringComparison.Ordinal) >= 0
                  && packed.IndexOf("|3|", StringComparison.Ordinal) >= 0,
                $"Pack 전설+3+옵션 (실제 {packed})");

            Check(AuctionState.TryListGear(drop.Id, GearOpt.ListQaPrice), "등록");
            Check(Equipment.Find(drop.Id) == null, "등록하면 가방에서 빠진다");
            Check(AuctionState.MineCount == 1, "내 등록 1");
            string lotId = "";
            string lotKey = "";
            var lots = AuctionState.Lots;
            for (int i = 0; i < lots.Count; i++)
            {
                if (lots[i] != null && !lots[i].Npc && lots[i].Gear)
                {
                    lotId = lots[i].Id;
                    lotKey = lots[i].Key;
                    break;
                }
            }
            Check(lotKey == packed, $"롯 Key=Pack (실제 {lotKey})");
            Check(AuctionState.TryCancel(lotId), "유찰 취소");
            Check(AuctionState.MineCount == 0, "취소 뒤 등록 0");
            GearItem back = null;
            var bag = Equipment.Unequipped();
            for (int i = 0; i < bag.Count; i++)
                if (bag[i].RecipeId == Equipment.LeatherArmorRecipe) back = bag[i];
            Check(back != null && back.Grade == GearGrade.Legendary && back.Enhance == 3
                  && GearOpt.CountOf(back) == 4,
                $"복원 전설 +3 옵션 4 (실제 {back?.Grade} +{back?.Enhance} {GearOpt.CountOf(back)})");
            Check(GearOpt.Format(back).IndexOf("생명", StringComparison.Ordinal) >= 0
                  && GearOpt.Format(back).IndexOf("견고", StringComparison.Ordinal) >= 0,
                $"복원 줄 (실제 {GearOpt.Format(back)})");

            Check(GearOpt.Parse("leather_armor|2", out string oldRec, out int oldEn,
                    out GearGrade oldGrade, out int[] oldAff)
                  && oldRec == "leather_armor" && oldEn == 2
                  && oldGrade == GearGrade.Common && oldAff.Length == 0,
                "옛 recipe|enhance는 일반 0");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            AuctionState.ResetForTest();
            GearOpt.ResetForTest();
            _ = LifeSystem.GetCharacters();
            if (GameState.Wallet.Copper < 50_000) GameState.Grant(50_000);
            Environment.SetEnvironmentVariable(GearOpt.EnvNo, "1");
            Check(GearOpt.Blocked, "QA_NO");
            var blocked = Equipment.TryGrantDrop(Equipment.LeatherArmorRecipe, GearGrade.Legendary);
            Check(blocked != null, "QA_NO 드랍");
            blocked.Grade = GearGrade.Legendary;
            blocked.Affixes = new[] { 0, 1, 2, 3 };
            blocked.Enhance = 1;
            Equipment.Flush();
            string oldPack = GearOpt.Pack(blocked);
            Check(oldPack == blocked.RecipeId + "|1", $"QA_NO Pack 옛 칸 (실제 {oldPack})");
            Check(AuctionState.TryListGear(blocked.Id, GearOpt.ListQaPrice), "QA_NO 등록");
            string blockedLot = "";
            lots = AuctionState.Lots;
            for (int i = 0; i < lots.Count; i++)
                if (lots[i] != null && !lots[i].Npc && lots[i].Gear) blockedLot = lots[i].Id;
            Check(AuctionState.TryCancel(blockedLot), "QA_NO 취소");
            GearItem lost = null;
            bag = Equipment.Unequipped();
            for (int i = 0; i < bag.Count; i++)
                if (bag[i].RecipeId == Equipment.LeatherArmorRecipe) lost = bag[i];
            Check(lost != null && lost.Grade == GearGrade.Common && GearOpt.CountOf(lost) == 0,
                $"QA_NO 복원은 일반 0 (실제 {lost?.Grade} {GearOpt.CountOf(lost)})");
            Check(GearOpt.ListLine().IndexOf("안 싣는다", StringComparison.Ordinal) >= 0,
                $"QA_NO 줄 (실제 {GearOpt.ListLine()})");
            Environment.SetEnvironmentVariable(GearOpt.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            AuctionState.ResetForTest();
            GearOpt.ResetForTest();
            Environment.SetEnvironmentVariable(GearOpt.EnvShowList, "1");
            GearOpt.SeedListQaIfRequested();
            Check(GearOpt.ShowListQa, "시드 ShowListQa");
            Check(GearOpt.ListLine().IndexOf("경매도 옵션", StringComparison.Ordinal) >= 0,
                $"시드 줄 (실제 {GearOpt.ListLine()})");
            bool seeded = false;
            bag = Equipment.Unequipped();
            for (int i = 0; i < bag.Count; i++)
            {
                if (bag[i].Grade == GearGrade.Legendary
                    && bag[i].RecipeId == Equipment.LeatherArmorRecipe
                    && GearOpt.CountOf(bag[i]) == 4)
                    seeded = true;
            }
            Check(seeded, "시드 복원 전설 옵션 4");
            Check(GearOpt.LastLine.IndexOf("옵션 4", StringComparison.Ordinal) >= 0,
                $"시드 마지막 줄 (실제 {GearOpt.LastLine})");
            Environment.SetEnvironmentVariable(GearOpt.EnvShowList, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string auctionSrc = File.ReadAllText(Path.Combine(runtime, "AuctionState.cs"));
            string equipSrc = File.ReadAllText(Path.Combine(runtime, "Equipment.cs"));
            string charSrc = File.ReadAllText(Path.Combine(runtime, "CharacterScreen.cs"));
            Check(auctionSrc.IndexOf("GearOpt.Pack", StringComparison.Ordinal) >= 0,
                "등록이 Pack을 읽는다");
            Check(equipSrc.IndexOf("GearOpt.Parse", StringComparison.Ordinal) >= 0,
                "복원이 Parse를 읽는다");
            Check(charSrc.IndexOf("GearOpt.SeedListQaIfRequested", StringComparison.Ordinal) >= 0
                  && charSrc.IndexOf("GearOpt.ListLine", StringComparison.Ordinal) >= 0,
                "캐릭터창이 시드·줄을 읽는다");

            _ = nameof(GearOpt.Pack);
            _ = nameof(GearOpt.Parse);
            _ = nameof(GearOpt.ListLine);
            _ = nameof(GearOpt.SeedListQaIfRequested);

            Environment.SetEnvironmentVariable(GearOpt.EnvShowList, show);
            Environment.SetEnvironmentVariable(GearOpt.EnvNo, no);
            GearOpt.ResetForTest();
            Equipment.ResetAll();
            AuctionState.ResetForTest();
            GameState.ResetAll();
            LifeSystem.ResetAll();

            if (_fail == 0) Debug.Log("[GearListSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[GearListSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[GearListSelfCheck] FAIL {_fail}건");
        }
    }
}
