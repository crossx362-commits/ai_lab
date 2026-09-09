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
        /// **생활 기술 규칙**(랩 ㉪ 4/N) — 치유·붕대·재봉처럼 「싸우지 않는 손」의 검사.
        /// 담는 것: 그 기술들의 수치·성공/실패 조건. 안 담는 것: 전투·제작(다른 partial), 자 배선(`Gates`).
        /// </summary>
        static void RunLifeSkillRules()
        {
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
            RunGates();
        }
    }
}
