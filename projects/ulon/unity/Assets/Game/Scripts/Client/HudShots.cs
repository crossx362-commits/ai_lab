using System.Collections;
using System.IO;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Client
{
    /// <summary>
    /// **HUD가 찍힌 화면**을 남긴다(검수 2026-09-07: 플레이어가 가장 오래 보는 화면을 검수가 한 번도 못 봤다).
    /// HUD는 IMGUI라 편집기 오프스크린 렌더(`QaShots`)에 안 나온다 — 그래서 실행 중인 스탠드얼론에서
    /// `ScreenCapture`로 찍는다. 경로는 프레임 프로브와 같다(`-hudshots -shotdir <폴더>`).
    ///
    /// **게이트가 아니다.** 눈으로 볼 증거를 만들 뿐이고 판정은 검수가 한다.
    /// 매 랩 돌리지 않는다 — HUD를 건드린 랩에서만(`bash tools/hud_shots.sh`).
    /// </summary>
    public sealed class HudShots : MonoBehaviour
    {
        static string dir;
        static bool stateNc;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = System.Environment.GetCommandLineArgs();
            bool on = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-hudshots") on = true;
                if (args[i] == "-hudstatenc") stateNc = true;   // 상태 고정을 전부 끄는 반대쪽 한계 판
                if (args[i] == "-shotdir" && i + 1 < args.Length) dir = args[i + 1];
            }
            if (!on)
                return;
            var go = new GameObject("HudShots");
            DontDestroyOnLoad(go);
            go.AddComponent<HudShots>();
        }

        IEnumerator Start()
        {
            if (string.IsNullOrEmpty(dir))
                dir = Application.persistentDataPath;
            Directory.CreateDirectory(dir);
            Screen.SetResolution(1280, 720, false);
            // **시계도 고정한다**(랩 ㉮). 계정을 고정해 같은 캐릭터로 시작해도, 프레임마다 흐른
            // **실제 시간**이 판마다 다르면 마을 사람이 다른 자리에 서 있고 카메라가 다른 각도로
            // 따라온다 — 그래서 첫 고정판에서도 여덟 장이 갈렸다. `captureFramerate`를 박으면
            // `Time.deltaTime`이 1/30로 고정돼 **같은 프레임 수 = 같은 세계 시각**이 된다.
            // 이 값은 이 프로세스에만 산다(찍고 나면 앱이 끝난다) — 저장되는 상태가 아니다.
            // NC(`-hudstatenc`)에서는 **이것도 끈다** — 반만 끈 NC는 약한 빨간불이라 더 위험하다(원장).
            if (!stateNc)
                Time.captureFramerate = 30;
            for (int i = 0; i < 30; i++) yield return null;

            // ① 캐릭터 생성 화면 — 새 계정이 처음 보는 화면이다.
            if (PersistDriver.Creating)
            {
                yield return Shot("hud_01_create");
                var picks = new[] { SkillId.Swordsmanship, SkillId.Mining, SkillId.Blacksmithing };
                var values = new[] { 50f, 30f, 20f };
                var snap = CharacterCreate.Build(PersistDriver.AccountKey(), "검수용", 0, 30, 25, 25, picks, values);
                PersistDriver.Commit(snap);
                for (int i = 0; i < 20; i++) yield return null;
            }

            var world = OfflineWorld.Instance;
            if (world == null || world.Player == null)
            {
                Debug.Log("[Ulon] HUD 샷 실패 — 월드/플레이어가 없습니다.");
                Application.Quit(1);
                yield break;
            }

            // ② 기본 HUD — 가방이 빈 채로. 플레이어가 로그인 직후 보는 화면.
            yield return Shot("hud_02_default");

            // ③ 가방·장비가 찬 상태 — 인벤토리 줄과 도구 횟수·동료 슬롯이 보이게.
            var bag = world.Player.GetComponent<InventoryBag>();
            if (bag != null)
            {
                bag.Add(ItemCatalog.Pickaxe, 1);
                bag.Add(ItemCatalog.Hatchet, 1);
                bag.Add("iron_ore", 12);
                bag.Add("wood", 8);
                bag.Add(ItemCatalog.Bandage, 5);
                Panel(1);                       // 가방 패널을 열고 찍는다
                for (int i = 0; i < 5; i++) yield return null;
                yield return Shot("hud_03_inventory");
            }

            // ④ 제작·수리 — 대장간 앞에 서서. 「수리」 버튼이 화면에 있는지 검수가 직접 본다.
            var forge = FindStation();
            if (forge != null)
            {
                world.Player.transform.position = forge.transform.position + new Vector3(1.5f, 0f, 1.5f);
                world.Select(null);
                Panel(5);                       // 근처 패널 — 수리 버튼이 여기 있다
                for (int i = 0; i < 10; i++) yield return null;
                yield return Shot("hud_04_craft_repair");
            }

            // ⑤ 전투 중 — 대상 줄·체력·타격 결과가 화면에 어떻게 나오는지.
            var victim = FindHostile(world);
            if (victim != null)
            {
                world.Player.transform.position = victim.transform.position + new Vector3(1.2f, 0f, 1.2f);
                world.Select(victim);
                Panel(0);                       // 전투 중에는 패널을 닫은 기본 화면
                for (int i = 0; i < 5; i++) yield return null;
                world.TryAttack(world.Player, victim);
                for (int i = 0; i < 3; i++) yield return null;
                yield return Shot("hud_05_combat");
            }

            // ⑥ 상점 — 상인 앞에서 근처 패널을 열고. 검수가 이 화면도 못 봤다.
            var vendor = FindAnyObjectByType<VendorStation>();
            if (vendor != null)
            {
                world.Player.transform.position = vendor.transform.position + new Vector3(1.4f, 0f, 1.4f);
                world.TryVendor(world.Player, vendor);
                Panel(5);
                for (int i = 0; i < 8; i++) yield return null;
                yield return Shot("hud_06_shop");
            }

            // ⑦ 「은행」 워프 착지 — 수치가 0.10m라도 **벽 속이 아닌지는 눈으로 봐야 한다**(검수 랩 B 조건 3).
            world.Player.transform.position = new Vector3(30f, world.Player.transform.position.y, 30f);
            world.TrySpeechKeyword(world.Player, "은행");
            Panel(0);
            for (int i = 0; i < 10; i++) yield return null;
            yield return Shot("hud_07_bank_warp");

            // ⑧ 시체 — **내 것과 남의 것이 화면에서 갈리는가**(검수 조건 ㉡, 2026-09-08).
            //    구별이 없으면 「가져갈 수 있는 것」을 착각한다. 두 장을 나란히 찍어 눈으로 본다.
            //    (열람 목록은 온라인에서 서버가 답으로 준다 — 여기선 화면 구성만 본다.)
            var corpseSpot = world.Player.transform.position + new Vector3(1.2f, 0f, 0f);
            world.ApplyCorpseView(PersistDriver.AccountKey(), "hudshot-mine", "나", corpseSpot, 600f, world.Player);
            world.ApplyCorpseItems(PersistDriver.AccountKey(), "bandage:2|wood:3");
            Panel(5);
            for (int i = 0; i < 8; i++) yield return null;
            yield return Shot("hud_08_corpse_mine");
            world.RemoveCorpseView(PersistDriver.AccountKey());
            world.ApplyCorpseView("someone-else", "hudshot-other", "떠돌이", corpseSpot, 600f, world.Player);
            world.ApplyCorpseItems("someone-else", "iron_ore:4");
            Panel(5);
            for (int i = 0; i < 8; i++) yield return null;
            yield return Shot("hud_09_corpse_other");
            world.RemoveCorpseView("someone-else");

            // 화면 규칙 검사 — **해상도 하나만 재면 다른 쪽이 깨진다**(검수). 두 해상도 모두 본다.
            bool ok = true;
            yield return CheckLayout(1280, 720, r => ok &= r);
            yield return CheckLayout(1920, 1080, r => ok &= r);

            // 조작 수단 수집 — 상태가 있어야 그려지는 버튼(궤짝·던전 문)은 그 자리에 서서 모은다.
            yield return VisitForControls(world);
            // 그린 자 등록 강제 + 그려진 조작 수단 기록(랩 A).
            yield return CheckOwners(r => ok &= r);
            WriteControls();

            Debug.Log("[Ulon] HUD 샷 완료 — " + dir + (ok ? " · 화면 규칙 통과" : " · **화면 규칙 위반**"));
            Application.Quit(ok ? 0 : 1);
        }

        /// <summary>
        /// ① 중앙 전투 영역에 UI 픽셀 0 ② 패널끼리 겹침 0 — 열 수 있는 패널을 **전부 열어 가며** 확인한다.
        /// 판정 근거는 HUD가 그 프레임에 실제로 그린 영역(`SliceHud.DrawnAreas`)이다.
        /// </summary>
        IEnumerator CheckLayout(int w, int h, System.Action<bool> result)
        {
            Screen.SetResolution(w, h, false);
            for (int i = 0; i < 10; i++) yield return null;
            var hud = FindAnyObjectByType<SliceHud>();
            if (hud == null)
            {
                Debug.Log("[Ulon] 화면 규칙 — SliceHud가 없습니다.");
                result(false);
                yield break;
            }
            bool ok = true;
            for (int p = 0; p < SliceHud.PanelCount; p++)
            {
                hud.ShowPanel(p);
                for (int i = 0; i < 3; i++) yield return null;
                Harvest();
                var areas = SliceHud.DrawnAreas.ToArray();
                var zone = SliceHud.CombatZone;
                for (int i = 0; i < areas.Length; i++)
                {
                    if (areas[i].Overlaps(zone))
                    {
                        Debug.Log("[Ulon] 화면 규칙 위반 " + w + "x" + h + " " + SliceHud.PanelName(p) +
                                  " — UI " + areas[i] + "가 중앙 전투 영역 " + zone + "을 침범");
                        ok = false;
                    }
                    for (int j = i + 1; j < areas.Length; j++)
                        if (areas[i].Overlaps(areas[j]))
                        {
                            Debug.Log("[Ulon] 화면 규칙 위반 " + w + "x" + h + " " + SliceHud.PanelName(p) +
                                      " — UI 겹침 " + areas[i] + " / " + areas[j]);
                            ok = false;
                        }
                }
            }
            hud.ShowPanel(0);
            Debug.Log("[Ulon] 화면 규칙 " + w + "x" + h + " — " + (ok ? "통과(중앙 침범 0·겹침 0)" : "위반"));
            result(ok);
        }

        /// <summary>
        /// 상태가 있어야 그려지는 버튼을 모으러 다닌다 — 궤짝 앞(따기)·던전 문 앞(들어가기)·훈련사 앞(훈련).
        /// 안 다니면 「코드에는 있는데 화면에 없다」와 「이 실행에서 그 자리에 안 가 봤다」가 구분되지 않는다.
        /// </summary>
        IEnumerator VisitForControls(OfflineWorld world)
        {
            var me = world.Player;
            var crate = OfflineWorld.FindCrate("LockedCrate");
            if (crate != null)
            {
                me.transform.position = crate.transform.position + new Vector3(1.0f, 0f, 1.0f);
                Panel(5);
                for (int i = 0; i < 5; i++) yield return null;
                Harvest();
            }
            var gate = GameObject.Find(Dungeon1.EntranceObject);
            if (gate != null)
            {
                me.transform.position = gate.transform.position + new Vector3(1.0f, 0f, 1.0f);
                Panel(5);
                for (int i = 0; i < 5; i++) yield return null;
                Harvest();
            }
            var trainer = FindAnyObjectByType<TrainerStation>();
            if (trainer != null)
            {
                me.transform.position = trainer.transform.position + new Vector3(1.2f, 0f, 1.2f);
                world.TryTrainer(me, trainer);
                Panel(5);
                for (int i = 0; i < 5; i++) yield return null;
                Harvest();
                world.CloseTrainer();
            }
        }

        /// <summary>패널을 도는 동안 화면에 그려진 버튼 라벨을 모은다 — 게이트가 소스와 대조할 원장이다.</summary>
        static readonly System.Collections.Generic.SortedSet<string> controls = new System.Collections.Generic.SortedSet<string>();
        static readonly System.Collections.Generic.HashSet<string> owners = new System.Collections.Generic.HashSet<string>();

        static void Harvest()
        {
            for (int i = 0; i < SliceHud.DrawnControls.Count; i++)
                controls.Add(SliceHud.DrawnControls[i]);
            foreach (var o in SliceHud.DrawnOwners)
                owners.Add(o);
        }

        void WriteControls()
        {
            string path = Path.Combine(dir, "hud_controls.txt");
            File.WriteAllText(path, string.Join("\n", controls) + "\n");
            Debug.Log("[Ulon] 그려진 조작 수단 " + controls.Count + "종 기록 — " + path);
        }

        /// <summary>
        /// **OnGUI를 그리면서 영역 등록을 안 한 컴포넌트**를 잡는다. 접속 패널이 상태 카드에 겹쳐 있는데도
        /// 화면 규칙이 통과했던 이유가 이것이고, 등록을 강제할 수단이 없으면 그대로 재발한다(검수 랩 A).
        /// 한계: 이 검사가 도는 상태(패널 전부 열어 보기)에서 **한 번도 안 그리는** 컴포넌트는 못 본다.
        /// </summary>
        IEnumerator CheckOwners(System.Action<bool> result)
        {
            var probe = new GameObject("UnregisteredGuiProbe").AddComponent<UnregisteredGuiProbe>();
            for (int i = 0; i < 5; i++) yield return null;
            bool ncRed = !OwnersOk(out string ncMissing);
            Destroy(probe.gameObject);
            for (int i = 0; i < 5; i++) yield return null;
            Debug.Log("[Ulon] 등록 강제 네거티브 컨트롤 — 등록 안 하는 OnGUI를 넣자 " +
                      (ncRed ? "빨간불(" + ncMissing + ")" : "**통과했다 — 게이트가 못 잡는다**"));

            bool ok = OwnersOk(out string missing);
            if (!ok)
                Debug.Log("[Ulon] 화면 규칙 위반 — OnGUI를 그리면서 SliceHud.RegisterArea로 영역을 등록하지 않은 컴포넌트: " + missing);
            else
                Debug.Log("[Ulon] 그린 자 등록 통과 — OnGUI 구현 " + owners.Count + "종 전부 영역을 등록한다(" +
                          string.Join(", ", owners) + ")");
            result(ok && ncRed);
        }

        static bool OwnersOk(out string missing)
        {
            var list = new System.Collections.Generic.List<string>();
            var all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i].GetType();
                if (t.GetMethod("OnGUI", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                                         | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly) == null)
                    continue;
                if (!owners.Contains(t.Name) && !SliceHud.DrawnOwners.Contains(t.Name) && !list.Contains(t.Name))
                    list.Add(t.Name);
            }
            missing = string.Join(", ", list);
            return list.Count == 0 && owners.Count > 0;   // 0이면 실패 — 아무도 안 그렸다면 잰 게 없다
        }

        static void Panel(int index)
        {
            var hud = FindAnyObjectByType<SliceHud>();
            if (hud != null) hud.ShowPanel(index);
        }

        IEnumerator Shot(string name)
        {
            string path = Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            // 캡처는 프레임 끝에 일어난다 — 몇 프레임 기다려야 파일이 실제로 생긴다.
            for (int i = 0; i < 10; i++) yield return null;
            Harvest();
            var w = OfflineWorld.Instance;
            string where = "";
            if (w != null && w.Player != null)
            {
                var pp = w.Player.transform.position;
                float g = Physics.Raycast(pp + Vector3.up * 300f, Vector3.down, out RaycastHit gh, 800f) ? gh.point.y : float.NaN;
                var camT = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
                where = " | 플레이어 " + pp.ToString("F1") + ", 지표 " + g.ToString("F1") +
                        ", 해수면 " + WorldTerrain.SeaLevel.ToString("F1") + ", 카메라 " + camT.ToString("F1");
            }
            Debug.Log("[Ulon] HUD 샷 " + name + " — " + path + where);
        }

        static CraftStation FindStation()
        {
            var stations = FindObjectsByType<CraftStation>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < stations.Length; i++)
                if (stations[i].name.IndexOf("Forge", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return stations[i];
            return stations.Length > 0 ? stations[0] : null;
        }

        static WorldBody FindHostile(OfflineWorld world)
        {
            var bodies = FindObjectsByType<WorldBody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < bodies.Length; i++)
                if (bodies[i] != world.Player && bodies[i].Alive && bodies[i].IsEnemy)
                    return bodies[i];
            return null;
        }
    }
}
