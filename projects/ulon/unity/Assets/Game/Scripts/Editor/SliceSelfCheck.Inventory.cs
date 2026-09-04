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
        static void AssertControlSlots()
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

            if (TameResolve.FollowerCap != 2)
                throw new InvalidOperationException("MaxControlSlots/FollowerCap은 2여야 합니다.");
            if (TameCritter.ControlSlots != 1 || TameBoar.ControlSlots != 1)
                throw new InvalidOperationException("하트/멧돼지 ControlCost는 1이어야 합니다.");
            if (!MobCatalog.TamableOf(TameCritter.Id) || !MobCatalog.TamableOf(TameBoar.Id))
                throw new InvalidOperationException("하트/멧돼지는 조련 가능해야 합니다.");

            var twoOk = TameResolve.Tame(new TameRequest { Skills = new SkillSet(), Tameable = true, UsedSlots = 1, ControlSlots = 1, FollowerCap = 2, Distance = 1f });
            if (!twoOk.Applied)
                throw new InvalidOperationException("cap2에서 used1+cost1은 성공해야 합니다.");
            var threeFail = TameResolve.Tame(new TameRequest { Skills = new SkillSet(), Tameable = true, UsedSlots = 2, ControlSlots = 1, FollowerCap = 2, Distance = 1f });
            if (threeFail.Applied || threeFail.FailReason != "no_slot")
                throw new InvalidOperationException("cap2에서 used2+cost1은 no_slot이어야 합니다.");
            var costly = TameResolve.Tame(new TameRequest { Skills = new SkillSet(), Tameable = true, UsedSlots = 1, ControlSlots = 2, FollowerCap = 2, Distance = 1f });
            if (costly.Applied || costly.FailReason != "no_slot")
                throw new InvalidOperationException("used1+cost2는 no_slot이어야 합니다.");

            var hartGo = GameObject.Find(TameCritter.Object);
            var hart = hartGo != null ? hartGo.GetComponent<WorldBody>() : null;
            if (hart == null || hart.ControlSlots != 1 || !hart.Tameable)
                throw new InvalidOperationException("씬 야생하트가 ControlSlots=1이어야 합니다.");
            var boarGo = GameObject.Find(TameBoar.Object);
            var boar = boarGo != null ? boarGo.GetComponent<WorldBody>() : null;
            if (boar == null || boar.ControlSlots != 1 || !boar.Tameable || boar.IsEnemy)
                throw new InvalidOperationException("씬 야생멧돼지가 있어야 합니다.");
            if (boar.MobId != TameBoar.Id || boar.DisplayName != TameBoar.DisplayName)
                throw new InvalidOperationException("멧돼지 카탈로그가 야생멧돼지여야 합니다.");
            Vector3 bpos = boarGo.transform.position;
            if (Math.Abs(bpos.x - TameBoar.X) > 0.4f || Math.Abs(bpos.z - TameBoar.Z) > 0.4f)
                throw new InvalidOperationException("멧돼지 좌표가 지정 위치와 같아야 합니다.");
            if (GuardZone.Contains(bpos.x, bpos.z))
                throw new InvalidOperationException("멧돼지는 GuardZone 밖이어야 합니다.");
            float[] lxs = { HousingPlot.X, TameCritter.X, 18.2f, 3.4f, -12.2f, Dungeon1.EntranceX, Dungeon2.EntranceX, 0f, FieldBoss.X, TravelGate.X, StableYard.X };
            float[] lzs = { HousingPlot.Z, TameCritter.Z, 2.4f, -19.6f, 20.4f, Dungeon1.EntranceZ, Dungeon2.EntranceZ, 13.2f, FieldBoss.Z, TravelGate.Z, StableYard.Z };
            for (int i = 0; i < lxs.Length; i++)
            {
                float dx = bpos.x - lxs[i];
                float dz = bpos.z - lzs[i];
                if ((dx * dx) + (dz * dz) < 9f)
                    throw new InvalidOperationException("멧돼지가 기존 랜드마크/하트와 너무 가까우면 안 됩니다.");
            }
            string bpath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(boarGo);
            if (string.IsNullOrEmpty(bpath) || bpath.IndexOf("/RAW/", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("멧돼지는 Prefab이어야 합니다(RAW fbx 아님).");
            string hpath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hartGo);
            if (string.IsNullOrEmpty(hpath) || hpath.IndexOf("/RAW/", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("하트는 Prefab이어야 합니다(RAW fbx 아님).");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-slots-world");
            GameObject ownerGo = null;
            GameObject thirdGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                hart.OwnerCharacterId = "";
                hart.PetFollow = false;
                hart.PetStabled = false;
                hart.PetGuard = false;
                hart.PetAttackTarget = null;
                if (!hartGo.activeSelf)
                    hartGo.SetActive(true);
                boar.OwnerCharacterId = "";
                boar.PetFollow = false;
                boar.PetStabled = false;
                boar.PetGuard = false;
                boar.PetAttackTarget = null;
                if (!boarGo.activeSelf)
                    boarGo.SetActive(true);

                ownerGo = new GameObject("selfcheck-slots-owner");
                ownerGo.transform.position = hartGo.transform.position;
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "slots-owner";
                owner.Gold = StableYard.GoldCost * 2;
                owner.ResetHp();

                var first = world.TryTame(owner, hart);
                if (!first.Applied)
                    throw new InvalidOperationException("첫 하트 조련 실패: " + first.FailReason);
                if (world.CountFollowers(owner.CharacterId) != 1)
                    throw new InvalidOperationException("하트 조련 후 used slots=1이어야 합니다.");

                ownerGo.transform.position = boarGo.transform.position;
                var second = world.TryTame(owner, boar);
                if (!second.Applied)
                    throw new InvalidOperationException("두 번째 멧돼지 조련 실패: " + second.FailReason);
                if (world.CountFollowers(owner.CharacterId) != 2)
                    throw new InvalidOperationException("두 마리 조련 후 used slots=2이어야 합니다.");

                thirdGo = new GameObject("selfcheck-slots-third");
                thirdGo.transform.position = ownerGo.transform.position;
                var third = thirdGo.AddComponent<WorldBody>();
                third.IsEnemy = false;
                third.IsAvatar = false;
                third.Tameable = true;
                third.MobId = TameBoar.Id;
                third.DisplayName = "여분";
                third.ControlSlots = 1;
                third.ResetHp();
                var over = world.TryTame(owner, third);
                if (over.Applied || over.FailReason != "no_slot")
                    throw new InvalidOperationException("세 번째 조련은 no_slot이어야 합니다: " + over.FailReason);

                var rel = world.TryPetRelease(owner, boar);
                if (!rel.Applied)
                    throw new InvalidOperationException("release는 성공해야 합니다: " + rel.FailReason);
                if (world.CountFollowers(owner.CharacterId) != 1)
                    throw new InvalidOperationException("release 후 used slots=1이어야 합니다.");
                ownerGo.transform.position = boarGo.transform.position;
                var retame = world.TryTame(owner, boar);
                if (!retame.Applied)
                    throw new InvalidOperationException("release 후 재조련 실패: " + retame.FailReason);
                if (world.CountFollowers(owner.CharacterId) != 2)
                    throw new InvalidOperationException("재조련 후 used slots=2이어야 합니다.");

                var stable = GameObject.Find(StableYard.Object)?.GetComponent<StableMaster>();
                if (stable == null)
                    throw new InvalidOperationException("StableMaster가 있어야 합니다.");
                ownerGo.transform.position = GameObject.Find(StableYard.Object).transform.position;
                owner.Gold = StableYard.GoldCost;
                int beforeStable = world.CountFollowers(owner.CharacterId);
                var parked = world.TryStable(owner, stable);
                if (!parked.Applied)
                    throw new InvalidOperationException("마구간 맡김 실패: " + parked.FailReason);
                if (world.CountFollowers(owner.CharacterId) != beforeStable - 1)
                    throw new InvalidOperationException("stable은 슬롯을 해제해야 합니다.");
                third.OwnerCharacterId = "";
                thirdGo.transform.position = ownerGo.transform.position;
                var afterStable = world.TryTame(owner, third);
                if (!afterStable.Applied)
                    throw new InvalidOperationException("stable 후 여유 슬롯 조련 실패: " + afterStable.FailReason);

                world.TryPetRelease(owner, third);
                world.TryPetRelease(owner, hart);
                world.TryPetRelease(owner, boar);
                world.ClearStabled(owner.CharacterId);
                world.ResetHousePlot();
            }
            finally
            {
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (thirdGo != null)
                    UnityEngine.Object.DestroyImmediate(thirdGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                if (hartGo != null)
                {
                    if (!hartGo.activeSelf)
                        hartGo.SetActive(true);
                    hart.OwnerCharacterId = "";
                    hart.PetFollow = false;
                    hart.PetStabled = false;
                    hart.PetGuard = false;
                    hart.PetAttackTarget = null;
                    hartGo.transform.SetPositionAndRotation(new Vector3(TameCritter.X, hartGo.transform.position.y, TameCritter.Z), hartGo.transform.rotation);
                }
                if (boarGo != null)
                {
                    if (!boarGo.activeSelf)
                        boarGo.SetActive(true);
                    boar.OwnerCharacterId = "";
                    boar.PetFollow = false;
                    boar.PetStabled = false;
                    boar.PetGuard = false;
                    boar.PetAttackTarget = null;
                    boarGo.transform.SetPositionAndRotation(new Vector3(TameBoar.X, boarGo.transform.position.y, TameBoar.Z), boarGo.transform.rotation);
                }
                OfflineWorld.Instance?.ClearStabled("slots-owner");
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }

        static void AssertNestedBag()
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
            if (GameObject.Find("DressVillage") != null)
                throw new InvalidOperationException("DressVillage 오브젝트가 있으면 안 됩니다.");

            if (!ItemCatalog.IsContainer(ItemCatalog.Pouch))
                throw new InvalidOperationException("pouch는 컨테이너여야 합니다.");
            if (ItemCatalog.Stackable(ItemCatalog.Pouch))
                throw new InvalidOperationException("pouch는 스택되면 안 됩니다.");
            if (ItemCatalog.WeightOf(ItemCatalog.Pouch) != 2f)
                throw new InvalidOperationException("pouch 무게는 2여야 합니다.");
            if (ItemCatalog.WeightOf(ItemCatalog.Cloth) != 0.5f)
                throw new InvalidOperationException("cloth 무게는 0.5여야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-nested-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                bodyGo = new GameObject("selfcheck-nested-body");
                bodyGo.transform.position = new Vector3(4f, 0f, 4f);
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "nested-body";
                body.RecalcFromStr(20);
                body.ResetHp();
                var bag = bodyGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Pouch, 1);
                bag.Add(ItemCatalog.Cloth, 1);
                world.StatsOf(body).ForceSet(20, 25, 25);

                string pouchId = bag.PouchInstanceId();
                if (string.IsNullOrEmpty(pouchId))
                    throw new InvalidOperationException("pouch InstanceId가 있어야 합니다.");

                float w0 = bag.TotalWeight();
                if (Math.Abs(w0 - (ItemCatalog.WeightOf(ItemCatalog.Pouch) + ItemCatalog.WeightOf(ItemCatalog.Cloth))) > 0.0001f)
                    throw new InvalidOperationException("초기 가방 무게가 pouch+cloth여야 합니다: " + w0);

                var badDepth = world.TryMoveToPouch(body, ItemCatalog.Pouch, pouchId);
                if (badDepth.Applied || badDepth.FailReason != "nested_depth")
                    throw new InvalidOperationException("파우치 속 파우치는 nested_depth 실패여야 합니다: " + badDepth.FailReason);

                var moved = world.TryMoveToPouch(body, ItemCatalog.Cloth, pouchId);
                if (!moved.Applied)
                    throw new InvalidOperationException("TryMoveToPouch 실패: " + moved.FailReason);
                if (bag.CountInPouch(ItemCatalog.Cloth, pouchId) != 1)
                    throw new InvalidOperationException("천이 파우치 안에 있어야 합니다.");
                bool clothNested = false;
                bool clothBackpack = false;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    var it = bag.Items[i];
                    if (it.TemplateId != ItemCatalog.Cloth)
                        continue;
                    if ((it.ParentContainerId ?? "") == pouchId)
                        clothNested = true;
                    if (string.IsNullOrEmpty(it.ParentContainerId))
                        clothBackpack = true;
                }
                if (!clothNested || clothBackpack)
                    throw new InvalidOperationException("천 ParentContainerId가 pouch InstanceId여야 합니다.");
                if (Math.Abs(bag.TotalWeight() - w0) > 0.0001f)
                    throw new InvalidOperationException("파우치 안 천 무게도 Carry에 합산되어야 합니다.");

                var taken = world.TryTakeFromPouch(body, ItemCatalog.Cloth, pouchId);
                if (!taken.Applied)
                    throw new InvalidOperationException("TryTakeFromPouch 실패: " + taken.FailReason);
                if (bag.CountInPouch(ItemCatalog.Cloth, pouchId) != 0)
                    throw new InvalidOperationException("꺼낸 뒤 파우치에 천이 없어야 합니다.");
                clothNested = false;
                clothBackpack = false;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    var it = bag.Items[i];
                    if (it.TemplateId != ItemCatalog.Cloth)
                        continue;
                    if ((it.ParentContainerId ?? "") == pouchId)
                        clothNested = true;
                    if (string.IsNullOrEmpty(it.ParentContainerId))
                        clothBackpack = true;
                }
                if (clothNested || !clothBackpack)
                    throw new InvalidOperationException("꺼낸 천은 백팩(Parent 없음)이어야 합니다.");
                if (Math.Abs(bag.TotalWeight() - w0) > 0.0001f)
                    throw new InvalidOperationException("꺼낸 뒤 무게가 유지되어야 합니다.");

                if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                    throw new InvalidOperationException("NestedBag 슬라이스 후 던전3가 생기면 안 됩니다.");
                world.ResetHousePlot();
            }
            finally
            {
                OfflineWorld.Instance?.ResetHousePlot();
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertGroundDecay()
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
            if (GameObject.Find("DressVillage") != null)
                throw new InvalidOperationException("DressVillage 오브젝트가 있으면 안 됩니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            // Clear leftover ground from prior runs.
            var stale = UnityEngine.Object.FindObjectsByType<GroundItem>(FindObjectsSortMode.None);
            for (int i = 0; i < stale.Length; i++)
            {
                if (stale[i] != null)
                    UnityEngine.Object.DestroyImmediate(stale[i].gameObject);
            }

            var worldGo = new GameObject("selfcheck-ground-world");
            GameObject ownerGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                if (Math.Abs(OfflineWorld.GroundDecaySeconds - 30f) > 0.0001f)
                    throw new InvalidOperationException("기본 GroundDecaySeconds는 30이어야 합니다.");

                var cloth = new ItemRecord { TemplateId = ItemCatalog.Cloth, Amount = 1 };
                var ground = world.SpawnGroundItem(cloth, new Vector3(1f, 0f, 1f), 0.1f);
                if (ground == null || string.IsNullOrEmpty(ground.GroundId))
                    throw new InvalidOperationException("SpawnGroundItem이 GroundItem을 만들어야 합니다.");
                if (OfflineWorld.CountGroundItems(ItemCatalog.Cloth) < 1)
                    throw new InvalidOperationException("스폰 직후 월드 천 GroundItem이 있어야 합니다.");
                if (ground.DecayAt <= Time.time)
                    throw new InvalidOperationException("DecayAt은 now+초여야 합니다(만료 전).");

                // Force expiry like corpse decay assert.
                ground.DecayAt = -9999f;
                world.TickGroundItems(0f);
                if (OfflineWorld.CountGroundItems(ItemCatalog.Cloth) != 0)
                    throw new InvalidOperationException("만료 GroundItem은 TickGroundItems 후 삭제되어야 합니다.");
                if (GameObject.Find("GroundItem") != null)
                {
                    var leftoverGo = GameObject.Find("GroundItem");
                    if (leftoverGo != null && leftoverGo.GetComponent<GroundItem>() != null)
                        throw new InvalidOperationException("만료 GroundItem GameObject가 남아 있으면 안 됩니다.");
                }

                // House lockdown / secure must NOT decay with ground tick.
                var station = GameObject.Find(HousingPlot.StationObject);
                var chestGo = GameObject.Find(HousingPlot.ChestObject);
                if (station == null || chestGo == null)
                    throw new InvalidOperationException("HousingPlot station/chest가 있어야 합니다.");
                ownerGo = new GameObject("selfcheck-ground-owner");
                ownerGo.transform.position = station.transform.position;
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "ground-owner";
                owner.AccountId = "ground-acc";
                owner.Gold = HousingPlot.ClaimGold;
                owner.ResetHp();
                var bag = ownerGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Cloth, 1);
                var claim = world.TryClaimHouse(owner, station.GetComponent<HousePlotStation>());
                if (!claim.Applied)
                    throw new InvalidOperationException("AssertGroundDecay claim 실패: " + claim.FailReason);
                ownerGo.transform.position = chestGo.transform.position;
                var locked = world.TryLockdown(owner, chestGo.GetComponent<HouseChest>(), ItemCatalog.Cloth);
                if (!locked.Applied)
                    throw new InvalidOperationException("AssertGroundDecay lockdown 실패: " + locked.FailReason);
                if (world.CountHouseSecureItems() < 1)
                    throw new InvalidOperationException("lockdown 후 secure 아이템이 있어야 합니다.");

                // Spawn another short-lived ground drop; lockdown must survive its tick.
                var ground2 = world.SpawnGroundItem(ItemCatalog.Cloth, new Vector3(2f, 0f, 2f), 1, 0.1f);
                ground2.DecayAt = -1f;
                world.TickGroundItems(Time.time + 9999f);
                if (OfflineWorld.CountGroundItems() != 0)
                    throw new InvalidOperationException("강제 만료 후 GroundItem이 없어야 합니다.");
                if (world.CountHouseSecureItems() < 1)
                    throw new InvalidOperationException("집 Lockdown/secure 아이템은 GroundDecay에 삭제되면 안 됩니다.");
                var take = world.TrySecureTake(owner, chestGo.GetComponent<HouseChest>());
                if (!take.Applied)
                    throw new InvalidOperationException("Tick 후에도 secure take가 되어야 합니다: " + take.FailReason);

                if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                    throw new InvalidOperationException("GroundDecay 슬라이스 후 던전3가 생기면 안 됩니다.");
                world.ResetHousePlot();
            }
            finally
            {
                OfflineWorld.Instance?.ResetHousePlot();
                var leftovers = UnityEngine.Object.FindObjectsByType<GroundItem>(FindObjectsSortMode.None);
                for (int i = 0; i < leftovers.Length; i++)
                {
                    if (leftovers[i] != null)
                        UnityEngine.Object.DestroyImmediate(leftovers[i].gameObject);
                }
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertStableSlice()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if (StableYard.GoldCost != 2)
                throw new InvalidOperationException("마구간 골드 비용은 2여야 합니다.");
            if (StableResolve.Park(new StableRequest { HasFollower = true, Gold = 2 }).Applied == false)
                throw new InvalidOperationException("기본 Park는 성공해야 합니다.");

            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            var go = GameObject.Find(StableYard.Object);
            var stable = go != null ? go.GetComponent<StableMaster>() : null;
            if (stable == null)
                throw new InvalidOperationException("마을 Stable이 있어야 합니다.");
            Vector3 pos = go.transform.position;
            if (Math.Abs(pos.x - StableYard.X) > 0.4f || Math.Abs(pos.z - StableYard.Z) > 0.4f)
                throw new InvalidOperationException("Stable 좌표가 지정 위치와 같아야 합니다.");
            if (!GuardZone.Contains(pos.x, pos.z))
                throw new InvalidOperationException("Stable은 마을 GuardZone 안이어야 합니다.");
            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (string.IsNullOrEmpty(path) || path.IndexOf("/RAW/", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("Stable은 Prefab이어야 합니다(RAW fbx 아님).");
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int k = 1; k < rends.Length; k++)
                    b.Encapsulate(rends[k].bounds);
                if (b.min.y < 0f)
                    throw new InvalidOperationException("Stable이 땅속에 있음 minY=" + b.min.y);
            }
            float[] lxs = { HousingPlot.X, TameCritter.X, TravelGate.X, -6.8f, -5.2f, -3.6f, HousingPlot.X - 1.6f, -7.5f, 2.5f, -5.5f, 4.5f, 1.2f, -2.2f, Dungeon1.EntranceX, Dungeon2.EntranceX, FieldBoss.X };
            float[] lzs = { HousingPlot.Z, TameCritter.Z, TravelGate.Z, 3.4f, 3.4f, -3.6f, HousingPlot.Z + 1.4f, 1.2f, 1.2f, -2.1f, -2.1f, -4.8f, 4.2f, Dungeon1.EntranceZ, Dungeon2.EntranceZ, FieldBoss.Z };
            for (int i = 0; i < lxs.Length; i++)
            {
                float dx = pos.x - lxs[i];
                float dz = pos.z - lzs[i];
                if ((dx * dx) + (dz * dz) < 36f)
                    throw new InvalidOperationException("Stable이 집/랜드마크와 겹치면 안 됩니다.");
            }

            var ghost = StableResolve.Park(new StableRequest { Ghost = true, HasFollower = true, Distance = 1f, Gold = StableYard.GoldCost });
            if (ghost.Applied || ghost.FailReason != "ghost")
                throw new InvalidOperationException("유령은 마구간에 맡기면 안 됩니다.");
            var far = StableResolve.Park(new StableRequest { HasFollower = true, Distance = 20f, Gold = StableYard.GoldCost });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("너무 멀면 마구간 실패해야 합니다.");
            var none = StableResolve.Park(new StableRequest { HasFollower = false, Distance = 1f, Gold = StableYard.GoldCost });
            if (none.Applied || none.FailReason != "no_pet")
                throw new InvalidOperationException("펫 없이 맡기면 안 됩니다.");
            var poor = StableResolve.Park(new StableRequest { HasFollower = true, Distance = 1f, Gold = 0 });
            if (poor.Applied || poor.FailReason != "gold")
                throw new InvalidOperationException("골드 부족 맡김은 실패해야 합니다.");
            var empty = StableResolve.Claim(new StableRequest { HasStabled = false, Distance = 1f });
            if (empty.Applied || empty.FailReason != "empty")
                throw new InvalidOperationException("빈 마구간 찾기는 실패해야 합니다.");

            var tameGo = GameObject.Find(TameCritter.Object);
            var pet = tameGo != null ? tameGo.GetComponent<WorldBody>() : null;
            if (pet == null)
                throw new InvalidOperationException("조련 대상이 있어야 합니다.");
            var worldGo = new GameObject("selfcheck-stable-world");
            GameObject ownerGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                ownerGo = new GameObject("selfcheck-stable-owner");
                ownerGo.transform.position = tameGo.transform.position;
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "stable-owner";
                owner.Gold = StableYard.GoldCost;
                owner.ResetHp();
                pet.OwnerCharacterId = "";
                pet.PetFollow = false;
                pet.PetStabled = false;
                if (!tameGo.activeSelf)
                    tameGo.SetActive(true);

                var noPet = world.TryStable(owner, stable);
                if (noPet.Applied)
                    throw new InvalidOperationException("조련 전 맡김은 실패해야 합니다.");

                var tame = world.TryTame(owner, pet);
                if (!tame.Applied)
                    throw new InvalidOperationException("마구간 테스트 조련 실패: " + tame.FailReason);
                float tameSkill = world.SkillsOf(owner).Get(SkillId.AnimalTaming);
                if (world.CountFollowers(owner.CharacterId) < 1)
                    throw new InvalidOperationException("조련 후 팔로워 슬롯이 있어야 합니다.");

                ownerGo.transform.position = go.transform.position + new Vector3(20f, 0f, 20f);
                var rangeFail = world.TryStable(owner, stable);
                if (rangeFail.Applied || rangeFail.FailReason != "range")
                    throw new InvalidOperationException("사거리 밖 맡김은 실패해야 합니다.");

                ownerGo.transform.position = go.transform.position;
                owner.Gold = 0;
                var goldFail = world.TryStable(owner, stable);
                if (goldFail.Applied || goldFail.FailReason != "gold")
                    throw new InvalidOperationException("골드 없이 맡김은 실패해야 합니다.");

                owner.Gold = StableYard.GoldCost;
                var parked = world.TryStable(owner, stable);
                if (!parked.Applied)
                    throw new InvalidOperationException("서버 맡김 실패: " + parked.FailReason);
                if (owner.Gold != 0)
                    throw new InvalidOperationException("맡김은 골드를 소모해야 합니다.");
                if (world.CountFollowers(owner.CharacterId) != 0)
                    throw new InvalidOperationException("맡긴 펫은 팔로워 슬롯에서 빠져야 합니다.");
                if (tameGo.activeInHierarchy)
                    throw new InvalidOperationException("맡긴 펫은 despawn 되어야 합니다.");
                if (Math.Abs(world.SkillsOf(owner).Get(SkillId.AnimalTaming) - tameSkill) > 0.0001f)
                    throw new InvalidOperationException("마구간은 조련 스킬을 올리면 안 됩니다.");
                if (!world.HasStabled(owner.CharacterId))
                    throw new InvalidOperationException("맡긴 뒤 HasStabled여야 합니다.");

                var again = world.TryStable(owner, stable);
                if (again.Applied)
                    throw new InvalidOperationException("이미 맡긴 뒤 재맡김은 실패해야 합니다.");

                GameObject otherGo = new GameObject("selfcheck-stable-other");
                try
                {
                    otherGo.transform.position = go.transform.position;
                    var other = otherGo.AddComponent<WorldBody>();
                    other.IsAvatar = true;
                    other.CharacterId = "stable-other";
                    other.Gold = StableYard.GoldCost;
                    other.ResetHp();
                    var otherClaim = world.TryClaimStable(other, stable);
                    if (otherClaim.Applied)
                        throw new InvalidOperationException("타인 마구간 찾기는 실패해야 합니다.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(otherGo);
                }

                var claimed = world.TryClaimStable(owner, stable);
                if (!claimed.Applied)
                    throw new InvalidOperationException("서버 찾기 실패: " + claimed.FailReason);
                if (!tameGo.activeInHierarchy)
                    throw new InvalidOperationException("찾은 펫은 다시 보여야 합니다.");
                if (pet.OwnerCharacterId != owner.CharacterId || !pet.PetFollow || pet.PetStabled)
                    throw new InvalidOperationException("찾은 펫은 주인을 따라야 합니다.");
                if (world.CountFollowers(owner.CharacterId) < 1)
                    throw new InvalidOperationException("찾기 후 팔로워 슬롯이 다시 차야 합니다.");
                if (world.HasStabled(owner.CharacterId))
                    throw new InvalidOperationException("찾기 후 HasStabled가 아니어야 합니다.");
                if (Math.Abs(world.SkillsOf(owner).Get(SkillId.AnimalTaming) - tameSkill) > 0.0001f)
                    throw new InvalidOperationException("찾기는 조련 스킬을 올리면 안 됩니다.");

                var emptyClaim = world.TryClaimStable(owner, stable);
                if (emptyClaim.Applied)
                    throw new InvalidOperationException("빈 마구간 재찾기는 실패해야 합니다.");
            }
            finally
            {
                var leftover = OfflineWorld.Instance;
                if (leftover != null)
                    leftover.ClearStabled("stable-owner");
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                if (tameGo != null)
                {
                    if (!tameGo.activeSelf)
                        tameGo.SetActive(true);
                    pet.OwnerCharacterId = "";
                    pet.PetFollow = false;
                    pet.PetStabled = false;
                    tameGo.transform.SetPositionAndRotation(new Vector3(TameCritter.X, tameGo.transform.position.y, TameCritter.Z), tameGo.transform.rotation);
                }
            }
        }

        static void AssertTravelSlice()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("여행 1은 문게이트만 두고 Recall 주문을 추가하지 않습니다(Bless Count=11과 별개).");

            var go = GameObject.Find(TravelGate.Object);
            var moon = go != null ? go.GetComponent<Moongate>() : null;
            if (moon == null)
                throw new InvalidOperationException("공개 문게이트가 있어야 합니다.");
            Vector3 pos = go.transform.position;
            if (Math.Abs(pos.x - TravelGate.X) > 0.4f || Math.Abs(pos.z - TravelGate.Z) > 0.4f)
                throw new InvalidOperationException("문게이트 좌표가 지정 위치와 같아야 합니다.");
            if (Math.Abs(TravelGate.PlazaX) > 0.0001f || Math.Abs(TravelGate.PlazaZ) > 0.0001f)
                throw new InvalidOperationException("문게이트 목적지는 광장 (0,0)이어야 합니다.");
            float[] lxs = { HousingPlot.X, TameCritter.X, 18.2f, 3.4f, -12.2f, Dungeon1.EntranceX, Dungeon2.EntranceX, 0f, FieldBoss.X };
            float[] lzs = { HousingPlot.Z, TameCritter.Z, 2.4f, -19.6f, 20.4f, Dungeon1.EntranceZ, Dungeon2.EntranceZ, 13.2f, FieldBoss.Z };
            for (int i = 0; i < lxs.Length; i++)
            {
                float dx = pos.x - lxs[i];
                float dz = pos.z - lzs[i];
                if ((dx * dx) + (dz * dz) < 36f)
                    throw new InvalidOperationException("문게이트가 기존 랜드마크와 겹치면 안 됩니다.");
            }

            var ghost = TravelResolve.Gate(new TravelRequest { Ghost = true, Distance = 1f, Gold = TravelGate.GoldCost });
            if (ghost.Applied || ghost.FailReason != "ghost")
                throw new InvalidOperationException("유령은 문게이트를 쓰면 안 됩니다.");
            var far = TravelResolve.Gate(new TravelRequest { Distance = 20f, Gold = TravelGate.GoldCost });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("너무 멀면 문게이트 실패해야 합니다.");
            var poor = TravelResolve.Gate(new TravelRequest { Distance = 1f, Gold = 0 });
            if (poor.Applied || poor.FailReason != "gold")
                throw new InvalidOperationException("골드가 부족하면 여행 실패해야 합니다.");
            var ok = TravelResolve.Gate(new TravelRequest { Distance = 1f, Gold = TravelGate.GoldCost });
            if (!ok.Applied)
                throw new InvalidOperationException("문게이트는 성공해야 합니다: " + ok.FailReason);

            var worldGo = new GameObject("selfcheck-gate-world");
            GameObject bodyGo = null;
            GameObject otherGo = null;
            Vector3 moonPos = go.transform.position;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                bodyGo = new GameObject("selfcheck-gate-body");
                bodyGo.transform.position = moonPos;
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "gate-body";
                body.Gold = TravelGate.GoldCost;
                body.ResetHp();
                var hit = world.TryGate(body, moon);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 문게이트 실패: " + hit.FailReason);
                if (Math.Abs(body.transform.position.x - TravelGate.PlazaX) > 0.2f || Math.Abs(body.transform.position.z - TravelGate.PlazaZ) > 0.2f)
                    throw new InvalidOperationException("문게이트는 광장으로 워프해야 합니다.");
                if (body.Gold != 0)
                    throw new InvalidOperationException("여행 골드 비용이 빠져야 합니다.");

                bodyGo.transform.position = moonPos + new Vector3(20f, 0f, 20f);
                body.Gold = TravelGate.GoldCost;
                var rangeFail = world.TryGate(body, moon);
                if (rangeFail.Applied || rangeFail.FailReason != "range")
                    throw new InvalidOperationException("사거리 밖 문게이트는 실패해야 합니다.");

                bodyGo.transform.position = moonPos;
                body.Gold = 0;
                var goldFail = world.TryGate(body, moon);
                if (goldFail.Applied || goldFail.FailReason != "gold")
                    throw new InvalidOperationException("골드 없는 문게이트는 실패해야 합니다.");

                otherGo = new GameObject("selfcheck-gate-other");
                bodyGo.transform.position = Vector3.zero;
                otherGo.transform.position = Vector3.zero;
                var other = otherGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.CharacterId = "gate-other";
                other.ResetHp();
                var assault = world.TryAttack(body, other);
                if (assault.Applied || assault.FailReason != "innocent")
                    throw new InvalidOperationException("마을 가드존 무고 공격은 막혀야 합니다.");
            }
            finally
            {
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertMarkRecall()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("Mark/Recall 1은 주문(Bless 포함 Count=11)과 별개로 골드 기록/귀환만 둡니다.");
            if (Math.Abs(TravelGate.PlazaX) > 0.0001f || Math.Abs(TravelGate.PlazaZ) > 0.0001f)
                throw new InvalidOperationException("문게이트 목적지는 광장이어야 합니다.");

            var ghostMark = TravelResolve.Mark(new TravelRequest { Ghost = true, Gold = TravelMark.GoldCost, GoldCost = TravelMark.GoldCost });
            if (ghostMark.Applied || ghostMark.FailReason != "ghost")
                throw new InvalidOperationException("유령은 위치 기록하면 안 됩니다.");
            var combatMark = TravelResolve.Mark(new TravelRequest { InCombat = true, Gold = TravelMark.GoldCost, GoldCost = TravelMark.GoldCost });
            if (combatMark.Applied || combatMark.FailReason != "combat")
                throw new InvalidOperationException("전투 중 위치 기록은 실패해야 합니다.");
            var poorMark = TravelResolve.Mark(new TravelRequest { Gold = 0, GoldCost = TravelMark.GoldCost });
            if (poorMark.Applied || poorMark.FailReason != "gold")
                throw new InvalidOperationException("골드 부족 기록은 실패해야 합니다.");
            var okMark = TravelResolve.Mark(new TravelRequest { Gold = TravelMark.GoldCost, GoldCost = TravelMark.GoldCost });
            if (!okMark.Applied)
                throw new InvalidOperationException("위치 기록은 성공해야 합니다: " + okMark.FailReason);

            var noMark = TravelResolve.Recall(new TravelRequest());
            if (noMark.Applied || noMark.FailReason != "no_mark")
                throw new InvalidOperationException("기록 없이 귀환하면 안 됩니다.");
            var ghostRecall = TravelResolve.Recall(new TravelRequest { Ghost = true, HasMark = true });
            if (ghostRecall.Applied || ghostRecall.FailReason != "ghost")
                throw new InvalidOperationException("유령은 귀환하면 안 됩니다.");
            var combatRecall = TravelResolve.Recall(new TravelRequest { InCombat = true, HasMark = true });
            if (combatRecall.Applied || combatRecall.FailReason != "combat")
                throw new InvalidOperationException("전투 중 귀환은 실패해야 합니다.");
            var okRecall = TravelResolve.Recall(new TravelRequest { HasMark = true });
            if (!okRecall.Applied)
                throw new InvalidOperationException("귀환은 성공해야 합니다: " + okRecall.FailReason);

            var oak = GameObject.Find("FieldOak");
            float fieldX = oak != null ? oak.transform.position.x : 18.2f;
            float fieldZ = oak != null ? oak.transform.position.z : 2.4f;
            var go = GameObject.Find(TravelGate.Object);
            var moon = go != null ? go.GetComponent<Moongate>() : null;
            if (moon == null)
                throw new InvalidOperationException("공개 문게이트가 있어야 합니다.");

            var worldGo = new GameObject("selfcheck-mark-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                bodyGo = new GameObject("selfcheck-mark-body");
                bodyGo.transform.position = new Vector3(fieldX, 0.1f, fieldZ);
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "mark-body";
                body.ResetHp();

                var empty = world.TryRecall(body);
                if (empty.Applied || empty.FailReason != "no_mark")
                    throw new InvalidOperationException("서버 기록 없이 귀환은 실패해야 합니다.");

                body.Gold = 0;
                var goldFail = world.TryMark(body);
                if (goldFail.Applied || goldFail.FailReason != "gold")
                    throw new InvalidOperationException("골드 없는 기록은 실패해야 합니다.");

                body.Ghost = true;
                body.Gold = TravelMark.GoldCost;
                var ghostFail = world.TryMark(body);
                if (ghostFail.Applied || ghostFail.FailReason != "ghost")
                    throw new InvalidOperationException("유령 기록은 실패해야 합니다.");
                body.Ghost = false;

                body.CombatUntil = Time.time + 30f;
                var combatFail = world.TryMark(body);
                if (combatFail.Applied || combatFail.FailReason != "combat")
                    throw new InvalidOperationException("전투 중 서버 기록은 실패해야 합니다.");
                body.CombatUntil = 0f;

                body.Gold = TravelMark.GoldCost;
                var marked = world.TryMark(body);
                if (!marked.Applied)
                    throw new InvalidOperationException("서버 위치 기록 실패: " + marked.FailReason);
                if (body.Gold != 0)
                    throw new InvalidOperationException("기록은 골드를 소모해야 합니다.");
                if (!body.HasMark)
                    throw new InvalidOperationException("기록 후 HasMark여야 합니다.");
                if (Math.Abs(body.MarkX - fieldX) > 0.2f || Math.Abs(body.MarkZ - fieldZ) > 0.2f)
                    throw new InvalidOperationException("기록 좌표는 필드여야 합니다.");

                bodyGo.transform.position = new Vector3(TravelGate.PlazaX, 0.1f, TravelGate.PlazaZ);
                body.Ghost = true;
                var ghostWarp = world.TryRecall(body);
                if (ghostWarp.Applied || ghostWarp.FailReason != "ghost")
                    throw new InvalidOperationException("유령 귀환은 실패해야 합니다.");
                body.Ghost = false;
                body.CombatUntil = Time.time + 30f;
                var combatWarp = world.TryRecall(body);
                if (combatWarp.Applied || combatWarp.FailReason != "combat")
                    throw new InvalidOperationException("전투 중 서버 귀환은 실패해야 합니다.");
                body.CombatUntil = 0f;

                var recalled = world.TryRecall(body);
                if (!recalled.Applied)
                    throw new InvalidOperationException("서버 귀환 실패: " + recalled.FailReason);
                if (Math.Abs(body.transform.position.x - fieldX) > 0.2f || Math.Abs(body.transform.position.z - fieldZ) > 0.2f)
                    throw new InvalidOperationException("귀환은 기록한 필드에 착지해야 합니다.");

                bodyGo.transform.position = go.transform.position;
                body.Gold = TravelGate.GoldCost;
                var gated = world.TryGate(body, moon);
                if (!gated.Applied)
                    throw new InvalidOperationException("문게이트는 그대로 성공해야 합니다: " + gated.FailReason);
                if (Math.Abs(body.transform.position.x - TravelGate.PlazaX) > 0.2f || Math.Abs(body.transform.position.z - TravelGate.PlazaZ) > 0.2f)
                    throw new InvalidOperationException("문게이트는 광장으로 워프해야 합니다.");
                if (Math.Abs(body.transform.position.x - fieldX) < 0.2f && Math.Abs(body.transform.position.z - fieldZ) < 0.2f)
                    throw new InvalidOperationException("문게이트는 기록 좌표가 아니라 광장이어야 합니다.");
            }
            finally
            {
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertHousingSlice()
        {
            if (GameObject.Find("Dungeon3") != null || GameObject.Find("Dungeon3Entrance") != null)
                throw new InvalidOperationException("던전 3 오브젝트가 있으면 안 됩니다.");
            OfflineWorld.Instance?.ResetHousePlot();

            var plot = GameObject.Find(HousingPlot.RootObject);
            if (plot == null)
                throw new InvalidOperationException("지정 HousingPlot이 있어야 합니다.");
            Vector3 pos = plot.transform.position;
            if (Math.Abs(pos.x - HousingPlot.X) > 0.2f || Math.Abs(pos.z - HousingPlot.Z) > 0.2f)
                throw new InvalidOperationException("HousingPlot 좌표가 지정 부지와 같아야 합니다.");
            if (GuardZone.Contains(pos.x, pos.z))
                throw new InvalidOperationException("HousingPlot은 GuardZone 밖이어야 합니다.");
            float[] lxs = { 18.2f, 3.4f, -12.2f, Dungeon1.EntranceX, Dungeon2.EntranceX };
            float[] lzs = { 2.4f, -19.6f, 20.4f, Dungeon1.EntranceZ, Dungeon2.EntranceZ };
            for (int i = 0; i < lxs.Length; i++)
            {
                float dx = pos.x - lxs[i];
                float dz = pos.z - lzs[i];
                if ((dx * dx) + (dz * dz) < 36f)
                    throw new InvalidOperationException("HousingPlot이 기존 랜드마크와 겹치면 안 됩니다.");
            }
            var station = GameObject.Find(HousingPlot.StationObject);
            if (station == null || station.GetComponent<HousePlotStation>() == null)
                throw new InvalidOperationException("HousePlotStation이 있어야 합니다.");
            var chestGo = GameObject.Find(HousingPlot.ChestObject);
            if (chestGo == null || chestGo.GetComponent<HouseChest>() == null)
                throw new InvalidOperationException("HouseChest가 있어야 합니다.");
            string[] landmarks = { "Forge", "Vendor", "Healer" };
            for (int i = 0; i < landmarks.Length; i++)
            {
                var mark = GameObject.Find(landmarks[i]);
                if (mark == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + landmarks[i]);
                var mr = mark.GetComponentsInChildren<Renderer>();
                if (mr.Length > 0)
                {
                    Bounds mb = mr[0].bounds;
                    for (int k = 1; k < mr.Length; k++)
                        mb.Encapsulate(mr[k].bounds);
                    if (mb.min.y < -0.05f)
                        throw new InvalidOperationException(landmarks[i] + "가 땅속에 있음 minY=" + mb.min.y);
                }
            }
            var vendorGo = GameObject.Find(HousingPlot.VendorObject);
            if (vendorGo == null || vendorGo.GetComponent<HouseVendor>() == null)
                throw new InvalidOperationException("HouseVendor가 있어야 합니다.");
            string vpath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(vendorGo);
            if (string.IsNullOrEmpty(vpath) || vpath.IndexOf("/RAW/", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("HouseVendor는 Prefab이어야 합니다(RAW fbx 아님).");
            var vr = vendorGo.GetComponentsInChildren<Renderer>();
            if (vr.Length > 0)
            {
                Bounds vb = vr[0].bounds;
                for (int k = 1; k < vr.Length; k++)
                    vb.Encapsulate(vr[k].bounds);
                if (vb.min.y < -0.05f)
                    throw new InvalidOperationException("HouseVendor가 땅속에 있음 minY=" + vb.min.y);
            }

            var none = HouseResolve.Claim(new HouseRequest
            {
                PlotExists = true,
                Occupied = false,
                Distance = 1f,
                Gold = 0,
                ActorCharacterId = "a"
            });
            if (none.Applied || none.FailReason != "gold")
                throw new InvalidOperationException("골드 부족 claim은 실패해야 합니다.");
            var occupied = HouseResolve.Claim(new HouseRequest
            {
                PlotExists = true,
                Occupied = true,
                Distance = 1f,
                Gold = HousingPlot.ClaimGold,
                ActorCharacterId = "b"
            });
            if (occupied.Applied || occupied.FailReason != "occupied")
                throw new InvalidOperationException("점유 부지 claim은 실패해야 합니다.");
            var strangerLock = HouseResolve.Lockdown(new HouseRequest
            {
                PlotExists = true,
                Occupied = true,
                Distance = 1f,
                ActorCharacterId = "x",
                OwnerCharacterId = "y",
                HasBackpackItem = true
            });
            if (strangerLock.Applied || strangerLock.FailReason != "not_owner")
                throw new InvalidOperationException("타인 lockdown은 실패해야 합니다.");

            var worldGo = new GameObject("selfcheck-house-world");
            GameObject ownerGo = null;
            GameObject otherGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                ownerGo = new GameObject("selfcheck-house-owner");
                ownerGo.transform.position = station.transform.position;
                var owner = ownerGo.AddComponent<WorldBody>();
                owner.IsAvatar = true;
                owner.CharacterId = "house-owner";
                owner.AccountId = "house-acc";
                owner.Gold = HousingPlot.ClaimGold;
                owner.ResetHp();
                var bag = ownerGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Cloth, 1);

                var stationMb = station.GetComponent<HousePlotStation>();
                var chestMb = chestGo.GetComponent<HouseChest>();
                var claim = world.TryClaimHouse(owner, stationMb);
                if (!claim.Applied)
                    throw new InvalidOperationException("빈 부지 claim은 성공해야 합니다: " + claim.FailReason);
                if (owner.Gold != 0)
                    throw new InvalidOperationException("claim은 골드를 소모해야 합니다.");
                var again = world.TryClaimHouse(owner, stationMb);
                if (again.Applied)
                    throw new InvalidOperationException("같은 캐릭터 재claim은 실패해야 합니다.");

                otherGo = new GameObject("selfcheck-house-other");
                otherGo.transform.position = station.transform.position;
                var other = otherGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.CharacterId = "house-other";
                other.AccountId = "house-other-acc";
                other.Gold = HousingPlot.ClaimGold;
                other.ResetHp();
                otherGo.AddComponent<InventoryBag>().Add(ItemCatalog.Cloth, 1);
                var otherClaim = world.TryClaimHouse(other, stationMb);
                if (otherClaim.Applied)
                    throw new InvalidOperationException("타인 점유 부지 claim은 실패해야 합니다.");
                otherGo.transform.position = chestGo.transform.position;
                var otherLock = world.TryLockdown(other, chestMb, ItemCatalog.Cloth);
                if (otherLock.Applied)
                    throw new InvalidOperationException("타인 lockdown은 실패해야 합니다.");
                var otherTake = world.TrySecureTake(other, chestMb);
                if (otherTake.Applied)
                    throw new InvalidOperationException("타인 secure take는 실패해야 합니다.");

                ownerGo.transform.position = chestGo.transform.position;
                var lockOk = world.TryLockdown(owner, chestMb, ItemCatalog.Cloth);
                if (!lockOk.Applied)
                    throw new InvalidOperationException("소유자 lockdown은 성공해야 합니다: " + lockOk.FailReason);
                int clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                if (clothLeft != 0)
                    throw new InvalidOperationException("lockdown은 가방 천을 옮겨야 합니다.");
                var takeOk = world.TrySecureTake(owner, chestMb);
                if (!takeOk.Applied)
                    throw new InvalidOperationException("소유자 take는 성공해야 합니다: " + takeOk.FailReason);
                clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                if (clothLeft < 1)
                    throw new InvalidOperationException("take는 천을 가방으로 되돌려야 합니다.");

                var vendorMb = vendorGo.GetComponent<HouseVendor>();
                ownerGo.transform.position = vendorGo.transform.position;
                otherGo.transform.position = vendorGo.transform.position;
                var otherList = world.TryListVendor(other, vendorMb, ItemCatalog.Cloth);
                if (otherList.Applied)
                    throw new InvalidOperationException("타인 vendor list는 실패해야 합니다.");
                int price = ItemCatalog.BuyPrice(ItemCatalog.Cloth);
                var listed = world.TryListVendor(owner, vendorMb, ItemCatalog.Cloth);
                if (!listed.Applied)
                    throw new InvalidOperationException("소유자 vendor list는 성공해야 합니다: " + listed.FailReason);
                clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                if (clothLeft != 0)
                    throw new InvalidOperationException("list는 가방 천을 옮겨야 합니다.");
                bag.Add(ItemCatalog.Cloth, 1);
                var listedAgain = world.TryListVendor(owner, vendorMb, ItemCatalog.Cloth);
                if (listedAgain.Applied)
                    throw new InvalidOperationException("vendor slot 1을 넘기면 안 됩니다.");
                var otherBag = otherGo.GetComponent<InventoryBag>();
                other.Gold = price;
                var buy = world.TryBuyHouseVendor(other, vendorMb);
                if (!buy.Applied)
                    throw new InvalidOperationException("타인 vendor buy는 성공해야 합니다: " + buy.FailReason);
                if (other.Gold != 0)
                    throw new InvalidOperationException("buy는 골드를 소모해야 합니다.");
                if (owner.Gold != price)
                    throw new InvalidOperationException("buy는 소유자에게 골드를 줘야 합니다.");
                int otherCloth = 0;
                for (int i = 0; i < otherBag.Items.Count; i++)
                    if (otherBag.Items[i].TemplateId == ItemCatalog.Cloth)
                        otherCloth += otherBag.Items[i].Amount;
                if (otherCloth < 1)
                    throw new InvalidOperationException("buy는 천을 구매자 가방으로 옮겨야 합니다.");
                var buyEmpty = world.TryBuyHouseVendor(other, vendorMb);
                if (buyEmpty.Applied)
                    throw new InvalidOperationException("빈 vendor buy는 실패해야 합니다.");
            }
            finally
            {
                OfflineWorld.Instance?.ResetHousePlot();
                if (ownerGo != null)
                    UnityEngine.Object.DestroyImmediate(ownerGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

    }
}
