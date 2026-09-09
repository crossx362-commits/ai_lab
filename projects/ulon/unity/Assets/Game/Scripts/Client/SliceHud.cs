using UnityEngine;
using Ulon.Shared;
using Ulon.Server;

namespace Ulon.Client
{
    public sealed partial class SliceHud : MonoBehaviour
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
        /// <summary>가방에서 고른 항목 — 「무엇에 대해」 착용·주머니 버튼이 작용하는지 화면에 보여야 한다(검수).</summary>
        int bagPick = -1;
        /// <summary>제작대에서 고른 제작법 / 상점에서 고른 물건 — **조작법을 인벤토리와 통일**한다(검수).</summary>
        string craftPick = "";
        string shopPick = "";

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
        /// <summary>패널 높이는 **내용에 맞춘다** — 내용이 위 1/4인데 화면을 위아래로 다 쓰면 안 된다(검수).</summary>
        float panelHeight = 200f;
        Rect PanelRect
        {
            get
            {
                float max = Screen.height - 148f;
                float h = Mathf.Clamp(panelHeight, 120f, max);
                return new Rect(Screen.width - 318f, Screen.height - 66f - h, PanelW, h);
            }
        }

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
            BeginFrame();
            RegisterOwner(this);                            // HUD도 「그린 자」로 자기 이름을 남긴다
            DrawStatusCard(world, me, net);
            DrawTargetCard(world, me);
            DrawQuickbar(world, me, net);
            DrawTabs(world, me);
            DrawPanel(world, me, net);
        }

        /// <summary>이 프레임에 **실제로 그린 버튼의 라벨** — 「코드가 부른다」와 「화면에 수단이 있다」는 다르다.
        /// 게이트(`SliceSelfCheck.HudReachable`)가 이 목록과 소스를 대조한다.</summary>
        public static readonly System.Collections.Generic.List<string> DrawnControls = new System.Collections.Generic.List<string>();

        /// <summary>이 프레임에 영역을 등록한 컴포넌트의 형 이름 — OnGUI를 그리면서 등록 안 한 것을 잡는다.</summary>
        public static readonly System.Collections.Generic.HashSet<string> DrawnOwners = new System.Collections.Generic.HashSet<string>();

        /// <summary>다른 화면 요소(예: 접속 패널)도 자기 영역을 여기 등록한다 —
        /// 화면 규칙 게이트가 **HUD만 보고 통과**해 버리는 구멍을 막는다(2026-09-07 실측으로 드러났다).
        /// `owner`는 그린 컴포넌트다 — 등록을 빼먹은 OnGUI를 게이트가 이름으로 짚기 위해 받는다.</summary>
        public static void RegisterArea(Rect r, MonoBehaviour owner)
        {
            BeginFrame();
            // IMGUI는 한 프레임에 OnGUI를 **여러 번**(Layout·Repaint) 부른다 — 같은 사각형을 두 번 담으면
            // 겹침 검사가 자기 자신과 겹쳤다고 한다. 프레임 단위로 비우되 같은 사각형은 한 번만 센다.
            if (!DrawnAreas.Contains(r))
                DrawnAreas.Add(r);
            RegisterOwner(owner);
        }

        public static void RegisterOwner(MonoBehaviour owner)
        {
            BeginFrame();
            if (owner != null)
                DrawnOwners.Add(owner.GetType().Name);
        }

        static int drawnFrame = -1;

        /// <summary>세 목록은 **프레임 번호로** 비운다 — 예전처럼 `SliceHud.OnGUI` 첫머리에서 비우면
        /// 그보다 먼저 그린 컴포넌트(접속 패널)의 등록이 지워져 게이트 눈 밖으로 나간다.</summary>
        static void BeginFrame()
        {
            if (drawnFrame == Time.frameCount)
                return;
            drawnFrame = Time.frameCount;
            DrawnAreas.Clear();
            DrawnControls.Clear();
            DrawnOwners.Clear();
        }

        /// <summary>버튼은 **전부 이 함수를 거친다** — 그려진 조작 수단을 남기기 위해서다.
        /// `GUILayout.Button`을 직접 부르면 그 기능은 게이트 눈 밖으로 나간다.</summary>
        static bool Btn(string label, params GUILayoutOption[] options)
        {
            bool hit = GUILayout.Button(label, options);
            DrawnControls.Add(label);
            return hit;
        }

        static bool Btn(string label, GUIStyle style, params GUILayoutOption[] options)
        {
            bool hit = GUILayout.Button(label, style, options);
            DrawnControls.Add(label);
            return hit;
        }

        void Area(Rect r, System.Action body)
        {
            RegisterArea(r, this);
            // 배경이 비쳐 글자가 묻히던 문제(검수 2026-09-07) — 어두운 판을 깔고 그 위에 상자를 얹는다.
            var prev = GUI.color;
            GUI.color = new Color(0.06f, 0.07f, 0.09f, 0.92f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
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
        void DrawTargetCard(OfflineWorld world, WorldBody me)
        {
            var target = me != null ? me.Selected : null;
            string msg = Msg(world, world.Player);
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

        static string Msg(OfflineWorld world, WorldBody me)
        {
            // 온라인은 TargetRpc로 받은 안내만 본다 — 로컬 Last*는 서버 전역이라 클라에선 빈 값이다.
            var net = me != null ? me.GetComponent<NetAvatar>() : null;
            if (net != null && net.IsClientInitialized && !string.IsNullOrEmpty(net.ClientHint))
                return net.ClientHint;
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
                if (Btn("붕대")) Bandage(net);
                if (Btn("물약")) Drink(net);
                if (Btn(SkillNames.KoreanOf(SkillId.Meditation))) Meditate(net);
                for (int i = 0; i < QuickSpells.Length; i++)
                    if (book.Knows(QuickSpells[i]) && Btn(SpellNames.KoreanOf(QuickSpells[i])))
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
            if (Btn(on ? "▸" + label : label))
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
            float used = 0f;
            Area(PanelRect, () =>
            {
                GUILayout.BeginVertical();
                switch (panel)
                {
                    case Panel.Bag: PanelBag(world, me, net); break;
                    case Panel.Action: PanelAction(world, me, net); break;
                    case Panel.Skills: PanelSkills(world); break;
                    case Panel.Social: PanelSocial(world, me, net); break;
                    case Panel.Nearby: PanelNearby(world, me, net); break;
                    case Panel.Gm: PanelGm(world, me); break;
                }
                GUILayout.EndVertical();
                if (Event.current.type == EventType.Repaint)
                    used = GUILayoutUtility.GetLastRect().height + 24f;
            });
            if (used > 1f)
                panelHeight = used;
        }

        /// <summary>회복 안내 한 줄 — **화면에 그려지는 그 문자열**. 검사도 이 함수를 부른다
        /// (재는 자와 그리는 자가 둘이면 화면과 검사가 갈린다, 원장).</summary>
        internal static string RecoveryLine(WorldBody me)
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
