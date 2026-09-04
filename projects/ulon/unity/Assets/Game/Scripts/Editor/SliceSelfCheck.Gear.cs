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
        static void AssertStrengthRequirement()
        {
            AssertDungeon3Leftover();
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            if (ItemCatalog.StrReqOf(ItemCatalog.IronSword) != 25)
                throw new InvalidOperationException("철검 Strength Requirement는 25여야 합니다.");
            if (ItemCatalog.StrReqOf(ItemCatalog.WoodenClub) != 0 || ItemCatalog.StrReqOf(ItemCatalog.WoodenBow) != 0)
                throw new InvalidOperationException("다른 무기는 Strength Requirement 기본 0이어야 합니다.");

            var lowResolve = EquipResolve.Equip(new EquipRequest
            {
                HasItem = true,
                Str = 10,
                StrReq = ItemCatalog.StrReqOf(ItemCatalog.IronSword),
                TemplateId = ItemCatalog.IronSword
            });
            if (lowResolve.Applied || lowResolve.FailReason != "str_req")
                throw new InvalidOperationException("저 STR EquipResolve는 str_req 실패여야 합니다.");
            string msg = EquipResolve.MessageFor("str_req", ItemCatalog.IronSword, 25);
            if (string.IsNullOrEmpty(msg) || msg.IndexOf("근력") < 0)
                throw new InvalidOperationException("장착 실패 메시지가 명확해야 합니다: " + msg);

            var highResolve = EquipResolve.Equip(new EquipRequest
            {
                HasItem = true,
                Str = 30,
                StrReq = ItemCatalog.StrReqOf(ItemCatalog.IronSword),
                TemplateId = ItemCatalog.IronSword
            });
            if (!highResolve.Applied)
                throw new InvalidOperationException("고 STR EquipResolve는 성공해야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-strreq-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                bodyGo = new GameObject("selfcheck-strreq-body");
                bodyGo.transform.position = new Vector3(2f, 0f, 2f);
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "strreq-body";
                body.RecalcFromStr(30);
                body.ResetHp();
                var bag = bodyGo.AddComponent<InventoryBag>();
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = 40 });

                world.StatsOf(body).ForceSet(10, 25, 25);
                var low = world.TryEquip(body, ItemCatalog.IronSword);
                if (low.Applied || low.FailReason != "str_req")
                    throw new InvalidOperationException("저 STR TryEquip은 실패해야 합니다: " + low.FailReason);
                if (world.EquippedOf(body) == ItemCatalog.IronSword)
                    throw new InvalidOperationException("저 STR이면 장착되면 안 됩니다.");
                if (world.LastEquipMessage.IndexOf("근력") < 0)
                    throw new InvalidOperationException("저 STR 장착 메시지가 명확해야 합니다: " + world.LastEquipMessage);

                world.StatsOf(body).ForceSet(30, 25, 25);
                var high = world.TryEquip(body, ItemCatalog.IronSword);
                if (!high.Applied)
                    throw new InvalidOperationException("고 STR TryEquip 실패: " + high.FailReason);
                if (world.EquippedOf(body) != ItemCatalog.IronSword)
                    throw new InvalidOperationException("고 STR이면 철검이 장착되어야 합니다.");
                if (world.LastEquipMessage.IndexOf("장착") < 0)
                    throw new InvalidOperationException("고 STR 장착 메시지가 있어야 합니다: " + world.LastEquipMessage);

                var club = world.TryEquip(body, ItemCatalog.WoodenClub);
                if (club.Applied || club.FailReason != "no_item")
                    throw new InvalidOperationException("없는 아이템 장착은 no_item이어야 합니다.");

                var off = world.TryUnequip(body);
                if (!off.Applied || world.EquippedOf(body) != "")
                    throw new InvalidOperationException("TryUnequip 후 장착이 비어야 합니다.");

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


        static void AssertOverweight()
        {
            AssertDungeon3Leftover();
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            if (ItemCatalog.CarryCap(10) != 40 || ItemCatalog.CarryCap(30) != 120)
                throw new InvalidOperationException("CarryCap은 STR*4(최소 10)여야 합니다.");
            if (ItemCatalog.WeightOf("iron_ore") != 2f)
                throw new InvalidOperationException("iron_ore 무게는 2여야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-weight-world");
            GameObject bodyGo = null;
            GameObject veinGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                bodyGo = new GameObject("selfcheck-weight-body");
                bodyGo.transform.position = new Vector3(3f, 0f, 3f);
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "weight-body";
                body.RecalcFromStr(10);
                body.ResetHp();
                var bag = bodyGo.AddComponent<InventoryBag>();
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.Pickaxe, Amount = 1, Uses = 20 });
                // Cap 40; pickaxe 6 + 17 ore = 40 exactly at cap. Next ore (+2) must fail.
                bag.Add("iron_ore", 17);
                world.StatsOf(body).ForceSet(10, 25, 25);
                float near = bag.TotalWeight();
                int cap = ItemCatalog.CarryCap(10);
                if (near > cap || near + ItemCatalog.WeightOf("iron_ore") <= cap)
                    throw new InvalidOperationException("과적 직전 세팅 실패: " + near + "/" + cap);

                veinGo = new GameObject("IronVein");
                veinGo.transform.position = bodyGo.transform.position;
                var vein = veinGo.AddComponent<ResourceNode>();
                vein.ResourceId = "iron_ore";
                vein.GatherSkill = SkillId.Mining;
                vein.Remaining = 5;
                vein.Capacity = 5;

                var blocked = world.TryGather(body, vein);
                if (blocked.Applied || blocked.FailReason != "overweight")
                    throw new InvalidOperationException("과적 직전 TryGather는 overweight 실패여야 합니다: " + blocked.FailReason);
                if (string.IsNullOrEmpty(world.LastWeightMessage) || world.LastWeightMessage.IndexOf("과적") < 0)
                    throw new InvalidOperationException("과적 메시지가 명확해야 합니다: " + world.LastWeightMessage);
                if (bag.TotalWeight() != near)
                    throw new InvalidOperationException("과적 실패 시 가방 무게가 변하면 안 됩니다.");

                // Drop one ore → room for one gather
                if (!bag.TakeOne("iron_ore"))
                    throw new InvalidOperationException("광석 드롭 실패");
                var afterDrop = world.TryGather(body, vein);
                if (!afterDrop.Applied)
                    throw new InvalidOperationException("드롭 후 TryGather 실패: " + afterDrop.FailReason);

                // Fill near high-STR cap then lower STR... better: raise STR and gather again after filling
                // Reset bag near low cap again, then higher STR succeeds without drop
                bag.Items.Clear();
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.Pickaxe, Amount = 1, Uses = 20 });
                bag.Add("iron_ore", 17);
                world.StatsOf(body).ForceSet(10, 25, 25);
                var stillBlocked = world.TryGather(body, vein);
                if (stillBlocked.Applied || stillBlocked.FailReason != "overweight")
                    throw new InvalidOperationException("재과적 TryGather는 실패해야 합니다.");
                world.StatsOf(body).ForceSet(30, 25, 25);
                var highStr = world.TryGather(body, vein);
                if (!highStr.Applied)
                    throw new InvalidOperationException("고 STR TryGather 실패: " + highStr.FailReason);

                // Buy path: low STR near cap
                bag.Items.Clear();
                bag.Add("iron_ore", 20); // 40/40
                world.StatsOf(body).ForceSet(10, 25, 25);
                body.Gold = 100;
                var vendorGo = GameObject.Find("Vendor");
                var vendor = vendorGo != null ? vendorGo.GetComponent<VendorStation>() : null;
                if (vendor == null)
                    throw new InvalidOperationException("VendorStation이 있어야 합니다.");
                bodyGo.transform.position = vendorGo.transform.position;
                var opened = world.TryVendor(body, vendor);
                if (!opened.Applied)
                    throw new InvalidOperationException("TryVendor 실패: " + opened.FailReason);
                var buyFail = world.TryBuy(body, ItemCatalog.Bandage);
                if (buyFail.Applied || buyFail.FailReason != "overweight")
                    throw new InvalidOperationException("과적 TryBuy는 overweight 실패여야 합니다: " + buyFail.FailReason);
                if (world.LastWeightMessage.IndexOf("과적") < 0)
                    throw new InvalidOperationException("과적 구매 메시지가 명확해야 합니다: " + world.LastWeightMessage);
                world.CloseVendor();

                AssertDungeon3Leftover("Weight 슬라이스 후");
                world.ResetHousePlot();
            }
            finally
            {
                OfflineWorld.Instance?.CloseVendor();
                OfflineWorld.Instance?.ResetHousePlot();
                if (veinGo != null)
                    UnityEngine.Object.DestroyImmediate(veinGo);
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertMeditationArmorPenalty()
        {
            AssertDungeon3Leftover();
            string[] keep = { "Forge", "Vendor", "Healer", HousingPlot.VendorObject, StableYard.Object };
            for (int i = 0; i < keep.Length; i++)
            {
                if (GameObject.Find(keep[i]) == null)
                    throw new InvalidOperationException("마을 랜드마크가 있어야 합니다: " + keep[i]);
            }
            var decor = GameObject.Find("VillageDecor");
            if (decor == null || decor.transform.childCount < 200)
                throw new InvalidOperationException("VillageDecor 울타리/집을 지우면 안 됩니다.");

            if (!ItemCatalog.IsHeavyArmor(ItemCatalog.IronPlate))
                throw new InvalidOperationException("iron_plate는 HeavyArmor여야 합니다.");
            if (ItemCatalog.IsHeavyArmor(ItemCatalog.WoodenShield) || ItemCatalog.IsHeavyArmor(ItemCatalog.Cloth))
                throw new InvalidOperationException("방패/천은 중갑이 아니어야 합니다.");
            if (Math.Abs(MeditationResolve.HeavyMul - 0.5f) > 0.0001f)
                throw new InvalidOperationException("중갑 명상 패널티는 절반(0.5)이어야 합니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int light = MeditationResolve.Amount(skills, stats, false);
            int heavy = MeditationResolve.Amount(skills, stats, true);
            int expected = light / 2;
            if (expected < 1)
                expected = 1;
            if (heavy != expected)
                throw new InvalidOperationException("중갑 명상 회복은 절반이어야 합니다: light=" + light + " heavy=" + heavy);
            if (heavy >= light)
                throw new InvalidOperationException("중갑은 경갑/무갑보다 명상 회복이 낮아야 합니다.");

            var lightReq = MeditationResolve.Resolve(new MeditationRequest
            {
                Now = 1f,
                Skills = new SkillSet(),
                Stats = new StatSet(),
                Mana = 5f,
                MaxMana = 35f,
                HeavyArmor = false,
                Difficulty = MeditationResolve.Difficulty
            });
            if (!lightReq.Applied || lightReq.Damage != light)
                throw new InvalidOperationException("무갑 명상은 정상 회복이어야 합니다.");

            var heavyReq = MeditationResolve.Resolve(new MeditationRequest
            {
                Now = 1f,
                Skills = new SkillSet(),
                Stats = new StatSet(),
                Mana = 5f,
                MaxMana = 35f,
                HeavyArmor = true,
                Difficulty = MeditationResolve.Difficulty
            });
            if (!heavyReq.Applied || heavyReq.Damage != expected)
                throw new InvalidOperationException("중갑 명상 Resolve 회복량이 절반이어야 합니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-med-armor-world");
            GameObject lightGo = null;
            GameObject heavyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                lightGo = new GameObject("selfcheck-med-armor-light");
                lightGo.transform.position = new Vector3(2.2f, 0f, 2.2f);
                var lightBody = lightGo.AddComponent<WorldBody>();
                lightBody.IsAvatar = true;
                lightBody.CharacterId = "med-armor-light";
                lightBody.RecalcFromInt(world.StatsOf(lightBody).Int);
                lightBody.SetMana(4f);
                lightGo.AddComponent<InventoryBag>();
                float beforeLight = lightBody.Mana;
                var lightHit = world.TryMeditate(lightBody);
                if (!lightHit.Applied)
                    throw new InvalidOperationException("무갑 TryMeditate 실패: " + lightHit.FailReason);
                int lightAmt = MeditationResolve.Amount(world.SkillsOf(lightBody), world.StatsOf(lightBody), false);
                if (lightHit.Damage != lightAmt)
                    throw new InvalidOperationException("무갑 TryMeditate 회복량이 정상이어야 합니다.");
                if (lightBody.Mana <= beforeLight)
                    throw new InvalidOperationException("무갑 명상이 마나를 올려야 합니다.");

                heavyGo = new GameObject("selfcheck-med-armor-heavy");
                heavyGo.transform.position = new Vector3(2.4f, 0f, 2.4f);
                var heavyBody = heavyGo.AddComponent<WorldBody>();
                heavyBody.IsAvatar = true;
                heavyBody.CharacterId = "med-armor-heavy";
                heavyBody.RecalcFromInt(world.StatsOf(heavyBody).Int);
                heavyBody.SetMana(4f);
                var heavyBag = heavyGo.AddComponent<InventoryBag>();
                heavyBag.Add(ItemCatalog.IronPlate, 1);
                if (!ItemCatalog.HasHeavyArmor(heavyBag.Items))
                    throw new InvalidOperationException("iron_plate 가방은 HasHeavyArmor여야 합니다.");
                float beforeHeavy = heavyBody.Mana;
                var heavyHit = world.TryMeditate(heavyBody);
                if (!heavyHit.Applied)
                    throw new InvalidOperationException("중갑 TryMeditate 실패: " + heavyHit.FailReason);
                int heavyAmt = MeditationResolve.Amount(world.SkillsOf(heavyBody), world.StatsOf(heavyBody), true);
                if (heavyHit.Damage != heavyAmt)
                    throw new InvalidOperationException("중갑 TryMeditate 회복량이 패널티를 받아야 합니다.");
                if (heavyHit.Damage >= lightHit.Damage)
                    throw new InvalidOperationException("중갑 TryMeditate 회복은 무갑보다 적어야 합니다.");
                if (heavyBody.Mana - beforeHeavy != heavyHit.Damage)
                    throw new InvalidOperationException("중갑 명상 마나 증가가 Damage와 같아야 합니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (lightGo != null)
                    UnityEngine.Object.DestroyImmediate(lightGo);
                if (heavyGo != null)
                    UnityEngine.Object.DestroyImmediate(heavyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
                OfflineWorld.Instance?.ResetHousePlot();
            }
        }

    }
}
