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
        // **지키고 고치는 주문**(랩 ㉱) — 정화·보호막·회복·축복.
        // 담는 것: 나와 동료에게 거는 주문. 안 담는 것: 상대에게 거는 것(`MageryOffense`),
        // 시전 자체의 규칙·이동·의뢰(본체).
        static void AssertCleanse()
        {
            AssertVillageIntact();

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
            AssertVillageIntact();

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
        static void AssertRestore()
        {
            AssertVillageIntact();

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
        static void AssertBless()
        {
            AssertVillageIntact();

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
