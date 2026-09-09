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
        // **몸을 감추는 기술**(랩 ㉫) — 숨기·은신·숨은 것 찾기. 「보이나 안 보이나」가 논점인 검사만 담는다.
        // 안 담는 것: 남의 것에 손대는 기술(`Roguery`)·짐승(`AnimalCare`)·새기고 바르는 것(`Inscribing`).
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




    }
}
