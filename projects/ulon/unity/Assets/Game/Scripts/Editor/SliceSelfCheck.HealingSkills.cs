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
        // **살리고 독을 빼는 것**(랩 ㉯) — 부활·붕대 해독.
        // 담는 것: 죽은 자를 되살리고 상태이상을 지우는 규칙. 안 담는 것: 무기(`WeaponSkills`),
        // 먹을 것(`Food`), 땅과 밭(본체).
        static void AssertHealingResurrect()
        {
            AssertDungeon3Leftover();
            if (SkillNames.KoreanOf(SkillId.Healing) != "치유")
                throw new InvalidOperationException("치유 스킬명을 바꾸면 안 됩니다.");
            if (StatSet.PrimaryOf(SkillId.Healing) != StatId.Dex)
                throw new InvalidOperationException("치유 Primary는 DEX이어야 합니다.");

            var ghostHealer = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                HealerGhost = true,
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (ghostHealer.Applied || ghostHealer.FailReason != "ghost")
                throw new InvalidOperationException("유령 시술자 붕대 부활은 실패해야 합니다.");

            var noGhost = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = false,
                TargetAvatar = true,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (noGhost.Applied || noGhost.FailReason != "not_ghost")
                throw new InvalidOperationException("유령 아닌 대상 붕대 부활은 실패해야 합니다.");

            var notAvatar = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = false,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (notAvatar.Applied || notAvatar.FailReason != "not_ghost")
                throw new InvalidOperationException("아바타가 아닌 Ghost 붕대 부활은 실패해야 합니다.");

            var noBnSkills = new SkillSet();
            var noBn = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = false,
                Distance = 1f,
                Skills = noBnSkills
            });
            if (noBn.Applied || noBn.FailReason != "no_bandage")
                throw new InvalidOperationException("붕대 없는 부활은 실패해야 합니다.");
            if (Math.Abs(noBnSkills.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("실패한 붕대 부활은 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = true,
                Distance = ItemCatalog.MeleeRange + 1f,
                Range = ItemCatalog.MeleeRange,
                Skills = farSkills
            });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("사거리 밖 붕대 부활은 실패해야 합니다.");
            if (Math.Abs(farSkills.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("사거리 밖 붕대 부활은 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = true,
                Distance = 1f,
                Skills = skills,
                Stats = stats,
                Difficulty = BandageResurrectResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("붕대 부활 Resolve는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Healing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 붕대 부활 후 Healing 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Magery)) > 0.0001f)
                throw new InvalidOperationException("붕대 부활은 마법을 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("붕대 부활은 수의학을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("붕대 부활 상승 시 DEX가 올라야 합니다.");

            var forceSkills = new SkillSet();
            var forceStats = new StatSet();
            var forced = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = true,
                Distance = 1f,
                Skills = forceSkills,
                Stats = forceStats,
                Force = true
            });
            if (!forced.Applied || Math.Abs(forceSkills.Get(SkillId.Healing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("Force 경로도 Healing 0.1이어야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Healing, SkillLock.Locked);
            var lockedOk = BandageResurrectResolve.Resolve(new BandageResurrectRequest
            {
                TargetGhost = true,
                TargetAvatar = true,
                HasBandage = true,
                Distance = 1f,
                Skills = locked,
                Force = true
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 Healing도 부활 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("잠긴 Healing은 오르면 안 됩니다.");

            OfflineWorld.Instance?.ResetHousePlot();
            var healerGo = new GameObject("selfcheck-healrez-healer");
            var ghostGo = new GameObject("selfcheck-healrez-ghost");
            GameObject worldGo = null;
            GameObject stationGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-healrez-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                world.ResetHousePlot();

                healerGo.transform.position = new Vector3(42f, 0f, 0f);
                var healer = healerGo.AddComponent<WorldBody>();
                healer.IsAvatar = true;
                healer.RecalcFromStr(30);
                healer.ResetHp();
                var bag = healerGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Bandage, 2);

                ghostGo.transform.position = healerGo.transform.position;
                var ghost = ghostGo.AddComponent<WorldBody>();
                ghost.IsAvatar = true;
                ghost.DisplayName = "유령";
                ghost.RecalcFromStr(30);
                ghost.ResetHp();
                ghost.Ghost = true;
                ghost.SetHp(0f);
                if (!ghost.Ghost)
                    throw new InvalidOperationException("대상은 Ghost여야 합니다.");

                var living = world.TryResurrectBandage(healer, healer);
                if (living.Applied)
                    throw new InvalidOperationException("살아있는 시술자 자신은 붕대 부활되면 안 됩니다.");

                var hit = world.TryResurrectBandage(healer, ghost);
                if (!hit.Applied || ghost.Ghost)
                    throw new InvalidOperationException("서버 붕대 부활 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Healing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("서버 붕대 부활 후 Healing 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("서버 붕대 부활은 마법을 올리면 안 됩니다.");
                int left = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Bandage)
                        left += bag.Items[i].Amount;
                if (left != 1)
                    throw new InvalidOperationException("성공 붕대 부활은 붕대 1을 소모해야 합니다.");
                if (ghost.Hp <= 0f || ghost.Ghost)
                    throw new InvalidOperationException("부활 후 HP가 회복되어야 합니다.");
                if (string.IsNullOrEmpty(world.LastHealRezMessage) || world.LastHealRezMessage.IndexOf("부활", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("붕대 부활 메시지가 있어야 합니다.");

                // TryHeal routes to resurrect when target ghost
                ghost.Ghost = true;
                ghost.SetHp(0f);
                ghost.Ghost = true;
                var viaHeal = world.TryHeal(healer, ghost);
                if (!viaHeal.Applied || ghost.Ghost)
                    throw new InvalidOperationException("TryHeal(ghost)는 붕대 부활이어야 합니다: " + viaHeal.FailReason);

                // HealerStation path stays
                ghost.Ghost = true;
                ghost.SetHp(0f);
                ghost.Ghost = true;
                stationGo = new GameObject("selfcheck-healrez-station");
                stationGo.transform.position = ghostGo.transform.position;
                var station = stationGo.AddComponent<HealerStation>();
                var stationRez = world.TryResurrect(ghost, station);
                if (!stationRez.Applied || ghost.Ghost)
                    throw new InvalidOperationException("HealerStation TryResurrect가 유지되어야 합니다: " + stationRez.FailReason);

                // fail: too far
                ghost.Ghost = true;
                ghost.SetHp(0f);
                ghost.Ghost = true;
                bag.Add(ItemCatalog.Bandage, 1);
                ghostGo.transform.position = healerGo.transform.position + new Vector3(ItemCatalog.MeleeRange + 2f, 0f, 0f);
                var ranged = world.TryResurrectBandage(healer, ghost);
                if (ranged.Applied)
                    throw new InvalidOperationException("먼 거리 붕대 부활은 실패해야 합니다.");

                // fail: healer ghost
                healer.Ghost = true;
                ghostGo.transform.position = healerGo.transform.position;
                var hg = world.TryResurrectBandage(healer, ghost);
                if (hg.Applied)
                    throw new InvalidOperationException("유령 시술자 서버 부활은 실패해야 합니다.");
                healer.Ghost = false;
                healer.ResetHp();

                AssertDungeon3Leftover("붕대 부활 후");
            }
            finally
            {
                OfflineWorld.Instance?.ResetHousePlot();
                UnityEngine.Object.DestroyImmediate(healerGo);
                UnityEngine.Object.DestroyImmediate(ghostGo);
                if (stationGo != null)
                    UnityEngine.Object.DestroyImmediate(stationGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertBandageDetox()
        {
            AssertVillageIntact();

            if (SkillNames.KoreanOf(SkillId.Healing) != "치유")
                throw new InvalidOperationException("치유 스킬명을 바꾸면 안 됩니다.");

            var ghostSkills = new SkillSet();
            var ghostFail = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                HealerGhost = false,
                TargetGhost = true,
                TargetAlive = false,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = 1f,
                Skills = ghostSkills
            });
            if (ghostFail.Applied || ghostFail.FailReason != "ghost")
                throw new InvalidOperationException("유령 대상 붕대 해독은 실패해야 합니다.");
            if (Math.Abs(ghostSkills.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("실패한 해독은 Healing을 올리면 안 됩니다.");

            var healerGhost = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                HealerGhost = true,
                TargetGhost = false,
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = 1f,
                Skills = new SkillSet()
            });
            if (healerGhost.Applied || healerGhost.FailReason != "ghost")
                throw new InvalidOperationException("유령 시술자 붕대 해독은 실패해야 합니다.");

            var noPoisonSkills = new SkillSet();
            var noPoison = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = 0,
                HasBandage = true,
                Distance = 1f,
                Skills = noPoisonSkills
            });
            if (noPoison.Applied || noPoison.FailReason != "no_poison")
                throw new InvalidOperationException("독 없는 붕대 해독은 실패해야 합니다.");

            var noBnSkills = new SkillSet();
            var noBn = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = false,
                Distance = 1f,
                Skills = noBnSkills
            });
            if (noBn.Applied || noBn.FailReason != "no_bandage")
                throw new InvalidOperationException("붕대 없는 해독은 실패해야 합니다.");
            if (Math.Abs(noBnSkills.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("붕대 없는 해독은 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = ItemCatalog.MeleeRange + 1f,
                Range = ItemCatalog.MeleeRange,
                Skills = farSkills
            });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("사거리 밖 붕대 해독은 실패해야 합니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = 1f,
                Skills = skills,
                Stats = stats,
                Difficulty = BandageCurePoisonResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("붕대 해독 Resolve는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Healing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 붕대 해독 후 Healing 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Magery)) > 0.0001f)
                throw new InvalidOperationException("붕대 해독은 마법을 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("붕대 해독은 수의학을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("붕대 해독 상승 시 DEX가 올라야 합니다.");

            var forceSkills = new SkillSet();
            var forced = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = 1f,
                Skills = forceSkills,
                Stats = new StatSet(),
                Force = true
            });
            if (!forced.Applied || Math.Abs(forceSkills.Get(SkillId.Healing) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("Force 경로도 Healing 0.1이어야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Healing, SkillLock.Locked);
            var lockedOk = BandageCurePoisonResolve.Resolve(new BandageCurePoisonRequest
            {
                TargetAlive = true,
                PoisonTicks = PoisoningResolve.TickCount,
                HasBandage = true,
                Distance = 1f,
                Skills = locked,
                Force = true
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 Healing도 해독 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("잠긴 Healing은 오르면 안 됩니다.");

            OfflineWorld.Instance?.ResetHousePlot();
            var healerGo = new GameObject("selfcheck-detox-healer");
            var allyGo = new GameObject("selfcheck-detox-ally");
            GameObject worldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-detox-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                world.ResetHousePlot();

                healerGo.transform.position = new Vector3(42f, 0f, 0f);
                var healer = healerGo.AddComponent<WorldBody>();
                healer.IsAvatar = true;
                healer.DisplayName = "치유사";
                healer.RecalcFromStr(30);
                healer.ResetHp();
                var bag = healerGo.AddComponent<InventoryBag>();
                bag.Add(ItemCatalog.Bandage, 3);

                // self cure
                healer.PoisonTicks = PoisoningResolve.TickCount;
                healer.NextPoisonAt = UnityEngine.Time.time + 10f;
                var selfHit = world.TryCurePoison(healer, healer);
                if (!selfHit.Applied)
                    throw new InvalidOperationException("자가 붕대 해독 실패: " + selfHit.FailReason);
                if (healer.PoisonTicks != 0 || healer.NextPoisonAt != 0f)
                    throw new InvalidOperationException("해독 후 독 틱이 남아 있으면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Healing) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("서버 해독 후 Healing 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(healer).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("붕대 해독은 Magery를 올리면 안 됩니다.");
                int left = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Bandage)
                        left += bag.Items[i].Amount;
                if (left != 2)
                    throw new InvalidOperationException("성공 해독은 붕대 1을 소모해야 합니다.");
                if (string.IsNullOrEmpty(world.LastCurePoisonMessage) || world.LastCurePoisonMessage.IndexOf("해독", System.StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("해독 메시지가 있어야 합니다.");

                // no poison fail
                var none = world.TryCurePoison(healer, healer);
                if (none.Applied || none.FailReason != "no_poison")
                    throw new InvalidOperationException("독 없을 때 TryCurePoison은 no_poison이어야 합니다.");

                // ally via TryHeal route
                allyGo.transform.position = healerGo.transform.position;
                var ally = allyGo.AddComponent<WorldBody>();
                ally.IsAvatar = true;
                ally.IsEnemy = false;
                ally.DisplayName = "동료";
                ally.RecalcFromStr(30);
                ally.ResetHp();
                ally.PoisonTicks = PoisoningResolve.TickCount;
                ally.NextPoisonAt = UnityEngine.Time.time + 5f;
                float healBefore = world.SkillsOf(healer).Get(SkillId.Healing);
                var viaHeal = world.TryHeal(healer, ally);
                if (!viaHeal.Applied)
                    throw new InvalidOperationException("TryHeal(poisoned)는 해독이어야 합니다: " + viaHeal.FailReason);
                if (ally.PoisonTicks != 0 || ally.NextPoisonAt != 0f)
                    throw new InvalidOperationException("TryHeal 해독 후 독이 남아 있으면 안 됩니다.");
                if (world.SkillsOf(healer).Get(SkillId.Healing) < healBefore)
                    throw new InvalidOperationException("TryHeal 해독 경로도 Healing이 유지/상승해야 합니다.");

                // range fail
                ally.PoisonTicks = PoisoningResolve.TickCount;
                allyGo.transform.position = healerGo.transform.position + new Vector3(ItemCatalog.MeleeRange + 2f, 0f, 0f);
                var ranged = world.TryCurePoison(healer, ally);
                if (ranged.Applied || ranged.FailReason != "range")
                    throw new InvalidOperationException("먼 거리 해독은 range 실패여야 합니다.");

                // no bandage
                allyGo.transform.position = healerGo.transform.position;
                ally.PoisonTicks = PoisoningResolve.TickCount;
                for (int i = bag.Items.Count - 1; i >= 0; i--)
                    if (bag.Items[i].TemplateId == ItemCatalog.Bandage)
                        bag.Items.RemoveAt(i);
                var noBag = world.TryCurePoison(healer, ally);
                if (noBag.Applied || noBag.FailReason != "no_bandage")
                    throw new InvalidOperationException("붕대 없으면 no_bandage여야 합니다.");
                if (ally.PoisonTicks != PoisoningResolve.TickCount)
                    throw new InvalidOperationException("실패한 해독은 독을 지우면 안 됩니다.");

                // ghost target
                bag.Add(ItemCatalog.Bandage, 1);
                ally.Ghost = true;
                ally.SetHp(0f);
                ally.Ghost = true;
                ally.PoisonTicks = PoisoningResolve.TickCount;
                var gh = world.TryCurePoison(healer, ally);
                if (gh.Applied || gh.FailReason != "ghost")
                    throw new InvalidOperationException("유령 대상 서버 해독은 ghost 실패여야 합니다.");

                AssertDungeon3Leftover("붕대 해독 후");
                world.ResetHousePlot();
            }
            finally
            {
                OfflineWorld.Instance?.ResetHousePlot();
                UnityEngine.Object.DestroyImmediate(healerGo);
                UnityEngine.Object.DestroyImmediate(allyGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }
    }
}
