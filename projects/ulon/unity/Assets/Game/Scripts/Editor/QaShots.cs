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
    public static partial class QaShots
    {
        const int W = 1280;
        const int H = 720;

        /// <summary>이번 실행에서 찍은 마을 사람 샷 이름 — 대조 시트가 이것만 모은다.</summary>
        static readonly System.Collections.Generic.List<string> villagerShots = new System.Collections.Generic.List<string>();

        internal struct Shot
        {
            public string Name;
            public Vector3 Eye;      // 카메라 위치(월드)
            public Vector3 Target;   // 바라보는 지점(월드)
            public bool PlayCamera;  // 플레이 카메라 재현(차폐 페이드 적용)
            public bool Vfx;         // 행동 효과 3종을 나란히 재생해 같이 찍는다
            public bool StandPlayer; // 플레이어를 그 자리에 실제로 세우고 찍는다(가려짐을 눈으로 보려면 몸이 있어야 한다)
            public Transform Subject; // 근접 샷의 피사체 — 자기 자신이 페이드에 물리지 않게 뺀다
            // **절단면 샷** — 지형과 뚜껑을 찍는 동안만 걷는다. 방은 지하 6m라, 걷지 않으면
            // 「방 절단면」이라는 이름의 샷이 **지표 잔디만** 담는다(2026-09-09 실측: 13이 그랬다).
            public bool CutAway;
        }

        [MenuItem("Ulon/QA Shots")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");

            // **반대쪽 한계는 환경변수 하나로 재현된다** — `ULON_FADE_NC=1 ./tools/qa_shots.sh`.
            // 「대상 뒤는 안 비친다」를 끄면 은행이 다시 통째로 비쳐야 한다. 켜진 판을 모르고 보면
            // 안 되므로 **로그 첫 줄에 상태를 찍는다**(끈 채로 찍힌 그림을 정상판으로 읽는 사고 방지).
            Ulon.Client.DungeonSightFade.IgnoreBehindRuleForNc =
                System.Environment.GetEnvironmentVariable("ULON_FADE_NC") == "1";
            if (Ulon.Client.DungeonSightFade.IgnoreBehindRuleForNc)
                Debug.Log("[Ulon] ⚠ 반대쪽 한계 판 — 「대상 뒤는 안 비친다」를 끄고 찍는다(정상판 아님)");

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../builds/qa"));
            Directory.CreateDirectory(dir);

            BearingNegativeControl("Forge");

            // **샷 목록은 한 곳에서 만든다**(2026-09-09). 「죽은 장」 자가 같은 목록을 읽어야
            // 재는 쪽과 찍는 쪽이 같은 화면을 본다 — 목록을 두 벌 두면 자가 다른 세계를 잰다.
            var shots = BuildShots();

            // **런타임 포즈로 찍는다.** 에디터에서 그냥 찍으면 모든 액터가 바인드 포즈(T포즈)라
            // 「칼이 얼굴 높이를 가로지른다」 같은 인상이 실제 플레이와 다르다(검수 2026-09-06 질의).
            // 애니메이터 기본 상태(Idle)를 실제로 샘플링해 포즈를 만든 뒤 찍는다.
            // **배치 렌더에서는 파티클이 돌지 않는다** — 화덕 불처럼 계속 나는 효과는 미리 시뮬레이션해야
            // 화면에 찍힌다(안 하면 「불을 붙였는데 샷엔 없다」가 된다).
            int simmed = 0;
            var loops = Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            // **씨앗 고정은 이 실행에만 산다**(검수 조건 2026-09-09). 게임이 도는 세계까지 박으면
            // 불·연기가 매번 같은 모양으로 피어 화면이 죽는다. 그래서 옛 값을 들고 있다가
            // **찍고 나서 그 자리에서 돌려놓는다**(아래 `finally`) — 씬 파일로도 새지 않는다.
            var seedUndo = new System.Collections.Generic.List<(ParticleSystem Ps, bool Auto, uint Seed)>();
            bool seedNc = System.Environment.GetEnvironmentVariable("ULON_SEED_NC") == "1";
            if (seedNc)
                Debug.Log("[Ulon] ⚠ 씨앗 반대쪽 한계 판 — 파티클 씨앗을 고정하지 않고 찍는다(정상판 아님)");
            for (int i = 0; i < loops.Length; i++)
            {
                // **수를 먼저 적는다**: 씬에서 살아 있는 파티클은 스크립트 참조 11곳이 아니라
                // **이 순간 1개**(계속 나는 효과)다 — 「11곳」을 보고 다시 세지 마라.
                // 유니티 기본값은 `useAutoRandomSeed = true` — **아무도 무작위를 켠 적이 없는데 무작위다.**
                // 판마다 새 씨앗이라 같은 코드·같은 씬으로 두 번 찍어도 VFX가 매번 달랐다.
                // 계속 나는 효과만 박았다가 **1회성 VFX(`24`·`25`, 최대 채널차 170)를 놓쳤다** —
                // **시뮬레이션 대상과 씨앗 대상은 다르다.**
                if (!seedNc)
                {
                    seedUndo.Add((loops[i], loops[i].useAutoRandomSeed, loops[i].randomSeed));
                    loops[i].useAutoRandomSeed = false;
                    loops[i].randomSeed = StableSeed(loops[i].transform);
                }
                var m = loops[i].main;
                if (!m.loop || !m.playOnAwake)
                    continue;
                // **배치 렌더에서는 파티클이 돌지 않는다** — 화덕 불처럼 계속 나는 효과는 미리 시뮬레이션해야
                // 화면에 찍힌다(안 하면 「불을 붙였는데 샷엔 없다」가 된다).
                // **위상은 자리마다 다르게** 준다(씨앗은 고정, 시작 시각만 자리별 상수) — 안 그러면
                // 마을의 모든 불이 같은 박자로 타올라 그림이 기계처럼 보인다.
                loops[i].Clear(true);
                loops[i].Simulate(1.2f + (StableSeed(loops[i].transform) % 97) * 0.01f, true, true);
                simmed++;
            }
            // **분모를 같이 찍는다** — 「N개 했다」만 적으면 빠진 것이 조용히 남는다(포즈 7체가 그렇게 샜다).
            Debug.Log("[Ulon] QA 파티클 — 씨앗 고정 " + seedUndo.Count + "/" + loops.Length + "개 · 그중 계속 나는 효과 " +
                      simmed + "개를 미리 시뮬레이션(나머지는 1회성이라 시뮬레이션 대상이 아니다)");

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
                AssertCutAwayShotsUncover(shots);
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
                    // 절단면 — 지형과 뚜껑을 잠깐 걷는다(찍고 바로 되돌린다).
                    var terrain = shot.CutAway ? Terrain.activeTerrain : null;
                    var capsHidden = new System.Collections.Generic.List<Renderer>();
                    if (terrain != null)
                    {
                        terrain.drawHeightmap = false;
                        foreach (var rd in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                        {
                            string rn = rd.transform.name;
                            if (!rn.StartsWith("DungeonCap", System.StringComparison.Ordinal) &&
                                !rn.StartsWith("CapDress", System.StringComparison.Ordinal))
                                continue;
                            rd.enabled = false;
                            capsHidden.Add(rd);
                        }
                    }
                    var vfx = shot.Vfx ? SliceSelfCheck.SpawnVfxTrio(shot.Target) : null;
                    // **여기서 난 효과에도 씨앗을 박는다**(랩 ㉬) — 위의 고정 루프는 씬을 연 순간의
                    // 파티클만 봤고, 이 셋은 **샷 직전에 태어나서** 그 그물을 빠져나갔다.
                    // 그 탓에 `24`·`25` 두 장만 최대 채널차 130~170으로 계속 흔들렸다.
                    if (vfx != null && !seedNc)   // NC는 **전부** 꺼야 NC다 — 반만 끄면 빨간불이 약하게 나온다
                        SeedParticles(vfx.transform);
                    // 「집 뒤에 서면 어떻게 보이나」는 **몸이 있어야** 보인다 — 좌표만 찍으면 빈 잔디다.
                    var player = shot.StandPlayer ? GameObject.Find("Player") : null;
                    Vector3 savedPlayer = player != null ? player.transform.position : Vector3.zero;
                    if (player != null)
                        player.transform.position = shot.Target - Vector3.up * 1.0f;
                    cam.Render();
                    if (player != null) player.transform.position = savedPlayer;
                    if (vfx != null) Object.DestroyImmediate(vfx);
                    if (terrain != null)
                    {
                        terrain.drawHeightmap = true;
                        for (int c = 0; c < capsHidden.Count; c++)
                            if (capsHidden[c] != null)
                                capsHidden[c].enabled = true;
                    }
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
                // **원복도 한 호흡에** — 예외로 빠져나가도 씨앗은 돌려놓는다.
                for (int i = 0; i < seedUndo.Count; i++)
                {
                    if (seedUndo[i].Ps == null)
                        continue;
                    seedUndo[i].Ps.randomSeed = seedUndo[i].Seed;
                    seedUndo[i].Ps.useAutoRandomSeed = seedUndo[i].Auto;
                }
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
            // 문구멍 어둠도 렌더가 필요하다 — 같은 자리(검수 판정 2026-09-09).
            SliceSelfCheck.AssertMouthDark();
            // 입구 근접이 대낮에 타지 않는가 — 포화는 렌더에서만 보인다(검수 큐 2).
            SliceSelfCheck.AssertEntranceNotBlownOut();
            SliceSelfCheck.AssertWaterNotBlownOut();
            // 근접에서 텍셀이 격자로 보이지 않는가 — 화면에서만 재진다(검수 지시 2026-09-10).
            SliceSelfCheck.AssertTexelNotBlocky();
            // 둑이 낟알이 아니라 덩어리로 읽히지 않는가 — 텍셀 자가 통과해도 남는 축이다.
            SliceSelfCheck.AssertBankNotBlocky();
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
        /// <summary>
        /// 이 가지 아래 파티클의 씨앗을 박고 처음부터 다시 돌린다(랩 ㉬).
        /// 씨앗은 `randomSeed` 지정 뒤 **다시 재생해야** 먹는다 — 그냥 값만 넣으면 이미 돌던 난수가 이어진다.
        /// </summary>
        static void SeedParticles(Transform root)
        {
            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].useAutoRandomSeed = false;
                systems[i].randomSeed = StableSeed(systems[i].transform);
                systems[i].Clear(true);
                systems[i].Simulate(0.35f, true, true);
            }
        }

        /// <summary>
        /// **어느 기계에서 찍어도 같은 씨앗** — 계층 경로를 FNV-1a로 접는다.
        /// `string.GetHashCode`를 쓰면 안 된다: 런타임에 따라 실행마다 값이 달라져
        /// 「씨앗을 고정했다」는 말만 남고 화면은 여전히 흔들린다.
        /// </summary>
        static uint StableSeed(Transform t)
        {
            uint h = 2166136261u;
            for (var cur = t; cur != null; cur = cur.parent)
            {
                string s = cur.name;
                for (int i = 0; i < s.Length; i++)
                {
                    h ^= s[i];
                    h *= 16777619u;
                }
                h ^= '/';
                h *= 16777619u;
            }
            return h == 0u ? 1u : h;   // 0은 유니티가 「자동」으로 읽을 수 있는 값이라 피한다
        }

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
            // **눈앞 NC** — 카메라가 설 자리에 물건을 놓으면 방위가 바뀌어야 한다(가림 광선으로는 안 잡히는 축).
            var lens = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lens.name = "QaEyeNcBlock";
            Object.DestroyImmediate(lens.GetComponent<Collider>());
            lens.transform.localScale = new Vector3(0.5f, 3f, 0.5f);
            lens.transform.position = before.Eye;
            // **자를 직접 양방향으로 문다.** 방위 전체를 다시 고르게 하는 NC는 못 쓴다 — 최종 눈 자리는
            // 방위를 고른 뒤 한 번 더 당겨져서, 그 점은 후보 루프가 평가한 점이 아니다(실측: 0.00m).
            // 그래서 **판정 함수**에 묻는다: 눈 자리에 기둥이 있으면 「막힘」, 치우면 「안 막힘」.
            ClearBlockerCache();
            bool crowded, clear;
            try
            {
                crowded = EyeCrowded(before.Eye, subject.transform);
            }
            finally
            {
                Object.DestroyImmediate(lens);
                ClearBlockerCache();
            }
            clear = EyeCrowded(before.Eye, subject.transform);
            if (!crowded)
                throw new System.InvalidOperationException("눈앞 네거티브 컨트롤 실패 — " + facility +
                    " 카메라 자리에 기둥을 세웠는데 「눈앞이 비었다」고 합니다.");
            if (clear)
                throw new System.InvalidOperationException("눈앞 네거티브 컨트롤 실패 — 기둥을 치웠는데도 막혔다고 합니다.");
            Debug.Log("[Ulon] 눈앞 양방향 NC 통과 — 카메라 자리에 기둥을 세우면 막힘 · 치우면 안 막힘");

            Debug.Log("[Ulon] 방위 NC 통과 — " + facility + " 앞에 콜라이더 없는 벽을 세우면 카메라가 " +
                      moved.ToString("0.0") + "m 비켜서고, 치우면 제자리로 돌아온다.");
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

        /// <summary>
        /// **입구는 진입로에 서서 본다**(검수 판정 2026-09-09). `Orbit`은 눈을 고정 대각 (−x,−z)에
        /// 두는데, 그러면 D1·D3는 **문 뒤에서** 찍힌다 — 플레이어가 절대 서지 않는 자리다.
        /// 거리·각(8m·20°)은 `Orbit` 그대로 두고 **방위만** 입구 원장에서 유도한다.
        /// 자(`EntranceCensus.ShotEye`)도 같은 규칙을 읽는다 — 눈이 갈리면 숫자는 화면이 아니다.
        /// </summary>
        static Shot EntranceOrbit(string name, float ex, float ez, float yaw)
        {
            const float Dist = 8f, Pitch = 20f;
            float y = GroundY(ex, ez);
            var t = new Vector3(ex, y + 1.2f, ez);
            var front = VisualSliceBuilder.EntranceFront(new Vector3(ex, 0f, ez), yaw);
            float rad = Pitch * Mathf.Deg2Rad;
            var eye = t + front * (Dist * Mathf.Cos(rad)) + Vector3.up * (Dist * Mathf.Sin(rad) + 1.5f);
            return new Shot { Name = name, Eye = eye, Target = t };
        }

        /// <summary>부두 끝에 서서 호수 건너 절개면을 본다 — 자리는 `WorldTerrain`의 부두 규칙에서 유도한다.</summary>
        static Shot PierAcross(string name)
        {
            var dir = new Vector2(-WorldTerrain.LakeX, -WorldTerrain.LakeZ).normalized;   // 호수 중심 → 마을
            float tipD = WorldTerrain.LakeRadius * 0.92f - WorldTerrain.PierLength;
            var tip = new Vector2(WorldTerrain.LakeX + dir.x * tipD, WorldTerrain.LakeZ + dir.y * tipD);
            var far = new Vector2(WorldTerrain.LakeX - dir.x * WorldTerrain.LakeRadius,
                                  WorldTerrain.LakeZ - dir.y * WorldTerrain.LakeRadius);
            return new Shot
            {
                Name = name,
                Eye = new Vector3(tip.x, WorldTerrain.PierTop + 1.6f, tip.y),
                Target = new Vector3(far.x, WorldTerrain.SeaLevel + 2.5f, far.y),
            };
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

        /// <summary>지형·뚜껑을 걷고 찍는다 — 「절단면」은 덮개를 치워야 절단면이다.</summary>
        static Shot CutAway(Shot shot) { shot.CutAway = true; return shot; }

        /// <summary>
        /// **절단면 샷은 덮개를 걷어야 한다** — 그러지 않으면 이름만 「절단면」이고 화면은 지표 잔디다
        /// (2026-09-09 실측: `13`이 before·after 모두 방 대신 지상 벽 윤곽만 담고 있었다).
        /// 왜 자가 필요한가: 이 조건은 **샷 목록에서 한 글자 지우면 조용히 사라진다** — 화면은
        /// 여전히 나오고, 다만 아무것도 안 보여 준다. 그래서 이름에 `cutaway`가 든 샷은 플래그를 강제한다.
        /// </summary>
        static void AssertCutAwayShotsUncover(Shot[] shots)
        {
            for (int i = 0; i < shots.Length; i++)
            {
                if (shots[i].Name == null || shots[i].Name.IndexOf("cutaway", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (!shots[i].CutAway)
                    throw new System.InvalidOperationException("샷 " + shots[i].Name +
                        "은 이름이 절단면인데 덮개를 걷지 않습니다 — 지형과 뚜껑이 방을 가려 지표만 찍힙니다.");
            }
        }

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

        /// <summary>
        /// 피사체의 **등 뒤** 한 지점 — 「이것이 피사체 너머에 오게」 세우면 카메라가 피사체 **앞**에 선다.
        /// 배경과 정면은 같은 축이라, 배경을 고르는 것은 곧 어느 쪽에서 볼지를 고르는 것이다.
        /// 못 찾으면 원점을 돌려준다(프레이밍이 옛 규칙으로 물러설 뿐 터지지 않는다).
        /// </summary>
        static Vector3? BehindSubject(string objectName, float far)
        {
            var go = GameObject.Find(objectName);
            if (go == null) return null;
            var back = -go.transform.forward;
            return go.transform.position + new Vector3(back.x, 0f, back.z).normalized * far;
        }

        /// <summary>샷의 이름 — 재는 쪽이 원장을 찾을 열쇠다.</summary>
        internal static string NameOf(Shot s) => s.Name;

        /// <summary>
        /// 샷의 눈과 보는 곳. **플레이 카메라 샷은 근사다** — 실제 촬영은 차폐 페이드·줌이 더 붙는다.
        /// 「주인공이 프레임에 담겼나」를 재는 데는 눈과 방향이면 되지만, 그 한계는 적어 둔다.
        /// </summary>
        internal static void EyeOf(Shot s, out Vector3 eye, out Vector3 look) { eye = s.Eye; look = s.Target; }

        /// <summary>QA 샷 목록 — 찍는 쪽과 재는 쪽(`ShotCensus`)이 **같은 목록**을 읽는다.</summary>
        internal static Shot[] BuildShots()
        {
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
                // **자리는 보스에게서 받는다**(죽은 장 훑기 2026-09-09). 고정 좌표 궤도라 보스가 옮겨진 뒤로
                // 이 장은 주인공이 **0.1%(51px)**인 채 오래 찍혀 왔다 — 화면 절반이 빈 흙바닥이었다.
                // 자리를 보스로 옮겨도 10m 궤도로는 0.8%다(몸이 얇아 거리를 좁혀야 담긴다). 그래서
                // **크기에서 거리를 유도하는** 근접 프레이밍을 쓴다 — 39·40 보스 샷과 같은 규칙이다.
                // **배경은 보스의 등 뒤**(검수 관찰 2026-09-09: 「필드 보스인데 배경이 마을 집·시설」).
                // 세어 보니 **자리는 문제가 아니었다** — 보스는 마을 중심에서 50.6m, 지역 밖이고 원장과
                // 어긋남 0.00m다(`OutdoorCensus.RunFieldBossPlace`). 방위에 따라 배경의 마을 몫이
                // **0.5%~8.5%**로 갈리는 것이 전부다. 그래서 이름도 자리도 아니라 **프레이밍**을 고친다.
                // 다만 「마을 반대쪽을 배경으로」는 틀린 처방이었다 — 카메라가 마을 쪽에 서면서
                // **보스가 등을 보였다**(보스는 +z를 보고 마을은 −110°다). 정면과 배경은 같은 축이므로
                // **보스의 등 뒤를 배경으로** 준다: 그러면 카메라가 보스 앞에 서고 마을은 프레임 가장자리로 밀린다.
                FacilityCloseUp("06_field_boss", FieldBoss.Object, BehindSubject(FieldBoss.Object, 30f), true),
                EntranceOrbit("07_d1_entrance", Dungeon1.EntranceX, Dungeon1.EntranceZ, Dungeon1.EntranceYaw),
                PlayCam("08_d1_interior_playcam", Dungeon1.InteriorX, Dungeon1.InteriorZ),
                // 귀퉁이에 선 화면 — 카메라 눈이 벽 밖으로 나가는 최악 자리(검수 2026-09-06 B).
                PlayCam("23_d1_corner_playcam", Dungeon1.InteriorX + 6f, Dungeon1.InteriorZ + 6f, Dungeon1.InteriorX, Dungeon1.InteriorZ),
                Inside("08_d1_interior", Dungeon1.InteriorX, Dungeon1.InteriorZ, Dungeon1.BossX, Dungeon1.BossZ),
                EntranceOrbit("09_d2_entrance", Dungeon2.EntranceX, Dungeon2.EntranceZ, Dungeon2.EntranceYaw),
                PlayCam("10_d2_interior_playcam", Dungeon2.InteriorX, Dungeon2.InteriorZ),
                Inside("10_d2_interior", Dungeon2.InteriorX, Dungeon2.InteriorZ, Dungeon2.BossX, Dungeon2.BossZ),
                EntranceOrbit("11_d3_entrance", Dungeon3.EntranceX, Dungeon3.EntranceZ, Dungeon3.EntranceYaw),
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
                CutAway(Roof("13_d1_room_cutaway", Dungeon1.InteriorX, Dungeon1.InteriorZ)),
                // §8.1 멀리서도 읽히는 실루엣 — 산·바다 조망, 호수·강 조망.
                BossCloseUp("17_boss_closeup", Dungeon3.BossX, Dungeon3.BossZ),
                ActorCloseUp("22_mob_closeup", Dungeon3.MobObject),
                Orbit("18_meadow", new Vector3(WorldRegions.Meadow.X, 0f, WorldRegions.Meadow.Z), 34f, 28f),
                Orbit("19_forest", new Vector3(WorldRegions.Forest.X, 0f, WorldRegions.Forest.Z), 38f, 26f),
                Orbit("20_mine", new Vector3(WorldRegions.Mine.X, 0f, WorldRegions.Mine.Z), 30f, 26f),
                Orbit("21_testchamber", new Vector3(WorldRegions.TestChamber.X, 0f, WorldRegions.TestChamber.Z), 24f, 22f),
                Free("14_world_vista", new Vector3(-165f, 95f, -165f), new Vector3(0f, WorldTerrain.LandBase, 0f)),
                // **부두에서 건너편 절개면을 마주 본다**(검수 조건 2026-09-09 — 안식각·너덜 랩).
                // 조망(15)에서는 그 면이 멀어 얼룩이 안 읽힌다. 자리는 지형 원장에서 유도한다:
                // 부두는 호수 중심에서 마을 쪽으로 반경 92% 자리에 서서 안쪽으로 14m 뻗는다.
                PierAcross("63_pier_cutface"),
                // **자리는 대상(호수·강)에서 유도한다** — 손으로 박은 눈·타깃은 이름이 약속한 둘을
                // 다 놓치고 바다 만과 부두만 담았다(검수 2026-09-10). `QaShots.Vista.cs` 참조.
                LakeRiverShot("15_lake_river"),
                RiverBendShot("64_river_bend"),
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
                // **미결**: 이 장은 지금 판정 불가다 — 대장간 몸통이 1.0×0.4×0.7m 무릎 높이 판매대라
                // 화면에 「대장간」으로 읽힐 것이 없다(방위·거리 랩에서 카메라 쪽은 다 맞췄다).
                // 근거와 조치는 `RoleLook.cs`의 Forge 항목에 적어 뒀다 — MegaKit(모루·화덕) 도착 시 최우선.
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
                BossShot41(),
                // 조련 생물 근접 — 덤불이 아니라 짐승으로 읽히는지 본다(동물 팩 랩 완료 기준).
                FacilityCloseUp("42_deer", TameCritter.Object, null, true),
                FacilityCloseUp("43_boar", TameBoar.Object, null, true),
                // 마구간 마당의 짐승 — 「빈 마당」 반려의 완료 근거(동물 랩).
                FacilityCloseUp("44_stable_beast", VisualSliceBuilder.StableBeastObject, null, true),
                // **볕 받는 면에서 본다.** 해는 `SunEuler`(50°, −30°)라 +x·−z 쪽 면이 볕이고 반대편은
                // 통째로 그늘이다 — 옛 자리(60,30,60)는 그 그늘 면을 정면으로 봐서 산이 검은 실루엣
                // 한 장이었다(before 샷). 세계를 밝히는 대신 **눈을 볕 쪽으로 옮긴다**: 조명을 만지면
                // 온 세계 스물한 샷이 같이 바뀐다.
                // 가까이 붙으면 **늘어난 무늬가 같이 커진다** — 첫 시도(120,34,−40)는 절벽이 화면을 채워
                // 스미어가 더 적나라했다. 능선이 이어지는 모양을 보여줄 만큼 물러선다.
                Free("16_mountain_ridge", new Vector3(158f, 72f, -86f), new Vector3(78f, WorldTerrain.LandBase + 24f, 26f)),
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
            return shotList.ToArray();
        }

    }
}
