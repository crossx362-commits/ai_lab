using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>영묘는 첫 캐릭터 삭제에 열린다. QA_NO면 삭제와 무관(§13-2).</summary>
    public static class MausoleumUnlockSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Mausoleum Unlock Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(Memorial.EnvShowUnlock);
            string no = Environment.GetEnvironmentVariable(Memorial.EnvNoUnlock);
            string rec = Environment.GetEnvironmentVariable(Memorial.EnvShow);
            Environment.SetEnvironmentVariable(Memorial.EnvShowUnlock, null);
            Environment.SetEnvironmentVariable(Memorial.EnvNoUnlock, null);
            Environment.SetEnvironmentVariable(Memorial.EnvShow, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            PartyState.ResetForTest();
            Memorial.ResetForTest();

            Check(!Memorial.Unlocked, "삭제가 없으면 잠김");
            Check(Memorial.LockReason() != null
                  && Memorial.LockReason().Contains("첫 캐릭터 삭제")
                  && Memorial.LockReason().Contains("§13-2"),
                $"잠금 문구 (실제 {Memorial.LockReason()})");
            Check(LifeSystem.GetDeletedCharacters().Count == 0, "기본 명부는 삭제 0");

            var roster = LifeSystem.GetCharacters();
            var ch = roster[0];
            ch.DeathCount = 1;
            LifeSystem.RegisterDeath(ch);
            Check(!ch.IsDeleted && !Memorial.Unlocked, "1회 사망은 아직 잠김");

            ch.DeathCount = 2;
            LifeSystem.RegisterDeath(ch);
            Check(ch.IsDeleted, "3회면 삭제");
            Check(Memorial.Unlocked, "첫 삭제가 연다");
            Check(string.IsNullOrEmpty(Memorial.LockReason()),
                "열린 뒤에는 잠금 문구 없음");

            GameState.Gain(Economy.LifeItem.RebornStone, 1);
            Check(LifeSystem.UseRebornStone(ch), "환생");
            Check(!ch.IsDeleted && LifeSystem.GetDeletedCharacters().Count == 0,
                "환생하면 삭제 명부는 비어 있다");
            Check(Memorial.Unlocked, "환생해도 영묘는 열린 채다");

            GameState.ForgetInMemoryForTest();
            LifeSystem.ForgetInMemoryForTest();
            Memorial.ForgetInMemoryForTest();
            Check(Memorial.Unlocked, "재기동 뒤에도 해금");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Memorial.ResetForTest();
            roster = LifeSystem.GetCharacters();
            ch = roster[0];
            ch.IsSpecialJob = true;
            ch.DeathCount = 0;
            LifeSystem.RegisterDeath(ch);
            Check(ch.IsDeleted && Memorial.Unlocked, "특수 직업 1회 삭제도 연다");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Memorial.ResetForTest();
            roster = LifeSystem.GetCharacters();
            ch = roster[0];
            ch.DeathCount = 2;
            LifeSystem.RegisterDeath(ch, isPvp: true);
            Check(!ch.IsDeleted && !Memorial.Unlocked, "PvP는 안 연다");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Memorial.ResetForTest();
            Environment.SetEnvironmentVariable(Memorial.EnvNoUnlock, "1");
            Check(Memorial.Unlocked, "QA_NO면 삭제 없어도 열림");
            Check(string.IsNullOrEmpty(Memorial.LockReason()),
                "QA_NO면 잠금 문구 없음");
            Environment.SetEnvironmentVariable(Memorial.EnvNoUnlock, null);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            Memorial.ResetForTest();
            Environment.SetEnvironmentVariable(Memorial.EnvShowUnlock, "1");
            Memorial.SeedUnlockQaIfRequested();
            Check(!Memorial.Unlocked, "시드는 잠김");
            Check(LifeSystem.GetDeletedCharacters().Count == 0, "시드는 삭제 0");
            Check(Memorial.LockLine().Contains("첫 캐릭터 삭제"),
                $"시드 문구 (실제 {Memorial.LockLine()})");
            Environment.SetEnvironmentVariable(Memorial.EnvShowUnlock, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string life = File.ReadAllText(Path.Combine(runtime, "LifeSystem.cs"));
            string estate = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            Check(life.Contains("Memorial.Open()"),
                "RegisterDeath가 Open을 읽는다");
            Check(estate.Contains("Memorial.LockReason")
                  && estate.Contains("Memorial.SeedUnlockQaIfRequested"),
                "영지가 잠금·시드를 읽는다");

            _ = nameof(Memorial.Unlocked);
            _ = nameof(Memorial.LockReason);
            _ = nameof(Memorial.Open);
            _ = nameof(Memorial.SeedUnlockQaIfRequested);

            Environment.SetEnvironmentVariable(Memorial.EnvShowUnlock, show);
            Environment.SetEnvironmentVariable(Memorial.EnvNoUnlock, no);
            Environment.SetEnvironmentVariable(Memorial.EnvShow, rec);
            Memorial.ResetForTest();
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();

            if (_fail == 0) Debug.Log("[MausoleumUnlockSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[MausoleumUnlockSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[MausoleumUnlockSelfCheck] FAIL {_fail}건");
        }
    }
}
