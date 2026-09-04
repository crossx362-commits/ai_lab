using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed partial class OfflineWorld
    {
        public AttackResult TryAttack(WorldBody target)
        {
            return TryAttack(Player, target);
        }

        public AttackResult TryAttack(WorldBody attacker, WorldBody target)
        {
            if (attacker == null || target == null)
                return new AttackResult { FailReason = "no_target" };
            if (attacker.Ghost)
                return new AttackResult { FailReason = "ghost" };
            if (!target.IsEnemy)
            {
                var ga = GuildOf(attacker);
                var gb = GuildOf(target);
                bool fieldDuel = DuelResolve.FieldDuel(
                    attacker.IsAvatar, target.IsAvatar, AtDuel(attacker, target),
                    attacker.transform.position.x, attacker.transform.position.z,
                    target.transform.position.x, target.transform.position.z);
                bool fieldWar = GuildWarResolve.FieldWar(
                    attacker.IsAvatar, target.IsAvatar,
                    ga != null ? ga.Id : "", gb != null ? gb.Id : "",
                    ga != null ? ga.WarWithId : "", gb != null ? gb.WarWithId : "",
                    attacker.transform.position.x, attacker.transform.position.z,
                    target.transform.position.x, target.transform.position.z);
                if (fieldDuel)
                {
                    // agreed duel: apply vs Innocent, no Criminal (field-only)
                }
                else if (fieldWar)
                {
                    // agreed guild war: apply vs Innocent, no Criminal
                }
                else if (AtDuel(attacker, target) || AtWar(attacker, target))
                {
                    return new AttackResult { FailReason = "innocent" };
                }
                else
                {
                    bool outdoor = PvpResolve.OutdoorOpen(
                        attacker.IsAvatar, target.IsAvatar, attacker.IsEnemy, target.IsEnemy,
                        attacker.transform.position.x, attacker.transform.position.z,
                        target.transform.position.x, target.transform.position.z);
                    FlagCriminal(attacker);
                    if (!outdoor)
                    {
                        if (GuardZone.Contains(attacker.transform.position.x, attacker.transform.position.z))
                            GuardStrike(attacker);
                        return new AttackResult { FailReason = "innocent" };
                    }
                }
            }
            if (TooHeavy(attacker))
                return new AttackResult { FailReason = "overweight" };
            var atkBag = attacker.GetComponent<InventoryBag>();
            string weapon = atkBag != null ? ItemCatalog.CombatWeaponOf(atkBag.Items) : "";
            SkillId weaponSkill = ItemCatalog.CombatSkillOf(weapon);
            if (!string.IsNullOrEmpty(weapon) && StatsOf(attacker).Str < ItemCatalog.StrReqOf(weapon))
                return new AttackResult { FailReason = "str_req" };

            int id = attacker.GetInstanceID();
            if (!nextAttackAt.TryGetValue(id, out float ready))
                ready = 0f;

            var req = new AttackRequest
            {
                Distance = Vector3.Distance(attacker.transform.position, target.transform.position),
                Range = ItemCatalog.CombatRangeOf(weaponSkill),
                Now = Time.time,
                NextAttackAt = ready,
                TargetAlive = target.Alive,
                Skills = SkillsOf(attacker),
                Stats = StatsOf(attacker),
                WeaponSkill = weaponSkill,
                Exceptional = atkBag != null && ItemCatalog.IsExceptional(atkBag.Items, weapon)
            };
            AttackResult result = AttackResolve.Resolve(req);
            if (!result.Applied)
                return result;
            attacker.BreakHide();
            attacker.CombatUntil = Time.time + TravelMark.CombatSeconds;
            if (attacker.IsAvatar)
                attacker.RecalcFromStr(StatsOf(attacker).Str);

            nextAttackAt[id] = Time.time + attackCooldown;
            if (!string.IsNullOrEmpty(weapon) && atkBag != null && ItemCatalog.MaxUsesOf(weapon) > 0)
                atkBag.WearTool(weapon);
            int dmg = result.Damage;
            if (dmg > 0 && attacker.IsWeakened(Time.time))
                dmg = dmg / 2;
            if (dmg > 0 && attacker.IsBlessed(Time.time))
                dmg = (dmg * 5) / 4;
            if (dmg > 0 && target.IsWarded(Time.time))
                dmg = dmg / 2;
            result.Damage = dmg;
            target.ApplyDamage(dmg);
            if (dmg > 0 && target.IsCasting(Time.time))
                target.ClearCast();
            if (ItemCatalog.IsMeleeWeapon(weapon) && poisonedWeapon.TryGetValue(id, out bool charged) && charged)
            {
                poisonedWeapon[id] = false;
                target.PoisonTicks = PoisoningResolve.TickCount;
                target.NextPoisonAt = Time.time;
                TickPoison(Time.time);
            }
            if (target.IsAvatar && attacker.IsEnemy)
                DefendOwner(target, attacker);
            if (!target.Alive)
            {
                if (Selected == target)
                    Selected = null;
                bool duelKill = AtDuel(attacker, target);
                if (duelKill)
                    ClearDuel(attacker);
                if (attacker.IsAvatar && target.IsAvatar && !target.IsEnemy && !AtWar(attacker, target) && !duelKill)
                {
                    attacker.MurderCount++;
                    if (PvpResolve.ShouldFlagMurderer(attacker.MurderCount))
                        attacker.Notoriety = NotorietyId.Murderer;
                }
                else if (attacker.IsAvatar)
                {
                    attacker.Fame += 10;
                    attacker.Karma += 1;
                    OpLog.Write("fame", PersistDriver.AccountKey(), target.name, "kill +10");
                    if (MobCatalog.IsBoss(target.MobId))
                    {
                        string drop = MobCatalog.KillDropOf(target.MobId);
                        if (!string.IsNullOrEmpty(drop))
                        {
                            Bag(attacker).Add(drop, 1);
                            OpLog.Write("drop", PersistDriver.AccountKey(), target.MobId, drop);
                        }
                    }
                }
            }
            else if (attacker.IsAvatar && target.IsEnemy && weaponSkill != SkillId.Archery && weaponSkill != SkillId.Fencing)
                TryEnemyStrike(target, attacker);
            return result;
        }

        public AttackResult TryHeal(WorldBody healer)
        {
            return TryHeal(healer, healer);
        }

        public AttackResult TryHeal(WorldBody healer, WorldBody target)
        {
            if (healer == null)
                return new AttackResult { FailReason = "no_body" };
            if (target == null)
                target = healer;
            if (healer.Ghost)
                return new AttackResult { FailReason = "ghost" };
            if (target.Ghost && target.IsAvatar)
                return TryResurrectBandage(healer, target);
            if (target.PoisonTicks > 0 && target.Alive && !target.Ghost)
                return TryCurePoison(healer, target);
            if (target.IsEnemy)
                return new AttackResult { FailReason = "enemy" };
            var bag = healer.GetComponent<InventoryBag>();
            bool has = bag != null && ItemCatalog.Has(bag.Items, ItemCatalog.Bandage);
            int id = healer.GetInstanceID();
            if (!nextHealAt.TryGetValue(id, out float ready))
                ready = 0f;
            var req = new HealRequest
            {
                Distance = Vector3.Distance(healer.transform.position, target.transform.position),
                Range = ItemCatalog.MeleeRange,
                Now = Time.time,
                NextHealAt = ready,
                TargetAlive = target.Alive,
                TargetGhost = target.Ghost,
                TargetHp = target.Hp,
                TargetMaxHp = target.MaxHp,
                Skills = SkillsOf(healer),
                Stats = StatsOf(healer),
                HasBandage = has,
                Difficulty = HealResolve.Difficulty
            };
            AttackResult result = HealResolve.Resolve(req);
            if (!result.Applied)
                return result;
            if (!bag.TakeOne(ItemCatalog.Bandage))
                return new AttackResult { FailReason = "no_bandage" };
            nextHealAt[id] = Time.time + HealResolve.Cooldown(StatsOf(healer));
            float nextHp = target.Hp + result.Damage;
            if (nextHp > target.MaxHp)
                nextHp = target.MaxHp;
            target.SetHp(nextHp);
            if (healer.IsAvatar)
                healer.RecalcFromStr(StatsOf(healer).Str);
            OpLog.Write("heal", PersistDriver.AccountKey(), target.DisplayName, "bandage +" + result.Damage);
            return result;
        }

        public AttackResult TryResurrectBandage(WorldBody healer, WorldBody target)
        {
            if (healer == null)
                return new AttackResult { FailReason = "no_body" };
            if (target == null)
                return new AttackResult { FailReason = "no_target" };
            var bag = healer.GetComponent<InventoryBag>();
            bool has = bag != null && ItemCatalog.Has(bag.Items, ItemCatalog.Bandage);
            var req = new BandageResurrectRequest
            {
                Distance = Vector3.Distance(healer.transform.position, target.transform.position),
                Range = ItemCatalog.MeleeRange,
                HealerGhost = healer.Ghost,
                TargetGhost = target.Ghost,
                TargetAvatar = target.IsAvatar,
                HasBandage = has,
                Skills = SkillsOf(healer),
                Stats = StatsOf(healer),
                Difficulty = BandageResurrectResolve.Difficulty
            };
            AttackResult result = BandageResurrectResolve.Resolve(req);
            if (!result.Applied)
                return result;
            if (bag == null || !bag.TakeOne(ItemCatalog.Bandage))
                return new AttackResult { FailReason = "no_bandage" };
            target.Resurrect();
            if (healer.IsAvatar)
                healer.RecalcFromStr(StatsOf(healer).Str);
            LastHealRezMessage = target.DisplayName + " 부활";
            OpLog.Write("rez", PersistDriver.AccountKey(), target.DisplayName, "bandage");
            return result;
        }

        public AttackResult TryCurePoison(WorldBody healer, WorldBody target)
        {
            if (healer == null)
                return new AttackResult { FailReason = "no_body" };
            if (target == null)
                target = healer;
            if (target.IsEnemy)
                return new AttackResult { FailReason = "enemy" };
            var bag = healer.GetComponent<InventoryBag>();
            bool has = bag != null && ItemCatalog.Has(bag.Items, ItemCatalog.Bandage);
            var req = new BandageCurePoisonRequest
            {
                Distance = Vector3.Distance(healer.transform.position, target.transform.position),
                Range = ItemCatalog.MeleeRange,
                HealerGhost = healer.Ghost,
                TargetGhost = target.Ghost,
                TargetAlive = target.Alive,
                PoisonTicks = target.PoisonTicks,
                HasBandage = has,
                Skills = SkillsOf(healer),
                Stats = StatsOf(healer),
                Difficulty = BandageCurePoisonResolve.Difficulty
            };
            AttackResult result = BandageCurePoisonResolve.Resolve(req);
            if (!result.Applied)
                return result;
            if (bag == null || !bag.TakeOne(ItemCatalog.Bandage))
                return new AttackResult { FailReason = "no_bandage" };
            target.PoisonTicks = 0;
            target.NextPoisonAt = 0f;
            if (healer.IsAvatar)
                healer.RecalcFromStr(StatsOf(healer).Str);
            LastCurePoisonMessage = target.DisplayName + " 해독";
            OpLog.Write("cure", PersistDriver.AccountKey(), target.DisplayName, "bandage");
            return result;
        }

        public AttackResult TryVet(WorldBody healer, WorldBody target)
        {
            if (healer == null)
                return new AttackResult { FailReason = "no_body" };
            if (healer.Ghost)
                return new AttackResult { FailReason = "ghost" };
            if (target == null)
                return new AttackResult { FailReason = "no_target" };
            if (target.Ghost && target.Bonded && !string.IsNullOrEmpty(target.OwnerCharacterId) && !target.IsAvatar)
                return TryVetResurrect(healer, target);
            var bag = healer.GetComponent<InventoryBag>();
            bool has = bag != null && ItemCatalog.Has(bag.Items, ItemCatalog.Bandage);
            int id = healer.GetInstanceID();
            if (!nextVetAt.TryGetValue(id, out float ready))
                ready = 0f;
            var req = new VeterinaryRequest
            {
                Distance = Vector3.Distance(healer.transform.position, target.transform.position),
                Range = ItemCatalog.MeleeRange,
                Now = Time.time,
                NextVetAt = ready,
                HasTarget = true,
                TargetEnemy = target.IsEnemy,
                TargetAlive = target.Alive,
                TargetGhost = target.Ghost,
                TargetHp = target.Hp,
                TargetMaxHp = target.MaxHp,
                Skills = SkillsOf(healer),
                Stats = StatsOf(healer),
                HasBandage = has,
                Difficulty = VeterinaryResolve.Difficulty
            };
            AttackResult result = VeterinaryResolve.Resolve(req);
            if (!result.Applied)
                return result;
            if (!bag.TakeOne(ItemCatalog.Bandage))
                return new AttackResult { FailReason = "no_bandage" };
            nextVetAt[id] = Time.time + VeterinaryResolve.Cooldown(StatsOf(healer));
            float nextHp = target.Hp + result.Damage;
            if (nextHp > target.MaxHp)
                nextHp = target.MaxHp;
            target.SetHp(nextHp);
            LastVetMessage = target.DisplayName + " +" + result.Damage.ToString("0");
            OpLog.Write("vet", PersistDriver.AccountKey(), target.DisplayName, "bandage +" + result.Damage);
            return result;
        }

        public AttackResult TryVetResurrect(WorldBody healer, WorldBody target)
        {
            if (healer == null)
                return new AttackResult { FailReason = "no_body" };
            if (target == null)
                return new AttackResult { FailReason = "no_target" };
            var bag = healer.GetComponent<InventoryBag>();
            bool has = bag != null && ItemCatalog.Has(bag.Items, ItemCatalog.Bandage);
            bool bondedPet = target.Bonded && !string.IsNullOrEmpty(target.OwnerCharacterId) && !target.IsAvatar;
            var req = new VeterinaryResurrectRequest
            {
                Distance = Vector3.Distance(healer.transform.position, target.transform.position),
                Range = ItemCatalog.MeleeRange,
                HealerGhost = healer.Ghost,
                TargetGhost = target.Ghost,
                TargetBondedPet = bondedPet,
                HasBandage = has,
                Skills = SkillsOf(healer),
                Stats = StatsOf(healer),
                Difficulty = VeterinaryResurrectResolve.Difficulty
            };
            AttackResult result = VeterinaryResurrectResolve.Resolve(req);
            if (!result.Applied)
                return result;
            if (bag == null || !bag.TakeOne(ItemCatalog.Bandage))
                return new AttackResult { FailReason = "no_bandage" };
            target.Resurrect();
            target.PetFollow = true;
            target.PetGuard = false;
            target.PetAttackTarget = null;
            if (healer.IsAvatar)
                healer.RecalcFromStr(StatsOf(healer).Str);
            LastVetRezMessage = target.DisplayName + " 부활";
            LastVetMessage = LastVetRezMessage;
            OpLog.Write("vetrez", PersistDriver.AccountKey(), target.DisplayName, "bandage");
            return result;
        }


        public AttackResult TryInscribe(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            var bag = body.GetComponent<InventoryBag>();
            var book = BookOf(body);
            bool hasCloth = bag != null && ItemCatalog.Has(bag.Items, ItemCatalog.Cloth);
            bool hasBlank = bag != null && ItemCatalog.Has(bag.Items, ItemCatalog.Blank);
            var req = new InscriptionRequest
            {
                KnowsEmber = book.Knows(SpellId.Ember),
                HasCloth = hasCloth,
                HasBlank = hasBlank,
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                Difficulty = InscriptionResolve.Difficulty
            };
            AttackResult result = InscriptionResolve.Resolve(req);
            if (!result.Applied)
                return result;
            bool took = false;
            if (hasBlank)
                took = bag.TakeOne(ItemCatalog.Blank);
            else if (hasCloth)
                took = bag.TakeOne(ItemCatalog.Cloth);
            if (!took)
                return new AttackResult { FailReason = "no_material" };
            bag.Add(ItemCatalog.ScrollEmber, 1);
            if (body.IsAvatar)
                body.RecalcFromInt(StatsOf(body).Int);
            LastInscribeMessage = ItemCatalog.ScrollEmber;
            OpLog.Write("inscribe", PersistDriver.AccountKey(), body.DisplayName, ItemCatalog.ScrollEmber);
            return result;
        }

        public AttackResult TryPoisonWeapon(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            var bag = body.GetComponent<InventoryBag>();
            bool hasPotion = bag != null && ItemCatalog.Has(bag.Items, ItemCatalog.HealthPotion);
            bool hasVial = bag != null && (ItemCatalog.Has(bag.Items, ItemCatalog.PoisonVial) || ItemCatalog.Has(bag.Items, ItemCatalog.Cloth));
            bool hasMelee = bag != null && ItemCatalog.HasMelee(bag.Items);
            var req = new PoisonWeaponRequest
            {
                HasMelee = hasMelee,
                HasPotion = hasPotion,
                HasVial = hasVial,
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                Difficulty = PoisoningResolve.Difficulty
            };
            AttackResult result = PoisoningResolve.Resolve(req);
            if (!result.Applied)
                return result;
            bool took = false;
            if (bag != null && ItemCatalog.Has(bag.Items, ItemCatalog.PoisonVial))
                took = bag.TakeOne(ItemCatalog.PoisonVial);
            else if (hasPotion)
                took = bag.TakeOne(ItemCatalog.HealthPotion);
            else if (bag != null && ItemCatalog.Has(bag.Items, ItemCatalog.Cloth))
                took = bag.TakeOne(ItemCatalog.Cloth);
            if (!took)
                return new AttackResult { FailReason = "no_poison" };
            poisonedWeapon[body.GetInstanceID()] = true;
            LastPoisonMessage = "poison";
            OpLog.Write("poison", PersistDriver.AccountKey(), body.DisplayName, "weapon");
            return result;
        }

        public void TickPoison(float now)
        {
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var body = list[i];
                if (body == null || body.PoisonTicks <= 0)
                    continue;
                while (body.PoisonTicks > 0 && now >= body.NextPoisonAt)
                {
                    if (!body.Alive)
                    {
                        body.PoisonTicks = 0;
                        break;
                    }
                    body.ApplyDamage(PoisoningResolve.TickDamage);
                    body.PoisonTicks--;
                    body.NextPoisonAt += PoisoningResolve.TickInterval;
                }
            }
        }

        public AttackResult TryUseScroll(WorldBody body, WorldBody target)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            var bag = body.GetComponent<InventoryBag>();
            bool has = bag != null && ItemCatalog.Has(bag.Items, ItemCatalog.ScrollEmber);
            var req = new ScrollUseRequest
            {
                Distance = target != null ? Vector3.Distance(body.transform.position, target.transform.position) : 999f,
                Range = SpellCast.EmberRange,
                HasScroll = has,
                HasTarget = target != null && target != body,
                TargetEnemy = target != null && target.IsEnemy != body.IsEnemy,
                TargetAlive = target != null && target.Alive,
                TargetGhost = target != null && target.Ghost,
                Skills = SkillsOf(body),
                Stats = StatsOf(body)
            };
            AttackResult result = ScrollUseResolve.Resolve(req);
            if (!result.Applied)
                return result;
            if (!bag.TakeOne(ItemCatalog.ScrollEmber))
                return new AttackResult { FailReason = "no_scroll" };
            var targetSkills = SkillsOf(target);
            var targetStats = StatsOf(target);
            var targetBag = target.GetComponent<InventoryBag>();
            int gear = ItemCatalog.EquipmentMagicResist(targetBag != null ? targetBag.Items : null);
            int dmg = result.Damage;
            MagicResistResolve.TryResist(targetSkills, targetStats, gear, MagicResistResolve.Difficulty, ref dmg, out _, out _);
            target.ApplyDamage(dmg);
            body.BreakHide();
            body.CombatUntil = Time.time + TravelMark.CombatSeconds;
            if (target.IsAvatar)
                target.RecalcFromInt(targetStats.Int);
            if (!target.Alive && Selected == target)
                Selected = null;
            result.Damage = dmg;
            OpLog.Write("scroll", PersistDriver.AccountKey(), target.DisplayName, ItemCatalog.ScrollEmber);
            return result;
        }




        public AttackResult TryDrink(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            if (!body.Alive)
                return new AttackResult { FailReason = "dead" };
            if (body.Hp >= body.MaxHp - 0.01f)
                return new AttackResult { FailReason = "full" };
            var bag = body.GetComponent<InventoryBag>();
            if (bag == null || !ItemCatalog.Has(bag.Items, ItemCatalog.HealthPotion))
                return new AttackResult { FailReason = "no_potion" };
            if (!bag.TakeOne(ItemCatalog.HealthPotion))
                return new AttackResult { FailReason = "no_potion" };
            const int heal = 12;
            float nextHp = body.Hp + heal;
            if (nextHp > body.MaxHp)
                nextHp = body.MaxHp;
            body.SetHp(nextHp);
            OpLog.Write("drink", PersistDriver.AccountKey(), body.DisplayName, "health_potion +" + heal);
            return new AttackResult { Applied = true, Hit = true, Damage = heal };
        }

        public AttackResult TryMeditate(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            int id = body.GetInstanceID();
            if (!nextMeditateAt.TryGetValue(id, out float ready))
                ready = 0f;
            var bag = body.GetComponent<InventoryBag>();
            var req = new MeditationRequest
            {
                Now = Time.time,
                NextMeditateAt = ready,
                Ghost = body.Ghost,
                Mana = body.Mana,
                MaxMana = body.MaxMana,
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                HeavyArmor = bag != null && ItemCatalog.HasHeavyArmor(bag.Items),
                Difficulty = MeditationResolve.Difficulty
            };
            AttackResult result = MeditationResolve.Resolve(req);
            if (!result.Applied)
                return result;
            nextMeditateAt[id] = Time.time + MeditationResolve.CooldownSeconds;
            float next = body.Mana + result.Damage;
            if (next > body.MaxMana)
                next = body.MaxMana;
            body.SetMana(next);
            if (body.IsAvatar)
                body.RecalcFromInt(StatsOf(body).Int);
            OpLog.Write("meditate", PersistDriver.AccountKey(), body.DisplayName, "mana +" + result.Damage);
            return result;
        }

        public EvalIntResult TryEvaluate(WorldBody body, WorldBody target)
        {
            if (body == null)
                return new EvalIntResult { FailReason = "no_body" };
            if (body.Ghost)
                return new EvalIntResult { FailReason = "ghost" };
            if (target == null)
                return new EvalIntResult { FailReason = "no_target" };
            int id = body.GetInstanceID();
            if (!nextEvalAt.TryGetValue(id, out float ready))
                ready = 0f;
            var targetStats = StatsOf(target);
            target.RecalcFromInt(targetStats.Int);
            var req = new EvalIntRequest
            {
                Distance = Vector3.Distance(body.transform.position, target.transform.position),
                Range = EvalIntResolve.Range,
                Now = Time.time,
                NextEvalAt = ready,
                TargetAlive = target.Alive,
                TargetGhost = target.Ghost,
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                TargetStats = targetStats,
                TargetMana = target.Mana,
                TargetMaxMana = target.MaxMana,
                Difficulty = EvalIntResolve.Difficulty
            };
            EvalIntResult result = EvalIntResolve.Resolve(req);
            if (!result.Applied)
                return result;
            nextEvalAt[id] = Time.time + EvalIntResolve.CooldownSeconds;
            LastEvalMessage = target.DisplayName + " INT " + result.Intelligence + " MP " + result.Mana + "/" + result.MaxMana;
            if (body.IsAvatar)
                body.RecalcFromInt(StatsOf(body).Int);
            OpLog.Write("evalint", PersistDriver.AccountKey(), target.DisplayName, LastEvalMessage);
            return result;
        }

        public TrackingResult TryTrack(WorldBody body, WorldBody target)
        {
            if (body == null)
                return new TrackingResult { FailReason = "no_body" };
            if (body.Ghost)
                return new TrackingResult { FailReason = "ghost" };
            if (target == null)
                return new TrackingResult { FailReason = "no_target" };
            int id = body.GetInstanceID();
            if (!nextTrackAt.TryGetValue(id, out float ready))
                ready = 0f;
            Vector3 pos = target.transform.position;
            var req = new TrackingRequest
            {
                Distance = Vector3.Distance(body.transform.position, pos),
                Range = TrackingResolve.Range,
                Now = Time.time,
                NextTrackAt = ready,
                HasTarget = true,
                IsCorpse = false,
                TargetAlive = target.Alive,
                TargetKind = string.IsNullOrEmpty(target.DisplayName) ? target.MobId : target.DisplayName,
                Hp = target.Hp,
                MaxHp = target.MaxHp,
                LastX = pos.x,
                LastZ = pos.z,
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                Difficulty = TrackingResolve.Difficulty
            };
            TrackingResult result = TrackingResolve.Resolve(req);
            if (!result.Applied)
                return result;
            nextTrackAt[id] = Time.time + TrackingResolve.CooldownSeconds;
            LastTrackMessage = result.Kind + " HP " + result.Hp.ToString("0") + "/" + result.MaxHp.ToString("0");
            OpLog.Write("track", PersistDriver.AccountKey(), result.Kind, LastTrackMessage);
            return result;
        }

        public TrackingResult TryTrackCorpse(WorldBody body, CorpseNode node)
        {
            if (body == null)
                return new TrackingResult { FailReason = "no_body" };
            if (body.Ghost)
                return new TrackingResult { FailReason = "ghost" };
            if (node == null)
                return new TrackingResult { FailReason = "no_target" };
            int id = body.GetInstanceID();
            if (!nextTrackAt.TryGetValue(id, out float ready))
                ready = 0f;
            Vector3 pos = node.transform.position;
            float lx = node.LastX;
            float lz = node.LastZ;
            var req = new TrackingRequest
            {
                Distance = Vector3.Distance(body.transform.position, pos),
                Range = TrackingResolve.Range,
                Now = Time.time,
                NextTrackAt = ready,
                HasTarget = true,
                IsCorpse = true,
                TargetAlive = false,
                TargetKind = string.IsNullOrEmpty(node.LastKind) ? "시체" : node.LastKind,
                LastX = lx,
                LastZ = lz,
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                Difficulty = TrackingResolve.Difficulty
            };
            TrackingResult result = TrackingResolve.Resolve(req);
            if (!result.Applied)
                return result;
            nextTrackAt[id] = Time.time + TrackingResolve.CooldownSeconds;
            LastTrackMessage = result.Kind + " 마지막 " + result.LastPosition;
            OpLog.Write("track", PersistDriver.AccountKey(), result.Kind, LastTrackMessage);
            return result;
        }


        public AnimalLoreResult TryLore(WorldBody body, WorldBody target)
        {
            if (body == null)
                return new AnimalLoreResult { FailReason = "no_body" };
            if (body.Ghost)
                return new AnimalLoreResult { FailReason = "ghost" };
            if (target == null)
                return new AnimalLoreResult { FailReason = "no_target" };
            int id = body.GetInstanceID();
            if (!nextLoreAt.TryGetValue(id, out float ready))
                ready = 0f;
            MobCatalog.LoreStats(target.MobId, out int str, out int resist, out int dmgMin, out int dmgMax);
            var req = new AnimalLoreRequest
            {
                Distance = Vector3.Distance(body.transform.position, target.transform.position),
                Range = AnimalLoreResolve.Range,
                Now = Time.time,
                NextLoreAt = ready,
                HasTarget = true,
                TargetEnemy = target.IsEnemy,
                TargetAlive = target.Alive,
                TargetKind = string.IsNullOrEmpty(target.DisplayName) ? target.MobId : target.DisplayName,
                MobId = target.MobId,
                Hp = target.Hp,
                MaxHp = target.MaxHp,
                Str = str,
                Resist = resist,
                DamageMin = dmgMin,
                DamageMax = dmgMax,
                Tamable = MobCatalog.TamableOf(target.MobId),
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                Difficulty = AnimalLoreResolve.Difficulty
            };
            AnimalLoreResult result = AnimalLoreResolve.Resolve(req);
            if (!result.Applied)
                return result;
            nextLoreAt[id] = Time.time + AnimalLoreResolve.CooldownSeconds;
            LastLoreMessage = result.Kind + " HP " + result.Hp.ToString("0") + "/" + result.MaxHp.ToString("0")
                + " STR " + result.Str + " 저항 " + result.Resist + " 피해 " + result.DamageBand + " 조련불가";
            OpLog.Write("animallore", PersistDriver.AccountKey(), result.Kind, LastLoreMessage);
            return result;
        }



    }
}
