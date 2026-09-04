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
        static void AssertHidingSlice()
        {
            if (SkillId.Hiding == SkillId.Provocation)
                throw new InvalidOperationException("은신 SkillId는 도발과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Hiding) != StatId.Dex)
                throw new InvalidOperationException("은신 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Hiding) != "은신" || SkillTitles.JobOf(SkillId.Hiding) != "은신자")
                throw new InvalidOperationException("은신 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Hiding) == "잠행")
                throw new InvalidOperationException("은신은 잠행이 아닙니다.");

            var ghostSkills = new SkillSet();
            var ghost = HidingResolve.Resolve(new HidingRequest { Now = 1f, Skills = ghostSkills, Ghost = true });
            if (ghost.Applied)
                throw new InvalidOperationException("유령 은신은 실패해야 합니다.");
            if (Math.Abs(ghostSkills.Get(SkillId.Hiding)) > 0.0001f)
                throw new InvalidOperationException("실패한 은신은 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = HidingResolve.Resolve(new HidingRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                Difficulty = HidingResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("은신은 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Hiding) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 은신 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Provocation)) > 0.0001f)
                throw new InvalidOperationException("은신은 도발을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("은신 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != 25)
                throw new InvalidOperationException("은신은 INT를 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Hiding, SkillLock.Locked);
            var lockedOk = HidingResolve.Resolve(new HidingRequest { Now = 1f, Skills = locked });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 은신도 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Hiding)) > 0.0001f)
                throw new InvalidOperationException("잠긴 은신은 오르면 안 됩니다.");

            var provoke = new SkillSet();
            ProvocationResolve.Resolve(new ProvocationRequest
            {
                Now = 1f,
                Skills = provoke,
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
            if (Math.Abs(provoke.Get(SkillId.Hiding)) > 0.0001f)
                throw new InvalidOperationException("도발은 은신을 올리면 안 됩니다.");

            var created = CharacterCreate.Build("hide-check", "은신자", 0, 20, 40, 20,
                new[] { SkillId.Hiding, SkillId.Tactics, SkillId.Anatomy },
                new[] { 50f, 30f, 20f });
            bool hasLute = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
            }
            if (hasLute)
                throw new InvalidOperationException("은신 시작은 류트를 주면 안 됩니다.");

            var go = new GameObject("selfcheck-hide");
            GameObject worldGo = null;
            GameObject mobGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-hide-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();

                mobGo = new GameObject("selfcheck-hide-mob");
                mobGo.transform.position = go.transform.position;
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                float hp = body.Hp;
                var miss = world.TryEnemyStrike(mob, body);
                if (!miss)
                    throw new InvalidOperationException("숨지 않은 플레이어는 몹 타격 대상이어야 합니다.");
                if (body.Hp >= hp - 0.01f)
                    throw new InvalidOperationException("숨지 않으면 몹 타격이 들어가야 합니다.");

                body.ResetHp();
                var hit = world.TryHide(body);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 은신 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Hiding) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 은신 후 서버 스킬 0.1이어야 합니다.");
                if (!body.IsHidden(Time.time))
                    throw new InvalidOperationException("은신 후 HiddenUntil이 있어야 합니다.");
                if (string.IsNullOrEmpty(world.LastHideMessage) || world.LastHideMessage.IndexOf("은신", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("은신 메시지가 있어야 합니다.");

                hp = body.Hp;
                var skipped = world.TryEnemyStrike(mob, body);
                if (skipped)
                    throw new InvalidOperationException("은신 중 몹은 플레이어를 타격하면 안 됩니다.");
                if (body.Hp < hp - 0.01f)
                    throw new InvalidOperationException("은신 중 HP가 줄면 안 됩니다.");

                var hideBag = go.AddComponent<InventoryBag>();
                hideBag.Add(ItemCatalog.WoodenClub, 1);
                float mobHp = mob.Hp;
                hp = body.Hp;
                var atk = world.TryAttack(body, mob);
                if (!atk.Applied)
                    throw new InvalidOperationException("은신 중 공격은 성공해야 합니다: " + atk.FailReason);
                if (body.IsHidden(Time.time))
                    throw new InvalidOperationException("공격하면 은신이 풀려야 합니다.");
                if (mob.Hp >= mobHp - 0.01f)
                    throw new InvalidOperationException("공격 후 몹 HP가 줄어야 합니다.");
                if (body.Hp >= hp - 0.01f)
                    throw new InvalidOperationException("은신 해제 후 몹 보복이 들어가야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertStealthSlice()
        {
            if (SkillId.Stealth == SkillId.Hiding)
                throw new InvalidOperationException("잠행 SkillId는 은신과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Stealth) != StatId.Dex)
                throw new InvalidOperationException("잠행 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Stealth) != "잠행" || SkillTitles.JobOf(SkillId.Stealth) != "잠행자")
                throw new InvalidOperationException("잠행 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Hiding) != "은신")
                throw new InvalidOperationException("은신 스킬명을 잠행으로 바꾸면 안 됩니다.");

            var ghostSkills = new SkillSet();
            var ghost = StealthResolve.Resolve(new StealthRequest { Now = 1f, Skills = ghostSkills, Ghost = true, AlreadyHidden = true });
            if (ghost.Applied)
                throw new InvalidOperationException("유령 잠행은 실패해야 합니다.");
            if (Math.Abs(ghostSkills.Get(SkillId.Stealth)) > 0.0001f)
                throw new InvalidOperationException("실패한 잠행은 스킬을 올리면 안 됩니다.");

            var standing = new SkillSet();
            var stand = StealthResolve.Resolve(new StealthRequest { Now = 1f, Skills = standing, AlreadyHidden = false });
            if (stand.Applied)
                throw new InvalidOperationException("숨지 않은 잠행은 실패해야 합니다.");
            if (Math.Abs(standing.Get(SkillId.Stealth)) > 0.0001f)
                throw new InvalidOperationException("숨지 않은 잠행은 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = StealthResolve.Resolve(new StealthRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                AlreadyHidden = true,
                Difficulty = StealthResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("잠행은 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Stealth) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 잠행 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Hiding)) > 0.0001f)
                throw new InvalidOperationException("잠행은 은신을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("잠행 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != 25)
                throw new InvalidOperationException("잠행은 INT를 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Stealth, SkillLock.Locked);
            var lockedOk = StealthResolve.Resolve(new StealthRequest { Now = 1f, Skills = locked, AlreadyHidden = true });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 잠행도 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Stealth)) > 0.0001f)
                throw new InvalidOperationException("잠긴 잠행은 오르면 안 됩니다.");

            var hiding = new SkillSet();
            HidingResolve.Resolve(new HidingRequest { Now = 1f, Skills = hiding });
            if (Math.Abs(hiding.Get(SkillId.Stealth)) > 0.0001f)
                throw new InvalidOperationException("은신은 잠행을 올리면 안 됩니다.");

            var created = CharacterCreate.Build("stealth-check", "잠행자", 0, 20, 40, 20,
                new[] { SkillId.Stealth, SkillId.Tactics, SkillId.Anatomy },
                new[] { 50f, 30f, 20f });
            bool hasLute = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
            }
            if (hasLute)
                throw new InvalidOperationException("잠행 시작은 류트를 주면 안 됩니다.");

            var go = new GameObject("selfcheck-stealth");
            GameObject worldGo = null;
            GameObject mobGo = null;
            GameObject walkGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-stealth-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();

                mobGo = new GameObject("selfcheck-stealth-mob");
                mobGo.transform.position = go.transform.position;
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                walkGo = new GameObject("selfcheck-stealth-walk");
                walkGo.transform.position = go.transform.position;
                var walker = walkGo.AddComponent<WorldBody>();
                walker.IsAvatar = true;
                walker.RecalcFromStr(30);
                walker.ResetHp();

                var miss = world.TryStealth(body);
                if (miss.Applied)
                    throw new InvalidOperationException("서버 잠행은 은신 전에 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Stealth)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 잠행은 스킬을 올리면 안 됩니다.");

                var walkHide = world.TryHide(walker);
                if (!walkHide.Applied)
                    throw new InvalidOperationException("이동 검사 은신 실패: " + walkHide.FailReason);
                walkGo.transform.position += new Vector3(2f, 0f, 0f);
                world.TickHiddenMovement(Time.time);
                if (walker.IsHidden(Time.time))
                    throw new InvalidOperationException("은신만으로는 이동하면 풀려야 합니다.");

                var hide = world.TryHide(body);
                if (!hide.Applied)
                    throw new InvalidOperationException("잠행 전 은신이 필요합니다: " + hide.FailReason);
                var hit = world.TryStealth(body);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 잠행 실패: " + hit.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Stealth) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 잠행 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Hiding) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("잠행은 기존 은신 값을 유지해야 합니다.");
                if (!body.IsHidden(Time.time) || !body.CanMoveHidden(Time.time))
                    throw new InvalidOperationException("잠행 후 이동 가능 은신 상태여야 합니다.");
                if (string.IsNullOrEmpty(world.LastStealthMessage) || world.LastStealthMessage.IndexOf("잠행", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("잠행 메시지가 있어야 합니다.");

                go.transform.position += new Vector3(0.4f, 0f, 0f);
                world.TickHiddenMovement(Time.time);
                if (!body.IsHidden(Time.time))
                    throw new InvalidOperationException("잠행 중 이동해도 은신이 유지되어야 합니다.");

                var hideBag = go.AddComponent<InventoryBag>();
                hideBag.Add(ItemCatalog.WoodenClub, 1);
                float hp = body.Hp;
                var atk = world.TryAttack(body, mob);
                if (!atk.Applied)
                    throw new InvalidOperationException("잠행 중 공격은 성공해야 합니다: " + atk.FailReason);
                if (body.IsHidden(Time.time) || body.CanMoveHidden(Time.time))
                    throw new InvalidOperationException("공격하면 잠행/은신이 풀려야 합니다.");
                if (body.Hp >= hp - 0.01f)
                    throw new InvalidOperationException("잠행 해제 후 몹 보복이 들어가야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (walkGo != null)
                    UnityEngine.Object.DestroyImmediate(walkGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }



        static void AssertDetectHiddenSlice()
        {
            AssertDungeon3Leftover();
            if (SkillId.DetectHidden == SkillId.Hiding)
                throw new InvalidOperationException("감지 SkillId는 은신과 달라야 합니다.");
            if (SkillId.DetectHidden == SkillId.Stealth)
                throw new InvalidOperationException("감지 SkillId는 잠행과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.DetectHidden) != StatId.Dex)
                throw new InvalidOperationException("감지 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.DetectHidden) != "감지" || SkillTitles.JobOf(SkillId.DetectHidden) != "탐지자")
                throw new InvalidOperationException("감지 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Hiding) != "은신" || SkillTitles.JobOf(SkillId.Hiding) != "은신자")
                throw new InvalidOperationException("은신 스킬명/직업명을 바꾸면 안 됩니다.");
            if (SkillNames.KoreanOf(SkillId.Stealth) != "잠행" || SkillTitles.JobOf(SkillId.Stealth) != "잠행자")
                throw new InvalidOperationException("잠행 스킬명/직업명을 바꾸면 안 됩니다.");

            var ghostSkills = new SkillSet();
            var ghost = DetectHiddenResolve.Resolve(new DetectHiddenRequest { Now = 1f, Skills = ghostSkills, Ghost = true });
            if (ghost.Applied)
                throw new InvalidOperationException("유령 감지는 실패해야 합니다.");
            if (Math.Abs(ghostSkills.Get(SkillId.DetectHidden)) > 0.0001f)
                throw new InvalidOperationException("실패한 감지는 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = DetectHiddenResolve.Resolve(new DetectHiddenRequest
            {
                Now = 1f,
                Skills = farSkills,
                Distance = DetectHiddenResolve.DetectRange + 1f,
                Range = DetectHiddenResolve.DetectRange
            });
            if (far.Applied || far.FailReason != "range")
                throw new InvalidOperationException("사거리 밖 감지는 실패해야 합니다.");
            if (Math.Abs(farSkills.Get(SkillId.DetectHidden)) > 0.0001f)
                throw new InvalidOperationException("사거리 밖 감지는 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            int intWas = stats.Int;
            int strWas = stats.Str;
            var ok = DetectHiddenResolve.Resolve(new DetectHiddenRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                Difficulty = DetectHiddenResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("감지는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.DetectHidden) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 감지 후 0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Hiding)) > 0.0001f)
                throw new InvalidOperationException("감지는 은신을 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Stealth)) > 0.0001f)
                throw new InvalidOperationException("감지는 잠행을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("감지 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != intWas)
                throw new InvalidOperationException("감지는 INT를 올리면 안 됩니다.");
            if (stats.Str != strWas)
                throw new InvalidOperationException("감지는 STR을 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.DetectHidden, SkillLock.Locked);
            var lockedOk = DetectHiddenResolve.Resolve(new DetectHiddenRequest { Now = 1f, Skills = locked });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 감지도 적용은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.DetectHidden)) > 0.0001f)
                throw new InvalidOperationException("잠긴 감지는 오르면 안 됩니다.");

            var hiding = new SkillSet();
            HidingResolve.Resolve(new HidingRequest { Now = 1f, Skills = hiding });
            if (Math.Abs(hiding.Get(SkillId.DetectHidden)) > 0.0001f)
                throw new InvalidOperationException("은신은 감지를 올리면 안 됩니다.");
            var stealth = new SkillSet();
            StealthResolve.Resolve(new StealthRequest { Now = 1f, Skills = stealth, AlreadyHidden = true });
            if (Math.Abs(stealth.Get(SkillId.DetectHidden)) > 0.0001f)
                throw new InvalidOperationException("잠행은 감지를 올리면 안 됩니다.");

            var created = CharacterCreate.Build("detect-check", "탐지자", 0, 20, 40, 20,
                new[] { SkillId.DetectHidden, SkillId.Hiding, SkillId.Stealth },
                new[] { 50f, 30f, 20f });
            bool hasLute = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Lute)
                    hasLute = true;
            }
            if (hasLute)
                throw new InvalidOperationException("감지 시작은 류트를 주면 안 됩니다.");

            var go = new GameObject("selfcheck-detect");
            GameObject worldGo = null;
            GameObject hidGo = null;
            GameObject mobGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-detect-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();

                hidGo = new GameObject("selfcheck-detect-hidden");
                hidGo.transform.position = go.transform.position;
                var hidden = hidGo.AddComponent<WorldBody>();
                hidden.IsAvatar = true;
                hidden.RecalcFromStr(30);
                hidden.ResetHp();

                mobGo = new GameObject("selfcheck-detect-mob");
                mobGo.transform.position = go.transform.position;
                var mob = mobGo.AddComponent<WorldBody>();
                mob.IsEnemy = true;
                mob.DisplayName = "도적";
                mob.MaxHp = 45f;
                mob.ResetHp();

                var hid = world.TryHide(hidden);
                if (!hid.Applied)
                    throw new InvalidOperationException("감지 대상 은신 실패: " + hid.FailReason);
                if (!hidden.IsHidden(Time.time))
                    throw new InvalidOperationException("은신 후 HiddenUntil이 있어야 합니다.");
                float hp = hidden.Hp;
                var missed = world.TryEnemyStrike(mob, hidden);
                if (missed)
                    throw new InvalidOperationException("은신 중 몹은 숨은 대상을 타격하면 안 됩니다.");
                if (hidden.Hp < hp - 0.01f)
                    throw new InvalidOperationException("은신 중 HP가 줄면 안 됩니다.");

                var detect = world.TryDetectHidden(body);
                if (!detect.Applied)
                    throw new InvalidOperationException("서버 감지 실패: " + detect.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.DetectHidden) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 감지 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Hiding)) > 0.0001f)
                    throw new InvalidOperationException("서버 감지는 은신을 올리면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Stealth)) > 0.0001f)
                    throw new InvalidOperationException("서버 감지는 잠행을 올리면 안 됩니다.");
                if (hidden.IsHidden(Time.time))
                    throw new InvalidOperationException("감지는 은신을 해제해야 합니다.");
                if (string.IsNullOrEmpty(world.LastDetectMessage) || world.LastDetectMessage.IndexOf("감지", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("감지 메시지가 있어야 합니다.");

                hp = hidden.Hp;
                var hit = world.TryEnemyStrike(mob, hidden);
                if (!hit)
                    throw new InvalidOperationException("감지 후 몹은 숨었던 대상을 타격해야 합니다.");
                if (hidden.Hp >= hp - 0.01f)
                    throw new InvalidOperationException("감지 후 몹 타격이 들어가야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (hidGo != null)
                    UnityEngine.Object.DestroyImmediate(hidGo);
                if (mobGo != null)
                    UnityEngine.Object.DestroyImmediate(mobGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }



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




        static void AssertInscription()
        {
            AssertDungeon3Leftover();
            if (SkillId.Inscription == SkillId.Magery)
                throw new InvalidOperationException("각인 SkillId는 마법과 달라야 합니다.");
            if (SkillId.Inscription == SkillId.Alchemy)
                throw new InvalidOperationException("각인 SkillId는 연금술과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Inscription) != StatId.Int)
                throw new InvalidOperationException("각인 Primary는 INT이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Inscription) != "각인" || SkillTitles.JobOf(SkillId.Inscription) != "각인사")
                throw new InvalidOperationException("각인 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Magery) != "마법" || SkillNames.KoreanOf(SkillId.Alchemy) != "연금술")
                throw new InvalidOperationException("마법/연금술 스킬명을 바꾸면 안 됩니다.");
            if (ItemCatalog.ScrollEmber != "scroll_ember")
                throw new InvalidOperationException("주문서 템플릿은 scroll_ember여야 합니다.");
            if (ItemCatalog.SellPrice(ItemCatalog.ScrollEmber) <= 0 || ItemCatalog.WeightOf(ItemCatalog.ScrollEmber) <= 0f)
                throw new InvalidOperationException("scroll_ember 무게/가격이 없습니다.");
            if (ItemCatalog.SellPrice(ItemCatalog.Blank) <= 0 || ItemCatalog.WeightOf(ItemCatalog.Blank) <= 0f)
                throw new InvalidOperationException("blank 무게/가격이 없습니다.");

            var unlearned = InscriptionResolve.Resolve(new InscriptionRequest
            {
                KnowsEmber = false,
                HasCloth = true,
                Skills = new SkillSet()
            });
            if (unlearned.Applied || unlearned.FailReason != "unlearned")
                throw new InvalidOperationException("불씨를 모르면 각인되면 안 됩니다.");

            var noMatSkills = new SkillSet();
            var noMat = InscriptionResolve.Resolve(new InscriptionRequest
            {
                KnowsEmber = true,
                HasCloth = false,
                HasBlank = false,
                Skills = noMatSkills
            });
            if (noMat.Applied)
                throw new InvalidOperationException("천/blank 없이 각인되면 안 됩니다.");
            if (Math.Abs(noMatSkills.Get(SkillId.Inscription)) > 0.0001f)
                throw new InvalidOperationException("실패한 각인은 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int intWas = stats.Int;
            int dexWas = stats.Dex;
            var ok = InscriptionResolve.Resolve(new InscriptionRequest
            {
                KnowsEmber = true,
                HasCloth = true,
                Skills = skills,
                Stats = stats,
                Difficulty = InscriptionResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("천 각인은 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Inscription) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("각인 0.0→0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Magery)) > 0.0001f)
                throw new InvalidOperationException("각인은 마법을 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Alchemy)) > 0.0001f)
                throw new InvalidOperationException("각인은 연금술을 올리면 안 됩니다.");
            if (stats.Int != intWas + 1)
                throw new InvalidOperationException("각인 상승 시 INT가 올라야 합니다.");
            if (stats.Dex != dexWas)
                throw new InvalidOperationException("각인은 DEX를 올리면 안 됩니다.");

            var blankOk = InscriptionResolve.Resolve(new InscriptionRequest
            {
                KnowsEmber = true,
                HasBlank = true,
                Skills = new SkillSet()
            });
            if (!blankOk.Applied)
                throw new InvalidOperationException("blank 각인도 성공해야 합니다.");

            var mag = new SkillSet();
            SkillGain.TryRaise(mag, SkillId.Magery, 20f, out _, out _);
            if (Math.Abs(mag.Get(SkillId.Inscription)) > 0.0001f)
                throw new InvalidOperationException("마법은 각인을 올리면 안 됩니다.");
            var alch = new SkillSet();
            SkillGain.TryRaise(alch, SkillId.Alchemy, 10f, out _, out _);
            if (Math.Abs(alch.Get(SkillId.Inscription)) > 0.0001f)
                throw new InvalidOperationException("연금술은 각인을 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Inscription, SkillLock.Locked);
            var lockedOk = InscriptionResolve.Resolve(new InscriptionRequest
            {
                KnowsEmber = true,
                HasCloth = true,
                Skills = locked
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 각인도 주문서는 만들어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Inscription)) > 0.0001f)
                throw new InvalidOperationException("잠긴 각인은 오르면 안 됩니다.");

            var noScroll = ScrollUseResolve.Resolve(new ScrollUseRequest { HasScroll = false, HasTarget = true, TargetEnemy = true });
            if (noScroll.Applied || noScroll.FailReason != "no_scroll")
                throw new InvalidOperationException("주문서 없이 쓰면 안 됩니다.");
            var noTgt = ScrollUseResolve.Resolve(new ScrollUseRequest { HasScroll = true, HasTarget = false });
            if (noTgt.Applied)
                throw new InvalidOperationException("대상 없는 주문서는 실패해야 합니다.");

            var created = CharacterCreate.Build("insc-check", "각인", 0, 20, 20, 40,
                new[] { SkillId.Inscription, SkillId.Magery, SkillId.Alchemy },
                new[] { 50f, 30f, 20f });
            bool hasClothStart = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Cloth && created.Inventory[i].Amount >= 1)
                    hasClothStart = true;
            }
            if (!hasClothStart)
                throw new InvalidOperationException("각인 시작은 천을 줘야 합니다.");
            bool hasEmber = false;
            if (created.Spells != null)
            {
                for (int i = 0; i < created.Spells.Length; i++)
                    if (created.Spells[i] == (int)SpellId.Ember)
                        hasEmber = true;
            }
            if (!hasEmber)
                throw new InvalidOperationException("마법 시작은 불씨를 알아야 합니다(각인과 별개).");

            var go = new GameObject("selfcheck-insc");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-insc-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromInt(25);
                var bag = go.AddComponent<InventoryBag>();

                var missing = world.TryInscribe(body);
                if (missing.Applied)
                    throw new InvalidOperationException("재료/주문 없이 서버 각인되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Inscription)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 각인은 스킬을 올리면 안 됩니다.");

                bag.Add(ItemCatalog.Cloth, 1);
                var stillUnknown = world.TryInscribe(body);
                if (stillUnknown.Applied || stillUnknown.FailReason != "unlearned")
                    throw new InvalidOperationException("불씨를 모르면 서버 각인되면 안 됩니다.");
                world.BookOf(body).Learn(SpellId.Ember);
                bag.Add(ItemCatalog.Blank, 1);
                var blankHit = world.TryInscribe(body);
                if (!blankHit.Applied)
                    throw new InvalidOperationException("서버 blank 각인 실패: " + blankHit.FailReason);
                int scrolls = 0, blanks = 0, clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.ScrollEmber)
                        scrolls += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.Blank)
                        blanks += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                }
                if (scrolls != 1 || blanks != 0 || clothLeft != 1)
                    throw new InvalidOperationException("blank를 우선 소모하고 scroll_ember 1을 만들어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Inscription) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 각인 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("서버 각인은 마법을 올리면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Alchemy)) > 0.0001f)
                    throw new InvalidOperationException("서버 각인은 연금술을 올리면 안 됩니다.");
                if (world.LastInscribeMessage != ItemCatalog.ScrollEmber)
                    throw new InvalidOperationException("각인 메시지가 있어야 합니다: " + world.LastInscribeMessage);

                var clothHit = world.TryInscribe(body);
                if (!clothHit.Applied)
                    throw new InvalidOperationException("서버 천 각인 실패: " + clothHit.FailReason);
                scrolls = 0;
                clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.ScrollEmber)
                        scrolls += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                }
                if (scrolls != 2 || clothLeft != 0)
                    throw new InvalidOperationException("천 1 → scroll_ember이어야 합니다.");

                tgtGo = new GameObject("selfcheck-insc-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MaxHp = 40f;
                tgt.ResetHp();
                float hpWas = tgt.Hp;
                int resinWas = 0;
                bag.Add(SpellCast.Reagent, 2);
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == SpellCast.Reagent)
                        resinWas += bag.Items[i].Amount;
                float manaWas = body.Mana;
                float magWas = world.SkillsOf(body).Get(SkillId.Magery);
                var used = world.TryUseScroll(body, tgt);
                if (!used.Applied)
                    throw new InvalidOperationException("주문서 사용 실패: " + used.FailReason);
                if (tgt.Hp >= hpWas)
                    throw new InvalidOperationException("주문서는 불씨 피해를 줘야 합니다.");
                int scrollsLeft = 0, resinLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.ScrollEmber)
                        scrollsLeft += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == SpellCast.Reagent)
                        resinLeft += bag.Items[i].Amount;
                }
                if (scrollsLeft != 1)
                    throw new InvalidOperationException("사용한 주문서는 소모되어야 합니다.");
                if (resinLeft != resinWas)
                    throw new InvalidOperationException("주문서는 시약을 소모하면 안 됩니다(마법과 별개).");
                if (Math.Abs(body.Mana - manaWas) > 0.01f)
                    throw new InvalidOperationException("주문서는 마나를 소모하면 안 됩니다(마법과 별개).");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Magery) - magWas) > 0.0001f)
                    throw new InvalidOperationException("주문서 사용은 마법을 올리면 안 됩니다.");

                var used2 = world.TryUseScroll(body, tgt);
                if (!used2.Applied)
                    throw new InvalidOperationException("두 번째 주문서 사용 실패: " + used2.FailReason);
                scrollsLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.ScrollEmber)
                        scrollsLeft += bag.Items[i].Amount;
                if (scrollsLeft != 0)
                    throw new InvalidOperationException("두 번째 사용 후 주문서는 없어야 합니다.");
                var used3 = world.TryUseScroll(body, tgt);
                if (used3.Applied)
                    throw new InvalidOperationException("소모된 주문서를 다시 쓰면 안 됩니다.");

                bag.Add(SpellCast.Reagent, 1);
                body.SetMana(body.MaxMana);
                var ember = world.TryCast(body, SpellId.Ember, tgt);
                if (!ember.Applied)
                    throw new InvalidOperationException("마법 불씨는 각인과 별개로 유지되어야 합니다: " + ember.FailReason);
                if (world.SkillsOf(body).Get(SkillId.Magery) < 0.09f)
                    throw new InvalidOperationException("마법 불씨는 마법을 올려야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }


        static void AssertPoisoning()
        {
            AssertDungeon3Leftover();
            if (SkillId.Poisoning == SkillId.Alchemy)
                throw new InvalidOperationException("독 SkillId는 연금술과 달라야 합니다.");
            if (SkillId.Poisoning == SkillId.Veterinary)
                throw new InvalidOperationException("독 SkillId는 수의학과 달라야 합니다.");
            if (SkillId.Poisoning == SkillId.Magery)
                throw new InvalidOperationException("독 SkillId는 마법과 달라야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Poisoning) != StatId.Dex)
                throw new InvalidOperationException("독 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Poisoning) != "독" || SkillTitles.JobOf(SkillId.Poisoning) != "독살자")
                throw new InvalidOperationException("독 스킬명/직업명이 기획과 같아야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Alchemy) != "연금술" || SkillTitles.JobOf(SkillId.Alchemy) != "연금술사")
                throw new InvalidOperationException("연금술 스킬명/직업명을 바꾸면 안 됩니다.");
            if (SkillNames.KoreanOf(SkillId.Veterinary) != "수의학" || SkillTitles.JobOf(SkillId.Veterinary) != "수의사")
                throw new InvalidOperationException("수의학 스킬명/직업명을 바꾸면 안 됩니다.");
            if (ItemCatalog.PoisonVial != "poison_vial")
                throw new InvalidOperationException("독병 템플릿은 poison_vial여야 합니다.");
            if (ItemCatalog.SellPrice(ItemCatalog.PoisonVial) <= 0 || ItemCatalog.WeightOf(ItemCatalog.PoisonVial) <= 0f)
                throw new InvalidOperationException("poison_vial 무게/가격이 없습니다.");
            var rec = CraftRecipes.Find("poison_vial");
            if (rec == null || rec.Ingredient != ItemCatalog.Cloth || rec.Output != ItemCatalog.PoisonVial
                || rec.Skill != SkillId.Poisoning || rec.Count != 1)
                throw new InvalidOperationException("천 1 → 독병 레시피가 있어야 합니다.");
            if (!ItemCatalog.IsMeleeWeapon(ItemCatalog.IronSword) || ItemCatalog.IsMeleeWeapon(ItemCatalog.WoodenBow))
                throw new InvalidOperationException("근접은 검/둔기/창, 활은 원거리여야 합니다.");

            var noMeleeSkills = new SkillSet();
            var noMelee = PoisoningResolve.Resolve(new PoisonWeaponRequest
            {
                HasMelee = false,
                HasPotion = true,
                Skills = noMeleeSkills
            });
            if (noMelee.Applied || noMelee.FailReason != "no_melee")
                throw new InvalidOperationException("근접 무기 없이 도포되면 안 됩니다.");
            if (Math.Abs(noMeleeSkills.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("실패한 도포는 스킬을 올리면 안 됩니다.");

            var noPoisonSkills = new SkillSet();
            var noPoison = PoisoningResolve.Resolve(new PoisonWeaponRequest
            {
                HasMelee = true,
                HasPotion = false,
                HasVial = false,
                Skills = noPoisonSkills
            });
            if (noPoison.Applied || noPoison.FailReason != "no_poison")
                throw new InvalidOperationException("물약/독병 없이 도포되면 안 됩니다.");
            if (Math.Abs(noPoisonSkills.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("재료 없는 도포는 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            int intWas = stats.Int;
            int strWas = stats.Str;
            var ok = PoisoningResolve.Resolve(new PoisonWeaponRequest
            {
                HasMelee = true,
                HasPotion = true,
                Skills = skills,
                Stats = stats,
                Difficulty = PoisoningResolve.Difficulty
            });
            if (!ok.Applied)
                throw new InvalidOperationException("연금 물약 도포는 성공해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Poisoning) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("독 0.0→0.1이어야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Alchemy)) > 0.0001f)
                throw new InvalidOperationException("독은 연금술을 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Veterinary)) > 0.0001f)
                throw new InvalidOperationException("독은 수의학을 올리면 안 됩니다.");
            if (Math.Abs(skills.Get(SkillId.Magery)) > 0.0001f)
                throw new InvalidOperationException("독은 마법을 올리면 안 됩니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("독 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != intWas)
                throw new InvalidOperationException("독은 INT를 올리면 안 됩니다.");
            if (stats.Str != strWas)
                throw new InvalidOperationException("독은 STR을 올리면 안 됩니다.");

            var vialOk = PoisoningResolve.Resolve(new PoisonWeaponRequest
            {
                HasMelee = true,
                HasVial = true,
                Skills = new SkillSet()
            });
            if (!vialOk.Applied)
                throw new InvalidOperationException("천 독병 도포도 성공해야 합니다.");

            var mag = new SkillSet();
            SkillGain.TryRaise(mag, SkillId.Magery, 20f, out _, out _);
            if (Math.Abs(mag.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("마법은 독을 올리면 안 됩니다.");
            var alch = new SkillSet();
            SkillGain.TryRaise(alch, SkillId.Alchemy, 10f, out _, out _);
            if (Math.Abs(alch.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("연금술은 독을 올리면 안 됩니다.");
            var vet = new SkillSet();
            SkillGain.TryRaise(vet, SkillId.Veterinary, 10f, out _, out _);
            if (Math.Abs(vet.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("수의학은 독을 올리면 안 됩니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Poisoning, SkillLock.Locked);
            var lockedOk = PoisoningResolve.Resolve(new PoisonWeaponRequest
            {
                HasMelee = true,
                HasPotion = true,
                Skills = locked
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 독도 도포는 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Poisoning)) > 0.0001f)
                throw new InvalidOperationException("잠긴 독은 오르면 안 됩니다.");

            var created = CharacterCreate.Build("poison-check", "독살", 0, 20, 40, 20,
                new[] { SkillId.Poisoning, SkillId.Alchemy, SkillId.Veterinary },
                new[] { 50f, 30f, 20f });
            bool hasClothStart = false;
            for (int i = 0; i < created.Inventory.Length; i++)
            {
                if (created.Inventory[i].TemplateId == ItemCatalog.Cloth && created.Inventory[i].Amount >= 1)
                    hasClothStart = true;
            }
            if (!hasClothStart)
                throw new InvalidOperationException("독 시작은 천을 줘야 합니다.");

            var go = new GameObject("selfcheck-poison");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-poison-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                var bag = go.AddComponent<InventoryBag>();

                var missing = world.TryPoisonWeapon(body);
                if (missing.Applied)
                    throw new InvalidOperationException("무기/재료 없이 서버 도포되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Poisoning)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 도포는 스킬을 올리면 안 됩니다.");

                bag.Add(ItemCatalog.HealthPotion, 1);
                var stillNoMelee = world.TryPoisonWeapon(body);
                if (stillNoMelee.Applied || stillNoMelee.FailReason != "no_melee")
                    throw new InvalidOperationException("근접 무기 없이 서버 도포되면 안 됩니다.");

                bag.Add(ItemCatalog.WoodenBow, 1);
                var bowOnly = world.TryPoisonWeapon(body);
                if (bowOnly.Applied || bowOnly.FailReason != "no_melee")
                    throw new InvalidOperationException("활만 있으면 도포되면 안 됩니다.");
                bag.TakeOne(ItemCatalog.WoodenBow);

                bag.Add(ItemCatalog.IronSword, 1);
                var potionHit = world.TryPoisonWeapon(body);
                if (!potionHit.Applied)
                    throw new InvalidOperationException("서버 물약 도포 실패: " + potionHit.FailReason);
                int pots = 0, vials = 0, clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    if (bag.Items[i].TemplateId == ItemCatalog.HealthPotion)
                        pots += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.PoisonVial)
                        vials += bag.Items[i].Amount;
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                }
                if (pots != 0)
                    throw new InvalidOperationException("도포한 연금 물약은 소모되어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Poisoning) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 도포 후 서버 스킬 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Alchemy)) > 0.0001f)
                    throw new InvalidOperationException("서버 도포는 연금술을 올리면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Veterinary)) > 0.0001f)
                    throw new InvalidOperationException("서버 도포는 수의학을 올리면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("서버 도포는 마법을 올리면 안 됩니다.");
                if (world.LastPoisonMessage != "poison")
                    throw new InvalidOperationException("도포 메시지가 있어야 합니다: " + world.LastPoisonMessage);

                tgtGo = new GameObject("selfcheck-poison-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MaxHp = 80f;
                tgt.ResetHp();
                float hpWas = tgt.Hp;
                float manaWas = body.Mana;
                int resinWas = 0;
                bag.Add(SpellCast.Reagent, 2);
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == SpellCast.Reagent)
                        resinWas += bag.Items[i].Amount;
                float magWas = world.SkillsOf(body).Get(SkillId.Magery);
                var hit = world.TryAttack(body, tgt);
                if (!hit.Applied)
                    throw new InvalidOperationException("독 무기 공격 실패: " + hit.FailReason);
                float afterHit = tgt.Hp;
                if (afterHit >= hpWas - hit.Damage + 0.01f)
                    throw new InvalidOperationException("다음 TryAttack은 독 HP 틱을 줘야 합니다.");
                if (Math.Abs(afterHit - (hpWas - hit.Damage - PoisoningResolve.TickDamage)) > 0.01f)
                    throw new InvalidOperationException("첫 독 틱은 공격과 함께 들어가야 합니다.");
                world.TickPoison(UnityEngine.Time.time + PoisoningResolve.TickInterval);
                world.TickPoison(UnityEngine.Time.time + PoisoningResolve.TickInterval * 2);
                float afterTicks = tgt.Hp;
                float expect = hpWas - hit.Damage - PoisoningResolve.TickDamage * PoisoningResolve.TickCount;
                if (Math.Abs(afterTicks - expect) > 0.01f)
                    throw new InvalidOperationException("짧은 HP 틱이 모두 들어가야 합니다.");
                int resinLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == SpellCast.Reagent)
                        resinLeft += bag.Items[i].Amount;
                if (resinLeft != resinWas)
                    throw new InvalidOperationException("독은 시약을 소모하면 안 됩니다(마법과 별개).");
                if (Math.Abs(body.Mana - manaWas) > 0.01f)
                    throw new InvalidOperationException("독은 마나를 소모하면 안 됩니다(마법과 별개).");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Magery) - magWas) > 0.0001f)
                    throw new InvalidOperationException("독 공격은 마법을 올리면 안 됩니다.");
                if (tgt.PoisonTicks != 0)
                    throw new InvalidOperationException("짧은 틱이 끝나면 독 잔여가 없어야 합니다.");

                bag.Add(ItemCatalog.Cloth, 1);
                var clothHit = world.TryPoisonWeapon(body);
                if (!clothHit.Applied)
                    throw new InvalidOperationException("서버 천 독병 도포 실패: " + clothHit.FailReason);
                clothLeft = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.Cloth)
                        clothLeft += bag.Items[i].Amount;
                if (clothLeft != 0)
                    throw new InvalidOperationException("천 독병은 소모되어야 합니다.");

                bag.Add(ItemCatalog.PoisonVial, 1);
                var vialHit = world.TryPoisonWeapon(body);
                if (!vialHit.Applied)
                    throw new InvalidOperationException("서버 poison_vial 도포 실패: " + vialHit.FailReason);
                vials = 0;
                for (int i = 0; i < bag.Items.Count; i++)
                    if (bag.Items[i].TemplateId == ItemCatalog.PoisonVial)
                        vials += bag.Items[i].Amount;
                if (vials != 0)
                    throw new InvalidOperationException("도포한 독병은 소모되어야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

    }
}
