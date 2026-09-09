using UnityEngine;
using Ulon.Shared;
using Ulon.Server;

namespace Ulon.Client
{
    public sealed partial class SliceHud : MonoBehaviour
    {
        // **탭을 열면 나오는 판들**(랩 ㉭) — 가방·행동·스킬·사교·주변·상거래·GM.
        // 담는 것: 판 하나하나의 배치와 내용. 안 담는 것: 항상 떠 있는 것(상태·타깃·퀵바·탭 = 본체),
        // 단추가 실제로 보내는 명령(`Actions`), 캐릭터 생성(`Create`).
        void PanelBag(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            var bag = me.GetComponent<InventoryBag>();
            var vault = me.GetComponent<BankVault>();
            int count = bag != null ? bag.Items.Count : 0;
            if (bagPick >= count)
                bagPick = -1;

            GUILayout.Label("가방" + (count > 0 ? "  " + count + "칸" : ""));
            bagScroll = GUILayout.BeginScrollView(bagScroll, GUILayout.Height(Mathf.Min(200f, 26f * Mathf.Max(1, count) + 8f)));
            if (count == 0)
                GUILayout.Label("비었습니다");
            else
                for (int i = 0; i < count; i++)
                {
                    var it = bag.Items[i];
                    // 같은 도구가 두 줄인 이유 — **개체마다 내구가 따로**라 겹쳐 쌓이지 않는다.
                    // 화면에 번호를 붙여 「왜 두 개지?」가 되지 않게 한다(검수 2026-09-07).
                    int dup = 0, seen = 0;
                    for (int j = 0; j < count; j++)
                        if (bag.Items[j].TemplateId == it.TemplateId) { dup++; if (j <= i) seen++; }
                    // 고른 항목이 화면에 보여야 한다 — 버튼이 「무엇에」 작용하는지 모르면 도달 불가와 같다(검수).
                    string line = (bagPick == i ? "▸ " : "   ") + ItemCatalog.DisplayNameOf(it.TemplateId) +
                                  (dup > 1 ? " (" + seen + "/" + dup + ")" : "") +
                                  (it.Amount > 1 ? " x" + it.Amount : "") +
                                  (it.Uses > 0 ? "  내구 " + it.Uses : "") +
                                  (it.Exceptional ? "  걸작" : "") +
                                  (!string.IsNullOrEmpty(it.MakerId) ? "  제작 " + it.MakerId : "");
                    if (Btn(line, ItemStyle(bagPick == i)))
                        bagPick = bagPick == i ? -1 : i;
                }
            GUILayout.EndScrollView();

            GUILayout.Label(vault != null && vault.Items.Count > 0 ? "은행" : "은행 비움");
            if (vault != null)
                for (int i = 0; i < vault.Items.Count; i++)
                    GUILayout.Label("   " + ItemCatalog.DisplayNameOf(vault.Items[i].TemplateId) +
                                    (vault.Items[i].Amount > 1 ? " x" + vault.Items[i].Amount : ""));
            if (me.Ghost)
                return;

            string picked = bagPick >= 0 && bagPick < count ? bag.Items[bagPick].TemplateId : "";
            GUILayout.Label(picked == "" ? "고른 것 없음 — 항목을 눌러 고르세요"
                                         : "고른 것: " + ItemCatalog.DisplayNameOf(picked));
            // 선택이 없으면 버튼은 **비활성**이다(누를 수는 있으나 아무 일도 안 하는 버튼을 두지 않는다).
            GUI.enabled = picked != "";
            GUILayout.BeginHorizontal();
            if (Btn("착용")) EquipItem(net, picked);
            if (Btn("주머니↓")) PouchInItem(net, picked);
            if (Btn("주머니↑")) PouchOutItem(net, picked);
            GUILayout.EndHorizontal();
            GUI.enabled = true;
            if (Btn("장비 해제")) Unequip(net);
        }

        static GUIStyle pickedStyle;
        static GUIStyle ItemStyle(bool on)
        {
            if (pickedStyle == null)
            {
                pickedStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
            }
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
            return on ? pickedStyle : style;
        }

        void PanelAction(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            GUILayout.Label("행동");
            if (me.Ghost)
            {
                if (Btn("붕대 부활")) ResurrectBandage(net);
                return;
            }
            Row3(("추적", () => Track(net)), ("연주", () => PlayLute(net)), (SkillNames.KoreanOf(SkillId.Peacemaking), () => Peace(net)));
            Row3((SkillNames.KoreanOf(SkillId.Provocation), () => Provoke(net)), (SkillNames.KoreanOf(SkillId.Hiding), () => Hide(net)), (SkillNames.KoreanOf(SkillId.Stealth), () => Stealth(net)));
            Row3((SkillNames.KoreanOf(SkillId.DetectHidden), () => DetectHidden(net)), (SkillNames.KoreanOf(SkillId.Camping), () => Camp(net)), (SkillNames.KoreanOf(SkillId.Stealing), () => Steal(net)));
            Row3((SkillNames.KoreanOf(SkillId.AnimalLore), () => Lore(net)), (SkillNames.KoreanOf(SkillId.Veterinary), () => Vet(net)), (SkillNames.KoreanOf(SkillId.Inscription), () => Inscribe(net)));
            Row3((SkillNames.KoreanOf(SkillId.Poisoning), () => PoisonWeapon(net)), ("주문서", () => UseScroll(net)), (SkillNames.KoreanOf(SkillId.EvaluateIntelligence), () => Evaluate(net)));
            Row3(("기록", () => Mark(net)), ("귀환", () => Recall(net)), ("해독", () => CurePoison(net)));
            Row3(("부활", () => ResurrectBandage(net)), ("의뢰", () => AcceptCraftOrder(net)), ("납품", () => TurnInCraftOrder(net)));
            Row3(("펫공격", () => PetAttack(net)), ("펫호출", () => PetCome(net)), ("펫놓아줌", () => PetRelease(net)));
            GUILayout.Space(6f);
            GUILayout.Label("말 걸기");
            Row3(("은행", () => SpeakKeyword(net, "은행")), ("경비", () => SpeakKeyword(net, "경비")), ("상점", () => SpeakKeyword(net, "상점")));
            GUILayout.BeginHorizontal();
            keywordSpeech = GUILayout.TextField(keywordSpeech ?? "");
            if (Btn("말", GUILayout.Width(48f)))
            {
                SpeakKeyword(net, keywordSpeech);
                keywordSpeech = "";
            }
            GUILayout.EndHorizontal();
        }

        static void Row3(params (string Label, System.Action OnClick)[] items)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < items.Length; i++)
                if (Btn(items[i].Label))
                    items[i].OnClick();
            GUILayout.EndHorizontal();
        }

        void PanelSkills(OfflineWorld world)
        {
            var sk = world.PlayerSkills;
            var st = world.PlayerStats;
            GUILayout.Label("스킬 — 이름을 누르면 잠금(↑ 오름 · = 고정 · ↓ 내림)");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 3; i++)
            {
                var id = (StatId)i;
                string name = id == StatId.Str ? "STR" : id == StatId.Dex ? "DEX" : "INT";
                int val = id == StatId.Str ? st.Str : id == StatId.Dex ? st.Dex : st.Int;
                if (Btn(name + " " + val + " " + LockMark(st.GetLock(id))))
                    st.CycleLock(id);
            }
            GUILayout.EndHorizontal();
            skillScroll = GUILayout.BeginScrollView(skillScroll);
            int n = (int)SkillId.Count;
            for (int i = 0; i < n; i += 2)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < 2 && i + c < n; c++)
                {
                    var id = (SkillId)(i + c);
                    if (Btn(SkillNames.KoreanOf(id) + " " + sk.Get(id).ToString("0.0") + " " + LockMark(sk.GetLock(id))))
                        sk.CycleLock(id);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        static string LockMark(SkillLock state) => state == SkillLock.Up ? "↑" : state == SkillLock.Down ? "↓" : "=";

        void PanelSocial(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            socialScroll = GUILayout.BeginScrollView(socialScroll);
            PanelParty(world, me, net);
            GUILayout.Space(8f);
            PanelGuild(world, me, net);
            GUILayout.Space(8f);
            PanelDuel(world, me, net);
            GUILayout.EndScrollView();
        }

        void PanelParty(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            var party = me.Party;
            bool netOpen = PartyView.Open && (net == null || !net.IsServerInitialized);
            GUILayout.Label("파티");
            if (party == null && !netOpen)
            {
                if (PartyView.PendingMe)
                {
                    if (Btn("수락"))
                    {
                        if (net != null && net.IsClientInitialized) net.RpcPartyAccept();
                        else world.TryPartyAccept(me);
                    }
                    return;
                }
                // **대상은 씬 이름이 아니라 거리로 고른다**(검수 지시 2026-09-08).
                // `GameObject.Find("Companion")`은 네트워크에서 그 오브젝트가 꺼지면 null이 되고,
                // 버튼이 통째로 사라져 **온라인에서만 파티를 못 만드는** 상태였다.
                // 반경은 서버가 받아 주는 사거리와 같은 값을 쓴다(`PartyResolve.InviteRange`) —
                // 버튼이 보이는데 서버가 거절하는 어긋남을 없앤다.
                var palBody = OfflineWorld.NearestInvitee(me, PartyResolve.InviteRange);
                if (palBody != null && Btn(palBody.IsAvatar ? "파티 초대" : "동료 초대"))
                {
                    var nob = palBody.GetComponent<FishNet.Object.NetworkObject>();
                    if (net != null && net.IsClientInitialized && nob != null) net.RpcPartyInvite(nob);
                    else world.TryPartyInvite(me, palBody);
                }
                return;
            }
            string roster = "";
            if (party != null)
            {
                if (party.Leader != null)
                    roster = party.Leader.DisplayName + " " + party.Leader.Hp.ToString("0") + "/" + party.Leader.MaxHp.ToString("0");
                for (int i = 0; i < party.Members.Count; i++)
                {
                    var m = party.Members[i];
                    if (m == null) continue;
                    roster += "  " + m.DisplayName + " " + m.Hp.ToString("0") + "/" + m.MaxHp.ToString("0");
                }
            }
            else
                roster = PartyView.Roster.Replace("\n", "  ");
            GUILayout.Label(roster);
            GUILayout.Label(party != null && party.Chat.Count > 0 ? party.Chat[party.Chat.Count - 1] : PartyView.Chat);
            GUILayout.BeginHorizontal();
            partyChat = GUILayout.TextField(partyChat ?? "");
            if (Btn("말", GUILayout.Width(48f)))
            {
                if (net != null && net.IsClientInitialized) net.RpcPartySay(partyChat);
                else world.TryPartySay(me, partyChat);
                partyChat = "";
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (Btn("탈퇴"))
            {
                if (net != null && net.IsClientInitialized) net.RpcPartyLeave();
                else world.TryPartyLeave(me);
            }
            if (party != null && party.Pending == me && Btn("수락"))
                world.TryPartyAccept(me);
            GUILayout.EndHorizontal();
        }

        void PanelGuild(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            var guild = world.GuildOf(me);
            bool netOpen = GuildView.Open && (net == null || !net.IsServerInitialized);
            GUILayout.Label("길드");
            if (guild == null && !netOpen && string.IsNullOrEmpty(me.GuildId))
            {
                if (GuildView.PendingMe)
                {
                    if (Btn("수락"))
                    {
                        if (net != null && net.IsClientInitialized) net.RpcGuildAccept();
                        else world.TryGuildAccept(me);
                    }
                    return;
                }
                GUILayout.BeginHorizontal();
                guildNameInput = GUILayout.TextField(guildNameInput ?? "");
                if (Btn("창설", GUILayout.Width(60f)))
                {
                    if (net != null && net.IsClientInitialized) net.RpcGuildCreate(guildNameInput);
                    else world.TryGuildCreate(me, guildNameInput);
                }
                GUILayout.EndHorizontal();
                return;
            }
            string name = guild != null ? guild.Name : GuildView.GuildName;
            string roster = "";
            if (guild != null)
            {
                if (guild.Leader != null)
                    roster = guild.Leader.DisplayName;
                for (int i = 0; i < guild.Members.Count; i++)
                {
                    var m = guild.Members[i];
                    if (m == null) continue;
                    roster += "  " + m.DisplayName;
                }
            }
            else
                roster = GuildView.Roster.Replace("\n", "  ");
            string war = guild != null && !string.IsNullOrEmpty(guild.WarWithId)
                ? (world.FindGuild(guild.WarWithId) != null ? world.FindGuild(guild.WarWithId).Name : guild.WarWithId)
                : GuildView.WarName;
            GUILayout.Label("[" + name + "] " + roster + (string.IsNullOrEmpty(war) ? "" : "  전쟁 " + war));
            // 파티와 **같은 구멍**이었다(검수 지시 2026-09-08): 씬 이름 고정이라 네트워크에서
            // Companion이 꺼지면 버튼이 사라져 「창설은 되는데 초대가 안 되는」 반쪽이 됐다.
            // 대상은 거리로 고르고, 반경은 서버가 받아 주는 사거리와 같은 값을 쓴다.
            var palBody = OfflineWorld.NearestInvitee(me, GuildRules.InviteRange);
            GUILayout.BeginHorizontal();
            if (palBody != null && guild != null && guild.Leader == me && Btn(palBody.IsAvatar ? "길드 초대" : "동료 초대"))
            {
                var nob = palBody.GetComponent<FishNet.Object.NetworkObject>();
                if (net != null && net.IsClientInitialized && nob != null) net.RpcGuildInvite(nob);
                else world.TryGuildInvite(me, palBody);
            }
            if (Btn("탈퇴"))
            {
                if (net != null && net.IsClientInitialized) net.RpcGuildLeave();
                else world.TryGuildLeave(me);
            }
            if (guild != null && guild.Pending == me && Btn("수락"))
                world.TryGuildAccept(me);
            GUILayout.EndHorizontal();
            var foe = me.Selected;
            if (guild != null && guild.Leader == me && foe != null && foe != me && world.GuildOf(foe) != null
                && world.GuildOf(foe) != guild && string.IsNullOrEmpty(guild.WarWithId) && Btn("선전포고"))
            {
                var nob = foe.GetComponent<FishNet.Object.NetworkObject>();
                if (net != null && net.IsClientInitialized && nob != null) net.RpcGuildWarDeclare(nob);
                else world.TryGuildWarDeclare(me, foe);
            }
            if (guild != null && guild.Leader == me && !string.IsNullOrEmpty(guild.WarWithId) && Btn("강화"))
            {
                if (net != null && net.IsClientInitialized) net.RpcGuildWarPeace();
                else world.TryGuildWarPeace(me);
            }
        }

        void PanelDuel(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            GUILayout.Label("결투");
            var foe = me.Selected;
            var pal = GameObject.Find("Companion");
            var palBody = pal != null ? pal.GetComponent<WorldBody>() : null;
            var duelFoe = foe != null && foe != me && foe.IsAvatar && !foe.IsEnemy ? foe : palBody;
            if (me.DuelOpponent == null)
            {
                if (duelFoe != null && duelFoe != me && Btn("결투 초대"))
                {
                    var nob = duelFoe.GetComponent<FishNet.Object.NetworkObject>();
                    if (net != null && net.IsClientInitialized && nob != null) net.RpcDuelInvite(nob);
                    else world.TryDuelInvite(me, duelFoe);
                }
                bool pendingMe = false;
                var bodies = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
                for (int i = 0; i < bodies.Length; i++)
                    if (bodies[i] != null && bodies[i].PendingDuel == me) { pendingMe = true; break; }
                if (pendingMe && Btn("결투수락"))
                {
                    if (net != null && net.IsClientInitialized) net.RpcDuelAccept();
                    else world.TryDuelAccept(me);
                }
                return;
            }
            GUILayout.Label("결투 중 " + (me.DuelOpponent.DisplayName ?? ""));
            GUILayout.BeginHorizontal();
            if (Btn("항복"))
            {
                if (net != null && net.IsClientInitialized) net.RpcDuelYield();
                else world.TryDuelYield(me);
            }
            if (Btn("종료"))
            {
                if (net != null && net.IsClientInitialized) net.RpcDuelEnd();
                else world.TryDuelEnd(me);
            }
            GUILayout.EndHorizontal();
        }

        // ── 근처(상황) — 상점·훈련·제작대·던전문·궤짝·거래 ────────────────────────────
        static int NearbyCount(OfflineWorld world, WorldBody me)
        {
            int n = 0;
            if (me.ActiveVendor != null) n++;
            if (me.ActiveTrainer != null) n++;
            if (me.Trade != null || TradeView.Open) n++;
            if (InRange(me, OfflineWorld.FindStation("Forge"))) n++;
            if (InRange(me, OfflineWorld.FindStation("Carpenter"))) n++;
            if (InRange(me, OfflineWorld.FindStation("Mortar"))) n++;
            if (InRangeCrate(me, OfflineWorld.FindCrate("LockedCrate"))) n++;
            if (NearestCorpse(me) != null) n++;
            if (NearestGate(me, Dungeon1.EntranceObject, Dungeon1.ExitObject,
                            Dungeon2.EntranceObject, Dungeon2.ExitObject,
                            Dungeon3.EntranceObject, Dungeon3.ExitObject) != null) n++;
            return n;
        }

        static bool InRange(WorldBody me, CraftStation s)
            => s != null && me != null && !me.Ghost
               && Vector3.Distance(me.transform.position, s.transform.position) <= s.InteractRange;

        static bool InRangeCrate(WorldBody me, LockedCrate c)
            => c != null && me != null && !me.Ghost
               && Vector3.Distance(me.transform.position, c.transform.position) <= c.InteractRange;

        /// <summary>
        /// **근접한 시체 하나** — 오너 판정(2026-09-08)대로 **내 것이든 남의 것이든** 가까이 있으면
        /// 화면에 뜬다. 「보는 것」은 근접 전원, 「가져가는 것」은 `loot_right`가 따로 지킨다.
        /// </summary>
        static CorpseNode NearestCorpse(WorldBody me)
        {
            if (me == null)
                return null;
            CorpseNode best = null;
            float bestD = float.MaxValue;
            var all = Object.FindObjectsByType<CorpseNode>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                float d = Vector3.Distance(me.transform.position, all[i].transform.position);
                if (d <= all[i].InteractRange && d < bestD)
                {
                    bestD = d;
                    best = all[i];
                }
            }
            return best;
        }

        void PanelNearby(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            GUILayout.Label("근처");
            // 목록이 길어져도 화면을 위아래로 다 쓰지 않는다 — 넘치면 스크롤(검수 조건 1).
            nearbyScroll = GUILayout.BeginScrollView(nearbyScroll, GUILayout.MaxHeight(Screen.height * 0.42f));
            if (NearbyCount(world, me) == 0)
                GUILayout.Label("가까이에 쓸 것이 없습니다.");

            var corpse = NearestCorpse(me);
            if (corpse != null)
            {
                // **내 시체와 남의 시체를 화면에서 가른다**(검수 조건 ㉡) — 구별이 없으면
                // 「가져갈 수 있는 것」을 착각한다. 이름 앞의 말이 그 구별이다.
                bool mineCorpse = corpse.OwnerId == PersistDriver.AccountKey();
                GUILayout.Label((mineCorpse ? "내 시체" : "남의 시체 · " + corpse.LastKind) +
                                "  ·  " + corpse.SecondsLeft.ToString("0") + "초");
                if (Btn("시체 보기"))
                {
                    if (net != null && net.IsClientInitialized)
                        net.RpcCorpsePeek(corpse.OwnerId);
                    else
                        OfflineWorld.Instance?.TryPeekCorpse(me, corpse, out _);
                }
                if (corpse.Items.Count == 0)
                    GUILayout.Label(NetAvatar.LastPeekFail == "" ? "   (열어 보세요)"
                                                                 : "   열람 거절 — " + NetAvatar.LastPeekFail);
                for (int i = 0; i < corpse.Items.Count; i++)
                    GUILayout.Label("   " + ItemCatalog.DisplayNameOf(corpse.Items[i].TemplateId) +
                                    " × " + corpse.Items[i].Amount);
                if (!me.Ghost && Btn("전부 가져가기"))
                {
                    if (net != null && net.IsClientInitialized)
                        net.RpcLoot(corpse.OwnerId);
                    else
                        OfflineWorld.Instance?.TryLootCorpse(me, corpse);
                }
                GUILayout.Space(6f);
            }
            if (me.ActiveVendor != null && !me.Ghost)
            {
                // 상인 이름이 이미 「잡화」다 — 앞에 종류를 또 붙이면 「잡화 잡화」가 된다.
                GUILayout.Label(me.ActiveVendor.DisplayName + "  ·  살 것");
                for (int i = 0; i < ShopBuy.Length; i++)
                {
                    string id = ShopBuy[i];
                    if (Btn((shopPick == id ? "▸ " : "   ") + ItemCatalog.DisplayNameOf(id) +
                                         "  " + ItemCatalog.BuyPrice(id) + "G", ItemStyle(shopPick == id)))
                        shopPick = shopPick == id ? "" : id;
                }
                GUILayout.Label("팔 것");
                for (int i = 0; i < ShopSell.Length; i++)
                {
                    string id = ShopSell[i];
                    if (Btn((shopPick == id ? "▸ " : "   ") + ItemCatalog.DisplayNameOf(id) +
                                         "  " + Owned(me, id) + "개 보유", ItemStyle(shopPick == id)))
                        shopPick = shopPick == id ? "" : id;
                }
                GUILayout.Label(shopPick == "" ? "고른 것 없음 — 항목을 눌러 고르세요"
                                               : "고른 것: " + ItemCatalog.DisplayNameOf(shopPick));
                // 산 것을 팔 수는 없다 — 고른 항목이 어느 목록의 것이냐에 따라 버튼이 켜진다.
                // (둘 다 켜 두면 눌러 보고 나서야 안 된다는 걸 알게 된다 — 가방과 같은 규칙.)
                GUILayout.BeginHorizontal();
                GUI.enabled = shopPick != "" && System.Array.IndexOf(ShopBuy, shopPick) >= 0;
                if (Btn("사기")) Shop(net, true, shopPick);
                GUI.enabled = shopPick != "" && System.Array.IndexOf(ShopSell, shopPick) >= 0;
                if (Btn("팔기")) Shop(net, false, shopPick);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
                if (Btn("상점 닫기")) world.CloseVendor(me);
                GUILayout.Space(6f);
            }
            if (me.ActiveTrainer != null && !me.Ghost)
            {
                GUILayout.Label("훈련  5G / +1  상한 30");
                for (int i = 0; i < Trainable.Length; i += 3)
                {
                    GUILayout.BeginHorizontal();
                    for (int c = 0; c < 3 && i + c < Trainable.Length; c++)
                    {
                        var id = Trainable[i + c];
                        if (Btn(SkillNames.KoreanOf(id)))
                            Train(net, id);
                    }
                    GUILayout.EndHorizontal();
                }
                if (Btn("훈련 닫기"))
                    world.CloseTrainer(me);
                GUILayout.Space(6f);
            }
            var forge = OfflineWorld.FindStation("Forge");
            if (InRange(me, forge)) { Station(world, me, net, forge, SkillId.Blacksmithing); GUILayout.Space(6f); }
            var carpenter = OfflineWorld.FindStation("Carpenter");
            if (InRange(me, carpenter)) { Station(world, me, net, carpenter, SkillId.Carpentry); GUILayout.Space(6f); }
            var mortar = OfflineWorld.FindStation("Mortar");
            if (InRange(me, mortar)) { Station(world, me, net, mortar, SkillId.Alchemy); GUILayout.Space(6f); }
            var locked = OfflineWorld.FindCrate("LockedCrate");
            if (InRangeCrate(me, locked))
            {
                GUILayout.Label(locked.Opened ? locked.DisplayName + " (열림)" : locked.DisplayName);
                if (!locked.Opened && Btn("따기"))
                {
                    if (net != null && net.IsClientInitialized) net.RpcPick(locked.gameObject.name);
                    else world.TryPick(me, locked);
                }
                GUILayout.Space(6f);
            }
            var gate = NearestGate(me, Dungeon1.EntranceObject, Dungeon1.ExitObject,
                                   Dungeon2.EntranceObject, Dungeon2.ExitObject,
                                   Dungeon3.EntranceObject, Dungeon3.ExitObject);
            if (gate != null && !me.Ghost)
            {
                GUILayout.Label(gate.DisplayName);
                if (Btn(gate.IsExit ? "나가기" : "들어가기"))
                {
                    if (net != null && net.IsClientInitialized) net.RpcDungeon(gate.gameObject.name);
                    else world.TryDungeon(me, gate);
                }
                GUILayout.Space(6f);
            }
            PanelTrade(world, me, net);
            GUILayout.EndScrollView();
        }

        static readonly string[] ShopBuy =
        {
            ItemCatalog.Pickaxe, ItemCatalog.Hatchet, ItemCatalog.FishingPole,
            ItemCatalog.Bandage, ItemCatalog.Cloth, "resin", ItemCatalog.Lute, ItemCatalog.Lockpick,
        };

        static readonly string[] ShopSell = { "iron_ore", "wood", ItemCatalog.Fish };

        static int Owned(WorldBody me, string id)
        {
            var bag = me != null ? me.GetComponent<InventoryBag>() : null;
            if (bag == null) return 0;
            int n = 0;
            for (int i = 0; i < bag.Items.Count; i++)
                if (bag.Items[i].TemplateId == id) n += bag.Items[i].Amount;
            return n;
        }

        /// <summary>
        /// 제작대 하나 — 그 대장간·목공소·연금대에서 만들 수 있는 제작법을 **원장에서** 뽑아 보여준다.
        /// 조작법은 가방과 같다: 목록에서 고르고, 버튼이 고른 것에 작용하고, 선택이 없으면 비활성.
        /// 재료가 모자라면 줄에 그대로 적는다 — 눌러 보고 나서야 알게 하지 않는다.
        /// </summary>
        void Station(OfflineWorld world, WorldBody me, NetAvatar net, CraftStation station, SkillId skill)
        {
            GUILayout.Label(station.DisplayName);
            var ids = CraftRecipes.CodeDefaults;
            for (int i = 0; i < ids.Length; i++)
            {
                var recipe = CraftRecipes.Find(ids[i].Id);
                if (recipe == null || recipe.Skill != skill)
                    continue;
                int have = Owned(me, recipe.Ingredient);
                string line = (craftPick == recipe.Id ? "▸ " : "   ") + ItemCatalog.DisplayNameOf(recipe.Output) +
                              "  ← " + ItemCatalog.DisplayNameOf(recipe.Ingredient) + " " + have + "/" + recipe.Count +
                              (have < recipe.Count ? "  재료 부족" : "");
                if (Btn(line, ItemStyle(craftPick == recipe.Id)))
                    craftPick = craftPick == recipe.Id ? "" : recipe.Id;
            }
            GUILayout.Label(craftPick == "" ? "고른 것 없음 — 제작법을 눌러 고르세요"
                                            : "고른 것: " + ItemCatalog.DisplayNameOf(CraftRecipes.Find(craftPick).Output));
            GUI.enabled = craftPick != "";
            GUILayout.BeginHorizontal();
            if (Btn("만들기")) CraftAt(net, station, craftPick);
            // 수리 전용 버튼 — 예전에는 「재료가 모자란 제작」이 우연히 수리로 떨어질 때만 수리됐다.
            if (Btn("수리")) RepairAt(net, station);
            GUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        static readonly SkillId[] Trainable =
        {
            SkillId.Swordsmanship, SkillId.Mining, SkillId.Lumberjacking, SkillId.Blacksmithing,
            SkillId.Magery, SkillId.Archery, SkillId.Parrying, SkillId.Meditation,
            SkillId.MagicResist, SkillId.EvaluateIntelligence, SkillId.Fishing, SkillId.Cooking,
            SkillId.Fencing, SkillId.Mace, SkillId.Alchemy, SkillId.Tracking,
            SkillId.Musicianship, SkillId.Peacemaking, SkillId.Provocation, SkillId.Hiding,
            SkillId.Stealth, SkillId.Lockpicking, SkillId.AnimalLore, SkillId.Inscription,
            SkillId.Poisoning, SkillId.DetectHidden, SkillId.Camping, SkillId.Stealing,
        };

        void PanelTrade(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            bool open = false;
            string otherName = "", mineOffer = "", theirs = "";
            bool mineOk = false, theirOk = false;
            if (TradeView.Open && net != null)
            {
                int myId = net.ObjectId;
                bool iAmA = myId == TradeView.IdA;
                bool iAmB = myId == TradeView.IdB;
                if (!iAmA && !iAmB)
                    return;
                open = true;
                otherName = iAmA ? TradeView.NameB : TradeView.NameA;
                mineOffer = iAmA ? TradeView.OfferA : TradeView.OfferB;
                theirs = iAmA ? TradeView.OfferB : TradeView.OfferA;
                mineOk = iAmA ? TradeView.AcceptA : TradeView.AcceptB;
                theirOk = iAmA ? TradeView.AcceptB : TradeView.AcceptA;
            }
            else if (me.Trade != null && (me.Trade.A == me || me.Trade.B == me))
            {
                open = true;
                var other = me.Trade.Other(me);
                otherName = other != null ? other.DisplayName : "?";
                mineOffer = me == me.Trade.A ? me.Trade.OfferA : me.Trade.OfferB;
                theirs = me == me.Trade.A ? me.Trade.OfferB : me.Trade.OfferA;
                mineOk = me == me.Trade.A ? me.Trade.AcceptA : me.Trade.AcceptB;
                theirOk = me == me.Trade.A ? me.Trade.AcceptB : me.Trade.AcceptA;
            }
            if (!open)
                return;
            GUILayout.Label("거래  " + otherName);
            GUILayout.Label("나: " + Label(mineOffer) + (mineOk ? "  수락" : ""));
            GUILayout.Label("상대: " + Label(theirs) + (theirOk ? "  수락" : ""));
            Row3(("광석", () => Offer(net, me, "iron_ore")),
                 ("철검", () => Offer(net, me, "iron_sword")),
                 ("없음", () => Offer(net, me, "")));
            GUILayout.BeginHorizontal();
            if (Btn("수락"))
            {
                if (net != null && net.IsClientInitialized) net.RpcTradeAccept();
                else world.ConfirmTrade(me);
            }
            if (Btn("취소"))
            {
                if (net != null && net.IsClientInitialized) net.RpcTradeCancel();
                else world.CancelTrade(me);
            }
            GUILayout.EndHorizontal();
        }

        // ── 디버그(GM) — 기본 숨김, F1 ────────────────────────────────────────────────
        void PanelGm(OfflineWorld world, WorldBody me)
        {
            GUILayout.Label(PersistDriver.Frozen ? "GM  계정 정지됨" : "GM  (F1로 열고 닫음)");
            Row3(("광장복구", () => world.GmWarpPlaza(me)),
                 ("테스트공간", () => world.GmWarpTest(me)),
                 ("검술+10", () => world.GmSetSkill(me, SkillId.Swordsmanship,
                                                    world.SkillsOf(me).Get(SkillId.Swordsmanship) + 10f)));
            Row3(("곡괭이", () => world.GmGive(me, ItemCatalog.Pickaxe, 1)),
                 ("철검", () => world.GmGive(me, ItemCatalog.IronSword, 1)),
                 ("회수", () => world.GmTake(me, "iron_ore")));
            Row3(("스켈소환", () => world.GmSpawnSkeleton()),
                 ("스켈삭제", () => world.GmDespawnExtra()),
                 ("백업", () => OpLog.Backup()));
            Row3(("원장 다시 읽기", () => ledgerLine = world.GmReloadLedgers()),
                 (PersistDriver.Frozen ? "정지 해제" : "계정 정지", () =>
                 {
                     bool next = !OpLog.IsFrozen(PersistDriver.AccountKey());
                     OpLog.Freeze(PersistDriver.AccountKey(), next);
                     PersistDriver.Frozen = next;
                 }));
            if (ledgerLine != "")
                GUILayout.Label(ledgerLine);
            string[] logs = OpLog.Recent(3);
            GUILayout.Label(logs.Length == 0 ? "(로그 없음)" : logs[logs.Length - 1]);
        }
    }
}
