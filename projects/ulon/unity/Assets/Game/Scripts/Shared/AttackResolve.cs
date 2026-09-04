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
        public bool Exceptional;
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
                int stat = req.WeaponSkill == SkillId.Archery || req.WeaponSkill == SkillId.Fencing ? req.Stats.Dex : req.Stats.Str;
                damage = req.Damage + stat / 10;
            }
            damage += (int)(req.Skills.Get(SkillId.Tactics) / 20f);
            damage += (int)(req.Skills.Get(SkillId.Anatomy) / 20f);
            if (req.Exceptional)
                damage += ExceptionalCraft.DamageBonus;
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

    public sealed class BandageResurrectRequest
    {
        public float Distance;
        public float Range = ItemCatalog.MeleeRange;
        public bool HealerGhost;
        public bool TargetGhost;
        public bool TargetAvatar;
        public bool HasBandage;
        public SkillSet Skills;
        public StatSet Stats;
        public float Difficulty = BandageResurrectResolve.Difficulty;
        public bool Force;
    }

    public static class BandageResurrectResolve
    {
        public const float Difficulty = HealResolve.Difficulty;

        public static AttackResult Resolve(BandageResurrectRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (req.HealerGhost)
                return Fail("ghost");
            if (!req.TargetGhost || !req.TargetAvatar)
                return Fail("not_ghost");
            if (!req.HasBandage)
                return Fail("no_bandage");
            if (req.Distance > req.Range)
                return Fail("range");

            float before = req.Skills.Get(SkillId.Healing);
            float after = before;
            if (req.Force)
            {
                float next = before + SkillGain.SliceStep;
                if (req.Skills.GetLock(SkillId.Healing) == SkillLock.Up && before < SkillSet.IndividualCap)
                {
                    req.Skills.TrySet(SkillId.Healing, next);
                    after = req.Skills.Get(SkillId.Healing);
                    if (after > before && req.Stats != null)
                        req.Stats.TryRaise(StatSet.PrimaryOf(SkillId.Healing));
                }
            }
            else
                SkillGain.TryRaise(req.Skills, SkillId.Healing, req.Difficulty, out before, out after, req.Stats);

            return new AttackResult
            {
                Applied = true,
                Hit = true,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };
    }


    public sealed class BandageCurePoisonRequest
    {
        public float Distance;
        public float Range = ItemCatalog.MeleeRange;
        public bool HealerGhost;
        public bool TargetGhost;
        public bool TargetAlive = true;
        public int PoisonTicks;
        public bool HasBandage;
        public SkillSet Skills;
        public StatSet Stats;
        public float Difficulty = BandageCurePoisonResolve.Difficulty;
        public bool Force;
    }

    public static class BandageCurePoisonResolve
    {
        public const float Difficulty = HealResolve.Difficulty;

        public static AttackResult Resolve(BandageCurePoisonRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (req.HealerGhost)
                return Fail("ghost");
            if (req.TargetGhost)
                return Fail("ghost");
            if (!req.TargetAlive)
                return Fail("dead");
            if (req.PoisonTicks <= 0)
                return Fail("no_poison");
            if (!req.HasBandage)
                return Fail("no_bandage");
            if (req.Distance > req.Range)
                return Fail("range");

            float before = req.Skills.Get(SkillId.Healing);
            float after = before;
            if (req.Force)
            {
                float next = before + SkillGain.SliceStep;
                if (req.Skills.GetLock(SkillId.Healing) == SkillLock.Up && before < SkillSet.IndividualCap)
                {
                    req.Skills.TrySet(SkillId.Healing, next);
                    after = req.Skills.Get(SkillId.Healing);
                    if (after > before && req.Stats != null)
                        req.Stats.TryRaise(StatSet.PrimaryOf(SkillId.Healing));
                }
            }
            else
                SkillGain.TryRaise(req.Skills, SkillId.Healing, req.Difficulty, out before, out after, req.Stats);

            return new AttackResult
            {
                Applied = true,
                Hit = true,
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
        public const float EmberRange = 8f;
        public const float BoltRange = 12f;
        public const float SparkRange = 6f;
        public const float BlinkDistance = 3.5f;
        public const float BoltCastSeconds = 0.75f;
        public const float WardSeconds = 8f;
        public const float BindSeconds = 4f;
        public const float WeakenSeconds = 6f;
        public const float BlessSeconds = 8f;

        public static float CastTimeOf(SpellId id) => id == SpellId.Bolt ? BoltCastSeconds : 0f;
        public static bool Interruptible(SpellId id) => id == SpellId.Bolt;

        public static int ManaCost(SpellId id)
        {
            if (id == SpellId.Mend) return 8;
            if (id == SpellId.Bolt) return 10;
            if (id == SpellId.Cleanse) return 6;
            if (id == SpellId.Ward) return 6;
            if (id == SpellId.Bind) return 6;
            if (id == SpellId.Weaken) return 6;
            if (id == SpellId.Spark) return 6;
            if (id == SpellId.Restore) return 10;
            if (id == SpellId.Blink) return 6;
            if (id == SpellId.Bless) return 6;
            return 6;
        }

        public static int ReagentCost(SpellId id)
        {
            if (id == SpellId.Restore) return 2;
            return 1;
        }

        public static float RangeOf(SpellId id)
        {
            if (id == SpellId.Bolt) return BoltRange;
            if (id == SpellId.Spark) return SparkRange;
            return EmberRange;
        }

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

        public static int BoltDamage(StatSet stats, SkillSet skills)
        {
            int dmg = 10;
            if (stats != null)
                dmg += stats.Int / 8;
            if (skills != null)
            {
                dmg += (int)(skills.Get(SkillId.Magery) / 16f);
                dmg += (int)(skills.Get(SkillId.EvaluateIntelligence) / 20f);
            }
            return dmg;
        }

        public static int SparkDamage(StatSet stats, SkillSet skills)
        {
            int dmg = 4;
            if (stats != null)
                dmg += stats.Int / 12;
            if (skills != null)
            {
                dmg += (int)(skills.Get(SkillId.Magery) / 24f);
                dmg += (int)(skills.Get(SkillId.EvaluateIntelligence) / 24f);
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

        public static int RestoreHeal(StatSet stats)
        {
            int heal = MendHeal(stats);
            return heal + heal / 2;
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
        public const float HeavyMul = 0.5f;
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
                // GAME_DESIGN 18.6 Armor Trade-off: heavy armor halves meditation mana restore.
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

    public sealed class TrackingRequest
    {
        public float Distance;
        public float Range = TrackingResolve.Range;
        public float Now;
        public float NextTrackAt;
        public bool HasTarget;
        public bool IsCorpse;
        public bool TargetAlive = true;
        public string TargetKind = "";
        public float Hp;
        public float MaxHp;
        public float LastX;
        public float LastZ;
        public SkillSet Skills;
        public StatSet Stats;
        public float Difficulty = TrackingResolve.Difficulty;
    }

    public struct TrackingResult
    {
        public bool Applied;
        public bool IsCorpse;
        public string Kind;
        public float Hp;
        public float MaxHp;
        public string LastPosition;
        public float SkillBefore;
        public float SkillAfter;
        public string FailReason;
    }

    public static class TrackingResolve
    {
        public const float Difficulty = 12f;
        public const float Range = 8f;
        public const float CooldownSeconds = 2f;

        public static string PositionString(float x, float z)
        {
            return "x=" + x.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                + " z=" + z.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static TrackingResult Resolve(TrackingRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (!req.HasTarget)
                return Fail("no_target");
            if (!req.IsCorpse && !req.TargetAlive)
                return Fail("dead");
            if (req.Distance > req.Range)
                return Fail("range");
            if (req.Now < req.NextTrackAt)
                return Fail("cooldown");

            SkillGain.TryRaise(req.Skills, SkillId.Tracking, req.Difficulty, out float before, out float after, req.Stats);
            string pos = PositionString(req.LastX, req.LastZ);
            return new TrackingResult
            {
                Applied = true,
                IsCorpse = req.IsCorpse,
                Kind = req.TargetKind ?? "",
                Hp = req.Hp,
                MaxHp = req.MaxHp,
                LastPosition = pos,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static TrackingResult Fail(string reason) => new TrackingResult { FailReason = reason };
    }

    public sealed class MusicianshipRequest
    {
        public bool HasInstrument;
        public float Now;
        public float NextPlayAt;
        public SkillSet Skills;
        public StatSet Stats;
        public float Difficulty = MusicianshipResolve.Difficulty;
    }

    public struct MusicianshipResult
    {
        public bool Applied;
        public int Calmed;
        public float SkillBefore;
        public float SkillAfter;
        public string FailReason;
    }

    public static class MusicianshipResolve
    {
        public const float Difficulty = 12f;
        public const float Range = 6f;
        public const float CooldownSeconds = 2f;
        public const float CalmSeconds = 4f;

        public static MusicianshipResult Resolve(MusicianshipRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (!req.HasInstrument)
                return Fail("no_instrument");
            if (req.Now < req.NextPlayAt)
                return Fail("cooldown");

            SkillGain.TryRaise(req.Skills, SkillId.Musicianship, req.Difficulty, out float before, out float after, req.Stats);
            return new MusicianshipResult
            {
                Applied = true,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static MusicianshipResult Fail(string reason) => new MusicianshipResult { FailReason = reason };
    }

    public sealed class PeacemakingRequest
    {
        public bool HasInstrument;
        public bool HasTarget;
        public bool TargetEnemy;
        public bool TargetAlive;
        public float Distance;
        public float Range = PeacemakingResolve.Range;
        public float Now;
        public float NextPeaceAt;
        public SkillSet Skills;
        public StatSet Stats;
        public float Difficulty = PeacemakingResolve.Difficulty;
    }

    public struct PeacemakingResult
    {
        public bool Applied;
        public float SkillBefore;
        public float SkillAfter;
        public string FailReason;
    }

    public static class PeacemakingResolve
    {
        public const float Difficulty = 12f;
        public const float Range = 8f;
        public const float CooldownSeconds = 2f;
        public const float PeaceSeconds = 10f;

        public static PeacemakingResult Resolve(PeacemakingRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (!req.HasInstrument)
                return Fail("no_instrument");
            if (!req.HasTarget)
                return Fail("no_target");
            if (!req.TargetEnemy)
                return Fail("not_mob");
            if (!req.TargetAlive)
                return Fail("dead");
            if (req.Distance > req.Range)
                return Fail("range");
            if (req.Now < req.NextPeaceAt)
                return Fail("cooldown");

            SkillGain.TryRaise(req.Skills, SkillId.Peacemaking, req.Difficulty, out float before, out float after, req.Stats);
            return new PeacemakingResult
            {
                Applied = true,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static PeacemakingResult Fail(string reason) => new PeacemakingResult { FailReason = reason };
    }

    public sealed class ProvocationRequest
    {
        public bool HasInstrument;
        public bool HasTargetA;
        public bool HasTargetB;
        public bool TargetAEnemy;
        public bool TargetBEnemy;
        public bool TargetAAlive;
        public bool TargetBAlive;
        public bool SameTarget;
        public float DistanceA;
        public float DistanceB;
        public float Range = ProvocationResolve.Range;
        public float Now;
        public float NextProvokeAt;
        public SkillSet Skills;
        public StatSet Stats;
        public float Difficulty = ProvocationResolve.Difficulty;
    }

    public struct ProvocationResult
    {
        public bool Applied;
        public float SkillBefore;
        public float SkillAfter;
        public string FailReason;
    }

    public static class ProvocationResolve
    {
        public const float Difficulty = 12f;
        public const float Range = 8f;
        public const float CooldownSeconds = 2f;
        public const float FightSeconds = 10f;

        public static ProvocationResult Resolve(ProvocationRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (!req.HasInstrument)
                return Fail("no_instrument");
            if (!req.HasTargetA || !req.HasTargetB)
                return Fail("no_target");
            if (req.SameTarget)
                return Fail("same_target");
            if (!req.TargetAEnemy || !req.TargetBEnemy)
                return Fail("not_mob");
            if (!req.TargetAAlive || !req.TargetBAlive)
                return Fail("dead");
            if (req.DistanceA > req.Range || req.DistanceB > req.Range)
                return Fail("range");
            if (req.Now < req.NextProvokeAt)
                return Fail("cooldown");

            SkillGain.TryRaise(req.Skills, SkillId.Provocation, req.Difficulty, out float before, out float after, req.Stats);
            return new ProvocationResult
            {
                Applied = true,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static ProvocationResult Fail(string reason) => new ProvocationResult { FailReason = reason };
    }

    public sealed class HidingRequest
    {
        public bool Ghost;
        public float Now;
        public float NextHideAt;
        public SkillSet Skills;
        public StatSet Stats;
        public float Difficulty = HidingResolve.Difficulty;
    }

    public struct HidingResult
    {
        public bool Applied;
        public float SkillBefore;
        public float SkillAfter;
        public string FailReason;
    }

    public static class HidingResolve
    {
        public const float Difficulty = 12f;
        public const float CooldownSeconds = 2f;
        public const float HideSeconds = 8f;

        public static HidingResult Resolve(HidingRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (req.Now < req.NextHideAt)
                return Fail("cooldown");

            SkillGain.TryRaise(req.Skills, SkillId.Hiding, req.Difficulty, out float before, out float after, req.Stats);
            return new HidingResult
            {
                Applied = true,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static HidingResult Fail(string reason) => new HidingResult { FailReason = reason };
    }

    public sealed class StealthRequest
    {
        public bool Ghost;
        public bool AlreadyHidden;
        public float Now;
        public float NextStealthAt;
        public SkillSet Skills;
        public StatSet Stats;
        public float Difficulty = StealthResolve.Difficulty;
    }

    public struct StealthResult
    {
        public bool Applied;
        public float SkillBefore;
        public float SkillAfter;
        public string FailReason;
    }

    public static class StealthResolve
    {
        public const float Difficulty = 12f;
        public const float CooldownSeconds = 1f;
        public const float StealthSeconds = 8f;

        public static StealthResult Resolve(StealthRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (!req.AlreadyHidden)
                return Fail("not_hidden");
            if (req.Now < req.NextStealthAt)
                return Fail("cooldown");

            SkillGain.TryRaise(req.Skills, SkillId.Stealth, req.Difficulty, out float before, out float after, req.Stats);
            return new StealthResult
            {
                Applied = true,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static StealthResult Fail(string reason) => new StealthResult { FailReason = reason };
    }

    public sealed class DetectHiddenRequest
    {
        public bool Ghost;
        public float Now;
        public float NextDetectAt;
        public bool HasHiddenTarget;
        public float Distance;
        public float Range = DetectHiddenResolve.DetectRange;
        public SkillSet Skills;
        public StatSet Stats;
        public float Difficulty = DetectHiddenResolve.Difficulty;
    }

    public struct DetectHiddenResult
    {
        public bool Applied;
        public float SkillBefore;
        public float SkillAfter;
        public string FailReason;
    }

    public static class DetectHiddenResolve
    {
        public const float Difficulty = 12f;
        public const float CooldownSeconds = 2f;
        public const float DetectRange = 8f;

        public static DetectHiddenResult Resolve(DetectHiddenRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (req.Now < req.NextDetectAt)
                return Fail("cooldown");
            if (req.Distance > req.Range)
                return Fail("range");

            SkillGain.TryRaise(req.Skills, SkillId.DetectHidden, req.Difficulty, out float before, out float after, req.Stats);
            return new DetectHiddenResult
            {
                Applied = true,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static DetectHiddenResult Fail(string reason) => new DetectHiddenResult { FailReason = reason };
    }

    public sealed class LockpickingRequest
    {
        public bool Ghost;
        public bool HasCrate;
        public bool CrateOpened;
        public bool HasLockpick;
        public float Distance;
        public float Range = 2.4f;
        public float Now;
        public float NextPickAt;
        public SkillSet Skills;
        public StatSet Stats;
        public float Difficulty = LockpickingResolve.Difficulty;
    }

    public struct LockpickingResult
    {
        public bool Applied;
        public float SkillBefore;
        public float SkillAfter;
        public string FailReason;
    }

    public static class LockpickingResolve
    {
        public const float Difficulty = 12f;
        public const float CooldownSeconds = 1f;

        public static LockpickingResult Resolve(LockpickingRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (!req.HasCrate)
                return Fail("no_crate");
            if (req.CrateOpened)
                return Fail("opened");
            if (!req.HasLockpick)
                return Fail("no_pick");
            if (req.Distance > req.Range)
                return Fail("range");
            if (req.Now < req.NextPickAt)
                return Fail("cooldown");

            SkillGain.TryRaise(req.Skills, SkillId.Lockpicking, req.Difficulty, out float before, out float after, req.Stats);
            return new LockpickingResult
            {
                Applied = true,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static LockpickingResult Fail(string reason) => new LockpickingResult { FailReason = reason };
    }

    public sealed class AnimalLoreRequest
    {
        public float Distance;
        public float Range = AnimalLoreResolve.Range;
        public float Now;
        public float NextLoreAt;
        public bool HasTarget;
        public bool TargetEnemy;
        public bool TargetAlive = true;
        public string TargetKind = "";
        public string MobId = "";
        public float Hp;
        public float MaxHp;
        public int Str;
        public int Resist;
        public int DamageMin;
        public int DamageMax;
        public bool Tamable;
        public SkillSet Skills;
        public StatSet Stats;
        public float Difficulty = AnimalLoreResolve.Difficulty;
    }

    public struct AnimalLoreResult
    {
        public bool Applied;
        public string Kind;
        public float Hp;
        public float MaxHp;
        public int Str;
        public int Resist;
        public string DamageBand;
        public bool Tamable;
        public float SkillBefore;
        public float SkillAfter;
        public string FailReason;
    }

    public static class AnimalLoreResolve
    {
        public const float Difficulty = 12f;
        public const float Range = 8f;
        public const float CooldownSeconds = 2f;

        public static string DamageBand(int min, int max) => min + "-" + max;

        public static AnimalLoreResult Resolve(AnimalLoreRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (!req.HasTarget)
                return Fail("no_target");
            if (!req.TargetEnemy)
                return Fail("not_mob");
            if (!req.TargetAlive)
                return Fail("dead");
            if (req.Distance > req.Range)
                return Fail("range");
            if (req.Now < req.NextLoreAt)
                return Fail("cooldown");

            SkillGain.TryRaise(req.Skills, SkillId.AnimalLore, req.Difficulty, out float before, out float after, req.Stats);
            return new AnimalLoreResult
            {
                Applied = true,
                Kind = req.TargetKind ?? "",
                Hp = req.Hp,
                MaxHp = req.MaxHp,
                Str = req.Str,
                Resist = req.Resist,
                DamageBand = DamageBand(req.DamageMin, req.DamageMax),
                Tamable = false,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static AnimalLoreResult Fail(string reason) => new AnimalLoreResult { FailReason = reason, Tamable = false };
    }
    public sealed class VeterinaryRequest
    {
        public float Distance;
        public float Range = ItemCatalog.MeleeRange;
        public float Now;
        public float NextVetAt;
        public bool HasTarget;
        public bool TargetEnemy;
        public bool TargetAlive = true;
        public bool TargetGhost;
        public float TargetHp;
        public float TargetMaxHp = 50f;
        public SkillSet Skills;
        public StatSet Stats;
        public bool HasBandage;
        public float Difficulty = VeterinaryResolve.Difficulty;
    }

    public static class VeterinaryResolve
    {
        public const int BaseHeal = 8;
        public const float Difficulty = 10f;

        public static int Amount(SkillSet skills, StatSet stats)
        {
            int heal = BaseHeal;
            if (skills != null)
                heal += (int)(skills.Get(SkillId.Veterinary) / 20f);
            if (stats != null)
                heal += stats.Dex / 10;
            return heal;
        }

        public static float Cooldown(StatSet stats) => HealResolve.Cooldown(stats);

        public static AttackResult Resolve(VeterinaryRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (!req.HasBandage)
                return Fail("no_bandage");
            if (!req.HasTarget)
                return Fail("no_target");
            if (!req.TargetEnemy)
                return Fail("not_mob");
            if (!req.TargetAlive || req.TargetGhost)
                return Fail("dead");
            if (req.Distance > req.Range)
                return Fail("range");
            if (req.Now < req.NextVetAt)
                return Fail("cooldown");
            if (req.TargetHp >= req.TargetMaxHp - 0.01f)
                return Fail("full");

            SkillGain.TryRaise(req.Skills, SkillId.Veterinary, req.Difficulty, out float before, out float after, req.Stats);
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

    public sealed class VeterinaryResurrectRequest
    {
        public float Distance;
        public float Range = ItemCatalog.MeleeRange;
        public bool HealerGhost;
        public bool TargetGhost;
        public bool TargetBondedPet;
        public bool HasBandage;
        public SkillSet Skills;
        public StatSet Stats;
        public float Difficulty = VeterinaryResurrectResolve.Difficulty;
        public bool Force;
    }

    public static class VeterinaryResurrectResolve
    {
        public const float Difficulty = VeterinaryResolve.Difficulty;

        public static AttackResult Resolve(VeterinaryResurrectRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (req.HealerGhost)
                return Fail("ghost");
            if (!req.TargetGhost || !req.TargetBondedPet)
                return Fail("not_pet_ghost");
            if (!req.HasBandage)
                return Fail("no_bandage");
            if (req.Distance > req.Range)
                return Fail("range");

            float before = req.Skills.Get(SkillId.Veterinary);
            float after = before;
            if (req.Force)
            {
                float next = before + SkillGain.SliceStep;
                if (req.Skills.GetLock(SkillId.Veterinary) == SkillLock.Up && before < SkillSet.IndividualCap)
                {
                    req.Skills.TrySet(SkillId.Veterinary, next);
                    after = req.Skills.Get(SkillId.Veterinary);
                    if (after > before && req.Stats != null)
                        req.Stats.TryRaise(StatSet.PrimaryOf(SkillId.Veterinary));
                }
            }
            else
                SkillGain.TryRaise(req.Skills, SkillId.Veterinary, req.Difficulty, out before, out after, req.Stats);

            return new AttackResult
            {
                Applied = true,
                Hit = true,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };
    }

    public sealed class InscriptionRequest
    {
        public bool KnowsEmber;
        public bool HasCloth;
        public bool HasBlank;
        public SkillSet Skills;
        public StatSet Stats;
        public float Difficulty = InscriptionResolve.Difficulty;
    }

    public static class InscriptionResolve
    {
        public const float Difficulty = 10f;

        public static AttackResult Resolve(InscriptionRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (!req.KnowsEmber)
                return Fail("unlearned");
            if (!req.HasCloth && !req.HasBlank)
                return Fail("no_material");
            SkillGain.TryRaise(req.Skills, SkillId.Inscription, req.Difficulty, out float before, out float after, req.Stats);
            return new AttackResult
            {
                Applied = true,
                Hit = true,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };
    }

    public sealed class ScrollUseRequest
    {
        public float Distance;
        public float Range = SpellCast.EmberRange;
        public bool HasScroll;
        public bool HasTarget;
        public bool TargetEnemy;
        public bool TargetAlive = true;
        public bool TargetGhost;
        public SkillSet Skills;
        public StatSet Stats;
    }

    public static class ScrollUseResolve
    {
        public static AttackResult Resolve(ScrollUseRequest req)
        {
            if (req == null)
                return Fail("bad_request");
            if (!req.HasScroll)
                return Fail("no_scroll");
            if (!req.HasTarget)
                return Fail("no_target");
            if (!req.TargetEnemy || !req.TargetAlive || req.TargetGhost)
                return Fail("no_target");
            if (req.Distance > req.Range)
                return Fail("range");
            int dmg = SpellCast.EmberDamage(req.Stats, req.Skills);
            return new AttackResult { Applied = true, Hit = true, Damage = dmg };
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };
    }

    public sealed class PoisonWeaponRequest
    {
        public bool HasMelee;
        public bool HasPotion;
        public bool HasVial;
        public SkillSet Skills;
        public StatSet Stats;
        public float Difficulty = PoisoningResolve.Difficulty;
    }

    public static class PoisoningResolve
    {
        public const float Difficulty = 10f;
        public const int TickDamage = 2;
        public const int TickCount = 3;
        public const float TickInterval = 1f;

        public static AttackResult Resolve(PoisonWeaponRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (!req.HasMelee)
                return Fail("no_melee");
            if (!req.HasPotion && !req.HasVial)
                return Fail("no_poison");
            SkillGain.TryRaise(req.Skills, SkillId.Poisoning, req.Difficulty, out float before, out float after, req.Stats);
            return new AttackResult
            {
                Applied = true,
                Hit = true,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static AttackResult Fail(string reason) => new AttackResult { FailReason = reason };
    }
    public sealed class CampingRequest
    {
        public bool Ghost;
        public float Now;
        public float NextCampAt;
        public bool NearCampfire;
        public bool HasKindling;
        public float Distance;
        public float Range = CampingResolve.CampRange;
        public SkillSet Skills;
        public StatSet Stats;
        public float Difficulty = CampingResolve.Difficulty;
    }

    public struct CampingResult
    {
        public bool Applied;
        public float SkillBefore;
        public float SkillAfter;
        public string FailReason;
    }

    public static class CampingResolve
    {
        public const float Difficulty = 12f;
        public const float CooldownSeconds = 2f;
        public const float CampRange = 2.4f;
        public const float SafeSeconds = 8f;

        public static CampingResult Resolve(CampingRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (req.Now < req.NextCampAt)
                return Fail("cooldown");
            if (!req.NearCampfire && !req.HasKindling)
                return Fail("no_fire");
            if (req.NearCampfire && req.Distance > req.Range)
                return Fail("range");

            SkillGain.TryRaise(req.Skills, SkillId.Camping, req.Difficulty, out float before, out float after, req.Stats);
            return new CampingResult
            {
                Applied = true,
                SkillBefore = before,
                SkillAfter = after
            };
        }

        static CampingResult Fail(string reason) => new CampingResult { FailReason = reason };
    }

    public sealed class StealingRequest
    {
        public bool Ghost;
        public float Now;
        public float NextStealAt;
        public bool HasPack;
        public float Distance;
        public float Range = StealingResolve.StealRange;
        public int PackGold;
        public int PackCloth;
        public bool InGuardZone;
        public bool Witnessed;
        public SkillSet Skills;
        public StatSet Stats;
        public float Difficulty = StealingResolve.Difficulty;
    }

    public struct StealingResult
    {
        public bool Applied;
        public bool Stolen;
        public bool Criminal;
        public string LootId;
        public float SkillBefore;
        public float SkillAfter;
        public string FailReason;
    }

    public static class StealingResolve
    {
        public const float Difficulty = 12f;
        public const float CooldownSeconds = 2f;
        public const float StealRange = 2.4f;
        public const float WitnessRange = 8f;

        public static StealingResult Resolve(StealingRequest req)
        {
            if (req == null || req.Skills == null)
                return Fail("bad_request");
            if (req.Ghost)
                return Fail("ghost");
            if (req.Now < req.NextStealAt)
                return Fail("cooldown");
            if (!req.HasPack)
                return Fail("no_pack");
            if (req.Distance > req.Range)
                return Fail("range");

            SkillGain.TryRaise(req.Skills, SkillId.Stealing, req.Difficulty, out float before, out float after, req.Stats);
            bool caught = req.InGuardZone || req.Witnessed;
            string loot = "";
            bool stolen = false;
            if (!caught)
            {
                loot = LowestLoot(req.PackGold, req.PackCloth);
                stolen = loot.Length > 0;
            }
            return new StealingResult
            {
                Applied = true,
                Stolen = stolen,
                Criminal = caught,
                LootId = loot,
                SkillBefore = before,
                SkillAfter = after,
                FailReason = caught ? (req.InGuardZone ? "guard" : "witness") : (stolen ? "" : "empty")
            };
        }

        public static string LowestLoot(int gold, int cloth)
        {
            if (gold > 0)
                return "gold";
            if (cloth > 0)
                return ItemCatalog.Cloth;
            return "";
        }

        static StealingResult Fail(string reason) => new StealingResult { FailReason = reason };
    }

}
