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
        // **새기고 바르는 것**(랩 ㉫) — 각인(스크롤)·독. 재료를 물건에 얹어 성질을 바꾸는 기술만 담는다.
        // 안 담는 것: 몸을 감추는 기술(`Stealth`), 짐승(`AnimalCare`), 남의 것에 손대는 기술(`Roguery`).
        static void AssertInscription()
        {
            AssertDungeon3Leftover();
            if (SkillId.Inscription == SkillId.Magery)
                throw new InvalidOperationException("각인 SkillId는 마법과 달라야 합니다.");
            if (SkillId.Inscription == SkillId.Alchemy)
                throw new InvalidOperationException("각인 SkillId는 연금술과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Inscription) != StatId.Int)
                throw new InvalidOperationException("각인 Primary는 INT이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Inscription) != "각인" || SkillTitles.JobOf(SkillId.Inscription) != "각인사")
                throw new InvalidOperationException("각인 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Magery) != "마법" || SkillNames.KoreanOf(SkillId.Alchemy) != "연금술")
                throw new InvalidOperationException("마법/연금술 스킬명을 바꾸면 안 됩니다.");
            if (ItemCatalog.ScrollEmber != "scroll_ember")
                throw new InvalidOperationException("주문서 템플릿은 scroll_ember여야 합니다.");
            if (ItemCatalog.SellPrice(ItemCatalog.ScrollEmber) <= 0 || ItemCatalog.WeightOf(ItemCatalog.ScrollEmber) <= 0f)
                throw new InvalidOperationException("scroll_ember 무게/가격이 없습니다.");
            if (ItemCatalog.SellPrice(ItemCatalog.Blank) <= 0 || ItemCatalog.WeightOf(ItemCatalog.Blank) <= 0f)
                throw new InvalidOperationException("blank 무게/가격이 없습니다.");

            var unlearned = InscriptionResolve.Resolve(new InscriptionRequest
            {
                KnowsEmber = false,
                HasCloth = true,
                Skills = new SkillSet()
            });
            if (unlearned.Applied || unlearned.FailReason != "unlearned")
                throw new InvalidOperationException("불씨를 모르면 각인되면 안 됩니다.");

            var noMatSkills = new SkillSet();
            var noMat = InscriptionResolve.Resolve(new InscriptionRequest
            {
                KnowsEmber = true,
                HasCloth = false,
                HasBlank = false,
                Skills = noMatSkills
            });
            if (noMat.Applied)
                throw new InvalidOperationException("천/blank 없이 각인되면 안 됩니다.");
            if (Math.Abs(noMatSkills.Get(SkillId.Inscription)) > 0.0001f)
                throw new InvalidOperationException("실패한 각인은 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int intWas = stats.Int;
            int dexWas = stats.Dex;
            var ok = InscriptionResolve.Resolve(new InscriptionRequest
            {
                KnowsEmber = true,
                HasCloth = true,
                Skills = skills,
                Stats = stats,
                Difficulty = InscriptionResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("천 각인은 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Inscription) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("각인 0.0→0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Magery)) > 0.0001f)
                throw new InvalidOperationException("각인은 마법을 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Alchemy)) > 0.0001f)
                throw new InvalidOperationException("각인은 연금술을 올리면 안 됩니다.");
            if (stats.Int != intWas + 1)
                throw new InvalidOperationException("각인 상승 시 INT가 올라야 합니다.");
            if (stats.Dex != dexWas)
                throw new InvalidOperationException("각인은 DEX를 올리면 안 됩니다.");

            var blankOk = InscriptionResolve.Resolve(new InscriptionRequest
            {
                KnowsEmber = true,
                HasBlank = true,
                Skills = new SkillSet()
            });
            if (!blankOk.Applied)
                throw new InvalidOperationException("blank 각인도 성공해야 합니다.");

            var mag = new SkillSet();
            SkillGain.TryRaise(mag, SkillId.Magery, 20f, out _, out _);
            if (Math.Abs(mag.Get(SkillId.Inscription)) > 0.0001f)
                throw new InvalidOperationException("마법은 각인을 올리면 안 됩니다.");
            var alch = new SkillSet();
            SkillGain.TryRaise(alch, SkillId.Alchemy, 10f, out _, out _);
            if (Math.Abs(alch.Get(SkillId.Inscription)) > 0.0001f)
                throw new InvalidOperationException("연금술은 각인을 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Inscription, SkillLock.Locked);
            var lockedOk = InscriptionResolve.Resolve(new InscriptionRequest
            {
                KnowsEmber = true,
                HasCloth = true,
                Skills = locked
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 각인도 주문서는 만들어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Inscription)) > 0.0001f)
                throw new InvalidOperationException("잠긴 각인은 오르면 안 됩니다.");

            var noScroll = ScrollUseResolve.Resolve(new ScrollUseRequest { HasScroll = false, HasTarget = true, TargetEnemy = true });
            if (noScroll.Applied || noScroll.FailReason != "no_scroll")
                throw new InvalidOperationException("주문서 없이 쓰면 안 됩니다.");
            var noTgt = ScrollUseResolve.Resolve(new ScrollUseRequest { HasScroll = true, HasTarget = false });
            if (noTgt.Applied)
                throw new InvalidOperationException("대상 없는 주문서는 실패해야 합니다.");

            var created = CharacterCreate.Build("insc-check", "각인", 0, 20, 20, 40,
                new[] { SkillId.Inscription, SkillId.Magery, SkillId.Alchemy },
                new[] { 50f, 30f, 20f });
            bool hasClothStart = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Cloth && created.Inventory[i].Amount >= 1)
                    hasClothStart = true;
            }
            if (!hasClothStart)
                throw new InvalidOperationException("각인 시작은 천을 줘야 합니다.");
            bool hasEmber = false;
            if (created.Spells != null)
            {
                for (int i = 0; i < created.Spells.Length; i++)
                    if (created.Spells[i] == (int)SpellId.Ember)
                        hasEmber = true;
            }
            if (!hasEmber)
                throw new InvalidOperationException("마법 시작은 불씨를 알아야 합니다(각인과 별개).");

            var go = new GameObject("selfcheck-insc");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-insc-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromInt(25);
                var bag = go.AddComponent<InventoryBag>();

                var missing = world.TryInscribe(body);
                if (missing.Applied)
                    throw new InvalidOperationException("재료/주문 없이 서버 각인되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Inscription)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 각인은 스킬을 올리면 안 됩니다.");

                bag.Add(ItemCatalog.Cloth, 1);
                var stillUnknown = world.TryInscribe(body);
                if (stillUnknown.Applied || stillUnknown.FailReason != "unlearned")
                    throw new InvalidOperationException("불씨를 모르면 서버 각인되면 안 됩니다.");
                world.BookOf(body).Learn(SpellId.Ember);
                bag.Add(ItemCatalog.Blank, 1);
                var blankHit = world.TryInscribe(body);
                if (!blankHit.Applied)
                    throw new InvalidOperationException("서버 blank 각인 실패: " + blankHit.FailReason);
                int scrolls = 0, blanks = 0, clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.ScrollEmber)
                        scrolls += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.Blank)
                        blanks += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                }
                if (scrolls != 1 || blanks != 0 || clothLeft != 1)
                    throw new InvalidOperationException("blank를 우선 소모하고 scroll_ember 1을 만들어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Inscription) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 각인 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("서버 각인은 마법을 올리면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Alchemy)) > 0.0001f)
                    throw new InvalidOperationException("서버 각인은 연금술을 올리면 안 됩니다.");
                if (world.LastInscribeMessage != ItemCatalog.ScrollEmber)
                    throw new InvalidOperationException("각인 메시지가 있어야 합니다: " + world.LastInscribeMessage);

                var clothHit = world.TryInscribe(body);
                if (!clothHit.Applied)
                    throw new InvalidOperationException("서버 천 각인 실패: " + clothHit.FailReason);
                scrolls = 0;
                clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.ScrollEmber)
                        scrolls += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                }
                if (scrolls != 2 || clothLeft != 0)
                    throw new InvalidOperationException("천 1 → scroll_ember이어야 합니다.");

                tgtGo = new GameObject("selfcheck-insc-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MaxHp = 40f;
                tgt.ResetHp();
                float hpWas = tgt.Hp;
                int resinWas = 0;
                bag.Add(SpellCast.Reagent, 2);
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == SpellCast.Reagent)
                        resinWas += bag.Items[i].Amount;
                float manaWas = body.Mana;
                float magWas = world.SkillsOf(body).Get(SkillId.Magery);
                var used = world.TryUseScroll(body, tgt);
                if (!used.Applied)
                    throw new InvalidOperationException("주문서 사용 실패: " + used.FailReason);
                if (tgt.Hp >= hpWas)
                    throw new InvalidOperationException("주문서는 불씨 피해를 줘야 합니다.");
                int scrollsLeft = 0, resinLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.ScrollEmber)
                        scrollsLeft += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == SpellCast.Reagent)
                        resinLeft += bag.Items[i].Amount;
                }
                if (scrollsLeft != 1)
                    throw new InvalidOperationException("사용한 주문서는 소모되어야 합니다.");
                if (resinLeft != resinWas)
                    throw new InvalidOperationException("주문서는 시약을 소모하면 안 됩니다(마법과 별개).");
                if (Math.Abs(body.Mana - manaWas) > 0.01f)
                    throw new InvalidOperationException("주문서는 마나를 소모하면 안 됩니다(마법과 별개).");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Magery) - magWas) > 0.0001f)
                    throw new InvalidOperationException("주문서 사용은 마법을 올리면 안 됩니다.");

                var used2 = world.TryUseScroll(body, tgt);
                if (!used2.Applied)
                    throw new InvalidOperationException("두 번째 주문서 사용 실패: " + used2.FailReason);
                scrollsLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.ScrollEmber)
                        scrollsLeft += bag.Items[i].Amount;
                if (scrollsLeft != 0)
                    throw new InvalidOperationException("두 번째 사용 후 주문서는 없어야 합니다.");
                var used3 = world.TryUseScroll(body, tgt);
                if (used3.Applied)
                    throw new InvalidOperationException("소모된 주문서를 다시 쓰면 안 됩니다.");

                bag.Add(SpellCast.Reagent, 1);
                body.SetMana(body.MaxMana);
                var ember = world.TryCast(body, SpellId.Ember, tgt);
                if (!ember.Applied)
                    throw new InvalidOperationException("마법 불씨는 각인과 별개로 유지되어야 합니다: " + ember.FailReason);
                if (world.SkillsOf(body).Get(SkillId.Magery) < 0.09f)
                    throw new InvalidOperationException("마법 불씨는 마법을 올려야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertPoisoning()
        {
            AssertDungeon3Leftover();
            if (SkillId.Poisoning == SkillId.Alchemy)
                throw new InvalidOperationException("독 SkillId는 연금술과 달라야 합니다.");
            if (SkillId.Poisoning == SkillId.Veterinary)
                throw new InvalidOperationException("독 SkillId는 수의학과 달라야 합니다.");
            if (SkillId.Poisoning == SkillId.Magery)
                throw new InvalidOperationException("독 SkillId는 마법과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Poisoning) != StatId.Dex)
                throw new InvalidOperationException("독 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Poisoning) != "독" || SkillTitles.JobOf(SkillId.Poisoning) != "독살자")
                throw new InvalidOperationException("독 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Alchemy) != "연금술" || SkillTitles.JobOf(SkillId.Alchemy) != "연금술사")
                throw new InvalidOperationException("연금술 스킬명/직업명을 바꾸면 안 됩니다.");
            if (SkillNames.KoreanOf(SkillId.Veterinary) != "수의학" || SkillTitles.JobOf(SkillId.Veterinary) != "수의사")
                throw new InvalidOperationException("수의학 스킬명/직업명을 바꾸면 안 됩니다.");
            if (ItemCatalog.PoisonVial != "poison_vial")
                throw new InvalidOperationException("독병 템플릿은 poison_vial여야 합니다.");
            if (ItemCatalog.SellPrice(ItemCatalog.PoisonVial) <= 0 || ItemCatalog.WeightOf(ItemCatalog.PoisonVial) <= 0f)
                throw new InvalidOperationException("poison_vial 무게/가격이 없습니다.");
            var rec = CraftRecipes.Find("poison_vial");
            if (rec == null || rec.Ingredient != ItemCatalog.Cloth || rec.Output != ItemCatalog.PoisonVial
                || rec.Skill != SkillId.Poisoning || rec.Count != 1)
                throw new InvalidOperationException("천 1 → 독병 레시피가 있어야 합니다.");
            if (!ItemCatalog.IsMeleeWeapon(ItemCatalog.IronSword) || ItemCatalog.IsMeleeWeapon(ItemCatalog.WoodenBow))
                throw new InvalidOperationException("근접은 검/둔기/창, 활은 원거리여야 합니다.");

            var noMeleeSkills = new SkillSet();
            var noMelee = PoisoningResolve.Resolve(new PoisonWeaponRequest
            {
                HasMelee = false,
                HasPotion = true,
                Skills = noMeleeSkills
            });
            if (noMelee.Applied || noMelee.FailReason != "no_melee")
                throw new InvalidOperationException("근접 무기 없이 도포되면 안 됩니다.");
            if (Math.Abs(noMeleeSkills.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("실패한 도포는 스킬을 올리면 안 됩니다.");

            var noPoisonSkills = new SkillSet();
            var noPoison = PoisoningResolve.Resolve(new PoisonWeaponRequest
            {
                HasMelee = true,
                HasPotion = false,
                HasVial = false,
                Skills = noPoisonSkills
            });
            if (noPoison.Applied || noPoison.FailReason != "no_poison")
                throw new InvalidOperationException("물약/독병 없이 도포되면 안 됩니다.");
            if (Math.Abs(noPoisonSkills.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("재료 없는 도포는 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            int intWas = stats.Int;
            int strWas = stats.Str;
            var ok = PoisoningResolve.Resolve(new PoisonWeaponRequest
            {
                HasMelee = true,
                HasPotion = true,
                Skills = skills,
                Stats = stats,
                Difficulty = PoisoningResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("연금 물약 도포는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Poisoning) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("독 0.0→0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Alchemy)) > 0.0001f)
                throw new InvalidOperationException("독은 연금술을 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("독은 수의학을 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Magery)) > 0.0001f)
                throw new InvalidOperationException("독은 마법을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("독 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != intWas)
                throw new InvalidOperationException("독은 INT를 올리면 안 됩니다.");
            if (stats.Str != strWas)
                throw new InvalidOperationException("독은 STR을 올리면 안 됩니다.");

            var vialOk = PoisoningResolve.Resolve(new PoisonWeaponRequest
            {
                HasMelee = true,
                HasVial = true,
                Skills = new SkillSet()
            });
            if (!vialOk.Applied)
                throw new InvalidOperationException("천 독병 도포도 성공해야 합니다.");

            var mag = new SkillSet();
            SkillGain.TryRaise(mag, SkillId.Magery, 20f, out _, out _);
            if (Math.Abs(mag.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("마법은 독을 올리면 안 됩니다.");
            var alch = new SkillSet();
            SkillGain.TryRaise(alch, SkillId.Alchemy, 10f, out _, out _);
            if (Math.Abs(alch.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("연금술은 독을 올리면 안 됩니다.");
            var vet = new SkillSet();
            SkillGain.TryRaise(vet, SkillId.Veterinary, 10f, out _, out _);
            if (Math.Abs(vet.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("수의학은 독을 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Poisoning, SkillLock.Locked);
            var lockedOk = PoisoningResolve.Resolve(new PoisonWeaponRequest
            {
                HasMelee = true,
                HasPotion = true,
                Skills = locked
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 독도 도포는 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("잠긴 독은 오르면 안 됩니다.");

            var created = CharacterCreate.Build("poison-check", "독살", 0, 20, 40, 20,
                new[] { SkillId.Poisoning, SkillId.Alchemy, SkillId.Veterinary },
                new[] { 50f, 30f, 20f });
            bool hasClothStart = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Cloth && created.Inventory[i].Amount >= 1)
                    hasClothStart = true;
            }
            if (!hasClothStart)
                throw new InvalidOperationException("독 시작은 천을 줘야 합니다.");

            var go = new GameObject("selfcheck-poison");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-poison-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                var bag = go.AddComponent<InventoryBag>();

                var missing = world.TryPoisonWeapon(body);
                if (missing.Applied)
                    throw new InvalidOperationException("무기/재료 없이 서버 도포되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Poisoning)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 도포는 스킬을 올리면 안 됩니다.");

                bag.Add(ItemCatalog.HealthPotion, 1);
                var stillNoMelee = world.TryPoisonWeapon(body);
                if (stillNoMelee.Applied || stillNoMelee.FailReason != "no_melee")
                    throw new InvalidOperationException("근접 무기 없이 서버 도포되면 안 됩니다.");

                bag.Add(ItemCatalog.WoodenBow, 1);
                var bowOnly = world.TryPoisonWeapon(body);
                if (bowOnly.Applied || bowOnly.FailReason != "no_melee")
                    throw new InvalidOperationException("활만 있으면 도포되면 안 됩니다.");
                bag.TakeOne(ItemCatalog.WoodenBow);

                bag.Add(ItemCatalog.IronSword, 1);
                var potionHit = world.TryPoisonWeapon(body);
                if (!potionHit.Applied)
                    throw new InvalidOperationException("서버 물약 도포 실패: " + potionHit.FailReason);
                int pots = 0, vials = 0, clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.HealthPotion)
                        pots += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.PoisonVial)
                        vials += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                }
                if (pots != 0)
                    throw new InvalidOperationException("도포한 연금 물약은 소모되어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Poisoning) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 도포 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Alchemy)) > 0.0001f)
                    throw new InvalidOperationException("서버 도포는 연금술을 올리면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Veterinary)) > 0.0001f)
                    throw new InvalidOperationException("서버 도포는 수의학을 올리면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("서버 도포는 마법을 올리면 안 됩니다.");
                if (world.LastPoisonMessage != "poison")
                    throw new InvalidOperationException("도포 메시지가 있어야 합니다: " + world.LastPoisonMessage);

                tgtGo = new GameObject("selfcheck-poison-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MaxHp = 80f;
                tgt.ResetHp();
                float hpWas = tgt.Hp;
                float manaWas = body.Mana;
                int resinWas = 0;
                bag.Add(SpellCast.Reagent, 2);
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == SpellCast.Reagent)
                        resinWas += bag.Items[i].Amount;
                float magWas = world.SkillsOf(body).Get(SkillId.Magery);
                var hit = world.TryAttack(body, tgt);
                if (!hit.Applied)
                    throw new InvalidOperationException("독 무기 공격 실패: " + hit.FailReason);
                float afterHit = tgt.Hp;
                if (afterHit >= hpWas - hit.Damage + 0.01f)
                    throw new InvalidOperationException("다음 TryAttack은 독 HP 틱을 줘야 합니다.");
                if (Math.Abs(afterHit - (hpWas - hit.Damage - PoisoningResolve.TickDamage)) > 0.01f)
                    throw new InvalidOperationException("첫 독 틱은 공격과 함께 들어가야 합니다.");
                world.TickPoison(UnityEngine.Time.time + PoisoningResolve.TickInterval);
                world.TickPoison(UnityEngine.Time.time + PoisoningResolve.TickInterval * 2);
                float afterTicks = tgt.Hp;
                float expect = hpWas - hit.Damage - PoisoningResolve.TickDamage * PoisoningResolve.TickCount;
                if (Math.Abs(afterTicks - expect) > 0.01f)
                    throw new InvalidOperationException("짧은 HP 틱이 모두 들어가야 합니다.");
                int resinLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == SpellCast.Reagent)
                        resinLeft += bag.Items[i].Amount;
                if (resinLeft != resinWas)
                    throw new InvalidOperationException("독은 시약을 소모하면 안 됩니다(마법과 별개).");
                if (Math.Abs(body.Mana - manaWas) > 0.01f)
                    throw new InvalidOperationException("독은 마나를 소모하면 안 됩니다(마법과 별개).");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Magery) - magWas) > 0.0001f)
                    throw new InvalidOperationException("독 공격은 마법을 올리면 안 됩니다.");
                if (tgt.PoisonTicks != 0)
                    throw new InvalidOperationException("짧은 틱이 끝나면 독 잔여가 없어야 합니다.");

                bag.Add(ItemCatalog.Cloth, 1);
                var clothHit = world.TryPoisonWeapon(body);
                if (!clothHit.Applied)
                    throw new InvalidOperationException("서버 천 독병 도포 실패: " + clothHit.FailReason);
                clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                if (clothLeft != 0)
                    throw new InvalidOperationException("천 독병은 소모되어야 합니다.");

                bag.Add(ItemCatalog.PoisonVial, 1);
                var vialHit = world.TryPoisonWeapon(body);
                if (!vialHit.Applied)
                    throw new InvalidOperationException("서버 poison_vial 도포 실패: " + vialHit.FailReason);
                vials = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.PoisonVial)
                        vials += bag.Items[i].Amount;
                if (vials != 0)
                    throw new InvalidOperationException("도포한 독병은 소모되어야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }
    }
}
