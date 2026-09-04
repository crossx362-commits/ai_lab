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




        static void AssertBolt()
        {
            AssertDungeon3Leftover();
            if ((int)SpellId.Count != 11)
                throw new InvalidOperationException("마법 주문은 불씨+봉합+벼락+정화+수호+속박+약화+섬광+회복+도약+축복 11개여야 합니다.");
            if (SpellNames.KoreanOf(SpellId.Bolt) != "벼락")
                throw new InvalidOperationException("SpellId.Bolt 한글은 벼락이어야 합니다.");
            if (StatSet.PrimaryOf(SkillId.Magery) != StatId.Int)
                throw new InvalidOperationException("마법 Primary는 INT이어야 합니다.");
            if (SpellCast.ManaCost(SpellId.Bolt) <= SpellCast.ManaCost(SpellId.Ember))
                throw new InvalidOperationException("벼락 마나는 불씨보다 커야 합니다.");
            if (SpellCast.RangeOf(SpellId.Bolt) <= SpellCast.RangeOf(SpellId.Ember))
                throw new InvalidOperationException("벼락 사거리는 불씨보다 길어야 합니다.");
            if (SpellCast.RangeOf(SpellId.Ember) != SpellCast.EmberRange || SpellCast.EmberRange != 8f)
                throw new InvalidOperationException("불씨 사거리는 8이어야 합니다.");
            if (SpellCast.BoltRange != 12f)
                throw new InvalidOperationException("벼락 사거리는 12이어야 합니다.");

            var hi = new StatSet();
            hi.ForceSet(20, 20, 40);
            var lo = new StatSet();
            lo.ForceSet(20, 20, 10);
            int emberDmg = SpellCast.EmberDamage(hi, new SkillSet());
            int boltDmg = SpellCast.BoltDamage(hi, new SkillSet());
            if (boltDmg <= emberDmg)
                throw new InvalidOperationException("벼락 피해는 불씨보다 커야 합니다.");
            if (SpellCast.BoltDamage(hi, new SkillSet()) <= SpellCast.BoltDamage(lo, new SkillSet()))
                throw new InvalidOperationException("벼락 피해는 INT에 비례해야 합니다.");

            var go = new GameObject("selfcheck-bolt");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            GameObject palGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-bolt-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.IsEnemy = false;
                body.MaxHp = 50f;
                body.ResetHp();
                world.StatsOf(body).ForceSet(20, 20, 40);
                body.RecalcFromInt(40);
                body.SetMana(body.MaxMana);
                var bag = go.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 8);

                var unknown = world.TryCast(body, SpellId.Bolt, null);
                if (unknown.Applied || unknown.FailReason != "unlearned")
                    throw new InvalidOperationException("배우지 않은 벼락은 실패해야 합니다.");

                world.BookOf(body).Learn(SpellId.Ember);
                world.BookOf(body).Learn(SpellId.Bolt);

                tgtGo = new GameObject("selfcheck-bolt-tgt");
                tgtGo.transform.position = go.transform.position + new Vector3(10f, 0f, 0f);
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MaxHp = 80f;
                tgt.ResetHp();

                var noTgt = world.TryCast(body, SpellId.Bolt, null);
                if (noTgt.Applied || noTgt.FailReason != "no_target")
                    throw new InvalidOperationException("대상 없는 벼락은 실패해야 합니다.");

                palGo = new GameObject("selfcheck-bolt-pal");
                palGo.transform.position = go.transform.position;
                var pal = palGo.AddComponent<WorldBody>();
                pal.IsEnemy = false;
                pal.MaxHp = 40f;
                pal.ResetHp();
                var same = world.TryCast(body, SpellId.Bolt, pal);
                if (same.Applied)
                    throw new InvalidOperationException("같은 편 벼락은 실패해야 합니다.");

                var emberFar = world.TryCast(body, SpellId.Ember, tgt);
                if (emberFar.Applied || emberFar.FailReason != "range")
                    throw new InvalidOperationException("10유닛은 불씨 사거리 밖이어야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Magery)) > 0.0001f)
                    throw new InvalidOperationException("실패한 불씨는 마법을 올리면 안 됩니다.");

                float hp0 = tgt.Hp;
                int resin0 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin0 += bag.Items[ri].Amount;
                var bolt = world.TryCast(body, SpellId.Bolt, tgt);
                if (!bolt.Applied)
                    throw new InvalidOperationException("10유닛 벼락 시전 시작 실패: " + bolt.FailReason);
                if (!body.IsCasting(Time.time))
                    throw new InvalidOperationException("벼락은 CastingUntil 풍업이어야 합니다.");
                if (tgt.Hp < hp0)
                    throw new InvalidOperationException("풍업 중 벼락 효과가 나가면 안 됩니다.");
                world.TickCast(Time.time + SpellCast.BoltCastSeconds);
                if (body.IsCasting(Time.time + SpellCast.BoltCastSeconds))
                    throw new InvalidOperationException("풍업 후 시전이 남아 있으면 안 됩니다.");
                if (tgt.Hp >= hp0)
                    throw new InvalidOperationException("10유닛 벼락은 맞아야 합니다: " + bolt.FailReason);
                int boltDealt = (int)(hp0 - tgt.Hp);
                if (boltDealt <= emberDmg)
                    throw new InvalidOperationException("서버 벼락 피해는 불씨보다 커야 합니다.");
                int resin1 = 0;
                for (int ri = 0; ri < bag.Items.Count; ri++)
                    if (bag.Items[ri].TemplateId == SpellCast.Reagent)
                        resin1 += bag.Items[ri].Amount;
                if (resin1 != resin0 - 1)
                    throw new InvalidOperationException("벼락은 시약 1을 써야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Magery) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("벼락 후 마법이 0.1이어야 합니다.");

                tgtGo.transform.position = go.transform.position + new Vector3(13f, 0f, 0f);
                var tooFar = world.TryCast(body, SpellId.Bolt, tgt);
                if (tooFar.Applied || tooFar.FailReason != "range")
                    throw new InvalidOperationException("13유닛은 벼락 사거리 밖이어야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (palGo != null)
                    UnityEngine.Object.DestroyImmediate(palGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

        static void AssertTrackingSlice()
        {
            if (StatSet.PrimaryOf(SkillId.Tracking) != StatId.Dex)
                throw new InvalidOperationException("추적 Primary는 DEX이어야 합니다.");
            if (SkillNames.KoreanOf(SkillId.Tracking) != "추적" || SkillTitles.JobOf(SkillId.Tracking) != "추적자")
                throw new InvalidOperationException("추적 스킬명/직업명이 기획과 같아야 합니다.");

            var noneSkills = new SkillSet();
            var none = TrackingResolve.Resolve(new TrackingRequest
            {
                Now = 1f,
                Skills = noneSkills,
                HasTarget = false
            });
            if (none.Applied)
                throw new InvalidOperationException("대상 없는 추적은 실패해야 합니다.");
            if (Math.Abs(noneSkills.Get(SkillId.Tracking)) > 0.0001f)
                throw new InvalidOperationException("실패한 추적은 스킬을 올리면 안 됩니다.");

            var farSkills = new SkillSet();
            var far = TrackingResolve.Resolve(new TrackingRequest
            {
                Distance = 20f,
                Now = 1f,
                Skills = farSkills,
                HasTarget = true,
                TargetAlive = true,
                TargetKind = "도적"
            });
            if (far.Applied)
                throw new InvalidOperationException("사거리 밖 추적은 들어가면 안 됩니다.");
            if (Math.Abs(farSkills.Get(SkillId.Tracking)) > 0.0001f)
                throw new InvalidOperationException("실패한 추적은 스킬을 올리면 안 됩니다.");

            var deadSkills = new SkillSet();
            var dead = TrackingResolve.Resolve(new TrackingRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = deadSkills,
                HasTarget = true,
                IsCorpse = false,
                TargetAlive = false,
                TargetKind = "도적"
            });
            if (dead.Applied)
                throw new InvalidOperationException("살아 있지 않은 몹 추적은 실패해야 합니다.");

            var skills = new SkillSet();
            var stats = new StatSet();
            int dexWas = stats.Dex;
            var ok = TrackingResolve.Resolve(new TrackingRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = skills,
                Stats = stats,
                HasTarget = true,
                TargetAlive = true,
                TargetKind = "도적",
                Hp = 45f,
                MaxHp = 45f,
                Difficulty = TrackingResolve.Difficulty
            });
            if (!ok.Applied || ok.Kind != "도적" || Math.Abs(ok.Hp - 45f) > 0.0001f || Math.Abs(ok.MaxHp - 45f) > 0.0001f)
                throw new InvalidOperationException("추적은 몹 종류/HP를 밝혀야 합니다.");
            if (Math.Abs(skills.Get(SkillId.Tracking) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("성공 추적 후 0.1이어야 합니다.");
            if (stats.Dex != dexWas + 1)
                throw new InvalidOperationException("추적 상승 시 DEX가 올라야 합니다.");
            if (stats.Int != 25)
                throw new InvalidOperationException("추적은 INT를 올리면 안 됩니다.");

            var corpseSkills = new SkillSet();
            var corpse = TrackingResolve.Resolve(new TrackingRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = corpseSkills,
                HasTarget = true,
                IsCorpse = true,
                TargetAlive = false,
                TargetKind = "스켈레톤",
                LastX = 3.5f,
                LastZ = 7.25f
            });
            if (!corpse.Applied || !corpse.IsCorpse)
                throw new InvalidOperationException("시체 추적은 성공해야 합니다.");
            if (string.IsNullOrEmpty(corpse.LastPosition) || corpse.LastPosition.IndexOf("x=3.5", StringComparison.Ordinal) < 0
                || corpse.LastPosition.IndexOf("z=7.3", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("시체 추적은 마지막 위치 문자열을 줘야 합니다: " + corpse.LastPosition);
            if (Math.Abs(corpseSkills.Get(SkillId.Tracking) - 0.1f) > 0.0001f)
                throw new InvalidOperationException("시체 추적 후 0.1이어야 합니다.");

            var locked = new SkillSet();
            locked.SetLock(SkillId.Tracking, SkillLock.Locked);
            var lockedOk = TrackingResolve.Resolve(new TrackingRequest
            {
                Distance = 1f,
                Now = 1f,
                Skills = locked,
                HasTarget = true,
                TargetAlive = true,
                TargetKind = "도적",
                Hp = 12f,
                MaxHp = 45f
            });
            if (!lockedOk.Applied || lockedOk.Kind != "도적")
                throw new InvalidOperationException("잠긴 추적도 정보는 보여야 합니다.");
            if (Math.Abs(locked.Get(SkillId.Tracking)) > 0.0001f)
                throw new InvalidOperationException("잠긴 추적은 오르면 안 됩니다.");

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
            if (Math.Abs(melee.Get(SkillId.Tracking)) > 0.0001f)
                throw new InvalidOperationException("물리 공격은 추적을 올리면 안 됩니다.");

            var go = new GameObject("selfcheck-track");
            GameObject worldGo = null;
            GameObject tgtGo = null;
            GameObject corpseGo = null;
            try
            {
                var world = OfflineWorld.Instance;
                if (world == null)
                {
                    worldGo = new GameObject("selfcheck-track-world");
                    world = worldGo.AddComponent<OfflineWorld>();
                }
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;

                var missing = world.TryTrack(body, null);
                if (missing.Applied)
                    throw new InvalidOperationException("서버 대상 없는 추적은 실패해야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Tracking)) > 0.0001f)
                    throw new InvalidOperationException("실패한 서버 추적은 스킬을 올리면 안 됩니다.");

                tgtGo = new GameObject("selfcheck-track-tgt");
                tgtGo.transform.position = go.transform.position;
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MobId = "bandit";
                tgt.DisplayName = "도적";
                tgt.MaxHp = 45f;
                tgt.ResetHp();
                tgt.SetHp(33f);

                var hit = world.TryTrack(body, tgt);
                if (!hit.Applied)
                    throw new InvalidOperationException("서버 추적 실패: " + hit.FailReason);
                if (hit.Kind != "도적" || Math.Abs(hit.Hp - 33f) > 0.0001f)
                    throw new InvalidOperationException("서버 추적은 종류/HP를 밝혀야 합니다.");
                if (Math.Abs(world.SkillsOf(body).Get(SkillId.Tracking) - 0.1f) > 0.0001f)
                    throw new InvalidOperationException("성공 추적 후 서버 스킬 0.1이어야 합니다.");
                if (string.IsNullOrEmpty(world.LastTrackMessage) || world.LastTrackMessage.IndexOf("도적", StringComparison.Ordinal) < 0
                    || world.LastTrackMessage.IndexOf("HP", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("추적 메시지가 종류/HP를 포함해야 합니다.");

                corpseGo = new GameObject("selfcheck-track-corpse");
                corpseGo.transform.position = go.transform.position;
                var node = corpseGo.AddComponent<CorpseNode>();
                node.LastKind = "스켈레톤";
                node.LastX = 4f;
                node.LastZ = -2.5f;
                var scoutGo = new GameObject("selfcheck-track-scout");
                scoutGo.transform.position = go.transform.position;
                var scout = scoutGo.AddComponent<WorldBody>();
                scout.IsAvatar = true;
                var corpseHit = world.TryTrackCorpse(scout, node);
                if (!corpseHit.Applied)
                    throw new InvalidOperationException("서버 시체 추적 실패: " + corpseHit.FailReason);
                if (string.IsNullOrEmpty(corpseHit.LastPosition) || corpseHit.LastPosition.IndexOf("x=4.0", StringComparison.Ordinal) < 0
                    || corpseHit.LastPosition.IndexOf("z=-2.5", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("서버 시체 추적은 마지막 위치 문자열이어야 합니다: " + corpseHit.LastPosition);
                if (string.IsNullOrEmpty(world.LastTrackMessage) || world.LastTrackMessage.IndexOf("마지막", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("시체 추적 메시지가 마지막 위치를 포함해야 합니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                var leftoverScout = GameObject.Find("selfcheck-track-scout");
                if (leftoverScout != null)
                    UnityEngine.Object.DestroyImmediate(leftoverScout);
                if (corpseGo != null)
                    UnityEngine.Object.DestroyImmediate(corpseGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }
        }

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
