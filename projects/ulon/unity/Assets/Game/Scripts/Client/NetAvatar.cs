using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Ulon.Server;
using Ulon.Shared;

namespace Ulon.Client
{
    public sealed class NetAvatar : NetworkBehaviour
    {
        readonly SyncVar<float> skill = new SyncVar<float>();
        string accountId;

        public float SwordSkill => skill.Value;

        public override void OnStartClient()
        {
            bool mine = IsOwner;
            var motor = GetComponent<ClickMotor>();
            var avatar = GetComponent<LocalAvatar>();
            if (motor != null)
                motor.enabled = mine;
            if (avatar != null)
                avatar.enabled = mine;

            if (!mine)
                return;

            var cam = Camera.main != null ? Camera.main.GetComponent<QuarterViewCamera>() : null;
            cam?.SetFollow(transform);
            var body = GetComponent<WorldBody>();
            OfflineWorld.Instance?.SetLocalPlayer(body);
            RpcBind(PersistDriver.AccountKey());
        }

        public override void OnStopNetwork()
        {
            if (IsServerInitialized)
                SaveNow();
        }

        [ServerRpc]
        public void RpcBind(string account)
        {
            accountId = account;
            CharacterStore.EnsureRunning();
            var body = GetComponent<WorldBody>();
            var skills = OfflineWorld.Instance.SkillsOf(body);
            var stats = OfflineWorld.Instance.StatsOf(body);
            var snap = CharacterStore.Load(account);
            if (snap != null)
            {
                CharacterBinder.Apply(body, snap, skills, stats);
                skill.Value = skills.Get(SkillId.Swordsmanship);
            }
            else
                PersistDriver.Creating = true;
        }

        [ServerRpc]
        public void RpcSetPos(Vector3 world)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;
            transform.position = world;
            if (cc != null)
                cc.enabled = true;
        }

        [ServerRpc]
        public void RpcDungeon(string gateName)
        {
            if (OfflineWorld.Instance == null)
                return;
            var gate = OfflineWorld.FindGate(gateName);
            OfflineWorld.Instance.TryDungeon(GetComponent<WorldBody>(), gate);
        }

        [ServerRpc]
        public void RpcGate(string gateName)
        {
            if (OfflineWorld.Instance == null)
                return;
            var gate = OfflineWorld.FindMoongate(gateName);
            var result = OfflineWorld.Instance.TryGate(GetComponent<WorldBody>(), gate);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcStable(string stableName)
        {
            if (OfflineWorld.Instance == null)
                return;
            var stable = OfflineWorld.FindStable(stableName);
            var body = GetComponent<WorldBody>();
            AttackResult result;
            string cid = body != null ? body.CharacterId : "";
            if (OfflineWorld.Instance.HasStabled(cid))
                result = OfflineWorld.Instance.TryClaimStable(body, stable);
            else
                result = OfflineWorld.Instance.TryStable(body, stable);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcRequestAttack(NetworkObject target)
        {
            if (target == null || OfflineWorld.Instance == null)
                return;
            var attacker = GetComponent<WorldBody>();
            var victim = target.GetComponent<WorldBody>();
            var result = OfflineWorld.Instance.TryAttack(attacker, victim);
            if (!result.Applied)
            {
                Debug.Log("[Ulon] attack fail " + result.FailReason);
                return;
            }
            skill.Value = result.SkillAfter;
            target.GetComponent<NetMob>()?.ServerSetHp(victim.Hp);
            RpcPlayAttack();
            SaveNow();
        }

        [ServerRpc]
        public void RpcGather(string nodeId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var node = OfflineWorld.FindNode(nodeId);
            var result = OfflineWorld.Instance.TryGather(GetComponent<WorldBody>(), node);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcTrade(NetworkObject other)
        {
            if (other == null || OfflineWorld.Instance == null)
                return;
            OfflineWorld.Instance.TryTrade(GetComponent<WorldBody>(), other.GetComponent<WorldBody>());
            BroadcastTrade();
        }

        [ServerRpc]
        public void RpcTradeOffer(string template)
        {
            OfflineWorld.Instance?.SetTradeOffer(GetComponent<WorldBody>(), template);
            BroadcastTrade();
        }

        [ServerRpc]
        public void RpcTradeAccept()
        {
            var result = OfflineWorld.Instance != null
                ? OfflineWorld.Instance.ConfirmTrade(GetComponent<WorldBody>())
                : default;
            if (result.Applied)
                SaveNow();
            BroadcastTrade();
        }

        [ServerRpc]
        public void RpcTradeCancel()
        {
            OfflineWorld.Instance?.CancelTrade();
            BroadcastTrade();
        }

        void BroadcastTrade()
        {
            var t = OfflineWorld.Instance != null ? OfflineWorld.Instance.ActiveTrade : null;
            if (t == null)
            {
                RpcTradeState(false, 0, 0, "", "", "", "", false, false);
                return;
            }
            int idA = t.A.GetComponent<NetworkObject>() != null ? t.A.GetComponent<NetworkObject>().ObjectId : 0;
            int idB = t.B.GetComponent<NetworkObject>() != null ? t.B.GetComponent<NetworkObject>().ObjectId : 0;
            RpcTradeState(true, idA, idB, t.A.DisplayName, t.B.DisplayName, t.OfferA, t.OfferB, t.AcceptA, t.AcceptB);
        }

        [ObserversRpc]
        void RpcTradeState(bool open, int idA, int idB, string nameA, string nameB, string offerA, string offerB, bool accA, bool accB)
        {
            TradeView.Open = open;
            TradeView.IdA = idA;
            TradeView.IdB = idB;
            TradeView.NameA = nameA;
            TradeView.NameB = nameB;
            TradeView.OfferA = offerA;
            TradeView.OfferB = offerB;
            TradeView.AcceptA = accA;
            TradeView.AcceptB = accB;
        }

        [ServerRpc]
        public void RpcCraft(string stationId, string recipeId = "")
        {
            if (OfflineWorld.Instance == null)
                return;
            var station = OfflineWorld.FindStation(stationId);
            var result = OfflineWorld.Instance.TryCraft(GetComponent<WorldBody>(), station, recipeId);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcBank(string stationId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var station = OfflineWorld.FindBank(stationId);
            var result = OfflineWorld.Instance.TryBank(GetComponent<WorldBody>(), station);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcVendor(string stationId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var vendor = OfflineWorld.FindVendor(stationId);
            OfflineWorld.Instance.TryVendor(GetComponent<WorldBody>(), vendor);
        }

        [ServerRpc]
        public void RpcBuy(string templateId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryBuy(GetComponent<WorldBody>(), templateId);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcSell(string templateId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TrySell(GetComponent<WorldBody>(), templateId);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcTrainer(string stationId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var trainer = OfflineWorld.FindTrainer(stationId);
            OfflineWorld.Instance.TryTrainer(GetComponent<WorldBody>(), trainer);
        }

        [ServerRpc]
        public void RpcTrain(int skillId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryTrain(GetComponent<WorldBody>(), (SkillId)skillId);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcCast(int spellId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryCast(GetComponent<WorldBody>(), (SpellId)spellId, OfflineWorld.Instance.Selected);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcMark()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryMark(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcRecall()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryRecall(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcDrink()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryDrink(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcHeal()
        {
            if (OfflineWorld.Instance == null)
                return;
            var body = GetComponent<WorldBody>();
            WorldBody target = OfflineWorld.Instance.Selected;
            if (target != null && target.Ghost && target.IsAvatar && target != body)
            {
                var rez = OfflineWorld.Instance.TryResurrectBandage(body, target);
                if (rez.Applied)
                    SaveNow();
                return;
            }
            if (target == null || target.IsEnemy || !target.Alive)
                target = body;
            var result = OfflineWorld.Instance.TryHeal(body, target);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcMeditate()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryMeditate(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcEvaluate()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryEvaluate(GetComponent<WorldBody>(), OfflineWorld.Instance.Selected);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcTrack()
        {
            if (OfflineWorld.Instance == null)
                return;
            var body = GetComponent<WorldBody>();
            var world = OfflineWorld.Instance;
            TrackingResult result;
            if (world.Selected != null)
                result = world.TryTrack(body, world.Selected);
            else
                result = world.TryTrackCorpse(body, OfflineWorld.FindCorpse(body != null ? body.CharacterId : ""));
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcLore()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryLore(GetComponent<WorldBody>(), OfflineWorld.Instance.Selected);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcVet()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryVet(GetComponent<WorldBody>(), OfflineWorld.Instance.Selected);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcInscribe()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryInscribe(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcPoisonWeapon()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryPoisonWeapon(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcUseScroll()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryUseScroll(GetComponent<WorldBody>(), OfflineWorld.Instance.Selected);
            if (result.Applied)
                SaveNow();
        }


        [ServerRpc]
        public void RpcPlay()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryPlay(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcPeace()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryPeace(GetComponent<WorldBody>(), OfflineWorld.Instance.Selected);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcProvoke()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryProvokeStep(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcHide()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryHide(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }


        [ServerRpc]
        public void RpcPick(string crateId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var crate = OfflineWorld.FindCrate(crateId);
            var result = OfflineWorld.Instance.TryPick(GetComponent<WorldBody>(), crate);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcStealth()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryStealth(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcDetectHidden()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryDetectHidden(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }


        [ServerRpc]
        public void RpcCamp()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TryCamp(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcSteal()
        {
            if (OfflineWorld.Instance == null)
                return;
            var result = OfflineWorld.Instance.TrySteal(GetComponent<WorldBody>());
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcResurrectBandage()
        {
            if (OfflineWorld.Instance == null)
                return;
            var body = GetComponent<WorldBody>();
            WorldBody target = OfflineWorld.Instance.Selected;
            if (target == null || !target.Ghost || !target.IsAvatar || target == body)
                target = OfflineWorld.NearestGhostAvatar(body);
            var result = OfflineWorld.Instance.TryResurrectBandage(body, target);
            if (result.Applied)
                SaveNow();
        }
        [ServerRpc]
        public void RpcCurePoison()
        {
            if (OfflineWorld.Instance == null)
                return;
            var body = GetComponent<WorldBody>();
            WorldBody target = OfflineWorld.Instance.Selected;
            if (target == null || target.IsEnemy || !target.Alive || target.Ghost)
                target = body;
            var result = OfflineWorld.Instance.TryCurePoison(body, target);
            if (result.Applied)
                SaveNow();
        }


        [ServerRpc]
        public void RpcResurrect(string stationId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var healer = OfflineWorld.FindHealer(stationId);
            var result = OfflineWorld.Instance.TryResurrect(GetComponent<WorldBody>(), healer);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcPartyInvite(NetworkObject other)
        {
            if (other == null || OfflineWorld.Instance == null)
                return;
            OfflineWorld.Instance.TryPartyInvite(GetComponent<WorldBody>(), other.GetComponent<WorldBody>());
            BroadcastParty();
        }

        [ServerRpc]
        public void RpcPartyAccept()
        {
            OfflineWorld.Instance?.TryPartyAccept(GetComponent<WorldBody>());
            BroadcastParty();
        }

        [ServerRpc]
        public void RpcPartyLeave()
        {
            OfflineWorld.Instance?.TryPartyLeave(GetComponent<WorldBody>());
            BroadcastParty();
        }

        [ServerRpc]
        public void RpcPartySay(string text)
        {
            OfflineWorld.Instance?.TryPartySay(GetComponent<WorldBody>(), text);
            BroadcastParty();
        }

        void BroadcastParty()
        {
            var p = OfflineWorld.Instance != null ? OfflineWorld.Instance.ActiveParty : null;
            if (p == null)
            {
                RpcPartyState(false, 0, "", "", "");
                return;
            }
            string roster = p.Leader != null ? p.Leader.DisplayName + " " + p.Leader.Hp.ToString("0") + "/" + p.Leader.MaxHp.ToString("0") : "";
            for (int i = 0; i < p.Members.Count; i++)
            {
                var m = p.Members[i];
                if (m == null)
                    continue;
                roster += "\n" + m.DisplayName + " " + m.Hp.ToString("0") + "/" + m.MaxHp.ToString("0");
            }
            string chat = "";
            for (int i = 0; i < p.Chat.Count; i++)
            {
                if (i > 0)
                    chat += "\n";
                chat += p.Chat[i];
            }
            int pendingId = 0;
            if (p.Pending != null)
            {
                var pn = p.Pending.GetComponent<NetworkObject>();
                if (pn != null)
                    pendingId = pn.ObjectId;
            }
            RpcPartyState(true, pendingId, p.Leader != null ? p.Leader.DisplayName : "", roster, chat);
        }

        [ObserversRpc]
        void RpcPartyState(bool open, int pendingId, string leader, string roster, string chat)
        {
            PartyView.Open = open;
            PartyView.PendingMe = pendingId != 0 && pendingId == ObjectId;
            PartyView.Leader = leader;
            PartyView.Roster = roster;
            PartyView.Chat = chat;
        }

        [ServerRpc]
        public void RpcGuildCreate(string name)
        {
            OfflineWorld.Instance?.TryGuildCreate(GetComponent<WorldBody>(), name);
            BroadcastGuild();
        }

        [ServerRpc]
        public void RpcGuildInvite(NetworkObject other)
        {
            if (other == null || OfflineWorld.Instance == null)
                return;
            OfflineWorld.Instance.TryGuildInvite(GetComponent<WorldBody>(), other.GetComponent<WorldBody>());
            BroadcastGuild();
        }

        [ServerRpc]
        public void RpcGuildAccept()
        {
            OfflineWorld.Instance?.TryGuildAccept(GetComponent<WorldBody>());
            BroadcastGuild();
        }

        [ServerRpc]
        public void RpcGuildLeave()
        {
            OfflineWorld.Instance?.TryGuildLeave(GetComponent<WorldBody>());
            BroadcastGuild();
        }

        [ServerRpc]
        public void RpcGuildWarDeclare(NetworkObject other)
        {
            if (other == null || OfflineWorld.Instance == null)
                return;
            OfflineWorld.Instance.TryGuildWarDeclare(GetComponent<WorldBody>(), other.GetComponent<WorldBody>());
            BroadcastGuild();
        }

        [ServerRpc]
        public void RpcGuildWarPeace()
        {
            OfflineWorld.Instance?.TryGuildWarPeace(GetComponent<WorldBody>());
            BroadcastGuild();
        }

        [ServerRpc]
        public void RpcDuelInvite(NetworkObject other)
        {
            if (other == null || OfflineWorld.Instance == null)
                return;
            OfflineWorld.Instance.TryDuelInvite(GetComponent<WorldBody>(), other.GetComponent<WorldBody>());
        }

        [ServerRpc]
        public void RpcDuelAccept()
        {
            OfflineWorld.Instance?.TryDuelAccept(GetComponent<WorldBody>());
        }

        [ServerRpc]
        public void RpcDuelEnd()
        {
            OfflineWorld.Instance?.TryDuelEnd(GetComponent<WorldBody>());
        }

        [ServerRpc]
        public void RpcDuelYield()
        {
            OfflineWorld.Instance?.TryDuelYield(GetComponent<WorldBody>());
        }

        void BroadcastGuild()
        {
            var body = GetComponent<WorldBody>();
            var world = OfflineWorld.Instance;
            var g = world != null ? world.GuildOf(body) : null;
            if (g == null)
            {
                RpcGuildState(false, 0, "", "", "", "", "");
                return;
            }
            string roster = g.Leader != null ? g.Leader.DisplayName : "";
            for (int i = 0; i < g.Members.Count; i++)
            {
                var m = g.Members[i];
                if (m == null)
                    continue;
                roster += "\n" + m.DisplayName;
            }
            int pendingId = 0;
            if (g.Pending != null)
            {
                var pn = g.Pending.GetComponent<NetworkObject>();
                if (pn != null)
                    pendingId = pn.ObjectId;
            }
            string warName = "";
            if (!string.IsNullOrEmpty(g.WarWithId))
            {
                var enemy = world.FindGuild(g.WarWithId);
                warName = enemy != null ? enemy.Name : g.WarWithId;
            }
            RpcGuildState(true, pendingId, g.Id, g.Name, g.Leader != null ? g.Leader.DisplayName : "", roster, warName);
        }

        [ObserversRpc]
        void RpcGuildState(bool open, int pendingId, string guildId, string guildName, string leader, string roster, string warName)
        {
            GuildView.Open = open;
            GuildView.PendingMe = pendingId != 0 && pendingId == ObjectId;
            GuildView.GuildId = guildId ?? "";
            GuildView.GuildName = guildName ?? "";
            GuildView.Leader = leader ?? "";
            GuildView.Roster = roster ?? "";
            GuildView.WarName = warName ?? "";
            var body = GetComponent<WorldBody>();
            if (body != null && IsOwner)
            {
                body.GuildId = guildId ?? "";
                body.GuildName = guildName ?? "";
            }
        }


        [ServerRpc]
        public void RpcLoot(string ownerId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var node = OfflineWorld.FindCorpse(ownerId);
            var result = OfflineWorld.Instance.TryLootCorpse(GetComponent<WorldBody>(), node);
            if (result.Applied)
                SaveNow();
        }

        [ObserversRpc]
        void RpcPlayAttack()
        {
            GetComponent<CharacterAnim>()?.PlayAttack();
        }


        [ServerRpc]
        public void RpcClaimHouse(string stationId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var station = OfflineWorld.FindHouseStation(stationId);
            var result = OfflineWorld.Instance.TryClaimHouse(GetComponent<WorldBody>(), station);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcHouseLockdown(string chestId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var chest = OfflineWorld.FindHouseChest(chestId);
            var result = OfflineWorld.Instance.TryLockdown(GetComponent<WorldBody>(), chest, ItemCatalog.Cloth);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcHouseTake(string chestId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var chest = OfflineWorld.FindHouseChest(chestId);
            var result = OfflineWorld.Instance.TrySecureTake(GetComponent<WorldBody>(), chest);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcHouseVendorList(string vendorId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var vendor = OfflineWorld.FindHouseVendor(vendorId);
            var result = OfflineWorld.Instance.TryListVendor(GetComponent<WorldBody>(), vendor, ItemCatalog.Cloth);
            if (result.Applied)
                SaveNow();
        }

        [ServerRpc]
        public void RpcHouseVendorBuy(string vendorId)
        {
            if (OfflineWorld.Instance == null)
                return;
            var vendor = OfflineWorld.FindHouseVendor(vendorId);
            var result = OfflineWorld.Instance.TryBuyHouseVendor(GetComponent<WorldBody>(), vendor);
            if (result.Applied)
                SaveNow();
        }

        void SaveNow()
        {
            if (string.IsNullOrEmpty(accountId) || OfflineWorld.Instance == null)
                return;
            var body = GetComponent<WorldBody>();
            CharacterStore.Save(CharacterBinder.Capture(accountId, body, OfflineWorld.Instance.SkillsOf(body), OfflineWorld.Instance.StatsOf(body)));
        }
    }
}
