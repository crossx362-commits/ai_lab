using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed partial class OfflineWorld
    {
        public MusicianshipResult TryPlay(WorldBody body)
        {
            if (body == null)
                return new MusicianshipResult { FailReason = "no_body" };
            if (body.Ghost)
                return new MusicianshipResult { FailReason = "ghost" };
            int id = body.GetInstanceID();
            if (!nextPlayAt.TryGetValue(id, out float ready))
                ready = 0f;
            var bag = body.GetComponent<InventoryBag>();
            bool has = bag != null && ItemCatalog.Has(bag.Items, ItemCatalog.Lute);
            var req = new MusicianshipRequest
            {
                HasInstrument = has,
                Now = Time.time,
                NextPlayAt = ready,
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                Difficulty = MusicianshipResolve.Difficulty
            };
            MusicianshipResult result = MusicianshipResolve.Resolve(req);
            if (!result.Applied)
                return result;
            nextPlayAt[id] = Time.time + MusicianshipResolve.CooldownSeconds;
            if (bag != null && ItemCatalog.MaxUsesOf(ItemCatalog.Lute) > 0)
                bag.WearTool(ItemCatalog.Lute);
            int calmed = 0;
            var others = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            Vector3 origin = body.transform.position;
            for (int i = 0; i < others.Length; i++)
            {
                WorldBody other = others[i];
                if (other == null || other == body || !other.IsEnemy || !other.Alive)
                    continue;
                if (Vector3.Distance(origin, other.transform.position) > MusicianshipResolve.Range)
                    continue;
                other.CalmUntil = Time.time + MusicianshipResolve.CalmSeconds;
                calmed++;
            }
            result.Calmed = calmed;
            LastPlayMessage = "연주 진정 " + calmed;
            OpLog.Write("play", PersistDriver.AccountKey(), body.DisplayName, LastPlayMessage);
            return result;
        }

        public PeacemakingResult TryPeace(WorldBody body, WorldBody target)
        {
            if (body == null)
                return new PeacemakingResult { FailReason = "no_body" };
            if (body.Ghost)
                return new PeacemakingResult { FailReason = "ghost" };
            if (target == null)
                return new PeacemakingResult { FailReason = "no_target" };
            int id = body.GetInstanceID();
            if (!nextPeaceAt.TryGetValue(id, out float ready))
                ready = 0f;
            var bag = body.GetComponent<InventoryBag>();
            bool has = bag != null && ItemCatalog.Has(bag.Items, ItemCatalog.Lute);
            var req = new PeacemakingRequest
            {
                HasInstrument = has,
                HasTarget = true,
                TargetEnemy = target.IsEnemy,
                TargetAlive = target.Alive,
                Distance = Vector3.Distance(body.transform.position, target.transform.position),
                Range = PeacemakingResolve.Range,
                Now = Time.time,
                NextPeaceAt = ready,
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                Difficulty = PeacemakingResolve.Difficulty
            };
            PeacemakingResult result = PeacemakingResolve.Resolve(req);
            if (!result.Applied)
                return result;
            nextPeaceAt[id] = Time.time + PeacemakingResolve.CooldownSeconds;
            if (bag != null && ItemCatalog.MaxUsesOf(ItemCatalog.Lute) > 0)
                bag.WearTool(ItemCatalog.Lute);
            target.CalmUntil = Time.time + PeacemakingResolve.PeaceSeconds;
            LastPeaceMessage = target.DisplayName + " 평화 " + PeacemakingResolve.PeaceSeconds.ToString("0") + "초";
            OpLog.Write("peace", PersistDriver.AccountKey(), target.DisplayName, LastPeaceMessage);
            return result;
        }


        public ProvocationResult TryProvoke(WorldBody body, WorldBody first, WorldBody second)
        {
            if (body == null)
                return new ProvocationResult { FailReason = "no_body" };
            if (body.Ghost)
                return new ProvocationResult { FailReason = "ghost" };
            if (first == null || second == null)
                return new ProvocationResult { FailReason = "no_target" };
            int id = body.GetInstanceID();
            if (!nextProvokeAt.TryGetValue(id, out float ready))
                ready = 0f;
            var bag = body.GetComponent<InventoryBag>();
            bool has = bag != null && ItemCatalog.Has(bag.Items, ItemCatalog.Lute);
            var req = new ProvocationRequest
            {
                HasInstrument = has,
                HasTargetA = true,
                HasTargetB = true,
                TargetAEnemy = first.IsEnemy && !first.IsAvatar,
                TargetBEnemy = second.IsEnemy && !second.IsAvatar,
                TargetAAlive = first.Alive,
                TargetBAlive = second.Alive,
                SameTarget = first == second,
                DistanceA = Vector3.Distance(body.transform.position, first.transform.position),
                DistanceB = Vector3.Distance(body.transform.position, second.transform.position),
                Range = ProvocationResolve.Range,
                Now = Time.time,
                NextProvokeAt = ready,
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                Difficulty = ProvocationResolve.Difficulty
            };
            ProvocationResult result = ProvocationResolve.Resolve(req);
            if (!result.Applied)
                return result;
            nextProvokeAt[id] = Time.time + ProvocationResolve.CooldownSeconds;
            if (bag != null && ItemCatalog.MaxUsesOf(ItemCatalog.Lute) > 0)
                bag.WearTool(ItemCatalog.Lute);
            first.CalmUntil = 0f;
            second.CalmUntil = 0f;
            first.ProvokeUntil = Time.time + ProvocationResolve.FightSeconds;
            second.ProvokeUntil = Time.time + ProvocationResolve.FightSeconds;
            first.ProvokePartner = second;
            second.ProvokePartner = first;
            LastProvokeMessage = first.DisplayName + " vs " + second.DisplayName + " 도발 " + ProvocationResolve.FightSeconds.ToString("0") + "초";
            OpLog.Write("provoke", PersistDriver.AccountKey(), first.DisplayName, LastProvokeMessage);
            return result;
        }


        public HidingResult TryHide(WorldBody body)
        {
            if (body == null)
                return new HidingResult { FailReason = "no_body" };
            int id = body.GetInstanceID();
            if (!nextHideAt.TryGetValue(id, out float ready))
                ready = 0f;
            var req = new HidingRequest
            {
                Ghost = body.Ghost,
                Now = Time.time,
                NextHideAt = ready,
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                Difficulty = HidingResolve.Difficulty
            };
            HidingResult result = HidingResolve.Resolve(req);
            if (!result.Applied)
                return result;
            nextHideAt[id] = Time.time + HidingResolve.CooldownSeconds;
            body.HiddenUntil = Time.time + HidingResolve.HideSeconds;
            body.StealthUntil = 0f;
            lastHiddenPos[id] = body.transform.position;
            LastHideMessage = "은신 " + HidingResolve.HideSeconds.ToString("0") + "초";
            OpLog.Write("hide", PersistDriver.AccountKey(), body.DisplayName, LastHideMessage);
            return result;
        }

        public StealthResult TryStealth(WorldBody body)
        {
            if (body == null)
                return new StealthResult { FailReason = "no_body" };
            int id = body.GetInstanceID();
            if (!nextStealthAt.TryGetValue(id, out float ready))
                ready = 0f;
            var req = new StealthRequest
            {
                Ghost = body.Ghost,
                AlreadyHidden = body.IsHidden(Time.time),
                Now = Time.time,
                NextStealthAt = ready,
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                Difficulty = StealthResolve.Difficulty
            };
            StealthResult result = StealthResolve.Resolve(req);
            if (!result.Applied)
                return result;
            nextStealthAt[id] = Time.time + StealthResolve.CooldownSeconds;
            body.HiddenUntil = Time.time + StealthResolve.StealthSeconds;
            body.StealthUntil = Time.time + StealthResolve.StealthSeconds;
            lastHiddenPos[id] = body.transform.position;
            LastStealthMessage = "잠행 " + StealthResolve.StealthSeconds.ToString("0") + "초";
            OpLog.Write("stealth", PersistDriver.AccountKey(), body.DisplayName, LastStealthMessage);
            return result;
        }

        public DetectHiddenResult TryDetectHidden(WorldBody body)
        {
            if (body == null)
                return new DetectHiddenResult { FailReason = "no_body" };
            int id = body.GetInstanceID();
            if (!nextDetectAt.TryGetValue(id, out float ready))
                ready = 0f;
            float now = Time.time;
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            float nearest = 0f;
            bool found = false;
            for (int i = 0; i < list.Length; i++)
            {
                WorldBody other = list[i];
                if (other == null || other == body || !other.IsHidden(now))
                    continue;
                float d = Vector3.Distance(body.transform.position, other.transform.position);
                if (d > DetectHiddenResolve.DetectRange)
                    continue;
                if (!found || d < nearest)
                {
                    nearest = d;
                    found = true;
                }
            }
            var req = new DetectHiddenRequest
            {
                Ghost = body.Ghost,
                Now = now,
                NextDetectAt = ready,
                HasHiddenTarget = found,
                Distance = found ? nearest : 0f,
                Range = DetectHiddenResolve.DetectRange,
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                Difficulty = DetectHiddenResolve.Difficulty
            };
            DetectHiddenResult result = DetectHiddenResolve.Resolve(req);
            if (!result.Applied)
                return result;
            nextDetectAt[id] = now + DetectHiddenResolve.CooldownSeconds;
            int revealed = 0;
            for (int i = 0; i < list.Length; i++)
            {
                WorldBody other = list[i];
                if (other == null || other == body || !other.IsHidden(now))
                    continue;
                float d = Vector3.Distance(body.transform.position, other.transform.position);
                if (d > DetectHiddenResolve.DetectRange)
                    continue;
                other.BreakHide();
                revealed++;
            }
            LastDetectMessage = revealed > 0 ? "감지 " + revealed : "감지";
            OpLog.Write("detect", PersistDriver.AccountKey(), body.DisplayName, LastDetectMessage);
            return result;
        }



        public CampingResult TryCamp(WorldBody body)
        {
            if (body == null)
                return new CampingResult { FailReason = "no_body" };
            int id = body.GetInstanceID();
            if (!nextCampAt.TryGetValue(id, out float ready))
                ready = 0f;
            var fire = GameObject.Find("Campfire");
            float dist = fire != null ? Vector3.Distance(body.transform.position, fire.transform.position) : 99f;
            bool near = fire != null && dist <= CampingResolve.CampRange;
            var bag = body.GetComponent<InventoryBag>();
            bool hasWood = bag != null && ItemCatalog.Has(bag.Items, "wood");
            var req = new CampingRequest
            {
                Ghost = body.Ghost,
                Now = Time.time,
                NextCampAt = ready,
                NearCampfire = near,
                HasKindling = hasWood,
                Distance = dist,
                Range = CampingResolve.CampRange,
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                Difficulty = CampingResolve.Difficulty
            };
            CampingResult result = CampingResolve.Resolve(req);
            if (!result.Applied)
                return result;
            nextCampAt[id] = Time.time + CampingResolve.CooldownSeconds;
            if (!near)
            {
                bag = Bag(body);
                if (!bag.TakeOne("wood"))
                    return new CampingResult { FailReason = "no_kindling" };
            }
            body.CampSafeUntil = Time.time + CampingResolve.SafeSeconds;
            LastCampMessage = "야영 " + CampingResolve.SafeSeconds.ToString("0") + "초";
            OpLog.Write("camp", PersistDriver.AccountKey(), body.DisplayName, LastCampMessage);
            return result;
        }


        public StealingResult TrySteal(WorldBody body)
        {
            if (body == null)
                return new StealingResult { FailReason = "no_body" };
            int id = body.GetInstanceID();
            if (!nextStealAt.TryGetValue(id, out float ready))
                ready = 0f;
            LockedCrate pack = NearestStealPack(body.transform.position, out float dist);
            bool witnessed = HasStealWitness(body);
            var req = new StealingRequest
            {
                Ghost = body.Ghost,
                Now = Time.time,
                NextStealAt = ready,
                HasPack = pack != null,
                Distance = pack != null ? dist : 99f,
                Range = pack != null ? pack.InteractRange : StealingResolve.StealRange,
                PackGold = pack != null ? pack.GoldLoot : 0,
                PackCloth = pack != null ? pack.ClothLoot : 0,
                InGuardZone = GuardZone.Contains(body.transform.position.x, body.transform.position.z),
                Witnessed = witnessed,
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                Difficulty = StealingResolve.Difficulty
            };
            StealingResult result = StealingResolve.Resolve(req);
            if (!result.Applied)
                return result;
            nextStealAt[id] = Time.time + StealingResolve.CooldownSeconds;
            if (result.Criminal)
                FlagCriminal(body);
            if (result.Stolen && pack != null)
            {
                if (result.LootId == "gold")
                {
                    pack.GoldLoot -= 1;
                    body.Gold += 1;
                }
                else if (result.LootId == ItemCatalog.Cloth)
                {
                    pack.ClothLoot -= 1;
                    Bag(body).Add(ItemCatalog.Cloth, 1);
                }
                LastStealMessage = "훔침";
            }
            else if (result.Criminal)
                LastStealMessage = "들킴";
            else
                LastStealMessage = "훔치기";
            OpLog.Write("steal", PersistDriver.AccountKey(), body.DisplayName, LastStealMessage);
            return result;
        }

        public static WorldBody NearestGhostAvatar(WorldBody healer)
        {
            if (healer == null)
                return null;
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            WorldBody best = null;
            float bestDist = ItemCatalog.MeleeRange;
            Vector3 pos = healer.transform.position;
            for (int i = 0; i < list.Length; i++)
            {
                WorldBody b = list[i];
                if (b == null || b == healer || !b.IsAvatar || !b.Ghost)
                    continue;
                float d = Vector3.Distance(pos, b.transform.position);
                if (d <= bestDist)
                {
                    best = b;
                    bestDist = d;
                }
            }
            return best;
        }

        static LockedCrate NearestStealPack(Vector3 pos, out float dist)
        {
            var list = Object.FindObjectsByType<LockedCrate>(FindObjectsSortMode.None);
            LockedCrate best = null;
            dist = 99f;
            for (int i = 0; i < list.Length; i++)
            {
                LockedCrate crate = list[i];
                if (crate == null)
                    continue;
                float d = Vector3.Distance(pos, crate.transform.position);
                if (best == null || d < dist)
                {
                    best = crate;
                    dist = d;
                }
            }
            return best;
        }

        static bool HasStealWitness(WorldBody body)
        {
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                WorldBody other = list[i];
                if (other == null || other == body || !other.IsAvatar || other.Ghost || !other.Alive)
                    continue;
                float d = Vector3.Distance(body.transform.position, other.transform.position);
                if (d <= StealingResolve.WitnessRange)
                    return true;
            }
            return false;
        }



        public LockpickingResult TryPick(WorldBody body, LockedCrate crate)
        {
            if (body == null)
                return new LockpickingResult { FailReason = "no_body" };
            int id = body.GetInstanceID();
            if (!nextPickAt.TryGetValue(id, out float ready))
                ready = 0f;
            var bag = body.GetComponent<InventoryBag>();
            bool hasPick = bag != null && ItemCatalog.Has(bag.Items, ItemCatalog.Lockpick);
            float dist = crate != null ? Vector3.Distance(body.transform.position, crate.transform.position) : 99f;
            float range = crate != null ? crate.InteractRange : 2.4f;
            var req = new LockpickingRequest
            {
                Ghost = body.Ghost,
                HasCrate = crate != null,
                CrateOpened = crate != null && crate.Opened,
                HasLockpick = hasPick,
                Distance = dist,
                Range = range,
                Now = Time.time,
                NextPickAt = ready,
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                Difficulty = LockpickingResolve.Difficulty
            };
            LockpickingResult result = LockpickingResolve.Resolve(req);
            if (!result.Applied)
                return result;
            nextPickAt[id] = Time.time + LockpickingResolve.CooldownSeconds;
            bag = Bag(body);
            if (!bag.TakeOne(ItemCatalog.Lockpick))
                return new LockpickingResult { FailReason = "no_pick" };
            crate.Opened = true;
            body.Gold += crate.GoldLoot;
            if (crate.ClothLoot > 0)
            {
                bag.Add(ItemCatalog.Cloth, crate.ClothLoot);
                if (bag.Overweight(StatsOf(body).Str))
                    bag.TakeOne(ItemCatalog.Cloth);
            }
            LastPickMessage = crate.DisplayName + " 열림 +" + crate.GoldLoot + "G";
            OpLog.Write("pick", PersistDriver.AccountKey(), body.DisplayName, LastPickMessage);
            return result;
        }

        public void TickHiddenMovement(float now)
        {
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                WorldBody body = list[i];
                if (body == null || !body.IsHidden(now))
                    continue;
                int id = body.GetInstanceID();
                Vector3 pos = body.transform.position;
                if (!lastHiddenPos.TryGetValue(id, out Vector3 last))
                {
                    lastHiddenPos[id] = pos;
                    continue;
                }
                Vector3 delta = pos - last;
                delta.y = 0f;
                lastHiddenPos[id] = pos;
                if (delta.sqrMagnitude < 0.0004f)
                    continue;
                if (body.CanMoveHidden(now))
                    continue;
                body.BreakHide();
            }
        }

        public ProvocationResult TryProvokeStep(WorldBody body)
        {
            WorldBody sel = Selected;
            if (PendingProvoke == null || PendingProvoke == sel)
            {
                if (sel == null)
                    return new ProvocationResult { FailReason = "no_target" };
                if (!sel.IsEnemy || sel.IsAvatar)
                    return new ProvocationResult { FailReason = "not_mob" };
                if (!sel.Alive)
                    return new ProvocationResult { FailReason = "dead" };
                PendingProvoke = sel;
                LastProvokeMessage = sel.DisplayName + " 도발 대상1";
                return new ProvocationResult { FailReason = "need_second" };
            }
            ProvocationResult result = TryProvoke(body, PendingProvoke, sel);
            PendingProvoke = null;
            return result;
        }

        public void TickProvoke(float now)
        {
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                WorldBody a = list[i];
                if (a == null || !a.Alive || now >= a.ProvokeUntil)
                {
                    if (a != null && now >= a.ProvokeUntil)
                        a.ProvokePartner = null;
                    continue;
                }
                WorldBody b = a.ProvokePartner;
                if (b == null || !b.Alive || now >= b.ProvokeUntil)
                {
                    a.ProvokePartner = null;
                    a.ProvokeUntil = 0f;
                    continue;
                }
                StrikeProvoke(a, b, now);
            }
        }

        void StrikeProvoke(WorldBody attacker, WorldBody defender, float now)
        {
            if (attacker == null || defender == null || !defender.Alive)
                return;
            if (now < attacker.CalmUntil)
                return;
            if (attacker.IsRooted(now))
                return;
            float dist = Vector3.Distance(attacker.transform.position, defender.transform.position);
            if (dist > ItemCatalog.MeleeRange)
                return;
            int id = attacker.GetInstanceID();
            if (!nextAttackAt.TryGetValue(id, out float ready))
                ready = 0f;
            if (now < ready)
                return;
            nextAttackAt[id] = now + attackCooldown;
            defender.ApplyDamage(AttackResolve.RetaliationDamage);
        }

        public bool TryEnemyStrike(WorldBody enemy, WorldBody defender)
        {
            if (enemy == null || defender == null || !defender.Alive || defender.Ghost)
                return false;
            if (defender.IsHidden(Time.time))
                return false;
            if (Time.time < enemy.CalmUntil)
                return false;
            if (enemy.IsRooted(Time.time))
                return false;
            float dist = Vector3.Distance(enemy.transform.position, defender.transform.position);
            if (dist > ItemCatalog.MeleeRange)
                return false;
            int dmg = AttackResolve.RetaliationDamage;
            if (dmg > 0 && enemy.IsWeakened(Time.time))
                dmg = dmg / 2;
            if (dmg > 0 && enemy.IsBlessed(Time.time))
                dmg = (dmg * 5) / 4;
            var bag = defender.GetComponent<InventoryBag>();
            bool shield = bag != null && ItemCatalog.HasShield(bag.Items);
            AttackResolve.TryParry(SkillsOf(defender), StatsOf(defender), shield, 20f, ref dmg, out _, out _);
            if (shield)
                bag.WearTool(ItemCatalog.WoodenShield);
            defender.ApplyDamage(dmg);
            if (defender.IsAvatar && enemy.IsEnemy)
                DefendOwner(defender, enemy);
            return true;
        }

        void DefendOwner(WorldBody owner, WorldBody attacker)
        {
            if (owner == null || attacker == null || !owner.IsAvatar || !attacker.IsEnemy || !attacker.Alive)
                return;
            if (string.IsNullOrEmpty(owner.CharacterId))
                return;
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var pet = list[i];
                if (pet == null || pet == attacker || pet.PetStabled || !pet.PetGuard || !pet.Alive)
                    continue;
                if (pet.OwnerCharacterId != owner.CharacterId)
                    continue;
                TryAttack(pet, attacker);
            }
        }

    }
}
