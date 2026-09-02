namespace Ulon.Shared
{
    public sealed class AttackRequest
    {
        public float Distance;
        public float Range = 2.4f;
        public float Now;
        public float NextAttackAt;
        public bool TargetAlive = true;
        public SkillSet Skills;
        public StatSet Stats;
        public SkillId WeaponSkill = SkillId.Swordsmanship;
        public float Difficulty = 20f;
        public int Damage = 8;
    }

    public struct AttackResult
    {
        public bool Applied;
        public bool Hit;
        public int Damage;
        public float SkillBefore;
        public float SkillAfter;
        public string FailReason;
    }

    public static class AttackResolve
    {
        public static AttackResult Resolve(AttackRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (!req.TargetAlive)
                return Fail("dead");
            if (req.Distance > req.Range)
                return Fail("range");
            if (req.Now < req.NextAttackAt)
                return Fail("cooldown");

            SkillGain.TryRaise(req.Skills, req.WeaponSkill, req.Difficulty, out float before, out float after, req.Stats);
            if (req.WeaponSkill != SkillId.Tactics)
                SkillGain.TryRaise(req.Skills, SkillId.Tactics, req.Difficulty, out _, out _, req.Stats);
            if (req.WeaponSkill != SkillId.Anatomy)
                SkillGain.TryRaise(req.Skills, SkillId.Anatomy, req.Difficulty, out _, out _, req.Stats);
            int damage = req.Damage;
            if (req.Stats != null)
            {
                int stat = req.WeaponSkill == SkillId.Archery ? req.Stats.Dex : req.Stats.Str;
                damage = req.Damage + stat / 10;
            }
            damage += (int)(req.Skills.Get(SkillId.Tactics) / 20f);
            damage += (int)(req.Skills.Get(SkillId.Anatomy) / 20f);
            return new AttackResult
            {
                Applied = true,
                Hit = true,
                Damage = damage,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };

        public const int RetaliationDamage = 4;

        public static bool TryParry(SkillSet defenderSkills, StatSet defenderStats, bool hasShield, float difficulty, ref int damage, out float before, out float after)
        {
            before = 0f;
            after = 0f;
            if (!hasShield || defenderSkills == null)
                return false;
            SkillGain.TryRaise(defenderSkills, SkillId.Parrying, difficulty, out before, out after, defenderStats);
            int reduce = 2 + (int)(defenderSkills.Get(SkillId.Parrying) / 20f);
            if (defenderStats != null)
                reduce += defenderStats.Dex / 20;
            damage -= reduce;
            if (damage < 0)
                damage = 0;
            return true;
        }
    }

    public sealed class HealRequest
    {
        public float Distance;
        public float Range = ItemCatalog.MeleeRange;
        public float Now;
        public float NextHealAt;
        public bool TargetAlive = true;
        public bool TargetGhost;
        public float TargetHp;
        public float TargetMaxHp = 50f;
        public SkillSet Skills;
        public StatSet Stats;
        public bool HasBandage;
        public float Difficulty = 10f;
    }

    public static class HealResolve
    {
        public const int BaseHeal = 8;
        public const float Difficulty = 10f;

        public static int Amount(SkillSet skills, StatSet stats)
        {
            int heal = BaseHeal;
            if (skills != null)
            {
                heal += (int)(skills.Get(SkillId.Healing) / 20f);
                heal += (int)(skills.Get(SkillId.Anatomy) / 20f);
            }
            if (stats != null)
                heal += stats.Dex / 10;
            return heal;
        }

        public static float Cooldown(StatSet stats)
        {
            float cd = 2.2f;
            if (stats != null)
                cd -= stats.Dex / 100f;
            if (cd < 1.1f)
                cd = 1.1f;
            return cd;
        }

        public static AttackResult Resolve(HealRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (!req.HasBandage)
                return Fail("no_bandage");
            if (!req.TargetAlive || req.TargetGhost)
                return Fail("dead");
            if (req.Distance > req.Range)
                return Fail("range");
            if (req.Now < req.NextHealAt)
                return Fail("cooldown");
            if (req.TargetHp >= req.TargetMaxHp - 0.01f)
                return Fail("full");

            SkillGain.TryRaise(req.Skills, SkillId.Healing, req.Difficulty, out float before, out float after, req.Stats);
            int heal = Amount(req.Skills, req.Stats);
            return new AttackResult
            {
                Applied = true,
                Hit = true,
                Damage = heal,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };
    }

    public sealed class Spellbook
    {
        readonly bool[] learned = new bool[(int)SpellId.Count];

        public bool Knows(SpellId id)
        {
            int i = (int)id;
            return i >= 0 && i < learned.Length && learned[i];
        }

        public void Learn(SpellId id)
        {
            int i = (int)id;
            if (i >= 0 && i < learned.Length)
                learned[i] = true;
        }

        public void ReadFrom(int[] ids)
        {
            for (int i = 0; i < learned.Length; i++)
                learned[i] = false;
            if (ids == null)
                return;
            for (int i = 0; i < ids.Length; i++)
                Learn((SpellId)ids[i]);
        }

        public int[] ToArray()
        {
            int n = 0;
            for (int i = 0; i < learned.Length; i++)
                if (learned[i]) n++;
            var ids = new int[n];
            int w = 0;
            for (int i = 0; i < learned.Length; i++)
                if (learned[i])
                    ids[w++] = i;
            return ids;
        }
    }

    public static class SpellCast
    {
        public const string Reagent = "resin";

        public static int ManaCost(SpellId id) => id == SpellId.Mend ? 8 : 6;

        public static int EmberDamage(StatSet stats, SkillSet skills)
        {
            int dmg = 6;
            if (stats != null)
                dmg += stats.Int / 10;
            if (skills != null)
            {
                dmg += (int)(skills.Get(SkillId.Magery) / 20f);
                dmg += (int)(skills.Get(SkillId.EvaluateIntelligence) / 20f);
            }
            return dmg;
        }

        public static int MendHeal(StatSet stats)
        {
            int heal = 8;
            if (stats != null)
                heal += stats.Int / 8;
            return heal;
        }
    }

    public static class MagicResistResolve
    {
        public const float Difficulty = 20f;

        public static int Reduce(int incoming, SkillSet skills, StatSet stats, int equipmentResist)
        {
            int reduce = equipmentResist;
            if (skills != null)
                reduce += 1 + (int)(skills.Get(SkillId.MagicResist) / 20f);
            if (stats != null)
                reduce += stats.Int / 20;
            int dmg = incoming - reduce;
            if (dmg < 0)
                dmg = 0;
            return dmg;
        }

        public static bool TryResist(SkillSet skills, StatSet stats, int equipmentResist, float difficulty, ref int damage, out float before, out float after)
        {
            before = 0f;
            after = 0f;
            if (skills == null)
                return false;
            SkillGain.TryRaise(skills, SkillId.MagicResist, difficulty, out before, out after, stats);
            damage = Reduce(damage, skills, stats, equipmentResist);
            return true;
        }
    }

    public sealed class MeditationRequest
    {
        public float Now;
        public float NextMeditateAt;
        public bool Ghost;
        public float Mana;
        public float MaxMana = 35f;
        public SkillSet Skills;
        public StatSet Stats;
        public bool HeavyArmor;
        public float Difficulty = 12f;
    }

    public static class MeditationResolve
    {
        public const int BaseRegen = 6;
        public const float Difficulty = 12f;
        public const float HeavyMul = 0.4f;
        public const float CooldownSeconds = 2f;

        public static int Amount(SkillSet skills, StatSet stats, bool heavyArmor)
        {
            int regen = BaseRegen;
            if (skills != null)
                regen += (int)(skills.Get(SkillId.Meditation) / 20f);
            if (stats != null)
                regen += stats.Int / 10;
            if (heavyArmor)
            {
                regen = (int)(regen * HeavyMul);
                if (regen < 1)
                    regen = 1;
            }
            return regen;
        }

        public static AttackResult Resolve(MeditationRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (req.Now < req.NextMeditateAt)
                return Fail("cooldown");
            if (req.Mana >= req.MaxMana - 0.01f)
                return Fail("full");

            SkillGain.TryRaise(req.Skills, SkillId.Meditation, req.Difficulty, out float before, out float after, req.Stats);
            int regen = Amount(req.Skills, req.Stats, req.HeavyArmor);
            return new AttackResult
            {
                Applied = true,
                Hit = true,
                Damage = regen,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };
    }

    public sealed class EvalIntRequest
    {
        public float Distance;
        public float Range = EvalIntResolve.Range;
        public float Now;
        public float NextEvalAt;
        public bool TargetAlive = true;
        public bool TargetGhost;
        public SkillSet Skills;
        public StatSet Stats;
        public StatSet TargetStats;
        public float TargetMana;
        public float TargetMaxMana = 35f;
        public float Difficulty = EvalIntResolve.Difficulty;
    }

    public struct EvalIntResult
    {
        public bool Applied;
        public int Intelligence;
        public int Mana;
        public int MaxMana;
        public float SkillBefore;
        public float SkillAfter;
        public string FailReason;
    }

    public static class EvalIntResolve
    {
        public const float Difficulty = 12f;
        public const float Range = 8f;
        public const float CooldownSeconds = 2f;

        public static EvalIntResult Resolve(EvalIntRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (req.TargetStats == null)
                return Fail("no_target");
            if (!req.TargetAlive || req.TargetGhost)
                return Fail("dead");
            if (req.Distance > req.Range)
                return Fail("range");
            if (req.Now < req.NextEvalAt)
                return Fail("cooldown");

            SkillGain.TryRaise(req.Skills, SkillId.EvaluateIntelligence, req.Difficulty, out float before, out float after, req.Stats);
            return new EvalIntResult
            {
                Applied = true,
                Intelligence = req.TargetStats.Int,
                Mana = (int)req.TargetMana,
                MaxMana = (int)req.TargetMaxMana,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static EvalIntResult Fail(string reason) => new EvalIntResult { FailReason = reason };
    }
}
