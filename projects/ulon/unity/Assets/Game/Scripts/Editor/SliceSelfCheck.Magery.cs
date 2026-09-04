using System;
using System.IO;
using FishNet.Object;
using Ulon.Server;
using Ulon.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        static void AssertCastInterrupt()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            if (SpellCast.BoltCastSeconds <= 0f)
                throw new InvalidOperationException("BoltCastSeconds는 양수여야 합니다.");
            if (!SpellCast.Interruptible(SpellId.Bolt))
                throw new InvalidOperationException("Bolt는 interruptible이어야 합니다.");
            if (SpellCast.Interruptible(SpellId.Ember) || SpellCast.Interruptible(SpellId.Mend) || SpellCast.Interruptible(SpellId.Cleanse) || SpellCast.Interruptible(SpellId.Ward) || SpellCast.Interruptible(SpellId.Bind) || SpellCast.Interruptible(SpellId.Weaken) || SpellCast.Interruptible(SpellId.Spark))
                throw new InvalidOperationException("Ember/Mend/Cleanse/Ward/Bind/Weaken/Spark는 이 슬라이스에서 non-interruptible이어야 합니다.");
            if (SpellCast.CastTimeOf(SpellId.Ember) != 0f || SpellCast.CastTimeOf(SpellId.Mend) != 0f || SpellCast.CastTimeOf(SpellId.Cleanse) != 0f || SpellCast.CastTimeOf(SpellId.Ward) != 0f || SpellCast.CastTimeOf(SpellId.Bind) != 0f || SpellCast.CastTimeOf(SpellId.Weaken) != 0f || SpellCast.CastTimeOf(SpellId.Spark) != 0f)
                throw new InvalidOperationException("Ember/Mend/Cleanse/Ward/Bind/Weaken/Spark CastTime은 0이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-cast-int-world");
            GameObject casterGo = null;
            GameObject tgtGo = null;
            GameObject atkGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-cast-int-caster");
                casterGo.transform.position = new Vector3(40f, 0f, 40f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "cast-int-caster";
                caster.MaxHp = 80f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(40, 20, 40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 6);
                world.BookOf(caster).Learn(SpellId.Ember);
                world.BookOf(caster).Learn(SpellId.Bolt);

                tgtGo = new GameObject("selfcheck-cast-int-tgt");
                tgtGo.transform.position = casterGo.transform.position + new Vector3(6f, 0f, 0f);
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MaxHp = 100f;
                tgt.ResetHp();

                atkGo = new GameObject("selfcheck-cast-int-atk");
                atkGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var atk = atkGo.AddComponent<WorldBody>();
                atk.IsAvatar = true;
                atk.IsEnemy = false;
                atk.CharacterId = "cast-int-atk";
                atk.MaxHp = 50f;
                atk.ResetHp();
                world.StatsOf(atk).ForceSet(40, 20, 20);
                atk.RecalcFromStr(40);
                atkGo.AddComponent<InventoryBag>().Add(ItemCatalog.IronSword, 1);

                float mana0 = caster.Mana;
                float hpT0 = tgt.Hp;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                var start = world.TryCast(caster, SpellId.Bolt, tgt);
                if (!start.Applied || !caster.IsCasting(Time.time))
                    throw new InvalidOperationException("시전 중단 테스트: 벼락 풍업 시작 실패: " + start.FailReason);
                if (tgt.Hp != hpT0)
                    throw new InvalidOperationException("풍업 직후 주문 효과가 나가면 안 됩니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("풍업 시작 시 마나가 소모되어야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("풍업 시작 시 시약이 소모되어야 합니다.");

                float manaAfterSpend = caster.Mana;
                float casterHp0 = caster.Hp;
                var hit = world.TryAttack(atk, caster);
                if (!hit.Applied || hit.Damage <= 0 || caster.Hp >= casterHp0)
                    throw new InvalidOperationException("시전 중단 테스트: TryAttack 피격 실패: " + hit.FailReason);
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("피격 후 CastingUntil이 취소되어야 합니다.");

                world.TickCast(Time.time + SpellCast.BoltCastSeconds + 0.1f);
                if (tgt.Hp != hpT0)
                    throw new InvalidOperationException("중단된 벼락은 효과를 내면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("중단된 벼락은 마법을 올리면 안 됩니다.");
                if (Math.Abs(caster.Mana - manaAfterSpend) > 0.0001f)
                    throw new InvalidOperationException("중단 시 마나는 환불되면 안 됩니다.");

                // complete path: start cast → wait → spell fires
                caster.SetMana(caster.MaxMana);
                bag.Add(SpellCast.Reagent, 2);
                float hpT1 = tgt.Hp;
                float manaB = caster.Mana;
                var start2 = world.TryCast(caster, SpellId.Bolt, tgt);
                if (!start2.Applied || !caster.IsCasting(Time.time))
                    throw new InvalidOperationException("완료 경로: 벼락 풍업 시작 실패: " + start2.FailReason);
                world.TickCast(Time.time + SpellCast.BoltCastSeconds + 0.1f);
                if (caster.IsCasting(Time.time + SpellCast.BoltCastSeconds + 0.1f))
                    throw new InvalidOperationException("완료 경로: 풍업 후 시전이 남아 있으면 안 됩니다.");
                if (tgt.Hp >= hpT1)
                    throw new InvalidOperationException("완료 경로: 벼락이 맞아야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("완료 경로: 마법이 0.1이어야 합니다.");
                if (caster.Mana >= manaB)
                    throw new InvalidOperationException("완료 경로: 마나가 소모되어야 합니다.");

                // Ember stays instant / non-interruptible
                tgt.ResetHp();
                float emberHp = tgt.Hp;
                var ember = world.TryCast(caster, SpellId.Ember, tgt);
                if (!ember.Applied || ember.Hit != true || tgt.Hp >= emberHp)
                    throw new InvalidOperationException("불씨는 즉시 시전되어야 합니다: " + ember.FailReason);
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("불씨 후 CastingUntil이 있으면 안 됩니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (atkGo != null)
                    UnityEngine.Object.DestroyImmediate(atkGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }

        static void AssertCleanse()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Cleanse) != "정화")
                throw new InvalidOperationException("SpellId.Cleanse 한글은 정화이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Cleanse) != SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("정화 마나는 불씨와 같아야 합니다.");
            if (SpellCast.Interruptible(SpellId.Cleanse) || SpellCast.CastTimeOf(SpellId.Cleanse) != 0f)
                throw new InvalidOperationException("정화는 즉시 시전이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-cleanse-world");
            GameObject casterGo = null;
            GameObject allyGo = null;
            GameObject foeGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-cleanse-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "cleanse-caster";
                caster.MaxHp = 60f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(20, 20, 40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 6);

                var unknown = world.TryCast(caster, SpellId.Cleanse, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 정화는 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Cleanse);

                caster.PoisonTicks = PoisoningResolve.TickCount;
                caster.NextPoisonAt = Time.time + 10f;
                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                var selfHit = world.TryCast(caster, SpellId.Cleanse, null);
                if (!selfHit.Applied)
                    throw new InvalidOperationException("자가 정화 실패: " + selfHit.FailReason);
                if (caster.PoisonTicks != 0 || caster.NextPoisonAt != 0f)
                    throw new InvalidOperationException("정화 후 독 틱이 남아 있으면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("정화 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("정화는 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("정화는 시약 1을 써야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("정화 후 CastingUntil이 있으면 안 됩니다.");

                allyGo = new GameObject("selfcheck-cleanse-ally");
                allyGo.transform.position = casterGo.transform.position + new Vector3(2f, 0f, 0f);
                var ally = allyGo.AddComponent<WorldBody>();
                ally.IsAvatar = true;
                ally.IsEnemy = false;
                ally.CharacterId = "cleanse-ally";
                ally.MaxHp = 50f;
                ally.ResetHp();
                ally.PoisonTicks = PoisoningResolve.TickCount;
                ally.NextPoisonAt = Time.time + 5f;

                caster.SetMana(caster.MaxMana);
                bag.Add(SpellCast.Reagent, 2);
                var allyHit = world.TryCast(caster, SpellId.Cleanse, ally);
                if (!allyHit.Applied)
                    throw new InvalidOperationException("아군 정화 실패: " + allyHit.FailReason);
                if (ally.PoisonTicks != 0 || ally.NextPoisonAt != 0f)
                    throw new InvalidOperationException("아군 정화 후 독이 남아 있으면 안 됩니다.");

                foeGo = new GameObject("selfcheck-cleanse-foe");
                foeGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var foe = foeGo.AddComponent<WorldBody>();
                foe.IsEnemy = true;
                foe.MaxHp = 40f;
                foe.ResetHp();
                foe.PoisonTicks = PoisoningResolve.TickCount;
                caster.SetMana(caster.MaxMana);
                bag.Add(SpellCast.Reagent, 1);
                var foeHit = world.TryCast(caster, SpellId.Cleanse, foe);
                if (foeHit.Applied || foeHit.FailReason != "no_target")
                    throw new InvalidOperationException("적 정화는 실패해야 합니다.");
                if (foe.PoisonTicks != PoisoningResolve.TickCount)
                    throw new InvalidOperationException("실패한 적 정화는 독을 지우면 안 됩니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (allyGo != null)
                    UnityEngine.Object.DestroyImmediate(allyGo);
                if (foeGo != null)
                    UnityEngine.Object.DestroyImmediate(foeGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }

        static void AssertWard()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Ward) != "수호")
                throw new InvalidOperationException("SpellId.Ward 한글은 수호이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Ward) != SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("수호 마나는 불씨와 같아야 합니다.");
            if (SpellCast.WardSeconds != 8f)
                throw new InvalidOperationException("WardSeconds는 8이어야 합니다.");
            if (SpellCast.Interruptible(SpellId.Ward) || SpellCast.CastTimeOf(SpellId.Ward) != 0f)
                throw new InvalidOperationException("수호는 즉시 시전이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-ward-world");
            GameObject casterGo = null;
            GameObject atkGo = null;
            GameObject atk2Go = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-ward-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "ward-caster";
                caster.MaxHp = 120f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(40, 20, 40);
                caster.RecalcFromStr(40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 6);

                var unknown = world.TryCast(caster, SpellId.Ward, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 수호는 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Ward);

                atkGo = new GameObject("selfcheck-ward-atk");
                atkGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var atk = atkGo.AddComponent<WorldBody>();
                atk.IsAvatar = true;
                atk.IsEnemy = false;
                atk.CharacterId = "ward-atk";
                atk.MaxHp = 50f;
                atk.ResetHp();
                world.StatsOf(atk).ForceSet(40, 20, 20);
                atk.RecalcFromStr(40);
                atkGo.AddComponent<InventoryBag>().Add(ItemCatalog.IronSword, 1);

                float hp0 = caster.Hp;
                var baseHit = world.TryAttack(atk, caster);
                if (!baseHit.Applied || baseHit.Damage <= 0 || caster.Hp >= hp0)
                    throw new InvalidOperationException("수호 기준 타격 실패: " + baseHit.FailReason);
                int baseDmg = baseHit.Damage;
                caster.ResetHp();
                caster.WardUntil = 0f;

                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                var cast = world.TryCast(caster, SpellId.Ward, null);
                if (!cast.Applied)
                    throw new InvalidOperationException("수호 시전 실패: " + cast.FailReason);
                if (!caster.IsWarded(Time.time))
                    throw new InvalidOperationException("수호 후 WardUntil이 활성이어야 합니다.");
                if (caster.WardUntil < Time.time + SpellCast.WardSeconds - 0.05f)
                    throw new InvalidOperationException("WardUntil은 약 8초여야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("수호 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("수호는 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("수호는 시약 1을 써야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("수호 후 CastingUntil이 있으면 안 됩니다.");

                atk2Go = new GameObject("selfcheck-ward-atk2");
                atk2Go.transform.position = casterGo.transform.position + new Vector3(1.2f, 0f, 0f);
                var atk2 = atk2Go.AddComponent<WorldBody>();
                atk2.IsAvatar = true;
                atk2.IsEnemy = false;
                atk2.CharacterId = "ward-atk2";
                atk2.MaxHp = 50f;
                atk2.ResetHp();
                world.StatsOf(atk2).ForceSet(40, 20, 20);
                atk2.RecalcFromStr(40);
                atk2Go.AddComponent<InventoryBag>().Add(ItemCatalog.IronSword, 1);

                float hp1 = caster.Hp;
                var wardedHit = world.TryAttack(atk2, caster);
                if (!wardedHit.Applied)
                    throw new InvalidOperationException("수호 중 타격 실패: " + wardedHit.FailReason);
                int expect = baseDmg / 2;
                if (wardedHit.Damage != expect)
                    throw new InvalidOperationException("수호 중 피해는 절반이어야 합니다: " + wardedHit.Damage + " vs " + expect + " (base " + baseDmg + ")");
                if (Math.Abs((hp1 - caster.Hp) - wardedHit.Damage) > 0.0001f)
                    throw new InvalidOperationException("수호 중 HP 감소가 Damage와 일치해야 합니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (atkGo != null)
                    UnityEngine.Object.DestroyImmediate(atkGo);
                if (atk2Go != null)
                    UnityEngine.Object.DestroyImmediate(atk2Go);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }



        static void AssertBind()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Bind) != "속박")
                throw new InvalidOperationException("SpellId.Bind 한글은 속박이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Bind) != SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("속박 마나는 불씨와 같아야 합니다.");
            if (SpellCast.BindSeconds != 4f)
                throw new InvalidOperationException("BindSeconds는 4이어야 합니다.");
            if (SpellCast.Interruptible(SpellId.Bind) || SpellCast.CastTimeOf(SpellId.Bind) != 0f)
                throw new InvalidOperationException("속박은 즉시 시전이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-bind-world");
            GameObject casterGo = null;
            GameObject mobGo = null;
            GameObject palGo = null;
            GameObject farGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-bind-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "bind-caster";
                caster.MaxHp = 120f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(40, 20, 40);
                caster.RecalcFromStr(40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);

                var unknown = world.TryCast(caster, SpellId.Bind, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 속박은 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Bind);

                var noTgt = world.TryCast(caster, SpellId.Bind, null);
                if (noTgt.Applied || noTgt.FailReason != "no_target")
                    throw new InvalidOperationException("대상 없는 속박은 실패해야 합니다.");

                palGo = new GameObject("selfcheck-bind-pal");
                palGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsAvatar = true;
                pal.IsEnemy = false;
                pal.CharacterId = "bind-pal";
                pal.MaxHp = 50f;
                pal.ResetHp();
                var onPal = world.TryCast(caster, SpellId.Bind, pal);
                if (onPal.Applied || onPal.FailReason != "no_target")
                    throw new InvalidOperationException("속박은 아바타에 쓰면 안 됩니다.");

                mobGo = new GameObject("selfcheck-bind-mob");
                mobGo.transform.position = casterGo.transform.position + new Vector3(1.2f, 0f, 0f);
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.IsAvatar = false;
                mob.MobId = "bandit";
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                farGo = new GameObject("selfcheck-bind-far");
                farGo.transform.position = casterGo.transform.position + new Vector3(20f, 0f, 0f);
                var far = farGo.AddComponent<WorldBody>();
                far.IsEnemy = true;
                far.IsAvatar = false;
                far.MaxHp = 45f;
                far.ResetHp();
                var tooFar = world.TryCast(caster, SpellId.Bind, far);
                if (tooFar.Applied || tooFar.FailReason != "range")
                    throw new InvalidOperationException("사거리 밖 속박은 실패해야 합니다.");

                caster.Ghost = true;
                var ghostCast = world.TryCast(caster, SpellId.Bind, mob);
                if (ghostCast.Applied || ghostCast.FailReason != "ghost")
                    throw new InvalidOperationException("유령 속박은 실패해야 합니다.");
                caster.Ghost = false;
                caster.ResetHp();
                caster.SetMana(caster.MaxMana);

                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                Vector3 mobPos = mobGo.transform.position;
                var cast = world.TryCast(caster, SpellId.Bind, mob);
                if (!cast.Applied)
                    throw new InvalidOperationException("속박 시전 실패: " + cast.FailReason);
                if (!mob.IsRooted(Time.time))
                    throw new InvalidOperationException("속박 후 RootUntil이 활성이어야 합니다.");
                if (mob.RootUntil < Time.time + SpellCast.BindSeconds - 0.05f)
                    throw new InvalidOperationException("RootUntil은 약 4초여야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("속박 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("속박은 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("속박은 시약 1을 써야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("속박 후 CastingUntil이 있으면 안 됩니다.");
                if ((mobGo.transform.position - mobPos).sqrMagnitude > 0.0001f)
                    throw new InvalidOperationException("속박 시전은 몹 위치를 바꾸면 안 됩니다.");

                float hp0 = caster.Hp;
                bool struck = world.TryEnemyStrike(mob, caster);
                if (struck || caster.Hp < hp0 - 0.01f)
                    throw new InvalidOperationException("속박 중 몹은 추격/반격하면 안 됩니다.");

                mob.RootUntil = 0f;
                bool freeStrike = world.TryEnemyStrike(mob, caster);
                if (!freeStrike)
                    throw new InvalidOperationException("속박 해제 후 몹 반격이 되어야 합니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (palGo != null)
                    UnityEngine.Object.DestroyImmediate(palGo);
                if (farGo != null)
                    UnityEngine.Object.DestroyImmediate(farGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }


        static void AssertWeaken()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Weaken) != "약화")
                throw new InvalidOperationException("SpellId.Weaken 한글은 약화이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Weaken) != SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("약화 마나는 불씨와 같아야 합니다.");
            if (SpellCast.WeakenSeconds != 6f)
                throw new InvalidOperationException("WeakenSeconds는 6이어야 합니다.");
            if (SpellCast.Interruptible(SpellId.Weaken) || SpellCast.CastTimeOf(SpellId.Weaken) != 0f)
                throw new InvalidOperationException("약화는 즉시 시전이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-weaken-world");
            GameObject casterGo = null;
            GameObject mobGo = null;
            GameObject palGo = null;
            GameObject farGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-weaken-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "weaken-caster";
                caster.MaxHp = 120f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(40, 20, 40);
                caster.RecalcFromStr(40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);

                var unknown = world.TryCast(caster, SpellId.Weaken, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 약화는 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Weaken);

                var noTgt = world.TryCast(caster, SpellId.Weaken, null);
                if (noTgt.Applied || noTgt.FailReason != "no_target")
                    throw new InvalidOperationException("대상 없는 약화는 실패해야 합니다.");

                palGo = new GameObject("selfcheck-weaken-pal");
                palGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsAvatar = true;
                pal.IsEnemy = false;
                pal.CharacterId = "weaken-pal";
                pal.MaxHp = 50f;
                pal.ResetHp();
                var onPal = world.TryCast(caster, SpellId.Weaken, pal);
                if (onPal.Applied || onPal.FailReason != "no_target")
                    throw new InvalidOperationException("약화는 아바타에 쓰면 안 됩니다.");

                mobGo = new GameObject("selfcheck-weaken-mob");
                mobGo.transform.position = casterGo.transform.position + new Vector3(1.2f, 0f, 0f);
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.IsAvatar = false;
                mob.MobId = "bandit";
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                farGo = new GameObject("selfcheck-weaken-far");
                farGo.transform.position = casterGo.transform.position + new Vector3(20f, 0f, 0f);
                var far = farGo.AddComponent<WorldBody>();
                far.IsEnemy = true;
                far.IsAvatar = false;
                far.MaxHp = 45f;
                far.ResetHp();
                var tooFar = world.TryCast(caster, SpellId.Weaken, far);
                if (tooFar.Applied || tooFar.FailReason != "range")
                    throw new InvalidOperationException("사거리 밖 약화는 실패해야 합니다.");

                caster.Ghost = true;
                var ghostCast = world.TryCast(caster, SpellId.Weaken, mob);
                if (ghostCast.Applied || ghostCast.FailReason != "ghost")
                    throw new InvalidOperationException("유령 약화는 실패해야 합니다.");
                caster.Ghost = false;
                caster.ResetHp();
                caster.SetMana(caster.MaxMana);

                float hpBase = caster.Hp;
                bool baseStruck = world.TryEnemyStrike(mob, caster);
                if (!baseStruck || caster.Hp >= hpBase)
                    throw new InvalidOperationException("약화 기준 몹 반격이 되어야 합니다.");
                float baseLost = hpBase - caster.Hp;
                if (baseLost < 1f)
                    throw new InvalidOperationException("약화 기준 피해가 있어야 합니다.");
                caster.ResetHp();
                mob.WeakenUntil = 0f;

                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                var cast = world.TryCast(caster, SpellId.Weaken, mob);
                if (!cast.Applied)
                    throw new InvalidOperationException("약화 시전 실패: " + cast.FailReason);
                if (!mob.IsWeakened(Time.time))
                    throw new InvalidOperationException("약화 후 WeakenUntil이 활성이어야 합니다.");
                if (mob.WeakenUntil < Time.time + SpellCast.WeakenSeconds - 0.05f)
                    throw new InvalidOperationException("WeakenUntil은 약 6초여야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("약화 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("약화는 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("약화는 시약 1을 써야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("약화 후 CastingUntil이 있으면 안 됩니다.");

                float hp1 = caster.Hp;
                bool weakStruck = world.TryEnemyStrike(mob, caster);
                if (!weakStruck)
                    throw new InvalidOperationException("약화 중에도 몹 반격은 되어야 합니다.");
                float weakLost = hp1 - caster.Hp;
                float expect = baseLost / 2f;
                if (Math.Abs(weakLost - expect) > 0.0001f)
                    throw new InvalidOperationException("약화 중 피해는 절반이어야 합니다: " + weakLost + " vs " + expect + " (base " + baseLost + ")");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (palGo != null)
                    UnityEngine.Object.DestroyImmediate(palGo);
                if (farGo != null)
                    UnityEngine.Object.DestroyImmediate(farGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }



        static void AssertSpark()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Spark) != "섬광")
                throw new InvalidOperationException("SpellId.Spark 한글은 섬광이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Spark) != SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("섬광 마나는 불씨와 같아야 합니다.");
            if (SpellCast.SparkRange >= SpellCast.EmberRange)
                throw new InvalidOperationException("섬광 사거리는 불씨보다 짧아야 합니다.");
            if (SpellCast.RangeOf(SpellId.Spark) != SpellCast.SparkRange || SpellCast.SparkRange != 6f)
                throw new InvalidOperationException("섬광 사거리는 6이어야 합니다.");
            if (SpellCast.Interruptible(SpellId.Spark) || SpellCast.CastTimeOf(SpellId.Spark) != 0f)
                throw new InvalidOperationException("섬광은 즉시 시전이어야 합니다.");
            var plain = new StatSet();
            plain.ForceSet(20, 20, 20);
            if (SpellCast.SparkDamage(plain, new SkillSet()) >= SpellCast.EmberDamage(plain, new SkillSet()))
                throw new InvalidOperationException("섬광 피해는 불씨보다 낮아야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-spark-world");
            GameObject casterGo = null;
            GameObject mobGo = null;
            GameObject palGo = null;
            GameObject farGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-spark-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "spark-caster";
                caster.MaxHp = 120f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(40, 20, 40);
                caster.RecalcFromStr(40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);

                var unknown = world.TryCast(caster, SpellId.Spark, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 섬광은 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Spark);

                var noTgt = world.TryCast(caster, SpellId.Spark, null);
                if (noTgt.Applied || noTgt.FailReason != "no_target")
                    throw new InvalidOperationException("대상 없는 섬광은 실패해야 합니다.");

                palGo = new GameObject("selfcheck-spark-pal");
                palGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsAvatar = true;
                pal.IsEnemy = false;
                pal.CharacterId = "spark-pal";
                pal.MaxHp = 50f;
                pal.ResetHp();
                var onPal = world.TryCast(caster, SpellId.Spark, pal);
                if (onPal.Applied || onPal.FailReason != "no_target")
                    throw new InvalidOperationException("섬광은 아바타에 쓰면 안 됩니다.");

                mobGo = new GameObject("selfcheck-spark-mob");
                mobGo.transform.position = casterGo.transform.position + new Vector3(1.2f, 0f, 0f);
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.IsAvatar = false;
                mob.MobId = "bandit";
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                farGo = new GameObject("selfcheck-spark-far");
                farGo.transform.position = casterGo.transform.position + new Vector3(20f, 0f, 0f);
                var far = farGo.AddComponent<WorldBody>();
                far.IsEnemy = true;
                far.IsAvatar = false;
                far.MaxHp = 45f;
                far.ResetHp();
                var tooFar = world.TryCast(caster, SpellId.Spark, far);
                if (tooFar.Applied || tooFar.FailReason != "range")
                    throw new InvalidOperationException("사거리 밖 섬광은 실패해야 합니다.");

                // Ember range ok but Spark shorter: place mob between SparkRange and EmberRange
                farGo.transform.position = casterGo.transform.position + new Vector3(7f, 0f, 0f);
                var midFar = world.TryCast(caster, SpellId.Spark, far);
                if (midFar.Applied || midFar.FailReason != "range")
                    throw new InvalidOperationException("섬광은 불씨보다 짧은 사거리여야 합니다.");
                farGo.transform.position = casterGo.transform.position + new Vector3(20f, 0f, 0f);

                caster.Ghost = true;
                var ghostCast = world.TryCast(caster, SpellId.Spark, mob);
                if (ghostCast.Applied || ghostCast.FailReason != "ghost")
                    throw new InvalidOperationException("유령 섬광은 실패해야 합니다.");
                caster.Ghost = false;
                caster.ResetHp();
                caster.SetMana(caster.MaxMana);

                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                float hp0 = mob.Hp;
                var cast = world.TryCast(caster, SpellId.Spark, mob);
                if (!cast.Applied)
                    throw new InvalidOperationException("섬광 시전 실패: " + cast.FailReason);
                if (!cast.Hit || cast.Damage <= 0)
                    throw new InvalidOperationException("섬광은 피해를 줘야 합니다.");
                if (mob.Hp >= hp0)
                    throw new InvalidOperationException("섬광 후 몹 HP가 줄어야 합니다.");
                if (cast.Damage >= SpellCast.EmberDamage(world.StatsOf(caster), world.SkillsOf(caster)))
                    throw new InvalidOperationException("섬광 피해는 같은 스탯에서 불씨보다 낮아야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("섬광 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("섬광은 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("섬광은 시약 1을 써야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("섬광 후 CastingUntil이 있으면 안 됩니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (palGo != null)
                    UnityEngine.Object.DestroyImmediate(palGo);
                if (farGo != null)
                    UnityEngine.Object.DestroyImmediate(farGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }



        static void AssertCraftOrder()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if (GameObject.Find("DressVillage") != null)
                throw new InvalidOperationException("DressVillage 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            if (CraftOrderRules.DefaultItem != ItemCatalog.IronSword)
                throw new InvalidOperationException("기본 제작의뢰는 iron_sword여야 합니다.");
            if (CraftOrderRules.GoldReward != 10)
                throw new InvalidOperationException("제작의뢰 골드 보상은 10이어야 합니다.");
            if (CraftOrderRules.Amount != 1)
                throw new InvalidOperationException("제작의뢰 수량은 1이어야 합니다.");

            var noOrder = CraftOrderResolve.TurnIn(new CraftOrderRequest { HasStation = true, Distance = 0f, ActiveOrder = "", HasMatchingCrafted = true });
            if (noOrder.Applied || noOrder.FailReason != "no_order")
                throw new InvalidOperationException("주문 없으면 납품 실패여야 합니다.");
            var wrong = CraftOrderResolve.TurnIn(new CraftOrderRequest { HasStation = true, Distance = 0f, ActiveOrder = ItemCatalog.IronSword, HasMatchingCrafted = false });
            if (wrong.Applied || wrong.FailReason != "wrong_item")
                throw new InvalidOperationException("잘못된 아이템 납품은 실패여야 합니다.");
            var already = CraftOrderResolve.Accept(new CraftOrderRequest { HasStation = true, Distance = 0f, ActiveOrder = ItemCatalog.IronSword, OfferItem = ItemCatalog.IronSword });
            if (already.Applied || already.FailReason != "already")
                throw new InvalidOperationException("이미 의뢰가 있으면 수락 실패여야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-craftorder-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                var forge = OfflineWorld.FindStation("Forge");
                if (forge == null)
                    throw new InvalidOperationException("Forge가 있어야 합니다.");

                bodyGo = new GameObject("selfcheck-craftorder-body");
                bodyGo.transform.position = forge.transform.position;
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "craft-order-smith";
                body.Gold = 0;
                body.ResetHp();
                var bag = bodyGo.AddComponent<InventoryBag>();

                var far = CraftOrderResolve.Accept(new CraftOrderRequest { HasStation = true, Distance = 99f, ActiveOrder = "", OfferItem = CraftOrderRules.DefaultItem });
                if (far.Applied || far.FailReason != "range")
                    throw new InvalidOperationException("멀리서 수락은 range 실패여야 합니다.");

                var accept = world.TryAcceptOrder(body, forge);
                if (!accept.Applied)
                    throw new InvalidOperationException("제작의뢰 수락 실패: " + accept.FailReason);
                if (body.ActiveCraftOrder != ItemCatalog.IronSword)
                    throw new InvalidOperationException("수락 후 ActiveCraftOrder가 iron_sword여야 합니다.");

                var dup = world.TryAcceptOrder(body, forge);
                if (dup.Applied || dup.FailReason != "already")
                    throw new InvalidOperationException("중복 수락은 already여야 합니다.");

                var emptyTurn = world.TryTurnInOrder(body, forge);
                if (emptyTurn.Applied || emptyTurn.FailReason != "wrong_item")
                    throw new InvalidOperationException("아이템 없으면 wrong_item이어야 합니다.");

                // Unmarked / wrong maker sword must not turn in
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 40, MakerId = "other" });
                var wrongMaker = world.TryTurnInOrder(body, forge);
                if (wrongMaker.Applied || wrongMaker.FailReason != "wrong_item")
                    throw new InvalidOperationException("타제작 검 납품은 실패해야 합니다.");

                bag.Add(new ItemRecord { TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 40, MakerId = body.CharacterId });
                world.SkillsOf(body).ForceSet(SkillId.Blacksmithing, 0f, SkillLock.Up);
                int gold0 = body.Gold;
                int swords0 = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.IronSword)
                        swords0 += bag.Items[i].Amount;

                var turn = world.TryTurnInOrder(body, forge);
                if (!turn.Applied)
                    throw new InvalidOperationException("납품 실패: " + turn.FailReason);
                if (body.Gold != gold0 + CraftOrderRules.GoldReward)
                    throw new InvalidOperationException("납품 후 골드가 +" + CraftOrderRules.GoldReward + "이어야 합니다.");
                if (!string.IsNullOrEmpty(body.ActiveCraftOrder))
                    throw new InvalidOperationException("납품 후 의뢰가 비어야 합니다.");
                int swords1 = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.IronSword)
                        swords1 += bag.Items[i].Amount;
                if (swords1 != swords0 - 1)
                    throw new InvalidOperationException("납품 시 제작 검 1개가 소모되어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Blacksmithing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("납품 후 대장 숙련이 0.1이어야 합니다.");

                var noAgain = world.TryTurnInOrder(body, forge);
                if (noAgain.Applied || noAgain.FailReason != "no_order")
                    throw new InvalidOperationException("의뢰 클리어 후 납품은 no_order여야 합니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }

        static void AssertRestore()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Restore) != "회복")
                throw new InvalidOperationException("SpellId.Restore 한글은 회복이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Restore) <= SpellCast.ManaCost(SpellId.Mend))
                throw new InvalidOperationException("회복 마나는 봉합보다 커야 합니다.");
            if (SpellCast.ReagentCost(SpellId.Restore) <= SpellCast.ReagentCost(SpellId.Mend))
                throw new InvalidOperationException("회복 시약은 봉합보다 커야 합니다.");
            if (SpellCast.Interruptible(SpellId.Restore) || SpellCast.CastTimeOf(SpellId.Restore) != 0f)
                throw new InvalidOperationException("회복은 즉시 시전이어야 합니다.");
            var plain = new StatSet();
            plain.ForceSet(20, 20, 40);
            if (SpellCast.RestoreHeal(plain) <= SpellCast.MendHeal(plain))
                throw new InvalidOperationException("회복 치유량은 봉합보다 커야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-restore-world");
            GameObject casterGo = null;
            GameObject allyGo = null;
            GameObject foeGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-restore-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "restore-caster";
                caster.MaxHp = 80f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(20, 20, 40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);

                var unknown = world.TryCast(caster, SpellId.Restore, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 회복은 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Restore);

                caster.SetHp(20f);
                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                var selfHit = world.TryCast(caster, SpellId.Restore, null);
                if (!selfHit.Applied)
                    throw new InvalidOperationException("자가 회복 실패: " + selfHit.FailReason);
                int expectHeal = SpellCast.RestoreHeal(world.StatsOf(caster));
                if (caster.Hp < 20f + expectHeal - 0.01f && caster.Hp < caster.MaxHp - 0.01f)
                    throw new InvalidOperationException("회복 후 HP가 올라야 합니다.");
                if (caster.Hp <= 20f)
                    throw new InvalidOperationException("회복은 HP를 올려야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("회복 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("회복은 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - SpellCast.ReagentCost(SpellId.Restore))
                    throw new InvalidOperationException("회복은 시약을 봉합보다 많이 써야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("회복 후 CastingUntil이 있으면 안 됩니다.");

                // Mend heals less than Restore at same INT
                world.BookOf(caster).Learn(SpellId.Mend);
                world.SkillsOf(caster).ForceSet(SkillId.Magery, 0f, SkillLock.Up);
                caster.SetHp(10f);
                caster.SetMana(caster.MaxMana);
                bag.Add(SpellCast.Reagent, 4);
                float beforeMend = caster.Hp;
                var mendHit = world.TryCast(caster, SpellId.Mend, caster);
                if (!mendHit.Applied)
                    throw new InvalidOperationException("비교용 봉합 실패: " + mendHit.FailReason);
                float mendGain = caster.Hp - beforeMend;
                world.SkillsOf(caster).ForceSet(SkillId.Magery, 0f, SkillLock.Up);
                caster.SetHp(10f);
                caster.SetMana(caster.MaxMana);
                float beforeRestore = caster.Hp;
                var restoreCmp = world.TryCast(caster, SpellId.Restore, caster);
                if (!restoreCmp.Applied)
                    throw new InvalidOperationException("비교용 회복 실패: " + restoreCmp.FailReason);
                float restoreGain = caster.Hp - beforeRestore;
                if (restoreGain <= mendGain)
                    throw new InvalidOperationException("회복 치유량은 봉합보다 커야 합니다(실측).");

                allyGo = new GameObject("selfcheck-restore-ally");
                allyGo.transform.position = casterGo.transform.position + new Vector3(2f, 0f, 0f);
                var ally = allyGo.AddComponent<WorldBody>();
                ally.IsAvatar = true;
                ally.IsEnemy = false;
                ally.CharacterId = "restore-ally";
                ally.MaxHp = 60f;
                ally.ResetHp();
                ally.SetHp(15f);
                world.SkillsOf(caster).ForceSet(SkillId.Magery, 0f, SkillLock.Up);
                caster.SetMana(caster.MaxMana);
                bag.Add(SpellCast.Reagent, 2);
                float allyHp0 = ally.Hp;
                var allyHit = world.TryCast(caster, SpellId.Restore, ally);
                if (!allyHit.Applied)
                    throw new InvalidOperationException("아군 회복 실패: " + allyHit.FailReason);
                if (ally.Hp <= allyHp0)
                    throw new InvalidOperationException("아군 회복 후 HP가 올라야 합니다.");

                foeGo = new GameObject("selfcheck-restore-foe");
                foeGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var foe = foeGo.AddComponent<WorldBody>();
                foe.IsEnemy = true;
                foe.IsAvatar = false;
                foe.MaxHp = 40f;
                foe.ResetHp();
                foe.SetHp(10f);
                caster.SetMana(caster.MaxMana);
                bag.Add(SpellCast.Reagent, 2);
                float foeHp0 = foe.Hp;
                var foeHit = world.TryCast(caster, SpellId.Restore, foe);
                if (foeHit.Applied || foeHit.FailReason != "no_target")
                    throw new InvalidOperationException("적 회복은 실패해야 합니다.");
                if (Math.Abs(foe.Hp - foeHp0) > 0.01f)
                    throw new InvalidOperationException("실패한 적 회복은 HP를 바꾸면 안 됩니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (allyGo != null)
                    UnityEngine.Object.DestroyImmediate(allyGo);
                if (foeGo != null)
                    UnityEngine.Object.DestroyImmediate(foeGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }



        static void AssertBlink()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Blink) != "도약")
                throw new InvalidOperationException("SpellId.Blink 한글은 도약이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Blink) != SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("도약 마나는 불씨와 같아야 합니다.");
            if (SpellCast.ReagentCost(SpellId.Blink) != SpellCast.ReagentCost(SpellId.Ember))
                throw new InvalidOperationException("도약 시약은 불씨와 같아야 합니다.");
            if (SpellCast.BlinkDistance < 3f || SpellCast.BlinkDistance > 4f)
                throw new InvalidOperationException("도약 거리는 3~4m여야 합니다.");
            if (SpellCast.Interruptible(SpellId.Blink) || SpellCast.CastTimeOf(SpellId.Blink) != 0f)
                throw new InvalidOperationException("도약은 즉시 시전이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-blink-world");
            GameObject casterGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-blink-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                casterGo.transform.rotation = Quaternion.identity;
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "blink-caster";
                caster.MaxHp = 80f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(20, 20, 40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);

                var unknown = world.TryCast(caster, SpellId.Blink, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 도약은 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Blink);

                caster.Ghost = true;
                var ghostFail = world.TryCast(caster, SpellId.Blink, null);
                if (ghostFail.Applied || ghostFail.FailReason != "ghost")
                    throw new InvalidOperationException("유령 도약은 실패해야 합니다.");
                caster.Ghost = false;

                caster.CombatUntil = Time.time + 30f;
                var combatFail = world.TryCast(caster, SpellId.Blink, null);
                if (combatFail.Applied || combatFail.FailReason != "combat")
                    throw new InvalidOperationException("전투 중 도약은 실패해야 합니다.");
                caster.CombatUntil = 0f;

                caster.SetMana(0f);
                var manaFail = world.TryCast(caster, SpellId.Blink, null);
                if (manaFail.Applied || manaFail.FailReason != "mana")
                    throw new InvalidOperationException("마나 없는 도약은 실패해야 합니다.");
                caster.SetMana(caster.MaxMana);

                bag.Items.Clear();
                var resinFail = world.TryCast(caster, SpellId.Blink, null);
                if (resinFail.Applied || resinFail.FailReason != "reagent")
                    throw new InvalidOperationException("시약 없는 도약은 실패해야 합니다.");
                bag.Add(SpellCast.Reagent, 4);

                // Distinct from Mark/Recall: Blink must not set HasMark / not use Mark coords
                caster.HasMark = false;
                caster.MarkX = 0f;
                caster.MarkZ = 0f;
                Vector3 before = casterGo.transform.position;
                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                world.SkillsOf(caster).ForceSet(SkillId.Magery, 0f, SkillLock.Up);
                var hit = world.TryCast(caster, SpellId.Blink, null);
                if (!hit.Applied)
                    throw new InvalidOperationException("도약 실패: " + hit.FailReason);
                Vector3 after = casterGo.transform.position;
                float moved = Vector3.Distance(new Vector3(before.x, 0f, before.z), new Vector3(after.x, 0f, after.z));
                if (moved < 3f)
                    throw new InvalidOperationException("도약 후 위치가 약 3m 이상 바뀌어야 합니다: " + moved);
                if (Mathf.Abs(moved - SpellCast.BlinkDistance) > 0.2f)
                    throw new InvalidOperationException("도약 거리는 BlinkDistance와 같아야 합니다: " + moved);
                // identity rotation → +Z
                if (Mathf.Abs(after.z - (before.z + SpellCast.BlinkDistance)) > 0.2f)
                    throw new InvalidOperationException("도약은 전방(+Z)으로 이동해야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("도약 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("도약은 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - SpellCast.ReagentCost(SpellId.Blink))
                    throw new InvalidOperationException("도약은 시약을 소모해야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("도약 후 CastingUntil이 있으면 안 됩니다.");
                if (caster.HasMark)
                    throw new InvalidOperationException("도약은 Mark 슬롯을 쓰면 안 됩니다.");
                if (Math.Abs(after.x - TravelGate.PlazaX) < 0.2f && Math.Abs(after.z - TravelGate.PlazaZ) < 0.2f)
                    throw new InvalidOperationException("도약은 문게이트 광장 워프가 아닙니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }



        static void AssertBless()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Bless) != "축복")
                throw new InvalidOperationException("SpellId.Bless 한글은 축복이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Bless) != SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("축복 마나는 불씨와 같아야 합니다.");
            if (SpellCast.ReagentCost(SpellId.Bless) != SpellCast.ReagentCost(SpellId.Ember))
                throw new InvalidOperationException("축복 시약은 불씨와 같아야 합니다.");
            if (SpellCast.BlessSeconds != 8f)
                throw new InvalidOperationException("BlessSeconds는 8이어야 합니다.");
            if (SpellCast.Interruptible(SpellId.Bless) || SpellCast.CastTimeOf(SpellId.Bless) != 0f)
                throw new InvalidOperationException("축복은 즉시 시전이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-bless-world");
            GameObject casterGo = null;
            GameObject tgtGo = null;
            GameObject allyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-bless-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "bless-caster";
                caster.MaxHp = 120f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(40, 20, 40);
                caster.RecalcFromStr(40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);
                bag.Add(ItemCatalog.IronSword, 1);

                var unknown = world.TryCast(caster, SpellId.Bless, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 축복은 실패해야 합니다.");

                world.BookOf(caster).Learn(SpellId.Bless);

                tgtGo = new GameObject("selfcheck-bless-tgt");
                tgtGo.transform.position = casterGo.transform.position + new Vector3(1f, 0f, 0f);
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsAvatar = true;
                tgt.IsEnemy = false;
                tgt.CharacterId = "bless-tgt";
                tgt.MaxHp = 200f;
                tgt.ResetHp();
                world.StatsOf(tgt).ForceSet(40, 20, 20);
                tgt.RecalcFromStr(40);
                tgtGo.AddComponent<InventoryBag>();

                float hp0 = tgt.Hp;
                var baseHit = world.TryAttack(caster, tgt);
                if (!baseHit.Applied || baseHit.Damage <= 0 || tgt.Hp >= hp0)
                    throw new InvalidOperationException("축복 기준 타격 실패: " + baseHit.FailReason);
                int baseDmg = baseHit.Damage;
                tgt.ResetHp();
                caster.BlessUntil = 0f;
                caster.WardUntil = 0f;
                tgt.WardUntil = 0f;

                float mana0 = caster.Mana;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;

                world.SkillsOf(caster).ForceSet(SkillId.Magery, 0f, SkillLock.Up);
                var cast = world.TryCast(caster, SpellId.Bless, null);
                if (!cast.Applied)
                    throw new InvalidOperationException("축복 시전 실패: " + cast.FailReason);
                if (!caster.IsBlessed(Time.time))
                    throw new InvalidOperationException("축복 후 BlessUntil이 활성이어야 합니다.");
                if (caster.BlessUntil < Time.time + SpellCast.BlessSeconds - 0.05f)
                    throw new InvalidOperationException("BlessUntil은 약 8초여야 합니다.");
                if (caster.IsWarded(Time.time))
                    throw new InvalidOperationException("축복은 WardUntil을 켜면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("축복 후 마법이 0.1이어야 합니다.");
                if (caster.Mana >= mana0)
                    throw new InvalidOperationException("축복은 마나를 소모해야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("축복은 시약 1을 써야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("축복 후 CastingUntil이 있으면 안 됩니다.");

                float hp1 = tgt.Hp;
                var nextAt = typeof(OfflineWorld).GetField("nextAttackAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (nextAt != null)
                {
                    var map = nextAt.GetValue(world) as System.Collections.IDictionary;
                    if (map != null)
                        map.Remove(caster.GetInstanceID());
                }
                var blessedHit = world.TryAttack(caster, tgt);
                if (!blessedHit.Applied)
                    throw new InvalidOperationException("축복 중 타격 실패: " + blessedHit.FailReason);
                int expect = (baseDmg * 5) / 4;
                if (blessedHit.Damage != expect)
                    throw new InvalidOperationException("축복 중 피해는 ×1.25여야 합니다: " + blessedHit.Damage + " vs " + expect + " (base " + baseDmg + ")");
                if (Math.Abs((hp1 - tgt.Hp) - blessedHit.Damage) > 0.0001f)
                    throw new InvalidOperationException("축복 중 HP 감소가 Damage와 일치해야 합니다.");
                if (expect <= baseDmg)
                    throw new InvalidOperationException("축복 피해는 기준보다 커야 합니다.");

                // Ally cast: nearby friendly avatar gets BlessUntil (not caster)
                allyGo = new GameObject("selfcheck-bless-ally");
                allyGo.transform.position = casterGo.transform.position + new Vector3(0.5f, 0f, 0f);
                var ally = allyGo.AddComponent<WorldBody>();
                ally.IsAvatar = true;
                ally.IsEnemy = false;
                ally.CharacterId = "bless-ally";
                ally.MaxHp = 80f;
                ally.ResetHp();
                world.StatsOf(ally).ForceSet(20, 20, 20);
                ally.RecalcFromStr(20);
                allyGo.AddComponent<InventoryBag>();
                caster.BlessUntil = 0f;
                caster.SetMana(caster.MaxMana);
                bag.Add(SpellCast.Reagent, 2);
                world.SkillsOf(caster).ForceSet(SkillId.Magery, 0f, SkillLock.Up);
                var allyCast = world.TryCast(caster, SpellId.Bless, ally);
                if (!allyCast.Applied)
                    throw new InvalidOperationException("아군 축복 시전 실패: " + allyCast.FailReason);
                if (!ally.IsBlessed(Time.time))
                    throw new InvalidOperationException("아군 축복 후 대상 BlessUntil이 활성이어야 합니다.");
                if (caster.IsBlessed(Time.time))
                    throw new InvalidOperationException("아군 축복은 시전자를 축복하면 안 됩니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (allyGo != null)
                    UnityEngine.Object.DestroyImmediate(allyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }


    }
}
