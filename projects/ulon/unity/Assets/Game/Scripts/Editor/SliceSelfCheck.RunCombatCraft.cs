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
        /// **전투·제작·훈련 규칙**(랩 ㉪ 4/N) — 때리고 막고 만들고 배우는 것의 검사.
        /// 담는 것: `AttackResolve`·`TryCraft`·`TryTrain` 언저리. 안 담는 것: 몹 카탈로그(본체), 생활 기술.
        /// </summary>
        static void RunCombatCraftRules()
        {
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

            if (!GuardZone.Contains(0f, 0f) || GuardZone.Contains(GuardZone.Radius * 1.25f, 0f))   // 「밖」도 반경에서 유도(랩 B)
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
                body.CharacterId = "carp-mark";
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
                bool clubMark = false;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.WoodenClub && bag.Items[i].MakerId == "carp-mark")
                        clubMark = true;
                if (!clubMark)
                    throw new InvalidOperationException("목공 제작품 Maker Mark가 있어야 합니다.");
                var clubRecipe = CraftRecipes.Find("wooden_club");
                var bowRecipe = CraftRecipes.Find("wooden_bow");
                if (clubRecipe == null || !clubRecipe.CanRepair || bowRecipe == null || !bowRecipe.CanRepair)
                    throw new InvalidOperationException("목공소 레시피는 수리 가능해야 합니다.");
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
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId != ItemCatalog.WoodenBow)
                        continue;
                    if (bag.Items[i].MakerId != "carp-mark")
                        throw new InvalidOperationException("나무활 Maker Mark가 있어야 합니다.");
                    var wornBow = bag.Items[i];
                    wornBow.Uses = 8;
                    bag.Items[i] = wornBow;
                    break;
                }
                bag.Add("wood", 1);
                var bowFix = world.TryCraft(body, station, "wooden_bow");
                if (!bowFix.Applied)
                    throw new InvalidOperationException("나무활 수리 실패: " + bowFix.FailReason);
                bool bowRestored = false;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId != ItemCatalog.WoodenBow)
                        continue;
                    if (bag.Items[i].Uses < 18)
                        throw new InvalidOperationException("목공소 수리 후 내구가 올라야 합니다.");
                    if (bag.Items[i].MakerId != "carp-mark")
                        throw new InvalidOperationException("목공소 수리는 Maker Mark를 지우면 안 됩니다.");
                    bowRestored = true;
                }
                if (!bowRestored)
                    throw new InvalidOperationException("수리 대상 나무활이 없습니다.");

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
        }
    }
}
