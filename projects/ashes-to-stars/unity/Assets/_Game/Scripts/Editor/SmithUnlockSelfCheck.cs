using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>대장간은 1차 전직 시점에 열린다. QA_NO면 기본직업만으로도 연다(§13-2).</summary>
    public static class SmithUnlockSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Smith Unlock Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(Equipment.EnvShowUnlock);
            string no = Environment.GetEnvironmentVariable(Equipment.EnvNoUnlock);
            Environment.SetEnvironmentVariable(Equipment.EnvShowUnlock, null);
            Environment.SetEnvironmentVariable(Equipment.EnvNoUnlock, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            PartyState.ResetForTest();

            var roster = LifeSystem.GetCharacters();
            Check(roster.Count >= 1, $"기본 명부 (실제 {roster.Count})");
            Check(roster[0].Advancement == AdvancementTier.Basic, "폴백 명부는 기본직업");
            Check(!Equipment.SmithUnlocked(), "기본직업만 있으면 잠김");
            Check(Equipment.LockReason() != null
                  && Equipment.LockReason().Contains("1차 전직")
                  && Equipment.LockReason().Contains("§13-2"),
                $"잠금 문구 (실제 {Equipment.LockReason()})");
            GameState.Gain(Economy.LifeItem.CraftHide, Equipment.LeatherArmorHideCost);
            Check(!Equipment.TryCraftLeatherArmor(), "잠긴 대장간은 제작 거부");
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) == Equipment.LeatherArmorHideCost,
                "거부하면 가죽은 그대로");

            var ch = roster[0];
            ch.Advancement = AdvancementTier.First;
            ch.Job = "수호기사";
            LifeSystem.PersistRoster();
            Check(Equipment.SmithUnlocked(), "1차가 연다");
            Check(string.IsNullOrEmpty(Equipment.LockReason()), "열린 뒤에는 잠금 문구 없음");
            Check(Equipment.TryCraftLeatherArmor(), "열린 뒤에는 제작");

            ch.Advancement = AdvancementTier.Second;
            LifeSystem.PersistRoster();
            Check(Equipment.SmithUnlocked(), "2차도 열린 채다");

            ch.IsDeleted = true;
            LifeSystem.PersistRoster();
            Check(!Equipment.SmithUnlocked(), "삭제된 1·2차는 안 연다");
            ch.IsDeleted = false;
            LifeSystem.PersistRoster();
            Check(Equipment.SmithUnlocked(), "되살리면 다시 연다");

            GameState.ForgetInMemoryForTest();
            LifeSystem.ForgetInMemoryForTest();
            Equipment.ForgetInMemoryForTest();
            Check(LifeSystem.GetCharacters()[0].Advancement == AdvancementTier.Second
                  && Equipment.SmithUnlocked(),
                "재기동 뒤에도 해금");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            Environment.SetEnvironmentVariable(Equipment.EnvNoUnlock, "1");
            Check(Equipment.SmithUnlocked(), "QA_NO면 기본직업만으로도 열림");
            Check(string.IsNullOrEmpty(Equipment.LockReason()), "QA_NO면 잠금 문구 없음");
            GameState.Gain(Economy.LifeItem.CraftHide, Equipment.LeatherArmorHideCost);
            Check(Equipment.TryCraftLeatherArmor(), "QA_NO면 기본직업도 제작");
            Environment.SetEnvironmentVariable(Equipment.EnvNoUnlock, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            roster = LifeSystem.GetCharacters();
            roster[0].Advancement = AdvancementTier.First;
            LifeSystem.PersistRoster();
            Environment.SetEnvironmentVariable(Equipment.EnvShowUnlock, "1");
            Equipment.SeedUnlockQaIfRequested();
            Check(LifeSystem.GetCharacters()[0].Advancement == AdvancementTier.Basic,
                "시드는 기본직업");
            Check(!Equipment.SmithUnlocked(), "시드는 잠김");
            Check(Equipment.LockLine().Contains("1차 전직"),
                $"시드 문구 (실제 {Equipment.LockLine()})");
            Environment.SetEnvironmentVariable(Equipment.EnvShowUnlock, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string equip = File.ReadAllText(Path.Combine(runtime, "Equipment.cs"));
            string estate = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            Check(equip.Contains("if (!SmithUnlocked()) return false"),
                "TryCraft가 SmithUnlocked를 읽는다");
            Check(estate.Contains("Equipment.LockReason")
                  && estate.Contains("Equipment.SeedUnlockQaIfRequested"),
                "영지가 잠금·시드를 읽는다");

            _ = nameof(Equipment.SmithUnlocked);
            _ = nameof(Equipment.LockReason);
            _ = nameof(Equipment.SeedUnlockQaIfRequested);

            Environment.SetEnvironmentVariable(Equipment.EnvShowUnlock, show);
            Environment.SetEnvironmentVariable(Equipment.EnvNoUnlock, no);
            Equipment.ResetAll();
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();

            if (_fail == 0) Debug.Log("[SmithUnlockSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[SmithUnlockSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[SmithUnlockSelfCheck] FAIL {_fail}건");
        }
    }
}
