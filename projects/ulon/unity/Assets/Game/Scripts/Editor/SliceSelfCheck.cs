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
    public static class SliceSelfCheck
    {
        [MenuItem("Ulon/Run Slice Self-Check")]
        public static void Run()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != scenePath)
                scene = EditorSceneManager.OpenScene(scenePath);
            VisualSliceBuilder.EnsureHuntMobs();
            VisualSliceBuilder.EnsureFishSpot();
            NetworkSliceSetup.WireMob("Skeleton");
            NetworkSliceSetup.WireMob("Bandit");

            var bandit = GameObject.Find("Bandit");
            var banditBody = bandit != null ? bandit.GetComponent<WorldBody>() : null;
            if (banditBody == null || banditBody.MobId != "bandit" || !banditBody.IsEnemy)
                throw new InvalidOperationException("두 번째 몬스터 도적이 사냥 구역에 있어야 합니다.");
            if (banditBody.DisplayName != "도적" || Math.Abs(banditBody.MaxHp - 45f) > 0.0001f)
                throw new InvalidOperationException("도적 카탈로그는 이름=도적, HP=45여야 합니다.");
            if (bandit.GetComponent<NetworkObject>() == null || bandit.GetComponent<NetMob>() == null)
                throw new InvalidOperationException("도적 전투 상태는 서버 NetworkObject/NetMob이 권한을 가져야 합니다.");
            if (MobCatalog.KindCount != 2 || !MobCatalog.TryGet(MobCatalog.Bandit, out MobDefinition banditDefinition)
                || banditDefinition.DisplayName != "도적" || Math.Abs(banditDefinition.MaxHp - 45f) > 0.0001f
                || Math.Abs(banditDefinition.Height - 1.75f) > 0.0001f)
                throw new InvalidOperationException("몬스터 카탈로그는 스켈레톤+도적 2종이어야 합니다.");

            var serverBanditGo = new GameObject("selfcheck-server-bandit");
            try
            {
                var serverBanditBody = serverBanditGo.AddComponent<WorldBody>();
                serverBanditBody.MobId = "bandit";
                var serverBandit = serverBanditGo.AddComponent<NetMob>();
                serverBandit.OnStartServer();
                if (serverBanditBody.DisplayName != "도적" || Math.Abs(serverBanditBody.MaxHp - 45f) > 0.0001f || Math.Abs(serverBanditBody.Hp - 45f) > 0.0001f)
                    throw new InvalidOperationException("서버 시작 시 도적 카탈로그와 HP를 권위 있게 적용해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(serverBanditGo);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var skills = new SkillSet();
            if (Math.Abs(skills.Get(SkillId.Swordsmanship)) > 0.0001f)
                throw new InvalidOperationException("검술은 0.0에서 시작해야 합니다.");

            var miss = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 9f,
                Skills = skills,
                TargetAlive = true
            });
            if (miss.Applied)
                throw new InvalidOperationException("사거리 밖 공격이 들어가면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Swordsmanship)) > 0.0001f || Math.Abs(skills.Get(SkillId.Tactics)) > 0.0001f || Math.Abs(skills.Get(SkillId.Anatomy)) > 0.0001f)
                throw new InvalidOperationException("실패한 공격은 스킬을 올리면 안 됩니다.");

            var hit = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1.2f,
                Now = 1f,
                NextAttackAt = 0f,
                Skills = skills,
                TargetAlive = true
            });
            if (!hit.Applied || !hit.Hit)
                throw new InvalidOperationException("사거리 안 공격이 들어가야 합니다.");
            if (Math.Abs(hit.SkillBefore) > 0.0001f || Math.Abs(hit.SkillAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException($"검술 0.0→0.1이어야 합니다. 실제 {hit.SkillBefore}→{hit.SkillAfter}");
            if (Math.Abs(skills.Get(SkillId.Swordsmanship) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("SkillSet에 0.1이 저장되어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("근접 공격 후 전술 0.0→0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("근접 공격 후 해부학 0.0→0.1이어야 합니다.");

            var a = new SkillSet();
            var b = new SkillSet();
            AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 1f, Skills = a, TargetAlive = true });
            AttackResolve.Resolve(new AttackRequest { Distance = 1f, Now = 1f, Skills = b, TargetAlive = true });
            if (Math.Abs(a.Get(SkillId.Swordsmanship) - 0.1f) > 0.0001f || Math.Abs(b.Get(SkillId.Swordsmanship) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("공격자별 스킬이 독립이어야 합니다.");

            var archSkills = new SkillSet();
            var archMiss = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 9f,
                Range = ItemCatalog.ArcheryRange,
                WeaponSkill = SkillId.Archery,
                Skills = archSkills,
                TargetAlive = true
            });
            if (archMiss.Applied)
                throw new InvalidOperationException("활 사거리 밖 공격이 들어가면 안 됩니다.");
            if (Math.Abs(archSkills.Get(SkillId.Archery)) > 0.0001f || Math.Abs(archSkills.Get(SkillId.Swordsmanship)) > 0.0001f || Math.Abs(archSkills.Get(SkillId.Tactics)) > 0.0001f || Math.Abs(archSkills.Get(SkillId.Anatomy)) > 0.0001f)
                throw new InvalidOperationException("실패한 원거리 공격은 스킬을 올리면 안 됩니다.");

            var archHit = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 7f,
                Range = ItemCatalog.ArcheryRange,
                Now = 1f,
                NextAttackAt = 0f,
                WeaponSkill = SkillId.Archery,
                Skills = archSkills,
                TargetAlive = true
            });
            if (!archHit.Applied || !archHit.Hit)
                throw new InvalidOperationException("활 사거리 안 공격이 들어가야 합니다.");
            if (Math.Abs(archSkills.Get(SkillId.Archery) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("궁술 0.0→0.1이어야 합니다.");
            if (Math.Abs(archSkills.Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("활 공격 후 전술 0.0→0.1이어야 합니다.");
            if (Math.Abs(archSkills.Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("활 공격 후 해부학 0.0→0.1이어야 합니다.");
            if (Math.Abs(archSkills.Get(SkillId.Swordsmanship)) > 0.0001f)
                throw new InvalidOperationException("활 공격은 검술을 올리면 안 됩니다.");

            var meleeFar = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 7f,
                Skills = new SkillSet(),
                TargetAlive = true
            });
            if (meleeFar.Applied)
                throw new InvalidOperationException("근접은 활 사거리에서 들어가면 안 됩니다.");

            var dexLow = new StatSet();
            dexLow.ForceSet(30, 10, 25);
            var dexHigh = new StatSet();
            dexHigh.ForceSet(30, 50, 25);
            var archLow = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.ArcheryRange,
                Now = 2f,
                WeaponSkill = SkillId.Archery,
                Skills = new SkillSet(),
                Stats = dexLow,
                TargetAlive = true
            });
            var archHigh = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1f,
                Range = ItemCatalog.ArcheryRange,
                Now = 2f,
                WeaponSkill = SkillId.Archery,
                Skills = new SkillSet(),
                Stats = dexHigh,
                TargetAlive = true
            });
            if (archHigh.Damage <= archLow.Damage)
                throw new InvalidOperationException("궁술 피해는 DEX 보정이 있어야 합니다.");

            if (!CharacterStore.EnsureRunning())
                throw new InvalidOperationException("persist 서버를 시작하지 못했습니다.");
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
            if (mageCreate.Spells == null || mageCreate.Spells.Length != 2)
                throw new InvalidOperationException("마법 시작은 주문 2개를 줘야 합니다.");
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
                var invited = world.TryPartyInvite(body, pal);
                if (!invited.Applied || world.ActiveParty == null || !world.ActiveParty.Contains(pal))
                    throw new InvalidOperationException("파티 초대 실패: " + invited.FailReason);
                var said = world.TryPartySay(body, "hi");
                if (!said.Applied || world.ActiveParty.Chat.Count < 1)
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

            var respawnGo = new GameObject("selfcheck-respawn");
            try
            {
                var node = respawnGo.AddComponent<ResourceNode>();
                node.ResourceId = "iron_ore";
                node.GatherSkill = SkillId.Mining;
                node.Remaining = 1;
                node.Capacity = 4;
                node.RespawnSeconds = 5f;
                node.EnsureCapacity();
                node.Remaining = 0;
                node.ReadyAt = 99999f;
                node.Tick(0f);
                if (node.Remaining != 0)
                    throw new InvalidOperationException("리스폰 전에 광맥이 차면 안 됩니다.");
                node.Tick(100000f);
                if (node.Remaining != 4)
                    throw new InvalidOperationException("리스폰 후 Capacity만큼 차야 합니다.");

                var world = OfflineWorld.Instance;
                if (world == null)
                    throw new InvalidOperationException("OfflineWorld 없음");
                var bushGo = new GameObject("ResinBush");
                bushGo.transform.position = Vector3.zero;
                var bush = bushGo.AddComponent<ResourceNode>();
                bush.ResourceId = SpellCast.Reagent;
                bush.GatherSkill = SkillId.Magery;
                bush.Remaining = 2;
                bush.Capacity = 2;
                bush.Difficulty = 8f;
                var herbBody = new GameObject("selfcheck-herb");
                herbBody.transform.position = Vector3.zero;
                var hb = herbBody.AddComponent<WorldBody>();
                hb.IsAvatar = true;
                herbBody.AddComponent<InventoryBag>();
                var herb = world.TryGather(hb, bush);
                if (!herb.Applied)
                    throw new InvalidOperationException("시약 채집 실패: " + herb.FailReason);
                var herbBag = herbBody.GetComponent<InventoryBag>();
                int resin = 0;
                for (int i = 0; i < herbBag.Items.Count; i++)
                    if (herbBag.Items[i].TemplateId == SpellCast.Reagent)
                        resin += herbBag.Items[i].Amount;
                if (resin < 1)
                    throw new InvalidOperationException("시약이 가방에 있어야 합니다.");
                if (world.SkillsOf(hb).Get(SkillId.Magery) < 0.09f)
                    throw new InvalidOperationException("시약 채집 후 마법이 올라야 합니다.");
                UnityEngine.Object.DestroyImmediate(bushGo);
                UnityEngine.Object.DestroyImmediate(herbBody);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(respawnGo);
            }

            var shopBody = new GameObject("selfcheck-shop");
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                    throw new InvalidOperationException("OfflineWorld 없음");
                var body = shopBody.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.Gold = 40;
                shopBody.AddComponent<InventoryBag>();
                var vendorGo = new GameObject("Vendor");
                vendorGo.transform.position = shopBody.transform.position;
                var vendor = vendorGo.AddComponent<VendorStation>();
                var open = world.TryVendor(body, vendor);
                if (!open.Applied)
                    throw new InvalidOperationException("상점 열기 실패");
                var buy = world.TryBuy(body, ItemCatalog.Pickaxe);
                if (!buy.Applied || body.Gold != 15)
                    throw new InvalidOperationException("곡괭이 구매 실패 gold=" + body.Gold);
                var bag = shopBody.GetComponent<InventoryBag>();
                if (bag.ToolUses(ItemCatalog.Pickaxe) <= 0)
                    throw new InvalidOperationException("산 곡괭이가 없음");
                bag.Add("iron_ore", 1);
                var sell = world.TrySell(body, "iron_ore");
                if (!sell.Applied || body.Gold != 17)
                    throw new InvalidOperationException("광석 판매 실패 gold=" + body.Gold);
                UnityEngine.Object.DestroyImmediate(vendorGo);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(shopBody);
            }

            var trainGo = new GameObject("selfcheck-train");
            try
            {
                var world = OfflineWorld.Instance;
                var body = trainGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.Gold = 10;
                var tr = new GameObject("TrainerTmp");
                tr.transform.position = trainGo.transform.position;
                var station = tr.AddComponent<TrainerStation>();
                var open = world.TryTrainer(body, station);
                if (!open.Applied)
                    throw new InvalidOperationException("훈련사 열기 실패");
                int str0 = world.StatsOf(body).Str;
                var trained = world.TryTrain(body, SkillId.Mining);
                if (!trained.Applied || Math.Abs(world.SkillsOf(body).Get(SkillId.Mining) - 1f) > 0.01f || body.Gold != 5)
                    throw new InvalidOperationException("훈련 실패");
                if (world.StatsOf(body).Str != str0)
                    throw new InvalidOperationException("NPC 훈련은 STR을 올리면 안 됩니다.");
                world.SkillsOf(body).ForceSet(SkillId.Mining, 30f, SkillLock.Up);
                body.Gold = 20;
                var capped = world.TryTrain(body, SkillId.Mining);
                if (capped.Applied)
                    throw new InvalidOperationException("30 이상은 훈련되면 안 됩니다.");
                UnityEngine.Object.DestroyImmediate(tr);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(trainGo);
            }

            OpLog.Write("gm", "selfcheck", "test", "hello");
            string[] recent = OpLog.Recent(5);
            bool saw = false;
            for (int i = 0; i < recent.Length; i++)
                if (recent[i].IndexOf("hello", StringComparison.Ordinal) >= 0)
                    saw = true;
            if (!saw)
                throw new InvalidOperationException("운영 로그 기록이 실패했습니다.");
            OpLog.Freeze("selfcheck-ban", true);
            if (!OpLog.IsFrozen("selfcheck-ban"))
                throw new InvalidOperationException("계정 정지가 안 됩니다.");
            OpLog.Freeze("selfcheck-ban", false);
            if (OpLog.IsFrozen("selfcheck-ban"))
                throw new InvalidOperationException("계정 해제가 안 됩니다.");
            string bak = OpLog.Backup();
            if (string.IsNullOrEmpty(bak) || !Directory.Exists(bak))
                throw new InvalidOperationException("백업 실패");
            var gmGo = new GameObject("selfcheck-gm");
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                    throw new InvalidOperationException("OfflineWorld 없음");
                var body = gmGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                gmGo.AddComponent<InventoryBag>();
                var give = world.GmGive(body, "iron_ore", 2);
                if (!give.Applied)
                    throw new InvalidOperationException("GM 지급 실패");
                var set = world.GmSetSkill(body, SkillId.Swordsmanship, 12f);
                if (!set.Applied || Math.Abs(world.SkillsOf(body).Get(SkillId.Swordsmanship) - 12f) > 0.01f)
                    throw new InvalidOperationException("GM 스킬 수정 실패");
                var warp = world.GmWarpPlaza(body);
                if (!warp.Applied || Math.Abs(body.transform.position.x) > 0.2f)
                    throw new InvalidOperationException("GM 워프 실패");
                var skel = new GameObject("Skeleton");
                var sb = skel.AddComponent<WorldBody>();
                sb.IsEnemy = true;
                sb.MaxHp = 30f;
                sb.ResetHp();
                var spawn = world.GmSpawnSkeleton();
                if (!spawn.Applied || GameObject.Find("Skeleton_gm") == null)
                    throw new InvalidOperationException("GM 소환 실패");
                var gone = world.GmDespawnExtra();
                if (!gone.Applied)
                    throw new InvalidOperationException("GM 삭제 실패");
                UnityEngine.Object.DestroyImmediate(skel);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gmGo);
            }

            if (!GuardZone.Contains(0f, 0f) || GuardZone.Contains(20f, 0f))
                throw new InvalidOperationException("가드존 반경이 마을과 안 맞습니다.");
            var notoGo = new GameObject("selfcheck-noto");
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                    throw new InvalidOperationException("OfflineWorld 없음");
                notoGo.transform.position = Vector3.zero;
                var body = notoGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.MaxHp = 50f;
                body.ResetHp();
                world.SetLocalPlayer(body);
                var mob = new GameObject("selfcheck-mob");
                mob.transform.position = Vector3.zero;
                var mb = mob.AddComponent<WorldBody>();
                mb.IsEnemy = true;
                mb.MaxHp = 8f;
                mb.ResetHp();
                int fame0 = body.Fame;
                var hunt = world.TryAttack(body, mb);
                if (!hunt.Applied)
                    throw new InvalidOperationException("사냥 실패: " + hunt.FailReason);
                if (!mb.Alive && body.Fame < fame0 + 10)
                    throw new InvalidOperationException("처치 후 명성이 올라야 합니다.");
                UnityEngine.Object.DestroyImmediate(mob);
                var vic = new GameObject("selfcheck-innocent");
                vic.transform.position = Vector3.zero;
                var vb = vic.AddComponent<WorldBody>();
                vb.IsEnemy = false;
                vb.MaxHp = 40f;
                vb.ResetHp();
                float hp0 = body.Hp;
                var assault = world.TryAttack(body, vb);
                if (assault.Applied || assault.FailReason != "innocent")
                    throw new InvalidOperationException("무고 공격은 막혀야 합니다.");
                if (body.Notoriety != NotorietyId.Criminal)
                    throw new InvalidOperationException("무고 공격 후 범죄가 되어야 합니다.");
                if (body.Hp >= hp0)
                    throw new InvalidOperationException("마을 가드가 범죄자를 쳐야 합니다.");
                UnityEngine.Object.DestroyImmediate(vic);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(notoGo);
            }


            var carpBody = new GameObject("selfcheck-carp");
            GameObject carpWorldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    carpWorldGo = new GameObject("selfcheck-carp-world");
                    world = carpWorldGo.AddComponent<OfflineWorld>();
                }
                var body = carpBody.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = carpBody.AddComponent<InventoryBag>();
                bag.Add("wood", 2);
                var bench = new GameObject("selfcheck-bench");
                bench.transform.position = carpBody.transform.position;
                var station = bench.AddComponent<CraftStation>();
                station.RecipeId = "wooden_club";
                station.DisplayName = "목공소";
                var made = world.TryCraft(body, station);
                if (!made.Applied)
                    throw new InvalidOperationException("목공 제작 실패: " + made.FailReason);
                bool club = false;
                int woodLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.WoodenClub) club = true;
                    if (bag.Items[i].TemplateId == "wood") woodLeft += bag.Items[i].Amount;
                }
                if (!club || woodLeft != 0)
                    throw new InvalidOperationException("나무 2 → 나무곤봉 1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Carpentry) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("목공 제작 후 0.1이어야 합니다.");
                bag.Add("wood", 3);
                var bowMade = world.TryCraft(body, station, "wooden_bow");
                if (!bowMade.Applied)
                    throw new InvalidOperationException("나무활 제작 실패: " + bowMade.FailReason);
                bool bow = false;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.WoodenBow)
                        bow = true;
                if (!bow)
                    throw new InvalidOperationException("나무 3 → 나무활 1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Carpentry) - 0.2f) > 0.0001f)
                    throw new InvalidOperationException("나무활 제작 후 목공 0.2이어야 합니다.");

                var dummy = new GameObject("selfcheck-arch-skel");
                try
                {
                    var skel = dummy.AddComponent<WorldBody>();
                    skel.IsEnemy = true;
                    skel.MaxHp = 40f;
                    skel.ResetHp();
                    dummy.transform.position = carpBody.transform.position + new Vector3(0f, 0f, 6.5f);
                    var shot = world.TryAttack(body, skel);
                    if (!shot.Applied)
                        throw new InvalidOperationException("활 원거리 공격 실패: " + shot.FailReason);
                    if (Math.Abs(world.SkillsOf(body).Get(SkillId.Archery) - 0.1f) > 0.0001f)
                        throw new InvalidOperationException("활 공격 후 궁술 0.1이어야 합니다.");
                    if (Math.Abs(world.SkillsOf(body).Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                        throw new InvalidOperationException("활 공격 후 전술 0.1이어야 합니다.");
                    if (Math.Abs(world.SkillsOf(body).Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                        throw new InvalidOperationException("활 공격 후 해부학 0.1이어야 합니다.");
                    if (skel.Hp >= 40f)
                        throw new InvalidOperationException("활 공격이 피해를 줘야 합니다.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(dummy);
                }
                UnityEngine.Object.DestroyImmediate(bench);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(carpBody);
                if (carpWorldGo != null)
                    UnityEngine.Object.DestroyImmediate(carpWorldGo);
            }

            int dmgOpen = AttackResolve.RetaliationDamage;
            var noShieldSkills = new SkillSet();
            if (AttackResolve.TryParry(noShieldSkills, new StatSet(), false, 20f, ref dmgOpen, out _, out _))
                throw new InvalidOperationException("방패 없이 막기가 되면 안 됩니다.");
            if (dmgOpen != AttackResolve.RetaliationDamage || Math.Abs(noShieldSkills.Get(SkillId.Parrying)) > 0.0001f)
                throw new InvalidOperationException("방패 없는 반격은 방패술을 올리면 안 됩니다.");

            int dmgBlock = AttackResolve.RetaliationDamage;
            var shieldSkills = new SkillSet();
            var parryStats = new StatSet();
            int dexWas = parryStats.Dex;
            if (!AttackResolve.TryParry(shieldSkills, parryStats, true, 20f, ref dmgBlock, out float parryBefore, out float parryAfter))
                throw new InvalidOperationException("방패 막기가 들어가야 합니다.");
            if (Math.Abs(parryBefore) > 0.0001f || Math.Abs(parryAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException($"방패술 0.0→0.1이어야 합니다. 실제 {parryBefore}→{parryAfter}");
            if (Math.Abs(shieldSkills.Get(SkillId.Parrying) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("SkillSet 방패술이 0.1이어야 합니다.");
            if (dmgBlock >= AttackResolve.RetaliationDamage)
                throw new InvalidOperationException("방패가 피해를 줄여야 합니다.");
            if (parryStats.Dex != dexWas + 1)
                throw new InvalidOperationException("방패술 상승 시 DEX가 올라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Parrying) != StatId.Dex)
                throw new InvalidOperationException("방패술 Primary는 DEX이어야 합니다.");

            var parryLocked = new SkillSet();
            parryLocked.SetLock(SkillId.Parrying, SkillLock.Locked);
            int dmgLocked = AttackResolve.RetaliationDamage;
            AttackResolve.TryParry(parryLocked, null, true, 20f, ref dmgLocked, out _, out _);
            if (Math.Abs(parryLocked.Get(SkillId.Parrying)) > 0.0001f)
                throw new InvalidOperationException("잠긴 방패술은 오르면 안 됩니다.");

            var parryGo = new GameObject("selfcheck-parry");
            GameObject parryWorldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    parryWorldGo = new GameObject("selfcheck-parry-world");
                    world = parryWorldGo.AddComponent<OfflineWorld>();
                }
                var body = parryGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.MaxHp = 50f;
                body.ResetHp();
                var bag = parryGo.AddComponent<InventoryBag>();
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.WoodenShield, Amount = 1, Uses = 30 });
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 40 });
                var dummy = new GameObject("selfcheck-parry-skel");
                try
                {
                    var skel = dummy.AddComponent<WorldBody>();
                    skel.IsEnemy = true;
                    skel.MaxHp = 40f;
                    skel.ResetHp();
                    dummy.transform.position = parryGo.transform.position;
                    float hp0 = body.Hp;
                    var swung = world.TryAttack(body, skel);
                    if (!swung.Applied)
                        throw new InvalidOperationException("방패 근접 공격 실패: " + swung.FailReason);
                    if (Math.Abs(world.SkillsOf(body).Get(SkillId.Parrying) - 0.1f) > 0.0001f)
                        throw new InvalidOperationException("근접 반격 후 방패술 0.1이어야 합니다.");
                    if (Math.Abs(world.SkillsOf(body).Get(SkillId.Swordsmanship) - 0.1f) > 0.0001f)
                        throw new InvalidOperationException("방패 루프가 검술 상승을 막으면 안 됩니다.");
                    if (Math.Abs(world.SkillsOf(body).Get(SkillId.Tactics) - 0.1f) > 0.0001f)
                        throw new InvalidOperationException("방패 루프가 전술 상승을 막으면 안 됩니다.");
                    if (Math.Abs(world.SkillsOf(body).Get(SkillId.Anatomy) - 0.1f) > 0.0001f)
                        throw new InvalidOperationException("방패 루프가 해부학 상승을 막으면 안 됩니다.");
                    if (body.Hp >= hp0)
                        throw new InvalidOperationException("반격 피해가 있어야 합니다.");
                    if (body.Hp <= hp0 - AttackResolve.RetaliationDamage)
                        throw new InvalidOperationException("방패가 반격 피해를 줄여야 합니다.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(dummy);
                }

                var bare = new GameObject("selfcheck-parry-bare");
                try
                {
                    var b2 = bare.AddComponent<WorldBody>();
                    b2.IsAvatar = true;
                    b2.MaxHp = 50f;
                    b2.ResetHp();
                    var bag2 = bare.AddComponent<InventoryBag>();
                    bag2.Add(new ItemRecord { TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 40 });
                    var dummy2 = new GameObject("selfcheck-parry-skel2");
                    var skel2 = dummy2.AddComponent<WorldBody>();
                    skel2.IsEnemy = true;
                    skel2.MaxHp = 40f;
                    skel2.ResetHp();
                    dummy2.transform.position = bare.transform.position;
                    float hpBare = b2.Hp;
                    var swung2 = world.TryAttack(b2, skel2);
                    if (!swung2.Applied)
                        throw new InvalidOperationException("무방패 근접 실패: " + swung2.FailReason);
                    if (Math.Abs(world.SkillsOf(b2).Get(SkillId.Parrying)) > 0.0001f)
                        throw new InvalidOperationException("방패 없이 방패술이 오르면 안 됩니다.");
                    if (Math.Abs(b2.Hp - (hpBare - AttackResolve.RetaliationDamage)) > 0.01f)
                        throw new InvalidOperationException("무방패 반격은 피해 " + AttackResolve.RetaliationDamage + "이어야 합니다.");
                    UnityEngine.Object.DestroyImmediate(dummy2);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(bare);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parryGo);
                if (parryWorldGo != null)
                    UnityEngine.Object.DestroyImmediate(parryWorldGo);
            }

            if (CraftRecipes.Find("bandage") == null || CraftRecipes.Find("bandage").Output != ItemCatalog.Bandage)
                throw new InvalidOperationException("붕대 레시피가 카탈로그에 있어야 합니다.");
            if (ItemCatalog.BuyPrice(ItemCatalog.Bandage) <= 0 || ItemCatalog.WeightOf(ItemCatalog.Bandage) <= 0f)
                throw new InvalidOperationException("붕대 카탈로그 항목이 없습니다.");
            if (StatSet.PrimaryOf(SkillId.Healing) != StatId.Dex)
                throw new InvalidOperationException("치유 Primary는 DEX이어야 합니다.");

            var noBandage = HealResolve.Resolve(new HealRequest
            {
                Distance = 0f,
                Now = 1f,
                Skills = new SkillSet(),
                HasBandage = false,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 50f
            });
            if (noBandage.Applied)
                throw new InvalidOperationException("붕대 없이 치유되면 안 됩니다.");

            var fullHp = HealResolve.Resolve(new HealRequest
            {
                Distance = 0f,
                Now = 1f,
                Skills = new SkillSet(),
                HasBandage = true,
                TargetAlive = true,
                TargetHp = 50f,
                TargetMaxHp = 50f
            });
            if (fullHp.Applied)
                throw new InvalidOperationException("만피면 치유되면 안 됩니다.");

            var farHeal = HealResolve.Resolve(new HealRequest
            {
                Distance = 9f,
                Now = 1f,
                Skills = new SkillSet(),
                HasBandage = true,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 50f
            });
            if (farHeal.Applied)
                throw new InvalidOperationException("사거리 밖 붕대는 들어가면 안 됩니다.");

            var healSkills = new SkillSet();
            var healStats = new StatSet();
            int healDexWas = healStats.Dex;
            var healed = HealResolve.Resolve(new HealRequest
            {
                Distance = 0f,
                Now = 1f,
                Skills = healSkills,
                Stats = healStats,
                HasBandage = true,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 50f,
                Difficulty = HealResolve.Difficulty
            });
            if (!healed.Applied || healed.Damage < HealResolve.BaseHeal)
                throw new InvalidOperationException("붕대 치유가 들어가야 합니다.");
            if (Math.Abs(healed.SkillBefore) > 0.0001f || Math.Abs(healed.SkillAfter - 0.1f) > 0.0001f)
                throw new InvalidOperationException($"치유 0.0→0.1이어야 합니다. 실제 {healed.SkillBefore}→{healed.SkillAfter}");
            if (Math.Abs(healSkills.Get(SkillId.Healing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("SkillSet 치유가 0.1이어야 합니다.");
            if (healStats.Dex != healDexWas + 1)
                throw new InvalidOperationException("치유 상승 시 DEX가 올라야 합니다.");
            if (Math.Abs(healSkills.Get(SkillId.Anatomy)) > 0.0001f)
                throw new InvalidOperationException("붕대는 해부학을 올리면 안 됩니다.");

            var anaBoost = new SkillSet();
            anaBoost.ForceSet(SkillId.Anatomy, 40f, SkillLock.Up);
            int plain = HealResolve.Amount(new SkillSet(), new StatSet());
            int boosted = HealResolve.Amount(anaBoost, new StatSet());
            if (boosted <= plain)
                throw new InvalidOperationException("해부학이 붕대 치유량에 반영되어야 합니다.");

            var healLock = new SkillSet();
            healLock.SetLock(SkillId.Healing, SkillLock.Locked);
            var lockedHeal = HealResolve.Resolve(new HealRequest
            {
                Distance = 0f,
                Now = 2f,
                Skills = healLock,
                HasBandage = true,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 50f
            });
            if (!lockedHeal.Applied)
                throw new InvalidOperationException("잠긴 치유도 치료는 되어야 합니다.");
            if (Math.Abs(healLock.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("잠긴 치유는 오르면 안 됩니다.");

            var healCreate = CharacterCreate.Build("heal-check", "치료", 0, 20, 40, 20,
                new[] { SkillId.Healing, SkillId.Anatomy, SkillId.Tailoring },
                new[] { 50f, 30f, 20f });
            bool hasBandageStart = false, hasClothStart = false;
            for (int i = 0; i < healCreate.Inventory.Length; i++)
            {
                if (healCreate.Inventory[i].TemplateId == ItemCatalog.Bandage && healCreate.Inventory[i].Amount >= 10)
                    hasBandageStart = true;
                if (healCreate.Inventory[i].TemplateId == ItemCatalog.Cloth && healCreate.Inventory[i].Amount >= 4)
                    hasClothStart = true;
            }
            if (!hasBandageStart)
                throw new InvalidOperationException("치유 시작은 붕대를 줘야 합니다.");
            if (!hasClothStart)
                throw new InvalidOperationException("재봉 시작은 천을 줘야 합니다.");

            var healGo = new GameObject("selfcheck-heal");
            GameObject healWorldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    healWorldGo = new GameObject("selfcheck-heal-world");
                    world = healWorldGo.AddComponent<OfflineWorld>();
                }
                var body = healGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.MaxHp = 50f;
                body.ResetHp();
                var bag = healGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Cloth, 1);
                var bench = new GameObject("selfcheck-tailor");
                bench.transform.position = healGo.transform.position;
                var station = bench.AddComponent<CraftStation>();
                station.RecipeId = "bandage";
                station.DisplayName = "재봉";
                var made = world.TryCraft(body, station);
                if (!made.Applied)
                    throw new InvalidOperationException("붕대 제작 실패: " + made.FailReason);
                bool hasBn = false;
                int clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.Bandage) hasBn = true;
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth) clothLeft += bag.Items[i].Amount;
                }
                if (!hasBn || clothLeft != 0)
                    throw new InvalidOperationException("천 1 → 붕대 1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Tailoring) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("붕대 제작 후 재봉 0.1이어야 합니다.");

                body.SetHp(20f);
                var none = world.TryHeal(body, body);
                if (!none.Applied)
                    throw new InvalidOperationException("자가 붕대 실패: " + none.FailReason);
                if (body.Hp <= 20f)
                    throw new InvalidOperationException("붕대가 HP를 올려야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Healing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 치유 후 0.1이어야 합니다.");
                int leftBn = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Bandage)
                        leftBn += bag.Items[i].Amount;
                if (leftBn != 0)
                    throw new InvalidOperationException("성공 치유는 붕대를 소모해야 합니다.");

                var noBn = world.TryHeal(body, body);
                if (noBn.Applied)
                    throw new InvalidOperationException("붕대 소진 후 치유되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Healing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("실패한 치유는 스킬을 올리면 안 됩니다.");

                var palHealerGo = new GameObject("selfcheck-heal-other");
                palHealerGo.transform.position = healGo.transform.position;
                var palHealer = palHealerGo.AddComponent<WorldBody>();
                palHealer.IsAvatar = true;
                palHealer.MaxHp = 50f;
                palHealer.ResetHp();
                var palBag = palHealerGo.AddComponent<InventoryBag>();
                palBag.Add(ItemCatalog.Bandage, 2);
                var pal = new GameObject("selfcheck-heal-pal");
                pal.transform.position = healGo.transform.position;
                var pb = pal.AddComponent<WorldBody>();
                pb.IsEnemy = false;
                pb.MaxHp = 40f;
                pb.ResetHp();
                pb.SetHp(15f);
                var palHeal = world.TryHeal(palHealer, pb);
                if (!palHeal.Applied || pb.Hp <= 15f)
                    throw new InvalidOperationException("아군 붕대 실패: " + palHeal.FailReason);

                var foe = new GameObject("selfcheck-heal-foe");
                foe.transform.position = healGo.transform.position;
                var fb = foe.AddComponent<WorldBody>();
                fb.IsEnemy = true;
                fb.MaxHp = 30f;
                fb.ResetHp();
                fb.SetHp(10f);
                var foeHeal = world.TryHeal(palHealer, fb);
                if (foeHeal.Applied)
                    throw new InvalidOperationException("적에게 붕대하면 안 됩니다.");
                UnityEngine.Object.DestroyImmediate(pal);
                UnityEngine.Object.DestroyImmediate(foe);
                UnityEngine.Object.DestroyImmediate(palHealerGo);
                UnityEngine.Object.DestroyImmediate(bench);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(healGo);
                if (healWorldGo != null)
                    UnityEngine.Object.DestroyImmediate(healWorldGo);
            }

            AssertMeditationSlice();
            AssertMagicResistSlice();
            AssertEvalIntSlice();
            AssertFishingSlice();
            Debug.Log("[Ulon] Slice self-check PASS — 몬스터 2종(스켈레톤+도적), 검술/채광/제작, 목공, 궁술/나무활, 전술 0.0→0.1, 방패술 0.0→0.1, 해부학 0.0→0.1, 치유 붕대 0.0→0.1, 명상 마나 0.0→0.1, 마법 저항 0.0→0.1, 지능 평가 0.0→0.1, 낚시 0.0→0.1, STR/HP, 700캡↓, 은행, 캐릭터 생성, 주문책, 시체/부활, 무게/도구, 리스폰/시약, 상점, 훈련, 운영툴, 명성/가드존");
        }

        static void AssertMeditationSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Meditation) != StatId.Int)
                throw new InvalidOperationException("명상 Primary는 INT이어야 합니다.");

            var fullSkills = new SkillSet();
            var full = MeditationResolve.Resolve(new MeditationRequest
            {
                Now = 1f,
                Skills = fullSkills,
                Mana = 35f,
                MaxMana = 35f
            });
            if (full.Applied)
                throw new InvalidOperationException("마나 가득이면 명상되면 안 됩니다.");
            if (Math.Abs(fullSkills.Get(SkillId.Meditation)) > 0.0001f)
                throw new InvalidOperationException("실패한 명상은 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            var ok = MeditationResolve.Resolve(new MeditationRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                Mana = 5f,
                MaxMana = 35f,
                Difficulty = MeditationResolve.Difficulty
            });
            if (!ok.Applied || ok.Damage < MeditationResolve.BaseRegen)
                throw new InvalidOperationException("명상이 마나를 회복해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Meditation) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 명상 후 0.1이어야 합니다.");

            int light = MeditationResolve.Amount(new SkillSet(), new StatSet(), false);
            int heavy = MeditationResolve.Amount(new SkillSet(), new StatSet(), true);
            if (heavy >= light)
                throw new InvalidOperationException("중갑은 명상 회복이 낮아야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Meditation, SkillLock.Locked);
            var lockedOk = MeditationResolve.Resolve(new MeditationRequest
            {
                Now = 1f,
                Skills = locked,
                Mana = 5f,
                MaxMana = 35f
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 명상도 회복은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Meditation)) > 0.0001f)
                throw new InvalidOperationException("잠긴 명상은 오르면 안 됩니다.");

            var go = new GameObject("selfcheck-meditate");
            GameObject worldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-meditate-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromInt(world.StatsOf(body).Int);
                body.SetMana(4f);
                float before = body.Mana;
                var none = world.TryMeditate(body);
                if (!none.Applied)
                    throw new InvalidOperationException("서버 명상 실패: " + none.FailReason);
                if (body.Mana <= before)
                    throw new InvalidOperationException("명상이 마나를 올려야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Meditation) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 명상 후 서버 스킬 0.1이어야 합니다.");

                body.SetMana(body.MaxMana);
                var fullWorld = world.TryMeditate(body);
                if (fullWorld.Applied)
                    throw new InvalidOperationException("가득 찬 마나로 명상되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Meditation) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 명상은 스킬을 올리면 안 됩니다.");

                var plateGo = new GameObject("selfcheck-meditate-plate");
                plateGo.transform.position = go.transform.position;
                var plateBody = plateGo.AddComponent<WorldBody>();
                plateBody.IsAvatar = true;
                plateBody.RecalcFromInt(world.StatsOf(plateBody).Int);
                plateBody.SetMana(4f);
                var plateBag = plateGo.AddComponent<InventoryBag>();
                plateBag.Add("iron_plate", 1);
                if (!ItemCatalog.HasHeavyArmor(plateBag.Items))
                    throw new InvalidOperationException("iron_plate는 중갑이어야 합니다.");
                var heavyHit = world.TryMeditate(plateBody);
                if (!heavyHit.Applied)
                    throw new InvalidOperationException("중갑 명상 실패: " + heavyHit.FailReason);
                int plated = MeditationResolve.Amount(world.SkillsOf(plateBody), world.StatsOf(plateBody), true);
                if (heavyHit.Damage != plated)
                    throw new InvalidOperationException("중갑 명상 회복량이 패널티를 받아야 합니다.");
                UnityEngine.Object.DestroyImmediate(plateGo);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertMagicResistSlice()
        {
            if (StatSet.PrimaryOf(SkillId.MagicResist) != StatId.Int)
                throw new InvalidOperationException("마법 저항 Primary는 INT이어야 합니다.");

            int raw = 10;
            int none = MagicResistResolve.Reduce(raw, new SkillSet(), new StatSet(), 0);
            int geared = MagicResistResolve.Reduce(raw, new SkillSet(), new StatSet(), 2);
            if (none >= raw)
                throw new InvalidOperationException("마법 저항은 마법 피해를 줄여야 합니다.");
            if (geared >= none)
                throw new InvalidOperationException("장비 저항이 마법 피해를 더 줄여야 합니다.");
            var melee = new SkillSet();
            var phys = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1.2f,
                Now = 1f,
                Skills = melee,
                TargetAlive = true
            });
            if (!phys.Applied)
                throw new InvalidOperationException("물리 공격 대조 실패");
            if (Math.Abs(melee.Get(SkillId.MagicResist)) > 0.0001f)
                throw new InvalidOperationException("물리 피격은 마법 저항을 올리면 안 됩니다.");

            var go = new GameObject("selfcheck-resist");
            GameObject worldGo = null;
            GameObject casterGo = null;
            GameObject plateGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-resist-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.IsEnemy = false;
                body.MaxHp = 50f;
                body.ResetHp();
                body.RecalcFromInt(world.StatsOf(body).Int);
                go.AddComponent<InventoryBag>();

                casterGo = new GameObject("selfcheck-resist-caster");
                casterGo.transform.position = go.transform.position;
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsEnemy = true;
                caster.MaxHp = 40f;
                caster.ResetHp();
                caster.RecalcFromInt(40);
                caster.SetMana(40f);
                var cbag = casterGo.AddComponent<InventoryBag>();
                cbag.Add(SpellCast.Reagent, 8);
                world.BookOf(caster).Learn(SpellId.Ember);
                world.BookOf(caster).Learn(SpellId.Mend);

                float hp0 = body.Hp;
                var ember = world.TryCast(caster, SpellId.Ember, body);
                if (!ember.Applied || body.Hp >= hp0)
                    throw new InvalidOperationException("적대 불씨가 플레이어에게 들어가야 합니다: " + ember.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.MagicResist) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("적대 주문 피격 후 마법 저항 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.MagicResist)) > 0.0001f)
                    throw new InvalidOperationException("시전자 마법 저항이 오르면 안 됩니다.");

                int dmg0 = ember.Damage;
                world.SkillsOf(body).ForceSet(SkillId.MagicResist, 40f, SkillLock.Up);
                body.SetHp(50f);
                var hard = world.TryCast(caster, SpellId.Ember, body);
                if (!hard.Applied)
                    throw new InvalidOperationException("고숙련 저항 불씨 실패: " + hard.FailReason);
                if (hard.Damage >= dmg0)
                    throw new InvalidOperationException("높은 마법 저항이 피해를 더 줄여야 합니다.");

                var palGo = new GameObject("selfcheck-resist-pal");
                palGo.transform.position = go.transform.position;
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsEnemy = true;
                pal.MaxHp = 40f;
                pal.ResetHp();
                var same = world.TryCast(caster, SpellId.Ember, pal);
                if (same.Applied)
                    throw new InvalidOperationException("같은 편 불씨는 실패해야 합니다.");
                UnityEngine.Object.DestroyImmediate(palGo);

                float resistBeforeMend = world.SkillsOf(body).Get(SkillId.MagicResist);
                var mend = world.TryCast(caster, SpellId.Mend, body);
                if (!mend.Applied)
                    throw new InvalidOperationException("봉합은 시전자 치유여야 합니다: " + mend.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.MagicResist) - resistBeforeMend) > 0.0001f)
                    throw new InvalidOperationException("우호 주문은 마법 저항을 올리면 안 됩니다.");

                var locked = world.SkillsOf(body);
                locked.ForceSet(SkillId.MagicResist, 0f, SkillLock.Locked);
                body.SetHp(50f);
                var lockedHit = world.TryCast(caster, SpellId.Ember, body);
                if (!lockedHit.Applied)
                    throw new InvalidOperationException("잠긴 저항도 피격은 되어야 합니다: " + lockedHit.FailReason);
                if (Math.Abs(locked.Get(SkillId.MagicResist)) > 0.0001f)
                    throw new InvalidOperationException("잠긴 마법 저항은 오르면 안 됩니다.");

                plateGo = new GameObject("selfcheck-resist-plate");
                plateGo.transform.position = casterGo.transform.position;
                var plateBody = plateGo.AddComponent<WorldBody>();
                plateBody.IsAvatar = true;
                plateBody.IsEnemy = false;
                plateBody.MaxHp = 50f;
                plateBody.ResetHp();
                var plateBag = plateGo.AddComponent<InventoryBag>();
                plateBag.Add("iron_plate", 1);
                if (ItemCatalog.EquipmentMagicResist(plateBag.Items) < 2)
                    throw new InvalidOperationException("iron_plate는 장비 마법 저항을 줘야 합니다.");
                float php = plateBody.Hp;
                var plateHit = world.TryCast(caster, SpellId.Ember, plateBody);
                if (!plateHit.Applied || plateBody.Hp >= php)
                    throw new InvalidOperationException("중갑 대상 불씨 실패: " + plateHit.FailReason);
                if (plateHit.Damage >= lockedHit.Damage)
                    throw new InvalidOperationException("장비 저항이 서버 불씨 피해를 더 줄여야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (plateGo != null)
                    UnityEngine.Object.DestroyImmediate(plateGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertEvalIntSlice()
        {
            if (StatSet.PrimaryOf(SkillId.EvaluateIntelligence) != StatId.Int)
                throw new InvalidOperationException("지능 평가 Primary는 INT이어야 합니다.");

            var noneSkills = new SkillSet();
            var none = EvalIntResolve.Resolve(new EvalIntRequest
            {
                Now = 1f,
                Skills = noneSkills,
                TargetStats = null
            });
            if (none.Applied)
                throw new InvalidOperationException("대상 없는 지능 평가는 실패해야 합니다.");
            if (Math.Abs(noneSkills.Get(SkillId.EvaluateIntelligence)) > 0.0001f)
                throw new InvalidOperationException("실패한 지능 평가는 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = EvalIntResolve.Resolve(new EvalIntRequest
            {
                Distance = 20f,
                Now = 1f,
                Skills = farSkills,
                TargetStats = new StatSet(),
                TargetAlive = true
            });
            if (far.Applied)
                throw new InvalidOperationException("사거리 밖 지능 평가는 들어가면 안 됩니다.");
            if (Math.Abs(farSkills.Get(SkillId.EvaluateIntelligence)) > 0.0001f)
                throw new InvalidOperationException("실패한 지능 평가는 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            var targetStats = new StatSet();
            targetStats.ForceSet(20, 20, 40);
            int intWas = stats.Int;
            var ok = EvalIntResolve.Resolve(new EvalIntRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = skills,
                Stats = stats,
                TargetStats = targetStats,
                TargetAlive = true,
                TargetMana = 12f,
                TargetMaxMana = 50f,
                Difficulty = EvalIntResolve.Difficulty
            });
            if (!ok.Applied || ok.Intelligence != 40 || ok.Mana != 12 || ok.MaxMana != 50)
                throw new InvalidOperationException("지능 평가는 대상 INT/마나를 밝혀야 합니다.");
            if (Math.Abs(skills.Get(SkillId.EvaluateIntelligence) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 지능 평가 후 0.1이어야 합니다.");
            if (stats.Int != intWas + 1)
                throw new InvalidOperationException("지능 평가 상승 시 INT가 올라야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.EvaluateIntelligence, SkillLock.Locked);
            var lockedOk = EvalIntResolve.Resolve(new EvalIntRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = locked,
                TargetStats = targetStats,
                TargetAlive = true,
                TargetMana = 12f,
                TargetMaxMana = 50f
            });
            if (!lockedOk.Applied || lockedOk.Intelligence != 40)
                throw new InvalidOperationException("잠긴 지능 평가도 정보는 보여야 합니다.");
            if (Math.Abs(locked.Get(SkillId.EvaluateIntelligence)) > 0.0001f)
                throw new InvalidOperationException("잠긴 지능 평가는 오르면 안 됩니다.");

            int plain = SpellCast.EmberDamage(new StatSet(), new SkillSet());
            var boosted = new SkillSet();
            boosted.ForceSet(SkillId.EvaluateIntelligence, 40f, SkillLock.Up);
            int withEval = SpellCast.EmberDamage(new StatSet(), boosted);
            if (withEval <= plain)
                throw new InvalidOperationException("지능 평가가 공격 마법 위력에 반영되어야 합니다.");

            var melee = new SkillSet();
            var phys = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1.2f,
                Now = 1f,
                Skills = melee,
                TargetAlive = true
            });
            if (!phys.Applied)
                throw new InvalidOperationException("물리 공격 대조 실패");
            if (Math.Abs(melee.Get(SkillId.EvaluateIntelligence)) > 0.0001f)
                throw new InvalidOperationException("물리 공격은 지능 평가를 올리면 안 됩니다.");

            var go = new GameObject("selfcheck-evalint");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            GameObject casterGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-evalint-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromInt(world.StatsOf(body).Int);

                var missing = world.TryEvaluate(body, null);
                if (missing.Applied)
                    throw new InvalidOperationException("서버 대상 없는 지능 평가는 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.EvaluateIntelligence)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 지능 평가는 스킬을 올리면 안 됩니다.");

                tgtGo = new GameObject("selfcheck-evalint-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.DisplayName = "스켈레톤";
                tgt.MaxHp = 30f;
                tgt.ResetHp();
                world.StatsOf(tgt).ForceSet(20, 20, 40);
                tgt.RecalcFromInt(40);
                tgt.SetMana(18f);

                var hit = world.TryEvaluate(body, tgt);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 지능 평가 실패: " + hit.FailReason);
                if (hit.Intelligence != 40 || hit.Mana != 18 || hit.MaxMana != StatSet.MaxManaOf(40))
                    throw new InvalidOperationException("서버 지능 평가는 대상 INT/마나를 밝혀야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.EvaluateIntelligence) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 지능 평가 후 서버 스킬 0.1이어야 합니다.");
                if (string.IsNullOrEmpty(world.LastEvalMessage) || world.LastEvalMessage.IndexOf("INT 40", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("지능 평가 메시지가 INT를 포함해야 합니다.");

                casterGo = new GameObject("selfcheck-evalint-caster");
                casterGo.transform.position = go.transform.position;
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.RecalcFromInt(world.StatsOf(caster).Int);
                caster.SetMana(40f);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 4);
                world.BookOf(caster).Learn(SpellId.Ember);
                float evalBeforeCast = world.SkillsOf(caster).Get(SkillId.EvaluateIntelligence);
                var ember = world.TryCast(caster, SpellId.Ember, tgt);
                if (!ember.Applied)
                    throw new InvalidOperationException("불씨 대조 실패: " + ember.FailReason);
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.EvaluateIntelligence) - evalBeforeCast) > 0.0001f)
                    throw new InvalidOperationException("주문 시전은 지능 평가를 올리면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertFishingSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Fishing) != StatId.Dex)
                throw new InvalidOperationException("낚시 Primary는 DEX이어야 합니다.");
            if (ItemCatalog.ToolFor(SkillId.Fishing) != ItemCatalog.FishingPole)
                throw new InvalidOperationException("낚시는 낚싯대가 도구여야 합니다.");
            if (ItemCatalog.MaxUsesOf(ItemCatalog.FishingPole) != 20)
                throw new InvalidOperationException("낚싯대 내구 20");
            if (ItemCatalog.BuyPrice(ItemCatalog.FishingPole) <= 0 || ItemCatalog.SellPrice(ItemCatalog.Fish) <= 0)
                throw new InvalidOperationException("낚싯대/생선 상점 가격이 없습니다.");

            var gain = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            SkillGain.TryRaise(gain, SkillId.Fishing, 10f, out _, out float after, stats);
            if (Math.Abs(after - 0.1f) > 0.0001f)
                throw new InvalidOperationException("낚시 0.0→0.1이어야 합니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("낚시 상승 시 DEX가 올라야 합니다.");

            var created = CharacterCreate.Build("fish-check", "낚시", 0, 20, 40, 20,
                new[] { SkillId.Fishing, SkillId.Mining, SkillId.Swordsmanship },
                new[] { 50f, 30f, 20f });
            bool hasPole = false;
            for (int i = 0; i < created.Inventory.Length; i++)
                if (created.Inventory[i].TemplateId == ItemCatalog.FishingPole && created.Inventory[i].Uses == 20)
                    hasPole = true;
            if (!hasPole)
                throw new InvalidOperationException("낚시 시작은 낚싯대를 줘야 합니다.");

            var spot = GameObject.Find("FishingSpot");
            if (spot == null)
                throw new InvalidOperationException("마을에 물가(FishingSpot)가 있어야 합니다.");
            var sceneNode = spot.GetComponent<ResourceNode>();
            if (sceneNode == null || sceneNode.GatherSkill != SkillId.Fishing || sceneNode.ResourceId != ItemCatalog.Fish)
                throw new InvalidOperationException("물가는 낚시 ResourceNode여야 합니다.");

            var go = new GameObject("selfcheck-fish");
            GameObject worldGo = null;
            GameObject nodeGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-fish-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = go.AddComponent<InventoryBag>();
                nodeGo = new GameObject("selfcheck-fish-node");
                nodeGo.transform.position = go.transform.position;
                var node = nodeGo.AddComponent<ResourceNode>();
                node.ResourceId = ItemCatalog.Fish;
                node.DisplayName = "물가";
                node.GatherSkill = SkillId.Fishing;
                node.Remaining = 5;
                node.Capacity = 5;
                node.Difficulty = 10f;
                var noTool = world.TryGather(body, node);
                if (noTool.Applied)
                    throw new InvalidOperationException("낚싯대 없이 낚시되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Fishing)) > 0.0001f)
                    throw new InvalidOperationException("실패한 낚시는 스킬을 올리면 안 됩니다.");
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.FishingPole, Amount = 1, Uses = 1 });
                var ok = world.TryGather(body, node);
                if (!ok.Applied)
                    throw new InvalidOperationException("낚싯대 낚시 실패: " + ok.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Fishing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 낚시 후 0.1이어야 합니다.");
                int fish = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Fish)
                        fish += bag.Items[i].Amount;
                if (fish < 1)
                    throw new InvalidOperationException("잡은 생선이 가방에 있어야 합니다.");
                var broken = world.TryGather(body, node);
                if (broken.Applied)
                    throw new InvalidOperationException("내구 0 낚싯대로 낚시되면 안 됩니다.");

                var locked = new SkillSet();
                locked.SetLock(SkillId.Fishing, SkillLock.Locked);
                SkillGain.TryRaise(locked, SkillId.Fishing, 10f, out _, out _);
                if (Math.Abs(locked.Get(SkillId.Fishing)) > 0.0001f)
                    throw new InvalidOperationException("잠긴 낚시는 오르면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (nodeGo != null)
                    UnityEngine.Object.DestroyImmediate(nodeGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }
    }
}
