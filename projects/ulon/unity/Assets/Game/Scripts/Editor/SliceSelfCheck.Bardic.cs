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
        // **소리로 마음을 움직이는 것**(랩 ㉱) — 악기·평화·도발.
        // 담는 것: 싸우지 않고 상대의 태도를 바꾸는 기술. 안 담는 것: 정신 수련(`MindSkills`)·활과 추적(본체).
        static void AssertMusicianshipSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Musicianship) != StatId.Dex)
                throw new InvalidOperationException("음악 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Musicianship) != "음악" || SkillTitles.JobOf(SkillId.Musicianship) != "음악가")
                throw new InvalidOperationException("음악 스킬명/직업명이 기획과 같아야 합니다.");
            var rec = CraftRecipes.Find("lute");
            if (rec == null || rec.Ingredient != "wood" || rec.Output != ItemCatalog.Lute
                || rec.Skill != SkillId.Carpentry || rec.Count != 2)
                throw new InvalidOperationException("나무 2 → 류트 레시피가 있어야 합니다.");
            if (ItemCatalog.BuyPrice(ItemCatalog.Lute) <= 0 || ItemCatalog.WeightOf(ItemCatalog.Lute) <= 0f)
                throw new InvalidOperationException("류트 무게/상점 가격이 없습니다.");
            if (ItemCatalog.MaxUsesOf(ItemCatalog.Lute) <= 0)
                throw new InvalidOperationException("류트 내구도가 있어야 합니다.");

            var noneSkills = new SkillSet();
            var none = MusicianshipResolve.Resolve(new MusicianshipRequest
            {
                Now = 1f,
                Skills = noneSkills,
                HasInstrument = false
            });
            if (none.Applied)
                throw new InvalidOperationException("악기 없는 연주는 실패해야 합니다.");
            if (Math.Abs(noneSkills.Get(SkillId.Musicianship)) > 0.0001f)
                throw new InvalidOperationException("실패한 연주는 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = MusicianshipResolve.Resolve(new MusicianshipRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                HasInstrument = true,
                Difficulty = MusicianshipResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("악기 연주는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Musicianship) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 연주 후 0.1이어야 합니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("음악 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != 25)
                throw new InvalidOperationException("음악은 INT를 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Musicianship, SkillLock.Locked);
            var lockedOk = MusicianshipResolve.Resolve(new MusicianshipRequest
            {
                Now = 1f,
                Skills = locked,
                HasInstrument = true
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 음악도 연주는 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Musicianship)) > 0.0001f)
                throw new InvalidOperationException("잠긴 음악은 오르면 안 됩니다.");

            var track = new SkillSet();
            TrackingResolve.Resolve(new TrackingRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = track,
                HasTarget = true,
                TargetAlive = true,
                TargetKind = "도적"
            });
            if (Math.Abs(track.Get(SkillId.Musicianship)) > 0.0001f)
                throw new InvalidOperationException("추적은 음악을 올리면 안 됩니다.");

            var created = CharacterCreate.Build("music-check", "음악가", 0, 20, 40, 20,
                new[] { SkillId.Musicianship, SkillId.Carpentry, SkillId.Tactics },
                new[] { 50f, 30f, 20f });
            bool hasLute = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
            }
            if (!hasLute)
                throw new InvalidOperationException("음악 시작은 류트를 줘야 합니다.");

            var go = new GameObject("selfcheck-music");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            GameObject farGo = null;
            GameObject stGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-music-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();
                var bag = go.AddComponent<InventoryBag>();

                var missing = world.TryPlay(body);
                if (missing.Applied)
                    throw new InvalidOperationException("서버 악기 없는 연주는 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Musicianship)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 연주는 스킬을 올리면 안 됩니다.");

                stGo = new GameObject("selfcheck-music-st");
                stGo.transform.position = go.transform.position;
                var station = stGo.AddComponent<CraftStation>();
                station.RecipeId = "wooden_club";
                station.DisplayName = "목공소";
                bag.Add("wood", 2);
                var crafted = world.TryCraft(body, station, "lute");
                if (!crafted.Applied)
                    throw new InvalidOperationException("목공 류트 제작 실패: " + crafted.FailReason);
                if (!ItemCatalog.Has(bag.Items, ItemCatalog.Lute))
                    throw new InvalidOperationException("나무 2 → 류트 1이어야 합니다.");

                tgtGo = new GameObject("selfcheck-music-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MobId = "bandit";
                tgt.DisplayName = "도적";
                tgt.MaxHp = 45f;
                tgt.ResetHp();

                farGo = new GameObject("selfcheck-music-far");
                farGo.transform.position = go.transform.position + new Vector3(20f, 0f, 0f);
                var far = farGo.AddComponent<WorldBody>();
                far.IsEnemy = true;
                far.MaxHp = 45f;
                far.ResetHp();

                var hit = world.TryPlay(body);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 연주 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Musicianship) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 연주 후 서버 스킬 0.1이어야 합니다.");
                if (hit.Calmed < 1)
                    throw new InvalidOperationException("가까운 적은 살짝 진정되어야 합니다.");
                if (tgt.CalmUntil <= Time.time)
                    throw new InvalidOperationException("연주 사거리 안 적은 CalmUntil이 있어야 합니다.");
                if (far.CalmUntil > Time.time)
                    throw new InvalidOperationException("사거리 밖 적은 진정되면 안 됩니다.");
                if (string.IsNullOrEmpty(world.LastPlayMessage) || world.LastPlayMessage.IndexOf("진정", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("연주 메시지가 진정을 포함해야 합니다.");

                float hpWas = body.Hp;
                var melee = world.TryAttack(body, tgt);
                if (!melee.Applied)
                    throw new InvalidOperationException("진정 중 근접 공격 실패: " + melee.FailReason);
                if (body.Hp < hpWas - 0.01f)
                    throw new InvalidOperationException("작은 진정은 반격을 막아야 합니다(전체 평화 아님).");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (farGo != null)
                    UnityEngine.Object.DestroyImmediate(farGo);
                if (stGo != null)
                    UnityEngine.Object.DestroyImmediate(stGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }



        static void AssertPeacemakingSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Peacemaking) != StatId.Dex)
                throw new InvalidOperationException("평화 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Peacemaking) != "평화" || SkillTitles.JobOf(SkillId.Peacemaking) != "평화사")
                throw new InvalidOperationException("평화 스킬명/직업명이 기획과 같아야 합니다.");

            var noneSkills = new SkillSet();
            var none = PeacemakingResolve.Resolve(new PeacemakingRequest
            {
                Now = 1f,
                Skills = noneSkills,
                HasInstrument = false,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                Distance = 1f
            });
            if (none.Applied)
                throw new InvalidOperationException("악기 없는 평화는 실패해야 합니다.");
            if (Math.Abs(noneSkills.Get(SkillId.Peacemaking)) > 0.0001f)
                throw new InvalidOperationException("실패한 평화는 스킬을 올리면 안 됩니다.");

            var noTgt = PeacemakingResolve.Resolve(new PeacemakingRequest
            {
                Now = 1f,
                Skills = new SkillSet(),
                HasInstrument = true,
                HasTarget = false,
                TargetEnemy = true,
                TargetAlive = true
            });
            if (noTgt.Applied)
                throw new InvalidOperationException("대상 없는 평화는 실패해야 합니다.");

            var pvp = PeacemakingResolve.Resolve(new PeacemakingRequest
            {
                Now = 1f,
                Skills = new SkillSet(),
                HasInstrument = true,
                HasTarget = true,
                TargetEnemy = false,
                TargetAlive = true,
                Distance = 1f
            });
            if (pvp.Applied)
                throw new InvalidOperationException("플레이어 대상 평화는 안 됩니다(Open PvP 아님).");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = PeacemakingResolve.Resolve(new PeacemakingRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                HasInstrument = true,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                Distance = 1f,
                Difficulty = PeacemakingResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("대상 몹 평화는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Peacemaking) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 평화 후 0.1이어야 합니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("평화 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != 25)
                throw new InvalidOperationException("평화는 INT를 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Peacemaking, SkillLock.Locked);
            var lockedOk = PeacemakingResolve.Resolve(new PeacemakingRequest
            {
                Now = 1f,
                Skills = locked,
                HasInstrument = true,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                Distance = 1f
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 평화도 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Peacemaking)) > 0.0001f)
                throw new InvalidOperationException("잠긴 평화는 오르면 안 됩니다.");

            var music = new SkillSet();
            MusicianshipResolve.Resolve(new MusicianshipRequest
            {
                Now = 1f,
                Skills = music,
                HasInstrument = true
            });
            if (Math.Abs(music.Get(SkillId.Peacemaking)) > 0.0001f)
                throw new InvalidOperationException("음악은 평화를 올리면 안 됩니다.");

            var created = CharacterCreate.Build("peace-check", "평화사", 0, 20, 40, 20,
                new[] { SkillId.Peacemaking, SkillId.Musicianship, SkillId.Tactics },
                new[] { 50f, 30f, 20f });
            bool hasLute = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
            }
            if (!hasLute)
                throw new InvalidOperationException("평화 시작은 류트를 줘야 합니다.");

            var go = new GameObject("selfcheck-peace");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            GameObject otherGo = null;
            GameObject palGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-peace-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();
                var bag = go.AddComponent<InventoryBag>();

                tgtGo = new GameObject("selfcheck-peace-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MobId = "bandit";
                tgt.DisplayName = "도적";
                tgt.MaxHp = 45f;
                tgt.ResetHp();

                var missing = world.TryPeace(body, tgt);
                if (missing.Applied)
                    throw new InvalidOperationException("서버 악기 없는 평화는 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Peacemaking)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 평화는 스킬을 올리면 안 됩니다.");

                bag.Add(ItemCatalog.Lute, 1);

                palGo = new GameObject("selfcheck-peace-pal");
                palGo.transform.position = go.transform.position;
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsAvatar = true;
                pal.IsEnemy = false;
                pal.MaxHp = 50f;
                pal.ResetHp();
                var onPlayer = world.TryPeace(body, pal);
                if (onPlayer.Applied)
                    throw new InvalidOperationException("서버 평화는 플레이어를 대상으로 하면 안 됩니다.");

                otherGo = new GameObject("selfcheck-peace-other");
                otherGo.transform.position = go.transform.position;
                var other = otherGo.AddComponent<WorldBody>();
                other.IsEnemy = true;
                other.DisplayName = "졸병";
                other.MaxHp = 30f;
                other.ResetHp();

                var hit = world.TryPeace(body, tgt);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 평화 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Peacemaking) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 평화 후 서버 스킬 0.1이어야 합니다.");
                if (tgt.CalmUntil <= Time.time)
                    throw new InvalidOperationException("평화 대상은 CalmUntil이 있어야 합니다.");
                if (other.CalmUntil > Time.time)
                    throw new InvalidOperationException("평화는 대상 한 몹만 멈춰야 합니다.");
                if (string.IsNullOrEmpty(world.LastPeaceMessage) || world.LastPeaceMessage.IndexOf("평화", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("평화 메시지가 있어야 합니다.");

                float hpWas = body.Hp;
                var melee = world.TryAttack(body, tgt);
                if (!melee.Applied)
                    throw new InvalidOperationException("평화 중 근접 공격 실패: " + melee.FailReason);
                if (body.Hp < hpWas - 0.01f)
                    throw new InvalidOperationException("평화는 대상 몹 반격을 막아야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (otherGo != null)
                    UnityEngine.Object.DestroyImmediate(otherGo);
                if (palGo != null)
                    UnityEngine.Object.DestroyImmediate(palGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertProvocationSlice()
        {
            if (SkillId.Provocation == SkillId.Peacemaking)
                throw new InvalidOperationException("도발 SkillId는 평화와 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Provocation) != StatId.Dex)
                throw new InvalidOperationException("도발 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Provocation) != "도발" || SkillTitles.JobOf(SkillId.Provocation) != "도발사")
                throw new InvalidOperationException("도발 스킬명/직업명이 기획과 같아야 합니다.");

            var noneSkills = new SkillSet();
            var none = ProvocationResolve.Resolve(new ProvocationRequest
            {
                Now = 1f,
                Skills = noneSkills,
                HasInstrument = false,
                HasTargetA = true,
                HasTargetB = true,
                TargetAEnemy = true,
                TargetBEnemy = true,
                TargetAAlive = true,
                TargetBAlive = true,
                DistanceA = 1f,
                DistanceB = 1f
            });
            if (none.Applied)
                throw new InvalidOperationException("악기 없는 도발은 실패해야 합니다.");
            if (Math.Abs(noneSkills.Get(SkillId.Provocation)) > 0.0001f)
                throw new InvalidOperationException("실패한 도발은 스킬을 올리면 안 됩니다.");

            var noTgt = ProvocationResolve.Resolve(new ProvocationRequest
            {
                Now = 1f,
                Skills = new SkillSet(),
                HasInstrument = true,
                HasTargetA = true,
                HasTargetB = false,
                TargetAEnemy = true,
                TargetBEnemy = true,
                TargetAAlive = true,
                TargetBAlive = true
            });
            if (noTgt.Applied)
                throw new InvalidOperationException("대상 하나뿐인 도발은 실패해야 합니다.");

            var same = ProvocationResolve.Resolve(new ProvocationRequest
            {
                Now = 1f,
                Skills = new SkillSet(),
                HasInstrument = true,
                HasTargetA = true,
                HasTargetB = true,
                SameTarget = true,
                TargetAEnemy = true,
                TargetBEnemy = true,
                TargetAAlive = true,
                TargetBAlive = true,
                DistanceA = 1f,
                DistanceB = 1f
            });
            if (same.Applied)
                throw new InvalidOperationException("같은 대상 둘은 도발 실패해야 합니다.");

            var pvp = ProvocationResolve.Resolve(new ProvocationRequest
            {
                Now = 1f,
                Skills = new SkillSet(),
                HasInstrument = true,
                HasTargetA = true,
                HasTargetB = true,
                TargetAEnemy = false,
                TargetBEnemy = true,
                TargetAAlive = true,
                TargetBAlive = true,
                DistanceA = 1f,
                DistanceB = 1f
            });
            if (pvp.Applied)
                throw new InvalidOperationException("플레이어 대상 도발은 안 됩니다.");

            var pvp2 = ProvocationResolve.Resolve(new ProvocationRequest
            {
                Now = 1f,
                Skills = new SkillSet(),
                HasInstrument = true,
                HasTargetA = true,
                HasTargetB = true,
                TargetAEnemy = true,
                TargetBEnemy = false,
                TargetAAlive = true,
                TargetBAlive = true,
                DistanceA = 1f,
                DistanceB = 1f
            });
            if (pvp2.Applied)
                throw new InvalidOperationException("두 번째가 플레이어면 도발은 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = ProvocationResolve.Resolve(new ProvocationRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                HasInstrument = true,
                HasTargetA = true,
                HasTargetB = true,
                TargetAEnemy = true,
                TargetBEnemy = true,
                TargetAAlive = true,
                TargetBAlive = true,
                DistanceA = 1f,
                DistanceB = 1f,
                Difficulty = ProvocationResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("몹 둘 도발은 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Provocation) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 도발 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Peacemaking)) > 0.0001f)
                throw new InvalidOperationException("도발은 평화를 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("도발 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != 25)
                throw new InvalidOperationException("도발은 INT를 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Provocation, SkillLock.Locked);
            var lockedOk = ProvocationResolve.Resolve(new ProvocationRequest
            {
                Now = 1f,
                Skills = locked,
                HasInstrument = true,
                HasTargetA = true,
                HasTargetB = true,
                TargetAEnemy = true,
                TargetBEnemy = true,
                TargetAAlive = true,
                TargetBAlive = true,
                DistanceA = 1f,
                DistanceB = 1f
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 도발도 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Provocation)) > 0.0001f)
                throw new InvalidOperationException("잠긴 도발은 오르면 안 됩니다.");

            var peace = new SkillSet();
            PeacemakingResolve.Resolve(new PeacemakingRequest
            {
                Now = 1f,
                Skills = peace,
                HasInstrument = true,
                HasTarget = true,
                TargetEnemy = true,
                TargetAlive = true,
                Distance = 1f
            });
            if (Math.Abs(peace.Get(SkillId.Provocation)) > 0.0001f)
                throw new InvalidOperationException("평화는 도발을 올리면 안 됩니다.");

            var created = CharacterCreate.Build("provoke-check", "도발사", 0, 20, 40, 20,
                new[] { SkillId.Provocation, SkillId.Musicianship, SkillId.Tactics },
                new[] { 50f, 30f, 20f });
            bool hasLute = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
            }
            if (!hasLute)
                throw new InvalidOperationException("도발 시작은 류트를 줘야 합니다.");

            var go = new GameObject("selfcheck-provoke");
            GameObject worldGo = null;
            GameObject aGo = null;
            GameObject bGo = null;
            GameObject palGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-provoke-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();
                var bag = go.AddComponent<InventoryBag>();

                aGo = new GameObject("selfcheck-provoke-a");
                aGo.transform.position = go.transform.position;
                var a = aGo.AddComponent<WorldBody>();
                a.IsEnemy = true;
                a.MobId = "bandit";
                a.DisplayName = "도적";
                a.MaxHp = 45f;
                a.ResetHp();

                bGo = new GameObject("selfcheck-provoke-b");
                bGo.transform.position = go.transform.position;
                var b = bGo.AddComponent<WorldBody>();
                b.IsEnemy = true;
                b.DisplayName = "졸병";
                b.MaxHp = 30f;
                b.ResetHp();

                var missing = world.TryProvoke(body, a, b);
                if (missing.Applied)
                    throw new InvalidOperationException("서버 악기 없는 도발은 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Provocation)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 도발은 스킬을 올리면 안 됩니다.");

                bag.Add(ItemCatalog.Lute, 1);

                palGo = new GameObject("selfcheck-provoke-pal");
                palGo.transform.position = go.transform.position;
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsAvatar = true;
                pal.IsEnemy = false;
                pal.MaxHp = 50f;
                pal.ResetHp();
                var onPlayer = world.TryProvoke(body, a, pal);
                if (onPlayer.Applied)
                    throw new InvalidOperationException("서버 도발은 플레이어를 대상으로 하면 안 됩니다.");
                var onPlayer2 = world.TryProvoke(body, pal, b);
                if (onPlayer2.Applied)
                    throw new InvalidOperationException("서버 도발은 첫 대상이 플레이어면 안 됩니다.");

                float playerHp = body.Hp;
                float aHp = a.Hp;
                float bHp = b.Hp;
                var hit = world.TryProvoke(body, a, b);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 도발 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Provocation) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 도발 후 서버 스킬 0.1이어야 합니다.");
                if (a.ProvokeUntil <= Time.time || b.ProvokeUntil <= Time.time)
                    throw new InvalidOperationException("도발 대상은 ProvokeUntil이 있어야 합니다.");
                if (a.ProvokePartner != b || b.ProvokePartner != a)
                    throw new InvalidOperationException("도발은 두 몹이 서로를 상대로 싸워야 합니다.");
                if (string.IsNullOrEmpty(world.LastProvokeMessage) || world.LastProvokeMessage.IndexOf("도발", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("도발 메시지가 있어야 합니다.");

                world.TickProvoke(Time.time);
                if (a.Hp >= aHp - 0.01f && b.Hp >= bHp - 0.01f)
                    throw new InvalidOperationException("도발 후 두 몹이 서로 싸워야 합니다.");
                if (body.Hp < playerHp - 0.01f)
                    throw new InvalidOperationException("도발은 플레이어를 때리면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (aGo != null)
                    UnityEngine.Object.DestroyImmediate(aGo);
                if (bGo != null)
                    UnityEngine.Object.DestroyImmediate(bGo);
                if (palGo != null)
                    UnityEngine.Object.DestroyImmediate(palGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }
    }
}
