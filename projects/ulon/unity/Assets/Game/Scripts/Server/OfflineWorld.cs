using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public sealed partial class OfflineWorld : MonoBehaviour
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

    }
}
