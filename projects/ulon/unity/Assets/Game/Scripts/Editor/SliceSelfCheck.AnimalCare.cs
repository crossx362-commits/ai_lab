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
        // **짐승을 다루는 손**(랩 ㉫) — 동물지식·수의. 길들임·치료처럼 「짐승이 상대」인 검사만 담는다.
        // 안 담는 것: 사람을 상대하는 기술 전부.
        static void AssertAnimalLoreSlice()
        {
            if (SkillId.AnimalLore == SkillId.Tracking)
                throw new InvalidOperationException("동물지식 SkillId는 추적과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.AnimalLore) != StatId.Int)
                throw new InvalidOperationException("동물지식 Primary는 INT이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.AnimalLore) != "동물지식" || SkillTitles.JobOf(SkillId.AnimalLore) != "동물학자")
                throw new InvalidOperationException("동물지식 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Tracking) != "추적")
                throw new InvalidOperationException("추적 스킬명을 바꾸면 안 됩니다.");
            if (MobCatalog.TamableOf(MobCatalog.Bandit) || MobCatalog.TamableOf("wolf"))
                throw new InvalidOperationException("동물지식은 조련 가능으로 표시하면 안 됩니다.");

            var noneSkills = new SkillSet();
            var none = AnimalLoreResolve.Resolve(new AnimalLoreRequest
            {
                Now = 1f,
                Skills = noneSkills,
                HasTarget = false
            });
            if (none.Applied)
                throw new InvalidOperationException("대상 없는 동물지식은 실패해야 합니다.");
            if (Math.Abs(noneSkills.Get(SkillId.AnimalLore)) > 0.0001f)
                throw new InvalidOperationException("실패한 동물지식은 스킬을 올리면 안 됩니다.");

            var playerSkills = new SkillSet();
            var player = AnimalLoreResolve.Resolve(new AnimalLoreRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = playerSkills,
                HasTarget = true,
                TargetEnemy = false,
                TargetAlive = true,
                TargetKind = "플레이어"
            });
            if (player.Applied || player.FailReason != "not_mob")
                throw new InvalidOperationException("플레이어 대상 동물지식은 실패해야 합니다(조련/펫 아님).");

            var farSkills = new SkillSet();
            var far = AnimalLoreResolve.Resolve(new AnimalLoreRequest
            {
                Distance = 20f,
                Now = 1f,
                Skills = farSkills,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                TargetKind = "도적"
            });
            if (far.Applied)
                throw new InvalidOperationException("사거리 밖 동물지식은 들어가면 안 됩니다.");

            var deadSkills = new SkillSet();
            var dead = AnimalLoreResolve.Resolve(new AnimalLoreRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = deadSkills,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = false,
                TargetKind = "도적"
            });
            if (dead.Applied)
                throw new InvalidOperationException("죽은 몹 동물지식은 실패해야 합니다(시체 추적이 아님).");

            MobCatalog.LoreStats(MobCatalog.Bandit, out int bStr, out int bRes, out int bMin, out int bMax);
            var skills = new SkillSet();
            var stats = new StatSet();
            int intWas = stats.Int;
            int dexWas = stats.Dex;
            var ok = AnimalLoreResolve.Resolve(new AnimalLoreRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = skills,
                Stats = stats,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                TargetKind = "도적",
                MobId = MobCatalog.Bandit,
                Hp = 33f,
                MaxHp = 45f,
                Str = bStr,
                Resist = bRes,
                DamageMin = bMin,
                DamageMax = bMax,
                Tamable = true,
                Difficulty = AnimalLoreResolve.Difficulty
            });
            if (!ok.Applied || ok.Kind != "도적" || Math.Abs(ok.Hp - 33f) > 0.0001f || Math.Abs(ok.MaxHp - 45f) > 0.0001f)
                throw new InvalidOperationException("동물지식은 종류/HP를 밝혀야 합니다.");
            if (ok.Str != 28 || ok.Resist != 1 || ok.DamageBand != "4-8")
                throw new InvalidOperationException("동물지식은 추적보다 STR/저항/피해밴드를 더 줘야 합니다.");
            if (ok.Tamable)
                throw new InvalidOperationException("동물지식 결과는 조련불가여야 합니다.");
            if (Math.Abs(skills.Get(SkillId.AnimalLore) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 동물지식 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Tracking)) > 0.0001f)
                throw new InvalidOperationException("동물지식은 추적을 올리면 안 됩니다.");
            if (stats.Int != intWas + 1)
                throw new InvalidOperationException("동물지식 상승 시 INT가 올라야 합니다.");
            if (stats.Dex != dexWas)
                throw new InvalidOperationException("동물지식은 DEX를 올리면 안 됩니다.");

            var trackSkills = new SkillSet();
            var track = TrackingResolve.Resolve(new TrackingRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = trackSkills,
                HasTarget = true,
                TargetAlive = true,
                TargetKind = "도적",
                Hp = 45f,
                MaxHp = 45f
            });
            if (!track.Applied)
                throw new InvalidOperationException("추적 대조 실패");
            if (Math.Abs(trackSkills.Get(SkillId.AnimalLore)) > 0.0001f)
                throw new InvalidOperationException("추적은 동물지식을 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.AnimalLore, SkillLock.Locked);
            var lockedOk = AnimalLoreResolve.Resolve(new AnimalLoreRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = locked,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                TargetKind = "도적",
                Hp = 12f,
                MaxHp = 45f,
                Str = 28,
                Resist = 1,
                DamageMin = 4,
                DamageMax = 8
            });
            if (!lockedOk.Applied || lockedOk.Kind != "도적" || lockedOk.Tamable)
                throw new InvalidOperationException("잠긴 동물지식도 정보는 보여야 하고 조련불가야 합니다.");
            if (Math.Abs(locked.Get(SkillId.AnimalLore)) > 0.0001f)
                throw new InvalidOperationException("잠긴 동물지식은 오르면 안 됩니다.");

            var go = new GameObject("selfcheck-lore");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            GameObject playerGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-lore-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;

                var missing = world.TryLore(body, null);
                if (missing.Applied)
                    throw new InvalidOperationException("서버 대상 없는 동물지식은 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.AnimalLore)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 동물지식은 스킬을 올리면 안 됩니다.");

                playerGo = new GameObject("selfcheck-lore-player");
                playerGo.transform.position = go.transform.position;
                var other = playerGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.IsEnemy = false;
                other.DisplayName = "다른이";
                var pHit = world.TryLore(body, other);
                if (pHit.Applied)
                    throw new InvalidOperationException("서버 동물지식은 플레이어를 살피면 안 됩니다.");

                tgtGo = new GameObject("selfcheck-lore-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MobId = "bandit";
                tgt.DisplayName = "도적";
                tgt.MaxHp = 45f;
                tgt.ResetHp();
                tgt.SetHp(33f);

                var hit = world.TryLore(body, tgt);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 동물지식 실패: " + hit.FailReason);
                if (hit.Kind != "도적" || Math.Abs(hit.Hp - 33f) > 0.0001f)
                    throw new InvalidOperationException("서버 동물지식은 종류/HP를 밝혀야 합니다.");
                if (hit.Str != 28 || hit.Resist != 1 || hit.DamageBand != "4-8" || hit.Tamable)
                    throw new InvalidOperationException("서버 동물지식은 STR/저항/피해밴드와 조련불가를 줘야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.AnimalLore) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 동물지식 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Tracking)) > 0.0001f)
                    throw new InvalidOperationException("서버 동물지식은 추적을 올리면 안 됩니다.");
                if (string.IsNullOrEmpty(world.LastLoreMessage) || world.LastLoreMessage.IndexOf("도적", StringComparison.Ordinal) < 0
                    || world.LastLoreMessage.IndexOf("HP", StringComparison.Ordinal) < 0
                    || world.LastLoreMessage.IndexOf("STR", StringComparison.Ordinal) < 0
                    || world.LastLoreMessage.IndexOf("저항", StringComparison.Ordinal) < 0
                    || world.LastLoreMessage.IndexOf("4-8", StringComparison.Ordinal) < 0
                    || world.LastLoreMessage.IndexOf("조련불가", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("동물지식 메시지가 추적보다 많은 정보를 포함해야 합니다: " + world.LastLoreMessage);
                if (!string.IsNullOrEmpty(world.LastTrackMessage) && world.LastTrackMessage.IndexOf("조련불가", StringComparison.Ordinal) >= 0)
                    throw new InvalidOperationException("추적 메시지에 동물지식 정보가 섞이면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (playerGo != null)
                    UnityEngine.Object.DestroyImmediate(playerGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertVeterinarySlice()
        {
            if (SkillId.Veterinary == SkillId.Healing)
                throw new InvalidOperationException("수의학 SkillId는 치유와 달라야 합니다.");
            if (SkillId.Veterinary == SkillId.AnimalLore)
                throw new InvalidOperationException("수의학 SkillId는 동물지식과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Veterinary) != StatId.Dex)
                throw new InvalidOperationException("수의학 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Veterinary) != "수의학" || SkillTitles.JobOf(SkillId.Veterinary) != "수의사")
                throw new InvalidOperationException("수의학 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Healing) != "치유")
                throw new InvalidOperationException("치유 스킬명을 바꾸면 안 됩니다.");

            var noneSkills = new SkillSet();
            var none = VeterinaryResolve.Resolve(new VeterinaryRequest
            {
                Now = 1f,
                Skills = noneSkills,
                HasBandage = true,
                HasTarget = false,
                TargetHp = 10f,
                TargetMaxHp = 45f
            });
            if (none.Applied)
                throw new InvalidOperationException("대상 없는 수의학은 실패해야 합니다.");
            if (Math.Abs(noneSkills.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("실패한 수의학은 스킬을 올리면 안 됩니다.");

            var playerSkills = new SkillSet();
            var player = VeterinaryResolve.Resolve(new VeterinaryRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = playerSkills,
                HasBandage = true,
                HasTarget = true,
                TargetEnemy = false,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 50f
            });
            if (player.Applied || player.FailReason != "not_mob")
                throw new InvalidOperationException("플레이어/아군 대상 수의학은 실패해야 합니다(치유와 구분).");

            var farSkills = new SkillSet();
            var far = VeterinaryResolve.Resolve(new VeterinaryRequest
            {
                Distance = 20f,
                Now = 1f,
                Skills = farSkills,
                HasBandage = true,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 45f
            });
            if (far.Applied)
                throw new InvalidOperationException("사거리 밖 수의학은 들어가면 안 됩니다.");

            var deadSkills = new SkillSet();
            var dead = VeterinaryResolve.Resolve(new VeterinaryRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = deadSkills,
                HasBandage = true,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = false,
                TargetHp = 0f,
                TargetMaxHp = 45f
            });
            if (dead.Applied)
                throw new InvalidOperationException("죽은 몹 수의학은 실패해야 합니다.");

            var noBnSkills = new SkillSet();
            var noBn = VeterinaryResolve.Resolve(new VeterinaryRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = noBnSkills,
                HasBandage = false,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 45f
            });
            if (noBn.Applied)
                throw new InvalidOperationException("붕대 없이 수의학되면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            int intWas = stats.Int;
            var ok = VeterinaryResolve.Resolve(new VeterinaryRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = skills,
                Stats = stats,
                HasBandage = true,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 45f,
                Difficulty = VeterinaryResolve.Difficulty
            });
            if (!ok.Applied || ok.Damage < 1)
                throw new InvalidOperationException("수의학은 산 몹을 치료해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Veterinary) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 수의학 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Healing)) > 0.0001f)
                throw new InvalidOperationException("수의학은 치유를 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.AnimalLore)) > 0.0001f)
                throw new InvalidOperationException("수의학은 동물지식을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("수의학 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != intWas)
                throw new InvalidOperationException("수의학은 INT를 올리면 안 됩니다.");

            var healSkills = new SkillSet();
            var heal = HealResolve.Resolve(new HealRequest
            {
                Distance = 0f,
                Now = 1f,
                Skills = healSkills,
                HasBandage = true,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 50f
            });
            if (!heal.Applied)
                throw new InvalidOperationException("치유 대조 실패");
            if (Math.Abs(healSkills.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("치유는 수의학을 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Veterinary, SkillLock.Locked);
            var lockedOk = VeterinaryResolve.Resolve(new VeterinaryRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = locked,
                HasBandage = true,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                TargetHp = 10f,
                TargetMaxHp = 45f
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 수의학도 치료는 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("잠긴 수의학은 오르면 안 됩니다.");

            var created = CharacterCreate.Build("vet-check", "수의", 0, 20, 40, 20,
                new[] { SkillId.Veterinary, SkillId.Healing, SkillId.Tailoring },
                new[] { 50f, 30f, 20f });
            bool hasBn = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Bandage && created.Inventory[i].Amount >= 10)
                    hasBn = true;
            }
            if (!hasBn)
                throw new InvalidOperationException("수의학 시작은 붕대를 줘야 합니다.");

            var go = new GameObject("selfcheck-vet");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            GameObject playerGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-vet-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var bag = go.AddComponent<InventoryBag>();

                var missing = world.TryVet(body, null);
                if (missing.Applied)
                    throw new InvalidOperationException("서버 대상 없는 수의학은 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Veterinary)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 수의학은 스킬을 올리면 안 됩니다.");

                playerGo = new GameObject("selfcheck-vet-player");
                playerGo.transform.position = go.transform.position;
                var other = playerGo.AddComponent<WorldBody>();
                other.IsAvatar = true;
                other.IsEnemy = false;
                other.MaxHp = 50f;
                other.ResetHp();
                other.SetHp(20f);
                bag.Add(ItemCatalog.Bandage, 3);
                var pHit = world.TryVet(body, other);
                if (pHit.Applied)
                    throw new InvalidOperationException("서버 수의학은 플레이어를 치료하면 안 됩니다(치유 영역).");

                tgtGo = new GameObject("selfcheck-vet-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MobId = "bandit";
                tgt.DisplayName = "도적";
                tgt.MaxHp = 45f;
                tgt.ResetHp();
                tgt.SetHp(10f);

                var hit = world.TryVet(body, tgt);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 수의학 실패: " + hit.FailReason);
                if (tgt.Hp <= 10f)
                    throw new InvalidOperationException("서버 수의학은 산 몹 HP를 올려야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Veterinary) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 수의학 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Healing)) > 0.0001f)
                    throw new InvalidOperationException("서버 수의학은 치유를 올리면 안 됩니다.");
                int leftBn = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Bandage)
                        leftBn += bag.Items[i].Amount;
                if (leftBn != 2)
                    throw new InvalidOperationException("성공 수의학은 붕대를 소모해야 합니다.");
                if (string.IsNullOrEmpty(world.LastVetMessage) || world.LastVetMessage.IndexOf("도적", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("수의학 메시지가 있어야 합니다: " + world.LastVetMessage);

                var foeHeal = world.TryHeal(body, tgt);
                if (foeHeal.Applied)
                    throw new InvalidOperationException("치유는 여전히 적 몹에 들어가면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (playerGo != null)
                    UnityEngine.Object.DestroyImmediate(playerGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }
    }
}
