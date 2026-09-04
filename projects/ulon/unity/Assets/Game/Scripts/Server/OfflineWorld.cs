using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed class OfflineWorld : MonoBehaviour
    {
        static OfflineWorld instance;
        public static OfflineWorld Instance
        {
            get
            {
                if (instance == null)
                    instance = FindFirstObjectByType<OfflineWorld>();
                return instance;
            }
            private set => instance = value;
        }

        [SerializeField] float attackCooldown = 1.1f;
        float nextGuardAt;

        readonly Dictionary<int, SkillSet> skills = new Dictionary<int, SkillSet>();
        readonly Dictionary<int, StatSet> stats = new Dictionary<int, StatSet>();
        readonly Dictionary<int, Spellbook> books = new Dictionary<int, Spellbook>();
        readonly Dictionary<int, float> nextAttackAt = new Dictionary<int, float>();
        readonly Dictionary<int, float> nextHealAt = new Dictionary<int, float>();
        readonly Dictionary<int, float> nextMeditateAt = new Dictionary<int, float>();
        readonly Dictionary<int, float> nextEvalAt = new Dictionary<int, float>();
        readonly Dictionary<int, float> nextTrackAt = new Dictionary<int, float>();
        readonly Dictionary<int, float> nextPlayAt = new Dictionary<int, float>();
        readonly Dictionary<int, float> nextPeaceAt = new Dictionary<int, float>();
        readonly Dictionary<int, float> nextProvokeAt = new Dictionary<int, float>();
        readonly Dictionary<int, float> nextHideAt = new Dictionary<int, float>();
        readonly Dictionary<int, float> nextStealthAt = new Dictionary<int, float>();
        readonly Dictionary<int, float> nextDetectAt = new Dictionary<int, float>();
        readonly Dictionary<int, float> nextCampAt = new Dictionary<int, float>();
        readonly Dictionary<int, float> nextStealAt = new Dictionary<int, float>();
        readonly Dictionary<int, float> nextPickAt = new Dictionary<int, float>();
        readonly Dictionary<int, float> nextVetAt = new Dictionary<int, float>();
        readonly Dictionary<int, float> nextLoreAt = new Dictionary<int, float>();
        readonly Dictionary<int, Vector3> lastHiddenPos = new Dictionary<int, Vector3>();
        readonly Dictionary<int, bool> poisonedWeapon = new Dictionary<int, bool>();
        readonly Dictionary<int, string> equipped = new Dictionary<int, string>();
        readonly Dictionary<string, HouseRecord> houses = new Dictionary<string, HouseRecord>();
        readonly Dictionary<string, StableRecord> stables = new Dictionary<string, StableRecord>();

        sealed class HouseRecord
        {
            public string PlotId = HousingPlot.Id;
            public string OwnerCharacterId = "";
            public string OwnerAccountId = "";
            public int PublicFlag;
            public readonly List<ItemRecord> Items = new List<ItemRecord>();
            public ItemRecord VendorItem;
        }

        sealed class StableRecord
        {
            public string CharacterId = "";
            public string PetId = "";
            public int ControlSlots = 1;
            public string DisplayName = "";
        }

        public string LastEvalMessage { get; private set; } = "";
        public string LastTrackMessage { get; private set; } = "";
        public string LastPlayMessage { get; private set; } = "";
        public string LastPeaceMessage { get; private set; } = "";
        public string LastProvokeMessage { get; private set; } = "";
        public string LastHideMessage { get; private set; } = "";
        public string LastStealthMessage { get; private set; } = "";
        public string LastDetectMessage { get; private set; } = "";
        public string LastCampMessage { get; private set; } = "";
        public string LastStealMessage { get; private set; } = "";
        public string LastHealRezMessage { get; private set; } = "";
        public string LastCurePoisonMessage { get; private set; } = "";
        public string LastPickMessage { get; private set; } = "";
        public string LastLoreMessage { get; private set; } = "";
        public string LastVetMessage { get; private set; } = "";
        public string LastVetRezMessage { get; private set; } = "";
        public string LastInscribeMessage { get; private set; } = "";
        public string LastPoisonMessage { get; private set; } = "";
        public string LastCraftOrderMessage { get; private set; } = "";
        public string LastTameMessage { get; private set; } = "";
        public string LastStableMessage { get; private set; } = "";
        public string LastTravelMessage { get; private set; } = "";
        WorldBody[] bodies = System.Array.Empty<WorldBody>();

        public WorldBody Player { get; private set; }
        public WorldBody Selected { get; private set; }
        public WorldBody PendingProvoke { get; private set; }
        public TradeSession ActiveTrade { get; private set; }
        public VendorStation ActiveVendor { get; private set; }

        public void CloseVendor() => ActiveVendor = null;
        public TrainerStation ActiveTrainer { get; private set; }
        public void CloseTrainer() => ActiveTrainer = null;
        public Party ActiveParty { get; private set; }
        readonly Dictionary<string, Guild> guilds = new Dictionary<string, Guild>();
        int guildSeq;
        public string LastGuildMessage { get; private set; } = "";

        public Guild FindGuild(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return guilds.TryGetValue(id, out var g) ? g : null;
        }

        public Guild GuildOf(WorldBody body)
        {
            return body == null ? null : FindGuild(body.GuildId);
        }

        public bool AtWar(WorldBody a, WorldBody b)
        {
            var ga = GuildOf(a);
            var gb = GuildOf(b);
            if (ga == null || gb == null || ga == gb)
                return false;
            return ga.WarWithId == gb.Id && gb.WarWithId == ga.Id;
        }

        public bool AtDuel(WorldBody a, WorldBody b)
        {
            if (a == null || b == null || a == b)
                return false;
            return a.DuelOpponent == b && b.DuelOpponent == a;
        }

        public string LastDuelMessage { get; private set; } = "";
        public string LastEquipMessage { get; private set; } = "";
        public string LastWeightMessage { get; private set; } = "";
        public string LastSpeechMessage { get; private set; } = "";
        public SkillSet PlayerSkills => Player != null ? SkillsOf(Player) : new SkillSet();
        public StatSet PlayerStats => Player != null ? StatsOf(Player) : new StatSet();

        void OnEnable()
        {
            instance = this;
        }

        void Awake()
        {
            instance = this;
            EnsureBanker();
            EnsureHealer();
            EnsureResinBush();
            EnsureFishSpot();
            EnsureCampfire();
            EnsureMortar();
            EnsureFieldOak();
            EnsureFieldFlax();
            EnsureFieldOre();
            EnsureVendor();
            EnsureLockedCrate();
            EnsureTrainer();
            EnsureCarpenter();
            EnsureDungeon1Runtime();
            EnsureDungeon2Runtime();
            EnsureFieldBossRuntime();
            bodies = FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].ResetHp();
                if (bodies[i].IsEnemy)
                    continue;
                if (bodies[i].IsAvatar)
                {
                    Player = bodies[i];
                    bodies[i].RecalcFromStr(StatsOf(bodies[i]).Str);
                    bodies[i].RecalcFromInt(StatsOf(bodies[i]).Int);
                    bodies[i].ResetHp();
                }
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public SkillSet SkillsOf(WorldBody body)
        {
            int id = body.GetInstanceID();
            if (!skills.TryGetValue(id, out SkillSet set))
            {
                set = new SkillSet();
                skills[id] = set;
            }
            return set;
        }

        public string TitleOf(WorldBody body)
        {
            return SkillTitles.Of(SkillsOf(body));
        }

        public string ReputationTitleOf(WorldBody body)
        {
            if (body == null)
                return "";
            return ReputationTitles.Of(body.Notoriety, body.Fame, body.Karma);
        }

        public StatSet StatsOf(WorldBody body)
        {
            int id = body.GetInstanceID();
            if (!stats.TryGetValue(id, out StatSet set))
            {
                set = new StatSet();
                stats[id] = set;
            }
            return set;
        }

        public Spellbook BookOf(WorldBody body)
        {
            int id = body.GetInstanceID();
            if (!books.TryGetValue(id, out Spellbook book))
            {
                book = new Spellbook();
                books[id] = book;
            }
            return book;
        }

        void Update()
        {
            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            for (int i = 0; i < nodes.Length; i++)
                nodes[i].Tick(Time.time);
            TickCorpses(Time.time);
            TickGroundItems(Time.time);
            TickGuard(Time.time);
            TickProvoke(Time.time);
            TickHiddenMovement(Time.time);
            TickPoison(Time.time);
            TickCast(Time.time);
            TickPets();
            if (Player == null || Player.Ghost)
                return;
            Player.SetMana(Player.Mana + Time.deltaTime);
        }

        public void TickGuard(float now)
        {
            if (Player == null || Player.Ghost)
                return;
            if (Player.Notoriety == NotorietyId.Criminal && now >= Player.CriminalUntil)
                Player.Notoriety = NotorietyId.Innocent;
            if (Player.Notoriety == NotorietyId.Innocent)
                return;
            if (!GuardZone.Contains(Player.transform.position.x, Player.transform.position.z))
                return;
            if (now < nextGuardAt)
                return;
            nextGuardAt = now + 1.6f;
            GuardStrike(Player);
        }

        public void FlagCriminal(WorldBody body)
        {
            if (body == null)
                return;
            if (body.Notoriety < NotorietyId.Murderer)
                body.Notoriety = NotorietyId.Criminal;
            body.CriminalUntil = Time.time + 120f;
            OpLog.Write("guard", PersistDriver.AccountKey(), body.name, "criminal");
        }

        public void GuardStrike(WorldBody body)
        {
            if (body == null || body.Ghost)
                return;
            body.ApplyDamage(12);
            nextGuardAt = Time.time + 1.6f;
            OpLog.Write("guard", PersistDriver.AccountKey(), body.name, "strike");
        }

        public void TickCorpses(float now)
        {
            var list = Object.FindObjectsByType<CorpseNode>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                if (now - list[i].SpawnedAt < list[i].DecaySeconds)
                    continue;
                KillGo(list[i].gameObject);
            }
        }

        public const float GroundDecaySeconds = 30f;

        public void TickGroundItems(float now)
        {
            var list = Object.FindObjectsByType<GroundItem>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == null || !list[i].Expired(now))
                    continue;
                KillGo(list[i].gameObject);
            }
        }

        public GroundItem SpawnGroundItem(ItemRecord item, Vector3 position, float decaySeconds = -1f)
        {
            if (decaySeconds < 0f)
                decaySeconds = GroundDecaySeconds;
            var go = new GameObject("GroundItem");
            go.transform.position = position;
            var node = go.AddComponent<GroundItem>();
            node.GroundId = System.Guid.NewGuid().ToString("N");
            node.Item = item;
            if (item.Amount <= 0)
            {
                var fixedItem = item;
                fixedItem.Amount = 1;
                node.Item = fixedItem;
            }
            node.DecayAt = Time.time + decaySeconds;
            OpLog.Write("drop", "", node.GroundId, string.IsNullOrEmpty(item.TemplateId) ? "ground" : item.TemplateId);
            return node;
        }

        public GroundItem SpawnGroundItem(string templateId, Vector3 position, int amount = 1, float decaySeconds = -1f)
        {
            var rec = new ItemRecord { TemplateId = templateId, Amount = amount > 0 ? amount : 1 };
            return SpawnGroundItem(rec, position, decaySeconds);
        }

        public static int CountGroundItems(string templateId = null)
        {
            var list = Object.FindObjectsByType<GroundItem>(FindObjectsSortMode.None);
            int n = 0;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == null)
                    continue;
                if (!string.IsNullOrEmpty(templateId) && list[i].Item.TemplateId != templateId)
                    continue;
                n++;
            }
            return n;
        }

        public int CountHouseSecureItems(string plotId = null)
        {
            if (string.IsNullOrEmpty(plotId))
                plotId = HousingPlot.Id;
            var rec = RecordOf(plotId);
            return rec != null ? rec.Items.Count : 0;
        }

        public void Select(WorldBody body) => Selected = body;

        public void SetLocalPlayer(WorldBody body)
        {
            Player = body;
            if (body != null)
                body.IsAvatar = true;
        }

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



        public int CountFollowers(string ownerCharacterId)
        {
            if (string.IsNullOrEmpty(ownerCharacterId))
                return 0;
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            int n = 0;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] != null && list[i].OwnerCharacterId == ownerCharacterId
                    && !list[i].PetStabled && list[i].gameObject.activeInHierarchy)
                    n += list[i].ControlSlots < 1 ? 1 : list[i].ControlSlots;
            }
            return n;
        }

        public void TickPets()
        {
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var pet = list[i];
                if (pet == null || string.IsNullOrEmpty(pet.OwnerCharacterId) || pet.PetStabled || !pet.Alive)
                    continue;
                if (pet.PetAttackTarget != null)
                {
                    WorldBody prey = pet.PetAttackTarget;
                    if (prey == null || !prey.Alive || !prey.IsEnemy || prey.IsAvatar)
                    {
                        pet.PetAttackTarget = null;
                    }
                    else
                    {
                        Vector3 t = prey.transform.position;
                        pet.transform.position = new Vector3(t.x + 0.6f, pet.transform.position.y, t.z + 0.6f);
                        TryAttack(pet, prey);
                        if (prey == null || !prey.Alive)
                            pet.PetAttackTarget = null;
                        continue;
                    }
                }
                if (!pet.PetFollow)
                    continue;
                WorldBody owner = null;
                for (int j = 0; j < list.Length; j++)
                {
                    if (list[j] != null && list[j].IsAvatar && list[j].CharacterId == pet.OwnerCharacterId)
                    {
                        owner = list[j];
                        break;
                    }
                }
                if (owner == null)
                    continue;
                Vector3 o = owner.transform.position;
                pet.transform.position = new Vector3(o.x + TameCritter.FollowOffsetX, pet.transform.position.y, o.z + TameCritter.FollowOffsetZ);
            }
        }

        public AttackResult TryTame(WorldBody body, WorldBody target)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (target == null)
                return new AttackResult { FailReason = "no_target" };
            var req = new TameRequest
            {
                Distance = Vector3.Distance(body.transform.position, target.transform.position),
                Range = TameResolve.Range,
                Ghost = body.Ghost,
                Tameable = target.Tameable || MobCatalog.TamableOf(target.MobId),
                AlreadyPet = !string.IsNullOrEmpty(target.OwnerCharacterId),
                UsedSlots = CountFollowers(body.CharacterId),
                ControlSlots = target.ControlSlots < 1 ? TameCritter.ControlSlots : target.ControlSlots,
                FollowerCap = TameResolve.FollowerCap,
                Skills = SkillsOf(body),
                Stats = StatsOf(body),
                Difficulty = TameResolve.Difficulty
            };
            AttackResult result = TameResolve.Tame(req);
            if (!result.Applied)
            {
                LastTameMessage = result.FailReason;
                return result;
            }
            string ownerId = body.CharacterId;
            if (string.IsNullOrEmpty(ownerId))
                ownerId = PersistDriver.AccountKey();
            if (string.IsNullOrEmpty(ownerId))
                ownerId = "local";
            target.OwnerCharacterId = ownerId;
            if (string.IsNullOrEmpty(body.CharacterId))
                body.CharacterId = ownerId;
            target.PetFollow = true;
            target.PetGuard = false;
            target.PetAttackTarget = null;
            target.IsEnemy = false;
            target.Tameable = true;
            target.Bonded = true;
            LastTameMessage = target.DisplayName + " 조련";
            OpLog.Write("tame", PersistDriver.AccountKey(), target.DisplayName, LastTameMessage);
            return result;
        }

        public AttackResult TryPetFollow(WorldBody body, WorldBody target)
        {
            var req = new PetCommandRequest
            {
                Ghost = body == null || body.Ghost,
                HasPet = target != null && !string.IsNullOrEmpty(target.OwnerCharacterId),
                IsOwner = body != null && target != null && !string.IsNullOrEmpty(body.CharacterId) && target.OwnerCharacterId == body.CharacterId
            };
            AttackResult result = TameResolve.Follow(req);
            if (!result.Applied)
                return result;
            target.PetFollow = true;
            target.PetGuard = false;
            target.PetAttackTarget = null;
            LastTameMessage = "따라와";
            return result;
        }

        public AttackResult TryPetStay(WorldBody body, WorldBody target)
        {
            var req = new PetCommandRequest
            {
                Ghost = body == null || body.Ghost,
                HasPet = target != null && !string.IsNullOrEmpty(target.OwnerCharacterId),
                IsOwner = body != null && target != null && !string.IsNullOrEmpty(body.CharacterId) && target.OwnerCharacterId == body.CharacterId
            };
            AttackResult result = TameResolve.Stay(req);
            if (!result.Applied)
                return result;
            target.PetFollow = false;
            target.PetGuard = false;
            target.PetAttackTarget = null;
            LastTameMessage = "머물러";
            return result;
        }

        public AttackResult TryPetGuard(WorldBody body, WorldBody target)
        {
            var req = new PetCommandRequest
            {
                Ghost = body == null || body.Ghost,
                HasPet = target != null && !string.IsNullOrEmpty(target.OwnerCharacterId),
                IsOwner = body != null && target != null && !string.IsNullOrEmpty(body.CharacterId) && target.OwnerCharacterId == body.CharacterId
            };
            AttackResult result = TameResolve.Guard(req);
            if (!result.Applied)
                return result;
            target.PetFollow = true;
            target.PetGuard = true;
            target.PetAttackTarget = null;
            LastTameMessage = "지켜";
            return result;
        }

        public AttackResult TryPetRelease(WorldBody body, WorldBody target)
        {
            var req = new PetCommandRequest
            {
                Ghost = body == null || body.Ghost,
                HasPet = target != null && !string.IsNullOrEmpty(target.OwnerCharacterId),
                IsOwner = body != null && target != null && !string.IsNullOrEmpty(body.CharacterId) && target.OwnerCharacterId == body.CharacterId
            };
            AttackResult result = TameResolve.Release(req);
            if (!result.Applied)
                return result;
            target.OwnerCharacterId = "";
            target.PetFollow = false;
            target.PetGuard = false;
            target.PetAttackTarget = null;
            target.Bonded = false;
            LastTameMessage = "놓아줌";
            OpLog.Write("tame", PersistDriver.AccountKey(), target.DisplayName, LastTameMessage);
            return result;
        }

        public AttackResult TryPetAttack(WorldBody body, WorldBody pet, WorldBody enemy)
        {
            var req = new PetCommandRequest
            {
                Ghost = body == null || body.Ghost,
                HasPet = pet != null && !string.IsNullOrEmpty(pet.OwnerCharacterId),
                IsOwner = body != null && pet != null && !string.IsNullOrEmpty(body.CharacterId) && pet.OwnerCharacterId == body.CharacterId,
                PetAlive = pet != null && pet.Alive,
                PetStabled = pet != null && pet.PetStabled,
                HasEnemy = enemy != null && enemy.IsEnemy && enemy.Alive && !enemy.IsAvatar
            };
            AttackResult result = TameResolve.Attack(req);
            if (!result.Applied)
                return result;
            pet.PetFollow = false;
            pet.PetGuard = false;
            pet.PetAttackTarget = enemy;
            LastTameMessage = "공격";
            return result;
        }

        public AttackResult TryPetCome(WorldBody body, WorldBody pet)
        {
            var req = new PetCommandRequest
            {
                Ghost = body == null || body.Ghost,
                HasPet = pet != null && !string.IsNullOrEmpty(pet.OwnerCharacterId),
                IsOwner = body != null && pet != null && !string.IsNullOrEmpty(body.CharacterId) && pet.OwnerCharacterId == body.CharacterId,
                PetAlive = pet != null && pet.Alive,
                PetStabled = pet != null && pet.PetStabled
            };
            AttackResult result = TameResolve.Come(req);
            if (!result.Applied)
                return result;
            pet.PetFollow = true;
            pet.PetGuard = false;
            pet.PetAttackTarget = null;
            LastTameMessage = "이리와";
            return result;
        }

        public static StableMaster FindStable(string name)
        {
            if (string.IsNullOrEmpty(name))
                name = StableYard.Object;
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<StableMaster>() : null;
        }

        public bool HasStabled(string characterId)
        {
            return TryGetStable(characterId, out _);
        }

        public void ClearStabled(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return;
            stables.Remove(characterId);
            PersistStable(new StableRecord { CharacterId = characterId });
        }

        bool TryGetStable(string characterId, out StableRecord rec)
        {
            rec = null;
            if (string.IsNullOrEmpty(characterId))
                return false;
            if (stables.TryGetValue(characterId, out rec) && rec != null && !string.IsNullOrEmpty(rec.PetId))
                return true;
            var snap = CharacterStore.LoadStable(characterId);
            if (snap != null && !string.IsNullOrEmpty(snap.PetId))
            {
                rec = new StableRecord
                {
                    CharacterId = characterId,
                    PetId = snap.PetId,
                    ControlSlots = snap.ControlSlots < 1 ? 1 : snap.ControlSlots,
                    DisplayName = snap.DisplayName ?? ""
                };
                stables[characterId] = rec;
                return true;
            }
            rec = null;
            return false;
        }

        WorldBody FindOwnedFollower(string ownerCharacterId)
        {
            if (string.IsNullOrEmpty(ownerCharacterId))
                return null;
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var pet = list[i];
                if (pet == null || pet.PetStabled)
                    continue;
                if (pet.OwnerCharacterId == ownerCharacterId)
                    return pet;
            }
            return null;
        }

        WorldBody FindPetBody(string petId)
        {
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var pet = list[i];
                if (pet == null)
                    continue;
                if (!string.IsNullOrEmpty(petId) && pet.MobId == petId)
                    return pet;
                if (pet.gameObject.name == TameCritter.Object)
                    return pet;
            }
            return null;
        }

        static void HidePet(WorldBody pet, bool hide)
        {
            if (pet == null)
                return;
            pet.gameObject.SetActive(!hide);
        }

        void PersistStable(StableRecord rec)
        {
            if (rec == null || string.IsNullOrEmpty(rec.CharacterId))
                return;
            CharacterStore.SaveStable(new StableSnapshot
            {
                CharacterId = rec.CharacterId,
                PetId = rec.PetId ?? "",
                ControlSlots = rec.ControlSlots < 1 ? 1 : rec.ControlSlots,
                DisplayName = rec.DisplayName ?? ""
            });
        }

        public AttackResult TryStable(WorldBody body, StableMaster stable)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (stable == null)
                return new AttackResult { FailReason = "no_stable" };
            string ownerId = body.CharacterId;
            if (string.IsNullOrEmpty(ownerId))
                ownerId = PersistDriver.AccountKey();
            if (string.IsNullOrEmpty(ownerId))
                ownerId = "local";
            if (string.IsNullOrEmpty(body.CharacterId))
                body.CharacterId = ownerId;
            var pet = FindOwnedFollower(ownerId);
            var req = new StableRequest
            {
                Distance = Vector3.Distance(body.transform.position, stable.transform.position),
                Range = stable.InteractRange,
                Ghost = body.Ghost,
                HasFollower = pet != null,
                HasStabled = TryGetStable(ownerId, out _),
                Gold = body.Gold,
                GoldCost = StableYard.GoldCost,
                HasStable = true
            };
            AttackResult result = StableResolve.Park(req);
            if (!result.Applied)
            {
                LastStableMessage = result.FailReason;
                return result;
            }
            body.Gold -= StableYard.GoldCost;
            var rec = new StableRecord
            {
                CharacterId = ownerId,
                PetId = string.IsNullOrEmpty(pet.MobId) ? TameCritter.Id : pet.MobId,
                ControlSlots = pet.ControlSlots < 1 ? TameCritter.ControlSlots : pet.ControlSlots,
                DisplayName = pet.DisplayName
            };
            stables[ownerId] = rec;
            pet.PetFollow = false;
            pet.PetGuard = false;
            pet.PetAttackTarget = null;
            pet.PetStabled = true;
            pet.OwnerCharacterId = "";
            pet.Bonded = false;
            HidePet(pet, true);
            PersistStable(rec);
            LastStableMessage = "마구간 맡김";
            OpLog.Write("stable", PersistDriver.AccountKey(), rec.DisplayName, LastStableMessage);
            return result;
        }

        public AttackResult TryClaimStable(WorldBody body, StableMaster stable)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (stable == null)
                return new AttackResult { FailReason = "no_stable" };
            string ownerId = body.CharacterId;
            if (string.IsNullOrEmpty(ownerId))
                ownerId = PersistDriver.AccountKey();
            if (string.IsNullOrEmpty(ownerId))
                ownerId = "local";
            if (string.IsNullOrEmpty(body.CharacterId))
                body.CharacterId = ownerId;
            TryGetStable(ownerId, out StableRecord rec);
            var req = new StableRequest
            {
                Distance = Vector3.Distance(body.transform.position, stable.transform.position),
                Range = stable.InteractRange,
                Ghost = body.Ghost,
                HasStabled = rec != null && !string.IsNullOrEmpty(rec.PetId),
                UsedSlots = CountFollowers(ownerId),
                ControlSlots = rec != null && rec.ControlSlots > 0 ? rec.ControlSlots : 1,
                FollowerCap = TameResolve.FollowerCap,
                HasStable = true
            };
            AttackResult result = StableResolve.Claim(req);
            if (!result.Applied)
            {
                LastStableMessage = result.FailReason;
                return result;
            }
            var pet = FindPetBody(rec.PetId);
            if (pet == null)
                return new AttackResult { FailReason = "no_pet" };
            HidePet(pet, false);
            pet.PetStabled = false;
            pet.OwnerCharacterId = ownerId;
            pet.PetFollow = true;
            pet.PetGuard = false;
            pet.PetAttackTarget = null;
            pet.IsEnemy = false;
            pet.Tameable = true;
            pet.Bonded = true;
            Vector3 s = stable.transform.position;
            pet.transform.position = new Vector3(s.x + TameCritter.FollowOffsetX, pet.transform.position.y, s.z + TameCritter.FollowOffsetZ);
            stables.Remove(ownerId);
            PersistStable(new StableRecord { CharacterId = ownerId });
            LastStableMessage = "마구간 찾음";
            OpLog.Write("stable", PersistDriver.AccountKey(), pet.DisplayName, LastStableMessage);
            return result;
        }

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

        public static ResourceNode FindNode(string name)
        {
            var nodes = Object.FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
            for (int i = 0; i < nodes.Length; i++)
                if (nodes[i].gameObject.name == name)
                    return nodes[i];
            return null;
        }

        public static CraftStation FindStation(string name)
        {
            var stations = Object.FindObjectsByType<CraftStation>(FindObjectsSortMode.None);
            for (int i = 0; i < stations.Length; i++)
                if (stations[i].gameObject.name == name)
                    return stations[i];
            return null;
        }

        public static BankStation FindBank(string name)
        {
            var banks = Object.FindObjectsByType<BankStation>(FindObjectsSortMode.None);
            for (int i = 0; i < banks.Length; i++)
                if (banks[i].gameObject.name == name)
                    return banks[i];
            return null;
        }

        public static HousePlotStation FindHouseStation(string name)
        {
            var stations = Object.FindObjectsByType<HousePlotStation>(FindObjectsSortMode.None);
            for (int i = 0; i < stations.Length; i++)
                if (stations[i].gameObject.name == name)
                    return stations[i];
            return null;
        }

        public static HouseChest FindHouseChest(string name)
        {
            var chests = Object.FindObjectsByType<HouseChest>(FindObjectsSortMode.None);
            for (int i = 0; i < chests.Length; i++)
                if (chests[i].gameObject.name == name)
                    return chests[i];
            return null;
        }

        public static HouseVendor FindHouseVendor(string name)
        {
            var list = Object.FindObjectsByType<HouseVendor>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
                if (list[i].gameObject.name == name)
                    return list[i];
            return list.Length > 0 ? list[0] : null;
        }

        static void EnsureBanker()
        {
            var go = GameObject.Find("Banker");
            if (go == null)
                go = GameObject.Find("windmill");
            if (go == null)
                return;
            go.name = "Banker";
            if (go.GetComponent<BankStation>() == null)
            {
                var bank = go.AddComponent<BankStation>();
                bank.DisplayName = "은행";
            }
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }

        static void EnsureHealer()
        {
            var go = GameObject.Find("Healer");
            if (go == null)
                go = GameObject.Find("fountain-round");
            if (go == null)
                return;
            go.name = "Healer";
            if (go.GetComponent<HealerStation>() == null)
            {
                var healer = go.AddComponent<HealerStation>();
                healer.DisplayName = "치유사";
            }
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }

        static void EnsureResinBush()
        {
            var go = GameObject.Find("ResinBush");
            if (go == null)
                go = GameObject.Find("plant_bushLarge");
            if (go == null)
                go = GameObject.Find("plant_bush");
            if (go == null)
                return;
            go.name = "ResinBush";
            var node = go.GetComponent<ResourceNode>() ?? go.AddComponent<ResourceNode>();
            node.ResourceId = SpellCast.Reagent;
            node.DisplayName = "수지 덤불";
            node.GatherSkill = SkillId.Magery;
            if (node.Remaining <= 0)
                node.Remaining = 8;
            node.Capacity = 8;
            node.Difficulty = 8f;
            node.RespawnSeconds = 8f;
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }


        static void EnsureFieldOak()
        {
            var go = GameObject.Find("FieldOak");
            if (go == null)
                return;
            var node = go.GetComponent<ResourceNode>() ?? go.AddComponent<ResourceNode>();
            node.ResourceId = "wood";
            node.DisplayName = "들판 참나무";
            node.GatherSkill = SkillId.Lumberjacking;
            if (node.Remaining <= 0)
                node.Remaining = 12;
            node.Capacity = 12;
            node.Difficulty = 10f;
            node.RespawnSeconds = 8f;
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }

        static void EnsureFieldFlax()
        {
            var go = GameObject.Find("FieldFlax");
            if (go == null)
                return;
            var node = go.GetComponent<ResourceNode>() ?? go.AddComponent<ResourceNode>();
            node.ResourceId = ItemCatalog.Cloth;
            node.DisplayName = "들판 아마";
            node.GatherSkill = SkillId.Tailoring;
            if (node.Remaining <= 0)
                node.Remaining = 10;
            node.Capacity = 10;
            node.Difficulty = 10f;
            node.RespawnSeconds = 8f;
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }


        static void EnsureFieldOre()
        {
            var go = GameObject.Find("FieldOre");
            if (go == null)
                return;
            var node = go.GetComponent<ResourceNode>() ?? go.AddComponent<ResourceNode>();
            node.ResourceId = "iron_ore";
            node.DisplayName = "들판 광맥";
            node.GatherSkill = SkillId.Mining;
            if (node.Remaining <= 0)
                node.Remaining = 10;
            node.Capacity = 10;
            node.Difficulty = 10f;
            node.RespawnSeconds = 8f;
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }

        static void EnsureFishSpot()
        {
            var go = GameObject.Find("FishingSpot");
            if (go == null)
                go = GameObject.Find("watermill");
            if (go == null)
                return;
            go.name = "FishingSpot";
            var node = go.GetComponent<ResourceNode>() ?? go.AddComponent<ResourceNode>();
            node.ResourceId = ItemCatalog.Fish;
            node.DisplayName = "물가";
            node.GatherSkill = SkillId.Fishing;
            if (node.Remaining <= 0)
                node.Remaining = 12;
            node.Capacity = 12;
            node.Difficulty = 10f;
            node.RespawnSeconds = 8f;
            node.InteractRange = 2.8f;
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }

        static void EnsureCampfire()
        {
            var go = GameObject.Find("Campfire");
            if (go == null)
            {
                var mill = GameObject.Find("FishingSpot");
                go = new GameObject("Campfire");
                if (mill != null)
                    go.transform.position = mill.transform.position + new Vector3(2.3f, 0f, 1.7f);
                else
                    go.transform.position = new Vector3(-9.2f, 0f, -6.8f);
                go.AddComponent<BoxCollider>();
            }
            go.name = "Campfire";
            var station = go.GetComponent<CraftStation>() ?? go.AddComponent<CraftStation>();
            station.RecipeId = "cooked_fish";
            station.DisplayName = "화덕";
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }


        static void EnsureMortar()
        {
            var go = GameObject.Find("Mortar");
            if (go == null)
            {
                var fire = GameObject.Find("Campfire");
                go = new GameObject("Mortar");
                if (fire != null)
                    go.transform.position = fire.transform.position + new Vector3(2.4f, 0f, 0.2f);
                else
                    go.transform.position = new Vector3(-6.8f, 0f, -6.6f);
                go.AddComponent<BoxCollider>();
            }
            go.name = "Mortar";
            var station = go.GetComponent<CraftStation>() ?? go.AddComponent<CraftStation>();
            station.RecipeId = "health_potion";
            station.DisplayName = "절구";
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }


        static void EnsureLockedCrate()
        {
            var go = GameObject.Find("LockedCrate");
            if (go == null)
                go = GameObject.Find("cart");
            if (go == null)
            {
                go = new GameObject("LockedCrate");
                go.transform.position = new Vector3(-7.4f, 0f, 6.4f);
                go.AddComponent<BoxCollider>();
            }
            go.name = "LockedCrate";
            var crate = go.GetComponent<LockedCrate>() ?? go.AddComponent<LockedCrate>();
            crate.DisplayName = "잠긴 상자";
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }

        static void EnsureVendor()
        {
            var go = GameObject.Find("Vendor");
            if (go == null)
                go = GameObject.Find("stall");
            if (go == null)
                return;
            go.name = "Vendor";
            if (go.GetComponent<VendorStation>() == null)
            {
                var v = go.AddComponent<VendorStation>();
                v.DisplayName = "잡화";
            }
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }


        static void EnsureCarpenter()
        {
            var go = GameObject.Find("Carpenter");
            if (go == null)
                go = GameObject.Find("stall-bench");
            if (go == null)
            {
                var forge = GameObject.Find("Forge");
                go = new GameObject("Carpenter");
                if (forge != null)
                    go.transform.position = forge.transform.position + new Vector3(-2.1f, 0f, 1.8f);
                go.AddComponent<BoxCollider>();
            }
            go.name = "Carpenter";
            var station = go.GetComponent<CraftStation>() ?? go.AddComponent<CraftStation>();
            station.RecipeId = "wooden_club";
            station.DisplayName = "목공소";
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();
        }

        static void EnsureTrainer()
        {
            var go = GameObject.Find("Trainer");
            if (go == null)
                return;
            if (go.GetComponent<TrainerStation>() == null)
            {
                var t = go.AddComponent<TrainerStation>();
                t.DisplayName = "훈련사";
            }
        }

        public static HealerStation FindHealer(string name)
        {
            var list = Object.FindObjectsByType<HealerStation>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
                if (list[i].gameObject.name == name)
                    return list[i];
            return null;
        }

        public static CorpseNode FindCorpse(string ownerId)
        {
            var list = Object.FindObjectsByType<CorpseNode>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
                if (list[i].OwnerId == ownerId)
                    return list[i];
            return null;
        }

        public AttackResult TryGather(WorldBody body, ResourceNode node)
        {
            if (body == null || node == null)
                return new AttackResult { FailReason = "no_node" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            node.Tick(Time.time);
            if (node.Remaining <= 0)
                return new AttackResult { FailReason = "depleted" };
            float dist = Vector3.Distance(body.transform.position, node.transform.position);
            if (dist > node.InteractRange)
                return new AttackResult { FailReason = "range" };
            var bag = Bag(body);
            if (!bag.CanCarry(StatsOf(body).Str, node.ResourceId, 1))
            {
                LastWeightMessage = WeightRefuseMessage(StatsOf(body).Str, bag);
                return new AttackResult { FailReason = "overweight" };
            }
            string tool = ItemCatalog.ToolFor(node.GatherSkill);
            if (!string.IsNullOrEmpty(tool) && !bag.WearTool(tool))
                return new AttackResult { FailReason = "no_tool" };

            var skills = SkillsOf(body);
            var req = new AttackRequest
            {
                Distance = 0f,
                Range = 99f,
                Now = Time.time,
                NextAttackAt = 0f,
                TargetAlive = true,
                Skills = skills,
                Difficulty = node.Difficulty,
                Damage = 0
            };
            SkillGain.TryRaise(skills, node.GatherSkill, node.Difficulty, out float before, out float after, StatsOf(body));
            if (body.IsAvatar)
                body.RecalcFromStr(StatsOf(body).Str);
            node.Remaining -= 1;
            node.AfterTake(Time.time);
            bag.Add(node.ResourceId, 1);
            return new AttackResult { Applied = true, Hit = true, SkillBefore = before, SkillAfter = after };
        }

        public AttackResult TryTrade(WorldBody from, WorldBody to)
        {
            if (from == null || to == null || from == to)
                return new AttackResult { FailReason = "no_target" };
            float dist = Vector3.Distance(from.transform.position, to.transform.position);
            if (dist > 2.8f)
                return new AttackResult { FailReason = "range" };
            ActiveTrade = new TradeSession { A = from, B = to };
            return new AttackResult { Applied = true };
        }

        public void SetTradeOffer(WorldBody me, string template)
        {
            ActiveTrade?.SetOffer(me, template);
        }

        public AttackResult ConfirmTrade(WorldBody me)
        {
            if (ActiveTrade == null)
                return new AttackResult { FailReason = "no_trade" };
            if (!ActiveTrade.SetAccept(me, true))
                return new AttackResult { FailReason = "waiting" };
            string offerA = ActiveTrade.OfferA;
            string offerB = ActiveTrade.OfferB;
            var a = ActiveTrade.A;
            var b = ActiveTrade.B;
            if (!string.IsNullOrEmpty(offerA) && CountItem(Bag(a), offerA) < 1)
                return CancelTrade("missing");
            if (!string.IsNullOrEmpty(offerB) && CountItem(Bag(b), offerB) < 1)
                return CancelTrade("missing");
            if (!string.IsNullOrEmpty(offerA))
            {
                ConsumeItem(Bag(a), offerA, 1);
                Bag(b).Add(offerA, 1);
            }
            if (!string.IsNullOrEmpty(offerB))
            {
                ConsumeItem(Bag(b), offerB, 1);
                Bag(a).Add(offerB, 1);
            }
            ActiveTrade = null;
            OpLog.Write("trade", PersistDriver.AccountKey(), "trade", (offerA ?? "") + "<->" + (offerB ?? ""));
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult CancelTrade(string reason = "cancel")
        {
            ActiveTrade = null;
            return new AttackResult { FailReason = reason };
        }

        static InventoryBag Bag(WorldBody body)
        {
            return body.GetComponent<InventoryBag>() ?? body.gameObject.AddComponent<InventoryBag>();
        }

        static BankVault Vault(WorldBody body)
        {
            return body.GetComponent<BankVault>() ?? body.gameObject.AddComponent<BankVault>();
        }

        public AttackResult TryBank(WorldBody body, BankStation station)
        {
            if (body == null || station == null)
                return new AttackResult { FailReason = "no_bank" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            float dist = Vector3.Distance(body.transform.position, station.transform.position);
            if (dist > station.InteractRange)
                return new AttackResult { FailReason = "range" };
            if (Bag(body).Items.Count > 0)
                return DepositAll(body);
            return WithdrawAll(body);
        }

        public AttackResult TrySpeechKeyword(WorldBody body, string text)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            if (string.IsNullOrWhiteSpace(text))
            {
                LastSpeechMessage = "";
                return new AttackResult { FailReason = "empty" };
            }
            string raw = text.Trim();
            string key = raw.ToLowerInvariant();
            if (key == "bank" || raw == "은행")
                return SpeechBank(body);
            if (key == "guards" || raw == "경비")
                return SpeechGuards(body);
            if (key == "vendor" || raw == "상점")
                return SpeechVendor(body);
            LastSpeechMessage = "";
            return new AttackResult { FailReason = "no_match" };
        }

        AttackResult SpeechBank(WorldBody body)
        {
            var station = FindBank("Banker");
            if (station == null)
            {
                var banks = Object.FindObjectsByType<BankStation>(FindObjectsSortMode.None);
                float best = float.MaxValue;
                for (int i = 0; i < banks.Length; i++)
                {
                    float d = Vector3.Distance(body.transform.position, banks[i].transform.position);
                    if (d >= best)
                        continue;
                    best = d;
                    station = banks[i];
                }
            }
            if (station == null)
            {
                LastSpeechMessage = "은행 없음";
                return new AttackResult { FailReason = "no_bank" };
            }
            float dist = Vector3.Distance(body.transform.position, station.transform.position);
            if (dist > station.InteractRange)
                WarpBody(body, station.transform.position.x, station.transform.position.z);
            var result = TryBank(body, station);
            LastSpeechMessage = "은행";
            if (result.Applied)
                return result;
            if (result.FailReason == "empty_bag" || result.FailReason == "empty_bank")
                return new AttackResult { Applied = true, Hit = true };
            return result;
        }

        AttackResult SpeechGuards(WorldBody body)
        {
            bool inZone = GuardZone.Contains(body.transform.position.x, body.transform.position.z);
            if (body.Notoriety == NotorietyId.Criminal || body.Notoriety == NotorietyId.Murderer)
            {
                if (inZone)
                {
                    GuardStrike(body);
                    LastSpeechMessage = "경비";
                    return new AttackResult { Applied = true, Hit = true };
                }
                LastSpeechMessage = "경비는 마을에만 있다.";
                return new AttackResult { Applied = true };
            }
            LastSpeechMessage = "경비가 순찰 중이다.";
            return new AttackResult { Applied = true };
        }

        AttackResult SpeechVendor(WorldBody body)
        {
            var vendors = Object.FindObjectsByType<VendorStation>(FindObjectsSortMode.None);
            VendorStation nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < vendors.Length; i++)
            {
                float d = Vector3.Distance(body.transform.position, vendors[i].transform.position);
                if (d >= best)
                    continue;
                best = d;
                nearest = vendors[i];
            }
            if (nearest == null)
            {
                LastSpeechMessage = "상점 없음";
                return new AttackResult { FailReason = "no_vendor" };
            }
            if (best > nearest.InteractRange)
                WarpBody(body, nearest.transform.position.x, nearest.transform.position.z);
            var result = TryVendor(body, nearest);
            if (result.Applied)
            {
                LastSpeechMessage = "상점";
                return result;
            }
            LastSpeechMessage = "상점: " + nearest.DisplayName + " 쪽으로";
            return new AttackResult { Applied = true };
        }


        static string AccountOf(WorldBody body)
        {
            if (body != null && !string.IsNullOrEmpty(body.AccountId))
                return body.AccountId;
            if (body != null && !string.IsNullOrEmpty(body.CharacterId))
                return body.CharacterId;
            return PersistDriver.AccountKey();
        }

        static string CharacterOf(WorldBody body)
        {
            if (body != null && !string.IsNullOrEmpty(body.CharacterId))
                return body.CharacterId;
            return AccountOf(body);
        }

        HouseRecord RecordOf(string plotId)
        {
            if (string.IsNullOrEmpty(plotId))
                plotId = HousingPlot.Id;
            if (!houses.TryGetValue(plotId, out HouseRecord rec) || rec == null)
            {
                rec = new HouseRecord { PlotId = plotId };
                houses[plotId] = rec;
            }
            return rec;
        }

        bool ActorOwnsAnyHouse(string characterId, string accountId)
        {
            foreach (var kv in houses)
            {
                var rec = kv.Value;
                if (rec == null || string.IsNullOrEmpty(rec.OwnerCharacterId))
                    continue;
                if (!string.IsNullOrEmpty(characterId) && rec.OwnerCharacterId == characterId)
                    return true;
                if (!string.IsNullOrEmpty(accountId) && rec.OwnerAccountId == accountId)
                    return true;
            }
            return false;
        }

        HouseRequest MakeHouseRequest(WorldBody body, Vector3 at, float range, HouseRecord rec, bool hasItem, bool chestHas)
        {
            string cid = CharacterOf(body);
            string aid = AccountOf(body);
            return new HouseRequest
            {
                PlotExists = true,
                Occupied = rec != null && !string.IsNullOrEmpty(rec.OwnerCharacterId),
                ActorOwnsHouse = ActorOwnsAnyHouse(cid, aid),
                Distance = body != null ? Vector3.Distance(body.transform.position, at) : 99f,
                Range = range,
                Ghost = body != null && body.Ghost,
                Gold = body != null ? body.Gold : 0,
                ActorCharacterId = cid,
                ActorAccountId = aid,
                OwnerCharacterId = rec != null ? rec.OwnerCharacterId : "",
                OwnerAccountId = rec != null ? rec.OwnerAccountId : "",
                HasBackpackItem = hasItem,
                ChestHasItem = chestHas
            };
        }

        void ShowHouse(string plotId, bool claimed)
        {
            var plot = GameObject.Find(HousingPlot.RootObject);
            if (plot == null)
                return;
            Transform house = plot.transform.Find(HousingPlot.HouseObject);
            if (house != null)
                house.gameObject.SetActive(claimed);
        }

        public AttackResult TryClaimHouse(WorldBody body, HousePlotStation station)
        {
            if (body == null || station == null)
                return new AttackResult { FailReason = "no_plot" };
            string plotId = string.IsNullOrEmpty(station.PlotId) ? HousingPlot.Id : station.PlotId;
            var rec = RecordOf(plotId);
            var req = MakeHouseRequest(body, station.transform.position, station.InteractRange, rec, false, false);
            var result = HouseResolve.Claim(req);
            if (!result.Applied)
                return result;
            rec.OwnerCharacterId = CharacterOf(body);
            rec.OwnerAccountId = AccountOf(body);
            rec.PublicFlag = 0;
            body.Gold -= HousingPlot.ClaimGold;
            ShowHouse(plotId, true);
            PersistPlot(plotId);
            return result;
        }

        public AttackResult TryLockdown(WorldBody body, HouseChest chest, string templateId = null)
        {
            if (body == null || chest == null)
                return new AttackResult { FailReason = "no_plot" };
            string plotId = string.IsNullOrEmpty(chest.PlotId) ? HousingPlot.Id : chest.PlotId;
            var rec = RecordOf(plotId);
            var bag = Bag(body);
            if (string.IsNullOrEmpty(templateId))
                templateId = ItemCatalog.Cloth;
            bool has = CountItem(bag, templateId) > 0;
            var req = MakeHouseRequest(body, chest.transform.position, chest.InteractRange, rec, has, rec.Items.Count > 0);
            var result = HouseResolve.Lockdown(req);
            if (!result.Applied)
                return result;
            ItemRecord moved = default;
            moved.TemplateId = templateId;
            moved.Amount = 1;
            for (int i = 0; i < bag.Items.Count; i++)
            {
                if (bag.Items[i].TemplateId != templateId)
                    continue;
                moved = bag.Items[i];
                moved.Amount = 1;
                break;
            }
            ConsumeItem(bag, templateId, 1);
            moved.Slot = rec.Items.Count;
            rec.Items.Add(moved);
            PersistPlot(plotId);
            return result;
        }

        public AttackResult TrySecureTake(WorldBody body, HouseChest chest)
        {
            if (body == null || chest == null)
                return new AttackResult { FailReason = "no_plot" };
            string plotId = string.IsNullOrEmpty(chest.PlotId) ? HousingPlot.Id : chest.PlotId;
            var rec = RecordOf(plotId);
            var req = MakeHouseRequest(body, chest.transform.position, chest.InteractRange, rec, false, rec.Items.Count > 0);
            var result = HouseResolve.SecureTake(req);
            if (!result.Applied)
                return result;
            ItemRecord it = rec.Items[rec.Items.Count - 1];
            rec.Items.RemoveAt(rec.Items.Count - 1);
            Bag(body).Add(it);
            PersistPlot(plotId);
            return result;
        }

        public bool OwnsPlot(WorldBody body, string plotId)
        {
            var rec = RecordOf(plotId);
            return rec != null && HouseResolve.IsOwner(new HouseRequest
            {
                ActorCharacterId = CharacterOf(body),
                OwnerCharacterId = rec.OwnerCharacterId
            });
        }

        bool VendorListed(HouseRecord rec)
        {
            return rec != null && !string.IsNullOrEmpty(rec.VendorItem.TemplateId) && rec.VendorItem.Amount > 0;
        }

        public AttackResult TryListVendor(WorldBody body, HouseVendor vendor, string templateId = null)
        {
            if (body == null || vendor == null)
                return new AttackResult { FailReason = "no_plot" };
            string plotId = string.IsNullOrEmpty(vendor.PlotId) ? HousingPlot.Id : vendor.PlotId;
            var rec = RecordOf(plotId);
            var bag = Bag(body);
            if (string.IsNullOrEmpty(templateId))
                templateId = ItemCatalog.Cloth;
            bool has = CountItem(bag, templateId) > 0;
            int price = ItemCatalog.BuyPrice(templateId);
            var req = MakeHouseRequest(body, vendor.transform.position, vendor.InteractRange, rec, has, rec.Items.Count > 0);
            req.PublicHouse = rec.PublicFlag != 0;
            req.VendorSlotFull = VendorListed(rec);
            req.VendorHasItem = VendorListed(rec);
            req.VendorPrice = price;
            var result = HouseResolve.ListVendor(req);
            if (!result.Applied)
                return result;
            ItemRecord moved = default;
            moved.TemplateId = templateId;
            moved.Amount = 1;
            for (int i = 0; i < bag.Items.Count; i++)
            {
                if (bag.Items[i].TemplateId != templateId)
                    continue;
                moved = bag.Items[i];
                moved.Amount = 1;
                break;
            }
            ConsumeItem(bag, templateId, 1);
            moved.Slot = HousingPlot.VendorSlot;
            rec.VendorItem = moved;
            rec.PublicFlag = 1;
            PersistPlot(plotId);
            return result;
        }

        public AttackResult TryBuyHouseVendor(WorldBody body, HouseVendor vendor)
        {
            if (body == null || vendor == null)
                return new AttackResult { FailReason = "no_plot" };
            string plotId = string.IsNullOrEmpty(vendor.PlotId) ? HousingPlot.Id : vendor.PlotId;
            var rec = RecordOf(plotId);
            int price = VendorListed(rec) ? ItemCatalog.BuyPrice(rec.VendorItem.TemplateId) : 0;
            var req = MakeHouseRequest(body, vendor.transform.position, vendor.InteractRange, rec, false, rec.Items.Count > 0);
            req.PublicHouse = rec.PublicFlag != 0;
            req.VendorSlotFull = VendorListed(rec);
            req.VendorHasItem = VendorListed(rec);
            req.VendorPrice = price;
            var result = HouseResolve.BuyVendor(req);
            if (!result.Applied)
                return result;
            ItemRecord sold = rec.VendorItem;
            rec.VendorItem = default;
            body.Gold -= price;
            var owner = FindOwnerBody(rec.OwnerCharacterId);
            if (owner != null)
                owner.Gold += price;
            Bag(body).Add(sold);
            PersistPlot(plotId);
            return result;
        }

        WorldBody FindOwnerBody(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return null;
            var list = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] != null && list[i].CharacterId == characterId)
                    return list[i];
            }
            return null;
        }

        public void ResetHousePlot(string plotId = null)
        {
            if (string.IsNullOrEmpty(plotId))
                plotId = HousingPlot.Id;
            var rec = RecordOf(plotId);
            rec.OwnerCharacterId = "";
            rec.OwnerAccountId = "";
            rec.PublicFlag = 0;
            rec.Items.Clear();
            rec.VendorItem = default;
            ShowHouse(plotId, false);
            PersistPlot(plotId);
        }

        public void LoadPlot(string plotId)
        {
            var snap = CharacterStore.LoadHouse(plotId);
            if (snap == null)
                return;
            var rec = RecordOf(string.IsNullOrEmpty(snap.PlotId) ? plotId : snap.PlotId);
            rec.OwnerCharacterId = snap.OwnerCharacterId ?? "";
            rec.OwnerAccountId = snap.AccountId ?? "";
            rec.PublicFlag = snap.PublicFlag;
            rec.Items.Clear();
            rec.VendorItem = default;
            if (snap.Items != null)
            {
                for (int i = 0; i < snap.Items.Length; i++)
                {
                    var it = snap.Items[i];
                    if (string.IsNullOrEmpty(it.TemplateId) || it.Amount <= 0)
                        continue;
                    if (it.Slot >= HousingPlot.VendorSlot)
                        rec.VendorItem = it;
                    else
                        rec.Items.Add(it);
                }
            }
            ShowHouse(rec.PlotId, !string.IsNullOrEmpty(rec.OwnerCharacterId));
        }

        void PersistPlot(string plotId)
        {
            if (!Application.isPlaying)
                return;
            var rec = RecordOf(plotId);
            int extra = VendorListed(rec) ? 1 : 0;
            var items = new ItemRecord[rec.Items.Count + extra];
            for (int i = 0; i < rec.Items.Count; i++)
            {
                var it = rec.Items[i];
                it.Slot = i;
                items[i] = it;
            }
            if (extra > 0)
            {
                var v = rec.VendorItem;
                v.Slot = HousingPlot.VendorSlot;
                items[rec.Items.Count] = v;
            }
            CharacterStore.SaveHouse(new HouseSnapshot
            {
                PlotId = rec.PlotId,
                OwnerCharacterId = rec.OwnerCharacterId,
                AccountId = rec.OwnerAccountId,
                PublicFlag = rec.PublicFlag,
                Items = items
            });
        }

        public AttackResult TryMoveToPouch(WorldBody body, string templateId, string pouchInstanceId)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            var bag = Bag(body);
            if (string.IsNullOrEmpty(pouchInstanceId))
                pouchInstanceId = bag.PouchInstanceId();
            if (string.IsNullOrEmpty(pouchInstanceId))
                return new AttackResult { FailReason = "no_pouch" };
            if (ItemCatalog.IsContainer(templateId))
                return new AttackResult { FailReason = "nested_depth" };
            if (!bag.TryMoveToPouch(templateId, pouchInstanceId))
                return new AttackResult { FailReason = "no_item" };
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult TryTakeFromPouch(WorldBody body, string templateId, string pouchInstanceId)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            var bag = Bag(body);
            if (string.IsNullOrEmpty(pouchInstanceId))
                pouchInstanceId = bag.PouchInstanceId();
            if (string.IsNullOrEmpty(pouchInstanceId))
                return new AttackResult { FailReason = "no_pouch" };
            if (!bag.TryTakeFromPouch(templateId, pouchInstanceId))
                return new AttackResult { FailReason = "no_item" };
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult DepositAll(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            var bag = Bag(body);
            if (bag.Items.Count == 0)
                return new AttackResult { FailReason = "empty_bag" };
            var vault = Vault(body);
            for (int i = 0; i < bag.Items.Count; i++)
                vault.Add(bag.Items[i]);
            bag.Items.Clear();
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult WithdrawAll(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            var vault = Vault(body);
            if (vault.Items.Count == 0)
                return new AttackResult { FailReason = "empty_bank" };
            var bag = Bag(body);
            for (int i = 0; i < vault.Items.Count; i++)
                bag.Add(vault.Items[i]);
            vault.Items.Clear();
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult TryCraft(WorldBody body, CraftStation station)
        {
            return TryCraft(body, station, null);
        }

        public AttackResult TryCraft(WorldBody body, CraftStation station, string recipeId)
        {
            if (body == null || station == null)
                return new AttackResult { FailReason = "no_station" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            float dist = Vector3.Distance(body.transform.position, station.transform.position);
            if (dist > station.InteractRange)
                return new AttackResult { FailReason = "range" };
            var home = CraftRecipes.Find(station.RecipeId);
            var recipe = CraftRecipes.Find(string.IsNullOrEmpty(recipeId) ? station.RecipeId : recipeId);
            if (recipe == null || home == null)
                return new AttackResult { FailReason = "unknown_recipe" };
            if (recipe.Skill != home.Skill)
                return new AttackResult { FailReason = "wrong_station" };
            var bag = body.GetComponent<InventoryBag>() ?? body.gameObject.AddComponent<InventoryBag>();
            int have = CountItem(bag, recipe.Ingredient);
            if (have < recipe.Count)
            {
                if (!recipe.CanRepair || have < 1 || !bag.RepairOne(10))
                    return new AttackResult { FailReason = "missing_" + recipe.Ingredient };
                ConsumeItem(bag, recipe.Ingredient, 1);
                return new AttackResult { Applied = true, Hit = true };
            }
            float projected = bag.TotalWeight()
                - ItemCatalog.WeightOf(recipe.Ingredient) * recipe.Count
                + ItemCatalog.WeightOf(recipe.Output);
            if (projected > ItemCatalog.CarryCap(StatsOf(body).Str))
            {
                LastWeightMessage = WeightRefuseMessage(StatsOf(body).Str, bag);
                return new AttackResult { FailReason = "overweight" };
            }
            ConsumeItem(bag, recipe.Ingredient, recipe.Count);
            var skills = SkillsOf(body);
            int uses = ItemCatalog.MaxUsesOf(recipe.Output);
            string maker = uses > 0 ? MakerOf(body) : "";
            bool exceptional = uses > 0 && ExceptionalCraft.Roll(skills.Get(recipe.Skill));
            if (exceptional)
                uses += ExceptionalCraft.UsesBonus;
            var made = new ItemRecord
            {
                TemplateId = recipe.Output,
                Amount = 1,
                Uses = uses,
                MakerId = maker,
                Exceptional = exceptional
            };
            bag.Add(made);
            SkillGain.TryRaise(skills, recipe.Skill, recipe.Difficulty, out float before, out float after, StatsOf(body));
            if (body.IsAvatar)
                body.RecalcFromStr(StatsOf(body).Str);
            OpLog.Write("craft", PersistDriver.AccountKey(), station.gameObject.name, recipe.Output);
            return new AttackResult { Applied = true, Hit = true, SkillBefore = before, SkillAfter = after };
        }

        static string MakerOf(WorldBody body)
        {
            if (body != null && !string.IsNullOrEmpty(body.CharacterId))
                return body.CharacterId;
            return PersistDriver.AccountKey();
        }

        public AttackResult TryAcceptOrder(WorldBody body, CraftStation station = null)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (station == null)
                station = FindStation("Forge");
            float dist = 999f;
            bool has = station != null;
            if (has)
                dist = Vector3.Distance(body.transform.position, station.transform.position);
            if (!has || dist > CraftOrderRules.InteractRange)
            {
                var vendor = FindVendor("Vendor");
                if (vendor != null)
                {
                    float vd = Vector3.Distance(body.transform.position, vendor.transform.position);
                    if (vd <= vendor.InteractRange)
                    {
                        has = true;
                        dist = vd;
                    }
                }
            }
            var gate = CraftOrderResolve.Accept(new CraftOrderRequest
            {
                Ghost = body.Ghost,
                HasStation = has,
                Distance = dist,
                Range = CraftOrderRules.InteractRange,
                ActiveOrder = body.ActiveCraftOrder ?? "",
                OfferItem = CraftOrderRules.DefaultItem
            });
            if (!gate.Applied)
            {
                LastCraftOrderMessage = CraftOrderMessage(gate.FailReason, body.ActiveCraftOrder);
                return gate;
            }
            body.ActiveCraftOrder = CraftOrderRules.DefaultItem;
            LastCraftOrderMessage = "제작의뢰 수락: " + CraftOrderRules.DefaultItem + " x" + CraftOrderRules.Amount;
            OpLog.Write("craft_order_accept", PersistDriver.AccountKey(), body.DisplayName, body.ActiveCraftOrder);
            return gate;
        }

        public AttackResult TryTurnInOrder(WorldBody body, CraftStation station = null)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (station == null)
                station = FindStation("Forge");
            float dist = 999f;
            bool has = station != null;
            if (has)
                dist = Vector3.Distance(body.transform.position, station.transform.position);
            if (!has || dist > CraftOrderRules.InteractRange)
            {
                var vendor = FindVendor("Vendor");
                if (vendor != null)
                {
                    float vd = Vector3.Distance(body.transform.position, vendor.transform.position);
                    if (vd <= vendor.InteractRange)
                    {
                        has = true;
                        dist = vd;
                    }
                }
            }
            string order = body.ActiveCraftOrder ?? "";
            var bag = body.GetComponent<InventoryBag>();
            string maker = MakerOf(body);
            bool match = HasCraftedByMaker(bag, order, maker);
            var gate = CraftOrderResolve.TurnIn(new CraftOrderRequest
            {
                Ghost = body.Ghost,
                HasStation = has,
                Distance = dist,
                Range = CraftOrderRules.InteractRange,
                ActiveOrder = order,
                HasMatchingCrafted = match
            });
            if (!gate.Applied)
            {
                LastCraftOrderMessage = CraftOrderMessage(gate.FailReason, order);
                return gate;
            }
            if (!TakeCraftedByMaker(bag, order, maker))
            {
                LastCraftOrderMessage = CraftOrderMessage("wrong_item", order);
                return new AttackResult { FailReason = "wrong_item" };
            }
            body.Gold += CraftOrderRules.GoldReward;
            body.ActiveCraftOrder = "";
            var skills = SkillsOf(body);
            SkillGain.TryRaise(skills, SkillId.Blacksmithing, CraftOrderRules.SkillDifficulty, out float before, out float after, StatsOf(body));
            LastCraftOrderMessage = "제작의뢰 납품 +" + CraftOrderRules.GoldReward + "G";
            OpLog.Write("craft_order_turnin", PersistDriver.AccountKey(), body.DisplayName, order);
            return new AttackResult { Applied = true, Hit = true, Damage = CraftOrderRules.GoldReward, SkillBefore = before, SkillAfter = after };
        }

        static string CraftOrderMessage(string reason, string order)
        {
            if (reason == "ghost") return "유령은 제작의뢰를 할 수 없습니다.";
            if (reason == "no_station") return "대장간/상점이 필요합니다.";
            if (reason == "range") return "대장간/상점에 더 가까이 가십시오.";
            if (reason == "already") return "이미 진행 중인 제작의뢰가 있습니다.";
            if (reason == "no_order") return "수락한 제작의뢰가 없습니다.";
            if (reason == "wrong_item") return "의뢰 품목(직접 제작)이 가방에 없습니다: " + order;
            return reason ?? "";
        }

        static bool HasCraftedByMaker(InventoryBag bag, string templateId, string makerId)
        {
            if (bag == null || string.IsNullOrEmpty(templateId))
                return false;
            for (int i = 0; i < bag.Items.Count; i++)
            {
                var it = bag.Items[i];
                if (it.TemplateId != templateId || it.Amount <= 0)
                    continue;
                if (it.MakerId == makerId)
                    return true;
            }
            return false;
        }

        static bool TakeCraftedByMaker(InventoryBag bag, string templateId, string makerId)
        {
            if (bag == null || string.IsNullOrEmpty(templateId))
                return false;
            for (int i = bag.Items.Count - 1; i >= 0; i--)
            {
                var it = bag.Items[i];
                if (it.TemplateId != templateId || it.Amount <= 0)
                    continue;
                if (it.MakerId != makerId)
                    continue;
                if (ItemCatalog.Stackable(templateId))
                {
                    it.Amount -= 1;
                    if (it.Amount <= 0)
                        bag.Items.RemoveAt(i);
                    else
                        bag.Items[i] = it;
                }
                else
                    bag.Items.RemoveAt(i);
                return true;
            }
            return false;
        }

        static int CountItem(InventoryBag bag, string template)
        {
            int n = 0;
            for (int i = 0; i < bag.Items.Count; i++)
                if (bag.Items[i].TemplateId == template)
                    n += bag.Items[i].Amount;
            return n;
        }

        static void ConsumeItem(InventoryBag bag, string template, int amount)
        {
            int left = amount;
            for (int i = bag.Items.Count - 1; i >= 0 && left > 0; i--)
            {
                var it = bag.Items[i];
                if (it.TemplateId != template)
                    continue;
                int take = it.Amount < left ? it.Amount : left;
                it.Amount -= take;
                left -= take;
                if (it.Amount <= 0)
                    bag.Items.RemoveAt(i);
                else
                    bag.Items[i] = it;
            }
        }

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

        public AttackResult HandleDeath(WorldBody body, string ownerId)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            body.Ghost = true;
            if (body.Hp > 0f)
                body.SetHp(0f);
            var bag = Bag(body);
            var existing = FindCorpse(ownerId);
            if (existing != null)
                KillGo(existing.gameObject);
            var go = SpawnCorpseGo(body);
            var node = go.AddComponent<CorpseNode>();
            node.CorpseId = System.Guid.NewGuid().ToString("N");
            node.OwnerId = ownerId ?? "";
            node.LastKind = string.IsNullOrEmpty(body.DisplayName) ? "시체" : body.DisplayName;
            node.LastX = body.transform.position.x;
            node.LastY = body.transform.position.y;
            node.LastZ = body.transform.position.z;
            node.SpawnedAt = Time.time;
            node.DecaySeconds = 900f;
            for (int i = 0; i < bag.Items.Count; i++)
                node.Items.Add(bag.Items[i]);
            bag.Items.Clear();
            OpLog.Write("drop", ownerId ?? "", node.CorpseId, "corpse");
            return new AttackResult { Applied = true, Hit = true };
        }

        static GameObject SpawnCorpseGo(WorldBody body)
        {
            var go = new GameObject("Corpse");
            Vector3 pos = body != null ? body.transform.position : Vector3.zero;
            go.transform.position = pos;
            Transform src = body != null ? body.transform.Find("Visual") : null;
            if (src != null)
            {
                var vis = Object.Instantiate(src.gameObject, go.transform, false);
                vis.name = "Visual";
                vis.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                float yaw = body.transform.eulerAngles.y;
                vis.transform.localRotation = Quaternion.Euler(90f, yaw, 0f);
                var anim = vis.GetComponent<Animator>();
                if (anim != null)
                    anim.enabled = false;
            }
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.9f, 0.4f, 1.6f);
            box.center = new Vector3(0f, 0.2f, 0f);
            return go;
        }

        public AttackResult TryResurrect(WorldBody body, HealerStation healer)
        {
            if (body == null || healer == null)
                return new AttackResult { FailReason = "no_healer" };
            if (!body.Ghost)
                return new AttackResult { FailReason = "not_ghost" };
            float dist = Vector3.Distance(body.transform.position, healer.transform.position);
            if (dist > healer.InteractRange)
                return new AttackResult { FailReason = "range" };
            body.Resurrect();
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult TryLootCorpse(WorldBody body, CorpseNode node)
        {
            if (body == null || node == null)
                return new AttackResult { FailReason = "no_corpse" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            if (!LootAllowed(body, node))
                return new AttackResult { FailReason = "loot_right" };
            float dist = Vector3.Distance(body.transform.position, node.transform.position);
            if (dist > node.InteractRange)
                return new AttackResult { FailReason = "range" };
            var bag = Bag(body);
            for (int i = 0; i < node.Items.Count; i++)
                bag.Add(node.Items[i]);
            node.Items.Clear();
            KillGo(node.gameObject);
            OpLog.Write("drop", node.OwnerId, node.CorpseId, "loot");
            return new AttackResult { Applied = true, Hit = true };
        }

        public void RestoreCorpse(string ownerId, CharacterSnapshot snap)
        {
            if (snap == null || snap.Corpse == null || snap.Corpse.Length == 0)
                return;
            if (FindCorpse(ownerId) != null)
                return;
            var go = SpawnCorpseGo(Player);
            go.transform.position = new Vector3(snap.CorpseX, snap.CorpseY, snap.CorpseZ);
            var node = go.GetComponent<CorpseNode>();
            if (node == null)
                node = go.AddComponent<CorpseNode>();
            node.CorpseId = string.IsNullOrEmpty(snap.CorpseId) ? System.Guid.NewGuid().ToString("N") : snap.CorpseId;
            node.OwnerId = ownerId ?? "";
            node.LastKind = string.IsNullOrEmpty(snap.Name) ? "시체" : snap.Name;
            node.LastX = snap.CorpseX;
            node.LastY = snap.CorpseY;
            node.LastZ = snap.CorpseZ;
            node.SpawnedAt = Time.time;
            node.DecaySeconds = 900f;
            for (int i = 0; i < snap.Corpse.Length; i++)
                node.Items.Add(snap.Corpse[i]);
        }

        public static void WriteCorpse(CharacterSnapshot snap, string ownerId)
        {
            if (snap == null)
                return;
            var node = FindCorpse(ownerId);
            if (node == null)
            {
                snap.CorpseId = "";
                snap.Corpse = System.Array.Empty<ItemRecord>();
                return;
            }
            snap.CorpseId = node.CorpseId;
            snap.CorpseX = node.transform.position.x;
            snap.CorpseY = node.transform.position.y;
            snap.CorpseZ = node.transform.position.z;
            snap.Corpse = node.Items.ToArray();
        }


        public static LockedCrate FindCrate(string name)
        {
            var list = Object.FindObjectsByType<LockedCrate>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
                if (list[i].gameObject.name == name)
                    return list[i];
            return list.Length > 0 ? list[0] : null;
        }

        public static VendorStation FindVendor(string name)
        {
            var list = Object.FindObjectsByType<VendorStation>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
                if (list[i].gameObject.name == name)
                    return list[i];
            return null;
        }

        public AttackResult TryVendor(WorldBody body, VendorStation vendor)
        {
            if (body == null || vendor == null)
                return new AttackResult { FailReason = "no_vendor" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            float dist = Vector3.Distance(body.transform.position, vendor.transform.position);
            if (dist > vendor.InteractRange)
                return new AttackResult { FailReason = "range" };
            ActiveVendor = vendor;
            return new AttackResult { Applied = true };
        }

        public string EquippedOf(WorldBody body)
        {
            if (body == null)
                return "";
            return equipped.TryGetValue(body.GetInstanceID(), out string id) ? id : "";
        }

        public AttackResult TryEquip(WorldBody body, string templateId)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            var bag = Bag(body);
            bool has = bag != null && ItemCatalog.Has(bag.Items, templateId);
            int req = ItemCatalog.StrReqOf(templateId);
            var result = EquipResolve.Equip(new EquipRequest
            {
                Ghost = body.Ghost,
                HasItem = has,
                Str = StatsOf(body).Str,
                StrReq = req,
                TemplateId = templateId
            });
            if (!result.Applied)
            {
                LastEquipMessage = EquipResolve.MessageFor(result.FailReason, templateId, req);
                return result;
            }
            equipped[body.GetInstanceID()] = templateId;
            LastEquipMessage = EquipResolve.MessageFor("", templateId, req);
            OpLog.Write("equip", PersistDriver.AccountKey(), body.DisplayName, templateId);
            return result;
        }

        public AttackResult TryUnequip(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            string cur = EquippedOf(body);
            var result = EquipResolve.Unequip(new EquipRequest
            {
                Ghost = body.Ghost,
                TemplateId = cur
            });
            if (!result.Applied)
            {
                LastEquipMessage = EquipResolve.MessageFor(result.FailReason, cur, 0);
                return result;
            }
            equipped.Remove(body.GetInstanceID());
            LastEquipMessage = "해제: " + cur;
            OpLog.Write("unequip", PersistDriver.AccountKey(), body.DisplayName, cur);
            return result;
        }

        public AttackResult TryBuy(WorldBody body, string templateId)
        {
            if (body == null || ActiveVendor == null)
                return new AttackResult { FailReason = "no_vendor" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            int price = ItemCatalog.BuyPrice(templateId);
            if (price <= 0)
                return new AttackResult { FailReason = "not_sold" };
            if (body.Gold < price)
                return new AttackResult { FailReason = "gold" };
            var bag = Bag(body);
            if (!bag.CanCarry(StatsOf(body).Str, templateId, 1))
            {
                LastWeightMessage = WeightRefuseMessage(StatsOf(body).Str, bag);
                return new AttackResult { FailReason = "overweight" };
            }
            bag.Add(templateId, 1);
            body.Gold -= price;
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult TrySell(WorldBody body, string templateId)
        {
            if (body == null || ActiveVendor == null)
                return new AttackResult { FailReason = "no_vendor" };
            int price = ItemCatalog.SellPrice(templateId);
            if (price <= 0)
                return new AttackResult { FailReason = "not_bought" };
            var bag = Bag(body);
            if (!bag.TakeOne(templateId))
                return new AttackResult { FailReason = "missing" };
            body.Gold += price;
            return new AttackResult { Applied = true, Hit = true };
        }

        public static TrainerStation FindTrainer(string name)
        {
            var list = Object.FindObjectsByType<TrainerStation>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
                if (list[i].gameObject.name == name)
                    return list[i];
            return null;
        }

        public AttackResult TryTrainer(WorldBody body, TrainerStation trainer)
        {
            if (body == null || trainer == null)
                return new AttackResult { FailReason = "no_trainer" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            float dist = Vector3.Distance(body.transform.position, trainer.transform.position);
            if (dist > trainer.InteractRange)
                return new AttackResult { FailReason = "range" };
            ActiveTrainer = trainer;
            return new AttackResult { Applied = true };
        }

        public AttackResult TryTrain(WorldBody body, SkillId skill)
        {
            if (body == null || ActiveTrainer == null)
                return new AttackResult { FailReason = "no_trainer" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            var trainer = ActiveTrainer;
            var skills = SkillsOf(body);
            float cur = skills.Get(skill);
            if (cur >= trainer.Cap - 0.0001f)
                return new AttackResult { FailReason = "trained" };
            if (body.Gold < trainer.Cost)
                return new AttackResult { FailReason = "gold" };
            float next = cur + 1f;
            if (next > trainer.Cap)
                next = trainer.Cap;
            if (!skills.TrySet(skill, next))
                return new AttackResult { FailReason = "cap" };
            body.Gold -= trainer.Cost;
            return new AttackResult { Applied = true, Hit = true, SkillBefore = cur, SkillAfter = skills.Get(skill) };
        }

        public AttackResult TryPartyInvite(WorldBody from, WorldBody to)
        {
            if (from == null || to == null || from == to)
                return new AttackResult { FailReason = "no_target" };
            if (from.Ghost)
                return new AttackResult { FailReason = "ghost" };
            if (to.IsEnemy)
                return new AttackResult { FailReason = "enemy" };
            float dist = Vector3.Distance(from.transform.position, to.transform.position);
            if (dist > 4f)
                return new AttackResult { FailReason = "range" };
            if (ActiveParty != null && ActiveParty.Leader != from)
                return new AttackResult { FailReason = "not_leader" };
            if (ActiveParty != null && ActiveParty.Contains(to))
                return new AttackResult { FailReason = "already" };
            if (ActiveParty != null && ActiveParty.Members.Count >= 3)
                return new AttackResult { FailReason = "full" };
            if (ActiveParty == null)
                ActiveParty = new Party { Leader = from };
            if (!to.IsAvatar)
            {
                ActiveParty.Add(to);
                return new AttackResult { Applied = true };
            }
            ActiveParty.Pending = to;
            return new AttackResult { Applied = true };
        }

        public AttackResult TryPartyAccept(WorldBody body)
        {
            if (ActiveParty == null || body == null)
                return new AttackResult { FailReason = "no_party" };
            if (ActiveParty.Pending != body)
                return new AttackResult { FailReason = "no_invite" };
            float dist = Vector3.Distance(body.transform.position, ActiveParty.Leader.transform.position);
            if (dist > 6f)
                return new AttackResult { FailReason = "range" };
            ActiveParty.Add(body);
            return new AttackResult { Applied = true };
        }

        public AttackResult TryPartyLeave(WorldBody body)
        {
            if (ActiveParty == null || body == null || !ActiveParty.Contains(body))
                return new AttackResult { FailReason = "no_party" };
            if (body == ActiveParty.Leader)
            {
                ActiveParty = null;
                return new AttackResult { Applied = true };
            }
            ActiveParty.Members.Remove(body);
            return new AttackResult { Applied = true };
        }

        public AttackResult TryPartySay(WorldBody body, string text)
        {
            if (ActiveParty == null || body == null || !ActiveParty.Contains(body))
                return new AttackResult { FailReason = "no_party" };
            if (string.IsNullOrEmpty(text))
                return new AttackResult { FailReason = "empty" };
            ActiveParty.Say(body.DisplayName, text);
            return new AttackResult { Applied = true };
        }

        public AttackResult TryGuildCreate(WorldBody body, string name)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_target" };
            name = GuildResolve.NormalizeName(name);
            var check = GuildResolve.Create(new GuildRequest
            {
                Name = name,
                Ghost = body.Ghost,
                AlreadyInGuild = !string.IsNullOrEmpty(body.GuildId),
                Gold = body.Gold,
                GoldCost = GuildRules.GoldCost
            });
            if (!check.Applied)
            {
                LastGuildMessage = check.FailReason;
                return check;
            }
            guildSeq++;
            string id = "g" + guildSeq;
            var guild = new Guild { Id = id, Name = name, Leader = body };
            guilds[id] = guild;
            body.GuildId = id;
            body.GuildName = name;
            body.Gold -= GuildRules.GoldCost;
            LastGuildMessage = "created";
            return new AttackResult { Applied = true, Hit = true, Damage = GuildRules.GoldCost };
        }

        public AttackResult TryGuildInvite(WorldBody from, WorldBody to)
        {
            if (from == null || to == null || from == to)
                return new AttackResult { FailReason = "no_target" };
            var guild = GuildOf(from);
            float dist = Vector3.Distance(from.transform.position, to.transform.position);
            var check = GuildResolve.Invite(new GuildRequest
            {
                Ghost = from.Ghost,
                HasGuild = guild != null,
                IsLeader = guild != null && guild.Leader == from,
                HasTarget = true,
                SameAsSelf = false,
                TargetEnemy = to.IsEnemy,
                TargetInGuild = !string.IsNullOrEmpty(to.GuildId),
                Distance = dist,
                Range = GuildRules.InviteRange
            });
            if (!check.Applied)
            {
                LastGuildMessage = check.FailReason;
                return check;
            }
            if (!to.IsAvatar)
            {
                guild.Add(to);
                to.GuildId = guild.Id;
                to.GuildName = guild.Name;
                LastGuildMessage = "joined";
                return new AttackResult { Applied = true };
            }
            guild.Pending = to;
            LastGuildMessage = "invited";
            return new AttackResult { Applied = true };
        }

        public AttackResult TryGuildAccept(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_target" };
            Guild guild = null;
            foreach (var kv in guilds)
            {
                if (kv.Value != null && kv.Value.Pending == body)
                {
                    guild = kv.Value;
                    break;
                }
            }
            float dist = guild != null && guild.Leader != null
                ? Vector3.Distance(body.transform.position, guild.Leader.transform.position)
                : 999f;
            var check = GuildResolve.Accept(new GuildRequest
            {
                HasGuild = guild != null,
                HasPending = guild != null && guild.Pending != null,
                PendingIsMe = guild != null && guild.Pending == body,
                Ghost = body.Ghost,
                AlreadyInGuild = !string.IsNullOrEmpty(body.GuildId),
                Distance = dist,
                Range = GuildRules.AcceptRange
            });
            if (!check.Applied)
            {
                LastGuildMessage = check.FailReason;
                return check;
            }
            guild.Add(body);
            body.GuildId = guild.Id;
            body.GuildName = guild.Name;
            LastGuildMessage = "accepted";
            return new AttackResult { Applied = true };
        }

        public AttackResult TryGuildLeave(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_target" };
            var guild = GuildOf(body);
            var check = GuildResolve.Leave(new GuildRequest { HasGuild = guild != null });
            if (!check.Applied)
            {
                LastGuildMessage = check.FailReason;
                return check;
            }
            if (body == guild.Leader)
            {
                ClearGuildWar(guild);
                ClearGuildMembers(guild);
                guilds.Remove(guild.Id);
            }
            else
            {
                guild.Members.Remove(body);
                if (guild.Pending == body)
                    guild.Pending = null;
                body.GuildId = "";
                body.GuildName = "";
            }
            LastGuildMessage = "left";
            return new AttackResult { Applied = true };
        }


        public AttackResult TryDuelInvite(WorldBody from, WorldBody to)
        {
            if (from == null || to == null)
                return new AttackResult { FailReason = "no_target" };
            float dist = Vector3.Distance(from.transform.position, to.transform.position);
            var check = DuelResolve.Invite(new DuelRequest
            {
                Ghost = from.Ghost,
                HasTarget = true,
                SameAsSelf = from == to,
                TargetEnemy = to.IsEnemy,
                TargetAvatar = to.IsAvatar,
                AlreadyDueling = from.DuelOpponent != null,
                TargetBusy = to.DuelOpponent != null || (to.PendingDuel != null && to.PendingDuel != from),
                Distance = dist,
                Range = DuelRules.InviteRange
            });
            if (!check.Applied)
            {
                LastDuelMessage = check.FailReason;
                return check;
            }
            from.PendingDuel = to;
            LastDuelMessage = "invited";
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult TryDuelAccept(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_target" };
            WorldBody from = null;
            var all = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].PendingDuel == body)
                {
                    from = all[i];
                    break;
                }
            }
            float dist = from != null ? Vector3.Distance(body.transform.position, from.transform.position) : 999f;
            var check = DuelResolve.Accept(new DuelRequest
            {
                HasPending = from != null,
                PendingIsMe = from != null && from.PendingDuel == body,
                Ghost = body.Ghost,
                AlreadyDueling = body.DuelOpponent != null || (from != null && from.DuelOpponent != null),
                Distance = dist,
                Range = DuelRules.AcceptRange
            });
            if (!check.Applied)
            {
                LastDuelMessage = check.FailReason;
                return check;
            }
            from.PendingDuel = null;
            from.DuelOpponent = body;
            body.DuelOpponent = from;
            body.PendingDuel = null;
            LastDuelMessage = "accepted";
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult TryDuelEnd(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_target" };
            var check = DuelResolve.End(new DuelRequest { InDuel = body.DuelOpponent != null });
            if (!check.Applied)
            {
                LastDuelMessage = check.FailReason;
                return check;
            }
            ClearDuel(body);
            LastDuelMessage = "ended";
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult TryDuelYield(WorldBody body)
        {
            var r = TryDuelEnd(body);
            if (r.Applied)
                LastDuelMessage = "yielded";
            return r;
        }

        void ClearDuel(WorldBody body)
        {
            if (body == null)
                return;
            var other = body.DuelOpponent;
            body.DuelOpponent = null;
            body.PendingDuel = null;
            if (other != null)
            {
                if (other.DuelOpponent == body)
                    other.DuelOpponent = null;
                if (other.PendingDuel == body)
                    other.PendingDuel = null;
            }
        }


        public AttackResult TryGuildWarDeclare(WorldBody from, WorldBody to)
        {
            if (from == null || to == null || from == to)
                return new AttackResult { FailReason = "no_target" };
            var ga = GuildOf(from);
            var gb = GuildOf(to);
            bool already = ga != null && gb != null && (ga.WarWithId == gb.Id || gb.WarWithId == ga.Id
                || (!string.IsNullOrEmpty(ga.WarWithId) && ga.WarWithId != (gb != null ? gb.Id : ""))
                || (!string.IsNullOrEmpty(gb != null ? gb.WarWithId : "") && gb.WarWithId != (ga != null ? ga.Id : "")));
            var check = GuildWarResolve.Declare(new GuildWarRequest
            {
                Ghost = from.Ghost,
                HasGuild = ga != null,
                IsLeader = ga != null && ga.Leader == from,
                HasTargetGuild = gb != null,
                SameGuild = ga != null && gb != null && ga.Id == gb.Id,
                AlreadyWar = already
            });
            if (!check.Applied)
            {
                LastGuildMessage = check.FailReason;
                return check;
            }
            ga.WarWithId = gb.Id;
            gb.WarWithId = ga.Id;
            LastGuildMessage = "war";
            return new AttackResult { Applied = true, Hit = true };
        }

        public AttackResult TryGuildWarPeace(WorldBody from)
        {
            if (from == null)
                return new AttackResult { FailReason = "no_target" };
            var ga = GuildOf(from);
            var gb = ga != null ? FindGuild(ga.WarWithId) : null;
            var check = GuildWarResolve.Peace(new GuildWarRequest
            {
                Ghost = from.Ghost,
                HasGuild = ga != null,
                IsLeader = ga != null && ga.Leader == from,
                AtWar = ga != null && gb != null && ga.WarWithId == gb.Id && gb.WarWithId == ga.Id
            });
            if (!check.Applied)
            {
                LastGuildMessage = check.FailReason;
                return check;
            }
            ClearGuildWar(ga);
            LastGuildMessage = "peace";
            return new AttackResult { Applied = true, Hit = true };
        }

        void ClearGuildWar(Guild guild)
        {
            if (guild == null)
                return;
            if (!string.IsNullOrEmpty(guild.WarWithId))
            {
                var enemy = FindGuild(guild.WarWithId);
                if (enemy != null && enemy.WarWithId == guild.Id)
                    enemy.WarWithId = "";
            }
            guild.WarWithId = "";
        }

        void ClearGuildMembers(Guild guild)
        {
            if (guild == null)
                return;
            if (guild.Leader != null)
            {
                guild.Leader.GuildId = "";
                guild.Leader.GuildName = "";
            }
            for (int i = 0; i < guild.Members.Count; i++)
            {
                var m = guild.Members[i];
                if (m == null)
                    continue;
                m.GuildId = "";
                m.GuildName = "";
            }
            if (guild.Pending != null)
            {
                guild.Pending = null;
            }
            guild.Members.Clear();
            guild.Leader = null;
        }


        public static DungeonGate FindGate(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<DungeonGate>() : null;
        }

        public AttackResult TryDungeon(WorldBody body, DungeonGate gate)
        {
            if (body == null || gate == null)
                return new AttackResult { FailReason = "no_gate" };
            if (body.Ghost)
                return new AttackResult { FailReason = "ghost" };
            float dist = Vector3.Distance(body.transform.position, gate.transform.position);
            if (dist > gate.InteractRange)
                return new AttackResult { FailReason = "range" };
            if (gate.DungeonId == Dungeon1.Id)
            {
                if (gate.IsExit)
                    WarpBody(body, Dungeon1.LeaveX, Dungeon1.LeaveZ);
                else
                    WarpBody(body, Dungeon1.InteriorX, Dungeon1.InteriorZ);
                return new AttackResult { Applied = true };
            }
            if (gate.DungeonId == Dungeon2.Id)
            {
                if (gate.IsExit)
                    WarpBody(body, Dungeon2.LeaveX, Dungeon2.LeaveZ);
                else
                    WarpBody(body, Dungeon2.InteriorX, Dungeon2.InteriorZ);
                return new AttackResult { Applied = true };
            }
            return new AttackResult { FailReason = "unknown_dungeon" };
        }

        public static Moongate FindMoongate(string name)
        {
            var go = GameObject.Find(name);
            return go != null ? go.GetComponent<Moongate>() : null;
        }

        public AttackResult TryGate(WorldBody body, Moongate gate)
        {
            if (body == null || gate == null)
                return new AttackResult { FailReason = "no_gate" };
            float dist = Vector3.Distance(body.transform.position, gate.transform.position);
            var result = TravelResolve.Gate(new TravelRequest
            {
                Distance = dist,
                Range = gate.InteractRange,
                Ghost = body.Ghost,
                Gold = body.Gold
            });
            if (!result.Applied)
                return result;
            body.Gold -= TravelGate.GoldCost;
            WarpBody(body, TravelGate.PlazaX, TravelGate.PlazaZ);
            return result;
        }

        public AttackResult TryMark(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            var result = TravelResolve.Mark(new TravelRequest
            {
                Ghost = body.Ghost,
                InCombat = body.InCombat(Time.time),
                Gold = body.Gold,
                GoldCost = TravelMark.GoldCost
            });
            if (!result.Applied)
            {
                LastTravelMessage = result.FailReason;
                return result;
            }
            body.Gold -= TravelMark.GoldCost;
            body.HasMark = true;
            body.MarkX = body.transform.position.x;
            body.MarkZ = body.transform.position.z;
            LastTravelMessage = "기록";
            return result;
        }

        public AttackResult TryRecall(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            var result = TravelResolve.Recall(new TravelRequest
            {
                Ghost = body.Ghost,
                InCombat = body.InCombat(Time.time),
                HasMark = body.HasMark
            });
            if (!result.Applied)
            {
                LastTravelMessage = result.FailReason;
                return result;
            }
            WarpBody(body, body.MarkX, body.MarkZ);
            LastTravelMessage = "귀환";
            return result;
        }

        static void WarpBody(WorldBody body, float x, float z)
        {
            var cc = body.GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;
            body.transform.position = new Vector3(x, 0.1f, z);
            if (cc != null)
                cc.enabled = true;
        }

        static void EnsureDungeon1Runtime()
        {
            var mob = GameObject.Find(Dungeon1.MobObject);
            if (mob != null)
            {
                var body = mob.GetComponent<WorldBody>();
                if (body != null)
                {
                    body.MobId = MobCatalog.Skeleton;
                    body.IsEnemy = true;
                    body.ApplyMobCatalog();
                }
            }
            var boss = GameObject.Find(Dungeon1.BossObject);
            if (boss == null)
                return;
            var bossBody = boss.GetComponent<WorldBody>();
            if (bossBody == null)
                return;
            bossBody.MobId = MobCatalog.BoneWarden;
            bossBody.IsEnemy = true;
            bossBody.ApplyMobCatalog();
        }

        static void EnsureDungeon2Runtime()
        {
            var mob = GameObject.Find(Dungeon2.MobObject);
            if (mob != null)
            {
                var body = mob.GetComponent<WorldBody>();
                if (body != null)
                {
                    body.MobId = MobCatalog.Bandit;
                    body.IsEnemy = true;
                    body.ApplyMobCatalog();
                }
            }
            var boss = GameObject.Find(Dungeon2.BossObject);
            if (boss == null)
                return;
            var bossBody = boss.GetComponent<WorldBody>();
            if (bossBody == null)
                return;
            bossBody.MobId = MobCatalog.ShadowCaptain;
            bossBody.IsEnemy = true;
            bossBody.ApplyMobCatalog();
        }

        static void EnsureFieldBossRuntime()
        {
            var boss = GameObject.Find(FieldBoss.Object);
            if (boss == null)
                return;
            var bossBody = boss.GetComponent<WorldBody>();
            if (bossBody == null)
                return;
            bossBody.MobId = MobCatalog.Hexarch;
            bossBody.IsEnemy = true;
            bossBody.ApplyMobCatalog();
        }

        public AttackResult GmWarpPlaza(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            WarpBody(body, 0f, 0f);
            OpLog.Write("gm", PersistDriver.AccountKey(), body.name, "warp_plaza");
            return new AttackResult { Applied = true };
        }

        public AttackResult GmGive(WorldBody body, string template, int amount)
        {
            if (body == null || string.IsNullOrEmpty(template))
                return new AttackResult { FailReason = "no_body" };
            Bag(body).Add(template, amount < 1 ? 1 : amount);
            OpLog.Write("gm", PersistDriver.AccountKey(), template, "give " + amount);
            return new AttackResult { Applied = true };
        }

        public AttackResult GmTake(WorldBody body, string template)
        {
            if (body == null || string.IsNullOrEmpty(template))
                return new AttackResult { FailReason = "no_body" };
            if (!Bag(body).TakeOne(template))
                return new AttackResult { FailReason = "missing" };
            OpLog.Write("gm", PersistDriver.AccountKey(), template, "take");
            return new AttackResult { Applied = true };
        }

        public AttackResult GmSetSkill(WorldBody body, SkillId skill, float value)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            if (!SkillsOf(body).TrySet(skill, value))
                return new AttackResult { FailReason = "lock" };
            OpLog.Write("gm", PersistDriver.AccountKey(), skill.ToString(), "set " + value.ToString("0.0"));
            return new AttackResult { Applied = true };
        }

        public AttackResult GmSpawnSkeleton()
        {
            var src = GameObject.Find("Skeleton");
            if (src == null)
                return new AttackResult { FailReason = "no_template" };
            var go = UnityEngine.Object.Instantiate(src);
            go.name = "Skeleton_gm";
            go.SetActive(true);
            go.transform.position = new Vector3(2.2f, 0.1f, 12.4f);
            var nob = go.GetComponent<FishNet.Object.NetworkObject>();
            if (nob != null)
                nob.enabled = false;
            var body = go.GetComponent<WorldBody>();
            if (body != null)
            {
                body.IsEnemy = true;
                body.Ghost = false;
                body.ResetHp();
            }
            OpLog.Write("gm", PersistDriver.AccountKey(), "Skeleton_gm", "spawn");
            return new AttackResult { Applied = true };
        }

        public AttackResult GmDespawnExtra()
        {
            int n = 0;
            var all = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject.name != "Skeleton_gm")
                    continue;
                KillGo(all[i].gameObject);
                n++;
            }
            OpLog.Write("gm", PersistDriver.AccountKey(), "Skeleton_gm", "despawn " + n);
            return n > 0 ? new AttackResult { Applied = true } : new AttackResult { FailReason = "none" };
        }

        bool LootAllowed(WorldBody looter, CorpseNode node)
        {
            if (ActiveParty == null)
                return true;
            if (ActiveParty.Contains(looter))
                return true;
            return false;
        }

        static string WeightRefuseMessage(int str, InventoryBag bag)
        {
            float w = bag != null ? bag.TotalWeight() : 0f;
            int cap = ItemCatalog.CarryCap(str);
            return "과적 — 더 들 수 없습니다 (" + w.ToString("0.#") + "/" + cap + ")";
        }

        bool TooHeavy(WorldBody body)
        {
            var bag = body.GetComponent<InventoryBag>();
            return bag != null && bag.Overweight(StatsOf(body).Str);
        }

        static void KillGo(GameObject go)
        {
            if (go == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(go);
            else
                UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
