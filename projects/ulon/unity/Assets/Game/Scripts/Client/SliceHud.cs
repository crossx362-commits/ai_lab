using UnityEngine;
using Ulon.Shared;
using Ulon.Server;

namespace Ulon.Client
{
    public sealed class SliceHud : MonoBehaviour
    {
        string createName = "";
        int createAppear;
        int createStr = 30;
        int createDex = 25;
        int createInt = 25;
        SkillId createA = SkillId.Swordsmanship;
        SkillId createB = SkillId.Mining;
        SkillId createC = SkillId.Blacksmithing;
        float createAv = 50f;
        float createBv = 30f;
        float createCv = 20f;
        string createError = "";
        bool lookApplied;
        string partyChat = "";
        string keywordSpeech = "";
        string guildNameInput = "";
        bool gmOpen;
        string ledgerLine = "";

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1))
                gmOpen = !gmOpen;
            if (PersistDriver.Creating || lookApplied)
                return;
            var world = OfflineWorld.Instance;
            if (world == null || world.Player == null)
                return;
            OutfitSwap.ApplyLook(world.Player.transform, world.Player.Appearance);
            lookApplied = true;
        }

        // ── 정보 구조(검수 2026-09-07 지시) ────────────────────────────────────────────
        // 상시 표시 : 내 상태 카드(좌상단) · 대상 카드(상단 중앙) · 퀵바(하단 중앙) · 탭(하단 우측)
        // 토글 패널 : 가방 · 행동 · 스킬 · 소셜 · 근처 — **한 번에 하나만**, 화면 우측 가장자리 정렬
        // 디버그    : GM 패널로 격리, 기본 숨김(F1)
        // 화면 규칙 : 중앙 전투 영역(가로 27~73%·세로 18~74%)에는 **어떤 UI도 그리지 않는다.**
        //            겹침은 GUILayout 영역으로 원천 차단한다 — 예전 절대 좌표 배치가 글자를 겹쳐 놨다.
        enum Panel { None, Bag, Action, Skills, Social, Nearby, Gm }

        Panel panel = Panel.None;
        Vector2 skillScroll, bagScroll, nearbyScroll, socialScroll;

        const float CardW = 300f;
        const float PanelW = 308f;
        const float TargetW = 360f;

        /// <summary>화면 규칙 검사용 — 어떤 패널을 열어 두고 찍을지 도구가 정한다(`HudShots`).</summary>
        public void ShowPanel(int index) => panel = (Panel)Mathf.Clamp(index, 0, PanelCount - 1);
        public static int PanelCount => 7;

        /// <summary>디버그 화면(GM·접속)이 열려 있는가 — 다른 디버그 패널이 이 스위치를 함께 본다.</summary>
        public static bool DebugOpen { get; private set; }
        public static string PanelName(int index) => ((Panel)Mathf.Clamp(index, 0, PanelCount - 1)).ToString();

        public static Rect CombatZone => new Rect(Screen.width * 0.27f, Screen.height * 0.18f,
                                                  Screen.width * 0.46f, Screen.height * 0.56f);

        static Rect StatusRect => new Rect(12f, 12f, CardW, 148f);
        static Rect TargetRect => new Rect((Screen.width - TargetW) * 0.5f, 12f, TargetW, 58f);
        static Rect QuickRect => new Rect((Screen.width - 580f) * 0.5f, Screen.height - 54f, 580f, 42f);
        static Rect TabsRect => new Rect(Screen.width - 318f, Screen.height - 54f, 306f, 42f);
        static Rect PanelRect => new Rect(Screen.width - 318f, 80f, PanelW, Screen.height - 148f);

        /// <summary>이 프레임에 실제로 그린 UI 영역 — 게이트가 겹침·중앙 침범을 재는 데 쓴다.</summary>
        public static readonly System.Collections.Generic.List<Rect> DrawnAreas = new System.Collections.Generic.List<Rect>();

        void OnGUI()
        {
            OfflineWorld world = OfflineWorld.Instance;
            if (world == null)
                return;
            if (PersistDriver.Creating)
            {
                DrawCreate();
                return;
            }
            var me = world.Player;
            if (me == null)
                return;
            var net = me.GetComponent<NetAvatar>();

            DebugOpen = gmOpen || Cli.Has("-ulon-gm");
            DrawnAreas.Clear();
            DrawStatusCard(world, me, net);
            DrawTargetCard(world);
            DrawQuickbar(world, me, net);
            DrawTabs(world, me);
            DrawPanel(world, me, net);
        }

        /// <summary>다른 화면 요소(예: 접속 패널)도 자기 영역을 여기 등록한다 —
        /// 화면 규칙 게이트가 **HUD만 보고 통과**해 버리는 구멍을 막는다(2026-09-07 실측으로 드러났다).</summary>
        public static void RegisterArea(Rect r) => DrawnAreas.Add(r);

        void Area(Rect r, System.Action body)
        {
            DrawnAreas.Add(r);
            GUI.Box(r, GUIContent.none);
            GUILayout.BeginArea(new Rect(r.x + 8f, r.y + 6f, r.width - 16f, r.height - 12f));
            body();
            GUILayout.EndArea();
        }

        // ── 상시: 내 상태 ─────────────────────────────────────────────────────────────
        void DrawStatusCard(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            var st = world.PlayerStats;
            var bag = me.GetComponent<InventoryBag>();
            Area(StatusRect, () =>
            {
                string guildTag = !string.IsNullOrEmpty(me.GuildName) ? "[" + me.GuildName + "]"
                    : (!string.IsNullOrEmpty(GuildView.GuildName) ? "[" + GuildView.GuildName + "]" : "");
                string repTitle = world.ReputationTitleOf(me);
                string title = world.TitleOf(me);
                GUILayout.Label(me.DisplayName + " " + guildTag +
                                (string.IsNullOrEmpty(repTitle) ? "" : "  " + repTitle) +
                                (string.IsNullOrEmpty(title) ? "" : "  " + title));
                Bar("HP", me.Hp, me.MaxHp);
                Bar("MP", me.Mana, me.MaxMana);
                float w = bag != null ? bag.TotalWeight() : 0f;
                int cap = ItemCatalog.CarryCap(st.Str);
                GUILayout.Label("STR " + st.Str + "  DEX " + st.Dex + "  INT " + st.Int +
                                "  G " + me.Gold + "  무게 " + w.ToString("0") + "/" + cap +
                                (bag != null && bag.Overweight(st.Str) ? " 과적" : ""));
                GUILayout.Label(ToolLine(bag) + "  동료 " + world.CountFollowers(me.CharacterId) + "/" + TameResolve.FollowerCap);
                GUILayout.Label(StateLine(world, me));
                string recovery = RecoveryLine(me);
                if (!string.IsNullOrEmpty(recovery))
                    GUILayout.Label(recovery);
            });
        }

        static void Bar(string label, float value, float max)
        {
            var r = GUILayoutUtility.GetRect(10f, 18f);
            float t = max > 0.01f ? Mathf.Clamp01(value / max) : 0f;
            GUI.Box(r, GUIContent.none);
            GUI.Box(new Rect(r.x + 1f, r.y + 1f, (r.width - 2f) * t, r.height - 2f), GUIContent.none);
            GUI.Label(new Rect(r.x + 6f, r.y, r.width - 12f, r.height),
                      label + " " + value.ToString("0") + "/" + max.ToString("0"));
        }

        static string StateLine(OfflineWorld world, WorldBody me)
        {
            string s = NotorietyId.Korean(me.Notoriety) + "  명성 " + me.Fame + "  " +
                       (GuardZone.Contains(me.transform.position.x, me.transform.position.z) ? "마을" : "야외");
            if (me.Ghost) s += "  유령";
            if (me.IsHidden(Time.time)) s += me.CanMoveHidden(Time.time) ? "  잠행" : "  은신";
            if (me.IsCampSafe(Time.time)) s += "  야영";
            return s;
        }

        // ── 상시: 대상 ────────────────────────────────────────────────────────────────
        void DrawTargetCard(OfflineWorld world)
        {
            var target = world.Selected;
            string msg = Msg(world);
            if ((target == null || !target.Alive) && string.IsNullOrEmpty(msg))
                return;
            Area(TargetRect, () =>
            {
                if (target != null && target.Alive)
                {
                    GUILayout.Label(target.DisplayName);
                    Bar("", target.Hp, target.MaxHp);
                }
                else
                    GUILayout.Label("대상 없음");
                if (!string.IsNullOrEmpty(msg))
                    GUILayout.Label(msg);
            });
        }

        static string Msg(OfflineWorld world)
        {
            string s = "";
            if (!string.IsNullOrEmpty(world.LastEvalMessage)) s += world.LastEvalMessage + " ";
            if (!string.IsNullOrEmpty(world.LastTravelMessage)) s += world.LastTravelMessage + " ";
            if (!string.IsNullOrEmpty(world.LastHealRezMessage)) s += world.LastHealRezMessage + " ";
            if (!string.IsNullOrEmpty(world.LastSpeechMessage)) s += world.LastSpeechMessage;
            return s.Trim();
        }

        // ── 상시: 퀵바(주문·자주 쓰는 행동) ───────────────────────────────────────────
        void DrawQuickbar(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            if (me.Ghost)
            {
                Area(QuickRect, () => GUILayout.Label("유령 — 치유사에게 가거나 붕대 부활(행동 패널)"));
                return;
            }
            var book = world.BookOf(me);
            Area(QuickRect, () =>
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("붕대")) Bandage(net);
                if (GUILayout.Button("물약")) Drink(net);
                if (GUILayout.Button(SkillNames.KoreanOf(SkillId.Meditation))) Meditate(net);
                for (int i = 0; i < QuickSpells.Length; i++)
                    if (book.Knows(QuickSpells[i]) && GUILayout.Button(SpellNames.KoreanOf(QuickSpells[i])))
                        Cast(net, QuickSpells[i]);
                GUILayout.EndHorizontal();
            });
        }

        static readonly SpellId[] QuickSpells =
        {
            SpellId.Ember, SpellId.Mend, SpellId.Bolt, SpellId.Cleanse, SpellId.Ward,
            SpellId.Bind, SpellId.Weaken, SpellId.Spark, SpellId.Restore, SpellId.Blink, SpellId.Bless,
        };

        // ── 상시: 탭 ──────────────────────────────────────────────────────────────────
        void DrawTabs(OfflineWorld world, WorldBody me)
        {
            bool nearby = NearbyCount(world, me) > 0;
            Area(TabsRect, () =>
            {
                GUILayout.BeginHorizontal();
                Tab("가방", Panel.Bag);
                Tab("행동", Panel.Action);
                Tab("스킬", Panel.Skills);
                Tab("소셜", Panel.Social);
                Tab(nearby ? "근처 •" : "근처", Panel.Nearby);
                if (gmOpen || Cli.Has("-ulon-gm"))
                    Tab("GM", Panel.Gm);
                GUILayout.EndHorizontal();
            });
        }

        void Tab(string label, Panel which)
        {
            bool on = panel == which;
            if (GUILayout.Button(on ? "▸" + label : label))
                panel = on ? Panel.None : which;
        }

        // ── 토글 패널 ─────────────────────────────────────────────────────────────────
        void DrawPanel(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            if (panel == Panel.None)
                return;
            if (panel == Panel.Gm && !gmOpen && !Cli.Has("-ulon-gm"))
                panel = Panel.None;
            if (panel == Panel.None)
                return;
            Area(PanelRect, () =>
            {
                switch (panel)
                {
                    case Panel.Bag: PanelBag(world, me, net); break;
                    case Panel.Action: PanelAction(world, me, net); break;
                    case Panel.Skills: PanelSkills(world); break;
                    case Panel.Social: PanelSocial(world, me, net); break;
                    case Panel.Nearby: PanelNearby(world, me, net); break;
                    case Panel.Gm: PanelGm(world, me); break;
                }
            });
        }

        void PanelBag(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            var bag = me.GetComponent<InventoryBag>();
            var vault = me.GetComponent<BankVault>();
            GUILayout.Label("가방");
            bagScroll = GUILayout.BeginScrollView(bagScroll);
            if (bag == null || bag.Items.Count == 0)
                GUILayout.Label("비었습니다");
            else
                for (int i = 0; i < bag.Items.Count; i++)
                {
                    var it = bag.Items[i];
                    GUILayout.Label(it.TemplateId + " x" + it.Amount +
                                    (it.Uses > 0 ? " (" + it.Uses + ")" : "") +
                                    (it.Exceptional ? " *" : "") +
                                    (!string.IsNullOrEmpty(it.MakerId) ? " [" + it.MakerId + "]" : ""));
                }
            GUILayout.Space(6f);
            GUILayout.Label(vault != null && vault.Items.Count > 0 ? "은행" : "은행 비움");
            if (vault != null)
                for (int i = 0; i < vault.Items.Count; i++)
                    GUILayout.Label(vault.Items[i].TemplateId + " x" + vault.Items[i].Amount);
            GUILayout.EndScrollView();
            if (me.Ghost)
                return;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("착용")) Equip(net);
            if (GUILayout.Button("해제")) Unequip(net);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("주머니↓")) PouchIn(net);
            if (GUILayout.Button("주머니↑")) PouchOut(net);
            GUILayout.EndHorizontal();
        }

        void PanelAction(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            GUILayout.Label("행동");
            if (me.Ghost)
            {
                if (GUILayout.Button("붕대 부활")) ResurrectBandage(net);
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
            if (GUILayout.Button("말", GUILayout.Width(48f)))
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
                if (GUILayout.Button(items[i].Label))
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
                if (GUILayout.Button(name + " " + val + " " + LockMark(st.GetLock(id))))
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
                    if (GUILayout.Button(SkillNames.KoreanOf(id) + " " + sk.Get(id).ToString("0.0") + " " + LockMark(sk.GetLock(id))))
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
            var party = world.ActiveParty;
            bool netOpen = PartyView.Open && (net == null || !net.IsServerInitialized);
            GUILayout.Label("파티");
            if (party == null && !netOpen)
            {
                if (PartyView.PendingMe)
                {
                    if (GUILayout.Button("수락"))
                    {
                        if (net != null && net.IsClientInitialized) net.RpcPartyAccept();
                        else world.TryPartyAccept(me);
                    }
                    return;
                }
                var pal = GameObject.Find("Companion");
                var palBody = pal != null ? pal.GetComponent<WorldBody>() : null;
                if (palBody != null && GUILayout.Button("동료 초대"))
                {
                    var nob = pal.GetComponent<FishNet.Object.NetworkObject>();
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
            if (GUILayout.Button("말", GUILayout.Width(48f)))
            {
                if (net != null && net.IsClientInitialized) net.RpcPartySay(partyChat);
                else world.TryPartySay(me, partyChat);
                partyChat = "";
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("탈퇴"))
            {
                if (net != null && net.IsClientInitialized) net.RpcPartyLeave();
                else world.TryPartyLeave(me);
            }
            if (party != null && party.Pending == me && GUILayout.Button("수락"))
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
                    if (GUILayout.Button("수락"))
                    {
                        if (net != null && net.IsClientInitialized) net.RpcGuildAccept();
                        else world.TryGuildAccept(me);
                    }
                    return;
                }
                GUILayout.BeginHorizontal();
                guildNameInput = GUILayout.TextField(guildNameInput ?? "");
                if (GUILayout.Button("창설", GUILayout.Width(60f)))
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
            var pal = GameObject.Find("Companion");
            var palBody = pal != null ? pal.GetComponent<WorldBody>() : null;
            GUILayout.BeginHorizontal();
            if (palBody != null && guild != null && guild.Leader == me && GUILayout.Button("동료 초대"))
            {
                var nob = pal.GetComponent<FishNet.Object.NetworkObject>();
                if (net != null && net.IsClientInitialized && nob != null) net.RpcGuildInvite(nob);
                else world.TryGuildInvite(me, palBody);
            }
            if (GUILayout.Button("탈퇴"))
            {
                if (net != null && net.IsClientInitialized) net.RpcGuildLeave();
                else world.TryGuildLeave(me);
            }
            if (guild != null && guild.Pending == me && GUILayout.Button("수락"))
                world.TryGuildAccept(me);
            GUILayout.EndHorizontal();
            var foe = world.Selected;
            if (guild != null && guild.Leader == me && foe != null && foe != me && world.GuildOf(foe) != null
                && world.GuildOf(foe) != guild && string.IsNullOrEmpty(guild.WarWithId) && GUILayout.Button("선전포고"))
            {
                var nob = foe.GetComponent<FishNet.Object.NetworkObject>();
                if (net != null && net.IsClientInitialized && nob != null) net.RpcGuildWarDeclare(nob);
                else world.TryGuildWarDeclare(me, foe);
            }
            if (guild != null && guild.Leader == me && !string.IsNullOrEmpty(guild.WarWithId) && GUILayout.Button("강화"))
            {
                if (net != null && net.IsClientInitialized) net.RpcGuildWarPeace();
                else world.TryGuildWarPeace(me);
            }
        }

        void PanelDuel(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            GUILayout.Label("결투");
            var foe = world.Selected;
            var pal = GameObject.Find("Companion");
            var palBody = pal != null ? pal.GetComponent<WorldBody>() : null;
            var duelFoe = foe != null && foe != me && foe.IsAvatar && !foe.IsEnemy ? foe : palBody;
            if (me.DuelOpponent == null)
            {
                if (duelFoe != null && duelFoe != me && GUILayout.Button("결투 초대"))
                {
                    var nob = duelFoe.GetComponent<FishNet.Object.NetworkObject>();
                    if (net != null && net.IsClientInitialized && nob != null) net.RpcDuelInvite(nob);
                    else world.TryDuelInvite(me, duelFoe);
                }
                bool pendingMe = false;
                var bodies = Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
                for (int i = 0; i < bodies.Length; i++)
                    if (bodies[i] != null && bodies[i].PendingDuel == me) { pendingMe = true; break; }
                if (pendingMe && GUILayout.Button("결투수락"))
                {
                    if (net != null && net.IsClientInitialized) net.RpcDuelAccept();
                    else world.TryDuelAccept(me);
                }
                return;
            }
            GUILayout.Label("결투 중 " + (me.DuelOpponent.DisplayName ?? ""));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("항복"))
            {
                if (net != null && net.IsClientInitialized) net.RpcDuelYield();
                else world.TryDuelYield(me);
            }
            if (GUILayout.Button("종료"))
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
            if (world.ActiveVendor != null) n++;
            if (world.ActiveTrainer != null) n++;
            if (world.ActiveTrade != null || TradeView.Open) n++;
            if (InRange(me, OfflineWorld.FindStation("Forge"))) n++;
            if (InRange(me, OfflineWorld.FindStation("Carpenter"))) n++;
            if (InRange(me, OfflineWorld.FindStation("Mortar"))) n++;
            if (InRangeCrate(me, OfflineWorld.FindCrate("LockedCrate"))) n++;
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

        void PanelNearby(OfflineWorld world, WorldBody me, NetAvatar net)
        {
            GUILayout.Label("근처");
            nearbyScroll = GUILayout.BeginScrollView(nearbyScroll);
            if (NearbyCount(world, me) == 0)
                GUILayout.Label("가까이에 쓸 것이 없습니다.");

            if (world.ActiveVendor != null && !me.Ghost)
            {
                GUILayout.Label("잡화  " + world.ActiveVendor.DisplayName);
                Row3(("곡괭이 25", () => Shop(net, true, ItemCatalog.Pickaxe)),
                     ("도끼 25", () => Shop(net, true, ItemCatalog.Hatchet)),
                     ("낚싯대 25", () => Shop(net, true, ItemCatalog.FishingPole)));
                Row3(("붕대 5", () => Shop(net, true, ItemCatalog.Bandage)),
                     ("천 3", () => Shop(net, true, ItemCatalog.Cloth)),
                     ("시약", () => Shop(net, true, "resin")));
                Row3(("류트 20", () => Shop(net, true, ItemCatalog.Lute)),
                     ("자물쇠 8", () => Shop(net, true, ItemCatalog.Lockpick)),
                     ("광석팔기", () => Shop(net, false, "iron_ore")));
                Row3(("나무팔기", () => Shop(net, false, "wood")),
                     ("생선팔기", () => Shop(net, false, ItemCatalog.Fish)),
                     ("닫기", () => world.CloseVendor()));
                GUILayout.Space(6f);
            }
            if (world.ActiveTrainer != null && !me.Ghost)
            {
                GUILayout.Label("훈련  5G / +1  상한 30");
                for (int i = 0; i < Trainable.Length; i += 3)
                {
                    GUILayout.BeginHorizontal();
                    for (int c = 0; c < 3 && i + c < Trainable.Length; c++)
                    {
                        var id = Trainable[i + c];
                        if (GUILayout.Button(SkillNames.KoreanOf(id)))
                            Train(net, id);
                    }
                    GUILayout.EndHorizontal();
                }
                if (GUILayout.Button("훈련 닫기"))
                    world.CloseTrainer();
                GUILayout.Space(6f);
            }
            var forge = OfflineWorld.FindStation("Forge");
            if (InRange(me, forge))
            {
                GUILayout.Label(forge.DisplayName);
                // 수리 전용 버튼 — 예전에는 「재료가 모자란 제작」이 우연히 수리로 떨어질 때만 수리됐다.
                Row3(("수리", () => RepairAt(net, forge)), ("자물쇠", () => CraftAt(net, forge, "lockpick")));
                GUILayout.Space(6f);
            }
            var carpenter = OfflineWorld.FindStation("Carpenter");
            if (InRange(me, carpenter))
            {
                GUILayout.Label(carpenter.DisplayName);
                Row3(("나무활", () => CraftAt(net, carpenter, "wooden_bow")),
                     ("나무창", () => CraftAt(net, carpenter, "wooden_spear")),
                     ("나무곤봉", () => CraftAt(net, carpenter, "wooden_club")));
                Row3(("류트", () => CraftAt(net, carpenter, "lute")));
                GUILayout.Space(6f);
            }
            var mortar = OfflineWorld.FindStation("Mortar");
            if (InRange(me, mortar))
            {
                GUILayout.Label(mortar.DisplayName);
                Row3(("회복물약", () => CraftAt(net, mortar, "health_potion")));
                GUILayout.Space(6f);
            }
            var locked = OfflineWorld.FindCrate("LockedCrate");
            if (InRangeCrate(me, locked))
            {
                GUILayout.Label(locked.Opened ? locked.DisplayName + " (열림)" : locked.DisplayName);
                if (!locked.Opened && GUILayout.Button("따기"))
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
                if (GUILayout.Button(gate.IsExit ? "나가기" : "들어가기"))
                {
                    if (net != null && net.IsClientInitialized) net.RpcDungeon(gate.gameObject.name);
                    else world.TryDungeon(me, gate);
                }
                GUILayout.Space(6f);
            }
            PanelTrade(world, me, net);
            GUILayout.EndScrollView();
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
            else if (world.ActiveTrade != null && (world.ActiveTrade.A == me || world.ActiveTrade.B == me))
            {
                open = true;
                var other = world.ActiveTrade.Other(me);
                otherName = other != null ? other.DisplayName : "?";
                mineOffer = me == world.ActiveTrade.A ? world.ActiveTrade.OfferA : world.ActiveTrade.OfferB;
                theirs = me == world.ActiveTrade.A ? world.ActiveTrade.OfferB : world.ActiveTrade.OfferA;
                mineOk = me == world.ActiveTrade.A ? world.ActiveTrade.AcceptA : world.ActiveTrade.AcceptB;
                theirOk = me == world.ActiveTrade.A ? world.ActiveTrade.AcceptB : world.ActiveTrade.AcceptA;
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
            if (GUILayout.Button("수락"))
            {
                if (net != null && net.IsClientInitialized) net.RpcTradeAccept();
                else world.ConfirmTrade(me);
            }
            if (GUILayout.Button("취소"))
            {
                if (net != null && net.IsClientInitialized) net.RpcTradeCancel();
                else world.CancelTrade();
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


        static string RecoveryLine(WorldBody me)
        {
            if (me == null)
                return "";
            if (me.Ghost)
            {
                var healer = GameObject.Find("Healer");
                if (healer == null)
                    return "유령 · 치유사에서 부활";
                float hd = Vector3.Distance(me.transform.position, healer.transform.position);
                return "유령 · 치유사 " + Compass(me.transform.position, healer.transform.position) + " " + hd.ToString("0") + "m";
            }
            var corpse = OfflineWorld.FindCorpse(PersistDriver.AccountKey());
            if (corpse == null)
                return "";
            float d = Vector3.Distance(me.transform.position, corpse.transform.position);
            return "시체 " + Compass(me.transform.position, corpse.transform.position) + " " + d.ToString("0") + "m · " + corpse.SecondsLeft.ToString("0") + "초";
        }

        static string Compass(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            d.y = 0f;
            if (d.sqrMagnitude < 0.25f)
                return "여기";
            float ang = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            if (ang < 0f)
                ang += 360f;
            int oct = (int)((ang + 22.5f) / 45f) % 8;
            if (oct == 0) return "북";
            if (oct == 1) return "북동";
            if (oct == 2) return "동";
            if (oct == 3) return "남동";
            if (oct == 4) return "남";
            if (oct == 5) return "남서";
            if (oct == 6) return "서";
            return "북서";
        }

        static bool LockButton(float x, float y, string name, SkillLock state)
        {
            return GUI.Button(new Rect(x, y, 86, 24), name + " " + SkillLockMarks.Glyph(state));
        }

        static string Label(string id) => string.IsNullOrEmpty(id) ? "(없음)" : id;

        static void Offer(NetAvatar net, WorldBody me, string template)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcTradeOffer(template);
            else
                OfflineWorld.Instance?.SetTradeOffer(me, template);
        }

        static void Cast(NetAvatar net, SpellId spell)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcCast((int)spell);
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryCast(OfflineWorld.Instance.Player, spell, OfflineWorld.Instance.Selected);
        }

        static void Mark(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcMark();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryMark(OfflineWorld.Instance.Player);
        }

        static void Recall(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcRecall();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryRecall(OfflineWorld.Instance.Player);
        }

        static void Meditate(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcMeditate();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryMeditate(OfflineWorld.Instance.Player);
        }

        static void Evaluate(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcEvaluate();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryEvaluate(OfflineWorld.Instance.Player, OfflineWorld.Instance.Selected);
        }

        static void Track(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcTrack();
            else if (OfflineWorld.Instance != null)
            {
                var world = OfflineWorld.Instance;
                if (world.Selected != null)
                    world.TryTrack(world.Player, world.Selected);
                else
                    world.TryTrackCorpse(world.Player, OfflineWorld.FindCorpse(world.Player != null ? world.Player.CharacterId : ""));
            }
        }

        static void Lore(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcLore();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryLore(OfflineWorld.Instance.Player, OfflineWorld.Instance.Selected);
        }

        static void Vet(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcVet();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryVet(OfflineWorld.Instance.Player, OfflineWorld.Instance.Selected);
        }

        static void Inscribe(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcInscribe();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryInscribe(OfflineWorld.Instance.Player);
        }

        static void PoisonWeapon(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcPoisonWeapon();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryPoisonWeapon(OfflineWorld.Instance.Player);
        }

        static void UseScroll(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcUseScroll();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryUseScroll(OfflineWorld.Instance.Player, OfflineWorld.Instance.Selected);
        }

        static void PlayLute(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcPlay();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryPlay(OfflineWorld.Instance.Player);
        }

        static void Peace(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcPeace();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryPeace(OfflineWorld.Instance.Player, OfflineWorld.Instance.Selected);
        }

        static void Provoke(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcProvoke();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryProvokeStep(OfflineWorld.Instance.Player);
        }

        static void Hide(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcHide();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryHide(OfflineWorld.Instance.Player);
        }

        static void Stealth(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcStealth();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryStealth(OfflineWorld.Instance.Player);
        }


        static void Camp(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcCamp();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryCamp(OfflineWorld.Instance.Player);
        }

        static void DetectHidden(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcDetectHidden();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryDetectHidden(OfflineWorld.Instance.Player);
        }

        static void Steal(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcSteal();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TrySteal(OfflineWorld.Instance.Player);
        }

        static void ResurrectBandage(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcResurrectBandage();
            else if (OfflineWorld.Instance != null)
            {
                var me = OfflineWorld.Instance.Player;
                WorldBody tgt = OfflineWorld.Instance.Selected;
                if (tgt == null || !tgt.Ghost || !tgt.IsAvatar || tgt == me)
                    tgt = OfflineWorld.NearestGhostAvatar(me);
                OfflineWorld.Instance.TryResurrectBandage(me, tgt);
            }
        }

        static void AcceptCraftOrder(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
            {
                net.RpcAcceptOrder();
                return;
            }
            if (OfflineWorld.Instance == null)
                return;
            OfflineWorld.Instance.TryAcceptOrder(OfflineWorld.Instance.Player);
        }

        static void TurnInCraftOrder(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
            {
                net.RpcTurnInOrder();
                return;
            }
            if (OfflineWorld.Instance == null)
                return;
            OfflineWorld.Instance.TryTurnInOrder(OfflineWorld.Instance.Player);
        }

        static void CurePoison(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcCurePoison();
            else if (OfflineWorld.Instance != null)
            {
                var me = OfflineWorld.Instance.Player;
                WorldBody tgt = OfflineWorld.Instance.Selected;
                if (tgt == null || tgt.IsEnemy || !tgt.Alive || tgt.Ghost)
                    tgt = me;
                OfflineWorld.Instance.TryCurePoison(me, tgt);
            }
        }


        static void PetAttack(NetAvatar net)
        {
            if (OfflineWorld.Instance == null)
                return;
            var me = OfflineWorld.Instance.Player;
            if (me == null)
                return;
            WorldBody pet = null;
            var list = UnityEngine.Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var b = list[i];
                if (b != null && !b.PetStabled && b.OwnerCharacterId == me.CharacterId)
                {
                    pet = b;
                    break;
                }
            }
            WorldBody enemy = OfflineWorld.Instance.Selected;
            if (enemy == null || !enemy.IsEnemy || !enemy.Alive || enemy.IsAvatar)
            {
                enemy = null;
                float best = TameResolve.AttackRange;
                for (int i = 0; i < list.Length; i++)
                {
                    var b = list[i];
                    if (b == null || b == me || !b.IsEnemy || !b.Alive || b.IsAvatar)
                        continue;
                    float d = Vector3.Distance(me.transform.position, b.transform.position);
                    if (d > best)
                        continue;
                    best = d;
                    enemy = b;
                }
            }
            var pno = pet.GetComponent<FishNet.Object.NetworkObject>();
            if (net != null && net.IsClientInitialized && pno != null)
            {
                net.RpcPetAttack(pno, enemy != null ? enemy.GetComponent<FishNet.Object.NetworkObject>() : null);
                return;
            }
            OfflineWorld.Instance.TryPetAttack(me, pet, enemy);
        }


        static void SpeakKeyword(NetAvatar net, string text)
        {
            if (OfflineWorld.Instance == null)
                return;
            var me = OfflineWorld.Instance.Player;
            if (me == null)
                return;
            if (net != null && net.IsClientInitialized)
            {
                net.RpcSpeech(text);
                return;
            }
            OfflineWorld.Instance.TrySpeechKeyword(me, text);
        }

        static void PetCome(NetAvatar net)
        {
            if (OfflineWorld.Instance == null)
                return;
            var me = OfflineWorld.Instance.Player;
            if (me == null)
                return;
            WorldBody pet = null;
            var list = UnityEngine.Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var b = list[i];
                if (b != null && !b.PetStabled && b.OwnerCharacterId == me.CharacterId)
                {
                    pet = b;
                    break;
                }
            }
            if (pet == null)
                return;
            var pno = pet.GetComponent<FishNet.Object.NetworkObject>();
            if (net != null && net.IsClientInitialized && pno != null)
            {
                net.RpcPetCome(pno);
                return;
            }
            OfflineWorld.Instance.TryPetCome(me, pet);
        }

        // 아래 5개는 서버 구현만 있고 클라 호출부가 없어 **플레이어가 쓸 수 없던** 기능이다
        // (2026-09-06 도달 스캔, AssertReachableFeatures). 셀프체크는 OfflineWorld를 직접 불러 통과시키고 있었다.
        static void PetRelease(NetAvatar net)
        {
            if (OfflineWorld.Instance == null)
                return;
            var me = OfflineWorld.Instance.Player;
            if (me == null)
                return;
            WorldBody pet = null;
            var list = UnityEngine.Object.FindObjectsByType<WorldBody>(FindObjectsSortMode.None);
            for (int i = 0; i < list.Length; i++)
            {
                var b = list[i];
                if (b != null && !b.PetStabled && b.OwnerCharacterId == me.CharacterId)
                {
                    pet = b;
                    break;
                }
            }
            if (pet == null)
                return;
            var pno = pet.GetComponent<FishNet.Object.NetworkObject>();
            if (net != null && net.IsClientInitialized && pno != null)
            {
                net.RpcPetRelease(pno);
                return;
            }
            OfflineWorld.Instance.TryPetRelease(me, pet);
        }

        /// <summary>가방에서 착용할 무기를 고른다 — 없으면 아무것도 안 한다.</summary>
        static string EquipCandidate(WorldBody me)
        {
            var bag = me != null ? me.GetComponent<InventoryBag>() : null;
            if (bag == null)
                return "";
            for (int i = 0; i < bag.Items.Count; i++)
            {
                var rec = bag.Items[i];
                if (rec.Amount > 0 && ItemCatalog.IsMeleeWeapon(rec.TemplateId))
                    return rec.TemplateId;
            }
            return "";
        }

        static void Equip(NetAvatar net)
        {
            if (OfflineWorld.Instance == null)
                return;
            string id = EquipCandidate(OfflineWorld.Instance.Player);
            if (string.IsNullOrEmpty(id))
                return;
            if (net != null && net.IsClientInitialized)
                net.RpcEquip(id);
            else
                OfflineWorld.Instance.TryEquip(OfflineWorld.Instance.Player, id);
        }

        static void Unequip(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcUnequip();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryUnequip(OfflineWorld.Instance.Player);
        }

        /// <summary>주머니에 넣을/에서 꺼낼 물건 — 주머니 자신은 넣을 수 없다(중첩 깊이 1).</summary>
        static string PouchCandidate(WorldBody me, bool inPouch)
        {
            var bag = me != null ? me.GetComponent<InventoryBag>() : null;
            if (bag == null)
                return "";
            string pouch = bag.PouchInstanceId();
            if (string.IsNullOrEmpty(pouch))
                return "";
            for (int i = 0; i < bag.Items.Count; i++)
            {
                var rec = bag.Items[i];
                if (rec.Amount <= 0 || ItemCatalog.IsContainer(rec.TemplateId))
                    continue;
                bool inside = rec.ParentContainerId == pouch;
                if (inside == inPouch)
                    return rec.TemplateId;
            }
            return "";
        }

        static void PouchIn(NetAvatar net)
        {
            if (OfflineWorld.Instance == null)
                return;
            string id = PouchCandidate(OfflineWorld.Instance.Player, false);
            if (string.IsNullOrEmpty(id))
                return;
            if (net != null && net.IsClientInitialized)
                net.RpcMoveToPouch(id);
            else
                OfflineWorld.Instance.TryMoveToPouch(OfflineWorld.Instance.Player, id, "");
        }

        static void PouchOut(NetAvatar net)
        {
            if (OfflineWorld.Instance == null)
                return;
            string id = PouchCandidate(OfflineWorld.Instance.Player, true);
            if (string.IsNullOrEmpty(id))
                return;
            if (net != null && net.IsClientInitialized)
                net.RpcTakeFromPouch(id);
            else
                OfflineWorld.Instance.TryTakeFromPouch(OfflineWorld.Instance.Player, id, "");
        }

        static void Pick(NetAvatar net, LockedCrate crate)
        {
            if (crate == null || OfflineWorld.Instance == null)
                return;
            if (net != null && net.IsClientInitialized)
                net.RpcPick(crate.gameObject.name);
            else
                OfflineWorld.Instance.TryPick(OfflineWorld.Instance.Player, crate);
        }

        static void Drink(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcDrink();
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryDrink(OfflineWorld.Instance.Player);
        }

        static void Bandage(NetAvatar net)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcHeal();
            else if (OfflineWorld.Instance != null)
            {
                var me = OfflineWorld.Instance.Player;
                WorldBody tgt = OfflineWorld.Instance.Selected;
                if (tgt != null && tgt.Ghost && tgt.IsAvatar && tgt != me)
                {
                    OfflineWorld.Instance.TryResurrectBandage(me, tgt);
                    return;
                }
                if (tgt == null || tgt.IsEnemy || !tgt.Alive)
                    tgt = me;
                var healed = OfflineWorld.Instance.TryHeal(me, tgt);
                if (healed.Applied && tgt != null)
                    ActionVfx.Play(ActionVfx.Kind.Heal, tgt.transform.position + Vector3.up * 1.0f);
                    ActionSfx.Play(ActionSfx.Kind.Heal, tgt.transform.position + Vector3.up * 1.0f);
            }
        }

        static void Train(NetAvatar net, SkillId skill)
        {
            if (net != null && net.IsClientInitialized)
                net.RpcTrain((int)skill);
            else if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.TryTrain(OfflineWorld.Instance.Player, skill);
        }

        /// <summary>도구가 얼마나 남았는지 — 곡괭이·도끼·낚싯대의 남은 사용 횟수(§18.8 Tool Uses).</summary>
        static string ToolLine(InventoryBag bag)
        {
            if (bag == null)
                return "도구 없음";
            string[] tools = { ItemCatalog.Pickaxe, ItemCatalog.Hatchet, ItemCatalog.FishingPole };
            string[] names = { "곡괭이", "도끼", "낚싯대" };
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < tools.Length; i++)
            {
                int uses = bag.ToolUses(tools[i]);
                if (uses <= 0)
                    continue;
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append(names[i]).Append(' ').Append(uses);
            }
            return sb.Length == 0 ? "도구 없음" : sb.ToString();
        }

        static void RepairAt(NetAvatar net, CraftStation station)
        {
            if (station == null || OfflineWorld.Instance == null)
                return;
            if (net != null && net.IsClientInitialized)
            {
                net.RpcRepair(station.gameObject.name);
                return;
            }
            OfflineWorld.Instance.TryRepair(OfflineWorld.Instance.Player, station);
        }

        static void CraftAt(NetAvatar net, CraftStation station, string recipeId)
        {
            if (station == null || OfflineWorld.Instance == null)
                return;
            if (net != null && net.IsClientInitialized)
            {
                net.RpcCraft(station.gameObject.name, recipeId);
                return;
            }
            var made = OfflineWorld.Instance.TryCraft(OfflineWorld.Instance.Player, station, recipeId);
            if (made.Applied)
                ActionVfx.Play(ActionVfx.Kind.Craft, station.transform.position + Vector3.up * 1.1f);
                ActionSfx.Play(ActionSfx.Kind.Craft, station.transform.position + Vector3.up * 1.1f);
        }

        static void Shop(NetAvatar net, bool buy, string template)
        {
            if (net != null && net.IsClientInitialized)
            {
                if (buy) net.RpcBuy(template);
                else net.RpcSell(template);
                return;
            }
            if (OfflineWorld.Instance == null)
                return;
            if (buy)
                OfflineWorld.Instance.TryBuy(OfflineWorld.Instance.Player, template);
            else
                OfflineWorld.Instance.TrySell(OfflineWorld.Instance.Player, template);
        }

        void DrawCreate()
        {
            GUI.Box(new Rect(16, 16, 460, 520), "");
            GUI.Label(new Rect(28, 24, 430, 22), "캐릭터 생성  (직업 선택 없음)");
            GUI.Label(new Rect(28, 50, 80, 22), "이름");
            createName = GUI.TextField(new Rect(110, 48, 200, 24), createName ?? "");
            GUI.Label(new Rect(28, 80, 80, 22), "외형");
            if (GUI.Button(new Rect(110, 78, 80, 24), createAppear == 0 ? "[기사]" : "기사"))
                createAppear = 0;
            if (GUI.Button(new Rect(196, 78, 90, 24), createAppear == 1 ? "[민머리]" : "민머리"))
                createAppear = 1;
            if (GUI.Button(new Rect(292, 78, 80, 24), createAppear == 2 ? "[야만]" : "야만"))
                createAppear = 2;

            int leftStat = CharacterCreate.StatTotal - createStr - createDex - createInt;
            GUI.Label(new Rect(28, 112, 400, 22), "스탯 총합 " + CharacterCreate.StatTotal + "  남은 " + leftStat);
            DrawStat(28, 136, "STR", ref createStr, leftStat);
            DrawStat(28, 164, "DEX", ref createDex, leftStat);
            DrawStat(28, 192, "INT", ref createInt, leftStat);

            float leftSkill = CharacterCreate.SkillTotal - createAv - createBv - createCv;
            GUI.Label(new Rect(28, 228, 400, 22), "시작 스킬 3개 총합 " + CharacterCreate.SkillTotal + "  남은 " + leftSkill.ToString("0"));
            DrawSkillPick(28, 256, ref createA, ref createAv, createB, createC, leftSkill);
            DrawSkillPick(28, 284, ref createB, ref createBv, createA, createC, leftSkill);
            DrawSkillPick(28, 312, ref createC, ref createCv, createA, createB, leftSkill);

            if (!string.IsNullOrEmpty(createError))
                GUI.Label(new Rect(28, 350, 420, 40), createError);
            if (GUI.Button(new Rect(28, 400, 140, 32), "시작"))
                SubmitCreate();
        }

        void DrawStat(float x, float y, string label, ref int value, int remaining)
        {
            GUI.Label(new Rect(x, y, 50, 22), label);
            if (GUI.Button(new Rect(x + 54, y, 28, 22), "-") && value > CharacterCreate.StatMin)
                value--;
            GUI.Label(new Rect(x + 88, y, 40, 22), value.ToString());
            if (GUI.Button(new Rect(x + 128, y, 28, 22), "+") && remaining > 0 && value < CharacterCreate.StatEachMax)
                value++;
        }

        void DrawSkillPick(float x, float y, ref SkillId id, ref float value, SkillId otherA, SkillId otherB, float remaining)
        {
            if (GUI.Button(new Rect(x, y, 24, 22), "<"))
                id = NextSkill(id, otherA, otherB, -1);
            GUI.Label(new Rect(x + 28, y, 88, 22), SkillNames.KoreanOf(id));
            if (GUI.Button(new Rect(x + 118, y, 24, 22), ">"))
                id = NextSkill(id, otherA, otherB, 1);
            if (GUI.Button(new Rect(x + 150, y, 28, 22), "-") && value > 1f)
                value -= 5f;
            if (value < 1f)
                value = 1f;
            GUI.Label(new Rect(x + 184, y, 40, 22), value.ToString("0"));
            if (GUI.Button(new Rect(x + 224, y, 28, 22), "+") && remaining >= 5f && value + 5f <= CharacterCreate.SkillEachMax)
                value += 5f;
        }

        static SkillId NextSkill(SkillId current, SkillId skipA, SkillId skipB, int dir)
        {
            int n = (int)SkillId.Count;
            int i = (int)current;
            for (int step = 0; step < n; step++)
            {
                i = (i + dir + n) % n;
                var id = (SkillId)i;
                if (id != skipA && id != skipB)
                    return id;
            }
            return current;
        }

        void SubmitCreate()
        {
            var picks = new[] { createA, createB, createC };
            var values = new[] { createAv, createBv, createCv };
            createError = CharacterCreate.Validate(createName, createStr, createDex, createInt, picks, values);
            if (createError != null)
                return;
            var snap = CharacterCreate.Build(PersistDriver.AccountKey(), createName, createAppear, createStr, createDex, createInt, picks, values);
            PersistDriver.Commit(snap);
            var world = OfflineWorld.Instance;
            if (world != null && world.Player != null)
                OutfitSwap.ApplyLook(world.Player.transform, createAppear);
            lookApplied = true;
            createError = "";
        }
        static DungeonGate NearestGate(WorldBody me, params string[] names)
        {
            DungeonGate near = null;
            float best = 0f;
            for (int i = 0; i < names.Length; i++)
            {
                var g = OfflineWorld.FindGate(names[i]);
                if (g == null)
                    continue;
                float d = Vector3.Distance(me.transform.position, g.transform.position);
                if (d > g.InteractRange)
                    continue;
                if (near == null || d < best)
                {
                    near = g;
                    best = d;
                }
            }
            return near;
        }

    }
}
