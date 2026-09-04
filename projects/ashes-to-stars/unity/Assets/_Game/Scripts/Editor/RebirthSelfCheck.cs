using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>환생석을 쓰면 레벨 1·경험 0. QA_NO면 레벨은 그대로(§4).</summary>
    public static class RebirthSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Rebirth Lv1 Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(Rebirth.EnvShow);
            string no = Environment.GetEnvironmentVariable(Rebirth.EnvNo);
            Environment.SetEnvironmentVariable(Rebirth.EnvShow, null);
            Environment.SetEnvironmentVariable(Rebirth.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            Rebirth.ResetForTest();
            var roster = LifeSystem.GetCharacters();
            var ch = roster[0];
            ch.Job = "수호기사";
            ch.Advancement = AdvancementTier.First;
            ch.Level = 50;
            ch.Exp = 12_345;
            ch.IsDeleted = true;
            ch.DeathCount = 3;
            GameState.Gain(Economy.LifeItem.RebornStone, 1);
            Check(Rebirth.Apply(ch) && ch.Level == 1 && ch.Exp == 0,
                $"Apply 50→1 · exp 0 (실제 Lv{ch.Level} exp {ch.Exp})");
            Check(ch.Job == "수호기사" && ch.Advancement == AdvancementTier.First,
                "직업·1차는 그대로");
            Check(Rebirth.Line().Contains("§4") && Rebirth.DoneLine().Contains("→Lv1"),
                $"문구 (실제 {Rebirth.DoneLine()})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Rebirth.ResetForTest();
            roster = LifeSystem.GetCharacters();
            ch = roster[0];
            ch.Level = 50;
            ch.Exp = 99;
            ch.IsDeleted = true;
            ch.DeathCount = 3;
            GameState.Gain(Economy.LifeItem.RebornStone, 1);
            Check(LifeSystem.UseRebornStone(ch), "환생석 사용");
            Check(ch.Level == 1 && ch.Exp == 0 && !ch.IsDeleted && ch.DeathCount == 0,
                $"UseRebornStone 50→1 (실제 Lv{ch.Level} 삭제 {ch.IsDeleted} 목숨 {ch.DeathCount})");
            Check(ch.AbsorbedBoons.Count == 0, "흡수 패시브는 그대로 비운다");

            LifeSystem.ForgetInMemoryForTest();
            roster = LifeSystem.GetCharacters();
            ch = roster[0];
            Check(ch.Level == 1 && ch.Exp == 0 && !ch.IsDeleted,
                $"재기동 유지 Lv1 (실제 Lv{ch.Level} exp {ch.Exp})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Rebirth.ResetForTest();
            roster = LifeSystem.GetCharacters();
            ch = roster[0];
            ch.Level = 1;
            ch.Exp = 0;
            ch.IsDeleted = true;
            ch.DeathCount = 3;
            GameState.Gain(Economy.LifeItem.RebornStone, 1);
            Check(LifeSystem.UseRebornStone(ch) && ch.Level == 1,
                "이미 Lv1이면 그대로");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Rebirth.ResetForTest();
            Environment.SetEnvironmentVariable(Rebirth.EnvNo, "1");
            roster = LifeSystem.GetCharacters();
            ch = roster[0];
            ch.Level = 50;
            ch.Exp = 12_345;
            ch.IsDeleted = true;
            ch.DeathCount = 3;
            GameState.Gain(Economy.LifeItem.RebornStone, 1);
            Check(LifeSystem.UseRebornStone(ch), "차단해도 환생은 된다");
            Check(ch.Level == 50 && ch.Exp == 12_345 && !ch.IsDeleted && ch.DeathCount == 0,
                $"차단하면 레벨 유지 (실제 Lv{ch.Level} exp {ch.Exp})");
            Check(Rebirth.Line().Contains("유지"), $"차단 문구 (실제 {Rebirth.Line()})");
            Environment.SetEnvironmentVariable(Rebirth.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Rebirth.ResetForTest();
            Environment.SetEnvironmentVariable(Rebirth.EnvShow, "1");
            Rebirth.SeedQaIfRequested();
            roster = LifeSystem.GetCharacters();
            Check(roster[0].IsDeleted && roster[0].Level == 50 && roster[0].Exp == Rebirth.QaFromExp,
                $"시드 1 삭제 Lv50 (실제 삭제 {roster[0].IsDeleted} Lv{roster[0].Level})");
            Check(roster[0].Name == "환생시험" && roster[0].Job == "수호기사",
                $"시드 이름 (실제 {roster[0].Name} {roster[0].Job})");
            Environment.SetEnvironmentVariable(Rebirth.EnvShow, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Rebirth.ResetForTest();
            Environment.SetEnvironmentVariable(Rebirth.EnvShow, "2");
            Rebirth.SeedQaIfRequested();
            roster = LifeSystem.GetCharacters();
            Check(!roster[0].IsDeleted && roster[0].Level == 1 && roster[0].Exp == 0,
                $"시드 2 환생 Lv1 (실제 삭제 {roster[0].IsDeleted} Lv{roster[0].Level})");
            Environment.SetEnvironmentVariable(Rebirth.EnvShow, null);

            string life = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/LifeSystem.cs"));
            string estate = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/EstateScreen.cs"));
            string character = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            Check(life.Contains("Rebirth.Apply("),
                "UseRebornStone이 Rebirth.Apply를 읽는다");
            Check(estate.Contains("Rebirth.MausoleumSubtitle")
                  && estate.Contains("Rebirth.Line")
                  && estate.Contains("Rebirth.SeedQaIfRequested"),
                "영묘가 자막·문구·시드를 읽는다");
            Check(character.Contains("Rebirth.Line")
                  && character.Contains("Rebirth.SeedQaIfRequested"),
                "캐릭터가 문구·시드를 읽는다");

            _ = nameof(Rebirth.Apply);
            _ = nameof(Rebirth.SeedQaIfRequested);
            _ = nameof(LifeSystem.UseRebornStone);

            Environment.SetEnvironmentVariable(Rebirth.EnvShow, show);
            Environment.SetEnvironmentVariable(Rebirth.EnvNo, no);
            Rebirth.ResetForTest();
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();

            if (_fail > 0)
            {
                Debug.LogError("[RebirthSelfCheck] FAIL " + _fail + "건\n" + _log);
                throw new InvalidOperationException("[RebirthSelfCheck] FAIL " + _fail + "건");
            }
            Debug.Log("[RebirthSelfCheck] PASS\n" + _log);
        }
    }
}
