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
        public string LastEvalMessage { get; private set; } = "";
        WorldBody[] bodies = System.Array.Empty<WorldBody>();

        public WorldBody Player { get; private set; }
        public WorldBody Selected { get; private set; }
        public TradeSession ActiveTrade { get; private set; }
        public VendorStation ActiveVendor { get; private set; }

        public void CloseVendor() => ActiveVendor = null;
        public TrainerStation ActiveTrainer { get; private set; }
        public void CloseTrainer() => ActiveTrainer = null;
        public Party ActiveParty { get; private set; }
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
            EnsureVendor();
            EnsureTrainer();
            EnsureCarpenter();
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
            TickGuard(Time.time);
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
                FlagCriminal(attacker);
                if (GuardZone.Contains(attacker.transform.position.x, attacker.transform.position.z))
                    GuardStrike(attacker);
                return new AttackResult { FailReason = "innocent" };
            }
            if (TooHeavy(attacker))
                return new AttackResult { FailReason = "overweight" };
            var atkBag = attacker.GetComponent<InventoryBag>();
            string weapon = atkBag != null ? ItemCatalog.CombatWeaponOf(atkBag.Items) : "";
            SkillId weaponSkill = ItemCatalog.CombatSkillOf(weapon);
            if (weapon == ItemCatalog.IronSword && StatsOf(attacker).Str < ItemCatalog.StrReqOf(weapon))
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
                WeaponSkill = weaponSkill
            };
            AttackResult result = AttackResolve.Resolve(req);
            if (!result.Applied)
                return result;
            if (attacker.IsAvatar)
                attacker.RecalcFromStr(StatsOf(attacker).Str);

            nextAttackAt[id] = Time.time + attackCooldown;
            if (!string.IsNullOrEmpty(weapon) && atkBag != null && ItemCatalog.MaxUsesOf(weapon) > 0)
                atkBag.WearTool(weapon);
            target.ApplyDamage(result.Damage);
            if (!target.Alive)
            {
                if (Selected == target)
                    Selected = null;
                if (attacker.IsAvatar)
                {
                    attacker.Fame += 10;
                    attacker.Karma += 1;
                    OpLog.Write("fame", PersistDriver.AccountKey(), target.name, "kill +10");
                }
            }
            else if (attacker.IsAvatar && target.IsEnemy && weaponSkill != SkillId.Archery)
                Retaliate(target, attacker);
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

        void Retaliate(WorldBody enemy, WorldBody defender)
        {
            if (enemy == null || defender == null || !defender.Alive || defender.Ghost)
                return;
            float dist = Vector3.Distance(enemy.transform.position, defender.transform.position);
            if (dist > ItemCatalog.MeleeRange)
                return;
            int dmg = AttackResolve.RetaliationDamage;
            var bag = defender.GetComponent<InventoryBag>();
            bool shield = bag != null && ItemCatalog.HasShield(bag.Items);
            AttackResolve.TryParry(SkillsOf(defender), StatsOf(defender), shield, 20f, ref dmg, out _, out _);
            if (shield)
                bag.WearTool(ItemCatalog.WoodenShield);
            defender.ApplyDamage(dmg);
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
            if (TooHeavy(body))
                return new AttackResult { FailReason = "overweight" };
            var bag = Bag(body);
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
            if (TooHeavy(body))
                return new AttackResult { FailReason = "overweight" };
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
            ConsumeItem(bag, recipe.Ingredient, recipe.Count);
            bag.Add(recipe.Output, 1);
            var skills = SkillsOf(body);
            SkillGain.TryRaise(skills, recipe.Skill, recipe.Difficulty, out float before, out float after, StatsOf(body));
            if (body.IsAvatar)
                body.RecalcFromStr(StatsOf(body).Str);
            OpLog.Write("craft", PersistDriver.AccountKey(), station.gameObject.name, recipe.Output);
            return new AttackResult { Applied = true, Hit = true, SkillBefore = before, SkillAfter = after };
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
            var book = BookOf(body);
            if (!book.Knows(spell))
                return new AttackResult { FailReason = "unlearned" };
            int cost = SpellCast.ManaCost(spell);
            if (body.Mana < cost)
                return new AttackResult { FailReason = "mana" };
            var bag = Bag(body);
            if (CountItem(bag, SpellCast.Reagent) < 1)
                return new AttackResult { FailReason = "reagent" };

            var skills = SkillsOf(body);
            var stats = StatsOf(body);
            if (spell == SpellId.Ember)
            {
                if (target == null || !target.Alive || target.Ghost || target == body)
                    return new AttackResult { FailReason = "no_target" };
                if (target.IsEnemy == body.IsEnemy)
                    return new AttackResult { FailReason = "no_target" };
                float dist = Vector3.Distance(body.transform.position, target.transform.position);
                if (dist > 8f)
                    return new AttackResult { FailReason = "range" };
                ConsumeItem(bag, SpellCast.Reagent, 1);
                body.SetMana(body.Mana - cost);
                int dmg = SpellCast.EmberDamage(stats, skills);
                var targetSkills = SkillsOf(target);
                var targetStats = StatsOf(target);
                var targetBag = target.GetComponent<InventoryBag>();
                int gear = ItemCatalog.EquipmentMagicResist(targetBag != null ? targetBag.Items : null);
                MagicResistResolve.TryResist(targetSkills, targetStats, gear, MagicResistResolve.Difficulty, ref dmg, out _, out _);
                target.ApplyDamage(dmg);
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

            ConsumeItem(bag, SpellCast.Reagent, 1);
            body.SetMana(body.Mana - cost);
            int heal = SpellCast.MendHeal(stats);
            body.SetHp(Mathf.Min(body.MaxHp, body.Hp + heal));
            SkillGain.TryRaise(skills, SkillId.Magery, 18f, out float mb, out float ma, stats);
            if (body.IsAvatar)
                body.RecalcFromInt(stats.Int);
            return new AttackResult { Applied = true, Hit = true, Damage = -heal, SkillBefore = mb, SkillAfter = ma };
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
            bag.Add(templateId, 1);
            if (bag.Overweight(StatsOf(body).Str))
            {
                bag.TakeOne(templateId);
                return new AttackResult { FailReason = "overweight" };
            }
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

        public AttackResult GmWarpPlaza(WorldBody body)
        {
            if (body == null)
                return new AttackResult { FailReason = "no_body" };
            var cc = body.GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;
            body.transform.position = new Vector3(0f, 0.1f, 0f);
            if (cc != null)
                cc.enabled = true;
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
