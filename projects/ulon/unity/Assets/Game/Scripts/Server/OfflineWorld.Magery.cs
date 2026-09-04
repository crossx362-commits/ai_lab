using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed partial class OfflineWorld
    {
        public AttackResult TryCast(WorldBody body, SpellId spell, WorldBody target)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            if (TooHeavy(body))
                return new AttackResult { FailReason = "overweight" };
            if (body.IsCasting(Time.time))
                return new AttackResult { FailReason = "casting" };
            var book = BookOf(body);
            if (!book.Knows(spell))
                return new AttackResult { FailReason = "unlearned" };
            int cost = SpellCast.ManaCost(spell);
            if (body.Mana < cost)
                return new AttackResult { FailReason = "mana" };
            var bag = Bag(body);
            int reagentNeed = SpellCast.ReagentCost(spell);
            if (CountItem(bag, SpellCast.Reagent) < reagentNeed)
                return new AttackResult { FailReason = "reagent" };

            var skills = SkillsOf(body);
            var stats = StatsOf(body);
            if (spell == SpellId.Ember || spell == SpellId.Bolt)
            {
                if (target == null || !target.Alive || target.Ghost || target == body)
                    return new AttackResult { FailReason = "no_target" };
                if (target.IsEnemy == body.IsEnemy)
                    return new AttackResult { FailReason = "no_target" };
                float dist = Vector3.Distance(body.transform.position, target.transform.position);
                if (dist > SpellCast.RangeOf(spell))
                    return new AttackResult { FailReason = "range" };

                if (SpellCast.Interruptible(spell) && SpellCast.CastTimeOf(spell) > 0f)
                {
                    ConsumeItem(bag, SpellCast.Reagent, reagentNeed);
                    body.SetMana(body.Mana - cost);
                    body.PendingSpell = spell;
                    body.PendingCastTarget = target;
                    body.CastingUntil = Time.time + SpellCast.CastTimeOf(spell);
                    body.BreakHide();
                    body.CombatUntil = Time.time + TravelMark.CombatSeconds;
                    return new AttackResult { Applied = true, Hit = false, Damage = 0 };
                }

                ConsumeItem(bag, SpellCast.Reagent, reagentNeed);
                body.SetMana(body.Mana - cost);
                int dmg = spell == SpellId.Bolt
                    ? SpellCast.BoltDamage(stats, skills)
                    : SpellCast.EmberDamage(stats, skills);
                var targetSkills = SkillsOf(target);
                var targetStats = StatsOf(target);
                var targetBag = target.GetComponent<InventoryBag>();
                int gear = ItemCatalog.EquipmentMagicResist(targetBag != null ? targetBag.Items : null);
                MagicResistResolve.TryResist(targetSkills, targetStats, gear, MagicResistResolve.Difficulty, ref dmg, out _, out _);
                target.ApplyDamage(dmg);
                body.BreakHide();
                body.CombatUntil = Time.time + TravelMark.CombatSeconds;
                SkillGain.TryRaise(skills, SkillId.Magery, 20f, out float before, out float after, stats);
                if (body.IsAvatar)
                {
                    body.RecalcFromStr(stats.Str);
                    body.RecalcFromInt(stats.Int);
                }
                if (target.IsAvatar)
                    target.RecalcFromInt(targetStats.Int);
                if (!target.Alive && Selected == target)
                    Selected = null;
                return new AttackResult { Applied = true, Hit = true, Damage = dmg, SkillBefore = before, SkillAfter = after };
            }

            if (spell == SpellId.Cleanse)
            {
                WorldBody cleanseTarget = target;
                if (cleanseTarget == null || cleanseTarget == body)
                    cleanseTarget = body;
                else
                {
                    if (!cleanseTarget.IsAvatar || cleanseTarget.Ghost || !cleanseTarget.Alive)
                        return new AttackResult { FailReason = "no_target" };
                    if (cleanseTarget.IsEnemy != body.IsEnemy)
                        return new AttackResult { FailReason = "no_target" };
                    float dist = Vector3.Distance(body.transform.position, cleanseTarget.transform.position);
                    if (dist > SpellCast.EmberRange)
                        return new AttackResult { FailReason = "range" };
                }

                ConsumeItem(bag, SpellCast.Reagent, reagentNeed);
                body.SetMana(body.Mana - cost);
                cleanseTarget.PoisonTicks = 0;
                cleanseTarget.NextPoisonAt = 0f;
                SkillGain.TryRaise(skills, SkillId.Magery, 18f, out float cb, out float ca, stats);
                if (body.IsAvatar)
                    body.RecalcFromInt(stats.Int);
                return new AttackResult { Applied = true, Hit = true, Damage = 0, SkillBefore = cb, SkillAfter = ca };
            }

            if (spell == SpellId.Ward)
            {
                ConsumeItem(bag, SpellCast.Reagent, reagentNeed);
                body.SetMana(body.Mana - cost);
                body.WardUntil = Time.time + SpellCast.WardSeconds;
                SkillGain.TryRaise(skills, SkillId.Magery, 18f, out float wb, out float wa, stats);
                if (body.IsAvatar)
                    body.RecalcFromInt(stats.Int);
                return new AttackResult { Applied = true, Hit = true, Damage = 0, SkillBefore = wb, SkillAfter = wa };
            }

            if (spell == SpellId.Bind)
            {
                if (target == null || !target.Alive || target.Ghost || target == body)
                    return new AttackResult { FailReason = "no_target" };
                if (!target.IsEnemy || target.IsAvatar)
                    return new AttackResult { FailReason = "no_target" };
                float bindDist = Vector3.Distance(body.transform.position, target.transform.position);
                if (bindDist > SpellCast.EmberRange)
                    return new AttackResult { FailReason = "range" };

                ConsumeItem(bag, SpellCast.Reagent, reagentNeed);
                body.SetMana(body.Mana - cost);
                target.RootUntil = Time.time + SpellCast.BindSeconds;
                body.BreakHide();
                body.CombatUntil = Time.time + TravelMark.CombatSeconds;
                SkillGain.TryRaise(skills, SkillId.Magery, 18f, out float bb, out float ba, stats);
                if (body.IsAvatar)
                    body.RecalcFromInt(stats.Int);
                return new AttackResult { Applied = true, Hit = true, Damage = 0, SkillBefore = bb, SkillAfter = ba };
            }

            if (spell == SpellId.Weaken)
            {
                if (target == null || !target.Alive || target.Ghost || target == body)
                    return new AttackResult { FailReason = "no_target" };
                if (!target.IsEnemy || target.IsAvatar)
                    return new AttackResult { FailReason = "no_target" };
                float weakenDist = Vector3.Distance(body.transform.position, target.transform.position);
                if (weakenDist > SpellCast.EmberRange)
                    return new AttackResult { FailReason = "range" };

                ConsumeItem(bag, SpellCast.Reagent, reagentNeed);
                body.SetMana(body.Mana - cost);
                target.WeakenUntil = Time.time + SpellCast.WeakenSeconds;
                body.BreakHide();
                body.CombatUntil = Time.time + TravelMark.CombatSeconds;
                SkillGain.TryRaise(skills, SkillId.Magery, 18f, out float wb2, out float wa2, stats);
                if (body.IsAvatar)
                    body.RecalcFromInt(stats.Int);
                return new AttackResult { Applied = true, Hit = true, Damage = 0, SkillBefore = wb2, SkillAfter = wa2 };
            }

            if (spell == SpellId.Spark)
            {
                if (target == null || !target.Alive || target.Ghost || target == body)
                    return new AttackResult { FailReason = "no_target" };
                if (!target.IsEnemy || target.IsAvatar)
                    return new AttackResult { FailReason = "no_target" };
                float sparkDist = Vector3.Distance(body.transform.position, target.transform.position);
                if (sparkDist > SpellCast.SparkRange)
                    return new AttackResult { FailReason = "range" };

                ConsumeItem(bag, SpellCast.Reagent, reagentNeed);
                body.SetMana(body.Mana - cost);
                int dmg = SpellCast.SparkDamage(stats, skills);
                var targetSkills = SkillsOf(target);
                var targetStats = StatsOf(target);
                var targetBag = target.GetComponent<InventoryBag>();
                int gear = ItemCatalog.EquipmentMagicResist(targetBag != null ? targetBag.Items : null);
                MagicResistResolve.TryResist(targetSkills, targetStats, gear, MagicResistResolve.Difficulty, ref dmg, out _, out _);
                target.ApplyDamage(dmg);
                body.BreakHide();
                body.CombatUntil = Time.time + TravelMark.CombatSeconds;
                SkillGain.TryRaise(skills, SkillId.Magery, 20f, out float sb, out float sa, stats);
                if (body.IsAvatar)
                {
                    body.RecalcFromStr(stats.Str);
                    body.RecalcFromInt(stats.Int);
                }
                if (target.IsAvatar)
                    target.RecalcFromInt(targetStats.Int);
                if (!target.Alive && Selected == target)
                    Selected = null;
                return new AttackResult { Applied = true, Hit = true, Damage = dmg, SkillBefore = sb, SkillAfter = sa };
            }

            if (spell == SpellId.Restore)
            {
                WorldBody restoreTarget = target;
                if (restoreTarget == null || restoreTarget == body)
                    restoreTarget = body;
                else
                {
                    if (!restoreTarget.IsAvatar || restoreTarget.Ghost || !restoreTarget.Alive)
                        return new AttackResult { FailReason = "no_target" };
                    if (restoreTarget.IsEnemy != body.IsEnemy)
                        return new AttackResult { FailReason = "no_target" };
                    float dist = Vector3.Distance(body.transform.position, restoreTarget.transform.position);
                    if (dist > SpellCast.EmberRange)
                        return new AttackResult { FailReason = "range" };
                }

                ConsumeItem(bag, SpellCast.Reagent, reagentNeed);
                body.SetMana(body.Mana - cost);
                int healR = SpellCast.RestoreHeal(stats);
                restoreTarget.SetHp(Mathf.Min(restoreTarget.MaxHp, restoreTarget.Hp + healR));
                SkillGain.TryRaise(skills, SkillId.Magery, 18f, out float rb, out float ra, stats);
                if (body.IsAvatar)
                    body.RecalcFromInt(stats.Int);
                return new AttackResult { Applied = true, Hit = true, Damage = -healR, SkillBefore = rb, SkillAfter = ra };
            }

            if (spell == SpellId.Blink)
            {
                if (body.InCombat(Time.time))
                    return new AttackResult { FailReason = "combat" };

                ConsumeItem(bag, SpellCast.Reagent, reagentNeed);
                body.SetMana(body.Mana - cost);
                Vector3 dir = body.transform.forward;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f)
                    dir = Vector3.forward;
                else
                    dir.Normalize();
                Vector3 pos = body.transform.position;
                float destX = pos.x + dir.x * SpellCast.BlinkDistance;
                float destZ = pos.z + dir.z * SpellCast.BlinkDistance;
                WarpBody(body, destX, destZ);
                SkillGain.TryRaise(skills, SkillId.Magery, 18f, out float blb, out float bla, stats);
                if (body.IsAvatar)
                    body.RecalcFromInt(stats.Int);
                return new AttackResult { Applied = true, Hit = true, Damage = 0, SkillBefore = blb, SkillAfter = bla };
            }

            if (spell == SpellId.Bless)
            {
                WorldBody blessTarget = target;
                if (blessTarget == null || blessTarget == body)
                    blessTarget = body;
                else
                {
                    if (!blessTarget.IsAvatar || blessTarget.Ghost || !blessTarget.Alive)
                        return new AttackResult { FailReason = "no_target" };
                    if (blessTarget.IsEnemy != body.IsEnemy)
                        return new AttackResult { FailReason = "no_target" };
                    float blessDist = Vector3.Distance(body.transform.position, blessTarget.transform.position);
                    if (blessDist > SpellCast.EmberRange)
                        return new AttackResult { FailReason = "range" };
                }

                ConsumeItem(bag, SpellCast.Reagent, reagentNeed);
                body.SetMana(body.Mana - cost);
                blessTarget.BlessUntil = Time.time + SpellCast.BlessSeconds;
                SkillGain.TryRaise(skills, SkillId.Magery, 18f, out float blsb, out float blsa, stats);
                if (body.IsAvatar)
                    body.RecalcFromInt(stats.Int);
                return new AttackResult { Applied = true, Hit = true, Damage = 0, SkillBefore = blsb, SkillAfter = blsa };
            }

            if (spell != SpellId.Mend)
                return new AttackResult { FailReason = "unlearned" };

            ConsumeItem(bag, SpellCast.Reagent, reagentNeed);
            body.SetMana(body.Mana - cost);
            int heal = SpellCast.MendHeal(stats);
            body.SetHp(Mathf.Min(body.MaxHp, body.Hp + heal));
            SkillGain.TryRaise(skills, SkillId.Magery, 18f, out float mb, out float ma, stats);
            if (body.IsAvatar)
                body.RecalcFromInt(stats.Int);
            return new AttackResult { Applied = true, Hit = true, Damage = -heal, SkillBefore = mb, SkillAfter = ma };
        }

        public void TickCast(float now)
        {
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var body = list[i];
                if (body == null || body.CastingUntil <= 0f)
                    continue;
                if (now < body.CastingUntil)
                    continue;
                ResolvePendingCast(body);
            }
        }

        void ResolvePendingCast(WorldBody body)
        {
            var spell = body.PendingSpell;
            var target = body.PendingCastTarget;
            body.ClearCast();
            if (body.Ghost || !body.Alive)
                return;
            if (spell != SpellId.Bolt)
                return;
            if (target == null || !target.Alive || target.Ghost || target == body)
                return;
            if (target.IsEnemy == body.IsEnemy)
                return;
            float dist = Vector3.Distance(body.transform.position, target.transform.position);
            if (dist > SpellCast.RangeOf(spell))
                return;

            var skills = SkillsOf(body);
            var stats = StatsOf(body);
            int dmg = SpellCast.BoltDamage(stats, skills);
            var targetSkills = SkillsOf(target);
            var targetStats = StatsOf(target);
            var targetBag = target.GetComponent<InventoryBag>();
            int gear = ItemCatalog.EquipmentMagicResist(targetBag != null ? targetBag.Items : null);
            MagicResistResolve.TryResist(targetSkills, targetStats, gear, MagicResistResolve.Difficulty, ref dmg, out _, out _);
            target.ApplyDamage(dmg);
            body.BreakHide();
            body.CombatUntil = Time.time + TravelMark.CombatSeconds;
            SkillGain.TryRaise(skills, SkillId.Magery, 20f, out _, out _, stats);
            if (body.IsAvatar)
            {
                body.RecalcFromStr(stats.Str);
                body.RecalcFromInt(stats.Int);
            }
            if (target.IsAvatar)
                target.RecalcFromInt(targetStats.Int);
            if (!target.Alive && Selected == target)
                Selected = null;
        }

    }
}
