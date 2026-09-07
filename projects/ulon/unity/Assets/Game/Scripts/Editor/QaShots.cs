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
    /// </summary>
    public static class QaShots
    {
        const int W = 1280;
        const int H = 720;

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

            var shots = new[]
            {
                Orbit("01_village_square", new Vector3(0f, 0f, 0f), 20f, 35f),
                Orbit("02_village_wide", new Vector3(0f, 0f, 0f), 55f, 45f),
                Orbit("03_hunt_mobs", new Vector3(2.8f, 0f, 13.6f), 26f, 30f),
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
                Free("16_mountain_ridge", new Vector3(60f, 30f, 60f), new Vector3(WorldTerrain.MountainPeak, WorldTerrain.LandBase + 18f, WorldTerrain.MountainPeak * 0.4f)),
            };

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
            Debug.Log("[Ulon] QA 파티클 시뮬레이션 — 계속 나는 효과 " + simmed + "개");

            int posed = SampleIdlePose();
            Debug.Log("[Ulon] QA 포즈 샘플링 — Idle 적용 액터 " + posed + "체");

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

            // VFX는 카메라 렌더가 필요해 -nographics 셀프체크에서 잴 수 없다 — 여기서 화면으로 잰다.
            SliceSelfCheck.AssertActionVfxOnScreen();
            SliceSelfCheck.AssertActionVfxNegativeControl();
            // 검은 배경에서 보이는 것과 **실제 플레이 프레임**에서 읽히는 것은 다르다(검수 랩 D).
            SliceSelfCheck.AssertActionVfxInPlayFrame();
            SliceSelfCheck.AssertActionVfxInPlayFrameNegativeControl();
        }

        /// <summary>
        /// 씬의 액터들에 애니메이터 기본 상태(Idle) 포즈를 입힌다 — 에디터 배치모드에서는 애니메이션이
        /// 돌지 않아 바인드 포즈(T포즈)로 찍힌다. 반환값은 포즈가 적용된 액터 수.
        /// </summary>
        static int SampleIdlePose()
        {
            int n = 0;
            var anims = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log("[Ulon] QA 포즈 — 애니메이터 " + anims.Length + "개");
            for (int i = 0; i < anims.Length; i++)
            {
                var rac = anims[i].runtimeAnimatorController;
                var ctrl = rac as AnimatorController;
                if (ctrl == null && rac != null)
                    ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(rac));
                if (ctrl == null || ctrl.layers.Length == 0 || ctrl.layers[0].stateMachine == null)
                {
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
            var go = GameObject.Find(objectName);
            if (go == null)
                return new Shot { Name = name, Eye = new Vector3(0f, 5f, -5f), Target = Vector3.zero };
            var rends = go.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            Bounds box = new Bounds();
            for (int i = 0; i < rends.Length; i++)
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
                for (int sx = -1; sx <= 1; sx++)
                    for (int sy = -1; sy <= 1; sy++)
                        for (int sz = -1; sz <= 1; sz++)
                        {
                            var p = box.center + new Vector3(sx * box.extents.x * 0.6f, sy * box.extents.y * 0.6f, sz * box.extents.z * 0.6f);
                            var seg = p - eyeK;
                            total++;
                            var hits = Physics.RaycastAll(eyeK, seg.normalized, seg.magnitude, ~0, QueryTriggerInteraction.Ignore);
                            System.Array.Sort(hits, (u, v) => u.distance.CompareTo(v.distance));
                            bool blocked = false;
                            for (int h = 0; h < hits.Length; h++)
                            {
                                if (hits[h].collider == null || SliceSelfCheck.IsTerrainCollider(hits[h].collider))
                                    continue;
                                // **「내 콜라이더에 맞았는가」로 재면 안 된다** — 발판·돌·분수처럼 콜라이더가
                                // 없는 시설은 광선이 그냥 통과해 「안 보임」으로 세어졌다(실측 11%).
                                // 재려는 것은 **가림**이다: 남의 것이 먼저 맞으면 가려진 것이고, 아무것도
                                // 안 맞으면 뚫려 있는 것이다.
                                blocked = !hits[h].collider.transform.IsChildOf(go.transform);
                                break;                      // 가장 가까운 것 하나만 본다
                            }
                            if (!blocked)
                                seen++;
                        }
                float share = total > 0 ? seen / (float)total : 0f;
                if (share > bestSeen + 0.02f) { bestSeen = share; bestYaw = y; bestPitch = pit; }
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
            var rot = Quaternion.Euler(bestPitch, bestYaw, 0f);
            // **방 안 피사체는 카메라도 방 안에 세운다**(검수 판정 2026-09-07 3(a)).
            // 밖에 서면 벽·뚜껑이 규칙대로 페이드돼 화면 위쪽에 바깥 지형·하늘이 들어온다(40·41이 그랬다) —
            // 방이 뚫린 게 아니라(뚜껑은 방 span+16m를 덮는다) **샷이 방 밖에서 찍힌 것**이다.
            dist = ClampInsideRoom(target, rot, dist);
            Debug.Log("[Ulon] 시설 근접 " + name + "(" + objectName + ") — 바운드 " + (any ? box.size.ToString("0.0") : "(없음)") +
                      ", 거리 " + dist.ToString("0.0") + "m, 방위 " + bestYaw.ToString("0") + "°/내려보기 " +
                      bestPitch.ToString("0") + "°(시설이 먼저 보이는 표본 " + (bestSeen * 100f).ToString("0") + "%)");
            return new Shot { Name = name, Eye = target - rot * Vector3.forward * dist, Target = target, PlayCamera = true, Subject = go.transform };
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
