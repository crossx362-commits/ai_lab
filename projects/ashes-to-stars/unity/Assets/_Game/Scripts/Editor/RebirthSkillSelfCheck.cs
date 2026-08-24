using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>환생 스킬 1개 선택. QA_NO면 옛 전체 스킬 줄(§4).</summary>
    public static class RebirthSkillSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Rebirth Skill Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(RebirthSkill.EnvShow);
            string no = Environment.GetEnvironmentVariable(RebirthSkill.EnvNo);
            Environment.SetEnvironmentVariable(RebirthSkill.EnvShow, null);
            Environment.SetEnvironmentVariable(RebirthSkill.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            Rebirth.ResetForTest();
            RebirthSkill.ResetForTest();

            var names = RebirthSkill.NamesOf("수호기사");
            Check(names.Length >= 2, $"수호기사 스킬 {names.Length}개 (2개 미만이면 선택 UI가 안 열린다)");
            bool hasTaunt = false;
            for (int i = 0; i < names.Length; i++)
                if (names[i] == RebirthSkill.QaKeep) hasTaunt = true;
            Check(hasTaunt, $"수호기사에 {RebirthSkill.QaKeep}");

            var roster = LifeSystem.GetCharacters();
            var ch = roster[0];
            ch.Job = "수호기사";
            ch.Advancement = AdvancementTier.First;
            ch.Level = 50;
            ch.IsDeleted = true;
            ch.DeathCount = 3;
            Check(RebirthSkill.NeedsPick(ch), "삭제된 수호기사는 선택이 열린다");
            Check(RebirthSkill.Apply(ch, RebirthSkill.QaKeep) && ch.KeptSkill == RebirthSkill.QaKeep,
                $"Apply {RebirthSkill.QaKeep} (실제 {ch.KeptSkill})");
            Check(RebirthSkill.Line(ch).Contains(RebirthSkill.QaKeep)
                  && RebirthSkill.Line(ch).Contains("소실")
                  && RebirthSkill.Line(ch).Contains("§4"),
                $"문구 (실제 {RebirthSkill.Line(ch)})");
            string keptLine = RebirthSkill.SkillLine(ch);
            Check(keptLine.Contains(RebirthSkill.QaKeep) && keptLine.Contains("계승 1개")
                  && keptLine.IndexOf("성채 방패", StringComparison.Ordinal) < 0,
                $"계승 줄에 방패가 없다 (실제 {keptLine})");
            Check(string.IsNullOrEmpty(RebirthSkill.SkillUltLine(ch)),
                "계승 뒤 초필 줄은 비운다");

            GameState.Gain(Economy.LifeItem.RebornStone, 1);
            Check(LifeSystem.UseRebornStone(ch), "환생석");
            Check(!ch.IsDeleted && ch.Level == 1 && ch.KeptSkill == RebirthSkill.QaKeep,
                $"환생 뒤에도 계승 유지 (삭제 {ch.IsDeleted} Lv{ch.Level} {ch.KeptSkill})");

            LifeSystem.ForgetInMemoryForTest();
            roster = LifeSystem.GetCharacters();
            ch = roster[0];
            Check(ch.KeptSkill == RebirthSkill.QaKeep,
                $"재기동 유지 (실제 {ch.KeptSkill})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            RebirthSkill.ResetForTest();
            roster = LifeSystem.GetCharacters();
            ch = roster[0];
            ch.Job = "수호기사";
            ch.Advancement = AdvancementTier.First;
            ch.IsDeleted = true;
            ch.DeathCount = 3;
            GameState.Gain(Economy.LifeItem.RebornStone, 1);
            Check(LifeSystem.UseRebornStone(ch) && string.IsNullOrEmpty(ch.KeptSkill),
                "선택 없이 환생하면 계승은 비어 옛 줄");
            Check(RebirthSkill.SkillLine(ch).IndexOf("성채 방패", StringComparison.Ordinal) >= 0,
                $"옛 줄에 방패 (실제 {RebirthSkill.SkillLine(ch)})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            RebirthSkill.ResetForTest();
            Environment.SetEnvironmentVariable(RebirthSkill.EnvNo, "1");
            roster = LifeSystem.GetCharacters();
            ch = roster[0];
            ch.Job = "수호기사";
            ch.Advancement = AdvancementTier.First;
            ch.IsDeleted = true;
            Check(!RebirthSkill.NeedsPick(ch), "차단하면 선택 안 연다");
            Check(!RebirthSkill.Apply(ch, RebirthSkill.QaKeep) && string.IsNullOrEmpty(ch.KeptSkill),
                "차단 Apply는 실패·빈 칸");
            Check(RebirthSkill.SkillLine(ch).IndexOf("성채 방패", StringComparison.Ordinal) >= 0,
                $"차단 줄은 옛 전체 (실제 {RebirthSkill.SkillLine(ch)})");
            Check(RebirthSkill.MausoleumSubtitle() == Rebirth.MausoleumSubtitle(),
                "차단 자막은 옛 환생 자막");
            Environment.SetEnvironmentVariable(RebirthSkill.EnvNo, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            RebirthSkill.ResetForTest();
            Environment.SetEnvironmentVariable(RebirthSkill.EnvShow, "1");
            RebirthSkill.SeedQaIfRequested();
            roster = LifeSystem.GetCharacters();
            Check(roster[0].IsDeleted && roster[0].Job == "수호기사"
                  && string.IsNullOrEmpty(roster[0].KeptSkill) && RebirthSkill.SeedPick,
                $"시드 1 선택 대기 (삭제 {roster[0].IsDeleted} 계승 '{roster[0].KeptSkill}' pick {RebirthSkill.SeedPick})");
            Environment.SetEnvironmentVariable(RebirthSkill.EnvShow, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            RebirthSkill.ResetForTest();
            Environment.SetEnvironmentVariable(RebirthSkill.EnvShow, "2");
            RebirthSkill.SeedQaIfRequested();
            roster = LifeSystem.GetCharacters();
            Check(!roster[0].IsDeleted && roster[0].Level == 1
                  && roster[0].KeptSkill == RebirthSkill.QaKeep,
                $"시드 2 계승 (삭제 {roster[0].IsDeleted} Lv{roster[0].Level} {roster[0].KeptSkill})");
            Check(RebirthSkill.SkillLine(roster[0]).Contains("계승 1개"),
                $"시드 2 줄 (실제 {RebirthSkill.SkillLine(roster[0])})");
            Environment.SetEnvironmentVariable(RebirthSkill.EnvShow, null);

            string life = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/LifeSystem.cs"));
            string estate = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/EstateScreen.cs"));
            string character = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            Check(life.Contains("KeptSkill") && life.Contains("RebirthSkill.Pack"),
                "로스터가 KeptSkill을 저장한다");
            Check(estate.Contains("RebirthSkill.NeedsPick")
                  && estate.Contains("RebirthSkill.SeedQaIfRequested")
                  && estate.Contains("RebirthSkill.MausoleumSubtitle"),
                "영묘가 선택·시드·자막을 읽는다");
            Check(character.Contains("RebirthSkill.SkillLine")
                  && character.Contains("RebirthSkill.SeedQaIfRequested"),
                "캐릭터가 계승 줄·시드를 읽는다");

            _ = nameof(RebirthSkill.Apply);
            _ = nameof(RebirthSkill.NeedsPick);
            _ = nameof(RebirthSkill.SkillLine);

            Environment.SetEnvironmentVariable(RebirthSkill.EnvShow, show);
            Environment.SetEnvironmentVariable(RebirthSkill.EnvNo, no);
            RebirthSkill.ResetForTest();
            Rebirth.ResetForTest();
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();

            if (_fail > 0)
            {
                Debug.LogError("[RebirthSkillSelfCheck] FAIL " + _fail + "건\n" + _log);
                throw new InvalidOperationException("[RebirthSkillSelfCheck] FAIL " + _fail + "건");
            }
            Debug.Log("[RebirthSkillSelfCheck] PASS\n" + _log);
        }
    }
}
