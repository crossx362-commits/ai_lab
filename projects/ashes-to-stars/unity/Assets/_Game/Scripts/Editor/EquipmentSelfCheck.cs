using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 대장간 첫 슬라이스 자가검사. 계산이 아니라 다음 판에서 읽히는지를 본다.
    /// </summary>
    public static class EquipmentSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Equipment Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();
            DefenseState.ResetForTest();

            var roster = LifeSystem.GetCharacters();
            Check(roster.Count >= 1, $"로스터 자동 생성 (실제 {roster.Count})");
            var tank = roster[0];

            Check(!Equipment.SmithUnlocked(), "기본직업만 있으면 대장간 잠김(§13-2)");
            Check(!Equipment.TryCraftLeatherArmor(), "잠긴 대장간은 가죽이 있어도 제작하지 않는다");

            GameState.Gain(Economy.LifeItem.CraftHide, Equipment.LeatherArmorHideCost);
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) == Equipment.LeatherArmorHideCost,
                "사냥 가죽 5장 획득");
            Check(!Equipment.TryCraftLeatherArmor(), "1차 전직 전에는 가죽 5장으로도 못 만든다");

            tank.Advancement = AdvancementTier.First;
            tank.Job = "수호기사";
            LifeSystem.PersistRoster();
            Check(Equipment.SmithUnlocked(), "1차 전직 후 대장간 해금");

            Check(Equipment.TryCraftLeatherArmor(), "가죽 5장 → 가죽 흉갑 1개");
            Check(GameState.Bag.GetCount(Economy.LifeItem.CraftHide) == 0, "제작이 가죽을 소비한다");
            Check(Equipment.All.Count == 1 && Equipment.All[0].Name == Equipment.LeatherArmorName,
                "제작된 흉갑이 가방에 있다");
            Check(!Equipment.TryCraftLeatherArmor(), "가죽 0장이면 제작 거부");

            Check(Equipment.TryEquip(tank, Equipment.All[0].Id), "흉갑 장착");
            Check(Mathf.Approximately(Equipment.HpMulOf(tank), Equipment.LeatherArmorHpMul),
                $"장착 체력 배율 {Equipment.LeatherArmorHpMul}");

            PartyState.ResetForTest();
            var sortie = PartyState.SortieCombatants();
            Check(sortie.Count > 0 && Mathf.Approximately(sortie[0].HpMul, Equipment.LeatherArmorHpMul),
                "출전 계약이 갑옷 배율을 전투에 넘긴다");

            float geared = global::W3Party.GearHpMultiplier(Equipment.LeatherArmorHpMul);
            Check(Mathf.Approximately(geared, Equipment.LeatherArmorHpMul),
                $"전투 HP에 갑옷이 곱해진다 ({geared})");
            string old = Environment.GetEnvironmentVariable("QA_NO_GEAR");
            Environment.SetEnvironmentVariable("QA_NO_GEAR", "1");
            Check(Mathf.Approximately(global::W3Party.GearHpMultiplier(Equipment.LeatherArmorHpMul), 1f),
                "QA_NO_GEAR=1이면 갑옷 배율이 1로 돌아간다");
            Environment.SetEnvironmentVariable("QA_NO_GEAR", old);

            GameState.ForgetInMemoryForTest();
            LifeSystem.ForgetInMemoryForTest();
            Equipment.ForgetInMemoryForTest();
            PartyState.ResetForTest();
            roster = LifeSystem.GetCharacters();
            tank = roster[0];
            Check(tank.Advancement == AdvancementTier.First
                  && Mathf.Approximately(Equipment.HpMulOf(tank), Equipment.LeatherArmorHpMul),
                "재기동 후에도 장착과 1차 전직이 남는다");

            for (int k = 0; k < 3; k++) LifeSystem.RegisterDeath(tank);
            Check(tank.IsDeleted, "3회 사망으로 삭제");
            Check(Equipment.All.Count == 0, "삭제 시 장착 장비가 사라진다(§4·§11)");
            Check(string.IsNullOrEmpty(tank.EquippedArmorId), "삭제된 캐릭터 슬롯은 비어 있다");

            GameState.Gain(Economy.LifeItem.RebornStone, 1);
            Check(LifeSystem.UseRebornStone(tank), "환생석으로 복구");
            Check(string.IsNullOrEmpty(tank.EquippedArmorId) && Equipment.HpMulOf(tank) == 1f,
                "환생한 캐릭터는 장비 없이 돌아온다(§4)");

            var rng = Rng.Stream(1, 0, SeedChannel.Drop);
            var drops = Economy.RollBattleDrops(Economy.DropSource.FieldDungeonBoss, 3, ref rng);
            Check(Economy.FieldHuntHideCount() == 1, "필드 사냥 생존은 가죽 1장");
            bool tableHasHide = false;
            for (uint s = 1; s <= 200; s++)
            {
                var r = Rng.Stream(s, 0, SeedChannel.Drop);
                foreach (var d in Economy.RollBattleDrops(Economy.DropSource.FieldDungeonBoss, 1, ref r))
                    if (d == Economy.LifeItem.CraftHide) tableHasHide = true;
            }
            Check(tableHasHide, "던전 보스 드랍 테이블에 사냥 가죽이 있다");
            Check(drops != null, "드랍 판정이 예외 없이 돈다");

            if (_fail == 0) Debug.Log("[EquipmentSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EquipmentSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EquipmentSelfCheck] FAIL {_fail}건");
        }
    }
}
