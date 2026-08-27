using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// G3 권장 파티 픽스처가 실제 장비·합성 CombatMuls 경로를 심는지.
    /// QA_NO_G3_GEAR면 옛 베어 로스터. 밸런스 수치는 여기서 맞추지 않는다.
    /// </summary>
    public static class TowerClimbG3FixtureSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Tower Climb G3 Fixture Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string old = Environment.GetEnvironmentVariable(TowerClimbCurveMeasure.EnvNoG3Gear);
            GameObject go = null;
            try
            {
                Environment.SetEnvironmentVariable(TowerClimbCurveMeasure.EnvNoG3Gear, null);
                GameState.ResetAll();
                LifeSystem.ResetAll();
                PartyState.ResetForTest();
                DefenseState.ResetForTest();

                int lv = BossHp.ExpectedLevel(TowerClimbCurveMeasure.TopFloor);
                var adv = lv >= 50 ? AdvancementTier.Second
                    : lv >= 20 ? AdvancementTier.First
                    : AdvancementTier.Basic;
                Check(adv != AdvancementTier.Basic, $"50층 기대 전직이 기본이 아니다 (실제 {adv})");

                TowerClimbCurveMeasure.SeedRecommendedRoster(lv, adv);
                Check(!TowerClimbCurveMeasure.G3GearBlocked, "기본은 QA_NO_G3_GEAR가 꺼져 있다");

                var roster = LifeSystem.GetCharacters();
                int n = roster.Count;
                int first = Mathf.Max(0, n - LifeSystem.BasicJobs.Length);
                Check(n - first == LifeSystem.BasicJobs.Length, $"권장 5인을 심었다 (실제 {n - first})");

                var tank = roster[first];
                var dps = roster[first + 1];
                var mage = roster[first + 2];
                Check(tank.Level == lv && tank.Advancement == adv, $"탱 Lv{lv}·{adv}");
                Check(Equipment.WornAll(tank).Count == Equipment.SlotCount,
                    $"탱 6부위 장착 (실제 {Equipment.WornAll(tank).Count})");
                Check(Equipment.WornAll(dps).Count == Equipment.SlotCount,
                    $"딜 6부위 장착 (실제 {Equipment.WornAll(dps).Count})");
                Check(Equipment.WornAll(mage).Count == Equipment.SlotCount - 1,
                    $"마딜은 물리 무기를 빼고 5부위 (실제 {Equipment.WornAll(mage).Count})");
                Check(Equipment.HpMulOf(tank) > 1.01f,
                    $"장비 CombatMuls(HpMulOf)가 1이 아니다 (실제 {Equipment.HpMulOf(tank):0.000})");

                var tankFuse = Fusion.CombatOf(tank);
                var dpsFuse = Fusion.CombatOf(dps);
                Check(tank.AbsorbedBoons.Contains((int)BoonId.강골), "탱 합성 강골");
                Check(dps.AbsorbedBoons.Contains((int)BoonId.예리함), "딜 합성 예리함");
                Check(Mathf.Approximately(tankFuse.Hp, 1.25f),
                    $"Fusion.CombatOf 강골 HP 1.25 (실제 {tankFuse.Hp:0.00})");
                Check(Mathf.Approximately(dpsFuse.Atk, 1.20f),
                    $"Fusion.CombatOf 예리함 Atk 1.20 (실제 {dpsFuse.Atk:0.00})");
                Check(!Mathf.Approximately(dpsFuse.AtkSpd, 1f),
                    $"딜 분노 AtkSpd가 Identity가 아니다 (실제 {dpsFuse.AtkSpd:0.00})");

                var sortie = PartyState.SortieCombatants();
                Check(sortie.Count == 5, $"출전 5인 (실제 {sortie.Count})");
                Check(sortie[0].HpMul > 1.01f,
                    $"출전 계약이 장비×합성 HP를 싣는다 (실제 {sortie[0].HpMul:0.000})");
                Check(Mathf.Approximately(sortie[1].Fuse.Atk, 1.20f),
                    $"출전 계약이 예리함 Atk를 싣는다 (실제 {sortie[1].Fuse.Atk:0.00})");
                Check(global::W3Party.GearHpMultiplier(sortie[0].HpMul) > 1.01f,
                    "전투 HP 경로가 출전 배율을 읽는다");
                Check(Mathf.Approximately(global::W3Party.FusionStatMultiplier(sortie[1].Fuse.Atk), 1.20f),
                    "전투 경로가 예리함 배율을 읽는다");

                go = BuildParty(out var party);
                party.ApplyGameParty();
                float gearedHp = global::W3Party.ActivePartyHp;
                Check(gearedHp > 0f, $"권장 파티 HP가 0-경로가 아니다 (실제 {gearedHp:0})");

                Environment.SetEnvironmentVariable(TowerClimbCurveMeasure.EnvNoG3Gear, "1");
                Check(TowerClimbCurveMeasure.G3GearBlocked, "QA_NO_G3_GEAR면 차단");
                TowerClimbCurveMeasure.SeedRecommendedRoster(lv, adv);
                var bareRoster = LifeSystem.GetCharacters();
                int bareFirst = Mathf.Max(0, bareRoster.Count - LifeSystem.BasicJobs.Length);
                var bareTank = bareRoster[bareFirst];
                var bareDps = bareRoster[bareFirst + 1];
                Check(bareTank.Level == lv && bareTank.Advancement == adv,
                    "QA_NO도 레벨·전직은 남긴다(옛 픽스처)");
                Check(Equipment.WornAll(bareTank).Count == 0, "QA_NO면 장비 없음");
                Check(bareTank.AbsorbedBoons.Count == 0 && bareDps.AbsorbedBoons.Count == 0,
                    "QA_NO면 합성 없음");
                Check(Mathf.Approximately(Equipment.HpMulOf(bareTank), 1f),
                    "QA_NO면 Equipment.HpMulOf=1");
                var bareFuse = Fusion.CombatOf(bareDps);
                Check(Mathf.Approximately(bareFuse.Atk, 1f) && Mathf.Approximately(bareFuse.Hp, 1f),
                    "QA_NO면 Fusion.CombatOf Identity");
                var bareSortie = PartyState.SortieCombatants();
                Check(bareSortie.Count == 5 && Mathf.Approximately(bareSortie[0].HpMul, 1f)
                      && Mathf.Approximately(bareSortie[1].Fuse.Atk, 1f),
                    "QA_NO 출전 계약은 베어 로스터");

                party.ApplyGameParty();
                float bareHp = global::W3Party.ActivePartyHp;
                Check(gearedHp > bareHp * 1.05f,
                    $"권장 파티 전투 HP가 베어보다 크다: 권장 {gearedHp:0} vs 베어 {bareHp:0}");

                Environment.SetEnvironmentVariable(TowerClimbCurveMeasure.EnvNoG3Gear, null);
                TowerClimbCurveMeasure.SeedRecommendedRoster(1, AdvancementTier.Basic);
                var weak = LifeSystem.GetCharacters();
                int weakFirst = Mathf.Max(0, weak.Count - LifeSystem.BasicJobs.Length);
                Check(Equipment.WornAll(weak[weakFirst]).Count == 0
                      && weak[weakFirst].AbsorbedBoons.Count == 0
                      && Mathf.Approximately(Equipment.HpMulOf(weak[weakFirst]), 1f),
                    "약한 파티(Lv1 기본직)는 환경 없이도 베어다");

                _ = nameof(Equipment.TryGrantDrop);
                _ = nameof(Equipment.TryEquip);
                _ = nameof(Equipment.HpMulOf);
                _ = nameof(Fusion.CombatOf);
                _ = nameof(TowerClimbCurveMeasure.SeedRecommendedRoster);
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                Environment.SetEnvironmentVariable(TowerClimbCurveMeasure.EnvNoG3Gear, old);
                GameState.ResetAll();
                LifeSystem.ResetAll();
                PartyState.ResetForTest();
            }

            if (_fail == 0) Debug.Log("[TowerClimbG3FixtureSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[TowerClimbG3FixtureSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[TowerClimbG3FixtureSelfCheck] FAIL {_fail}건");
        }

        static GameObject BuildParty(out global::W3Party party)
        {
            var go = new GameObject("TowerClimbG3FixtureSelfCheck");
            go.SetActive(false);
            party = TestAttach.AttachWithAwake<global::W3Party>(go, p => { p.GameMode = true; });
            return go;
        }
    }
}
