// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §3 씬 구조 — 10_Title / 20_Lobby(팀·탱크 선택, 맵 투표) / 30_Battle.
//
// 명세는 이걸 **씬 3개**로 그렸지만 여기서는 **한 씬 안의 화면 상태**로 만든다.
// 이유: 이 프로젝트는 씬 파일을 쓰지 않는다(§3-0, `Play` 만 누르면 코드가 전부 만든다).
// 씬을 나누면 지형·탱크를 화면마다 다시 만들어야 하고, 그 순간 "씬 없이 돈다"는 전제가 깨진다.
// 대신 타이틀·선택 화면이 **살아 있는 전장을 배경으로 깔고** 그 위에 UI 를 얹는다 — 로딩이 0 이고,
// 고른 탱크가 뒤에서 바로 바뀌어 보인다.
//
// ⚠️ 자동 검증 모드(-autoshot·-perf·-gallery·-phasecheck·-practice…)는 타이틀을 거치지 않는다.
//    거치게 하면 하네스가 메뉴에서 멈춰 "통과"도 "실패"도 아닌 침묵이 된다(CLAUDE.md: 응답 성공 ≠ 지시 전달 성공).
//
// ⚠️ 색은 전부 `Ui` 팔레트에서 온다. 여기서 새 색을 적지 마라(Ui.cs 머리말).

using System.Collections.Generic;
using UnityEngine;
using Tankfall.Sim;

namespace Tankfall.View
{
    public sealed partial class BattleDemo
    {
        /// <summary>화면 상태. Battle 이 아니면 전투 로직(Update 의 페이즈 머신)이 아예 안 돈다.</summary>
        public enum GameScreen { Title, TankSelect, Setup, Battle, Pause, Result }

        GameScreen _screen = GameScreen.Battle;      // 기본은 Battle — 자동 모드가 메뉴에 걸리지 않게 한다
        int _menuSel;                        // 타이틀 메뉴 커서
        int _pickCursor;                     // 탱크 선택 커서(0..12)
        readonly List<TankKind> _picked = new List<TankKind>();
        int _setupSel;                       // 설정 화면 행 커서

        static readonly string[] TitleMenu = { "전투 시작", "연습장", "조작법", "종료" };
        bool _showHelp;

        // ── 전적(결과 화면용) ──────────────────────────────────
        // "이겼다/졌다"만 주면 왜 이겼는지가 안 남는다. 명중률과 준 피해가 다음 판의 조작을 바꾼다.
        struct TeamStat { public int Shots, Hits, Damage, Taken; }
        TeamStat[] _stat = new TeamStat[2];
        int _shooterTeam = -1;               // 지금 날아가는 포탄의 주인(피해 집계용)
        float _battleClock;                  // 한 판 길이 실측(§11 M3 게이트가 요구한 값)

        // ── 피해 팝업(§8 DamagePopupUI) ────────────────────────
        // 로그 한 줄로만 알려주면 **누가 얼마나 맞았는지**가 화면에서 사라진다. 맞은 자리에 숫자를 띄운다.
        struct Popup { public Vector3 World; public string Text; public Color Col; public float Life; }
        readonly List<Popup> _popups = new List<Popup>();

        void AddPopup(Vector3 world, string text, Color col)
        {
            // ⚠️ 한 발이 여러 명을 맞히면 숫자가 포개져 읽을 수 없다(실제로 그렇게 나왔다).
            //    같은 자리 근처에 이미 떠 있는 팝업 수만큼 위로 띄운다.
            int stack = 0;
            foreach (var p in _popups)
                if ((p.World - world).sqrMagnitude < 12f * 12f) stack++;
            _popups.Add(new Popup
            {
                World = world + Vector3.up * (2.4f + stack * 1.5f),
                Text = text, Col = col, Life = 1.6f,
            });
        }

        void TickPopups(float dt)
        {
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                var p = _popups[i];
                p.Life -= dt;
                p.World += Vector3.up * dt * 3.2f;      // 떠오른다
                if (p.Life <= 0f) _popups.RemoveAt(i); else _popups[i] = p;
            }
        }

        // ══════════════════════════════════════════════════════
        //  입력
        // ══════════════════════════════════════════════════════

        /// <summary>Battle 이 아닐 때의 Update. true 를 돌려주면 전투 로직을 건너뛴다.</summary>
        bool ScreenUpdate()
        {
            // 타이틀·선택 화면은 전장을 천천히 도는 카메라를 배경으로 쓴다.
            // 멈춘 그림이면 뒤에 살아 있는 전장이 있다는 게 안 보인다 — 그게 이 화면 구성의 이유다(머리말).
            if (_screen == GameScreen.Title || _screen == GameScreen.TankSelect || _screen == GameScreen.Setup)
                OrbitCamera(Time.deltaTime);

            switch (_screen)
            {
                case GameScreen.Title: TitleInput(); return true;
                case GameScreen.TankSelect: TankSelectInput(); return true;
                case GameScreen.Setup: SetupInput(); return true;
                case GameScreen.Pause: PauseInput(); return true;
                case GameScreen.Result: ResultInput(); return true;
            }
            return false;
        }

        /// <summary>맵 한가운데를 중심으로 도는 궤도 카메라. 전투 카메라(UpdateCamera)와 섞이지 않게 따로 둔다.</summary>
        void OrbitCamera(float dt)
        {
            if (_cam == null) return;
            _titleYaw += dt * 6f;
            float cx = MapSize * 0.5f, cz = MapSize * 0.5f;
            float gy = _vol != null ? TankGroundProbe.GroundBelow(_vol, cx, cz, 90f) : 0f;
            if (float.IsNegativeInfinity(gy)) gy = 0f;
            var focus = new Vector3(cx, gy + 8f, cz);
            var rot = Quaternion.Euler(16f, _titleYaw, 0f);
            _cam.transform.position = focus + rot * Vector3.back * 96f;
            _cam.transform.LookAt(focus);
            if (_skyDome != null) _skyDome.position = _cam.transform.position;   // 안 따라오면 타이틀에서 하늘 밖으로 나간다
        }
        float _titleYaw = 40f;

        /// <summary>키가 눌렸는가. 메뉴 조작음을 여기서 한 번에 낸다 — 호출부마다 적으면 빠뜨린 곳이 생긴다.</summary>
        static bool Down(KeyCode k)
        {
            if (!Input.GetKeyDown(k)) return false;
            Sfx.Click();
            return true;
        }
        static bool Enter()
        {
            if (!(Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))) return false;
            Sfx.Confirm();
            return true;
        }

        void TitleInput()
        {
            if (_showHelp) { if (Enter() || Down(KeyCode.Escape)) _showHelp = false; return; }
            if (Down(KeyCode.UpArrow) || Down(KeyCode.W)) _menuSel = (_menuSel - 1 + TitleMenu.Length) % TitleMenu.Length;
            if (Down(KeyCode.DownArrow) || Down(KeyCode.S)) _menuSel = (_menuSel + 1) % TitleMenu.Length;
            if (!Enter()) return;
            switch (_menuSel)
            {
                case 0:                                   // 전투 시작 → 탱크 고르기
                    _picked.Clear();
                    foreach (var k in _roster) _picked.Add(k);
                    _pickCursor = 0;
                    _screen = GameScreen.TankSelect;
                    break;
                case 1:                                   // 연습장 — 전투와 같은 탄도·지형을 쓰되 적이 반격하지 않는다(§68)
                    _practice = true;
                    StartBattle();
                    break;
                case 2: _showHelp = true; break;
                case 3: Quit(); break;
            }
        }

        void TankSelectInput()
        {
            if (Down(KeyCode.Escape)) { _screen = GameScreen.Title; return; }
            int n = TankStats.Count;
            if (Down(KeyCode.RightArrow) || Down(KeyCode.D)) _pickCursor = (_pickCursor + 1) % n;
            if (Down(KeyCode.LeftArrow) || Down(KeyCode.A)) _pickCursor = (_pickCursor - 1 + n) % n;
            if (Down(KeyCode.DownArrow) || Down(KeyCode.S)) _pickCursor = (_pickCursor + PickCols) % n;
            if (Down(KeyCode.UpArrow) || Down(KeyCode.W)) _pickCursor = (_pickCursor - PickCols + n) % n;

            if (Down(KeyCode.Space))
            {
                var k = (TankKind)_pickCursor;
                if (_picked.Contains(k)) _picked.Remove(k);
                else if (_picked.Count < 3) _picked.Add(k);
                // ⚠️ 같은 기종 3대를 막지 않는다 — 원작에도 그런 제한이 없고, 막으면 "3종 다 캐논" 실험을 못 한다.
            }
            if ((Down(KeyCode.Return) || Down(KeyCode.KeypadEnter)) && _picked.Count == 3)
            {
                _roster = _picked.ToArray();
                _screen = GameScreen.Setup;
                _setupSel = 0;
            }
        }

        void SetupInput()
        {
            if (Down(KeyCode.Escape)) { _screen = GameScreen.TankSelect; return; }
            if (Down(KeyCode.DownArrow) || Down(KeyCode.S)) _setupSel = (_setupSel + 1) % 4;
            if (Down(KeyCode.UpArrow) || Down(KeyCode.W)) _setupSel = (_setupSel + 3) % 4;

            int dir = (Down(KeyCode.RightArrow) || Down(KeyCode.D)) ? 1 : (Down(KeyCode.LeftArrow) || Down(KeyCode.A)) ? -1 : 0;
            if (dir != 0)
                switch (_setupSel)
                {
                    case 0:   // 맵 — 바꾸면 지형을 다시 만들어야 한다(StartBattle 에서 한다)
                        _map = (MapKind)(((int)_map + dir + 3) % 3);
                        break;
                    case 1: _difficulty = (_difficulty + dir + Difficulties.Length) % Difficulties.Length; break;
                    case 2: _itemSlots = Mathf.Clamp(_itemSlots + dir, 0, 4); break;
                    case 3:   // 날씨: 자동(확률) / 맑음 / 눈
                        int w = _weatherForced == null ? 0 : _weatherForced == Weather.Clear ? 1 : 2;
                        w = (w + dir + 3) % 3;
                        _weatherForced = w == 0 ? (Weather?)null : w == 1 ? Weather.Clear : Weather.Snow;
                        break;
                }

            if (Down(KeyCode.Return) || Down(KeyCode.KeypadEnter)) StartBattle();
        }

        void PauseInput()
        {
            if (Down(KeyCode.Escape) || Enter()) { _screen = GameScreen.Battle; return; }
            if (Down(KeyCode.T)) ToTitle();
            if (Down(KeyCode.Q)) Quit();
        }

        void ResultInput()
        {
            if (Down(KeyCode.R)) { StartBattle(); return; }
            if (Down(KeyCode.T) || Down(KeyCode.Escape)) ToTitle();
            if (Down(KeyCode.Q)) Quit();
        }

        void ToTitle()
        {
            _practice = false;
            _screen = GameScreen.Title;
            _menuSel = 0;
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(0);
#endif
        }

        // ══════════════════════════════════════════════════════
        //  판 시작 — 탱크 선택 확정·재시작이 **전부 이 한 곳**을 쓴다.
        //  (같은 초기화가 두 곳에 살면 반드시 어긋난다 — §2-9-1 교대 순서 버그의 교훈)
        // ══════════════════════════════════════════════════════
        void StartBattle()
        {
            foreach (var u in _units) if (u.Root != null) Destroy(u.Root.gameObject);
            _units.Clear();

            // ⚠️ 날씨 → 지형 → 탱크 순서를 지켜라. 지형 색이 눈 여부를 보고 칠해진다(TerrainPalette).
            SetWeather(_weatherForced ?? (Random.value < SnowChance ? Weather.Snow : Weather.Clear));
            RebuildTerrain();
            SpawnTeams();

            _items.Clear();
            _itemRng = new Rng((uint)Random.Range(1, int.MaxValue));
            if (_itemSlots > 0)
            {
                var roll = new List<ItemKind>();
                foreach (var u in _units) { Items.Roll(ref _itemRng, _itemSlots, roll); _items.Bag(u.Id).AddRange(roll); }
            }

            if (_practice)
                foreach (var u in _units)
                    if (u.Team == 0 && u.Id != 0) u.Root.gameObject.SetActive(false);

            _stat = new TeamStat[2];
            _battleClock = 0f;
            _round = 1;
            _winner = null;
            _popups.Clear();
            _power = 0f; _charging = false; _useSs = false; _niceFlash = null;
            _phase = Phase.Move;
            _phaseTimer = MovePhaseSec;
            RollWind();
            _screen = GameScreen.Battle;
            _log = _practice
                ? "연습장 — Space 로 조준, F2 로 정답 보기"
                : $"전투 개시 — {MapHeightFunction.Name(_map)} · {WeatherName(_weather)} · AI {Difficulties[_difficulty].Name}";
            Debug.Log($"[Tankfall] {_log}");
        }

        // ══════════════════════════════════════════════════════
        //  그리기
        // ══════════════════════════════════════════════════════

        /// <summary>Battle 이 아닌 화면. true 면 전투 HUD 를 그리지 않는다.</summary>
        bool ScreenGUI()
        {
            float W = Screen.width, H = Screen.height;
            switch (_screen)
            {
                case GameScreen.Title: DrawTitle(W, H); return true;
                case GameScreen.TankSelect: DrawTankSelect(W, H); return true;
                case GameScreen.Setup: DrawSetup(W, H); return true;
            }
            return false;   // Pause·Result 는 전투 화면 위에 겹쳐 그린다
        }

        void Scrim(float W, float H, float a = 0.55f) => Ui.Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.03f, 0.05f, a));

        void DrawTitle(float W, float H)
        {
            Scrim(W, H, 0.45f);
            Ui.TextShadow(new Rect(0, H * 0.16f, W, 90f), "TANKFALL", 72, Ui.Ink, TextAnchor.MiddleCenter, true);
            Ui.TextShadow(new Rect(0, H * 0.16f + 88f, W, 26f), "3D 턴제 포격전", 16, Ui.Dim, TextAnchor.MiddleCenter);

            if (_showHelp) { DrawHelp(W, H); return; }

            float y = H * 0.44f;
            for (int i = 0; i < TitleMenu.Length; i++)
            {
                var r = new Rect(W * 0.5f - 140f, y + i * 46f, 280f, 38f);
                bool sel = i == _menuSel;
                if (sel) { Ui.Fill(r, new Color(1f, 0.72f, 0.25f, 0.18f)); Ui.Frame(r, Ui.Power, 2f); }
                else Ui.Frame(r, Ui.Border);
                Ui.Text(r, TitleMenu[i], sel ? 19 : 17, sel ? Ui.Ink : Ui.Dim, TextAnchor.MiddleCenter, sel);
            }
            Ui.TextShadow(new Rect(0, H - 44f, W, 20f), "위아래 = 고르기   Enter = 확인", 12, Ui.Dim, TextAnchor.MiddleCenter);
        }

        void DrawHelp(float W, float H)
        {
            string[,] keys =
            {
                { "WASD", "이동 (MOVE 페이즈, 이동 게이지 소비)" },
                { "방향키 좌우 / 상하", "포탑 회전 / 포신 각도" },
                { "Space", "길게 눌러 파워 차징 → 놓으면 발사" },
                { "Q / E", "나이스샷 표시점 옮기기 (맞춰 멈추면 SS 포인트)" },
                { "1 / 2 / 3 / 4", "일반탄 / 특수탄 / SS / 궁극기(§49)" },
                { "[ ] + Enter", "아이템 고르고 쓰기" },
                { "우클릭 드래그 / 휠", "카메라 회전 / 줌" },
                { "F1 / Esc", "AI 난이도 / 일시정지" },
            };
            // ⚠️ 높이를 행 수에서 계산한다. 250 고정이었을 때 8행째(F1/Esc)가 "닫기" 안내와 겹쳤다 —
            //    행을 늘릴 때마다 재발할 조건이라 고정값을 없앤다.
            float panelH = 16f + keys.GetLength(0) * 28f + 34f;
            var r = new Rect(W * 0.5f - 300f, H * 0.36f, 600f, panelH);
            Ui.Box(r);
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                Ui.Text(new Rect(r.x + 20f, r.y + 16f + i * 28f, 200f, 24f), keys[i, 0], 13, Ui.Power, TextAnchor.MiddleLeft, true);
                Ui.Text(new Rect(r.x + 230f, r.y + 16f + i * 28f, 350f, 24f), keys[i, 1], 13, Ui.Ink);
            }
            Ui.Text(new Rect(r.x, r.yMax - 26f, r.width, 20f), "Enter / Esc 로 닫기", 11, Ui.Dim, TextAnchor.MiddleCenter);
        }

        // ── 탱크 선택(§3 20_Lobby) ─────────────────────────────
        const int PickCols = 5;

        void DrawTankSelect(float W, float H)
        {
            Scrim(W, H, 0.62f);
            Ui.TextShadow(new Rect(0, 18f, W, 30f), "탱크 3대를 고른다", 22, Ui.Ink, TextAnchor.MiddleCenter, true);
            Ui.TextShadow(new Rect(0, 48f, W, 20f),
                          "방향키 = 이동   Space = 선택/해제   Enter = 확정   Esc = 뒤로", 12, Ui.Dim, TextAnchor.MiddleCenter);

            int n = TankStats.Count;
            int rows = (n + PickCols - 1) / PickCols;
            float cw = Mathf.Min(196f, (W - 80f) / PickCols), ch = Mathf.Min(116f, (H - 250f) / rows);
            float gx = W * 0.5f - cw * PickCols * 0.5f, gy = 80f;

            for (int i = 0; i < n; i++)
            {
                var k = (TankKind)i;
                var st = TankStats.Get(k);
                var r = new Rect(gx + (i % PickCols) * cw + 4f, gy + (i / PickCols) * ch + 4f, cw - 8f, ch - 8f);
                bool cur = i == _pickCursor;
                int pickIdx = _picked.IndexOf(k);

                Ui.Fill(r, pickIdx >= 0 ? new Color(0.15f, 0.48f, 0.98f, 0.20f) : Ui.Panel);
                Ui.Frame(r, cur ? Ui.Power : pickIdx >= 0 ? Ui.Ally : Ui.Border, cur ? 2f : 1f);

                // 종별 차체색 — 화면의 탱크와 같은 색이라야 "이게 그놈"이 읽힌다(ProceduralTank.BodyColor 가 단일 소스)
                Ui.Fill(new Rect(r.x + 6f, r.y + 6f, 10f, 10f), TankShape.BodyColor(k));
                Ui.Text(new Rect(r.x + 22f, r.y + 2f, r.width - 28f, 18f), st.Name, 13, Ui.Ink, TextAnchor.MiddleLeft, true);
                Ui.Text(new Rect(r.x + 22f, r.y + 18f, r.width - 28f, 14f),
                        TankStats.EraName(TankStats.EraOf(k)), 10, Ui.Dim);

                // 스탯 막대 4줄. 전부 같은 기준으로 정규화해야 종 사이 비교가 성립한다.
                StatRow(r, 0, "체력", st.Hp / 1200f, Ui.Good);
                StatRow(r, 1, "방어", st.Defense / 130f, Ui.Gauge);
                StatRow(r, 2, "사거리", st.MaxRange / 260f, Ui.Power);
                StatRow(r, 3, "속도", 1f - (st.Delay - 520f) / 80f, Ui.Warn);   // 딜레이가 짧을수록 자주 쏜다

                if (pickIdx >= 0)
                {
                    var badge = new Rect(r.xMax - 22f, r.y + 4f, 18f, 18f);
                    Ui.Fill(badge, Ui.Ally);
                    Ui.Text(badge, $"{pickIdx + 1}", 12, Color.black, TextAnchor.MiddleCenter, true);
                }
            }

            // 고른 것 + 특수탄 설명
            var bar = new Rect(W * 0.5f - 330f, H - 122f, 660f, 78f);
            Ui.Box(bar);
            Ui.Text(new Rect(bar.x + 14f, bar.y + 6f, 200f, 18f), $"고른 탱크 {_picked.Count}/3", 12, Ui.Dim, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < 3; i++)
            {
                var slot = new Rect(bar.x + 14f + i * 150f, bar.y + 26f, 142f, 42f);
                Ui.Fill(slot, Ui.Slot); Ui.Frame(slot, Ui.Border);
                if (i < _picked.Count)
                {
                    var st = TankStats.Get(_picked[i]);
                    Ui.Fill(new Rect(slot.x + 5f, slot.y + 5f, 8f, 32f), TankShape.BodyColor(_picked[i]));
                    Ui.Text(new Rect(slot.x + 18f, slot.y + 3f, 120f, 18f), st.Name, 13, Ui.Ink, TextAnchor.MiddleLeft, true);
                    Ui.Text(new Rect(slot.x + 18f, slot.y + 21f, 120f, 16f), st.SpecialName, 10, Ui.Warn);
                }
                else Ui.Text(slot, "비어 있음", 11, Ui.Dim, TextAnchor.MiddleCenter);
            }

            // 커서가 가리키는 기종의 자세한 값
            var cs = TankStats.Get((TankKind)_pickCursor);
            Ui.Text(new Rect(bar.x + 470f, bar.y + 6f, 180f, 18f), cs.Name, 13, Ui.Power, TextAnchor.MiddleLeft, true);
            Ui.Text(new Rect(bar.x + 470f, bar.y + 24f, 180f, 16f),
                    $"각도 {cs.MinPitch:F0}~{cs.MaxPitch:F0}°  딜레이 {cs.Delay}", 10, Ui.Dim);
            Ui.Text(new Rect(bar.x + 470f, bar.y + 40f, 180f, 16f),
                    $"특수탄 {cs.SpecialName}", 10, Ui.Warn);
            // ⚠️ `SpBlast`/`SpBase` 는 **배율**이다 — 그대로 찍으면 전 기종이 "1" 로 보인다(실제로 그렇게 나왔다).
            //    실제 수치는 탄종을 적용한 `TankStats.For` 만 안다.
            var spSt = TankStats.For((TankKind)_pickCursor, ShellKind.Special, 1f, Weather.Clear);
            Ui.Text(new Rect(bar.x + 470f, bar.y + 56f, 180f, 16f),
                    $"폭발 {spSt.BlastRadius:F1}m  피해 {spSt.BaseDamage:F0}", 10, Ui.Dim);

            if (_picked.Count == 3)
                Ui.TextShadow(new Rect(0, H - 36f, W, 22f), "Enter = 전투 설정으로", 14, Ui.Mark, TextAnchor.MiddleCenter, true);
        }

        void StatRow(Rect card, int row, string label, float frac, Color col)
        {
            float y = card.y + 36f + row * 15f;
            Ui.Text(new Rect(card.x + 8f, y - 2f, 40f, 14f), label, 9, Ui.Dim);
            var b = new Rect(card.x + 48f, y + 2f, card.width - 58f, 7f);
            Ui.Bar(b, Mathf.Clamp01(frac), col, null, null);
        }

        // ── 전투 설정(맵·난이도·아이템·날씨) ────────────────────
        void DrawSetup(float W, float H)
        {
            Scrim(W, H, 0.62f);
            Ui.TextShadow(new Rect(0, H * 0.18f, W, 30f), "전투 설정", 22, Ui.Ink, TextAnchor.MiddleCenter, true);

            string weather = _weatherForced == null ? "자동 (25% 눈)" : _weatherForced == Weather.Clear ? "맑음" : "눈";
            var rows = new[]
            {
                ("맵", MapHeightFunction.Name(_map), "지형이 사거리·엄폐를 바꾼다"),
                ("AI 난이도", Difficulties[_difficulty].Name, "조준 오차 — 사다리 검증됨(§2-9-8)"),
                ("아이템", _itemSlots == 0 ? "없음" : $"{_itemSlots}개", "판 시작에 무작위로 받는다"),
                ("날씨", weather, "눈이면 포세이돈이 강해진다"),
            };

            float y = H * 0.30f;
            for (int i = 0; i < rows.Length; i++)
            {
                var r = new Rect(W * 0.5f - 250f, y + i * 48f, 500f, 40f);
                bool sel = i == _setupSel;
                Ui.Fill(r, sel ? new Color(1f, 0.72f, 0.25f, 0.14f) : Ui.Panel);
                Ui.Frame(r, sel ? Ui.Power : Ui.Border, sel ? 2f : 1f);
                Ui.Text(new Rect(r.x + 16f, r.y, 150f, r.height), rows[i].Item1, 14, sel ? Ui.Ink : Ui.Dim, TextAnchor.MiddleLeft, sel);
                Ui.Text(new Rect(r.x + 160f, r.y, 180f, r.height), (sel ? "< " : "  ") + rows[i].Item2 + (sel ? " >" : ""),
                        14, sel ? Ui.Power : Ui.Ink, TextAnchor.MiddleCenter, sel);
                Ui.Text(new Rect(r.x + 350f, r.y, 140f, r.height), rows[i].Item3, 9, Ui.Dim);
            }

            var team = new Rect(W * 0.5f - 250f, y + rows.Length * 48f + 12f, 500f, 44f);
            Ui.Box(team);
            Ui.Text(new Rect(team.x + 14f, team.y, 90f, team.height), "내 팀", 12, Ui.Ally, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < _roster.Length; i++)
            {
                Ui.Fill(new Rect(team.x + 86f + i * 130f, team.y + 12f, 8f, 20f), TankShape.BodyColor(_roster[i]));
                Ui.Text(new Rect(team.x + 100f + i * 130f, team.y, 120f, team.height), TankStats.Get(_roster[i]).Name, 12, Ui.Ink);
            }

            Ui.TextShadow(new Rect(0, H - 60f, W, 22f), "Enter = 전투 시작", 15, Ui.Mark, TextAnchor.MiddleCenter, true);
            Ui.TextShadow(new Rect(0, H - 38f, W, 20f), "좌우 = 바꾸기   위아래 = 이동   Esc = 탱크 다시 고르기", 11, Ui.Dim, TextAnchor.MiddleCenter);
        }

        // ── 일시정지 ───────────────────────────────────────────
        void DrawPause(float W, float H)
        {
            Scrim(W, H, 0.60f);
            Ui.TextShadow(new Rect(0, H * 0.34f, W, 50f), "일시정지", 34, Ui.Ink, TextAnchor.MiddleCenter, true);
            string[] lines = { "Esc / Enter — 계속", "T — 타이틀로", "Q — 종료" };
            for (int i = 0; i < lines.Length; i++)
                Ui.TextShadow(new Rect(0, H * 0.34f + 60f + i * 28f, W, 24f), lines[i], 15, Ui.Dim, TextAnchor.MiddleCenter);
        }

        // ── 결과(§11 M3 게이트: 한 판 길이 실측) ────────────────
        void DrawResult(float W, float H)
        {
            Scrim(W, H, 0.66f);
            bool win = _winner != null && _winner.Contains("아군");
            Ui.TextShadow(new Rect(0, H * 0.16f, W, 64f), win ? "승리" : "패배", 52, win ? Ui.Ally : Ui.Enemy, TextAnchor.MiddleCenter, true);
            Ui.TextShadow(new Rect(0, H * 0.16f + 62f, W, 24f), _winner ?? "", 14, Ui.Dim, TextAnchor.MiddleCenter);

            var labels = new[] { "발사", "명중", "명중률", "준 피해", "생존" };
            // ⚠️ 높이는 행 수에서 계산한다(190 고정이었을 때 "생존" 행과 하단 캡션이 겹쳤다).
            var r = new Rect(W * 0.5f - 290f, H * 0.33f, 580f, 36f + labels.Length * 28f + 36f);
            Ui.Box(r);

            // 헤더
            Ui.Text(new Rect(r.x + 180f, r.y + 8f, 180f, 20f), "아군", 14, Ui.Ally, TextAnchor.MiddleCenter, true);
            Ui.Text(new Rect(r.x + 380f, r.y + 8f, 180f, 20f), "적군", 14, Ui.Enemy, TextAnchor.MiddleCenter, true);

            for (int i = 0; i < labels.Length; i++)
            {
                float y = r.y + 36f + i * 28f;
                Ui.Text(new Rect(r.x + 20f, y, 150f, 24f), labels[i], 12, Ui.Dim);
                for (int t = 0; t < 2; t++)
                {
                    string v;
                    switch (i)
                    {
                        case 0: v = $"{_stat[t].Shots}"; break;
                        case 1: v = $"{_stat[t].Hits}"; break;
                        case 2: v = _stat[t].Shots > 0 ? $"{_stat[t].Hits * 100f / _stat[t].Shots:F0}%" : "-"; break;
                        case 3: v = $"{_stat[t].Damage}"; break;
                        default:
                            int alive = 0; foreach (var u in _units) if (u.Team == t && u.Alive) alive++;
                            v = $"{alive}/3"; break;
                    }
                    Ui.Text(new Rect(r.x + 180f + t * 200f, y, 180f, 24f), v, 14,
                            t == 0 ? Ui.Ally : Ui.Enemy, TextAnchor.MiddleCenter, true);
                }
            }

            Ui.Text(new Rect(r.x + 20f, r.yMax - 26f, r.width - 40f, 20f),
                    $"라운드 {_round}   ·   한 판 {_battleClock / 60f:F1}분   ·   {MapHeightFunction.Name(_map)} · {WeatherName(_weather)} · AI {Difficulties[_difficulty].Name}",
                    11, Ui.Dim, TextAnchor.MiddleCenter);

            Ui.TextShadow(new Rect(0, r.yMax + 22f, W, 24f), "R — 다시   T — 타이틀   Q — 종료", 15, Ui.Ink, TextAnchor.MiddleCenter, true);
        }

        // ══════════════════════════════════════════════════════
        //  전투 중 추가 위젯
        // ══════════════════════════════════════════════════════

        /// <summary>피해 숫자를 맞은 자리에 띄운다. 카메라 뒤는 그리지 않는다(뒤집혀 나온다).</summary>
        void DrawPopups()
        {
            if (_cam == null) return;
            foreach (var p in _popups)
            {
                var sp = _cam.WorldToScreenPoint(p.World);
                if (sp.z <= 0f) continue;
                float a = Mathf.Clamp01(p.Life / 1.6f);
                var col = new Color(p.Col.r, p.Col.g, p.Col.b, a);
                Ui.TextShadow(new Rect(sp.x - 60f, Screen.height - sp.y - 12f, 120f, 24f),
                              p.Text, 16, col, TextAnchor.MiddleCenter, true);
            }
        }

        /// <summary>
        /// 미니맵(§59). 3D 라 화면 밖의 적이 어디 있는지가 안 보인다 — 탑다운 점으로 준다.
        /// ⚠️ 지형 높이는 그리지 않는다. 고저차를 여기서 읽게 하면 §58(탄착 예측 금지)의 우회로가 된다.
        /// </summary>
        void DrawMiniMap(float W, float H)
        {
            const float S = 132f;
            var r = new Rect(W - S - 10f, H - S - 128f, S, S);
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 6f, r.y + 2f, 80f, 14f), "미니맵", 9, Ui.Dim);

            foreach (var c in _supply.Crates)
            {
                var p = MapToMini(r, c.X, c.Z);
                Ui.Fill(new Rect(p.x - 2f, p.y - 2f, 4f, 4f), Ui.Warn);
            }
            foreach (var u in _units)
            {
                if (!u.Alive) continue;
                var p = MapToMini(r, u.Pos.x, u.Pos.z);
                bool cur = u == Current;
                var col = u.Team == 0 ? Ui.Ally : Ui.Enemy;
                if (cur) Ui.Frame(new Rect(p.x - 6f, p.y - 6f, 12f, 12f), Ui.Ink);
                Ui.Fill(new Rect(p.x - 3f, p.y - 3f, 6f, 6f), col);
            }
        }

        Vector2 MapToMini(Rect r, float x, float z)
            => new Vector2(r.x + Mathf.Clamp01(x / MapSize) * r.width,
                           r.yMax - Mathf.Clamp01(z / MapSize) * r.height);   // z+ 가 위로 가게 뒤집는다

        /// <summary>
        /// 턴 순서(§2-9 딜레이제). 교대가 아니라 **누적 딜레이가 작은 쪽이 먼저**라
        /// 다음이 누군지 안 보이면 규칙이 있는 줄도 모른다.
        /// </summary>
        void DrawTurnOrder(float W)
        {
            if (_order == null || _units.Count == 0) return;
            var alive = new List<Unit>();
            foreach (var u in _units) if (u.Alive) alive.Add(u);
            alive.Sort((a, b) =>
            {
                int d = _order.Accumulated(a.Id).CompareTo(_order.Accumulated(b.Id));
                return d != 0 ? d : a.Id.CompareTo(b.Id);      // 동률은 등록 순서(§2-9-1) — 화면도 같은 규칙을 보여야 한다
            });

            var r = new Rect(8f, 8f + 12f + 26f * 6f + 19f + 6f, 250f, 26f + alive.Count * 18f);
            Ui.Box(r);
            Ui.Text(new Rect(r.x + 8f, r.y + 3f, 200f, 16f), "턴 순서 <size=9>(누적 딜레이)</size>", 10, Ui.Dim, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < alive.Count; i++)
            {
                var u = alive[i];
                float y = r.y + 21f + i * 18f;
                bool cur = u == Current;
                if (cur) Ui.Fill(new Rect(r.x + 4f, y, r.width - 8f, 17f), new Color(1f, 1f, 1f, 0.08f));
                Ui.Fill(new Rect(r.x + 8f, y + 5f, 3f, 8f), u.Team == 0 ? Ui.Ally : Ui.Enemy);
                Ui.Text(new Rect(r.x + 16f, y, 130f, 17f),
                        $"{i + 1}. {TankStats.Get(u.Kind).Name}", 11, cur ? Ui.Ink : Ui.Dim, TextAnchor.MiddleLeft, cur);
                Ui.Text(new Rect(r.xMax - 60f, y, 52f, 17f), $"{_order.Accumulated(u.Id)}", 10, Ui.Dim, TextAnchor.MiddleRight);
            }
        }

        /// <summary>상태이상(독·화상·속박). 아이콘이 없으면 "왜 못 움직이지"가 화면에 안 남는다.</summary>
        void DrawStatusIcons(Unit u, Rect under)
        {
            float x = under.x;
            if (!_status.CanMove(u.Id)) { StatusChip(ref x, under.y, "속박", Ui.Bad); }
            if (_items.HasShield(u.Id)) { StatusChip(ref x, under.y, "실드", Ui.Gauge); }
        }

        void StatusChip(ref float x, float y, string s, Color c)
        {
            var r = new Rect(x, y, 42f, 16f);
            Ui.Fill(r, new Color(c.r, c.g, c.b, 0.20f));
            Ui.Frame(r, c);
            Ui.Text(r, s, 10, c, TextAnchor.MiddleCenter, true);
            x += 46f;
        }

        // ══════════════════════════════════════════════════════
        //  -uiselftest — 화면을 전부 돌며 찍고, **그려졌는지까지** 검사한다
        //
        //  ⚠️ 네거티브 컨트롤이 핵심이다. 단계마다 같은 장면을 두 장 찍는다:
        //     (a) HUD 켜고 (b) HUD 끄고. UI 가 실제로 그려졌다면 두 장의 픽셀이 눈에 띄게 달라야 한다.
        //     안 달라지면 "찍혔지만 아무것도 안 그려진" 상태다 — 통과가 공짜가 아님을 이걸로 보장한다.
        //     (2026-09-17 사고: 예전 하네스는 파일 존재만 셌고 -nographics 의 **새까만 PNG 8장**을
        //      "검증 8/8 확인" 으로 통과시켰다. 같은 실수를 구조적으로 못 하게 만든 장치다.)
        // ══════════════════════════════════════════════════════
        bool _uiSelfTest;

        // ── -shellcheck : 탱크별 탄이 **정말 다 다른가**를 기계로 확인한다 ──────────
        //
        // 오너 지시(2026-09-17) "탱크별로 발사하는거 다 확인해".
        // 눈으로 13종을 하나씩 띄워 보는 건 놓치기 쉽다 — 실루엣이 비슷하면 사람은 "다르다"고 느껴버린다.
        // 그래서 **정점 수와 무게중심**을 재서 같은 모양이 있으면 실패로 만든다.
        // (네거티브 컨트롤: 두 기종을 같은 case 로 묶으면 이 검사가 바로 빨간불을 낸다.)
        bool _shellCheck;

        // ── -shellgallery : 13종 탄·자취·폭발을 **나란히 놓고 눈으로** 확인 ──────────
        //
        // 오너 지시(2026-09-17) "미사일 모양도 탱크 전체 체크 / 이펙트도 전체 체크".
        // `-shellcheck` 는 모양이 서로 다른지를 **기계로** 보증하지만, 다르다고 해서 잘 보이는 건 아니다.
        // 나란히 놓고 봐야 "13종이 화면에서 갈리는가"를 사람이 판정할 수 있다(§9 갤러리와 같은 이유 —
        // 각 종을 따로 보면 다 탱크처럼 보여서 차이를 과대평가하게 된다).
        bool _shellGallery;
        int _galStep, _galFrame;
        readonly List<Transform> _galShells = new List<Transform>();

        static readonly string[] GalSteps = { "탄모양_1번탄", "탄모양_2번탄", "자취_전체", "폭발_2번탄", "폭발_1번탄" };

        /// <summary>몇 프레임 뒤에 찍을지. 자취가 줄로 남으려면 탄이 그만큼 움직여야 한다.</summary>
        int GalShotFrame => _galStep == 2 ? 22 : _galStep >= 3 ? 6 : 3;

        void BuildShellRow(ShellKind shellKind)
        {
            foreach (var t in _galShells) if (t != null) Destroy(t.gameObject);
            _galShells.Clear();

            // 13종을 한 줄로. 카메라는 정면에서 전부를 담는다.
            for (int i = 0; i < TankStats.Count; i++)
            {
                var kind = (TankKind)i;
                var go = new GameObject($"Gal_{kind}");
                go.AddComponent<MeshFilter>().sharedMesh = ProceduralTank.Shell(kind, shellKind);
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = MakeMat(ProceduralTank.ShellColor(kind, shellKind), 0.4f);
                go.transform.position = GalSlot(i);
                go.transform.rotation = Quaternion.Euler(0f, 210f, 0f);   // 옆·앞이 같이 보이는 각
                go.transform.localScale = Vector3.one * 5.5f;             // 탄은 작아서 그대로면 안 보인다
                _galShells.Add(go.transform);
            }
        }

        const int GalCols = 5;

        Vector3 GalSlot(int i)
        {
            float cx = MapSize * 0.5f, cz = MapSize * 0.5f;
            return new Vector3(cx - 24f + (i % GalCols) * 12f, 52f - (i / GalCols) * 11f, cz);
        }

        void GalleryCamera()
        {
            if (_cam == null) return;
            _cam.transform.position = new Vector3(MapSize * 0.5f, 40f, MapSize * 0.5f - 46f);
            _cam.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            if (_skyDome != null) _skyDome.position = _cam.transform.position;
        }

        /// <summary>갤러리 전용 라벨. 이름이 없으면 "13종이 다르다"를 확인할 수가 없다.</summary>
        void DrawGalleryLabels()
        {
            if (_cam == null) return;
            Ui.TextShadow(new Rect(0f, 14f, Screen.width, 30f),
                          $"TANKFALL 갤러리 — {(_galStep < GalSteps.Length ? GalSteps[_galStep] : "")}",
                          20, Ui.Ink, TextAnchor.MiddleCenter, true);

            for (int i = 0; i < TankStats.Count; i++)
            {
                var sp = _cam.WorldToScreenPoint(GalSlot(i));
                if (sp.z <= 0f) continue;
                var r = new Rect(sp.x - 70f, Screen.height - sp.y + 26f, 140f, 20f);
                Ui.TextShadow(r, TankStats.Get((TankKind)i).Name, 13, Ui.Ink, TextAnchor.MiddleCenter, true);
                // 폭발 단계에서는 굴착 반경을 같이 적는다 — 크기 차이를 숫자로도 대조하게
                if (_galStep >= 3)
                {
                    var st = TankStats.For((TankKind)i, _galStep == 3 ? ShellKind.Special : ShellKind.Normal, 1f, _weather);
                    Ui.TextShadow(new Rect(r.x, r.y + 17f, r.width, 18f),
                                  $"굴착 {st.CraterRadius:F1}m", 11, Ui.Dim, TextAnchor.MiddleCenter);
                }
            }
        }

        void ShellGalleryStep()
        {
            GalleryCamera();
            if (_galStep >= GalSteps.Length)
            {
                VerifyShots();
                Application.Quit(0);
                return;
            }

            // 단계마다: 0=세팅, 1=한 프레임 재생(파티클이 뿜도록), 2=촬영, 3=다음
            switch (_galFrame)
            {
                case 0:
                    _hudOff = true;              // 전투 HUD 를 끈다 — 갤러리는 탄만 보여야 한다
                    if (_fx == null) _fx = new GameObject("ParticleFx").AddComponent<ParticleFx>();
                    if (_galStep == 0) BuildShellRow(ShellKind.Normal);
                    else if (_galStep == 1) BuildShellRow(ShellKind.Special);
                    else if (_galStep == 2)
                    {
                        // 자취: 13종 탄에 각자 자취를 붙인다. 제자리에 뿜으므로 한 줄로 갈린다.
                        BuildShellRow(ShellKind.Special);
                        for (int i = 0; i < _galShells.Count; i++)
                            _fx.AttachTrail(_galShells[i], ShellTrail.Of((TankKind)i, ShellKind.Special, false));
                    }
                    else
                    {
                        // 폭발: 13종의 **실제 굴착 반경**으로 나란히 터뜨린다 — 크기 차이가 눈에 보여야 한다.
                        foreach (var t in _galShells) if (t != null) t.gameObject.SetActive(false);
                        var th = MapTheme.Of(_map, _weather == Weather.Snow);
                        // 3단계=2번탄, 4단계=1번탄 — 기종×탄종 폭발 스타일(ParticleFx.BlastStyle)을 둘 다 대조한다
                        var gsh = _galStep == 3 ? ShellKind.Special : ShellKind.Normal;
                        for (int i = 0; i < TankStats.Count; i++)
                        {
                            var st = TankStats.For((TankKind)i, gsh, 1f, _weather);
                            _fx.Blast(GalSlot(i), st.CraterRadius, th.RockDark, (TankKind)i, gsh);
                        }
                    }
                    _log = $"갤러리 {GalSteps[_galStep]}";
                    break;

                default:
                    // ⚠️ 자취 단계는 탄을 **움직여야** 한다. 세워 두면 입자가 탄 안에서 겹쳐 쌓여
                    //    "자취가 없다"로 보인다(실제로 그렇게 찍혔다) — 게임에서는 탄이 날아가므로 뒤에 남는다.
                    if (_galStep == 2)
                        for (int i = 0; i < _galShells.Count; i++)
                            if (_galShells[i] != null)
                                _galShells[i].position = GalSlot(i) + Vector3.up * (_galFrame * 0.55f - 6f);

                    // ⚠️ `ScreenCapture.CaptureScreenshot` 은 **다음 프레임 끝**에 찍는다.
                    //    찍자마자 단계를 넘기면 그 다음 단계의 라벨이 사진에 들어간다
                    //    (자취 사진에 "폭발_전체" 라고 적혀 나왔다). 한 프레임 더 두고 넘어간다.
                    if (_galFrame == GalShotFrame) Shot($"gal{_galStep + 1}_{GalSteps[_galStep]}");
                    else if (_galFrame > GalShotFrame + 1) { _galStep++; _galFrame = -1; }
                    break;
            }
            _galFrame++;
        }

        void ShellCheckStep()
        {
            var seen = new Dictionary<string, string>();
            int dup = 0;
            Debug.Log("[Tankfall] === 탱크별 탄 확인 (13종 × 1·2번탄) ===");
            for (int i = 0; i < TankStats.Count; i++)
            {
                var kind = (TankKind)i;
                for (int sIdx = 0; sIdx < 2; sIdx++)
                {
                    var shellKind = sIdx == 0 ? ShellKind.Normal : ShellKind.Special;
                    var mesh = ProceduralTank.Shell(kind, shellKind);
                    var col = ProceduralTank.ShellColor(kind, shellKind);
                    var trail = ShellTrail.Of(kind, shellKind, false);

                    // 모양 지문: 정점 수 + 바운드 크기(소수 둘째 자리). 같은 case 를 공유하면 똑같이 나온다.
                    var b = mesh.bounds.size;
                    string fp = $"v{mesh.vertexCount}_b{b.x:F2}x{b.y:F2}x{b.z:F2}";
                    string who = $"{TankStats.Get(kind).Name}/{(sIdx == 0 ? "1번탄" : "2번탄")}";

                    if (seen.TryGetValue(fp, out var other)) { dup++; Debug.LogError($"[Tankfall] ❌ 탄 모양이 겹친다: {who} == {other}  ({fp})"); }
                    else seen[fp] = who;

                    Debug.Log($"[Tankfall] {who,-22} 모양 {fp,-28} 색 #{ColorUtility.ToHtmlStringRGB(col)} 자취 {(trail.Glow ? "발광" : "연무")} 크기{trail.Size:F2} 간격{trail.Interval:F3}");
                }
            }

            if (dup == 0) Debug.Log($"[Tankfall] ✅ 탄 확인 {seen.Count}/{TankStats.Count * 2} — 전부 서로 다른 모양이다");
            else Debug.LogError($"[Tankfall] ❌ 탄 확인 실패 — 겹치는 모양 {dup}개");

            Application.Quit(dup == 0 ? 0 : 1);
        }
        int _uiStep, _uiSub;
        readonly List<(string Name, string Hud, string NoHud)> _uiPairs = new List<(string, string, string)>();

        static readonly string[] UiSteps =
        { "타이틀", "조작법", "탱크선택", "전투설정", "전투HUD", "파워게이지", "일시정지", "결과" };

        /// <summary>단계별로 화면 상태를 만든다. 실제 화면 코드를 그대로 쓴다 — 별도 그리기를 만들면 검사가 거짓이 된다.</summary>
        void UiSelfTestSetup(int step)
        {
            _showHelp = false;
            switch (step)
            {
                case 0: _screen = GameScreen.Title; _menuSel = 0; break;
                case 1: _screen = GameScreen.Title; _menuSel = 2; _showHelp = true; break;
                case 2:
                    _screen = GameScreen.TankSelect;
                    _picked.Clear();
                    _picked.Add(TankKind.Cannon); _picked.Add(TankKind.Carrot); _picked.Add(TankKind.Laser);
                    _pickCursor = (int)TankKind.Poseidon;
                    break;
                case 3: _screen = GameScreen.Setup; _setupSel = 1; break;
                case 4:
                    _screen = GameScreen.Battle;
                    _phase = Phase.Move; _phaseTimer = MovePhaseSec * 0.7f;
                    _turn = _units.FindIndex(o => o.Team == 0 && o.Alive);
                    if (_turn < 0) _turn = 0;
                    _units[_turn].Hp = Mathf.RoundToInt(_units[_turn].HpMax * 0.62f);
                    AddPopup(_units[_turn].Pos, "-128", Ui.Enemy);         // 피해 팝업도 화면에 있어야 한다
                    break;
                case 5:
                    _screen = GameScreen.Battle;
                    _phase = Phase.Fire; _phaseTimer = FirePhaseSec * 0.5f;
                    _charging = true; _power = 0.62f; _mark = 0.74f;
                    // 게이지가 궁극기까지 찬 모습을 찍는다 — 슬롯 4칸이 전부 살아 있는 상태가 검사 대상이다
                    for (int i = 0; i < NiceShot.UltimateCost; i++) _units[_turn].Skill.OnNiceShot();
                    break;
                case 6: _screen = GameScreen.Pause; break;
                case 7:
                    _winner = "아군 승리";
                    _phase = Phase.GameOver;
                    _screen = GameScreen.Result;
                    _stat[0] = new TeamStat { Shots = 14, Hits = 9, Damage = 2480, Taken = 1310 };
                    _stat[1] = new TeamStat { Shots = 13, Hits = 5, Damage = 1310, Taken = 2480 };
                    _battleClock = 372f;
                    break;
            }
        }

        void UiSelfTestStep()
        {
            // 서브프레임: 0=상태 세팅, 1=HUD 켜고 캡처, 2=HUD 끄고 캡처, 3=복원하고 다음 단계
            if (_uiStep >= UiSteps.Length)
            {
                VerifyUiPairs();
                VerifyShots();
                Application.Quit(0);
                return;
            }

            string name = $"ui{_uiStep + 1}_{UiSteps[_uiStep]}";
            switch (_uiSub)
            {
                case 0:
                    UiSelfTestSetup(_uiStep);
                    _hudOff = false;
                    // ⚠️ 카메라를 갱신하지 않으면 전투 화면 단계가 **초기 위치**에서 찍힌다 —
                    //    지형이 거의 안 보이는 사진이 남아 "지형이 사라졌다"고 오진하게 된다(실제로 겪었다).
                    if (_screen == GameScreen.Battle || _screen == GameScreen.Pause || _screen == GameScreen.Result)
                        UpdateCamera(0f);
                    else
                        OrbitCamera(0f);
                    break;
                case 1:
                    Shot(name);
                    break;
                case 2:
                    _hudOff = true;                    // ← 네거티브 컨트롤: UI 를 빼고 같은 장면
                    break;
                case 3:
                    Shot(name + "_nohud");
                    _uiPairs.Add((UiSteps[_uiStep], ShotPath(name), ShotPath(name + "_nohud")));
                    break;
                default:
                    _hudOff = false;
                    _uiStep++;
                    _uiSub = -1;                       // 아래에서 ++ 되어 0
                    break;
            }
            _uiSub++;
        }

        /// <summary>HUD 있는 장면과 없는 장면이 실제로 다른가. 같으면 UI 가 안 그려진 것이다.</summary>
        void VerifyUiPairs()
        {
            int ok = 0;
            foreach (var pair in _uiPairs)
            {
                float diff = PixelDiff(pair.Hud, pair.NoHud);
                bool good = diff >= 0.004f;            // 화면의 0.4% 이상이 달라야 "UI 가 있다"
                if (good) ok++;
                Debug.Log($"[Tankfall] UI {pair.Name,-8} 차이 {diff * 100f,6:F2}%  {(good ? "OK" : "❌ UI 가 안 그려졌다")}");
            }
            // ── 네거티브 컨트롤 ──────────────────────────────────
            // 위 검사는 "HUD 있는 장면 vs 없는 장면이 다르다" 만 본다. 그 차이가 **UI 때문**이라는 걸
            // 따로 보여야 한다 — 안 그러면 카메라가 흔들리기만 해도 전부 통과한다.
            // 그래서 **UI 가 없는 두 장끼리** 비교한다. 같은 카메라·같은 장면이므로 거의 같아야 하고,
            // 그게 임계값 아래로 내려가야 "임계값을 넘긴 건 UI 였다"가 성립한다.
            bool controlOk = true;
            if (_uiPairs.Count >= 2)
            {
                float ctrl = PixelDiff(_uiPairs[0].NoHud, _uiPairs[1].NoHud);
                controlOk = ctrl < 0.004f;
                Debug.Log($"[Tankfall] UI 네거티브대조 (UI 없는 두 장) 차이 {ctrl * 100f:F2}%  " +
                          $"{(controlOk ? "OK — 임계값을 넘긴 것은 UI 가 맞다" : "❌ UI 없이도 화면이 달라진다 — 이 검사는 UI 를 재는 게 아니다")}");
            }

            if (ok == _uiPairs.Count && controlOk)
                Debug.Log($"[Tankfall] ✅ UI 자체검사 {ok}/{_uiPairs.Count} + 네거티브대조 통과 — 모든 화면이 실제로 그려졌다");
            else
                Debug.LogError($"[Tankfall] ❌ UI 자체검사 {ok}/{_uiPairs.Count} · 네거티브대조 {(controlOk ? "OK" : "실패")}");
        }

        static float PixelDiff(string a, string b)
        {
            try
            {
                var ta = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                var tb = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ta.LoadImage(System.IO.File.ReadAllBytes(a)) || !tb.LoadImage(System.IO.File.ReadAllBytes(b))) return 0f;
                var pa = ta.GetPixels32(); var pb = tb.GetPixels32();
                Destroy(ta); Destroy(tb);
                if (pa.Length != pb.Length || pa.Length == 0) return 0f;
                int n = 0, step = Mathf.Max(1, pa.Length / 40000), cnt = 0;
                for (int i = 0; i < pa.Length; i += step)
                {
                    cnt++;
                    if (Mathf.Abs(pa[i].r - pb[i].r) + Mathf.Abs(pa[i].g - pb[i].g) + Mathf.Abs(pa[i].b - pb[i].b) > 24) n++;
                }
                return cnt == 0 ? 0f : n / (float)cnt;
            }
            catch (System.Exception e) { Debug.LogError($"[Tankfall] 픽셀 비교 실패: {e.Message}"); return 0f; }
        }

    }
}