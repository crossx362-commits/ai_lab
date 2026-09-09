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
        // **정신 쪽 기술**(랩 ㉱) — 명상·마법 저항·감정.
        // 담는 것: 마나와 마법을 다루는 몸가짐. 안 담는 것: 소리로 거는 것(`Bardic`).
        static void AssertMeditationSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Meditation) != StatId.Int)
                throw new InvalidOperationException("명상 Primary는 INT이어야 합니다.");

            var fullSkills = new SkillSet();
            var full = MeditationResolve.Resolve(new MeditationRequest
            {
                Now = 1f,
                Skills = fullSkills,
                Mana = 35f,
                MaxMana = 35f
            });
            if (full.Applied)
                throw new InvalidOperationException("마나 가득이면 명상되면 안 됩니다.");
            if (Math.Abs(fullSkills.Get(SkillId.Meditation)) > 0.0001f)
                throw new InvalidOperationException("실패한 명상은 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            var ok = MeditationResolve.Resolve(new MeditationRequest
            {
                Now = 1f,
                Skills = skills,
                Stats = stats,
                Mana = 5f,
                MaxMana = 35f,
                Difficulty = MeditationResolve.Difficulty
            });
            if (!ok.Applied || ok.Damage < MeditationResolve.BaseRegen)
                throw new InvalidOperationException("명상이 마나를 회복해야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Meditation) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 명상 후 0.1이어야 합니다.");

            int light = MeditationResolve.Amount(new SkillSet(), new StatSet(), false);
            int heavy = MeditationResolve.Amount(new SkillSet(), new StatSet(), true);
            if (heavy >= light)
                throw new InvalidOperationException("중갑은 명상 회복이 낮아야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Meditation, SkillLock.Locked);
            var lockedOk = MeditationResolve.Resolve(new MeditationRequest
            {
                Now = 1f,
                Skills = locked,
                Mana = 5f,
                MaxMana = 35f
            });
            if (!lockedOk.Applied)
                throw new InvalidOperationException("잠긴 명상도 회복은 되어야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Meditation)) > 0.0001f)
                throw new InvalidOperationException("잠긴 명상은 오르면 안 됩니다.");

            var go = new GameObject("selfcheck-meditate");
            GameObject worldGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-meditate-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromInt(world.StatsOf(body).Int);
                body.SetMana(4f);
                float before = body.Mana;
                var none = world.TryMeditate(body);
                if (!none.Applied)
                    throw new InvalidOperationException("서버 명상 실패: " + none.FailReason);
                if (body.Mana <= before)
                    throw new InvalidOperationException("명상이 마나를 올려야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Meditation) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 명상 후 서버 스킬 0.1이어야 합니다.");

                body.SetMana(body.MaxMana);
                var fullWorld = world.TryMeditate(body);
                if (fullWorld.Applied)
                    throw new InvalidOperationException("가득 찬 마나로 명상되면 안 됩니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Meditation) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 명상은 스킬을 올리면 안 됩니다.");

                var plateGo = new GameObject("selfcheck-meditate-plate");
                plateGo.transform.position = go.transform.position;
                var plateBody = plateGo.AddComponent<WorldBody>();
                plateBody.IsAvatar = true;
                plateBody.RecalcFromInt(world.StatsOf(plateBody).Int);
                plateBody.SetMana(4f);
                var plateBag = plateGo.AddComponent<InventoryBag>();
                plateBag.Add(ItemCatalog.IronPlate, 1);
                if (!ItemCatalog.HasHeavyArmor(plateBag.Items))
                    throw new InvalidOperationException("iron_plate는 중갑이어야 합니다.");
                var heavyHit = world.TryMeditate(plateBody);
                if (!heavyHit.Applied)
                    throw new InvalidOperationException("중갑 명상 실패: " + heavyHit.FailReason);
                int plated = MeditationResolve.Amount(world.SkillsOf(plateBody), world.StatsOf(plateBody), true);
                if (heavyHit.Damage != plated)
                    throw new InvalidOperationException("중갑 명상 회복량이 패널티를 받아야 합니다.");
                UnityEngine.Object.DestroyImmediate(plateGo);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertMagicResistSlice()
        {
            if (StatSet.PrimaryOf(SkillId.MagicResist) != StatId.Int)
                throw new InvalidOperationException("마법 저항 Primary는 INT이어야 합니다.");

            int raw = 10;
            int none = MagicResistResolve.Reduce(raw, new SkillSet(), new StatSet(), 0);
            int geared = MagicResistResolve.Reduce(raw, new SkillSet(), new StatSet(), 2);
            if (none >= raw)
                throw new InvalidOperationException("마법 저항은 마법 피해를 줄여야 합니다.");
            if (geared >= none)
                throw new InvalidOperationException("장비 저항이 마법 피해를 더 줄여야 합니다.");
            var melee = new SkillSet();
            var phys = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1.2f,
                Now = 1f,
                Skills = melee,
                TargetAlive = true
            });
            if (!phys.Applied)
                throw new InvalidOperationException("물리 공격 대조 실패");
            if (Math.Abs(melee.Get(SkillId.MagicResist)) > 0.0001f)
                throw new InvalidOperationException("물리 피격은 마법 저항을 올리면 안 됩니다.");

            var go = new GameObject("selfcheck-resist");
            GameObject worldGo = null;
            GameObject casterGo = null;
            GameObject plateGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-resist-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.IsEnemy = false;
                body.MaxHp = 50f;
                body.ResetHp();
                body.RecalcFromInt(world.StatsOf(body).Int);
                go.AddComponent<InventoryBag>();

                casterGo = new GameObject("selfcheck-resist-caster");
                casterGo.transform.position = go.transform.position;
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsEnemy = true;
                caster.MaxHp = 40f;
                caster.ResetHp();
                caster.RecalcFromInt(40);
                caster.SetMana(40f);
                var cbag = casterGo.AddComponent<InventoryBag>();
                cbag.Add(SpellCast.Reagent, 8);
                world.BookOf(caster).Learn(SpellId.Ember);
                world.BookOf(caster).Learn(SpellId.Mend);

                float hp0 = body.Hp;
                var ember = world.TryCast(caster, SpellId.Ember, body);
                if (!ember.Applied || body.Hp >= hp0)
                    throw new InvalidOperationException("적대 불씨가 플레이어에게 들어가야 합니다: " + ember.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.MagicResist) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("적대 주문 피격 후 마법 저항 0.1이어야 합니다.");
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.MagicResist)) > 0.0001f)
                    throw new InvalidOperationException("시전자 마법 저항이 오르면 안 됩니다.");

                int dmg0 = ember.Damage;
                world.SkillsOf(body).ForceSet(SkillId.MagicResist, 40f, SkillLock.Up);
                body.SetHp(50f);
                var hard = world.TryCast(caster, SpellId.Ember, body);
                if (!hard.Applied)
                    throw new InvalidOperationException("고숙련 저항 불씨 실패: " + hard.FailReason);
                if (hard.Damage >= dmg0)
                    throw new InvalidOperationException("높은 마법 저항이 피해를 더 줄여야 합니다.");

                var palGo = new GameObject("selfcheck-resist-pal");
                palGo.transform.position = go.transform.position;
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsEnemy = true;
                pal.MaxHp = 40f;
                pal.ResetHp();
                var same = world.TryCast(caster, SpellId.Ember, pal);
                if (same.Applied)
                    throw new InvalidOperationException("같은 편 불씨는 실패해야 합니다.");
                UnityEngine.Object.DestroyImmediate(palGo);

                float resistBeforeMend = world.SkillsOf(body).Get(SkillId.MagicResist);
                var mend = world.TryCast(caster, SpellId.Mend, body);
                if (!mend.Applied)
                    throw new InvalidOperationException("봉합은 시전자 치유여야 합니다: " + mend.FailReason);
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.MagicResist) - resistBeforeMend) > 0.0001f)
                    throw new InvalidOperationException("우호 주문은 마법 저항을 올리면 안 됩니다.");

                var locked = world.SkillsOf(body);
                locked.ForceSet(SkillId.MagicResist, 0f, SkillLock.Locked);
                body.SetHp(50f);
                var lockedHit = world.TryCast(caster, SpellId.Ember, body);
                if (!lockedHit.Applied)
                    throw new InvalidOperationException("잠긴 저항도 피격은 되어야 합니다: " + lockedHit.FailReason);
                if (Math.Abs(locked.Get(SkillId.MagicResist)) > 0.0001f)
                    throw new InvalidOperationException("잠긴 마법 저항은 오르면 안 됩니다.");

                plateGo = new GameObject("selfcheck-resist-plate");
                plateGo.transform.position = casterGo.transform.position;
                var plateBody = plateGo.AddComponent<WorldBody>();
                plateBody.IsAvatar = true;
                plateBody.IsEnemy = false;
                plateBody.MaxHp = 50f;
                plateBody.ResetHp();
                var plateBag = plateGo.AddComponent<InventoryBag>();
                plateBag.Add("iron_plate", 1);
                if (ItemCatalog.EquipmentMagicResist(plateBag.Items) < 2)
                    throw new InvalidOperationException("iron_plate는 장비 마법 저항을 줘야 합니다.");
                float php = plateBody.Hp;
                var plateHit = world.TryCast(caster, SpellId.Ember, plateBody);
                if (!plateHit.Applied || plateBody.Hp >= php)
                    throw new InvalidOperationException("중갑 대상 불씨 실패: " + plateHit.FailReason);
                if (plateHit.Damage >= lockedHit.Damage)
                    throw new InvalidOperationException("장비 저항이 서버 불씨 피해를 더 줄여야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (plateGo != null)
                    UnityEngine.Object.DestroyImmediate(plateGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertEvalIntSlice()
        {
            if (StatSet.PrimaryOf(SkillId.EvaluateIntelligence) != StatId.Int)
                throw new InvalidOperationException("지능 평가 Primary는 INT이어야 합니다.");

            var noneSkills = new SkillSet();
            var none = EvalIntResolve.Resolve(new EvalIntRequest
            {
                Now = 1f,
                Skills = noneSkills,
                TargetStats = null
            });
            if (none.Applied)
                throw new InvalidOperationException("대상 없는 지능 평가는 실패해야 합니다.");
            if (Math.Abs(noneSkills.Get(SkillId.EvaluateIntelligence)) > 0.0001f)
                throw new InvalidOperationException("실패한 지능 평가는 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = EvalIntResolve.Resolve(new EvalIntRequest
            {
                Distance = 20f,
                Now = 1f,
                Skills = farSkills,
                TargetStats = new StatSet(),
                TargetAlive = true
            });
            if (far.Applied)
                throw new InvalidOperationException("사거리 밖 지능 평가는 들어가면 안 됩니다.");
            if (Math.Abs(farSkills.Get(SkillId.EvaluateIntelligence)) > 0.0001f)
                throw new InvalidOperationException("실패한 지능 평가는 스킬을 올리면 안 됩니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            var targetStats = new StatSet();
            targetStats.ForceSet(20, 20, 40);
            int intWas = stats.Int;
            var ok = EvalIntResolve.Resolve(new EvalIntRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = skills,
                Stats = stats,
                TargetStats = targetStats,
                TargetAlive = true,
                TargetMana = 12f,
                TargetMaxMana = 50f,
                Difficulty = EvalIntResolve.Difficulty
            });
            if (!ok.Applied || ok.Intelligence != 40 || ok.Mana != 12 || ok.MaxMana != 50)
                throw new InvalidOperationException("지능 평가는 대상 INT/마나를 밝혀야 합니다.");
            if (Math.Abs(skills.Get(SkillId.EvaluateIntelligence) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 지능 평가 후 0.1이어야 합니다.");
            if (stats.Int != intWas + 1)
                throw new InvalidOperationException("지능 평가 상승 시 INT가 올라야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.EvaluateIntelligence, SkillLock.Locked);
            var lockedOk = EvalIntResolve.Resolve(new EvalIntRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = locked,
                TargetStats = targetStats,
                TargetAlive = true,
                TargetMana = 12f,
                TargetMaxMana = 50f
            });
            if (!lockedOk.Applied || lockedOk.Intelligence != 40)
                throw new InvalidOperationException("잠긴 지능 평가도 정보는 보여야 합니다.");
            if (Math.Abs(locked.Get(SkillId.EvaluateIntelligence)) > 0.0001f)
                throw new InvalidOperationException("잠긴 지능 평가는 오르면 안 됩니다.");

            int plain = SpellCast.EmberDamage(new StatSet(), new SkillSet());
            var boosted = new SkillSet();
            boosted.ForceSet(SkillId.EvaluateIntelligence, 40f, SkillLock.Up);
            int withEval = SpellCast.EmberDamage(new StatSet(), boosted);
            if (withEval <= plain)
                throw new InvalidOperationException("지능 평가가 공격 마법 위력에 반영되어야 합니다.");

            var melee = new SkillSet();
            var phys = AttackResolve.Resolve(new AttackRequest
            {
                Distance = 1.2f,
                Now = 1f,
                Skills = melee,
                TargetAlive = true
            });
            if (!phys.Applied)
                throw new InvalidOperationException("물리 공격 대조 실패");
            if (Math.Abs(melee.Get(SkillId.EvaluateIntelligence)) > 0.0001f)
                throw new InvalidOperationException("물리 공격은 지능 평가를 올리면 안 됩니다.");

            var go = new GameObject("selfcheck-evalint");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            GameObject casterGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-evalint-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromInt(world.StatsOf(body).Int);

                var missing = world.TryEvaluate(body, null);
                if (missing.Applied)
                    throw new InvalidOperationException("서버 대상 없는 지능 평가는 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.EvaluateIntelligence)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 지능 평가는 스킬을 올리면 안 됩니다.");

                tgtGo = new GameObject("selfcheck-evalint-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.DisplayName = "스켈레톤";
                tgt.MaxHp = 30f;
                tgt.ResetHp();
                world.StatsOf(tgt).ForceSet(20, 20, 40);
                tgt.RecalcFromInt(40);
                tgt.SetMana(18f);

                var hit = world.TryEvaluate(body, tgt);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 지능 평가 실패: " + hit.FailReason);
                if (hit.Intelligence != 40 || hit.Mana != 18 || hit.MaxMana != StatSet.MaxManaOf(40))
                    throw new InvalidOperationException("서버 지능 평가는 대상 INT/마나를 밝혀야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.EvaluateIntelligence) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 지능 평가 후 서버 스킬 0.1이어야 합니다.");
                if (string.IsNullOrEmpty(world.LastEvalMessage) || world.LastEvalMessage.IndexOf("INT 40", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("지능 평가 메시지가 INT를 포함해야 합니다.");

                casterGo = new GameObject("selfcheck-evalint-caster");
                casterGo.transform.position = go.transform.position;
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.RecalcFromInt(world.StatsOf(caster).Int);
                caster.SetMana(40f);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 4);
                world.BookOf(caster).Learn(SpellId.Ember);
                float evalBeforeCast = world.SkillsOf(caster).Get(SkillId.EvaluateIntelligence);
                var ember = world.TryCast(caster, SpellId.Ember, tgt);
                if (!ember.Applied)
                    throw new InvalidOperationException("불씨 대조 실패: " + ember.FailReason);
                if (Math.Abs(world.SkillsOf(caster).Get(SkillId.EvaluateIntelligence) - evalBeforeCast) > 0.0001f)
                    throw new InvalidOperationException("주문 시전은 지능 평가를 올리면 안 됩니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }
    }
}
