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
        /// <summary>
        /// **캐릭터 규칙**(랩 ㉪ 4/N) — 스탯·스킬 상한·가방/무게·은행·시체와 부활.
        /// 담는 것: 사람 한 명에게 붙는 규칙. 안 담는 것: 그 사람이 무엇을 때리는지(전투 partial).
        /// </summary>
        static void RunCharacterRules()
        {
            var snap = new CharacterSnapshot
            {
                AccountId = "selfcheck",
                CharacterId = "selfcheck",
                Name = "검사",
                X = 1.5f,
                Y = 0f,
                Z = -2f,
                Hp = 41f,
                Skills = new[] { new SkillRecord { Id = (int)SkillId.Swordsmanship, Value = 0.4f, Lock = 0 } },
                Inventory = new[] { new ItemRecord { Slot = 0, TemplateId = "iron_ore", Amount = 2 } }
            };
            CharacterStore.Save(snap);
            var loaded = CharacterStore.Load("selfcheck");
            if (loaded == null || Math.Abs(loaded.Skills[0].Value - 0.4f) > 0.0001f || loaded.Inventory.Length != 1)
                throw new InvalidOperationException("persist 저장/로드 실패");
            snap.Inventory = new[]
            {
                new ItemRecord { Slot = 0, TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 37, MakerId = "crafter-a" }
            };
            CharacterStore.Save(snap);
            loaded = CharacterStore.Load("selfcheck");
            if (loaded == null || loaded.Inventory.Length != 1
                || loaded.Inventory[0].Uses != 37 || loaded.Inventory[0].MakerId != "crafter-a")
                throw new InvalidOperationException("persist 내구/Maker Mark 왕복 실패");

            var gatherSkills = new SkillSet();
            SkillGain.TryRaise(gatherSkills, SkillId.Mining, 10f, out _, out float mineAfter);
            if (Math.Abs(mineAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException("채광 0.0→0.1이어야 합니다.");
            SkillGain.TryRaise(gatherSkills, SkillId.Blacksmithing, 15f, out _, out float smithAfter);
            if (Math.Abs(smithAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException("대장장이 0.0→0.1이어야 합니다.");
            SkillGain.TryRaise(gatherSkills, SkillId.Lumberjacking, 10f, out _, out float woodAfter);
            if (Math.Abs(woodAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException("벌목 0.0→0.1이어야 합니다.");
            SkillGain.TryRaise(gatherSkills, SkillId.Carpentry, 12f, out _, out float carpAfter);
            if (Math.Abs(carpAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException("목공 0.0→0.1이어야 합니다.");

            var weak = new StatSet();
            weak.ForceSet(20, 25, 25);
            var strong = new StatSet();
            strong.ForceSet(50, 25, 25);
            var dmgWeak = AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 2f, Skills = new SkillSet(), Stats = weak, TargetAlive = true });
            var dmgStrong = AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 2f, Skills = new SkillSet(), Stats = strong, TargetAlive = true });
            if (dmgStrong.Damage <= dmgWeak.Damage)
                throw new InvalidOperationException("STR가 근접 피해에 반영되어야 합니다.");
            var tac = new SkillSet();
            tac.ForceSet(SkillId.Tactics, 40f, SkillLock.Up);
            var dmgTac = AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 3f, Skills = tac, Stats = weak, TargetAlive = true });
            if (dmgTac.Damage <= dmgWeak.Damage)
                throw new InvalidOperationException("전술이 근접 피해에 반영되어야 합니다.");
            var ana = new SkillSet();
            ana.ForceSet(SkillId.Anatomy, 40f, SkillLock.Up);
            var dmgAna = AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 3f, Skills = ana, Stats = weak, TargetAlive = true });
            if (dmgAna.Damage <= dmgWeak.Damage)
                throw new InvalidOperationException("해부학이 근접 피해에 반영되어야 합니다.");
            if (StatSet.MaxHpOf(30) != 50 || StatSet.MaxHpOf(50) != 70)
                throw new InvalidOperationException("MaxHp=20+STR 이어야 합니다.");
            var gainStats = new StatSet();
            int strBefore = gainStats.Str;
            SkillGain.TryRaise(new SkillSet(), SkillId.Swordsmanship, 20f, out _, out _, gainStats);
            if (gainStats.Str != strBefore + 1)
                throw new InvalidOperationException("검술 상승 시 STR가 올라야 합니다.");
            snap.Str = 44;
            snap.Dex = 22;
            snap.Int = 18;
            CharacterStore.Save(snap);
            loaded = CharacterStore.Load("selfcheck");
            if (loaded == null || loaded.Str != 44 || loaded.Dex != 22 || loaded.Int != 18)
                throw new InvalidOperationException("persist STR/DEX/INT 왕복 실패");

            var cap = new SkillSet();
            cap.ForceSet(SkillId.Archery, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Tactics, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Parrying, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Anatomy, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Healing, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Magery, 100f, SkillLock.Locked);
            cap.ForceSet(SkillId.Mining, 100f, SkillLock.Locked);
            if (Math.Abs(cap.Total - 700f) > 0.01f)
                throw new InvalidOperationException("700 캡 픽스처 실패 total=" + cap.Total);
            if (SkillGain.TryRaise(cap, SkillId.Swordsmanship, 50f, out _, out _))
                throw new InvalidOperationException("↓ 스킬 없이 700 캡을 넘기면 안 됩니다.");
            cap.SetLock(SkillId.Mining, SkillLock.Down);
            if (!SkillGain.TryRaise(cap, SkillId.Swordsmanship, 50f, out _, out float afterCap))
                throw new InvalidOperationException("↓ 채광이 있으면 검술이 올라야 합니다.");
            if (Math.Abs(afterCap - 0.1f) > 0.0001f || Math.Abs(cap.Get(SkillId.Mining) - 99.9f) > 0.0001f)
                throw new InvalidOperationException("700캡에서 ↓ 채광이 0.1 줄어야 합니다.");
            cap.SetLock(SkillId.Swordsmanship, SkillLock.Locked);
            if (SkillGain.TryRaise(cap, SkillId.Swordsmanship, 50f, out _, out _))
                throw new InvalidOperationException("잠긴 스킬은 오르면 안 됩니다.");

            var lockedStr = new StatSet();
            lockedStr.SetLock(StatId.Str, SkillLock.Locked);
            int strWas = lockedStr.Str;
            SkillGain.TryRaise(new SkillSet(), SkillId.Swordsmanship, 20f, out _, out _, lockedStr);
            if (lockedStr.Str != strWas)
                throw new InvalidOperationException("잠긴 STR은 스킬로 오르면 안 됩니다.");

            var tacLock = new SkillSet();
            tacLock.SetLock(SkillId.Tactics, SkillLock.Locked);
            AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 4f, Skills = tacLock, TargetAlive = true });
            if (Math.Abs(tacLock.Get(SkillId.Tactics)) > 0.0001f)
                throw new InvalidOperationException("잠긴 전술은 오르면 안 됩니다.");
            if (Math.Abs(tacLock.Get(SkillId.Swordsmanship) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("전술 잠금이 검술 상승을 막으면 안 됩니다.");
            if (StatSet.PrimaryOf(SkillId.Tactics) != StatId.Str)
                throw new InvalidOperationException("전술 Primary는 STR이어야 합니다.");

            var anaLock = new SkillSet();
            anaLock.SetLock(SkillId.Anatomy, SkillLock.Locked);
            AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 5f, Skills = anaLock, TargetAlive = true });
            if (Math.Abs(anaLock.Get(SkillId.Anatomy)) > 0.0001f)
                throw new InvalidOperationException("잠긴 해부학은 오르면 안 됩니다.");
            if (Math.Abs(anaLock.Get(SkillId.Swordsmanship) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("해부학 잠금이 검술 상승을 막으면 안 됩니다.");
            if (StatSet.PrimaryOf(SkillId.Anatomy) != StatId.Int)
                throw new InvalidOperationException("해부학 Primary는 INT이어야 합니다.");
            var anaStats = new StatSet();
            int intWas = anaStats.Int;
            SkillGain.TryRaise(new SkillSet(), SkillId.Anatomy, 20f, out _, out _, anaStats);
            if (anaStats.Int != intWas + 1)
                throw new InvalidOperationException("해부학 상승 시 INT가 올라야 합니다.");

            snap.Inventory = new[] { new ItemRecord { Slot = 0, TemplateId = "wood", Amount = 3 } };
            snap.Bank = System.Array.Empty<ItemRecord>();
            CharacterStore.Save(snap);
            var bankBody = new GameObject("selfcheck-bank");
            GameObject worldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = bankBody.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = bankBody.AddComponent<InventoryBag>();
                bag.Add("wood", 3);
                var dep = world.DepositAll(body);
                if (!dep.Applied)
                    throw new InvalidOperationException("은행 맡기기 실패: " + dep.FailReason);
                if (bag.Items.Count != 0)
                    throw new InvalidOperationException("맡긴 뒤 가방이 비어야 합니다.");
                var vault = bankBody.GetComponent<BankVault>();
                if (vault == null || vault.Items.Count != 1 || vault.Items[0].Amount != 3)
                    throw new InvalidOperationException("은행에 wood x3이 있어야 합니다.");
                snap = CharacterBinder.Capture("selfcheck", body, new SkillSet(), new StatSet());
                snap.AccountId = "selfcheck";
                snap.CharacterId = "selfcheck";
                CharacterStore.Save(snap);
                loaded = CharacterStore.Load("selfcheck");
                if (loaded == null || loaded.Bank == null || loaded.Bank.Length != 1 || loaded.Bank[0].Amount != 3)
                    throw new InvalidOperationException("persist 은행 왕복 실패");
                CharacterBinder.Apply(body, loaded, new SkillSet(), new StatSet());
                var wd = world.WithdrawAll(body);
                if (!wd.Applied || bag.Items.Count == 0 || vault.Items.Count != 0)
                    throw new InvalidOperationException("은행 찾기 실패");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bankBody);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }

            if (CharacterCreate.Validate("", 30, 25, 25, new[] { SkillId.Swordsmanship, SkillId.Mining, SkillId.Blacksmithing }, new[] { 50f, 30f, 20f }) == null)
                throw new InvalidOperationException("빈 이름은 거절해야 합니다.");
            if (CharacterCreate.Validate("검사", 50, 25, 10, new[] { SkillId.Swordsmanship, SkillId.Mining, SkillId.Blacksmithing }, new[] { 50f, 30f, 20f }) == null)
                throw new InvalidOperationException("스탯 총합 80이 아니면 거절해야 합니다.");
            if (CharacterCreate.Validate("검사", 30, 25, 25, new[] { SkillId.Swordsmanship, SkillId.Mining, SkillId.Blacksmithing }, new[] { 60f, 20f, 20f }) == null)
                throw new InvalidOperationException("시작 스킬 개별 50 초과는 거절해야 합니다.");
            var created = CharacterCreate.Build("create-check", "검사", 1, 30, 25, 25,
                new[] { SkillId.Swordsmanship, SkillId.Mining, SkillId.Blacksmithing },
                new[] { 50f, 30f, 20f });
            if (created.Name != "검사" || created.Str != 30 || created.Appearance != 1 || created.Hp != 50)
                throw new InvalidOperationException("생성 스냅샷 기본값 실패");
            if (created.Skills.Length != 3 || Math.Abs(created.Skills[0].Value - 50f) > 0.01f)
                throw new InvalidOperationException("생성 시작 스킬 실패");
            bool hasSword = false, hasOre = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == "iron_sword") hasSword = true;
                if (created.Inventory[i].TemplateId == "iron_ore" && created.Inventory[i].Amount == 2) hasOre = true;
            }
            if (!hasSword || !hasOre)
                throw new InvalidOperationException("시작 장비 실패");
            var archerCreate = CharacterCreate.Build("archer-check", "궁수", 0, 20, 40, 20,
                new[] { SkillId.Archery, SkillId.Lumberjacking, SkillId.Carpentry },
                new[] { 50f, 30f, 20f });
            bool hasBow = false;
            for (int i = 0; i < archerCreate.Inventory.Length; i++)
                if (archerCreate.Inventory[i].TemplateId == ItemCatalog.WoodenBow)
                    hasBow = true;
            if (!hasBow)
                throw new InvalidOperationException("궁술 시작은 나무활을 줘야 합니다.");
            var parryCreate = CharacterCreate.Build("parry-check", "방패", 0, 20, 40, 20,
                new[] { SkillId.Parrying, SkillId.Swordsmanship, SkillId.Mining },
                new[] { 50f, 30f, 20f });
            bool hasShieldStart = false;
            for (int i = 0; i < parryCreate.Inventory.Length; i++)
                if (parryCreate.Inventory[i].TemplateId == ItemCatalog.WoodenShield)
                    hasShieldStart = true;
            if (!hasShieldStart)
                throw new InvalidOperationException("방패술 시작은 나무방패를 줘야 합니다.");
            CharacterStore.Save(created);
            loaded = CharacterStore.Load("create-check");
            if (loaded == null || loaded.Name != "검사" || loaded.Appearance != 1 || loaded.Skills == null || loaded.Skills.Length != 3)
                throw new InvalidOperationException("생성 persist 왕복 실패");

            var mageCreate = CharacterCreate.Build("mage-check", "마법", 0, 20, 20, 40,
                new[] { SkillId.Magery, SkillId.Meditation, SkillId.EvaluateIntelligence },
                new[] { 50f, 30f, 20f });
            if (mageCreate.Spells == null || mageCreate.Spells.Length != 3)
                throw new InvalidOperationException("마법 시작은 주문 3개를 줘야 합니다.");
            bool hasBoltStart = false;
            for (int si = 0; si < mageCreate.Spells.Length; si++)
                if (mageCreate.Spells[si] == (int)SpellId.Bolt)
                    hasBoltStart = true;
            if (!hasBoltStart)
                throw new InvalidOperationException("마법 시작 주문에 벼락이 있어야 합니다.");
            bool hasResin = false;
            for (int i = 0; i < mageCreate.Inventory.Length; i++)
                if (mageCreate.Inventory[i].TemplateId == SpellCast.Reagent && mageCreate.Inventory[i].Amount >= 8)
                    hasResin = true;
            if (!hasResin)
                throw new InvalidOperationException("마법 시작 시약은 resin x8");

            var book = new Spellbook();
            if (SpellCast.ManaCost(SpellId.Ember) != 6)
                throw new InvalidOperationException("불씨 마나 비용");
            var mageBody = new GameObject("selfcheck-mage");
            GameObject mageWorldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    mageWorldGo = new GameObject("selfcheck-mage-world");
                    world = mageWorldGo.AddComponent<OfflineWorld>();
                }
                var body = mageBody.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.MaxHp = 50f;
                body.ResetHp();
                body.RecalcFromInt(40);
                var bag = mageBody.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);
                world.BookOf(body).Learn(SpellId.Ember);
                world.BookOf(body).Learn(SpellId.Mend);
                var noTgt = world.TryCast(body, SpellId.Ember, null);
                if (noTgt.Applied)
                    throw new InvalidOperationException("대상 없는 불씨는 실패해야 합니다.");
                var dummy = new GameObject("selfcheck-skel");
                var skel = dummy.AddComponent<WorldBody>();
                skel.IsEnemy = true;
                skel.MaxHp = 30f;
                skel.ResetHp();
                dummy.transform.position = mageBody.transform.position;
                var ember = world.TryCast(body, SpellId.Ember, skel);
                if (!ember.Applied || skel.Hp >= 30f)
                    throw new InvalidOperationException("불씨 피해 실패: " + ember.FailReason);
                if (world.SkillsOf(body).Get(SkillId.Magery) < 0.09f)
                    throw new InvalidOperationException("불씨 후 마법이 올라야 합니다.");
                body.SetHp(20f);
                var mend = world.TryCast(body, SpellId.Mend, body);
                if (!mend.Applied || body.Hp <= 20f)
                    throw new InvalidOperationException("봉합 실패: " + mend.FailReason);

                bag.Add("iron_sword", 1);
                var death = world.HandleDeath(body, "mage-check");
                if (!death.Applied || !body.Ghost || bag.Items.Count != 0)
                    throw new InvalidOperationException("사망 시 가방이 시체로 가야 합니다.");
                var corpse = OfflineWorld.FindCorpse("mage-check");
                if (corpse == null || corpse.Items.Count < 1)
                    throw new InvalidOperationException("시체 아이템 없음");
                var healerGo = new GameObject("Healer");
                healerGo.transform.position = body.transform.position;
                var healer = healerGo.AddComponent<HealerStation>();
                var rez = world.TryResurrect(body, healer);
                if (!rez.Applied || body.Ghost)
                    throw new InvalidOperationException("부활 실패: " + rez.FailReason);
                var corpseRends = corpse.GetComponentsInChildren<Renderer>(true);
                for (int ri = 0; ri < corpseRends.Length; ri++)
                {
                    var mat = corpseRends[ri].sharedMaterial;
                    if (mat != null && mat.name.IndexOf("Default-Material", StringComparison.OrdinalIgnoreCase) >= 0)
                        throw new InvalidOperationException("시체는 Default-Material 프리미티브면 안 됩니다.");
                }
                var loot = world.TryLootCorpse(body, corpse);
                if (!loot.Applied)
                    throw new InvalidOperationException("시체 회수 실패: " + loot.FailReason);
                var palGo = new GameObject("selfcheck-pal");
                palGo.transform.position = body.transform.position;
                var pal = palGo.AddComponent<WorldBody>();
                pal.DisplayName = "동료";
                pal.IsEnemy = false;
                pal.MaxHp = 40f;
                pal.ResetHp();
                // **HUD가 고르는 것과 서버가 받는 것이 같은가** — 초대 대상 선정을 씬 이름에서
                // 거리 기반으로 바꿨으니(2026-09-08), 오프라인에서도 옆에 선 몸을 집어야 한다.
                // 이걸 안 재면 「온라인을 고치다 오프라인을 잃는」 회귀를 못 본다(검수 조건 2).
                var picked = OfflineWorld.NearestInvitee(body, PartyResolve.InviteRange);
                if (picked == null)
                    throw new InvalidOperationException("초대 대상 선정 실패 — 사거리 " +
                        PartyResolve.InviteRange + "m 안에 몸이 있는데 HUD가 쓰는 선정 함수가 아무것도 못 골랐습니다.");
                var invited = world.TryPartyInvite(body, pal);
                if (!invited.Applied || body.Party == null || !body.Party.Contains(pal))
                    throw new InvalidOperationException("파티 초대 실패: " + invited.FailReason);
                var said = world.TryPartySay(body, "hi");
                if (!said.Applied || body.Party.Chat.Count < 1)
                    throw new InvalidOperationException("파티 채팅 실패");
                bag.Add("resin", 1);
                world.HandleDeath(body, "mage-check");
                var partyCorpse = OfflineWorld.FindCorpse("mage-check");
                var palLoot = world.TryLootCorpse(pal, partyCorpse);
                if (!palLoot.Applied)
                    throw new InvalidOperationException("파티 룻 실패: " + palLoot.FailReason);
                var strangerGo = new GameObject("selfcheck-stranger");
                strangerGo.transform.position = body.transform.position;
                var stranger = strangerGo.AddComponent<WorldBody>();
                stranger.ResetHp();
                bag.Add("wood", 1);
                var healer2 = healerGo.GetComponent<HealerStation>();
                world.TryResurrect(body, healer2);
                world.HandleDeath(body, "mage-check");
                var locked = OfflineWorld.FindCorpse("mage-check");
                var denied = world.TryLootCorpse(stranger, locked);
                if (denied.Applied)
                    throw new InvalidOperationException("파티 밖은 룻하면 안 됩니다.");
                world.TryLootCorpse(pal, locked);
                world.TryResurrect(body, healer2);
                world.TryPartyLeave(body);
                UnityEngine.Object.DestroyImmediate(palGo);
                UnityEngine.Object.DestroyImmediate(strangerGo);
                bag.Add("wood", 1);
                world.HandleDeath(body, "mage-check");
                var rotting = OfflineWorld.FindCorpse("mage-check");
                if (rotting == null)
                    throw new InvalidOperationException("두 번째 시체 없음");
                rotting.SpawnedAt = -9999f;
                rotting.DecaySeconds = 1f;
                world.TickCorpses(0f);
                if (OfflineWorld.FindCorpse("mage-check") != null)
                    throw new InvalidOperationException("시체가 소멸해야 합니다.");
                UnityEngine.Object.DestroyImmediate(dummy);
                UnityEngine.Object.DestroyImmediate(healerGo);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mageBody);
                if (mageWorldGo != null)
                    UnityEngine.Object.DestroyImmediate(mageWorldGo);
            }

            if (ItemCatalog.CarryCap(30) != 120 || ItemCatalog.WeightOf("iron_ore") != 2f)
                throw new InvalidOperationException("무게 공식 실패");
            var miner = CharacterCreate.Build("tool-check", "광부", 0, 30, 25, 25,
                new[] { SkillId.Mining, SkillId.Lumberjacking, SkillId.Swordsmanship },
                new[] { 40f, 30f, 30f });
            bool pick = false, hat = false;
            for (int i = 0; i < miner.Inventory.Length; i++)
            {
                if (miner.Inventory[i].TemplateId == ItemCatalog.Pickaxe && miner.Inventory[i].Uses == 20) pick = true;
                if (miner.Inventory[i].TemplateId == ItemCatalog.Hatchet && miner.Inventory[i].Uses == 20) hat = true;
            }
            if (!pick || !hat)
                throw new InvalidOperationException("채광/벌목 시작 도구 실패");

            var toolBody = new GameObject("selfcheck-tool");
            GameObject toolWorldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    toolWorldGo = new GameObject("selfcheck-tool-world");
                    world = toolWorldGo.AddComponent<OfflineWorld>();
                }
                var body = toolBody.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = toolBody.AddComponent<InventoryBag>();
                var veinGo = new GameObject("IronVein");
                veinGo.transform.position = toolBody.transform.position;
                var vein = veinGo.AddComponent<ResourceNode>();
                vein.ResourceId = "iron_ore";
                vein.GatherSkill = SkillId.Mining;
                vein.Remaining = 5;
                var noTool = world.TryGather(body, vein);
                if (noTool.Applied)
                    throw new InvalidOperationException("곡괭이 없이 채광되면 안 됩니다.");
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.Pickaxe, Amount = 1, Uses = 1 });
                var g1 = world.TryGather(body, vein);
                if (!g1.Applied)
                    throw new InvalidOperationException("곡괭이 채광 실패: " + g1.FailReason);
                var g2 = world.TryGather(body, vein);
                if (g2.Applied)
                    throw new InvalidOperationException("내구 0 곡괭이로 채광되면 안 됩니다.");
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.Pickaxe, Amount = 1, Uses = 1 });
                var forgeGo = new GameObject("Forge");
                forgeGo.transform.position = toolBody.transform.position;
                var forge = forgeGo.AddComponent<CraftStation>();
                var repaired = world.TryCraft(body, forge);
                if (!repaired.Applied)
                    throw new InvalidOperationException("도구 수리 실패: " + repaired.FailReason);
                if (bag.ToolUses(ItemCatalog.Pickaxe) < 10)
                    throw new InvalidOperationException("수리 후 내구가 올라야 합니다.");

                body.CharacterId = "smith-mark";
                bag.Add("iron_ore", 2);
                var forged = world.TryCraft(body, forge);
                if (!forged.Applied)
                    throw new InvalidOperationException("철검 제작 실패: " + forged.FailReason);
                bool markedSword = false;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId != ItemCatalog.IronSword)
                        continue;
                    if (bag.Items[i].MakerId != "smith-mark")
                        throw new InvalidOperationException("제작품 Maker Mark가 제작자 id여야 합니다.");
                    if (bag.Items[i].Uses != ItemCatalog.MaxUsesOf(ItemCatalog.IronSword))
                        throw new InvalidOperationException("제작품 내구가 최대여야 합니다.");
                    markedSword = true;
                }
                if (!markedSword)
                    throw new InvalidOperationException("철검 제작 결과가 가방에 있어야 합니다.");
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId != ItemCatalog.IronSword)
                        continue;
                    var worn = bag.Items[i];
                    worn.Uses = 12;
                    bag.Items[i] = worn;
                    break;
                }
                bag.Add("iron_ore", 1);
                var swordFix = world.TryCraft(body, forge);
                if (!swordFix.Applied)
                    throw new InvalidOperationException("철검 수리 실패: " + swordFix.FailReason);
                bool restored = false;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId != ItemCatalog.IronSword)
                        continue;
                    if (bag.Items[i].Uses < 22)
                        throw new InvalidOperationException("수리 후 철검 내구가 올라야 합니다.");
                    if (bag.Items[i].MakerId != "smith-mark")
                        throw new InvalidOperationException("수리는 Maker Mark를 지우면 안 됩니다.");
                    restored = true;
                }
                if (!restored)
                    throw new InvalidOperationException("수리 대상 철검이 없습니다.");

                world.StatsOf(body).ForceSet(10, 25, 25);
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 40 });
                var weakSkelGo = new GameObject("selfcheck-str");
                var weakSkel = weakSkelGo.AddComponent<WorldBody>();
                weakSkel.IsEnemy = true;
                weakSkel.MaxHp = 30f;
                weakSkel.ResetHp();
                weakSkelGo.transform.position = toolBody.transform.position;
                var blocked = world.TryAttack(body, weakSkel);
                if (blocked.Applied)
                    throw new InvalidOperationException("STR 부족인데 철검 공격이 들어가면 안 됩니다.");
                world.StatsOf(body).ForceSet(30, 25, 25);
                var okAtk = world.TryAttack(body, weakSkel);
                if (!okAtk.Applied)
                    throw new InvalidOperationException("STR 충족 공격 실패: " + okAtk.FailReason);

                bag.Add("iron_ore", 80);
                if (!bag.Overweight(30))
                    throw new InvalidOperationException("과적 판정 실패");
                UnityEngine.Object.DestroyImmediate(veinGo);
                UnityEngine.Object.DestroyImmediate(forgeGo);
                UnityEngine.Object.DestroyImmediate(weakSkelGo);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(toolBody);
                if (toolWorldGo != null)
                    UnityEngine.Object.DestroyImmediate(toolWorldGo);
            }
        }
    }
}
