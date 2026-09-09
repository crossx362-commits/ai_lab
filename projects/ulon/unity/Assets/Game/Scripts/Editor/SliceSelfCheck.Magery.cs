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
        // **여기 남은 것은 주문 자체의 규칙**(랩 ㉱) — 시전 방해·순간이동·제작 의뢰.
        // 「무엇을 거느냐」가 아니라 「거는 행위가 어떻게 도느냐」다. 주문별 검사는 갈린 두 파일에 있다.
        static void AssertCastInterrupt()
        {
            AssertVillageIntact();

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

        static void AssertCraftOrder()
        {
            AssertVillageIntact();

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

        static void AssertBlink()
        {
            AssertVillageIntact();

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





    }
}
