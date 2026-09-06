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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = System.Environment.GetCommandLineArgs();
            bool on = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-hudshots") on = true;
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

            // 화면 규칙 검사 — **해상도 하나만 재면 다른 쪽이 깨진다**(검수). 두 해상도 모두 본다.
            bool ok = true;
            yield return CheckLayout(1280, 720, r => ok &= r);
            yield return CheckLayout(1920, 1080, r => ok &= r);

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
