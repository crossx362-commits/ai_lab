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
        public enum GameScreen { Title, TankSelect, Setup, Settings, Battle, Pause, Result }

        GameScreen _screen = GameScreen.Battle;      // 기본은 Battle — 자동 모드가 메뉴에 걸리지 않게 한다
        int _menuSel;                        // 타이틀 메뉴 커서
        int _pickCursor;                     // 탱크 선택 커서(0..12)
        readonly List<TankKind> _picked = new List<TankKind>();
        int _setupSel;                       // 전투 설정(맵·난이도 등) 화면 행 커서

        static readonly string[] TitleMenu = { "전투 시작", "연습장", "설정", "조작법", "종료" };
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
            if (_screen == GameScreen.Title || _screen == GameScreen.TankSelect || _screen == GameScreen.Setup || _screen == GameScreen.Settings)
                OrbitCamera(Time.deltaTime);

            // 미리보기는 선택 화면 전용이다 — 다른 화면으로 가면 반드시 치운다(카메라에 붙어 있어서
            // 안 치우면 전투 중에도 탱크 한 대가 화면 가운데에 따라다닌다).
            if (_screen != GameScreen.TankSelect) ClearPreview();

            switch (_screen)
            {
                case GameScreen.Title: TitleInput(); return true;
                case GameScreen.TankSelect: TankSelectInput(); return true;
                case GameScreen.Setup: SetupInput(); return true;
                case GameScreen.Settings: SettingsInput(); return true;
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
            TitleActivate(_menuSel);
        }

        /// <summary>
        /// 타이틀 메뉴 한 항목을 실행한다. **입력 판정과 분리한 이유는 자체검사가 부를 수 있게 하기 위해서다** —
        /// `Input.GetKeyDown` 은 합성할 수 없어서, 여기 있는 동안에는 "눌렀을 때 그 화면으로 가는가"를
        /// 아무도 검증하지 못했다(옛 고르기 창이 타이틀을 덮어 세 화면이 도달 불가였던 것도 그래서 안 잡혔다).
        /// </summary>
        void TitleActivate(int sel)
        {
            switch (sel)
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
                case 2: OpenSettings(GameScreen.Title); break;
                case 3: _showHelp = true; break;
                case 4: Quit(); break;
            }
        }

        /// <summary>설정 화면 — 지금은 음량 하나뿐이다. 조작(전투 설정 §DrawSetup)과 헷갈리지 않게
        /// "게임을 매번 바꾸는 값"(맵·난이도 등)과 "환경을 한 번 맞추는 값"(음량)을 화면째 나눈다.</summary>
        // ══════════════════════════════════════════════════════════════
        //  소리 설정 — **한 곳이 진짜 소스다** (2026-09-19 통합, 오너 허가 (c))
        //
        //  그 전까지 소리 설정이 **두 화면에 나뉘어** 있었다: 채널 켜기/끄기는 전투 설정 화면,
        //  전체 음량은 타이틀→설정 화면. 직교하는 값이라 둘 다 필요한데 **사람이 보기엔 두 군데**였다.
        //  → 값은 그대로 두고 **자리만 합쳤다.** 여기가 소스이고, 다른 곳은 «이 화면을 여는 것»이다.
        //
        //  ⚠️ **UI 를 두 벌 만들지 마라.** 이 프로젝트가 가장 크게 데인 자리가 정확히 이거다 —
        //     옛 `DrawPicker` 와 `ScreenGUI()` 가 둘 다 살아 있어서 **먼저 그리는 쪽이 이겼고**
        //     폴리시된 화면 3종이 통째로 도달 불가였다. 전투 설정 화면은 이제 **보여주기만** 한다.
        //  ⚠️ 저장 키는 안 바뀌었다(`sfxoff`·`musicoff`·`volume`). 자리만 옮긴 것이라
        //     오너가 이미 저장해 둔 값이 그대로 살아 있어야 한다.
        //  ⚠️ 행 번호를 숫자로 박지 마라 — 아래 표에서 센다(소리 행을 붙이다 커서가 깨졌던 자리).
        // ══════════════════════════════════════════════════════════════
        static readonly string[,] SettingsRows =
        {
            { "음량",   "전체 크기 — 채널을 켠 채로 줄인다" },
            { "효과음", "발사·폭발·조작음 (일시정지 N)" },
            { "음악",   "타이틀·전투 배경음악 (일시정지 M)" },
        };
        const int SettingsRowVolume = 0, SettingsRowSfx = 1, SettingsRowMusic = 2;
        int _settingsSel;
        /// <summary>설정을 **어디서 열었나** — 닫으면 그리로 돌아간다(타이틀·전투 설정·일시정지).</summary>
        GameScreen _settingsBack = GameScreen.Title;

        void OpenSettings(GameScreen back)
        {
            _settingsBack = back;
            _settingsSel = 0;
            _screen = GameScreen.Settings;
        }

        /// <summary>전투 설정 화면이 **읽기만** 하는 한 줄 요약. 소스는 설정 화면이다.</summary>
        string SoundSummary()
            => $"음량 {Mathf.RoundToInt(Sfx.Volume * 100f)}% · 효과음 {(Sfx.SfxOff ? "끔" : "켬")} · 음악 {(Sfx.MusicOff ? "끔" : "켬")}";

        /// <summary>
        /// 설정 닫기. **입력 처리와 자체검사가 같은 함수를 봐야 한다** — 여기 규칙을 검사가 따로 베끼면
        /// 한쪽만 낡는다(이 프로젝트가 반복해서 데인 자리: 적용과 평가가 같은 함수를 봐야 한다).
        /// </summary>
        void SettingsCloseForTest() => _screen = _settingsBack;

        void SettingsInput()
        {
            if (Down(KeyCode.Escape) || Enter()) { SettingsCloseForTest(); return; }
            int rows = SettingsRows.GetLength(0);
            if (Down(KeyCode.DownArrow) || Down(KeyCode.S)) _settingsSel = (_settingsSel + 1) % rows;
            if (Down(KeyCode.UpArrow) || Down(KeyCode.W)) _settingsSel = (_settingsSel - 1 + rows) % rows;

            int dir = (Down(KeyCode.RightArrow) || Down(KeyCode.D)) ? 1 : (Down(KeyCode.LeftArrow) || Down(KeyCode.A)) ? -1 : 0;
            if (dir == 0) return;
            switch (_settingsSel)
            {
                case SettingsRowVolume: Sfx.Volume += 0.1f * dir; break;
                // 켜고 끄기뿐이라 방향은 안 본다(Boom 행과 같은 규칙).
                case SettingsRowSfx: ToggleSfx(); break;
                case SettingsRowMusic: ToggleMusic(); break;
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
                else if (_picked.Count < MapHeightFunction.TeamSize) _picked.Add(k);
                // ⚠️ 같은 기종을 여러 대 고르는 걸 막지 않는다 — 원작에도 그런 제한이 없고, 막으면 "3종 다 캐논" 실험을 못 한다.
            }
            if ((Down(KeyCode.Return) || Down(KeyCode.KeypadEnter)) && _picked.Count == MapHeightFunction.TeamSize)
            {
                _roster = _picked.ToArray();
                _screen = GameScreen.Setup;
                _setupSel = 0;
            }
        }

        void SetupInput()
        {
            if (Down(KeyCode.Escape)) { _screen = GameScreen.TankSelect; return; }
            // ⚠️ 행을 추가하면 이 상수도 같이 늘려라. 숫자를 두 곳에 적지 않으려고 DrawSetup 의 표에서 센다.
            int rows = SetupRows.GetLength(0);
            if (Down(KeyCode.DownArrow) || Down(KeyCode.S)) _setupSel = (_setupSel + 1) % rows;
            if (Down(KeyCode.UpArrow) || Down(KeyCode.W)) _setupSel = (_setupSel - 1 + rows) % rows;

            int dir = (Down(KeyCode.RightArrow) || Down(KeyCode.D)) ? 1 : (Down(KeyCode.LeftArrow) || Down(KeyCode.A)) ? -1 : 0;
            if (dir != 0)
                switch (_setupSel)
                {
                    case 0:   // 맵 — 바꾸면 지형을 다시 만들어야 한다(StartBattle 에서 한다)
                        int mc = MapHeightFunction.Count;
                        _map = (MapKind)(((int)_map + dir + mc) % mc);
                        break;
                    case 1: _difficulty = (_difficulty + dir + Difficulties.Length) % Difficulties.Length; break;
                    case 2: _itemSlots = Mathf.Clamp(_itemSlots + dir, 0, 4); break;
                    case 3:   // 날씨: 자동(확률) / 맑음 / 눈
                        int w = _weatherForced == null ? 0 : _weatherForced == Weather.Clear ? 1 : 2;
                        w = (w + dir + 3) % 3;
                        _weatherForced = w == 0 ? (Weather?)null : w == 1 ? Weather.Clear : Weather.Snow;
                        break;
                    case SetupRowBoom: _boom = !_boom; break;   // Boom 모드(§2-9-16) — 켜고 끄기뿐이라 방향은 안 본다
                    // 소리는 여기서 안 고친다 — 설정 화면을 연다(Enter 는 전투 시작이라 못 쓴다).
                    case SetupRowSound: OpenSettings(GameScreen.Setup); return;
                }

            if (Down(KeyCode.Return) || Down(KeyCode.KeypadEnter)) StartBattle();
        }

        void PauseInput()
        {
            if (Down(KeyCode.Escape) || Enter()) { _screen = GameScreen.Battle; return; }
            // M/N 은 **빠른 토글**로 남긴다(사람이 소리를 끄고 싶은 순간은 대개 게임 중이다).
            // 음량까지 여기서 조절하게 만들지 마라 — 그러면 소리 UI 가 세 벌이 된다. 화면을 여는 것으로 족하다.
            if (Down(KeyCode.M)) ToggleMusic();
            if (Down(KeyCode.N)) ToggleSfx();
            if (Down(KeyCode.O)) { OpenSettings(GameScreen.Pause); return; }
            if (Down(KeyCode.T)) ToTitle();
            if (Down(KeyCode.Q)) Quit();
        }

        // ── 소리 설정 — 설정 화면과 일시정지가 **같은 함수**를 부른다. 저장도 여기서 한다(끄고 껐다 켜면 그대로여야 한다).
        void ToggleSfx()   { Sfx.SfxOff = !Sfx.SfxOff; if (Sfx.SfxOff) Sfx.Engine(false); SavePrefs(); }
        void ToggleMusic() { Sfx.MusicOff = !Sfx.MusicOff; SavePrefs(); }

        // ── 설정 저장·복원(Prefs.cs) ─────────────────────────
        // ⚠️ 사람 판에서만 부른다 — 호출부(Start · StartBattle · 토글)가 `_headless`/`_autoMode` 로 거른다.
        Prefs.Settings CurrentSettings() => new Prefs.Settings
        {
            Map = (int)_map, Difficulty = _difficulty, ItemSlots = _itemSlots,
            Weather = _weatherForced == null ? 0 : _weatherForced == Weather.Clear ? 1 : 2,
            Boom = _boom, SfxOff = Sfx.SfxOff, MusicOff = Sfx.MusicOff,
            Roster = string.Join(",", System.Array.ConvertAll(_roster, k => k.ToString())),
        };

        static readonly Prefs.Settings DefaultSettings = new Prefs.Settings
        {
            Map = (int)MapKind.TwinHills, Difficulty = 1, ItemSlots = 2, Weather = 0, Boom = false,
            Roster = string.Join(",", System.Array.ConvertAll(DefaultRoster, k => k.ToString())),
        };

        void LoadPrefs()
        {
            var cur = CurrentSettings();
            if (!Prefs.Load(ref cur, DefaultSettings)) return;
            // ⚠️ 상한을 숫자로 적지 마라 — 맵이 3종일 때 적어둔 `2` 가 6종이 되고도 남아,
            //    사람이 고른 황무지(5)가 켤 때마다 테라스(2)로 조용히 되돌아갔다(2026-09-18 병합).
            //    설정 화면(`case 0`)이 쓰는 것과 **같은 소스**를 본다.
            _map = (MapKind)Mathf.Clamp(cur.Map, 0, MapHeightFunction.Count - 1);
            _difficulty = Mathf.Clamp(cur.Difficulty, 0, Difficulties.Length - 1);
            _itemSlots = Mathf.Clamp(cur.ItemSlots, 0, 4);
            _weatherForced = cur.Weather == 0 ? (Weather?)null : cur.Weather == 1 ? Weather.Clear : Weather.Snow;
            _boom = cur.Boom;
            Sfx.SfxOff = cur.SfxOff; Sfx.MusicOff = cur.MusicOff;
            Sfx.Apply();                                      // 저장된 음량을 실제 출력에 건다
            if (!_rosterFixed && !string.IsNullOrEmpty(cur.Roster))
            {
                var list = new List<TankKind>();
                foreach (var n in cur.Roster.Split(','))
                    if (System.Enum.TryParse<TankKind>(n.Trim(), true, out var k)) list.Add(k);
                if (list.Count == MapHeightFunction.TeamSize) _roster = list.ToArray();     // 기종이 사라졌거나 손상됐으면 기본 로스터
            }
            Debug.Log($"[Tankfall] 저장 설정 복원 — {MapHeightFunction.Name(_map)} · AI {Difficulties[_difficulty].Name} · 아이템 {_itemSlots} · 효과음 {(Sfx.SfxOff ? "끔" : "켬")} · 음악 {(Sfx.MusicOff ? "끔" : "켬")}");
        }

        void SavePrefs()
        {
            if (_headless || _autoMode) return;
            Prefs.Save(CurrentSettings());
        }

        /// <summary>승패 문구. 양 팀 생존 0 이면 무승부, 둘 다 살아 있으면 null(아직 안 끝남). NextTurn 과 자체검사가 같이 쓴다.</summary>
        static string WinnerText(int aliveA, int aliveB)
        {
            if (aliveA > 0 && aliveB > 0) return null;
            if (aliveA == 0 && aliveB == 0) return "무승부";
            return aliveA > 0 ? "아군 승리" : "적군 승리";
        }

        // ═════════════════════════════════════════════════════
        //  -gameselftest — 게임으로서 붙인 것들이 실제로 동작하는가(2026-09-18).
        //  각 항목에 **빨간불 대조군**이 있다: 이음새 검사는 안 페이드된 파형에서 실패해야 하고,
        //  저장값 없음에서는 Load 가 false 여야 하고, 음악 끔에서는 트랙이 서면 안 된다.
        //  ⚠️ 사람 저장값을 건드리지 않는다 — Prefs.Namespace 를 바꿔 쓰고 끝에 지운다.
        // ═════════════════════════════════════════════════════
        bool _gameSelfTest;

        /// <summary>
        /// 루프 이음새 — 딸깍은 **값의 점프**에서 난다. 끝 n 샘플이 조용하고 첫 샘플이 0 근처면 점프가 없다.
        /// (처음 "양끝 다 조용"으로 쟀다가 첫 박 킥이 걸렸다 — 0 에서 올라오는 어택은 딸깍이 아니다. 2026-09-18)
        /// </summary>
        static bool SeamQuiet(float[] d, int n, float limit)
        {
            if (Mathf.Abs(d[0]) > limit) return false;
            for (int i = 0; i < n; i++)
                if (Mathf.Abs(d[d.Length - 1 - i]) > limit) return false;
            return true;
        }

        void GameSelfTestStep()
        {
            int fail = 0;
            void Check(bool ok, string what) { if (ok) Debug.Log($"[Tankfall] 게임 자체검사 ✓ {what}"); else { Debug.Log($"[Tankfall] ❌ {what}"); fail++; } }

            // 1) 배경음악 — 두 트랙이 구워지고, 들리는 진폭이며, 루프 경계가 조용하다
            foreach (var t in new[] { Sfx.Track.Title, Sfx.Track.Battle })
            {
                var clip = Sfx.BuildClip(t);
                var d = new float[clip.samples];
                clip.GetData(d, 0);
                float peak = 0f; foreach (var v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
                Check(clip.samples > 44100 * 5, $"{t} 길이 {clip.samples / 44100f:F1}s (5s 이상)");
                Check(peak > 0.15f && peak <= 1f, $"{t} 최대 진폭 {peak:F2} (0.15~1.0)");
                Check(SeamQuiet(d, 30, 0.03f), $"{t} 루프 이음새 조용함");
            }
            {   // 대조군: 페이드 없는 사인파는 이음새 검사에 걸려야 한다
                var raw = new float[44100]; for (int i = 0; i < raw.Length; i++) raw[i] = Mathf.Sin(i * 0.05f + 1f) * 0.5f;
                Check(!SeamQuiet(raw, 30, 0.03f), "대조군 — 안 페이드된 파형은 이음새 검사 실패");
            }

            // 2) 음악 게이트 — 끔/음소거면 트랙이 서지 않는다
            bool savedMuted = Sfx.Muted, savedOff = Sfx.MusicOff;
            Sfx.Muted = false; Sfx.MusicOff = true; Sfx.Music(Sfx.Track.Title);
            Check(Sfx.Current == Sfx.Track.None, "음악 끔 → 트랙 없음");
            Sfx.MusicOff = false; Sfx.Music(Sfx.Track.Title);
            Check(Sfx.Current == Sfx.Track.Title, "음악 켬 → 타이틀 트랙");
            Sfx.Music(Sfx.Track.Battle);
            Check(Sfx.Current == Sfx.Track.Battle, "전투 화면 → 전투 트랙으로 전환");
            Sfx.Muted = true; Sfx.Music(Sfx.Track.Battle);
            Check(Sfx.Current == Sfx.Track.None, "하네스 음소거 → 트랙 없음(대조군)");
            Sfx.Music(Sfx.Track.None);
            Sfx.Muted = savedMuted; Sfx.MusicOff = savedOff;

            // 3) 설정 저장 — 왕복 · 인자 우선 · 없음이면 false
            string ns = Prefs.Namespace;
            Prefs.Namespace = "tankfall.selftest.";
            try
            {
                Prefs.DeleteAll(Difficulties.Length);
                var probe = DefaultSettings;
                Check(!Prefs.Load(ref probe, DefaultSettings), "저장값 없음 → Load=false(대조군)");
                var a = new Prefs.Settings { Map = 2, Difficulty = 3, ItemSlots = 0, Weather = 2, Boom = true, SfxOff = true, MusicOff = true, Roster = "Poseidon,Duke,IonAttacker" };
                Prefs.Save(a);
                var b = DefaultSettings;
                Check(Prefs.Load(ref b, DefaultSettings) && b.Map == 2 && b.Difficulty == 3 && b.ItemSlots == 0 && b.Weather == 2 && b.Boom && b.SfxOff && b.MusicOff && b.Roster == a.Roster,
                      "저장 → 복원 왕복");
                var c = DefaultSettings; c.Map = 1;                 // -map 인자를 준 상황
                Prefs.Load(ref c, DefaultSettings);
                Check(c.Map == 1 && c.Difficulty == 3, "인자로 준 맵은 안 덮고 나머지는 복원");
                Prefs.Record(1, Prefs.Outcome.Win); Prefs.Record(1, Prefs.Outcome.Win);
                Prefs.Record(0, Prefs.Outcome.Lose); Prefs.Record(2, Prefs.Outcome.Draw);
                var tot = Prefs.Total(Difficulties.Length);
                Check(tot.Win == 2 && tot.Lose == 1 && tot.Draw == 1, $"전적 누계 {tot.Win}승 {tot.Lose}패 {tot.Draw}무");
                Check(Prefs.TotalText(Difficulties.Length) == "통산 2승 1패 1무", "전적 문구");
                // 음량 — 저장 경로가 Prefs 를 거치는가. 여기서 쓴 값이 네임스페이스 밖으로 새면
                // 사람 저장 파일이 더러워진다(병합 직후 실제로 샜다 — Prefs.Volume 머리말).
                Sfx.Volume = 0.3f;
                Check(Mathf.Approximately(Prefs.Volume, 0.3f) && Mathf.Approximately(AudioListener.volume, 0.3f),
                      "음량 저장 → Prefs 경유 + 실제 출력 반영");
                Check(!PlayerPrefs.HasKey("tankfall_volume"), "음량이 네임스페이스 밖으로 안 샌다(대조군)");

                // 턴 시간 실측(2026-09-19) — §2-5-0 의 "분" 열이 매달린 18초/턴 **가정**을 사람 판이 검증한다.
                // 여기서 재는 것은 «누적과 평균이 맞는가»다. 값 자체가 아니라 산수가 틀리면 결론이 조용히 틀어진다.
                Check(Prefs.TurnPace().Games == 0, "턴 시간 — 판이 없으면 0(대조군)");
                Prefs.RecordTurnPace(120f, 10, 20f, 10);      // 내 턴 12초 · AI 턴 2초
                Prefs.RecordTurnPace(60f, 5, 10f, 5);         // 내 턴 12초 · AI 턴 2초 (같은 평균)
                var pc = Prefs.TurnPace();
                Check(pc.Games == 2 && Mathf.Approximately(pc.Human, 12f) && Mathf.Approximately(pc.Ai, 2f)
                      && Mathf.Approximately(pc.All, 7f),
                      $"턴 시간 누적 — 내 턴 {pc.Human:F1}초 · AI {pc.Ai:F1}초 · 전체 {pc.All:F1}초 ({pc.Games}판)");
                // ⚠️ 자체검사가 사람 저장값을 안 건드리는 성질이 **새 키에도** 걸려야 한다.
                //
                // 🚨 2026-09-19: 이 청소가 `SettingKeys` 라는 **사람이 관리하는 목록**으로 돌고 있었고,
                //    목록에 없는 키는 영영 남았다 — 실제로 `tankfall.selftest.sfx_off_v2` 가 사람 plist 에 남았다.
                //    이제 쓰는 쪽이 색인(`__keys`)을 갱신하고 `DeleteAll` 이 그걸 지운다.
                //    여기서 재는 것은 **"목록에 없어도 지워지는가"** 다 — `pace.*` 는 이제 목록(LegacyKeys)에 없다.
                Prefs.DeleteAll(Difficulties.Length);
                Check(Prefs.TurnPace().Games == 0, "턴 시간 키도 DeleteAll 이 지운다(색인 경유 — 목록에 없다)");
                Check(!PlayerPrefs.HasKey(Prefs.Namespace + "pace.hsec")
                      && !PlayerPrefs.HasKey(Prefs.Namespace + "pace.games")
                      && !PlayerPrefs.HasKey(Prefs.Namespace + "volume")
                      && !PlayerPrefs.HasKey(Prefs.Namespace + "__keys"),
                      "색인·색인이 아는 키가 plist 에서 실제로 사라졌다");

                // 맵 상한 — 저장된 마지막 맵이 클램프에 잘리지 않는가. 숫자를 박아두면 맵이 늘 때 조용히 깨진다.
                int last = MapHeightFunction.Count - 1;
                var m = new Prefs.Settings { Map = last, Difficulty = 0, ItemSlots = 2, Weather = 0, Roster = "" };
                Prefs.Save(m);
                var mb = DefaultSettings;
                Prefs.Load(ref mb, DefaultSettings);
                Check(Mathf.Clamp(mb.Map, 0, MapHeightFunction.Count - 1) == last,
                      $"마지막 맵({MapHeightFunction.Name((MapKind)last)})이 복원에서 안 잘린다");

                // ── 사격 산포가 기종별로 다르게 걸리는가(오너 지시 2026-09-18) ──
                // 상수를 적어두기만 하고 호출부가 기종을 안 넘기면 **전부 같은 값으로 조용히 돈다** —
                // 컴파일러가 안 잡아주는 자리라 실제로 뽑아서 잰다.
                {
                    var jr = new Rng(12345u);
                    float SpreadOf(TankKind kk)
                    {
                        float sum = 0f; const int N = 400;
                        for (int i2 = 0; i2 < N; i2++)
                        {
                            Spread.AimJitter(ref jr, kk, out float a2, out float b2);
                            sum += Mathf.Abs(a2) + Mathf.Abs(b2);
                        }
                        return sum / (N * 2);
                    }
                    float cat = SpreadOf(TankKind.Catapult), las = SpreadOf(TankKind.Laser);
                    Check(cat > las * 2f, $"기종별 산포 — 캐터펄트 {cat:F3}° > 레이저 {las:F3}°");
                    Check(las > 0f, "가장 정밀한 기종도 0 은 아니다(산포가 아예 안 걸리면 이 값이 0)");
                    // 상한 — 표에 적은 값을 넘지 않는가(두 번 뽑아 더하는 분포라 최대치는 표 값이다)
                    float worst = 0f;
                    for (int i2 = 0; i2 < 2000; i2++)
                    {
                        Spread.AimJitter(ref jr, TankKind.Catapult, out float a2, out float b2);
                        worst = Mathf.Max(worst, Mathf.Max(Mathf.Abs(a2), Mathf.Abs(b2)));
                    }
                    Check(worst <= Spread.AimJitterDeg(TankKind.Catapult) + 0.001f,
                          $"산포가 표의 상한을 안 넘는다({worst:F3}° ≤ {Spread.AimJitterDeg(TankKind.Catapult):F2}°)");
                }

                Prefs.DeleteAll(Difficulties.Length);
                Check(Prefs.TotalText(Difficulties.Length) == "", "지운 뒤 전적 문구 비어 있음");
                Check(Mathf.Approximately(Prefs.Volume, 1f), "지운 뒤 음량이 기본값으로 돌아옴");
            }
            finally { Prefs.Namespace = ns; }

            // ── 메뉴에서 화면에 실제로 도달하는가 ──
            // 🚨 "화면은 만들었는데 아무도 못 간다"가 이 프로젝트에서 실제로 났다(옛 IMGUI 고르기 창이
            //    타이틀·탱크선택·전투설정 셋을 통째로 덮었다). 그리기는 `-uiselftest` 가 재지만 그건
            //    화면을 **강제로 세워놓고** 찍는 것이라 **도달 가능성은 못 잰다.** 여기서 그걸 잰다.
            {
                var savedScreen = _screen; bool savedHelp = _showHelp;
                _screen = GameScreen.Title; _showHelp = false;

                TitleActivate(0);
                Check(_screen == GameScreen.TankSelect, $"전투 시작 → 탱크 선택 (간 곳: {_screen})");
                Check(_picked.Count == MapHeightFunction.TeamSize, $"탱크 선택이 로스터로 채워져 열린다({_picked.Count}대)");

                _screen = GameScreen.Title;
                TitleActivate(2);
                Check(_screen == GameScreen.Settings, $"설정 → 설정 화면 (간 곳: {_screen})");

                _screen = GameScreen.Title; _showHelp = false;
                TitleActivate(3);
                Check(_showHelp, "조작법 → 도움말 열림");

                _screen = savedScreen; _showHelp = savedHelp;
            }

            // 3-1) 눈이 **몸에 붙은 불**도 끄는가 (2026-09-19 (b) 배선)
            //      장판 불(`HazardField.ClearFires`)만 끄고 유닛 화상을 안 끄면
            //      «바닥 불은 꺼지는데 탱크는 계속 타는» 비대칭이 된다. 그 자리를 코드가 지킨다.
            {
                var w0 = _weather;
                SetWeather(Weather.Clear);
                _status.Burn(4242, 40, 3);
                Check(_status.HasDot(4242), "화상이 실제로 걸린다(대조군 — 안 걸리면 아래 검사가 무의미하다)");
                SetWeather(Weather.Snow);
                Check(!_status.HasDot(4242), "눈 → 몸에 붙은 불도 꺼진다(장판만 끄면 비대칭)");
                SetWeather(w0);
            }

            // 3-2) **사람이 실제로 밟는 화면 왕복** (2026-09-19)
            //
            // 🚨 지금까지 화면 검사는 전부 `_screen = ...` 로 **화면을 세워 놓고 그리기만** 했다
            //    (`-uiselftest` 9종). 그래서 «열고 → 닫으면 원래 자리로 돌아오는가»는 **한 번도 안 쟀다.**
            //    소리 설정을 세 곳(타이틀·전투 설정·일시정지)에서 열 수 있게 만든 뒤라 정확히 여기가 위험하다 —
            //    `_settingsBack` 을 한 군데서 안 세우면 **사람이 설정에서 나올 때 엉뚱한 화면으로 떨어진다.**
            //    그건 그려지기는 하니까 스크린샷 검사로는 절대 안 잡힌다.
            {
                var back0 = _screen;
                // 세 진입점 전부 — 연 곳으로 돌아와야 한다.
                foreach (var from in new[] { GameScreen.Title, GameScreen.Setup, GameScreen.Pause })
                {
                    _screen = from;
                    OpenSettings(from);
                    Check(_screen == GameScreen.Settings, $"{from} → 소리 설정이 열린다 (간 곳 {_screen})");
                    SettingsCloseForTest();
                    Check(_screen == from, $"소리 설정 닫으면 {from} 으로 돌아온다 (간 곳 {_screen})");
                }
                // 커서가 남아 있으면 다음에 열 때 엉뚱한 행이 잡힌다 — 열 때마다 처음으로.
                OpenSettings(GameScreen.Title);
                Check(_settingsSel == 0, $"설정을 열면 커서가 처음 행이다 ({_settingsSel})");
                _screen = back0;
            }

            // 4) 승패 판정 — 동시 전멸은 무승부
            Check(WinnerText(0, 0) == "무승부", "0:0 → 무승부");
            Check(WinnerText(2, 0) == "아군 승리" && WinnerText(0, 1) == "적군 승리", "한쪽 생존 → 그쪽 승리");
            Check(WinnerText(1, 1) == null, "양쪽 생존 → 미결");

            // 5) 설정 화면 행 — 이름 상수와 표가 어긋나지 않았는가
            Check(SetupRows[SetupRowBoom, 0] == "Boom 모드" && SetupRows[SetupRowSound, 0] == "소리"
                  && SetupRowSound == SetupRows.GetLength(0) - 1, "전투 설정 행 상수 ↔ 표 일치");
            Check(SettingsRows[SettingsRowVolume, 0] == "음량" && SettingsRows[SettingsRowSfx, 0] == "효과음"
                  && SettingsRows[SettingsRowMusic, 0] == "음악" && SettingsRows.GetLength(0) == 3,
                  "소리 설정 행 상수 ↔ 표 일치");

            // 6) 소리 설정이 **한 곳뿐인가** — 전투 설정 화면이 값을 고치면 두 벌이 된다(옛 DrawPicker 사고).
            {
                bool sfx0 = Sfx.SfxOff, mus0 = Sfx.MusicOff;
                var back0 = _screen;
                OpenSettings(GameScreen.Setup);
                Check(_screen == GameScreen.Settings && _settingsBack == GameScreen.Setup,
                      $"전투 설정의 소리 행 → 설정 화면이 열리고 돌아갈 곳을 기억한다(간 곳 {_screen}, 복귀 {_settingsBack})");
                Check(Sfx.SfxOff == sfx0 && Sfx.MusicOff == mus0, "여는 것만으로는 값이 안 바뀐다");
                _screen = back0;
            }

            if (fail == 0) Debug.Log("[Tankfall] ✅ 게임 자체검사 전부 통과(음악·설정 저장·전적·무승부·설정 행)");
            else Debug.Log($"[Tankfall] ❌ 게임 자체검사 실패 {fail}건");
            Application.Quit(fail == 0 ? 0 : 1);
        }

        /// <summary>결과 글자색 — 승리/패배/무승부. HUD 와 결과 화면이 같은 규칙을 쓴다.</summary>
        Color ResultColor()
            => _winner == null ? Ui.Ink : _winner.Contains("아군") ? Ui.Ally : _winner.Contains("무승부") ? Ui.Warn : Ui.Enemy;

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
            ClearWrecks();            // 지난 판 잔해를 안 치우면 새 전장에 남의 무덤이 서 있다

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
            _turnMark = 0f; _humanSec = _aiSec = 0f; _humanTurns = _aiTurns = 0;   // 턴 시간 실측도 판마다 초기화
            _round = 1;
            _winner = null;
            _popups.Clear();
            _power = 0f; _charging = false; _useSs = false; _niceFlash = null;
            _phase = Phase.Move;
            _phaseTimer = MovePhaseSec;
            RollWind();
            _screen = GameScreen.Battle;
            SavePrefs();                       // 여기까지 온 설정이 "다음에도 쓸 설정"이다
            _log = _practice
                ? "연습장 — Space 로 조준, F2 로 정답 보기"
                : $"전투 개시 — {MapHeightFunction.Name(_map)} · {WeatherName(_weather)} · AI {Difficulties[_difficulty].Name}"
                  + (_boom ? " · Boom 모드" : "");
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
                case GameScreen.Settings: DrawSettings(W, H); return true;
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
            string rec = Prefs.TotalText(Difficulties.Length);
            if (rec.Length > 0)
                Ui.TextShadow(new Rect(0, y + TitleMenu.Length * 46f + 10f, W, 20f), rec, 12, Ui.Dim, TextAnchor.MiddleCenter);
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

        void DrawSettings(float W, float H)
        {
            Scrim(W, H, 0.62f);
            Ui.TextShadow(new Rect(0, H * 0.30f, W, 30f), "소리", 22, Ui.Ink, TextAnchor.MiddleCenter, true);

            int rows = SettingsRows.GetLength(0);
            float y = H * 0.40f;
            for (int i = 0; i < rows; i++)
            {
                var r = new Rect(W * 0.5f - 250f, y + i * 48f, 500f, 40f);
                bool sel = i == _settingsSel;
                Ui.Fill(r, sel ? new Color(1f, 0.72f, 0.25f, 0.14f) : Ui.Panel);
                Ui.Frame(r, sel ? Ui.Power : Ui.Border, sel ? 2f : 1f);
                Ui.Text(new Rect(r.x + 16f, r.y, 150f, r.height), SettingsRows[i, 0], 14, sel ? Ui.Ink : Ui.Dim, TextAnchor.MiddleLeft, sel);

                string v = i == SettingsRowVolume ? $"{Mathf.RoundToInt(Sfx.Volume * 100f)}%"
                         : i == SettingsRowSfx ? (Sfx.SfxOff ? "끔" : "켬")
                                               : (Sfx.MusicOff ? "끔" : "켬");
                Ui.Text(new Rect(r.x + 160f, r.y, 180f, r.height), (sel ? "< " : "  ") + v + (sel ? " >" : ""),
                        14, sel ? Ui.Power : Ui.Ink, TextAnchor.MiddleCenter, sel);

                // 음량은 막대로도 보여준다 — 숫자만으로는 "얼마나 남았나"가 안 읽힌다.
                if (i == SettingsRowVolume) Ui.Bar(new Rect(r.x + 350f, r.y + 14f, 130f, 12f), Sfx.Volume, Ui.Power, null, null);
                else Ui.Text(new Rect(r.x + 350f, r.y, 140f, r.height), SettingsRows[i, 1], 9, Ui.Dim);
            }

            Ui.TextShadow(new Rect(0, H - 44f, W, 20f), "위아래 = 고르기   좌우 = 조절   Enter / Esc = 뒤로", 12, Ui.Dim, TextAnchor.MiddleCenter);
        }

        // ── 탱크 선택(§3 20_Lobby) ─────────────────────────────
        const int PickCols = 5;

        /// <summary>
        /// 탱크 선택(§2-9-17) — **롤 챔피언 선택 방식**(오너 지시 2026-09-19 "롤 처럼 선택창").
        ///
        /// 예전에는 13칸 그리드 하나가 전부였다. 스탯이 카드마다 잘게 박혀 있어 비교가 안 됐고,
        /// 저장된 로스터로 **이미 4/4 채워진 채** 열려서 고르는 화면이 아니라 확인 화면처럼 보였다
        /// (오너: "왜 탱크 선택창 없냐" — 화면은 있었지만 선택하는 화면으로 안 읽혔다).
        ///
        /// 네 구역으로 나눈다:
        ///   왼쪽  — 내 팀 슬롯. 고른 순서대로 채워지고 빈 칸은 비어 보인다(진행이 보여야 한다).
        ///   가운데 — 커서가 가리키는 기종의 **실물 3D 미리보기**(카메라에 붙어 천천히 돈다).
        ///   오른쪽 — 그 기종의 스탯·2번탄 상세. 한 곳에 모아야 기종끼리 비교가 된다.
        ///   아래   — 13종 풀. 작게, 이름과 색만.
        /// </summary>
        void DrawTankSelect(float W, float H)
        {
            // ⚠️ 스크림을 세게 깔면 **미리보기까지 어두워진다**(IMGUI 가 3D 위에 그려지므로 모델도 덮인다).
            //    패널(Ui.Box)이 제 배경을 갖고 있으니 스크림은 배경을 눌러줄 만큼만.
            Scrim(W, H, 0.42f);
            var k = (TankKind)_pickCursor;
            var st = TankStats.Get(k);
            EnsurePreview(k);

            // 제목 띠 — 스크림을 낮춘 대신 글자 뒤는 눌러 준다(밝은 하늘에 흰 글자가 묻혔다).
            Ui.Fill(new Rect(0, 0, W, 66f), new Color(0.02f, 0.03f, 0.05f, 0.72f));
            Ui.TextShadow(new Rect(0, 14f, W, 28f), "탱크 선택", 22, Ui.Ink, TextAnchor.MiddleCenter, true);
            Ui.TextShadow(new Rect(0, 42f, W, 18f),
                          $"{_picked.Count} / {MapHeightFunction.TeamSize} 선택됨    방향키 이동 · Space 선택/해제 · Enter 확정 · Esc 뒤로",
                          12, Ui.Dim, TextAnchor.MiddleCenter);

            float poolH = 132f;
            float bodyTop = 70f, bodyBot = H - poolH - 46f;

            // ── 왼쪽: 내 팀 슬롯 ──────────────────────────────
            float slotW = 224f, slotH = Mathf.Min(74f, (bodyBot - bodyTop - 26f) / MapHeightFunction.TeamSize);
            var teamBox = new Rect(24f, bodyTop, slotW, bodyBot - bodyTop);
            Ui.Box(teamBox);
            Ui.Text(new Rect(teamBox.x + 10f, teamBox.y + 4f, slotW - 20f, 16f), "내 팀", 12, Ui.Ally, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < MapHeightFunction.TeamSize; i++)
            {
                var r = new Rect(teamBox.x + 8f, teamBox.y + 24f + i * (slotH + 6f), slotW - 16f, slotH);
                bool has = i < _picked.Count;
                Ui.Fill(r, has ? new Color(0.15f, 0.48f, 0.98f, 0.18f) : new Color(1f, 1f, 1f, 0.04f));
                Ui.Frame(r, has ? Ui.Ally : Ui.Border);
                if (has)
                {
                    var ps = TankStats.Get(_picked[i]);
                    Ui.Fill(new Rect(r.x + 8f, r.y + 8f, 6f, r.height - 16f), TankShape.BodyColor(_picked[i]));
                    Ui.Text(new Rect(r.x + 22f, r.y + 6f, r.width - 30f, 20f), ps.Name, 15, Ui.Ink, TextAnchor.MiddleLeft, true);
                    Ui.Text(new Rect(r.x + 22f, r.y + 26f, r.width - 30f, 16f),
                            $"{TankStats.EraName(TankStats.EraOf(_picked[i]))} · {ps.SpecialName}", 10, Ui.Dim, TextAnchor.MiddleLeft);
                }
                else Ui.Text(r, $"{i + 1}번 자리 — 비어 있음", 11, Ui.Dim, TextAnchor.MiddleCenter);
            }

            // ── 오른쪽: 커서 기종 상세 ─────────────────────────
            float infoW = 330f;
            var info = new Rect(W - infoW - 24f, bodyTop, infoW, bodyBot - bodyTop);
            Ui.Box(info);
            Ui.Fill(new Rect(info.x + 12f, info.y + 12f, 12f, 12f), TankShape.BodyColor(k));
            Ui.Text(new Rect(info.x + 32f, info.y + 6f, infoW - 44f, 26f), st.Name, 20, Ui.Ink, TextAnchor.MiddleLeft, true);
            Ui.Text(new Rect(info.x + 32f, info.y + 30f, infoW - 44f, 16f),
                    TankStats.EraName(TankStats.EraOf(k)), 11, Ui.Power, TextAnchor.MiddleLeft);

            float sy = info.y + 56f;
            void Stat(string label, float frac, Color col, string val)
            {
                Ui.Text(new Rect(info.x + 14f, sy, 52f, 16f), label, 10, Ui.Dim, TextAnchor.MiddleLeft);
                Ui.Bar(new Rect(info.x + 70f, sy + 4f, infoW - 150f, 9f), Mathf.Clamp01(frac), col, null, Ui.Border);
                Ui.Text(new Rect(info.xMax - 76f, sy, 62f, 16f), val, 10, Ui.Ink, TextAnchor.MiddleRight);
                sy += 22f;
            }
            Stat("체력", StatBars.Hp.Norm(st.Hp), Ui.Good, $"{st.Hp}");
            Stat("방어", StatBars.Def.Norm(st.Defense), Ui.Gauge, $"{st.Defense:F0}");
            Stat("사거리", StatBars.Range.Norm(st.MaxRange), Ui.Power, $"{st.MaxRange:F0}m");
            Stat("속도", StatBars.Delay.NormInv(st.Delay), Ui.Warn, $"딜레이 {st.Delay}");
            Stat("정확도", StatBars.Acc.NormInv(Spread.AimJitterDeg(k)), Ui.Mark, $"±{Spread.AimJitterDeg(k):F2}°");

            sy += 6f;
            Ui.Fill(new Rect(info.x + 14f, sy, infoW - 28f, 1f), Ui.Border); sy += 10f;
            Ui.Text(new Rect(info.x + 14f, sy, infoW - 28f, 18f), $"2번탄 · {st.SpecialName}", 13, Ui.Warn, TextAnchor.MiddleLeft, true);
            sy += 20f;
            // ⚠️ `SpBlast`/`SpBase` 는 **배율**이다 — 그대로 찍으면 전 기종이 "1" 로 보인다.
            //    실제 수치는 탄종을 적용한 `TankStats.For` 만 안다.
            var sp = TankStats.For(k, ShellKind.Special, 1f, Weather.Clear);
            Ui.Text(new Rect(info.x + 14f, sy, infoW - 28f, 16f),
                    $"폭발 {sp.BlastRadius:F1}m · 피해 {sp.BaseDamage:F0} · 각도 {st.MinPitch:F0}~{st.MaxPitch:F0}°", 10, Ui.Dim, TextAnchor.MiddleLeft);

            // ── 아래: 13종 풀 ────────────────────────────────
            int n = TankStats.Count;
            float cw = (W - 48f) / PickCols, chh = 40f;
            float gy = H - poolH - 24f;
            Ui.Box(new Rect(24f, gy - 24f, W - 48f, poolH + 18f));
            Ui.Text(new Rect(34f, gy - 20f, 200f, 16f), "기종", 11, Ui.Dim, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < n; i++)
            {
                var kk = (TankKind)i;
                var r = new Rect(24f + (i % PickCols) * cw + 4f, gy + (i / PickCols) * (chh + 4f) + 4f, cw - 8f, chh);
                bool cur = i == _pickCursor;
                int idx = _picked.IndexOf(kk);
                Ui.Fill(r, idx >= 0 ? new Color(0.15f, 0.48f, 0.98f, 0.22f) : Ui.Panel);
                Ui.Frame(r, cur ? Ui.Power : idx >= 0 ? Ui.Ally : Ui.Border, cur ? 2f : 1f);
                Ui.Fill(new Rect(r.x + 7f, r.y + 7f, 9f, r.height - 14f), TankShape.BodyColor(kk));
                Ui.Text(new Rect(r.x + 22f, r.y, r.width - 50f, r.height),
                        TankStats.Get(kk).Name, 13, cur || idx >= 0 ? Ui.Ink : Ui.Dim, TextAnchor.MiddleLeft, cur);
                if (idx >= 0)
                {
                    var badge = new Rect(r.xMax - 22f, r.y + 10f, 16f, 16f);
                    Ui.Fill(badge, Ui.Ally);
                    Ui.Text(badge, $"{idx + 1}", 10, Ui.Ink, TextAnchor.MiddleCenter, true);
                }
            }

            if (_picked.Count == MapHeightFunction.TeamSize)
                Ui.TextShadow(new Rect(0, H - 22f, W, 20f), "Enter = 전투 설정으로", 14, Ui.Mark, TextAnchor.MiddleCenter, true);
            else
                Ui.TextShadow(new Rect(0, H - 22f, W, 20f),
                              $"{MapHeightFunction.TeamSize - _picked.Count}대 더 고르세요", 13, Ui.Dim, TextAnchor.MiddleCenter);
        }

        /// <summary>
        /// 스탯 막대의 눈금. **기종 표에서 실제 최소·최대를 뽑아** 쓴다.
        ///
        /// ⚠️ 예전엔 `st.Hp / 1200f` 처럼 상수를 손으로 박아 뒀다. 밸런스가 수치를 옮기자마자
        ///    눈금이 낡아 두 가지로 거짓말을 했다(2026-09-17 실측):
        ///      · **넘쳐서 잘렸다** — 슈퍼탱크 사거리 291m ÷ 260 = 1.12 → Clamp01 이 1.0 으로 자른다.
        ///        13종 중 1등인 게 "그냥 꽉 참"으로 보여 2·3등과 구별이 안 됐다.
        ///      · **차이가 안 보였다** — 체력은 0.77~0.96, 즉 막대 폭의 **19%** 안에서만 움직였다.
        ///        13종을 나란히 놓고 고르라는 화면인데 전부 같은 길이로 보인다.
        ///    그래서 상수를 없앤다. 눈금이 표를 따라오므로 밸런스가 수치를 바꿔도 다시 안 낡는다
        ///    (§2-9-1 "같은 값을 여러 곳에 적으면 반드시 어긋난다").
        /// </summary>
        readonly struct StatScale
        {
            readonly float _min, _span;
            public StatScale(float min, float max) { _min = min; _span = Mathf.Max(max - min, 1e-4f); }

            // 최약체도 **막대가 보여야** "값이 0"과 구별된다 — 바닥을 0.10 으로 둔다.
            public float Norm(float v)    => Mathf.Lerp(0.10f, 1f, Mathf.Clamp01((v - _min) / _span));
            public float NormInv(float v) => Mathf.Lerp(0.10f, 1f, 1f - Mathf.Clamp01((v - _min) / _span));
        }

        static class StatBars
        {
            public static readonly StatScale Hp, Def, Range, Delay, Acc;

            static StatBars()
            {
                float hpL = float.MaxValue, hpH = float.MinValue, dfL = float.MaxValue, dfH = float.MinValue;
                float rgL = float.MaxValue, rgH = float.MinValue, dlL = float.MaxValue, dlH = float.MinValue;
                float acL = float.MaxValue, acH = float.MinValue;
                for (int i = 0; i < TankStats.Count; i++)
                {
                    var s = TankStats.Get((TankKind)i);
                    hpL = Mathf.Min(hpL, s.Hp);       hpH = Mathf.Max(hpH, s.Hp);
                    dfL = Mathf.Min(dfL, s.Defense);  dfH = Mathf.Max(dfH, s.Defense);
                    rgL = Mathf.Min(rgL, s.MaxRange); rgH = Mathf.Max(rgH, s.MaxRange);
                    dlL = Mathf.Min(dlL, s.Delay);    dlH = Mathf.Max(dlH, s.Delay);
                    float j = Spread.AimJitterDeg((TankKind)i);
                    acL = Mathf.Min(acL, j);          acH = Mathf.Max(acH, j);
                }
                Hp = new StatScale(hpL, hpH); Def = new StatScale(dfL, dfH);
                Range = new StatScale(rgL, rgH); Delay = new StatScale(dlL, dlH);
                Acc = new StatScale(acL, acH);
            }
        }

        // 🗑️ `StatRow(card, row, label, frac, col)` 을 2026-09-19 에 지웠다. 호출부 0곳이었다.
        //    **옛 결과 화면 레이아웃의 잔재**다 — 지금 결과 화면(`DrawResult`)은 팀별 열로 직접 그린다.
        //    ⚠️ 살려 두면 «막대형 스탯 행 경로가 아직 있다»고 믿게 만든다.
        //       「둘 다 살려두면 먼저 그리는 쪽이 이긴다」 계열이라 남길 이유가 없었다(옛 `DrawPicker` 사고).

        // ── 전투 설정(맵·난이도·아이템·날씨·Boom 모드) ──────────
        /// <summary>
        /// 설정 행의 **이름·설명**. 값은 지금 상태에 따라 달라지니 <see cref="DrawSetup"/> 에서 만든다.
        /// 여기 두는 이유는 <see cref="SetupInput"/> 의 커서 상한이 이 표를 세게 하기 위해서다 —
        /// 행 수를 두 곳에 적으면 행을 늘릴 때 커서가 마지막 행에 못 간다(§2-9-1 "같은 값을 여러 곳에").
        /// </summary>
        static readonly string[,] SetupRows =
        {
            { "맵",        "지형이 사거리·엄폐를 바꾼다" },
            { "AI 난이도", "조준 오차 — 사다리 검증됨(§2-9-8)" },
            { "아이템",    "판 시작에 무작위로 받는다" },
            { "날씨",      "눈이면 포세이돈이 강해진다" },
            { "Boom 모드", "지뢰밭 + 지진·유성(§2-9-16)" },
            { "소리",      "" },   // 설명 칸은 SoundSummary() 가 실시간으로 채운다
        };
        // ⚠️ 행 번호를 숫자로 적지 마라. 소리 행을 뒤에 붙이자 "마지막 행 = Boom" 가정이 깨졌다(자체검사 커서).
        const int SetupRowBoom = 4, SetupRowSound = 5;

        void DrawSetup(float W, float H)
        {
            Scrim(W, H, 0.62f);
            Ui.TextShadow(new Rect(0, H * 0.18f, W, 30f), "전투 설정", 22, Ui.Ink, TextAnchor.MiddleCenter, true);

            string weather = _weatherForced == null ? "자동 (25% 눈)" : _weatherForced == Weather.Clear ? "맑음" : "눈";
            var values = new[]
            {
                MapHeightFunction.Name(_map),
                Difficulties[_difficulty].Name,
                _itemSlots == 0 ? "없음" : $"{_itemSlots}개",
                weather,
                _boom ? "켬" : "끔",
                // 🚨 **여기서 값을 고치지 않는다 — 보여주기만 한다.** 소리의 진짜 소스는 설정 화면 하나다.
                //    같은 값을 두 화면에서 고치게 두면 "먼저 그리는 쪽이 이기는" 사고가 난다(옛 DrawPicker).
                // ⚠️ 값 칸(180px)에 요약을 통째로 넣었더니 **끝이 잘렸다**(실측: "…음악" 에서 끊김).
                //    값 칸은 «무슨 일이 일어나는가»(열기)만, 실제 상태는 아래 설명 칸에 건다.
                "열기",
            };

            int rows = SetupRows.GetLength(0);
            float y = H * 0.30f;
            for (int i = 0; i < rows; i++)
            {
                var r = new Rect(W * 0.5f - 250f, y + i * 48f, 500f, 40f);
                bool sel = i == _setupSel;
                Ui.Fill(r, sel ? new Color(1f, 0.72f, 0.25f, 0.14f) : Ui.Panel);
                Ui.Frame(r, sel ? Ui.Power : Ui.Border, sel ? 2f : 1f);
                Ui.Text(new Rect(r.x + 16f, r.y, 150f, r.height), SetupRows[i, 0], 14, sel ? Ui.Ink : Ui.Dim, TextAnchor.MiddleLeft, sel);
                Ui.Text(new Rect(r.x + 160f, r.y, 180f, r.height), (sel ? "< " : "  ") + values[i] + (sel ? " >" : ""),
                        14, sel ? Ui.Power : Ui.Ink, TextAnchor.MiddleCenter, sel);
                // 소리 행만 설명 칸이 **살아 있는 값**이다(나머지는 고정 안내문).
                Ui.Text(new Rect(r.x + 350f, r.y, 150f, r.height),
                        i == SetupRowSound ? SoundSummary() : SetupRows[i, 1], 9, Ui.Dim);
            }

            // 상자 폭을 인원에서 유도한다(130f 간격 × 인원 + 여백) — 넷째 이름이 상자 밖으로 삐져나갔던 자리.
            float teamW = 100f + MapHeightFunction.TeamSize * 130f;
            var team = new Rect(W * 0.5f - teamW * 0.5f, y + rows * 48f + 12f, teamW, 44f);
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
            string[] lines =
            {
                "Esc / Enter — 계속",
                $"M — 음악 {(Sfx.MusicOff ? "끔" : "켬")}     N — 효과음 {(Sfx.SfxOff ? "끔" : "켬")}",
                $"O — 소리 설정 (음량 {Mathf.RoundToInt(Sfx.Volume * 100f)}%)",
                "T — 타이틀로", "Q — 종료",
            };
            // 🚨 **패널 없이 글자만 띄우고 있었다**(2026-09-19, 화면 6종 감사). 일시정지는 **전투 HUD 가
            //    그대로 살아 있는 화면**이라(팀 패널·턴 순서·미니맵·파워 게이지) 글자가 그 위에 겹쳐
            //    탱크·체력바와 섞였다. 스크림(0.60)만으로는 **전장이 아니라 UI 가** 배경이라 안 죽는다.
            //    결과 화면은 같은 상황에서 `Ui.Box` 를 깐다 — **같은 문제에 같은 해법을 쓴다.**
            // ⚠️ 높이는 줄 수에서 계산한다. 고정값으로 두면 줄이 늘 때 마지막 줄이 상자 밖으로 나간다
            //    (결과 화면이 「190 고정」으로 똑같이 한 번 겪은 자리다).
            float bh = 58f + lines.Length * 28f + 18f;
            var r = new Rect(W * 0.5f - 240f, H * 0.34f - 14f, 480f, bh);
            Ui.Box(r);
            Ui.TextShadow(new Rect(r.x, r.y + 8f, r.width, 44f), "일시정지", 32, Ui.Ink, TextAnchor.MiddleCenter, true);
            for (int i = 0; i < lines.Length; i++)
                Ui.TextShadow(new Rect(r.x, r.y + 58f + i * 28f, r.width, 24f), lines[i], 15, Ui.Dim, TextAnchor.MiddleCenter);
        }

        // ── 결과(§11 M3 게이트: 한 판 길이 실측) ────────────────
        void DrawResult(float W, float H)
        {
            Scrim(W, H, 0.66f);
            bool win = _winner != null && _winner.Contains("아군");
            bool draw = _winner != null && _winner.Contains("무승부");
            Ui.TextShadow(new Rect(0, H * 0.16f, W, 64f), draw ? "무승부" : win ? "승리" : "패배", 52, ResultColor(), TextAnchor.MiddleCenter, true);
            string sub = _winner ?? "";
            if (!_practice) { string rec = Prefs.TotalText(Difficulties.Length); if (rec.Length > 0) sub += "   ·   " + rec; }
            Ui.TextShadow(new Rect(0, H * 0.16f + 62f, W, 24f), sub, 14, Ui.Dim, TextAnchor.MiddleCenter);

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
                            v = $"{alive}/{MapHeightFunction.TeamSize}"; break;
                    }
                    Ui.Text(new Rect(r.x + 180f + t * 200f, y, 180f, 24f), v, 14,
                            t == 0 ? Ui.Ally : Ui.Enemy, TextAnchor.MiddleCenter, true);
                }
            }

            Ui.Text(new Rect(r.x + 20f, r.yMax - 44f, r.width - 40f, 20f),
                    $"라운드 {_round}   ·   한 판 {_battleClock / 60f:F1}분   ·   {MapHeightFunction.Name(_map)} · {WeatherName(_weather)} · AI {Difficulties[_difficulty].Name}",
                    11, Ui.Dim, TextAnchor.MiddleCenter);

            // 턴 시간 실측(2026-09-19) — 명세 §2-5-0 의 "분" 열이 통째로 매달린 **18초/턴 가정**을
            // 사람 판이 공짜로 검증한다. 그래서 결과 화면에 띄운다(로그에만 두면 아무도 안 본다).
            // ⚠️ 이건 **측정값**이지 목표가 아니다. 이 숫자를 보고 페이즈 초를 고치지 마라(밸런스).
            {
                float allAvg = (_humanTurns + _aiTurns) > 0 ? (_humanSec + _aiSec) / (_humanTurns + _aiTurns) : 0f;
                var pace = Prefs.TurnPace();
                // 🚨 **`0.0초 (0턴)` 로 찍히고 있었다**(2026-09-19, 이 줄을 처음 사진으로 보고 발견).
                //    사람이 한 턴도 안 쐈으면 그건 **「0.0초가 걸렸다」가 아니라 「잰 적이 없다」**다.
                //    숫자를 0 으로 보여 주면 화면이 **측정값인 척 거짓말을 한다** — §2-5-0 의 18초/턴 가정을
                //    닫으려고 띄운 줄인데, 하필 그 줄이 «없음»을 «0» 으로 말하면 근거가 오염된다.
                // ⚠️ 빈 상태는 자동사격·AI 대전에서 **항상** 나온다. 숫자가 있는 사진만 보면 영영 안 걸린다
                //    (그래서 `-uiselftest` 에 「결과_무승부」를 **턴 기록 없음**으로 찍는 칸을 만들었다).
                string line = _humanTurns > 0
                    ? $"내 턴 평균 {_humanSec / _humanTurns:F1}초 ({_humanTurns}턴)   ·   전체 턴 평균 {allAvg:F1}초"
                    : $"내 턴 기록 없음 — 사람이 쏜 턴이 없다   ·   AI 턴 평균 {allAvg:F1}초";
                if (pace.Games > 1) line += $"   ·   통산 {pace.Games}판 내 턴 {pace.Human:F1}초 · 전체 {pace.All:F1}초";
                Ui.Text(new Rect(r.x + 20f, r.yMax - 26f, r.width - 40f, 20f), line, 11, Ui.Mark, TextAnchor.MiddleCenter);
            }

            Ui.TextShadow(new Rect(0, r.yMax + 22f, W, 24f), "R — 다시   T — 타이틀   Q — 종료", 15, Ui.Ink, TextAnchor.MiddleCenter, true);
        }

        // ══════════════════════════════════════════════════════
        //  전투 중 추가 위젯
        // ══════════════════════════════════════════════════════

        /// <summary>피해 숫자를 맞은 자리에 띄운다. 카메라 뒤는 그리지 않는다(뒤집혀 나온다).</summary>
        // ── 탱크 머리 위 체력(원작 방식) ─────────────────────────
        // 원작(포트리스)은 **체력이 탱크에 붙어 있다.** 이 게임은 구석 팀 패널에만 있어서
        // "화면의 저 탱크가 지금 몇 남았나"를 알려면 이름을 패널에서 찾아 짝지어야 했다 —
        // 3D 라 탱크가 흩어져 있어 더 안 맞는다. 맞은 자리에 숫자가 뜨는데(팝업) 정작 **남은 양**은 없었다.
        //
        // ⚠️ 겹침이 이 UI 의 유일한 실패 모드다. 8대가 몰리면 바가 서로를 덮는다 —
        //    거리로 크기를 줄이고, 화면 밖·카메라 뒤는 건너뛰고, 죽은 탱크는 안 그린다.
        // ⚠️ `_hudOff`(스크린샷 비교용 HUD 끄기)를 따라야 한다 — 호출부가 그 뒤에 있다.
        void DrawTankHpBars()
        {
            if (_cam == null) return;
            var camPos = _cam.transform.position;
            foreach (var u in _units)
            {
                if (!u.Alive) continue;
                var head = u.Pos + Vector3.up * 3.1f;
                var sp = _cam.WorldToScreenPoint(head);
                if (sp.z <= 0f) continue;                                  // 카메라 뒤
                float d = Vector3.Distance(camPos, u.Pos);
                if (d > 320f) continue;                                    // 너무 멀면 점만 찍히므로 생략
                float k = Mathf.Clamp01(1f - (d - 60f) / 260f);            // 멀수록 작게
                float w = Mathf.Lerp(46f, 86f, k), h = Mathf.Lerp(5f, 8f, k);
                float x = sp.x - w * 0.5f, y = Screen.height - sp.y;

                bool cur = u == Current;
                var team = u.Team == 0 ? Ui.Ally : Ui.Enemy;
                float f = u.HpFrac;

                // 이름 — 원작의 닉네임 자리. 누구 것인지 색과 글자 둘 다로 말한다.
                if (k > 0.35f)
                    Ui.TextShadow(new Rect(x - 30f, y - Mathf.Lerp(12f, 17f, k), w + 60f, 14f),
                                  $"{(u.Team == 0 ? "아군" : "적군")}{u.Id % MapHeightFunction.TeamSize + 1}",
                                  Mathf.RoundToInt(Mathf.Lerp(9f, 11f, k)), cur ? Ui.Ink : team,
                                  TextAnchor.MiddleCenter, cur);

                var bar = new Rect(x, y, w, h);
                Ui.Fill(new Rect(bar.x - 1f, bar.y - 1f, bar.width + 2f, bar.height + 2f), new Color(0f, 0f, 0f, 0.55f));
                Ui.Bar(bar, f, Ui.HpColor(f), null, team);
                // 지금 턴인 탱크는 테를 한 겹 더 — 카메라가 따라가도 "내 차례"가 화면에서 읽혀야 한다
                if (cur) Ui.Frame(new Rect(bar.x - 3f, bar.y - 3f, bar.width + 6f, bar.height + 6f), Ui.Ink);

                // 숫자는 가까울 때만 — 멀리서도 찍으면 글자가 서로 겹쳐 읽을 수 없다.
                // ⚠️ **바 아래는 피해 팝업이 떠오르는 길목이다**(AddPopup 이 탱크에서 위로 띄운다).
                //    거기 두면 맞는 순간 남은 체력이 숫자에 가려 안 보인다 — 실제로 겹쳤다. 왼쪽에 둔다.
                if (k > 0.55f)
                    Ui.TextShadow(new Rect(x - 46f, y - 3f, 42f, 14f), $"{u.Hp}", 11, Ui.Ink, TextAnchor.MiddleRight, true);

                // 상태 표시 — 팀 패널과 같은 규칙(독·속박·방해). 여기 있으면 전장에서 바로 읽힌다.
                if (k > 0.5f)
                {
                    float sx = bar.xMax + 4f;
                    void Chip(string t2, Color c2)
                    {
                        var r2 = new Rect(sx, y - 2f, 13f, 13f);
                        Ui.Fill(r2, new Color(c2.r, c2.g, c2.b, 0.85f));
                        Ui.Text(r2, t2, 9, Ui.Ink, TextAnchor.MiddleCenter, true);
                        sx += 14f;
                    }
                    if (_status.DotTurnsLeft(u.Id) > 0) Chip("독", Ui.Warn);
                    if (_status.RootTurnsLeft(u.Id) > 0 || !_status.CanMove(u.Id)) Chip("속", Ui.Bad);
                    if (_items.HasShield(u.Id)) Chip("실", Ui.Gauge);
                }
            }
        }

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
        /// 미니맵(§59). 3D 라 화면 밖의 적이 어디 있는지가 안 보인다 — 탑다운으로 준다.
        ///
        /// 🚨 **여기 「지형 높이는 그리지 않는다 — §58 의 우회로가 된다」고 적혀 있었다.**
        ///    그런데 **이 주석이 자기가 인용한 규칙보다 엄격했다**(2026-09-19 확인).
        ///    §58 이 실제로 금지하는 셋은 **「예상 탄착 마커 · 전체 궤적선 · 자동 보정선」**이고,
        ///    §2-6 은 그 충돌을 절충하며 **거리계에 `↕ 고도차`를 이미 «수치로» 허용**한다.
        ///    ⇒ 상위 문서(기획서 §2-6)가 코드 주석을 이긴다. 실루엣은 금지 목록에 없다.
        ///
        /// **여기서 허용되는 것**: 채도 죽인 **실루엣·음영**(어디가 높고 어디가 파였는지의 «대략»).
        ///   사람은 이미 화면으로 지형을 본다. 탑다운 실루엣은 **사격 해가 아니다** —
        ///   각도·파워·바람은 여전히 사람 몫이라 이걸로는 한 발도 못 푼다.
        ///   지형을 안 보여 주는 건 정보 은폐가 아니라 **미니맵이 쓸모없는 것**이고,
        ///   §59 가 요구한 건 «쓸모 있는 탑다운»이다.
        /// 🚫 **금지**: 숫자 고도 · **등고선처럼 «읽어서 계산할 수 있는» 형태**(그래서 밴딩 없이
        ///    **부드럽게** 칠한다 — 층이 지면 그게 등고선이다) · 예상 탄착 마커 · 궤적선 · 보정선.
        ///    §2-6 이 수치로 주기로 한 것은 **거리계 하나뿐**이다.
        /// </summary>
        void DrawMiniMap(float W, float H)
        {
            const float S = 132f;
            // ⚠️ 아래 여백 128 → 152 (2026-09-19). 무기 패널이 **2번탄 설명 두 줄만큼** 커져서
            //    그대로 두면 미니맵 아랫변과 겹쳤다. 두 값은 서로를 보고 있다 —
            //    무기 패널 높이(`HudWeapons` 의 134)를 바꾸면 여기도 같이 봐라.
            var r = new Rect(W - S - 10f, H - S - 152f, S, S);
            // ⚠️ 기본 패널(알파 0.78)은 여기선 너무 비친다 — 나무·언덕이 통과해 보여 점과 섞였다.
            //    미니맵은 **화면에서 유일한 전장 개관**이라 배경이 조용해야 한다.
            Ui.Box(r, new Color(0.03f, 0.05f, 0.07f, 0.95f));
            // 지형 실루엣을 깔고 그 위에 어둠을 한 겹 덮는다 — **실루엣은 «바닥»이지 «주인공»이 아니다.**
            // 이 한 겹이 없으면 지형 대비가 유닛 점과 싸운다(§59 가 요구한 건 유닛 개관이다).
            var mini = MiniTerrain();
            if (mini != null)
            {
                var prev = GUI.color;
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), mini);
                GUI.color = prev;
                // ⚠️ 이 어둠은 **0.20 이다.** 처음 0.34 로 덮었더니 지형은 물론 **파낸 자리까지 같이 지워졌다** —
                //    계측에선 30칸이 잡히는데 화면엔 아무것도 없었다(「기능이 있는데 화면에 없는 것」).
                //    유닛이 튀는 데 필요한 건 «지형을 어둡게»지 «전부 어둡게»가 아니다.
                Ui.Fill(r, new Color(0.02f, 0.03f, 0.05f, 0.20f));
            }
            Ui.Text(new Rect(r.x + 6f, r.y + 2f, 80f, 14f), "미니맵", 9, Ui.Dim);

            // ⚠️ 3D 라 미니맵이 **유일한 전장 개관**이다. 예전엔 유닛과 상자만 찍어서
            //    화면 밖 지뢰·불·회오리가 어디 있는지 알 길이 아예 없었다 — 위험물부터 그린다.
            //    순서가 중요하다: 위험물 → 상자 → 유닛(중요한 것이 위에 덮이게).
            // 🚨 **미니맵 색은 팀 색과 겹치면 안 된다**(2026-09-19 검수에서 걸렸다).
            //    회오리가 `Gauge`(하늘색)라 **아군 파랑과**, 지뢰가 `Bad`(빨강)라 **적군 빨강과** 헷갈렸다.
            //    점 하나를 적으로 잘못 읽으면 판단이 통째로 틀어진다. 그래서 규칙을 고정한다:
            //      파랑·빨강 = 탱크(오직 이것만) · 초록 = 이로운 것(보급) · 노랑 = 위험물 · 주황 선 = 증폭벽 · 흰 테 = 회오리
            foreach (var w in _air.Walls)                      // 증폭벽 — z 축으로 뻗은 주황 선
            {
                var a = MapToMini(r, w.X, w.Z - w.HalfLen);
                var b = MapToMini(r, w.X, w.Z + w.HalfLen);
                Ui.Fill(new Rect(a.x - 1f, Mathf.Min(a.y, b.y), 2f, Mathf.Abs(b.y - a.y)), Ui.Power);
            }
            foreach (var t in _air.Tornadoes)                  // 회오리 — 흰 테(팀색이 아닌 것으로)
            {
                var p = MapToMini(r, t.X, t.Z);
                float rr = Mathf.Max(3f, t.Radius / MapSize * r.width);
                Ui.Frame(new Rect(p.x - rr, p.y - rr, rr * 2f, rr * 2f), Ui.Ink);
            }
            _hazards.Snapshot(_hazardBuf);
            foreach (var h in _hazardBuf)
            {
                var p = MapToMini(r, h.X, h.Z);
                // 지뢰(Kind 0)는 점, 장판(불·독)은 반경만큼의 네모 — 둘 다 노랑(위험)
                if (h.Kind == 0) Ui.Fill(new Rect(p.x - 1.5f, p.y - 1.5f, 3f, 3f), Ui.Warn);
                else
                {
                    float rr = Mathf.Max(2f, h.Radius / MapSize * r.width);
                    var c2 = Ui.Warn; c2.a = 0.45f;
                    Ui.Fill(new Rect(p.x - rr, p.y - rr, rr * 2f, rr * 2f), c2);
                }
            }
            foreach (var c in _supply.Crates)                  // 보급 — 초록(이로운 것)
            {
                var p = MapToMini(r, c.X, c.Z);
                Ui.Fill(new Rect(p.x - 2f, p.y - 2f, 4f, 4f), Ui.Good);
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

        // ── 미니맵 지형 실루엣 ────────────────────────────────────────────────────
        // ⚠️ **매 프레임 만들면 안 된다**(64×64 = 4096 샘플). `TerrainView.RebuildCount` 가
        //    바뀔 때만 다시 굽는다 — 「몇 초마다」로 재면 **안 바뀌었을 때 갈고 바뀐 직후엔 늦는다.**
        const int MiniN = 64;
        Texture2D _miniTex;
        MapKind _miniTexMap;
        int _miniTexRebuild = -1;

        Texture2D MiniTerrain()
        {
            int rc = _terrain != null ? _terrain.RebuildCount : 0;
            if (_miniTex != null && _miniTexMap == _map && _miniTexRebuild == rc) return _miniTex;

            if (_miniTex == null)
                _miniTex = new Texture2D(MiniN, MiniN, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,      // 부드럽게 — 층이 지면 그게 등고선이다(위 🚫)
                    wrapMode = TextureWrapMode.Clamp,
                };
            _miniTexMap = _map; _miniTexRebuild = rc;

            float cell = MapSize / (MiniN - 1);
            var h = new float[MiniN * MiniN];
            // ⚠️ 높이 범위를 **맵마다 다시 구한다.** 고정 범위로 정규화하면 고저차가 작은 맵은
            //    통째로 한 색이 되고 큰 맵은 양끝이 뭉갠다 — 여섯을 한 번에 보고서야 알게 되는 종류다.
            float lo = float.MaxValue, hi = float.MinValue;
            for (int j = 0; j < MiniN; j++)
                for (int i = 0; i < MiniN; i++)
                {
                    float v = MapHeightFunction.Height(_map, i * cell, j * cell);
                    h[j * MiniN + i] = v;
                    if (v < lo) lo = v;
                    if (v > hi) hi = v;
                }
            float span = Mathf.Max(1f, hi - lo);

            var px = new Color32[MiniN * MiniN];
            int dug = 0;
            for (int j = 0; j < MiniN; j++)
                for (int i = 0; i < MiniN; i++)
                {
                    float t = (h[j * MiniN + i] - lo) / span;
                    // 경사 음영 — 「어디가 비탈인가」가 실루엣을 읽히게 한다. 등고선이 아니라 **연속 음영**이다.
                    float hx = h[j * MiniN + Mathf.Min(i + 1, MiniN - 1)] - h[j * MiniN + Mathf.Max(i - 1, 0)];
                    float hz = h[Mathf.Min(j + 1, MiniN - 1) * MiniN + i] - h[Mathf.Max(j - 1, 0) * MiniN + i];
                    float shade = Mathf.Clamp01(0.5f + (hx + hz) * 0.05f);

                    // 🚨 **채도를 죽인 회청색만 쓴다.** 팀색(파랑·빨강)·보급(초록)·위험물(노랑)과
                    //    색상환에서 겹치면 점 하나를 적으로 잘못 읽는다 — 이미 한 번 걸린 자리다.
                    var c = Color.Lerp(new Color(0.09f, 0.12f, 0.15f), new Color(0.40f, 0.44f, 0.48f), t);
                    c *= 0.74f + shade * 0.44f;

                    // 파낸 자리 — 원래 지표 1.5m 아래가 «공기»면 파인 것이다(SDF 는 >0 이 바깥).
                    // 기둥을 훑지 않고 **한 점만** 본다: 미니맵에 필요한 건 깊이가 아니라 «파였다» 하나다.
                    if (_vol != null && _vol.SampleWorld(i * cell, h[j * MiniN + i] - 1.5f, j * cell) > 0f)
                    // ⚠️ 섞는 양이 크다(0.88) — 파인 칸은 **한 판에 30/4096 = 0.7%** 뿐이라
                    //    약하게 섞으면 2px 짜리가 배경에 먹힌다. 대신 **색상은 계속 저채도 흙색**이라
                    //    팀색(파랑·빨강)·보급(초록)·위험물(노랑) 어느 것과도 안 헷갈린다.
                    { c = Color.Lerp(c, new Color(0.52f, 0.35f, 0.22f), 0.88f); dug++; }

                    c.a = 1f;
                    px[j * MiniN + i] = c;
                }
            _miniTex.SetPixels32(px);
            _miniTex.Apply(false);
            // 「파낸 자리」가 실제로 몇 칸이나 잡혔는지 — **0 이면 기능이 없는 것과 같다.**
            // 눈으로 「안 보이는 것 같다」로 끝내지 않으려고 숫자로 남긴다.
            if (_autoMode) Debug.Log($"[Tankfall] 미니맵 재생성 rc={rc} · 파낸칸 {dug}/{MiniN * MiniN} · 높이 {lo:F1}~{hi:F1}m");
            return _miniTex;
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

            // ⚠️ Y 는 좌상단 팀 패널 **아래**여야 한다. 예전엔 그 높이(`26f * 6f`)를 손으로 베껴 뒀는데,
            //    인원이 4:4 로 늘자 팀 패널만 길어지고 이 패널은 안 내려와 **두 패널이 2줄 겹쳤다.**
            //    베끼지 말고 같은 식으로 유도한다(BattleDemo.HudTeamPanel 과 같은 상수를 본다).
            var r = new Rect(8f, 8f + 12f + 26f * (MapHeightFunction.TeamSize * 2) + 19f + 6f, 250f, 26f + alive.Count * 18f);
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
            int root = _status.RootTurnsLeft(u.Id);
            if (root > 0 || !_status.CanMove(u.Id)) StatusChip(ref x, under.y, root > 0 ? $"속박 {root}" : "속박", Ui.Bad);
            int dot = _status.DotTurnsLeft(u.Id);
            if (dot > 0) StatusChip(ref x, under.y, $"독 {_status.DotPerTurn(u.Id)}×{dot}", Ui.Warn);
            if (_items.HasShield(u.Id)) StatusChip(ref x, under.y, "실드", Ui.Gauge);
            foreach (ImpairKind k in System.Enum.GetValues(typeof(ImpairKind)))
            {
                int t = _impair.TurnsLeft(u.Id, k);
                if (t > 0) StatusChip(ref x, under.y, $"{Impair.Name(k)} {t}", Ui.Mark);
            }
        }

        /// <summary>
        /// 팀 패널 한 줄 오른쪽 끝의 작은 상태 표시. **적 것도 보여야 한다** —
        /// 예전엔 현재 턴 유닛에게만, 그것도 속박·실드 두 개만 그려서 "적이 중독인지"를
        /// 알 방법이 아예 없었다(Sim 은 남은 턴·턴당 피해를 다 알고 있는데 UI 가 안 읽었다).
        /// </summary>
        void DrawRowStatus(Unit u, Rect row)
        {
            float x = row.x, w = 0f;
            void Dot(string s, Color c)
            {
                var r = new Rect(row.xMax - 54f - w, row.y + 5f, 14f, 14f);
                Ui.Fill(r, new Color(c.r, c.g, c.b, 0.85f));
                Ui.Text(r, s, 9, Ui.Ink, TextAnchor.MiddleCenter, true);
                w += 16f;
            }
            _ = x;
            if (_status.DotTurnsLeft(u.Id) > 0) Dot("독", Ui.Warn);
            if (_status.RootTurnsLeft(u.Id) > 0 || !_status.CanMove(u.Id)) Dot("속", Ui.Bad);
            foreach (ImpairKind k in System.Enum.GetValues(typeof(ImpairKind)))
                if (_impair.TurnsLeft(u.Id, k) > 0) { Dot("방", Ui.Mark); break; }
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
        // 🚨 **결과 화면이 「아군 승리」 하나만 찍히고 있었다**(2026-09-19). 자동사격은 거의 항상 이기고
        //    끝나서 **패배·무승부는 이 게이트가 한 번도 안 덮었다** — 「가장 덜 다듬어졌을 자리를
        //    가장 안 보고 있었던」 셈이다. 셋을 다 찍는다.
        //    ⚠️ 셋은 **같은 `DrawResult`** 를 쓴다(제목·색만 갈린다). 그래도 따로 찍는 이유는
        //       «색이 실제로 갈리는가»와 «제목이 안 잘리는가»를 사진이 덮게 하려는 것이다.
        { "타이틀", "조작법", "설정", "탱크선택", "전투설정", "전투HUD", "파워게이지", "일시정지",
          "결과_승리", "결과_패배", "결과_무승부" };

        /// <summary>단계별로 화면 상태를 만든다. 실제 화면 코드를 그대로 쓴다 — 별도 그리기를 만들면 검사가 거짓이 된다.</summary>
        void UiSelfTestSetup(int step)
        {
            _showHelp = false;
            // Boom 모드 행이 **그려지는지**를 스크린샷이 실제로 덮게 한다 — 커서를 그 행에 올리고 켠 상태로 찍는다.
            // (설정에 없어서 `-boom` 으로만 켤 수 있던 걸 노출한 변경. 화면에 안 나오면 다시 도달 불가가 된다.)
            _boom = step == 4;
            switch (step)
            {
                case 0: _screen = GameScreen.Title; _menuSel = 0; break;
                case 1: _screen = GameScreen.Title; _menuSel = 3; _showHelp = true; break;   // TitleMenu[3]="조작법"(설정 삽입으로 인덱스 이동)
                case 2: OpenSettings(GameScreen.Title); break;
                case 3:
                    _screen = GameScreen.TankSelect;
                    _picked.Clear();
                    // 인원만큼 채운다 — 덜 채우면 게이트 사진이 "빈 슬롯"으로 남아 화면이 틀린 줄 알게 된다.
                    foreach (var dk in DefaultRoster) _picked.Add(dk);
                    _pickCursor = (int)TankKind.Poseidon;
                    break;
                case 4: _screen = GameScreen.Setup; _setupSel = SetupRowBoom; break;   // Boom 모드 행에 커서
                case 5:
                    _screen = GameScreen.Battle;
                    _phase = Phase.Move; _phaseTimer = MovePhaseSec * 0.7f;
                    _turn = _units.FindIndex(o => o.Team == 0 && o.Alive);
                    if (_turn < 0) _turn = 0;
                    _units[_turn].Hp = Mathf.RoundToInt(_units[_turn].HpMax * 0.62f);
                    AddPopup(_units[_turn].Pos, "-128", Ui.Enemy);         // 피해 팝업도 화면에 있어야 한다
                    break;
                case 6:
                    _screen = GameScreen.Battle;
                    _phase = Phase.Fire; _phaseTimer = FirePhaseSec * 0.5f;
                    _charging = true; _power = 0.62f; _mark = 0.74f;
                    // 게이지가 궁극기까지 찬 모습을 찍는다 — 슬롯 4칸이 전부 살아 있는 상태가 검사 대상이다
                    for (int i = 0; i < NiceShot.UltimateCost; i++) _units[_turn].Skill.OnNiceShot();
                    // 2번탄을 골라 둔다 — **설명 줄이 실제로 그려진 사진**이 남아야 한다(2026-09-19).
                    // ⚠️ 그냥 첫 유닛을 쓰면 캐논이 잡히는데 캐논 2번탄은 `EffectType.None` 이라
                    //    "특수 효과 없음" 한 줄만 찍힌다 — **정작 검사하려는 경로(효과 설명)가 안 찍힌다.**
                    //    그래서 팀에서 **효과가 있는 기종**을 골라 턴을 준다(로스터가 바뀌어도 성립한다).
                    int fxTurn = _units.FindIndex(o => o.Team == 0 && o.Alive
                                   && ShellEffects.Of(o.Kind, ShellKind.Special).Type != ShellEffects.EffectType.None);
                    if (fxTurn >= 0) _turn = fxTurn;
                    _units[_turn].Shell = ShellKind.Special; _useSs = false; _useUlt = false;
                    for (int i = 0; i < NiceShot.UltimateCost; i++) _units[_turn].Skill.OnNiceShot();
                    break;
                case 7: _screen = GameScreen.Pause; break;
                case 8:
                case 9:
                case 10:
                    // 셋 다 같은 판을 만들고 **승패만 바꾼다** — 레이아웃 차이를 승패 탓으로 오해하지 않으려고.
                    _winner = step == 8 ? "아군 승리" : step == 9 ? "적군 승리" : "무승부";
                    _phase = Phase.GameOver;
                    _screen = GameScreen.Result;
                    _stat[0] = new TeamStat { Shots = 14, Hits = 9, Damage = 2480, Taken = 1310 };
                    _stat[1] = new TeamStat { Shots = 13, Hits = 5, Damage = 1310, Taken = 2480 };
                    _battleClock = 372f;
                    // 턴 시간 줄이 **숫자와 함께** 찍히게 한다 — 0턴이면 "0.0초"만 나와서
                    // 정작 검사하려는 줄이 사진에 안 남는다(2번탄 설명에서 같은 실수를 한 번 했다).
                    _humanSec = 186f; _humanTurns = 12; _aiSec = 24f; _aiTurns = 12;
                    // ⚠️ **무승부 칸은 «기록 없음»으로 찍는다.** 사람 턴이 0 인 판(AI 대전·자동)에서 그 줄이
                    //    어떻게 보이는지를 **아무도 눈으로 본 적이 없다.** 숫자가 있는 사진만 남기면
                    //    「값이 없을 때」는 영영 안 덮인다 — 빈 상태야말로 거짓말하기 쉬운 자리다.
                    if (step == 10) { _humanSec = 0f; _humanTurns = 0; }
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