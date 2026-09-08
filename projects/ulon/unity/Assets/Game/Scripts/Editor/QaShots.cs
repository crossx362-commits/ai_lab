using System.IO;
using UnityEditor.Animations;
using Ulon.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 검수용 오프스크린 스크린샷 — 셀프체크 PASS가 화면 품질을 보증하지 않으므로(검수 2026-09-06)
    /// 화면 근거를 PNG로 남긴다. 배치모드(-nographics 없이)로 돌린다. 게임 로직은 건드리지 않는다.
    ///   Unity -batchmode -projectPath . -executeMethod Ulon.Editor.QaShots.Run -quit
    /// 산출물: projects/ulon/builds/qa/*.png
    ///
    /// **금지: 샷을 위해 세계를 옮기지 않는다**(검수 판정 2026-09-08).
    /// 은행원이 은행 안에, 상인이 차양 밑에, 훈련사가 지붕 밑에 선 것은 §18.19가 맞게 구현된
    /// 모습이다. 근접 샷이 안 찍힌다고 그 사람들을 문 밖으로 끌어내면 **계측기가 세계를 바꾸는
    /// 것**이고, 게임은 나빠지고 숫자만 좋아진다. 막히면 **카메라를 껍데기 안으로** 넣고,
    /// 그래도 안 되면 그 사실을 이름과 함께 **보고**한다.
    /// </summary>
    public static class QaShots
    {
        const int W = 1280;
        const int H = 720;

        /// <summary>이번 실행에서 찍은 마을 사람 샷 이름 — 대조 시트가 이것만 모은다.</summary>
        static readonly System.Collections.Generic.List<string> villagerShots = new System.Collections.Generic.List<string>();

        struct Shot
        {
            public string Name;
            public Vector3 Eye;      // 카메라 위치(월드)
            public Vector3 Target;   // 바라보는 지점(월드)
            public bool PlayCamera;  // 플레이 카메라 재현(차폐 페이드 적용)
            public bool Vfx;         // 행동 효과 3종을 나란히 재생해 같이 찍는다
            public bool StandPlayer; // 플레이어를 그 자리에 실제로 세우고 찍는다(가려짐을 눈으로 보려면 몸이 있어야 한다)
            public Transform Subject; // 근접 샷의 피사체 — 자기 자신이 페이드에 물리지 않게 뺀다
        }

        [MenuItem("Ulon/QA Shots")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../builds/qa"));
            Directory.CreateDirectory(dir);

            BearingNegativeControl("Forge");

            var shots = new[]
            {
                Orbit("01_village_square", new Vector3(0f, 0f, 0f), 20f, 35f),
                CompanionBlock("58_companion_block"),
                PersonPlayCam("59_banker_playcam", "Banker"),
                Orbit("02_village_wide", new Vector3(0f, 0f, 0f), 55f, 45f),
                // 마을 쪽(남)에서 북쪽 사냥터를 본다 — 마을이 카메라 **뒤**라 프레임 밖이다
                // (검수 완료 기준 랩 ⑦: 8체가 다 들어오고 마을이 화면에 없을 것).
                // 눈 자리는 `VisualSliceBuilder.HuntViewEye` 원장 — 겹침 게이트가 **같은 눈**으로 잰다.
                Free("03_hunt_mobs",
                     new Vector3(VisualSliceBuilder.HuntViewEye.x,
                                 GroundY(VisualSliceBuilder.HuntViewEye.x, VisualSliceBuilder.HuntViewEye.y) + VisualSliceBuilder.HuntViewEyeHeight,
                                 VisualSliceBuilder.HuntViewEye.y),
                     new Vector3(VisualSliceBuilder.HuntViewTarget.x,
                                 GroundY(VisualSliceBuilder.HuntViewTarget.x, VisualSliceBuilder.HuntViewTarget.y) + VisualSliceBuilder.HuntViewTargetHeight,
                                 VisualSliceBuilder.HuntViewTarget.y)),
                // 잡몹이 나란히 선 눈높이 샷 — **키를 서로 비교해서 읽는** 화면(검수 완료 기준 랩 ⑥).
                // 위에서 내려다보면 원근이 키 차이를 먹는다. 낮게·가까이서 본다.
                // 마을 반대쪽(북)에서 눈높이로 — 궤도 샷은 지붕이 화면 절반을 먹었다.
                Free("56_mob_lineup",
                     new Vector3(2.8f, GroundY(2.8f, 45f) + 4.0f, 45f),
                     new Vector3(2.8f, GroundY(2.8f, 33.5f) + 1.0f, 33.5f)),
                Orbit("06_field_boss", new Vector3(22.6f, 0f, 8.4f), 10f, 25f),
                Orbit("07_d1_entrance", new Vector3(Dungeon1.EntranceX, 0f, Dungeon1.EntranceZ), 8f, 20f),
                PlayCam("08_d1_interior_playcam", Dungeon1.InteriorX, Dungeon1.InteriorZ),
                // 귀퉁이에 선 화면 — 카메라 눈이 벽 밖으로 나가는 최악 자리(검수 2026-09-06 B).
                PlayCam("23_d1_corner_playcam", Dungeon1.InteriorX + 6f, Dungeon1.InteriorZ + 6f, Dungeon1.InteriorX, Dungeon1.InteriorZ),
                Inside("08_d1_interior", Dungeon1.InteriorX, Dungeon1.InteriorZ, Dungeon1.BossX, Dungeon1.BossZ),
                Orbit("09_d2_entrance", new Vector3(Dungeon2.EntranceX, 0f, Dungeon2.EntranceZ), 8f, 20f),
                PlayCam("10_d2_interior_playcam", Dungeon2.InteriorX, Dungeon2.InteriorZ),
                Inside("10_d2_interior", Dungeon2.InteriorX, Dungeon2.InteriorZ, Dungeon2.BossX, Dungeon2.BossZ),
                Orbit("11_d3_entrance", new Vector3(Dungeon3.EntranceX, 0f, Dungeon3.EntranceZ), 8f, 20f),
                // 문구멍 슬랩을 안쪽으로 물린 뒤 **비스듬한 방위에서 판의 앞면이 노출되는지** 본다
                // (검수 조건 2026-09-08: 정면 한 장 = 11번, 45° 한 장 = 이것).
                // 물려받은 자의 근거를 화면으로 확인한다(검수 2026-09-09): 허용 50%는 **던전 소품끼리**
                // 유도한 값이다. 마을에서 가장 깊이 물린 쌍(`cart-high↔House` 36%)이 화면에서
                // 「박혀 보이는가」를 눈으로 보고, 안 보이면 건물 쌍에도 유효하다고 근거를 적는다.
                // 자동 방위(FacilityCloseUp)는 첫 판에 이웃 집 처마 **안쪽**을 골라 피사체가 안 보였다 —
                // 판정 대상이 안 찍히는 샷은 판정이 아니다. 수레↔집 선의 **옆**에서 본다.
                Free("61_cart_house", new Vector3(-17.4f, GroundY(-17.4f, 12.6f) + 2.2f, 12.6f),
                     new Vector3(-12.6f, GroundY(-12.6f, 7.2f) + 0.8f, 7.2f)),
                // 지붕 경증 둘(굴뚝이 지붕 앞으로 뜸 · 박공 주황 널판 돌출)을 재판정할 근접.
                FacilityCloseUp("62_house_roof", "House"),
                Angled("60_d3_entrance_45", new Vector3(Dungeon3.EntranceX, 0f, Dungeon3.EntranceZ), 7f, 18f, 90f),
                PlayCam("12_d3_interior_playcam", Dungeon3.InteriorX, Dungeon3.InteriorZ),
                Vfx(PlayCam("24_action_vfx", Dungeon3.InteriorX, Dungeon3.InteriorZ)),
                Inside("12_d3_interior", Dungeon3.InteriorX, Dungeon3.InteriorZ, Dungeon3.BossX, Dungeon3.BossZ),
                Roof("13_d1_room_cutaway", Dungeon1.InteriorX, Dungeon1.InteriorZ),
                // §8.1 멀리서도 읽히는 실루엣 — 산·바다 조망, 호수·강 조망.
                BossCloseUp("17_boss_closeup", Dungeon3.BossX, Dungeon3.BossZ),
                ActorCloseUp("22_mob_closeup", Dungeon3.MobObject),
                Orbit("18_meadow", new Vector3(WorldRegions.Meadow.X, 0f, WorldRegions.Meadow.Z), 34f, 28f),
                Orbit("19_forest", new Vector3(WorldRegions.Forest.X, 0f, WorldRegions.Forest.Z), 38f, 26f),
                Orbit("20_mine", new Vector3(WorldRegions.Mine.X, 0f, WorldRegions.Mine.Z), 30f, 26f),
                Orbit("21_testchamber", new Vector3(WorldRegions.TestChamber.X, 0f, WorldRegions.TestChamber.Z), 24f, 22f),
                Free("14_world_vista", new Vector3(-165f, 95f, -165f), new Vector3(0f, WorldTerrain.LandBase, 0f)),
                Free("15_lake_river", new Vector3(WorldTerrain.LakeX + 46f, 40f, WorldTerrain.LakeZ + 46f), new Vector3(WorldTerrain.LakeX - 12f, WorldTerrain.SeaLevel, WorldTerrain.LakeZ)),
                // 효과를 **야외 대낮**에서도 한 장(실내만 보면 어두운 배경 덕을 본다), 그리고
                // 풍차(은행) 뒤에 선 자리 — 건물을 페이드 대상에 올린 뒤 화면이 어떻게 보이는지(검수 요구).
                Vfx(Stand(PlayCamOutdoor("25_action_vfx_village", 0f, 0f))),
                Stand(PlayCamOutdoor("26_behind_bank", -10f, 8f)),
                // 던전 입구 앞 — 지표 높이인데 머리 위에 구조물이 있다. 줌이 실내로 튀지 않는지 눈으로 본다.
                Stand(PlayCamAuto("27_entrance_zoom", Dungeon2.EntranceX, Dungeon2.EntranceZ)),
                // 마을 시설 근접 — 「저게 대장간이구나」가 화면에서 읽히는지 눈으로 본다(검수 랩 ① 요구).
                Stand(PlayCamOutdoor("28_facilities", -5.2f, 3.4f)),
                Stand(PlayCamOutdoor("29_forge_carpenter", -6.8f, 5.2f)),
                // 시설별 **진짜 근접** — 28·29는 플레이 거리라 시설이 수십 픽셀이었다(검수 반려).
                FacilityCloseUp("30_forge", "Forge"),
                FacilityCloseUp("31_carpenter", "Carpenter"),
                FacilityCloseUp("32_vendor", "Vendor"),
                FacilityCloseUp("33_campfire", "Campfire"),
                FacilityCloseUp("34_mortar", "Mortar"),
                FacilityCloseUp("35_fishing", "FishingSpot",
                    new Vector3(WorldTerrain.LakeX, WorldTerrain.SeaLevel, WorldTerrain.LakeZ)),   // 호수를 등지지 않게
                FacilityCloseUp("36_stable", "Stable"),
                FacilityCloseUp("37_banker", "Banker"),
                FacilityCloseUp("38_healer", "Healer"),
                // 보스가 **바닥에 서 있는지** 눈으로 본다(검수 판정 2026-09-07 ① — 최대 1.10m 묻혀 있었다).
                FacilityCloseUp("39_boss1", Dungeon1.BossObject, null, true),
                FacilityCloseUp("40_boss2", Dungeon2.BossObject, null, true),
                FacilityCloseUp("41_boss3", Dungeon3.BossObject, null, true),
                // 조련 생물 근접 — 덤불이 아니라 짐승으로 읽히는지 본다(동물 팩 랩 완료 기준).
                FacilityCloseUp("42_deer", TameCritter.Object, null, true),
                FacilityCloseUp("43_boar", TameBoar.Object, null, true),
                // 마구간 마당의 짐승 — 「빈 마당」 반려의 완료 근거(동물 랩).
                FacilityCloseUp("44_stable_beast", VisualSliceBuilder.StableBeastObject, null, true),
                Free("16_mountain_ridge", new Vector3(60f, 30f, 60f), new Vector3(WorldTerrain.MountainPeak, WorldTerrain.LandBase + 18f, WorldTerrain.MountainPeak * 0.4f)),
            };

            // **마을 사람 근접** — 5역할이 서로 다른 모습인지 눈으로 본다(검수 랩 ③사람 완료 기준).
            // 대상은 이름 목록이 아니라 `VillagerLook.Villagers()` 전수다 — 역할이 늘면 샷도 늘어난다.
            var shotList = new System.Collections.Generic.List<Shot>(shots);
            var villagers = VillagerLook.Villagers();
            villagerShots.Clear();
            for (int i = 0; i < villagers.Count; i++)
            {
                string nm = (45 + i).ToString("00") + "_person_" + VillagerLook.HostOf(villagers[i]);
                villagerShots.Add(nm);
                shotList.Add(FacilityCloseUp(nm, villagers[i].name, null, true));
            }
            shotList.Add(PairCloseUp("51_player_companion", "Player", VisualSliceBuilder.CompanionObject));
            // 도적 근접 — Mage 차림이던 이름-외형 어긋남을 고친 뒤(검수 승인) 화면으로 확인한다.
            shotList.Add(FacilityCloseUp("52_bandit", "Bandit", null, true));
            shotList.Add(FacilityCloseUp("53_rogue", "Rogue", null, true));   // 자객 단독
            // 검수 완료 기준 — **도적과 자객을 한 화면에**. 도적을 Rogue 모델로 옮겼으니
            // 「겹침이 Rogue 쪽으로 옮겨간 것 아니냐」를 눈으로 확인할 수 있어야 한다.
            shotList.Add(PairCloseUp("54_bandit_rogue", "Bandit", "Rogue"));
            // 아마밭 — 「밭으로 읽히는가」는 근접 한 장으로 판정한다(검수 완료 기준, 랩 ⑤).
            // 한 포기에 붙으면 「밭」이 화면에 안 담긴다 — **뙈기 전체**가 들어오는 거리·각도로 찍는다.
            shotList.Add(Orbit("55_flaxfield", new Vector3(WorldSplat.FlaxX, 0f, WorldSplat.FlaxZ), 13f, 32f));
            shots = shotList.ToArray();

            // **런타임 포즈로 찍는다.** 에디터에서 그냥 찍으면 모든 액터가 바인드 포즈(T포즈)라
            // 「칼이 얼굴 높이를 가로지른다」 같은 인상이 실제 플레이와 다르다(검수 2026-09-06 질의).
            // 애니메이터 기본 상태(Idle)를 실제로 샘플링해 포즈를 만든 뒤 찍는다.
            // **배치 렌더에서는 파티클이 돌지 않는다** — 화덕 불처럼 계속 나는 효과는 미리 시뮬레이션해야
            // 화면에 찍힌다(안 하면 「불을 붙였는데 샷엔 없다」가 된다).
            int simmed = 0;
            var loops = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < loops.Length; i++)
            {
                var m = loops[i].main;
                if (!m.loop || !m.playOnAwake)
                    continue;
                loops[i].Simulate(1.2f, true, true);
                simmed++;
            }
            // **분모를 같이 찍는다** — 「N개 했다」만 적으면 빠진 것이 조용히 남는다(포즈 7체가 그렇게 샜다).
            Debug.Log("[Ulon] QA 파티클 시뮬레이션 — 계속 나는 효과 " + simmed + "/" + loops.Length +
                      "개(나머지는 1회성이라 시뮬레이션 대상이 아니다)");

            int posed = SampleIdlePose(out int animTotal);
            Debug.Log("[Ulon] QA 포즈 샘플링 — Idle 적용 액터 " + posed + "/" + animTotal + "체" +
                      (posed < animTotal ? " — 빠진 것은 위의 「건너뜀」 줄에 사유가 있다" : ""));

            var camGo = new GameObject("QaShotCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt;
                for (int i = 0; i < shots.Length; i++)
                {
                    var shot = shots[i];
                    camGo.transform.position = shot.Eye;
                    camGo.transform.LookAt(shot.Target);
                    var faded = new System.Collections.Generic.List<Renderer>();
                    if (shot.PlayCamera)
                        Ulon.Client.DungeonSightFade.Hide(shot.Eye, shot.Target, Ulon.Client.DungeonSightFade.DefaultRadius, faded, shot.Subject);
                    // **무엇이 반투명해졌는지 이름으로 남긴다** — 화면에 유령이 보이면 그것이 벽 장식인지
                    // 피사체의 일부인지 로그로 갈린다(검수 의심 2026-09-07: 41 오른쪽 반투명 칼날).
                    for (int f = 0; f < faded.Count && f < 12; f++)
                        if (faded[f] != null)
                            Debug.Log("[Ulon] 샷 페이드 " + shot.Name + " ← " + faded[f].transform.root.name + "/" + faded[f].name);
                    var vfx = shot.Vfx ? SliceSelfCheck.SpawnVfxTrio(shot.Target) : null;
                    // 「집 뒤에 서면 어떻게 보이나」는 **몸이 있어야** 보인다 — 좌표만 찍으면 빈 잔디다.
                    var player = shot.StandPlayer ? GameObject.Find("Player") : null;
                    Vector3 savedPlayer = player != null ? player.transform.position : Vector3.zero;
                    if (player != null)
                        player.transform.position = shot.Target - Vector3.up * 1.0f;
                    cam.Render();
                    if (player != null) player.transform.position = savedPlayer;
                    if (vfx != null) Object.DestroyImmediate(vfx);
                    Ulon.Client.DungeonSightFade.Restore(faded);
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    File.WriteAllBytes(Path.Combine(dir, shot.Name + ".png"), tex.EncodeToPNG());
                    Debug.Log("[Ulon] QA shot " + shot.Name);
                }
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = null;
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
            // **샷을 먼저 찍고 게이트는 나중에 돈다**(검수 지시 2026-09-07). 게이트가 먼저 돌면
            // 빨간불 때 옛 PNG가 남아 「이번 화면」으로 오독된다 — 실제로 한 번 판정을 흐릴 뻔했다.
            // 언제 찍은 것인지도 파일로 남긴다(hud_controls.txt 머리말과 같은 처방).
            File.WriteAllText(Path.Combine(dir, "shots.txt"),
                "# 촬영 " + System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "Z · 이 폴더의 PNG는 이 시각의 것이다\n" +
                string.Join("\n", System.Array.ConvertAll(shots, x => x.Name + ".png")) + "\n");
            Debug.Log("[Ulon] QA shots " + shots.Length + "장 — " + dir);
            ContactSheet(dir, villagerShots, "50_villagers");

            // VFX는 카메라 렌더가 필요해 -nographics 셀프체크에서 잴 수 없다 — 여기서 화면으로 잰다.
            SliceSelfCheck.AssertActionVfxOnScreen();
            SliceSelfCheck.AssertActionVfxNegativeControl();
            // 검은 배경에서 보이는 것과 **실제 플레이 프레임**에서 읽히는 것은 다르다(검수 랩 D).
            SliceSelfCheck.AssertActionVfxInPlayFrame();
            SliceSelfCheck.AssertActionVfxInPlayFrameNegativeControl();
            // 사람 샷이 **앞에서** 찍혔는가 — 등만 나온 샷은 「누구인지」를 판정할 수 없다(검수 반려).
            SliceSelfCheck.AssertPersonShotsFrontNegativeControl();
            SliceSelfCheck.AssertPersonShotsFront();
            // 앞에서 찍혔어도 **가려져 있으면** 판정할 수 없다 — 두 축은 따로 잰다(검수 반려 2026-09-08).
            SliceSelfCheck.AssertPersonShotsUnblockedNegativeControl();
            SliceSelfCheck.AssertPersonShotsUnblocked();
        }

        /// <summary>
        /// **대조 시트** — 방금 찍은 근접 샷들을 한 장에 나란히 붙인다(검수 랩 ③ 완료 기준:
        /// 「5역할이 서로 다른 모습임을 **한 장에**」).
        ///
        /// 사람들이 마을 20m에 흩어져 있어 한 프레임에 다 넣으면 한 명이 50픽셀이 된다 —
        /// **판정 대상이 안 찍힌 샷은 판정이 아니다**(원장). 그래서 각자를 실제 자리에서 찍은
        /// 진짜 렌더를 타일로 붙인다. 아무도 옮기지 않고, 새로 그리지도 않는다.
        /// </summary>
        static void ContactSheet(string dir, System.Collections.Generic.List<string> names, string outName)
        {
            if (names.Count == 0)
            {
                Debug.LogWarning("[Ulon] 대조 시트 — 붙일 샷이 없습니다(0이면 실패).");
                return;
            }
            // **빈 칸을 만들지 않는다**(검수 지적) — 5장은 5칸 한 줄이다. 격자로 접으면 6칸째가 검게 남는다.
            int cols = names.Count;
            int rows = 1;
            int tw = W / 2, th = H / 2;
            var sheet = new Texture2D(cols * tw, rows * th, TextureFormat.RGB24, false);
            var fill = new Color32[cols * tw * rows * th];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(18, 18, 20, 255);
            sheet.SetPixels32(fill);
            var tile = new Texture2D(2, 2, TextureFormat.RGB24, false);
            for (int i = 0; i < names.Count; i++)
            {
                string path = Path.Combine(dir, names[i] + ".png");
                if (!File.Exists(path) || !tile.LoadImage(File.ReadAllBytes(path)))
                    continue;
                var small = ScaleHalf(tile, tw, th);
                int cx = (i % cols) * tw;
                int cy = (rows - 1 - i / cols) * th;      // 왼쪽 위부터 채운다(텍스처 원점은 아래)
                sheet.SetPixels(cx, cy, tw, th, small);
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(dir, outName + ".png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(tile);
            Object.DestroyImmediate(sheet);
            Debug.Log("[Ulon] 대조 시트 " + outName + ".png — " + names.Count + "장(" + string.Join(", ", names) + ")");
        }

        /// <summary>단순 축소(최근접) — 판정은 「누가 누구와 같은가」라 보간 품질이 결과를 바꾸지 않는다.</summary>
        static Color[] ScaleHalf(Texture2D src, int tw, int th)
        {
            var outp = new Color[tw * th];
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                    outp[y * tw + x] = src.GetPixelBilinear((x + 0.5f) / tw, (y + 0.5f) / th);
            return outp;
        }

        /// <summary>
        /// 씬의 액터들에 애니메이터 기본 상태(Idle) 포즈를 입힌다 — 에디터 배치모드에서는 애니메이션이
        /// 돌지 않아 바인드 포즈(T포즈)로 찍힌다. 반환값은 포즈가 적용된 액터 수.
        /// </summary>
        static int SampleIdlePose(out int total)
        {
            int n = 0;
            var anims = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            total = anims.Length;
            for (int i = 0; i < anims.Length; i++)
            {
                var rac = anims[i].runtimeAnimatorController;
                var ctrl = rac as AnimatorController;
                if (ctrl == null && rac != null)
                    ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(rac));
                // **건너뛴 것을 침묵으로 두지 마라** — 여기서 조용히 빠진 액터는 T포즈 그대로 찍힌다.
                // 「포즈 15체 적용」만 찍고 22개 중 7개가 왜 빠졌는지 안 적어 T포즈가 샷에 남았다.
                if (ctrl == null || ctrl.layers.Length == 0 || ctrl.layers[0].stateMachine == null)
                {
                    Debug.Log("[Ulon] QA 포즈 건너뜀(" +
                              (rac == null ? "컨트롤러 없음" : ctrl == null ? "컨트롤러를 에셋으로 못 읽음" : "레이어/상태기 없음") +
                              ") " + GroundFit.NodePath(anims[i].transform) + " — 이 액터는 바인드 포즈(T포즈)로 찍힙니다");
                    continue;
                }
                // 기본 상태의 motion이 블렌드 트리면 클립이 안 나온다 — 컨트롤러가 들고 있는 클립 중
                // 이름에 idle이 든 것을 쓴다(없으면 첫 클립).
                AnimationClip clip = null;
                var clips = ctrl.animationClips;
                for (int c = 0; c < clips.Length; c++)
                {
                    if (clips[c] == null)
                        continue;
                    if (clip == null)
                        clip = clips[c];
                    if (clips[c].name.ToLowerInvariant().Contains("idle"))
                    {
                        clip = clips[c];
                        break;
                    }
                }
                if (clip == null)
                {
                    Debug.Log("[Ulon] QA 포즈 건너뜀(클립 없음) " + anims[i].name + " ctrl=" + ctrl.name);
                    continue;
                }
                clip.SampleAnimation(anims[i].gameObject, 0.4f);
                Debug.Log("[Ulon] QA 포즈 적용 " + GroundFit.NodePath(anims[i].transform) + " ← " + clip.name);
                n++;
            }
            return n;
        }

        /// <summary>액터를 **정면에서** 잡는다 — 뒤에서 찍으면 망토만 보인다(잡몹 검수용).</summary>
        static Shot ActorCloseUp(string name, string objectName)
        {
            var go = GameObject.Find(objectName);
            if (go == null)
                return new Shot { Name = name, Eye = new Vector3(0f, 5f, -5f), Target = Vector3.zero };
            var p = go.transform.position;
            var target = p + new Vector3(0f, 1.1f, 0f);
            var eye = target + go.transform.forward * 3.0f + new Vector3(0f, 0.9f, 0f);
            return new Shot { Name = name, Eye = eye, Target = target };
        }

        /// <summary>
        /// **시설 근접**(검수 2026-09-07: 「시설 하나가 화면의 1/3 이상 차지하게」).
        /// 지난번 근접 샷은 플레이 카메라 거리(12m) 그대로라 사실상 마을 전경이었고, 대장간이
        /// 수십 픽셀이라 검수가 판정할 수 없었다. 그래서 거리를 **재서 정한다** —
        /// 시설의 보이는 바운드 반지름과 카메라 화각으로 「화면 높이의 절반을 채우는 거리」를 푼다.
        /// 각도(pitch·yaw)는 플레이 카메라와 같게 둔다 — 게임에서 보는 방향 그대로 판정하기 위해서다.
        /// **이건 플레이 거리 샷이 아니다**(자산이 무엇으로 읽히는지 보는 확대 샷이다) — 숨기지 않고 적는다.
        /// </summary>

        /// <summary>
        /// **방위 선택 네거티브 컨트롤** — 시설 앞에 **콜라이더 없는** 이웃을 세우면 방위가 바뀌는가.
        ///
        /// 콜라이더로 걸면 이번 구멍을 못 잰다: 예전 자가 콜라이더 광선이었고, 이웃 좌판·집 지붕에
        /// 콜라이더가 없어 「100% 보임」이 나왔던 것이 결함의 전부였다(검수 조건 2026-09-09).
        /// 그래서 막는 물건도 **보이기만 하고 콜라이더가 없는** 것으로 세운다.
        /// </summary>
        static void BearingNegativeControl(string facility)
        {
            var before = FacilityCloseUp("nc_bearing", facility);
            var subject = FindSubject(facility);
            if (subject == null)
                throw new System.InvalidOperationException("방위 NC 대상이 없습니다: " + facility + "(0이면 실패).");

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "QaBearingNcWall";
            Object.DestroyImmediate(wall.GetComponent<Collider>());     // **콜라이더 없이** — 이것이 이번 구멍이다
            wall.transform.localScale = new Vector3(8f, 8f, 0.4f);
            var eyeDir = (before.Eye - before.Target);
            eyeDir.y = 0f;
            wall.transform.position = before.Target + eyeDir.normalized * 2.0f + Vector3.up * 2f;
            wall.transform.rotation = Quaternion.LookRotation(eyeDir.normalized);
            ClearBlockerCache();                                        // 세계가 바뀌었으면 캐시도 버린다
            Shot after;
            try
            {
                after = FacilityCloseUp("nc_bearing", facility);
            }
            finally
            {
                Object.DestroyImmediate(wall);
                ClearBlockerCache();
            }
            var back = FacilityCloseUp("nc_bearing", facility);

            float moved = Vector3.Distance(new Vector3(before.Eye.x, 0f, before.Eye.z),
                                           new Vector3(after.Eye.x, 0f, after.Eye.z));
            if (moved < 1f)
                throw new System.InvalidOperationException("방위 네거티브 컨트롤 실패 — " + facility +
                    " 앞에 콜라이더 없는 벽을 세웠는데 카메라가 " + moved.ToString("0.00") +
                    "m밖에 안 움직였습니다(가림을 안 재고 있습니다).");
            float returned = Vector3.Distance(before.Eye, back.Eye);
            if (returned > 0.5f)
                throw new System.InvalidOperationException("방위 네거티브 컨트롤 실패 — 벽을 치웠는데 방위가 안 돌아왔습니다(" +
                    returned.ToString("0.00") + "m).");
            Debug.Log("[Ulon] 방위 NC 통과 — " + facility + " 앞에 콜라이더 없는 벽을 세우면 카메라가 " +
                      moved.ToString("0.0") + "m 비켜서고, 치우면 제자리로 돌아온다.");
        }

        static Shot FacilityCloseUp(string name, string objectName) => FacilityCloseUp(name, objectName, null);

        /// <param name="beyond">
        /// 이 지점이 **피사체 너머(배경)**에 오도록 카메라를 세운다. 낚시터처럼 「무엇 옆에 있는가」가
        /// 판정의 핵심인 시설에 쓴다 — 가림만 보고 방위를 고르면 물을 등지고 찍어 물이 화면에서 사라진다(실측).
        /// </param>
        static Shot FacilityCloseUp(string name, string objectName, Vector3? beyond) =>
            FacilityCloseUp(name, objectName, beyond, false);

        /// <param name="lowAngle">
        /// **발이 바닥에 닿았는지**를 보는 샷은 내려보는 각을 낮춘다. 시설용 각(35~65°)으로 사람을 찍으면
        /// 정수리와 어깨만 나와 **발과 바닥의 접점이 화면에 없다** — 판정 대상이 안 찍히는 샷은 판정이 아니다
        /// (첫 촬영본 39_boss1이 그랬다: 왕관만 보였다).
        /// </param>
        static Shot FacilityCloseUp(string name, string objectName, Vector3? beyond, bool lowAngle)
        {
            // **캐시는 한 샷보다 오래 살면 안 된다** — NC가 세계에 판을 세웠다 치웠다 하는데
            // 캐시가 남아 있으면 자가 옛 세계를 잰다(실측: 은행원 NC가 「둘러쌌는데도 통과」로 울었다).
            ClearBlockerCache();
            var go = FindSubject(objectName);
            if (go == null)
                return new Shot { Name = name, Eye = new Vector3(0f, 5f, -5f), Target = Vector3.zero };
            var rends = go.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            Bounds box = new Bounds();
            // **피사체가 사람이면 사람의 몸을 잰다.** 시설 프레이밍은 「시설에 서 있는 사람」을 빼는데,
            // 그 규칙을 사람 피사체에 그대로 적용했더니 바운드가 통째로 비어 `new Bounds()`의 중심,
            // 즉 **월드 원점**을 향해 방위를 골랐다(첫 촬영본 46_person_Vendor에 상인이 아예 없었다).
            // 무엇을 빼는가가 곧 정의다 — 장비는 빼고(GroundFit.BodyBounds) 몸만 잰다.
            // 사람 몸에서 **장비와 시설 부속을 뺀다** — 훈련사 밑에 걸린 시설 깃발(FacPart*)이 섞여
            // 몸이 2.9m로 읽혔고 카메라가 5.5m 뒤로 물러나 사람이 콩알이 됐다(첫 촬영본 47).
            if (go.GetComponent<CharacterController>() != null &&
                GroundFit.WorldBounds(go.transform, out Bounds body,
                    t => GroundFit.IsGear(go.transform, t) || GroundFit.IsFacilityPart(go.transform, t)))
            {
                box = body;
                any = true;
                Debug.Log("[Ulon] 근접 바운드 " + name + " ← 사람 몸 " + body.size.ToString("0.0"));
            }
            bool personBox = any;                       // 사람 몸을 이미 쟀으면 아래 시설 루프는 돌지 않는다
            for (int i = 0; i < rends.Length && !personBox; i++)
            {
                if (!rends[i].enabled || !rends[i].gameObject.activeInHierarchy)
                    continue;
                // **파티클은 프레이밍에서 뺀다** — 월드 시뮬레이션 파티클의 바운드는 수십 m로 잡혀
                // 「화덕 근접」이 마을 전경이 됐다(화덕에 불을 붙인 직후 실측).
                if (rends[i] is ParticleSystemRenderer)
                    continue;
                // **시설에 세운 사람은 시설의 크기가 아니다**(검수 승인 2026-09-07) — 스킨드 바운드가
                // 부풀어 근접 거리를 키운다. 파티클을 뺀 것과 같은 처리다.
                if (rends[i].GetComponentInParent<CharacterController>() != null)
                    continue;
                if (!any) { box = rends[i].bounds; any = true; }
                else box.Encapsulate(rends[i].bounds);
                Debug.Log("[Ulon] 근접 바운드 " + name + " ← " + rends[i].gameObject.name + " " + rends[i].bounds.size.ToString("0.0"));
            }
            var target = any ? box.center : go.transform.position + Vector3.up;
            float radius = any ? Mathf.Max(box.extents.magnitude, 0.6f) : 1.5f;
            // 화면 높이의 절반을 채우려면 거리 = 반지름 / tan(화각/2) — 여기에 1.35배 여유(가장자리 잘림 방지).
            float dist = radius / Mathf.Tan(55f * 0.5f * Mathf.Deg2Rad) * 1.35f;
            var qv = Object.FindFirstObjectByType<Ulon.Client.QuarterViewCamera>(FindObjectsInactive.Include);
            float pitch = qv != null ? qv.Pitch : 35f;
            float baseYaw = qv != null ? qv.Yaw : 45f;
            // **어느 쪽에서 봐야 시설이 보이는가를 잰다.** 마을 한복판이라 게임 방위 그대로 잡으면
            // 앞집이 가려 판정이 불가능한 샷이 나온다(첫 촬영본 30_forge가 그랬다). 네 방위를 쏴 보고
            // **가리는 것이 가장 적은 쪽**을 고른다 — 취향이 아니라 광선으로 고르고, 고른 쪽을 로그에 남긴다.
            float bestYaw = baseYaw;
            float bestPitch = pitch;
            float bestSeen = -1f;
            float bestDist = -1f;
            float bestLit = -2f;
            var blockers = new System.Collections.Generic.List<string>();
            var perBearing = new System.Collections.Generic.List<string>();
            int frontRejected = 0;
            // 마을은 시설이 2~3m 간격으로 붙어 있어 게임 각도에서는 앞집 지붕이 시설을 통째로 덮는다
            // (첫 촬영본 35_fishing이 그랬다). 방위 8 × 내려보는 각 3을 다 재고 제일 잘 보이는 조합을 쓴다.
            float[] pitches = lowAngle ? new[] { 10f, 18f, 26f } : new[] { pitch, 50f, 65f };
            for (int k = 0; k < 8 * pitches.Length; k++)
            {
                float y = baseYaw + (k % 8) * 45f;
                float pit = pitches[k / 8];
                var eyeK = target - Quaternion.Euler(pit, y, 0f) * Vector3.forward * dist;
                // 중심선 하나만 쏘면 「앞집 옆을 스쳐 지나가」 0개로 읽힌다(첫 시도가 그랬다) —
                // 시설 표면 표본에 쏴서 **몇 %가 실제로 이 시설로 먼저 닿는지**를 잰다(차폐 게이트와 같은 방식).
                int seen = 0, total = 0;
                bool byRenderer = go.GetComponent<CharacterController>() != null;
                for (int sx = -1; sx <= 1; sx++)
                    for (int sy = -1; sy <= 1; sy++)
                        for (int sz = -1; sz <= 1; sz++)
                        {
                            var p = box.center + new Vector3(sx * box.extents.x * 0.6f, sy * box.extents.y * 0.6f, sz * box.extents.z * 0.6f);
                            total++;
                            // **사람은 콜라이더로 가려짐을 못 잰다** — 좌판·집 같은 시설은 콜라이더가
                            // 없거나 성기어서 광선이 그냥 통과했고, 「100% 보인다」로 고른 방위에서
                            // 화면엔 벽만 찍혔다(첫 촬영본 46: 상인이 아예 없었다).
                            // 사람 피사체는 **보이는 것**(렌더러 바운드)으로 가려짐을 잰다.
                            // **시설도 사람과 같은 자로 잰다**(검수 판정 2026-09-09).
                            // 예전엔 시설만 **콜라이더 광선**으로 쟀는데 이웃 좌판·집 지붕엔 콜라이더가
                            // 없어 광선이 그냥 통과했다 — `30_forge`는 「표본 100% 보임」으로 기본 방위를
                            // 고르고 화면엔 이웃 지붕만 찍혔다. **자가 있는데 고르는 쪽이 안 부른 것**이다.
                            // 사람 쪽은 이미 렌더러(=보이는 것)로 옳게 재고 있었으므로 그 자를 부른다.
                            if (BlockedByRenderer(eyeK, p, go.transform))
                                continue;
                            seen++;
                        }
                float share = total > 0 ? seen / (float)total : 0f;
                // **사람은 앞에서 찍는다 — 선호가 아니라 규칙이다**(검수 반려 2026-09-07).
                // 처음엔 점수에 가산점으로 얹었더니 가림 점수에 묻혀 치유사가 뒷모습으로 찍혔다.
                // 등을 보이는 각은 아예 **후보에서 뺀다** — 얼굴이 없으면 「누구인지」가 화면에 없다.
                if (byRenderer && !IgnoreFrontRuleForNc && FrontDot(go.transform, target, pit, y, dist) < PersonFrontMin)
                {
                    frontRejected++;                 // 「가려서 못 찍는다」와 「앞이 아니라 못 쓴다」를 갈라 센다
                    continue;
                }
                // **「대상이 실제로 보이는가」로 후보를 거른다**(검수 반려 2026-09-08).
                // 처음엔 「반투명이 끼는 방위를 뺀다」로 걸었는데 그건 **대리 지표**였다 —
                // 반투명이 없다 ≠ 대상이 보인다. 불투명한 지붕에 통째로 가려진 방위가 그 규칙을
                // 통과해서, 게이트는 EXIT=0인데 `50_villagers` 한 타일은 붉은 지붕만, 마법사 타일은
                // 지붕 위로 모자만 나왔다. 그래서 **머리와 몸통 두 점 모두**가 카메라에서 안 막힌
                // 방위만 남긴다 — 반투명·불투명을 가리지 않는다(막힘은 막힘이다).
                if (byRenderer)
                {
                    // **껍데기 안으로 들어가서 찍는다**(검수 판정 2026-09-08, (ㄴ)).
                    // 은행원이 은행 안에, 상인이 차양 밑에, 훈련사가 지붕 밑에 서 있는 것은 §18.19가
                    // 맞게 구현된 모습이지 결함이 아니다. **샷이 안 찍힌다고 사람을 문 밖으로 옮기는
                    // (ㄱ)안은 금지다** — 그건 계측기가 세계를 바꾸는 짓이고, 게임은 나빠지고 숫자만 좋아진다.
                    // 그래서 밖에서 막히면 카메라를 그 껍데기 **안쪽까지** 당겨 본다. 하한 1.9m는
                    // 온몸이 화면에 들어오는 거리다(55° 화각·키 1.8m 기준 1.73m가 꽉 차는 거리) —
                    // 예전에 1.4m까지 열었다가 훈련사가 얼굴만 찍힌 개악을 되풀이하지 않기 위한 바닥이다.
                    float tryDist = -1f;
                    string lastBlocker = "";            // 이 방위에서 마지막으로 막은 것 — 방위별 로그의 실체
                    // **하한은 「더 당기지 마라」이지 「그보다 가까우면 안 본다」가 아니다.**
                    // 처음엔 `d >= 하한`으로만 돌렸더니, 원래 거리가 이미 1.9m 아래인 작은 피사체는
                    // 루프가 **한 번도 안 돌아** 「전 방위가 막혔다」로 보고됐다(실측: 마구간지기 —
                    // 실제로는 아무것도 안 막고 있었다). 원래 거리는 언제나 한 번 잰다.
                    float stop = Mathf.Min(dist, InsidePullFloor);
                    for (float d = dist; d >= stop - 0.01f; d -= 0.3f)
                    {
                        // 0.3m 격자가 하한을 건너뛰면 「1.9m에서 보이는데 못 찾는」 일이 생긴다(실측 은행원).
                        if (d - 0.3f < stop && d > stop)
                            d = stop;
                        var eyeD = target - Quaternion.Euler(pit, y, 0f) * Vector3.forward * d;
                        if (PersonBlocked(eyeD, box, go.transform, out string bd))
                        {
                            lastBlocker = bd;
                            if (bd != "" && !blockers.Contains(bd))
                                blockers.Add(bd);
                            continue;
                        }
                        tryDist = d;
                        break;
                    }
                    if (tryDist < 0f)
                    {
                        // **방위별로 무엇이 막았는지 남긴다**(검수 지시 2026-09-08, 마구간지기).
                        // 이 줄이 없어서 「막힘 기록 0」이 찍혔고, 나는 그것을 세계의 사실로 읽을 뻔했다 —
                        // 리스트를 만들어 놓고 채우지 않은 것은 **계측기가 결과를 만든 것**이다.
                        // 막은 이름이 비어 있으면 그것도 그대로 적는다(빈칸을 숨기면 다시 같은 오독이 난다).
                        if (perBearing.Count < 40)
                            perBearing.Add("요 " + y.ToString("0") + "°/내려 " + pit.ToString("0") + "° ← " +
                                           (lastBlocker == "" ? "(막은 이름 없음)" : lastBlocker));
                        continue;
                    }
                    // 가림이 같으면 **해를 등지지 않는 쪽**을 고른다(검수 지시 2026-09-09: 은행원 칸이
                    // 그늘로만 찍혔다). 방위를 옮기는 것은 세계를 안 바꾼다 — 사람을 문 밖으로 끌어내는
                    // (ㄱ)안과 다른 점이 그것이다. 그다음에야 **덜 당긴 방위**를 고른다.
                    float lit = SunFacing(pit, y);
                    bool tie = Mathf.Abs(share - bestSeen) <= 0.02f;
                    if (share > bestSeen + 0.02f
                        || (tie && lit > bestLit + 0.05f)
                        || (tie && Mathf.Abs(lit - bestLit) <= 0.05f && tryDist > bestDist))
                    {
                        bestSeen = share; bestYaw = y; bestPitch = pit; bestDist = tryDist; bestLit = lit;
                    }
                    continue;
                }
                if (share > bestSeen + 0.02f) { bestSeen = share; bestYaw = y; bestPitch = pit; }
            }
            // **차선으로 찍지 않는다**(검수 지시 2026-09-08). 뚫린 방위가 하나도 없으면 그 사실이
            // 곧 배치 보고다 — 기본 방위로 찍어 두되 **이름과 함께 실패로 올린다**(`PersonShotClear`).
            // 예전엔 「반투명이 끼는 차선」으로 몰래 찍었고, 그래서 못 쓰는 샷이 통과했다.
            if (go.GetComponent<CharacterController>() != null)
            {
                PersonShotClear[name] = bestSeen >= 0f;
                Debug.Log("[Ulon] 사람 샷 결론 " + name + "(" + go.name + ") — bestSeen " + bestSeen.ToString("0.00") +
                          " · clear " + (bestSeen >= 0f));
                if (bestSeen < 0f)
                {
                    // 진단 — **얼마나 더 들어가면 보이는가**를 같이 잰다(하한 1.9m는 판정용이고,
                    // 이 탐색은 「불가능인가, 하한이 문제인가」를 가른다).
                    float clears = -1f; float clearYaw = 0f, clearPitch = 0f;
                    for (int k = 0; k < 8 * pitches.Length && clears < 0f; k++)
                    {
                        float y2 = baseYaw + (k % 8) * 45f, pit2 = pitches[k / 8];
                        for (float d = InsidePullFloor; d >= 0.9f; d -= 0.15f)
                        {
                            var e2 = target - Quaternion.Euler(pit2, y2, 0f) * Vector3.forward * d;
                            if (PersonBlocked(e2, box, go.transform, out _))
                                continue;
                            clears = d; clearYaw = y2; clearPitch = pit2;
                            break;
                        }
                    }
                    Debug.Log("[Ulon] 사람 샷 진단 " + name + " — 원래 거리 " + dist.ToString("0.00") +
                              "m · 정면 탈락 " + frontRejected + " · 막힘 기록 " + perBearing.Count +
                              " · 바운드 " + box.size.ToString("0.00") + " · 반지름 " + radius.ToString("0.00"));
                    Debug.Log("[Ulon] 사람 샷 " + name + " — 머리·몸통이 다 보이는 방위가 없다(24조합 전부 막힘). " +
                              "기본 방위로 찍고 실패로 보고한다. 막은 것: " + string.Join(", ", blockers) +
                              "\n  방위별(정면 규칙에 걸린 것 " + frontRejected + "개 제외): " + string.Join(" / ", perBearing) +
                              " | 하한을 낮추면 " + (clears < 0f ? "0.9m까지 내려도 안 보인다" :
                              clears.ToString("0.0") + "m·방위 " + clearYaw.ToString("0") + "°/" + clearPitch.ToString("0") + "°에서 보인다"));
                }
            }
            if (beyond.HasValue)
            {
                // 배경에 둬야 할 것의 **반대편**에 선다 — 그래야 그것이 피사체 뒤로 들어온다.
                var away = target - beyond.Value; away.y = 0f;
                if (away.sqrMagnitude > 0.0001f)
                {
                    bestYaw = Quaternion.LookRotation(-away.normalized, Vector3.up).eulerAngles.y;
                    bestPitch = 20f;                     // 낮게 봐야 수면이 화면에 들어온다
                }
            }
            // 사람은 껍데기 안까지 당겨 고른 그 거리로 찍는다(위 (ㄴ) 판정).
            if (bestDist > 0f && !beyond.HasValue)
            {
                if (bestDist < dist - 0.05f)
                    Debug.Log("[Ulon] 사람 샷 " + name + " — 밖에서는 막혀 껍데기 안까지 " +
                              dist.ToString("0.0") + "m → " + bestDist.ToString("0.0") + "m로 들어가 찍는다");
                dist = bestDist;
            }
            var rot = Quaternion.Euler(bestPitch, bestYaw, 0f);
            // **방 안 피사체는 카메라도 방 안에 세운다**(검수 판정 2026-09-07 3(a)).
            // 밖에 서면 벽·뚜껑이 규칙대로 페이드돼 화면 위쪽에 바깥 지형·하늘이 들어온다(40·41이 그랬다) —
            // 방이 뚫린 게 아니라(뚜껑은 방 span+16m를 덮는다) **샷이 방 밖에서 찍힌 것**이다.
            dist = ClampInsideRoom(target, rot, dist);
            // **사람은 가리는 것 앞으로 당겨 선다**(검수 사소 지적 2026-09-07: 반투명 벽이 인물을 덮었다).
            // 페이드는 벽을 지워 주는 것이 아니라 **반투명하게** 만든다 — 그 유령 너머로 사람을 보면
            // 색이 섞여 「무슨 색 옷인가」가 흐려진다. 막는 것이 있으면 그 앞까지 카메라를 당긴다.
            if (go.GetComponent<CharacterController>() != null)
            {
                // 당김은 **반투명해질 것**(페이드 레이어)만 피한다. 처음엔 아무 렌더러나 피하게 했더니
                // 울타리·바닥 바운드까지 걸려 1.2m까지 붙었고 얼굴만 찍혔다(개악) — 그래서
                // ① 대상을 페이드 레이어로 좁히고 ② 원래 거리의 70%까지만 당긴다.
                // **하한 70%는 지킨다.** 「뚫린 방위가 없으면 더 깊이 당기자」고 1.4m까지 열어 봤더니
                // 훈련사가 **얼굴만** 찍혔다(원장에 이미 적힌 개악을 그대로 다시 밟았다 — 2026-09-07 재확인).
                // 유령이 남는 것보다 대상이 안 찍히는 것이 나쁘다.
                // 하한은 껍데기 안으로 들어갈 때 쓰는 하한과 **같은 값**을 쓴다 — 안 그러면
                // 애써 1.9m로 정한 자리를 이 루프가 1.5m까지 다시 당겨 얼굴만 남긴다(실측).
                float floor = Mathf.Max(InsidePullFloor, dist * 0.7f);
                int pulled = 0;
                while (dist > floor && FadeBlocked(target - rot * Vector3.forward * dist, target, go.transform))
                {
                    dist -= 0.2f;
                    pulled++;
                }
                if (pulled > 0)
                    Debug.Log("[Ulon] 사람 샷 가림 회피 " + name + " — " + (pulled * 0.2f).ToString("0.0") +
                              "m 당겨 " + dist.ToString("0.0") + "m에서 찍는다");
            }
            // **뚫린 방위가 없다고 이미 실패한 샷은 정면성으로 또 세지 않는다** — 한 결함에 게이트 하나다.
            // (기본 방위로 찍은 그림은 뒤통수일 수밖에 없어, 안 그러면 같은 원인으로 빨간불이 두 번 뜨고
            //  정작 「가려서 못 찍는다」는 진짜 사유가 정면 실패에 가려진다.)
            if (go.GetComponent<CharacterController>() != null && PersonShotClear.TryGetValue(name, out bool wasClear) && !wasClear)
                Debug.Log("[Ulon] 사람 샷 정면성 " + name + " — 가려서 못 찍은 샷이라 정면 판정에서 뺀다(가림 게이트가 잡는다)");
            else if (go.GetComponent<CharacterController>() != null)
            {
                float front = FrontDot(go.transform, target, bestPitch, bestYaw, dist);
                PersonShotFront[name] = front;
                Debug.Log("[Ulon] 사람 샷 정면성 " + name + " — " + front.ToString("0.00") +
                          "(하한 " + PersonFrontMin + ", 1=정면 -1=뒤통수)");
            }
            Debug.Log("[Ulon] 시설 근접 " + name + "(" + objectName + ") — 바운드 " + (any ? box.size.ToString("0.0") : "(없음)") +
                      ", 거리 " + dist.ToString("0.0") + "m, 방위 " + bestYaw.ToString("0") + "°/내려보기 " +
                      bestPitch.ToString("0") + "°(시설이 먼저 보이는 표본 " + (bestSeen * 100f).ToString("0") + "%)");
            return new Shot { Name = name, Eye = target - rot * Vector3.forward * dist, Target = target, PlayCamera = true, Subject = go.transform };
        }

        /// <summary>
        /// **네거티브 컨트롤 전용 스위치** — 켜면 정면 규칙이 없던 때로 돌아간다(가산점도 없다).
        /// 규칙을 넣고 나면 프레이밍이 **구조적으로** 앞을 고르기 때문에, 씬을 돌려세워도 결함이 안 만들어진다.
        /// 그래서 규칙 자체를 끄고 「그때는 뒷모습이 나오는가」를 확인한다(앵커 NC와 같은 처방).
        /// </summary>
        public static bool IgnoreFrontRuleForNc;

        /// <summary>사람 샷의 프레이밍만 다시 계산해 정면성 표를 갱신한다(렌더는 하지 않는다).</summary>
        public static void RecomputePersonFront()
        {
            PersonShotFront.Clear();
            PersonShotClear.Clear();
            var people = VillagerLook.Villagers();
            for (int i = 0; i < people.Count; i++)
                FacilityCloseUp((45 + i).ToString("00") + "_person_" + VillagerLook.HostOf(people[i]),
                                people[i].name, null, true);
        }

        /// <summary>눈과 피사체 사이에 **페이드될 것**(DungeonBlocker 레이어)이 있는가.</summary>
        static bool FadeBlocked(Vector3 eye, Vector3 point, Transform subject)
        {
            int layer = LayerMask.NameToLayer(Ulon.Client.DungeonSightFade.BlockerLayer);
            if (layer < 0)
                return false;
            var seg = point - eye;
            float len = seg.magnitude;
            if (len < 0.001f)
                return false;
            var ray = new Ray(eye, seg / len);
            var rends = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || rends[i].gameObject.layer != layer)
                    continue;
                var t = rends[i].transform;
                if (t == subject || t.IsChildOf(subject))
                    continue;
                if (rends[i].bounds.IntersectRay(ray, out float d) && d < len - 0.05f)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 껍데기 안으로 들어갈 때의 **최소 거리** — 온몸이 화면에 들어오는 거리다
        /// (55° 화각·키 1.8m면 1.73m에서 화면 높이를 꽉 채운다). 이 아래로는 얼굴만 찍힌다.
        /// </summary>
        const float InsidePullFloor = 1.9f;

        /// <summary>사람 샷이 앞에서 찍혔다고 인정하는 최소 정면성(코사인) — 0.2는 정면 ±78°다.</summary>
        public const float PersonFrontMin = 0.2f;

        /// <summary>
        /// 이번 실행의 사람 샷이 **뚫린 방위에서 찍혔는가** — 샷 이름 → 참/거짓.
        /// 거짓이면 그 사람은 24개 방위·내려보기 조합 어디에서도 머리·몸통이 다 보이지 않는다는 뜻이고,
        /// 그건 샷의 문제가 아니라 **배치 보고**다(검수 지시 2026-09-08).
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<string, bool> PersonShotClear =
            new System.Collections.Generic.Dictionary<string, bool>();

        /// <summary>이번 실행의 사람 샷 정면성 — 샷 이름 → 코사인. 게이트가 이 값을 판정한다.</summary>
        public static readonly System.Collections.Generic.Dictionary<string, float> PersonShotFront =
            new System.Collections.Generic.Dictionary<string, float>();

        /// <summary>카메라가 사람의 앞쪽에 있는가 — 1이면 정면, -1이면 뒤통수.</summary>
        static float FrontDot(Transform person, Vector3 target, float pit, float yaw, float dist)
        {
            var toEye = -(Quaternion.Euler(pit, yaw, 0f) * Vector3.forward * dist);
            toEye.y = 0f;
            if (toEye.sqrMagnitude < 0.0001f)
                return 0f;
            return Vector3.Dot(toEye.normalized, person.forward);
        }

        /// <summary>
        /// 이름으로 피사체를 찾되 **액터(CharacterController)를 먼저** 고른다.
        /// KayKit 프리팹 안에 모델 이름과 같은 노드가 있어(`Rogue/Rogue`) `GameObject.Find`가
        /// **속 노드**를 집었고, 그 노드엔 CC가 없어 사람 판정이 빗나가 바운드가 통째로 비었다
        /// (첫 촬영본 53_rogue: 프레이밍이 대상 없이 잡혔다). **이름은 유일하지 않다.**
        /// </summary>
        static GameObject FindSubject(string objectName)
        {
            var actors = Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < actors.Length; i++)
                if (actors[i].gameObject.name == objectName)
                    return actors[i].gameObject;
            return GameObject.Find(objectName);
        }

        /// <summary>
        /// **둘을 한 화면에** — 동료가 플레이어와 갈리는지는 나란히 놓고 봐야 판정된다(검수 완료 기준).
        /// 두 몸 바운드를 합쳐 가운데를 보고, 플레이어 앞쪽에서 낮게 찍는다(얼굴·앞섶이 보이게).
        /// </summary>
        static Shot PairCloseUp(string name, string aName, string bName)
        {
            // **이름은 유일하지 않다** — `GameObject.Find("Rogue")`는 실체가 아니라 프리팹 속 같은 이름의
            // 노드를 집는다(근접 샷에서 이미 당했다). 여기도 같은 함정이었다: 두 거리는 「같다」고 찍히는데
            // 화면에서는 한쪽이 확연히 작았다 — **화면이 계측과 어긋나면 계측이 다른 것을 재고 있는 것**이다.
            var a = FindSubject(aName);
            var b = FindSubject(bName);
            if (a == null || b == null ||
                !GroundFit.BodyBounds(a.transform, out Bounds ba) || !GroundFit.BodyBounds(b.transform, out Bounds bb))
                return new Shot { Name = name, Eye = new Vector3(0f, 5f, -5f), Target = Vector3.zero };
            var box = ba; box.Encapsulate(bb);
            // **나란히 비교하는 샷은 두 대상을 같은 거리에 둔다**(검수 규칙 2026-09-07) — 거리가 다르면
            // 원근이 크기를 바꿔 「갈리는가」 판정이 오염된다. 합친 상자의 중심은 두 몸 중앙이 아니다
            // (덩치 큰 쪽으로 끌린다) — **두 몸 중심의 중점**을 보고, 그 둘을 잇는 선의 **수직**에서 본다.
            // 그러면 두 거리는 대칭으로 같아진다. 아래 로그가 실제 두 거리를 찍어 규칙을 증명한다.
            var target = (ba.center + bb.center) * 0.5f;
            float radius = Mathf.Max(box.extents.magnitude, 0.8f);
            float dist = radius / Mathf.Tan(55f * 0.5f * Mathf.Deg2Rad) * 1.25f;
            // 둘이 나란히 서므로 **둘을 잇는 선의 옆**에서 봐야 서로 겹치지 않는다. 그 두 방향 중
            // 플레이어의 앞쪽을 고른다 — 뒤통수 둘을 찍으면 누가 누구인지가 화면에 없다.
            var along = bb.center - ba.center; along.y = 0f;
            var side = Vector3.Cross(along.normalized, Vector3.up);
            if (Vector3.Dot(side, a.transform.forward) < 0f)
                side = -side;
            var eye = target + side * dist + Vector3.up * dist * 0.30f;
            float da = Vector3.Distance(eye, ba.center), db = Vector3.Distance(eye, bb.center);
            Debug.Log("[Ulon] 둘 근접 " + name + " — 합친 바운드 " + box.size.ToString("0.0") + ", 거리 " + dist.ToString("0.0") +
                      "m, 두 대상까지 " + aName + " " + da.ToString("0.00") + "m ↔ " + bName + " " + db.ToString("0.00") +
                      "m (차이 " + Mathf.Abs(da - db).ToString("0.00") + "m — 같아야 원근이 크기를 안 바꾼다)" +
                      " | 화면 높이 비 " + ((ba.size.y / da) / Mathf.Max(0.0001f, bb.size.y / db)).ToString("0.00") +
                      " (몸 " + ba.size.y.ToString("0.00") + "m·" + bb.size.y.ToString("0.00") + "m, 자리 " +
                      ba.center.ToString("F1") + "·" + bb.center.ToString("F1") + ")");
            return new Shot { Name = name, Eye = eye, Target = target, PlayCamera = true, Subject = a.transform };
        }

        /// <summary>
        /// 눈에서 표본점까지 **보이는 것**이 가로막는가 — 콜라이더가 아니라 렌더러 바운드로 잰다.
        /// 피사체 자신과 지형은 막는 것으로 세지 않는다(지형은 발밑이라 늘 걸린다).
        /// </summary>

        /// <summary>
        /// 가림 판정이 훑는 렌더러 목록 — **한 판에 한 번만** 모은다.
        /// 시설 근접까지 렌더러로 재게 되면서 호출이 표본 27 × 방위 24로 늘었다.
        /// 매번 `FindObjectsByType`를 돌면 잰 값은 같은데 시간만 든다.
        /// 수명은 **한 샷**이다 — `FacilityCloseUp` 첫 줄에서 버린다(NC가 판을 세웠다 치웠다 하므로).
        /// </summary>
        static Renderer[] blockerCache;

        static Renderer[] BlockerCache()
        {
            if (blockerCache == null)
                blockerCache = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            return blockerCache;
        }

        static void ClearBlockerCache() => blockerCache = null;

        static bool BlockedByRenderer(Vector3 eye, Vector3 point, Transform subject)
            => BlockedByRenderer(eye, point, subject, out _);

        /// <summary>
        /// **사람이 판정할 만큼 보이는가** — 머리와 몸통 두 점을 본다.
        ///
        /// **다섯 점(실루엣 좌우 끝·손 높이)까지 요구해 봤다가 되돌렸다**(2026-09-09):
        /// 검수가 지적한 `50_villagers` 첫 칸(은행원)은 **가림이 아니라 그늘 + 벽 페이드**였고
        /// — 원본 샷에서는 모자 비례도 손도 읽힌다 — 다섯 점으로 조인 결과 고친 것은 없이
        /// **훈련사가 아예 못 찍히는** 대가만 남았다(전 방위 탈락). 자를 조이면 세계가 좁아진다.
        /// 남은 밝기 문제는 「후보 방위 중 밝은 쪽 고르기」로 따로 잡는다(검수: 랩 B 뒤로).
        /// </summary>
        static bool PersonBlocked(Vector3 eye, Bounds box, Transform subject, out string blocker)
        {
            var points = new[]
            {
                box.center + Vector3.up * box.extents.y * 0.8f,   // 머리
                box.center,                                        // 몸통
            };
            for (int i = 0; i < points.Length; i++)
                if (BlockedByRenderer(eye, points[i], subject, out blocker))
                    return true;
            blocker = "";
            return false;
        }

        /// <summary>같은 판정에 **무엇이 막았는지**를 같이 돌려준다 — 「막혔다」만으로는
        /// 진짜 지붕인지 남의 바운드가 부푼 것인지 구분할 수 없다(이 저장소가 여러 번 밟은 함정).</summary>
        static bool BlockedByRenderer(Vector3 eye, Vector3 point, Transform subject, out string blocker)
        {
            blocker = "";
            var seg = point - eye;
            float len = seg.magnitude;
            if (len < 0.001f)
                return false;
            var ray = new Ray(eye, seg / len);
            var rends = BlockerCache();
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || rends[i] is ParticleSystemRenderer)
                    continue;
                var t = rends[i].transform;
                if (t == subject || t.IsChildOf(subject))
                    continue;
                if (rends[i].GetComponent<TerrainCollider>() != null || t.GetComponent<Terrain>() != null)
                    continue;
                // **선언된 예외 하나: 플레이어 아바타.** QA 씬의 플레이어는 스폰 자리에 세워 둔
                // 소품이라, 그 몸이 훈련사 앞을 막아 24방위가 전부 막힌 것으로 읽혔다(실측
                // `Player>Knight_Helmet`). 실제 플레이에서 플레이어는 비켜서면 그만이므로
                // 「구조적으로 안 보이는 자리」가 아니다 — 다른 사람·짐승은 그대로 가리는 것으로 센다.
                if (t.root.name == "Player")
                    continue;
                if (Vector3.Distance(rends[i].bounds.center, point) > 30f)
                    continue;                                   // 멀리 있는 것은 이 표본을 못 가린다
                // **카메라가 그 껍데기 안에 있으면 그것은 가리는 것이 아니다**(검수 (ㄴ) 판정 2026-09-08).
                // 바운드 안에서 쏜 광선은 `IntersectRay`가 거리 0으로 참을 돌려주기 때문에,
                // 안으로 들어가 찍는 순간 지붕이 스스로를 「가림」으로 세었다(당겨도 계속 빨간불이던 이유).
                if (rends[i].bounds.Contains(eye))
                    continue;
                // **표본 점이 남의 바운드 안에 있다고 봐주지 않는다**(2026-09-08 되돌림).
                // 한때 「바운드가 머리를 품으면 판정 불가」로 건너뛰었더니, 훈련사 타일이
                // **청록 지붕 뒤로 모자만 나온 채 통과**했다 — 자의 한계를 봐주는 규칙이
                // 곧 못 쓰는 샷을 통과시키는 구멍이 된다. 판정은 자를 느슨하게 해서가 아니라
                // 사람이 지붕 밑에 있다는 **사실을 보고**해서 닫는다.
                if (rends[i].bounds.IntersectRay(ray, out float dist) && dist < len - 0.05f)
                {
                    blocker = t.root.name + ">" + (t.parent != null ? t.parent.name + "/" : "") + rends[i].gameObject.name +
                              "(바운드 " + rends[i].bounds.size.ToString("0.0") + ")";
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 피사체가 던전 방 안이면 카메라가 방 벽을 넘지 않도록 **거리를 줄인다**.
        /// 각도는 그대로 둔다 — 판정 각(발-바닥 접점)이 바뀌면 안 되기 때문이다.
        /// </summary>
        static float ClampInsideRoom(Vector3 target, Quaternion rot, float dist)
        {
            var rooms = new[]
            {
                (new Vector2(Dungeon1.InteriorX, Dungeon1.InteriorZ), Dungeon1.RoomHalf),
                (new Vector2(Dungeon2.InteriorX, Dungeon2.InteriorZ), Dungeon2.RoomHalf),
                (new Vector2(Dungeon3.InteriorX, Dungeon3.InteriorZ), Dungeon3.RoomHalf),
            };
            var flat = new Vector2(target.x, target.z);
            for (int i = 0; i < rooms.Length; i++)
            {
                if (Vector2.Distance(flat, rooms[i].Item1) > rooms[i].Item2)
                    continue;                                   // 이 방 안의 피사체가 아니다
                float half = rooms[i].Item2 - 0.8f;             // 벽 두께·여유
                for (int k = 0; k < 40; k++)                    // 0.2m씩 당기며 방 안에 들어올 때까지
                {
                    var eye = target - rot * Vector3.forward * dist;
                    if (Vector2.Distance(new Vector2(eye.x, eye.z), rooms[i].Item1) <= half)
                        break;
                    dist -= 0.2f;
                    if (dist < 1.2f) { dist = 1.2f; break; }
                }
                Debug.Log("[Ulon] 근접 샷 방 안 제한 — 거리 " + dist.ToString("0.0") + "m로 당김(방 반경 " +
                          rooms[i].Item2.ToString("0.0") + "m)");
                break;
            }
            return dist;
        }

        /// <summary>보스 근접 — 왕관·큰 무기를 확인하는 검수용 샷(검수 요청 2026-09-06).</summary>
        static Shot BossCloseUp(string name, float bx, float bz)
        {
            float y = GroundY(bx, bz) - VisualSliceBuilder.DungeonDepth;
            var target = new Vector3(bx, y + 1.5f, bz);
            // 방 중앙 쪽에서 본다 — 보스는 벽 가까이 서 있어 바깥쪽에서 잡으면 벽 속이다.
            var toCenter = new Vector3(Dungeon3.InteriorX - bx, 0f, Dungeon3.InteriorZ - bz).normalized;
            return new Shot { Name = name, Eye = target + toCenter * 3.4f + new Vector3(0f, 1.6f, 0f), Target = target };
        }

        /// <summary>임의 시점 — 조망 샷용.</summary>
        /// <summary>
        /// 이 방위에서 **피사체의 카메라 쪽 면이 얼마나 해를 받는가**(-1~1). 카메라가 있는 쪽 방향과
        /// 햇빛이 오는 방향이 같을수록 1이다 — 해를 등지고 찍으면 얼굴이 통째로 그늘이다.
        /// </summary>
        static float SunFacing(float pitch, float yaw)
        {
            var sun = Object.FindFirstObjectByType<Light>(FindObjectsInactive.Include);
            Light dir = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { dir = l; break; }
            if (dir == null && sun == null)
                return 0f;
            Vector3 from = -(dir != null ? dir.transform.forward : sun.transform.forward);  // 햇빛이 오는 쪽
            Vector2 sunSide = new Vector2(from.x, from.z);
            if (sunSide.sqrMagnitude < 0.0001f)
                return 0f;
            Vector3 eyeDir = -(Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward);          // 카메라가 있는 쪽
            Vector2 camSide = new Vector2(eyeDir.x, eyeDir.z);
            if (camSide.sqrMagnitude < 0.0001f)
                return 0f;
            return Vector2.Dot(sunSide.normalized, camSide.normalized);
        }

        static Shot Free(string name, Vector3 eye, Vector3 target)
        {
            return new Shot { Name = name, Eye = eye, Target = target };
        }

        /// <summary>바깥에서 대상 주위를 내려다본다.</summary>
        static Shot Orbit(string name, Vector3 target, float dist, float pitch)
        {
            float y = GroundY(target.x, target.z);
            var t = new Vector3(target.x, y + 1.2f, target.z);
            float rad = pitch * Mathf.Deg2Rad;
            var eye = t + new Vector3(-dist * Mathf.Cos(rad), dist * Mathf.Sin(rad) + 1.5f, -dist * Mathf.Cos(rad)) * 0.7071f;
            return new Shot { Name = name, Eye = eye, Target = t };
        }

        /// <summary>요를 지정해 비스듬히 본다 — 정면에서만 멀쩡한 배치를 걸러내는 각이다.</summary>
        static Shot Angled(string name, Vector3 target, float dist, float pitch, float yaw)
        {
            float y = GroundY(target.x, target.z);
            var t = new Vector3(target.x, y + 1.2f, target.z);
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            return new Shot { Name = name, Eye = t - rot * Vector3.forward * dist, Target = t };
        }

        /// <summary>방 안에서 찍는다 — 천장이 있는 실내는 밖에서 보면 뚜껑만 보인다.</summary>
        static Shot Inside(string name, float cx, float cz, float lookX, float lookZ)
        {
            float y = GroundY(cx, cz) - VisualSliceBuilder.DungeonDepth;
            var eye = new Vector3(cx - 4.4f, y + 2.0f, cz - 4.4f);
            var target = new Vector3(lookX, y + 1.0f, lookZ);
            return new Shot { Name = name, Eye = eye, Target = target };
        }

        /// <summary>
        /// **플레이 카메라 그대로** 찍는다 — 씬의 QuarterViewCamera 값(pitch·yaw·distance)을 읽고
        /// 런타임과 같은 차폐 페이드를 적용한다. 검증 카메라가 플레이 카메라와 다르면 증거가 아니다.
        /// </summary>
        static Shot PlayCam(string name, float cx, float cz) => PlayCam(name, cx, cz, cx, cz);

        static Shot Vfx(Shot shot) { shot.Vfx = true; return shot; }

        /// <summary>
        /// **런타임과 같은 규칙으로** 줌을 고른다 — `QuarterViewCamera.IsIndoor`가 실내라고 하면 실내 줌.
        /// 실내/야외를 내가 골라 찍으면 「줌이 튀는지」를 증거로 쓸 수 없다.
        /// </summary>
        static Shot PlayCamAuto(string name, float cx, float cz)
        {
            var qv = Object.FindFirstObjectByType<Ulon.Client.QuarterViewCamera>(FindObjectsInactive.Include);
            float pitch = qv != null ? qv.Pitch : 35f;
            float yaw = qv != null ? qv.Yaw : 45f;
            var player = new Vector3(cx, GroundY(cx, cz) + 1.0f, cz);
            bool indoor = Ulon.Client.QuarterViewCamera.IsIndoor(player);
            float dist = qv != null ? (indoor ? Mathf.Min(qv.Distance, qv.IndoorDistance) : qv.Distance) : 12f;
            Debug.Log("[Ulon] QA 자동 줌 " + name + " — IsIndoor=" + indoor + ", 거리 " + dist.ToString("0.0") + "m");
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            return new Shot { Name = name, Eye = player - rot * Vector3.forward * dist, Target = player, PlayCamera = true };
        }

        /// <summary>
        /// **동료가 카메라와 플레이어 사이에 선 화면**(검수 의심 확인용 2026-09-07).
        /// 야외 시선 게이트의 최악 20%가 `Companion`이었는데 「몹 제외」로 빠져서, 화면에서 얼마나
        /// 나쁜지는 아무도 안 봤다. 동료를 옮기지 않는다 — **플레이어를 동료 뒤에 세운다**
        /// (카메라 각은 고정이라 시야선은 동료를 지나간다). 씬을 흔들지 않고 그 상황을 만든다.
        /// </summary>
        static Shot CompanionBlock(string name)
        {
            // **게이트가 최악이라고 지목한 그 자리**를 그대로 재현한다(마을 (-2,2), 가림 20% ← Companion).
            // 내가 임의로 만든 배치는 게이트가 잰 상황이 아니다 — 증거는 잰 자리에서 찍어야 한다.
            var comp = FindSubject("Companion");
            var qv = Object.FindFirstObjectByType<Ulon.Client.QuarterViewCamera>(FindObjectsInactive.Include);
            float pitch = qv != null ? qv.Pitch : 35f;
            float yaw = qv != null ? qv.Yaw : 45f;
            // **거리는 줄인다**(검수 반려 2026-09-07: 잘라 확대해야 보이는 샷은 증거로서 미완성).
            // 각(요·피치)은 플레이 카메라 그대로라 「가리는가」의 기하는 같고, 거리만 당겨
            // 판정 대상이 화면의 1/3 이상을 차지하게 한다.
            float dist = 7f;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            if (comp == null)
                return new Shot { Name = name, Eye = new Vector3(0f, 5f, -5f), Target = Vector3.zero };
            // 카메라 → 플레이어 방향이 -(rot*forward)의 반대이므로, 동료보다 **더 먼 쪽**에 플레이어를 둔다.
            // 플레이어는 스폰 자리(0,0)에 세운다 — 동료 자리는 이 자리 기준으로 정해진 상수다.
            var player = new Vector3(0f, GroundY(0f, 0f) + 1.0f, 0f);
            Debug.Log("[Ulon] 동료 가림 샷 — 동료 " + comp.transform.position.ToString("0.0") +
                      ", 플레이어 " + player.ToString("0.0") + " (카메라 거리 " + dist.ToString("0.0") + "m)");
            return Stand(new Shot
            {
                Name = name,
                Eye = player - rot * Vector3.forward * dist,
                Target = player,
                PlayCamera = true,
            });
        }

        /// <summary>
        /// **플레이 카메라로 그 사람에게 다가간 화면**(검수 조건 2026-09-08).
        /// 근접 샷은 껍데기 안으로 들어가 찍지만, 플레이어가 실제로 다가갈 때 지붕이 걷혀
        /// 보이는지는 **플레이 카메라 각·거리**로만 확인된다 — 샷에서만 보이고 플레이에서
        /// 못 보는 사람이면 그건 진짜 배치 결함이다.
        /// </summary>
        static Shot PersonPlayCam(string name, string objectName)
        {
            var go = FindSubject(objectName);
            var qv = Object.FindFirstObjectByType<Ulon.Client.QuarterViewCamera>(FindObjectsInactive.Include);
            float pitch = qv != null ? qv.Pitch : 35f;
            float yaw = qv != null ? qv.Yaw : 45f;
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            if (go == null)
                return new Shot { Name = name, Eye = new Vector3(0f, 5f, -5f), Target = Vector3.zero };
            // 플레이어는 그 사람 **바로 옆**(카메라 쪽으로 1.2m)에 선다 — 다가간 상황을 재현한다.
            var toCam = rot * Vector3.back;
            toCam.y = 0f;
            toCam = toCam.sqrMagnitude > 0.0001f ? toCam.normalized : Vector3.back;
            var p = go.transform.position + toCam * 1.2f;
            var player = new Vector3(p.x, GroundY(p.x, p.z) + 1.0f, p.z);
            float dist = 5f;                                  // 판정 대상이 화면의 1/3 이상을 차지하는 거리
            Debug.Log("[Ulon] 플레이 접근 샷 " + name + " — " + objectName + " " +
                      go.transform.position.ToString("0.0") + ", 플레이어 " + player.ToString("0.0"));
            return Stand(new Shot
            {
                Name = name,
                Eye = player - rot * Vector3.forward * dist,
                Target = player,
                PlayCamera = true,
            });
        }

        static Shot Stand(Shot shot) { shot.StandPlayer = true; return shot; }

        /// <summary>(hx,hz)의 지표에서 방 바닥 높이를 정한다 — 귀퉁이 샷은 방 중심 높이를 써야 바닥을 안 벗어난다.</summary>
        static Shot PlayCam(string name, float cx, float cz, float hx, float hz)
        {
            var qv = Object.FindFirstObjectByType<Ulon.Client.QuarterViewCamera>(FindObjectsInactive.Include);
            float pitch = qv != null ? qv.Pitch : 35f;
            float yaw = qv != null ? qv.Yaw : 45f;
            // 실내는 런타임과 같은 실내 줌 거리로 찍는다(§4.2 줌 허용) — 밖에서 찍으면 지붕 윗면만 나온다.
            float dist = qv != null ? Mathf.Min(qv.Distance, qv.IndoorDistance) : 5.5f;
            // 방은 지하다 — 플레이어는 지면이 아니라 방 바닥에 선다.
            float y = GroundY(hx, hz) - VisualSliceBuilder.DungeonDepth;
            var player = new Vector3(cx, y + 1.0f, cz);
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            return new Shot { Name = name, Eye = player - rot * Vector3.forward * dist, Target = player, PlayCamera = true };
        }

        /// <summary>
        /// **야외** 플레이 카메라 — 지표에 서고 야외 줌 거리(qv.Distance)를 쓴다.
        /// 실내용 `PlayCam`은 방 깊이를 빼므로 마을에 쓰면 플레이어가 지하로 들어간다.
        /// </summary>
        static Shot PlayCamOutdoor(string name, float cx, float cz)
        {
            var qv = Object.FindFirstObjectByType<Ulon.Client.QuarterViewCamera>(FindObjectsInactive.Include);
            float pitch = qv != null ? qv.Pitch : 35f;
            float yaw = qv != null ? qv.Yaw : 45f;
            float dist = qv != null ? qv.Distance : 12f;
            var player = new Vector3(cx, GroundY(cx, cz) + 1.0f, cz);
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            return new Shot { Name = name, Eye = player - rot * Vector3.forward * dist, Target = player, PlayCamera = true };
        }

        /// <summary>천장 위에서 방 전체 배치를 본다(뚜껑 포함 — 실내 여부 자체 확인용).</summary>
        static Shot Roof(string name, float cx, float cz)
        {
            float y = GroundY(cx, cz);
            return new Shot
            {
                Name = name,
                Eye = new Vector3(cx - 14f, y + 12f, cz - 14f),
                Target = new Vector3(cx, y + 1.5f, cz)
            };
        }

        static float GroundY(float x, float z)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                return 0f;
            return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
        }
    }
}
