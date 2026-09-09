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
        // **떠돌이의 손**(랩 ㉫) — 야영·훔치기·자물쇠. 「남의 자리·남의 것」을 다루는 규칙만 담는다.
        // 안 담는 것: 들키지 않는 몸놀림 자체(`Stealth`) — 그건 감추는 쪽이고 여기는 손대는 쪽이다.
        static void AssertCamping()
        {
            AssertDungeon3Leftover();
            if (SkillId.Camping == SkillId.Cooking)
                throw new InvalidOperationException("야영 SkillId는 요리와 달라야 합니다.");
            if (SkillId.Camping == SkillId.Hiding)
                throw new InvalidOperationException("야영 SkillId는 은신과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Camping) != StatId.Dex)
                throw new InvalidOperationException("야영 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Camping) != "야영" || SkillTitles.JobOf(SkillId.Camping) != "야영꾼")
                throw new InvalidOperationException("야영 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Cooking) != "요리" || SkillTitles.JobOf(SkillId.Cooking) != "요리사")
                throw new InvalidOperationException("요리 스킬명/직업명을 바꾸면 안 됩니다.");
            if (SkillNames.KoreanOf(SkillId.Hiding) != "은신" || SkillTitles.JobOf(SkillId.Hiding) != "은신자")
                throw new InvalidOperationException("은신 스킬명/직업명을 바꾸면 안 됩니다.");

            var ghostSkills = new SkillSet();
            var ghost = CampingResolve.Resolve(new CampingRequest { Now = 1f, Skills = ghostSkills, Ghost = true, NearCampfire = true });
            if (ghost.Applied)
                throw new InvalidOperationException("유령 야영은 실패해야 합니다.");
            if (Math.Abs(ghostSkills.Get(SkillId.Camping)) > 0.0001f)
                throw new InvalidOperationException("실패한 야영은 스킬을 올리면 안 됩니다.");

            var noneSkills = new SkillSet();
            var none = CampingResolve.Resolve(new CampingRequest
            {
                Now = 1f,
                Skills = noneSkills,
                NearCampfire = false,
                HasKindling = false,
                Distance = CampingResolve.CampRange + 4f,
                Range = CampingResolve.CampRange
            });
            if (none.Applied || none.FailReason != "no_fire")
                throw new InvalidOperationException("화덕/불씨 없이 야영하면 안 됩니다.");
            if (Math.Abs(noneSkills.Get(SkillId.Camping)) > 0.0001f)
                throw new InvalidOperationException("화덕 밖 야영 실패는 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = CampingResolve.Resolve(new CampingRequest
            {
                Now = 1f,
                Skills = farSkills,
                NearCampfire = true,
                Distance = CampingResolve.CampRange + 1f,
                Range = CampingResolve.CampRange
            });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("사거리 밖 화덕 야영은 실패해야 합니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            int intWas = stats.Int;
            int strWas = stats.Str;
            var ok = CampingResolve.Resolve(new CampingRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                NearCampfire = true,
                Distance = 1f,
                Difficulty = CampingResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("화덕 야영은 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Camping) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 야영 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Cooking)) > 0.0001f)
                throw new InvalidOperationException("야영은 요리를 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Hiding)) > 0.0001f)
                throw new InvalidOperationException("야영은 은신을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("야영 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != intWas)
                throw new InvalidOperationException("야영은 INT를 올리면 안 됩니다.");
            if (stats.Str != strWas)
                throw new InvalidOperationException("야영은 STR을 올리면 안 됩니다.");

            var kindling = new SkillSet();
            var kindled = CampingResolve.Resolve(new CampingRequest
            {
                Now = 1f,
                Skills = kindling,
                NearCampfire = false,
                HasKindling = true
            });
            if (!kindled.Applied)
                throw new InvalidOperationException("나무 불씨 야영은 성공해야 합니다.");
            if (Math.Abs(kindling.Get(SkillId.Camping) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("불씨 야영 후 0.1이어야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Camping, SkillLock.Locked);
            var lockedOk = CampingResolve.Resolve(new CampingRequest { Now = 1f, Skills = locked, NearCampfire = true, Distance = 1f });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 야영도 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Camping)) > 0.0001f)
                throw new InvalidOperationException("잠긴 야영은 오르면 안 됩니다.");

            var cooking = new SkillSet();
            SkillGain.TryRaise(cooking, SkillId.Cooking, 10f, out _, out _);
            if (Math.Abs(cooking.Get(SkillId.Camping)) > 0.0001f)
                throw new InvalidOperationException("요리는 야영을 올리면 안 됩니다.");
            var hiding = new SkillSet();
            HidingResolve.Resolve(new HidingRequest { Now = 1f, Skills = hiding });
            if (Math.Abs(hiding.Get(SkillId.Camping)) > 0.0001f)
                throw new InvalidOperationException("은신은 야영을 올리면 안 됩니다.");

            var created = CharacterCreate.Build("camp-check", "야영꾼", 0, 20, 40, 20,
                new[] { SkillId.Camping, SkillId.Hiding, SkillId.Cooking },
                new[] { 50f, 30f, 20f });
            bool hasWood = false, hasLute = false, hasFish = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == "wood" && created.Inventory[i].Amount >= 1)
                    hasWood = true;
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
                if (created.Inventory[i].TemplateId == ItemCatalog.Fish)
                    hasFish = true;
            }
            if (!hasWood)
                throw new InvalidOperationException("야영 시작은 나무를 줘야 합니다.");
            if (hasLute)
                throw new InvalidOperationException("야영 시작은 류트를 주면 안 됩니다.");
            if (!hasFish)
                throw new InvalidOperationException("요리 시작은 생선을 줘야 합니다.");

            var fire = GameObject.Find("Campfire");
            if (fire == null)
                throw new InvalidOperationException("마을에 화덕(Campfire)이 있어야 합니다.");

            var go = new GameObject("selfcheck-camp");
            GameObject worldGo = null;
            GameObject kindleGo = null;
            GameObject cookGo = null;
            GameObject stGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-camp-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                go.transform.position = fire.transform.position;
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();
                var bag = go.AddComponent<InventoryBag>();
                bag.Add("wood", 1);

                var camp = world.TryCamp(body);
                if (!camp.Applied)
                    throw new InvalidOperationException("서버 화덕 야영 실패: " + camp.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Camping) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 야영 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Cooking)) > 0.0001f)
                    throw new InvalidOperationException("서버 야영은 요리를 올리면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Hiding)) > 0.0001f)
                    throw new InvalidOperationException("서버 야영은 은신을 올리면 안 됩니다.");
                if (!body.IsCampSafe(Time.time))
                    throw new InvalidOperationException("야영 후 CampSafeUntil이 있어야 합니다.");
                if (body.IsHidden(Time.time))
                    throw new InvalidOperationException("야영은 HiddenUntil을 켜면 안 됩니다.");
                int woodLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == "wood")
                        woodLeft += bag.Items[i].Amount;
                if (woodLeft != 1)
                    throw new InvalidOperationException("화덕 근처 야영은 나무를 쓰면 안 됩니다.");
                if (string.IsNullOrEmpty(world.LastCampMessage) || world.LastCampMessage.IndexOf("야영", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("야영 메시지가 있어야 합니다.");

                kindleGo = new GameObject("selfcheck-camp-kindle");
                kindleGo.transform.position = fire.transform.position + new Vector3(20f, 0f, 20f);
                var kindleBody = kindleGo.AddComponent<WorldBody>();
                kindleBody.IsAvatar = true;
                kindleBody.RecalcFromStr(30);
                kindleBody.ResetHp();
                var kindleBag = kindleGo.AddComponent<InventoryBag>();
                var noWood = world.TryCamp(kindleBody);
                if (noWood.Applied)
                    throw new InvalidOperationException("화덕 밖·불씨 없이 야영되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(kindleBody).Get(SkillId.Camping)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 야영은 스킬을 올리면 안 됩니다.");
                kindleBag.Add("wood", 1);
                var kindleOk = world.TryCamp(kindleBody);
                if (!kindleOk.Applied)
                    throw new InvalidOperationException("서버 불씨 야영 실패: " + kindleOk.FailReason);
                if (Math.Abs(world.SkillsOf(kindleBody).Get(SkillId.Camping) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("불씨 야영 후 서버 스킬 0.1이어야 합니다.");
                if (!kindleBody.IsCampSafe(Time.time))
                    throw new InvalidOperationException("불씨 야영 후 CampSafeUntil이 있어야 합니다.");
                int kindleWood = 0;
                for (int i = 0; i < kindleBag.Items.Count; i++)
                    if (kindleBag.Items[i].TemplateId == "wood")
                        kindleWood += kindleBag.Items[i].Amount;
                if (kindleWood != 0)
                    throw new InvalidOperationException("화덕 밖 야영은 나무를 1개 써야 합니다.");

                cookGo = new GameObject("selfcheck-camp-cook");
                cookGo.transform.position = fire.transform.position;
                var cookBody = cookGo.AddComponent<WorldBody>();
                cookBody.IsAvatar = true;
                var cookBag = cookGo.AddComponent<InventoryBag>();
                cookBag.Add(ItemCatalog.Fish, 1);
                stGo = new GameObject("selfcheck-camp-st");
                stGo.transform.position = cookGo.transform.position;
                var station = stGo.AddComponent<CraftStation>();
                station.RecipeId = "cooked_fish";
                station.DisplayName = "화덕";
                var cooked = world.TryCraft(cookBody, station);
                if (!cooked.Applied)
                    throw new InvalidOperationException("요리 대조 실패: " + cooked.FailReason);
                if (Math.Abs(world.SkillsOf(cookBody).Get(SkillId.Camping)) > 0.0001f)
                    throw new InvalidOperationException("요리는 야영을 올리면 안 됩니다.");
                if (cookBody.IsCampSafe(Time.time))
                    throw new InvalidOperationException("요리는 CampSafeUntil을 켜면 안 됩니다.");

                var hide = world.TryHide(body);
                if (!hide.Applied)
                    throw new InvalidOperationException("은신 대조 실패: " + hide.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Camping) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("은신은 야영을 올리면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (kindleGo != null)
                    UnityEngine.Object.DestroyImmediate(kindleGo);
                if (cookGo != null)
                    UnityEngine.Object.DestroyImmediate(cookGo);
                if (stGo != null)
                    UnityEngine.Object.DestroyImmediate(stGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertStealing()
        {
            AssertDungeon3Leftover();
            if (SkillId.Stealing == SkillId.Lockpicking)
                throw new InvalidOperationException("훔치기 SkillId는 자물쇠따기와 달라야 합니다.");
            if (SkillId.Stealing == SkillId.Camping)
                throw new InvalidOperationException("훔치기 SkillId는 야영과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Stealing) != StatId.Dex)
                throw new InvalidOperationException("훔치기 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Stealing) != "훔치기" || SkillTitles.JobOf(SkillId.Stealing) != "도둑")
                throw new InvalidOperationException("훔치기 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Lockpicking) != "자물쇠따기" || SkillTitles.JobOf(SkillId.Lockpicking) != "자물쇠공")
                throw new InvalidOperationException("자물쇠따기 스킬명/직업명을 바꾸면 안 됩니다.");
            if (SkillNames.KoreanOf(SkillId.Camping) != "야영" || SkillTitles.JobOf(SkillId.Camping) != "야영꾼")
                throw new InvalidOperationException("야영 스킬명/직업명을 바꾸면 안 됩니다.");
            if (StealingResolve.LowestLoot(1, 1) != "gold")
                throw new InvalidOperationException("훔치기는 최저가 골드 1을 먼저 집어야 합니다.");
            if (StealingResolve.LowestLoot(0, 2) != ItemCatalog.Cloth)
                throw new InvalidOperationException("골드가 없으면 천 1을 집어야 합니다.");

            var ghostSkills = new SkillSet();
            var ghost = StealingResolve.Resolve(new StealingRequest { Now = 1f, Skills = ghostSkills, Ghost = true, HasPack = true, PackGold = 1 });
            if (ghost.Applied)
                throw new InvalidOperationException("유령 훔치기는 실패해야 합니다.");
            if (Math.Abs(ghostSkills.Get(SkillId.Stealing)) > 0.0001f)
                throw new InvalidOperationException("실패한 훔치기는 스킬을 올리면 안 됩니다.");

            var noneSkills = new SkillSet();
            var none = StealingResolve.Resolve(new StealingRequest { Now = 1f, Skills = noneSkills, HasPack = false, PackGold = 1 });
            if (none.Applied || none.FailReason != "no_pack")
                throw new InvalidOperationException("팩 없는 훔치기는 실패해야 합니다(플레이어 가방 아님).");
            if (Math.Abs(noneSkills.Get(SkillId.Stealing)) > 0.0001f)
                throw new InvalidOperationException("팩 없는 훔치기는 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = StealingResolve.Resolve(new StealingRequest
            {
                Now = 1f,
                Skills = farSkills,
                HasPack = true,
                PackGold = 1,
                Distance = StealingResolve.StealRange + 1f,
                Range = StealingResolve.StealRange
            });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("사거리 밖 훔치기는 실패해야 합니다.");
            if (Math.Abs(farSkills.Get(SkillId.Stealing)) > 0.0001f)
                throw new InvalidOperationException("사거리 밖 훔치기는 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            int intWas = stats.Int;
            int strWas = stats.Str;
            var ok = StealingResolve.Resolve(new StealingRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                HasPack = true,
                PackGold = 2,
                PackCloth = 1,
                Distance = 1f,
                Difficulty = StealingResolve.Difficulty
            });
            if (!ok.Applied || !ok.Stolen || ok.Criminal || ok.LootId != "gold")
                throw new InvalidOperationException("조용한 훔치기는 골드 1을 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Stealing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 훔치기 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Lockpicking)) > 0.0001f)
                throw new InvalidOperationException("훔치기는 자물쇠따기를 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("훔치기 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != intWas)
                throw new InvalidOperationException("훔치기는 INT를 올리면 안 됩니다.");
            if (stats.Str != strWas)
                throw new InvalidOperationException("훔치기는 STR을 올리면 안 됩니다.");

            var clothOnly = new SkillSet();
            var clothOk = StealingResolve.Resolve(new StealingRequest { Now = 1f, Skills = clothOnly, HasPack = true, PackCloth = 1, Distance = 1f });
            if (!clothOk.Applied || !clothOk.Stolen || clothOk.LootId != ItemCatalog.Cloth)
                throw new InvalidOperationException("골드 없는 팩은 천을 훔쳐야 합니다.");

            var guardSkills = new SkillSet();
            var guard = StealingResolve.Resolve(new StealingRequest { Now = 1f, Skills = guardSkills, HasPack = true, PackGold = 1, Distance = 1f, InGuardZone = true });
            if (!guard.Applied || guard.Stolen || !guard.Criminal || guard.FailReason != "guard")
                throw new InvalidOperationException("가드존 실패는 범죄이고 아이템을 주면 안 됩니다.");
            if (Math.Abs(guardSkills.Get(SkillId.Stealing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("가드존 실패 시도도 0.1이어야 합니다.");

            var witSkills = new SkillSet();
            var wit = StealingResolve.Resolve(new StealingRequest { Now = 1f, Skills = witSkills, HasPack = true, PackGold = 1, Distance = 1f, Witnessed = true });
            if (!wit.Applied || wit.Stolen || !wit.Criminal || wit.FailReason != "witness")
                throw new InvalidOperationException("목격 실패는 범죄이고 아이템을 주면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Stealing, SkillLock.Locked);
            var lockedOk = StealingResolve.Resolve(new StealingRequest { Now = 1f, Skills = locked, HasPack = true, PackGold = 1, Distance = 1f });
            if (!lockedOk.Applied || !lockedOk.Stolen)
                throw new InvalidOperationException("잠긴 훔치기도 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Stealing)) > 0.0001f)
                throw new InvalidOperationException("잠긴 훔치기는 오르면 안 됩니다.");

            var pickSkills = new SkillSet();
            LockpickingResolve.Resolve(new LockpickingRequest { Now = 1f, Skills = pickSkills, HasCrate = true, HasLockpick = true });
            if (Math.Abs(pickSkills.Get(SkillId.Stealing)) > 0.0001f)
                throw new InvalidOperationException("자물쇠따기는 훔치기를 올리면 안 됩니다.");

            var created = CharacterCreate.Build("steal-check", "도둑", 0, 20, 40, 20,
                new[] { SkillId.Stealing, SkillId.Lockpicking, SkillId.Camping },
                new[] { 50f, 30f, 20f });
            bool hasPick = false, hasLute = false, hasWood = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Lockpick)
                    hasPick = true;
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
                if (created.Inventory[i].TemplateId == "wood")
                    hasWood = true;
            }
            if (!hasPick)
                throw new InvalidOperationException("자물쇠따기 시작은 자물쇠를 줘야 합니다.");
            if (hasLute)
                throw new InvalidOperationException("훔치기 시작은 류트를 주면 안 됩니다.");
            if (!hasWood)
                throw new InvalidOperationException("야영 시작은 나무를 줘야 합니다.");

            var village = GameObject.Find("LockedCrate");
            if (village == null || village.GetComponent<LockedCrate>() == null)
                throw new InvalidOperationException("마을 Kenney 상자(LockedCrate)를 훔치기 팩으로 재사용해야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();
            var go = new GameObject("selfcheck-steal");
            GameObject worldGo = null;
            GameObject packGo = null;
            GameObject guardGo = null;
            GameObject guardPackGo = null;
            GameObject witGo = null;
            GameObject witPackGo = null;
            GameObject otherGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-steal-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                world.ResetHousePlot();
                go.transform.position = new Vector3(40f, 0f, 0f);
                if (GuardZone.Contains(go.transform.position.x, go.transform.position.z))
                    throw new InvalidOperationException("성공 훔치기 더미는 GuardZone 밖이어야 합니다.");
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();
                body.Gold = 0;
                var bag = go.AddComponent<InventoryBag>();

                packGo = new GameObject("selfcheck-steal-pack");
                packGo.transform.position = go.transform.position;
                var pack = packGo.AddComponent<LockedCrate>();
                pack.GoldLoot = 2;
                pack.ClothLoot = 1;
                pack.Opened = false;

                otherGo = new GameObject("selfcheck-steal-other");
                otherGo.transform.position = go.transform.position + new Vector3(30f, 0f, 0f);
                var other = otherGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.RecalcFromStr(30);
                other.ResetHp();
                var otherBag = otherGo.AddComponent<InventoryBag>();
                otherBag.Add(ItemCatalog.Cloth, 3);

                var hit = world.TrySteal(body);
                if (!hit.Applied || !hit.Stolen)
                    throw new InvalidOperationException("서버 조용 훔치기 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Stealing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 훔치기 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Lockpicking)) > 0.0001f)
                    throw new InvalidOperationException("서버 훔치기는 자물쇠따기를 올리면 안 됩니다.");
                if (body.Gold != 1 || pack.GoldLoot != 1 || pack.ClothLoot != 1)
                    throw new InvalidOperationException("성공 훔치기는 최저가 골드 1만 가져야 합니다.");
                if (pack.Opened)
                    throw new InvalidOperationException("훔치기는 상자를 열면 안 됩니다.");
                if (body.Notoriety == NotorietyId.Criminal)
                    throw new InvalidOperationException("조용한 성공은 범죄가 아니어야 합니다.");
                int otherCloth = 0;
                for (int i = 0; i < otherBag.Items.Count; i++)
                    if (otherBag.Items[i].TemplateId == ItemCatalog.Cloth)
                        otherCloth += otherBag.Items[i].Amount;
                if (otherCloth != 3)
                    throw new InvalidOperationException("다른 플레이어 가방을 건드리면 안 됩니다.");
                if (string.IsNullOrEmpty(world.LastStealMessage) || world.LastStealMessage.IndexOf("훔", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("훔치기 메시지가 있어야 합니다.");

                guardGo = new GameObject("selfcheck-steal-guard");
                guardGo.transform.position = Vector3.zero;
                if (!GuardZone.Contains(0f, 0f))
                    throw new InvalidOperationException("가드존 실패 더미는 GuardZone 안이어야 합니다.");
                var guardBody = guardGo.AddComponent<WorldBody>();
                guardBody.IsAvatar = true;
                guardBody.RecalcFromStr(30);
                guardBody.ResetHp();
                guardBody.Gold = 0;
                guardGo.AddComponent<InventoryBag>();
                guardPackGo = new GameObject("selfcheck-steal-guard-pack");
                guardPackGo.transform.position = Vector3.zero;
                var guardPack = guardPackGo.AddComponent<LockedCrate>();
                guardPack.GoldLoot = 1;
                guardPack.ClothLoot = 0;
                var guardHit = world.TrySteal(guardBody);
                if (!guardHit.Applied || guardHit.Stolen || !guardHit.Criminal)
                    throw new InvalidOperationException("서버 가드존 실패는 범죄여야 합니다: " + guardHit.FailReason);
                if (guardBody.Notoriety != NotorietyId.Criminal)
                    throw new InvalidOperationException("가드존 실패는 FlagCriminal이어야 합니다.");
                if (guardBody.Gold != 0 || guardPack.GoldLoot != 1)
                    throw new InvalidOperationException("가드존 실패는 아이템을 주면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(guardBody).Get(SkillId.Stealing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("가드존 실패 시도도 서버 스킬 0.1이어야 합니다.");

                witGo = new GameObject("selfcheck-steal-wit");
                witGo.transform.position = new Vector3(50f, 0f, 0f);
                var witBody = witGo.AddComponent<WorldBody>();
                witBody.IsAvatar = true;
                witBody.RecalcFromStr(30);
                witBody.ResetHp();
                witBody.Gold = 0;
                witGo.AddComponent<InventoryBag>();
                witPackGo = new GameObject("selfcheck-steal-wit-pack");
                witPackGo.transform.position = witGo.transform.position;
                var witPack = witPackGo.AddComponent<LockedCrate>();
                witPack.GoldLoot = 1;
                other.transform.position = witGo.transform.position;
                var witHit = world.TrySteal(witBody);
                if (!witHit.Applied || witHit.Stolen || !witHit.Criminal)
                    throw new InvalidOperationException("서버 목격 실패는 범죄여야 합니다: " + witHit.FailReason);
                if (witBody.Notoriety != NotorietyId.Criminal)
                    throw new InvalidOperationException("목격 실패는 FlagCriminal이어야 합니다.");
                if (witBody.Gold != 0 || witPack.GoldLoot != 1)
                    throw new InvalidOperationException("목격 실패는 아이템을 주면 안 됩니다.");
            }
            finally
            {
                OfflineWorld.Instance?.ResetHousePlot();
                UnityEngine.Object.DestroyImmediate(go);
                if (packGo != null)
                    UnityEngine.Object.DestroyImmediate(packGo);
                if (guardGo != null)
                    UnityEngine.Object.DestroyImmediate(guardGo);
                if (guardPackGo != null)
                    UnityEngine.Object.DestroyImmediate(guardPackGo);
                if (witGo != null)
                    UnityEngine.Object.DestroyImmediate(witGo);
                if (witPackGo != null)
                    UnityEngine.Object.DestroyImmediate(witPackGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertLockpickingSlice()
        {
            if (SkillId.Lockpicking == SkillId.Stealth)
                throw new InvalidOperationException("자물쇠따기 SkillId는 잠행과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Lockpicking) != StatId.Dex)
                throw new InvalidOperationException("자물쇠따기 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Lockpicking) != "자물쇠따기" || SkillTitles.JobOf(SkillId.Lockpicking) != "자물쇠공")
                throw new InvalidOperationException("자물쇠따기 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Stealth) != "잠행")
                throw new InvalidOperationException("잠행 스킬명을 바꾸면 안 됩니다.");

            var ghostSkills = new SkillSet();
            var ghost = LockpickingResolve.Resolve(new LockpickingRequest { Now = 1f, Skills = ghostSkills, Ghost = true, HasCrate = true, HasLockpick = true });
            if (ghost.Applied)
                throw new InvalidOperationException("유령 자물쇠따기는 실패해야 합니다.");
            if (Math.Abs(ghostSkills.Get(SkillId.Lockpicking)) > 0.0001f)
                throw new InvalidOperationException("실패한 자물쇠따기는 스킬을 올리면 안 됩니다.");

            var noCrate = new SkillSet();
            var missCrate = LockpickingResolve.Resolve(new LockpickingRequest { Now = 1f, Skills = noCrate, HasCrate = false, HasLockpick = true });
            if (missCrate.Applied || missCrate.FailReason != "no_crate")
                throw new InvalidOperationException("상자 없는 자물쇠따기는 실패해야 합니다(플레이어 훔치기 아님).");
            if (Math.Abs(noCrate.Get(SkillId.Lockpicking)) > 0.0001f)
                throw new InvalidOperationException("상자 없는 자물쇠따기는 스킬을 올리면 안 됩니다.");

            var noPick = new SkillSet();
            var missPick = LockpickingResolve.Resolve(new LockpickingRequest { Now = 1f, Skills = noPick, HasCrate = true, HasLockpick = false });
            if (missPick.Applied)
                throw new InvalidOperationException("자물쇠 없는 따기는 실패해야 합니다.");

            var opened = new SkillSet();
            var missOpen = LockpickingResolve.Resolve(new LockpickingRequest { Now = 1f, Skills = opened, HasCrate = true, CrateOpened = true, HasLockpick = true });
            if (missOpen.Applied)
                throw new InvalidOperationException("이미 연 상자는 다시 따면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dex0 = stats.Dex;
            var ok = LockpickingResolve.Resolve(new LockpickingRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                HasCrate = true,
                HasLockpick = true,
                Difficulty = LockpickingResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("자물쇠따기는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Lockpicking) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 자물쇠따기 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Stealth)) > 0.0001f)
                throw new InvalidOperationException("자물쇠따기는 잠행을 올리면 안 됩니다.");
            if (stats.Dex <= dex0)
                throw new InvalidOperationException("자물쇠따기 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != StatSet.DefaultInt)
                throw new InvalidOperationException("자물쇠따기는 INT를 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Lockpicking, SkillLock.Locked);
            var lockedOk = LockpickingResolve.Resolve(new LockpickingRequest { Now = 1f, Skills = locked, HasCrate = true, HasLockpick = true });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 자물쇠따기도 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Lockpicking)) > 0.0001f)
                throw new InvalidOperationException("잠긴 자물쇠따기는 오르면 안 됩니다.");

            var created = CharacterCreate.Build("pick-check", "자물쇠공", 0, 20, 40, 20,
                new[] { SkillId.Lockpicking, SkillId.Tactics, SkillId.Anatomy },
                new[] { 50f, 30f, 20f });
            bool hasPick = false;
            bool hasLute = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Lockpick)
                    hasPick = true;
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
            }
            if (!hasPick)
                throw new InvalidOperationException("자물쇠따기 시작은 자물쇠를 줘야 합니다.");
            if (hasLute)
                throw new InvalidOperationException("자물쇠따기 시작은 류트를 주면 안 됩니다.");
            if (ItemCatalog.BuyPrice(ItemCatalog.Lockpick) <= 0)
                throw new InvalidOperationException("잡화가 자물쇠를 팔아야 합니다.");
            var recipe = CraftRecipes.Find("lockpick");
            if (recipe == null || recipe.Output != ItemCatalog.Lockpick || recipe.Ingredient != "iron_ore" || recipe.Count != 1 || recipe.Skill != SkillId.Blacksmithing)
                throw new InvalidOperationException("자물쇠는 철광석 1로 대장간 제작이어야 합니다.");

            var village = GameObject.Find("LockedCrate");
            if (village == null)
                throw new InvalidOperationException("마을에 Kenney 잠긴 상자(LockedCrate)가 있어야 합니다.");
            var villageCrate = village.GetComponent<LockedCrate>();
            if (villageCrate == null)
                throw new InvalidOperationException("LockedCrate 컴포넌트가 있어야 합니다.");
            if (!GuardZone.Contains(village.transform.position.x, village.transform.position.z))
                throw new InvalidOperationException("잠긴 상자는 마을 가드존 안이어야 합니다.");

            var go = new GameObject("selfcheck-pick");
            GameObject worldGo = null;
            GameObject crateGo = null;
            GameObject forgeGo = null;
            GameObject otherGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-pick-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();
                body.Gold = 0;
                var bag = go.AddComponent<InventoryBag>();

                crateGo = new GameObject("selfcheck-crate");
                crateGo.transform.position = go.transform.position;
                var crate = crateGo.AddComponent<LockedCrate>();
                crate.DisplayName = "잠긴 상자";
                crate.GoldLoot = 8;
                crate.ClothLoot = 1;

                otherGo = new GameObject("selfcheck-other");
                otherGo.transform.position = go.transform.position;
                var other = otherGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                var otherBag = otherGo.AddComponent<InventoryBag>();
                otherBag.Add(ItemCatalog.Cloth, 3);

                var theft = world.TryPick(body, null);
                if (theft.Applied)
                    throw new InvalidOperationException("플레이어 대상 자물쇠따기는 없어야 합니다.");

                var miss = world.TryPick(body, crate);
                if (miss.Applied)
                    throw new InvalidOperationException("서버 자물쇠 없는 따기는 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Lockpicking)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 따기는 스킬을 올리면 안 됩니다.");

                forgeGo = new GameObject("selfcheck-pick-forge");
                forgeGo.transform.position = go.transform.position;
                var station = forgeGo.AddComponent<CraftStation>();
                station.RecipeId = "iron_sword";
                station.DisplayName = "대장간";
                bag.Add("iron_ore", 1);
                var crafted = world.TryCraft(body, station, "lockpick");
                if (!crafted.Applied)
                    throw new InvalidOperationException("대장간 자물쇠 제작 실패: " + crafted.FailReason);
                if (!ItemCatalog.Has(bag.Items, ItemCatalog.Lockpick))
                    throw new InvalidOperationException("철광석 1 → 자물쇠 1이어야 합니다.");

                int otherCloth = 0;
                for (int i = 0; i < otherBag.Items.Count; i++)
                    if (otherBag.Items[i].TemplateId == ItemCatalog.Cloth)
                        otherCloth += otherBag.Items[i].Amount;
                var hit = world.TryPick(body, crate);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 자물쇠따기 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Lockpicking) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 따기 후 서버 스킬 0.1이어야 합니다.");
                if (!crate.Opened)
                    throw new InvalidOperationException("성공하면 상자가 열려야 합니다.");
                if (body.Gold != 8)
                    throw new InvalidOperationException("열린 상자 골드 보상이 있어야 합니다.");
                if (!ItemCatalog.Has(bag.Items, ItemCatalog.Cloth))
                    throw new InvalidOperationException("열린 상자 천 보상이 있어야 합니다.");
                if (ItemCatalog.Has(bag.Items, ItemCatalog.Lockpick))
                    throw new InvalidOperationException("성공 따기는 자물쇠를 소모해야 합니다.");
                int otherClothAfter = 0;
                for (int i = 0; i < otherBag.Items.Count; i++)
                    if (otherBag.Items[i].TemplateId == ItemCatalog.Cloth)
                        otherClothAfter += otherBag.Items[i].Amount;
                if (otherClothAfter != otherCloth)
                    throw new InvalidOperationException("다른 플레이어 가방을 건드리면 안 됩니다.");
                if (string.IsNullOrEmpty(world.LastPickMessage) || world.LastPickMessage.IndexOf("열림", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("따기 메시지가 있어야 합니다.");

                bag.Add(ItemCatalog.Lockpick, 1);
                var again = world.TryPick(body, crate);
                if (again.Applied)
                    throw new InvalidOperationException("한 번 연 상자는 다시 열리면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (crateGo != null)
                    UnityEngine.Object.DestroyImmediate(crateGo);
                if (forgeGo != null)
                    UnityEngine.Object.DestroyImmediate(forgeGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }
    }
}
