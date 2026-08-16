using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 필드·탑·던전 잡몹 생존 경험치. 테스터 레벨 정체의 구멍.
    /// </summary>
    public static class HuntExpSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Hunt Exp Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string noHunt = Environment.GetEnvironmentVariable("QA_NO_HUNT_EXP");
            string qaHunt = Environment.GetEnvironmentVariable("QA_HUNT_EXP");
            float oldRace = Economy.ForceRaceXpMul;
            Environment.SetEnvironmentVariable("QA_NO_HUNT_EXP", null);
            Environment.SetEnvironmentVariable("QA_HUNT_EXP", null);
            Economy.ForceRaceXpMul = 1f;

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();

            Check(Economy.HuntExpPerSecond == 58f, $"초당 58 (§18-6, 실제 {Economy.HuntExpPerSecond})");
            Check(Economy.WaveHuntExp(0, 0f) == 0, "0초는 경험치 0");
            Check(Economy.WaveHuntExp(0, 100f) == 5800, $"T1 100초 = 5800 (실제 {Economy.WaveHuntExp(0, 100f)})");
            Check(Economy.WaveHuntExp(1, 100f) == 9280, $"T2 100초 = 9280 (실제 {Economy.WaveHuntExp(1, 100f)})");

            PartyState.SetSlotsForTest(0);
            var solo = LifeSystem.GetCharacters()[0];
            int lv0 = solo.Level;
            long need = LifeSystem.ExpToNext(lv0);
            float seconds = (need / Economy.HuntExpPerSecond) + 1f;
            var lines = LifeSystem.AwardWaveHunt(seconds);
            Check(lines.Count == 1, "솔로 한 판은 줄 1개");
            Check(solo.Level == lv0 + 1, $"솔로가 한 칸 오른다 (Lv {lv0}→{solo.Level})");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            Economy.ForceRaceXpMul = 1f;
            var five = LifeSystem.GetCharacters();
            int before = five[0].Level;
            LifeSystem.AwardWaveHunt(240f);
            long got = 0;
            foreach (var c in five) got += c.Exp + ExtraSpent(c, 10);
            Check(got == 13920, $"5인 240초 총합 13920 (실제 {got})");
            Check(five[0].Level == before, "5인 240초는 시작 Lv10에서 한 칸이 아니다");
            Check(five[0].Exp > 0, "5인이어도 경험치는 쌓인다");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            Environment.SetEnvironmentVariable("QA_NO_HUNT_EXP", "1");
            Economy.ForceRaceXpMul = 1f;
            Check(Economy.WaveHuntExp(0, 240f) == 0, "QA_NO_HUNT_EXP면 총량 0");
            LifeSystem.AwardWaveHunt(240f);
            Check(LifeSystem.GetCharacters()[0].Exp == 0
                  && LifeSystem.GetCharacters()[0].Level == 10,
                "차단하면 레벨·경험치가 그대로다");
            Environment.SetEnvironmentVariable("QA_NO_HUNT_EXP", null);

            Economy.ForceRaceXpMul = 1.15f;
            long human = Economy.WaveHuntExp(0, 100f);
            Economy.ForceRaceXpMul = 1f;
            long elf = Economy.WaveHuntExp(0, 100f);
            Check(human == 6670 && elf == 5800,
                $"인간 +15% (인간 {human} / 엘프 {elf})");

            Environment.SetEnvironmentVariable("QA_HUNT_EXP", "1");
            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            Economy.ForceRaceXpMul = 1f;
            LifeSystem.SeedHuntExpQaIfRequested();
            Check(LifeSystem.GetCharacters()[0].Level == 11, "QA 시드는 솔로로 Lv11");
            Environment.SetEnvironmentVariable("QA_HUNT_EXP", qaHunt);
            Environment.SetEnvironmentVariable("QA_NO_HUNT_EXP", noHunt);
            Economy.ForceRaceXpMul = oldRace;

            _ = nameof(LifeSystem.AwardWaveHunt);
            _ = nameof(Economy.WaveHuntExp);
            _ = nameof(LifeSystem.SeedHuntExpQaIfRequested);
            _ = nameof(BattleScreen.SeedHuntExpRewardQaIfRequested);

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();

            if (_fail == 0) Debug.Log("[HuntExpSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[HuntExpSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[HuntExpSelfCheck] FAIL {_fail}건");
        }

        static long ExtraSpent(CharacterRecord c, int fromLevel)
        {
            long spent = 0;
            for (int lv = fromLevel; lv < c.Level; lv++)
                spent += LifeSystem.ExpToNext(lv);
            return spent;
        }
    }
}
