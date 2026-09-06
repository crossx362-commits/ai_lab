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
                Inside("08_d1_interior", Dungeon1.InteriorX, Dungeon1.InteriorZ, Dungeon1.BossX, Dungeon1.BossZ),
                Orbit("09_d2_entrance", new Vector3(Dungeon2.EntranceX, 0f, Dungeon2.EntranceZ), 8f, 20f),
                PlayCam("10_d2_interior_playcam", Dungeon2.InteriorX, Dungeon2.InteriorZ),
                Inside("10_d2_interior", Dungeon2.InteriorX, Dungeon2.InteriorZ, Dungeon2.BossX, Dungeon2.BossZ),
                Orbit("11_d3_entrance", new Vector3(Dungeon3.EntranceX, 0f, Dungeon3.EntranceZ), 8f, 20f),
                PlayCam("12_d3_interior_playcam", Dungeon3.InteriorX, Dungeon3.InteriorZ),
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
                Free("16_mountain_ridge", new Vector3(60f, 30f, 60f), new Vector3(WorldTerrain.MountainPeak, WorldTerrain.LandBase + 18f, WorldTerrain.MountainPeak * 0.4f)),
            };

            // **런타임 포즈로 찍는다.** 에디터에서 그냥 찍으면 모든 액터가 바인드 포즈(T포즈)라
            // 「칼이 얼굴 높이를 가로지른다」 같은 인상이 실제 플레이와 다르다(검수 2026-09-06 질의).
            // 애니메이터 기본 상태(Idle)를 실제로 샘플링해 포즈를 만든 뒤 찍는다.
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
                        Ulon.Client.DungeonSightFade.Hide(shot.Eye, shot.Target, Ulon.Client.DungeonSightFade.DefaultRadius, faded);
                    cam.Render();
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
            Debug.Log("[Ulon] QA shots " + shots.Length + "장 — " + dir);
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
        static Shot PlayCam(string name, float cx, float cz)
        {
            var qv = Object.FindFirstObjectByType<Ulon.Client.QuarterViewCamera>(FindObjectsInactive.Include);
            float pitch = qv != null ? qv.Pitch : 35f;
            float yaw = qv != null ? qv.Yaw : 45f;
            // 실내는 런타임과 같은 실내 줌 거리로 찍는다(§4.2 줌 허용) — 밖에서 찍으면 지붕 윗면만 나온다.
            float dist = qv != null ? Mathf.Min(qv.Distance, qv.IndoorDistance) : 5.5f;
            // 방은 지하다 — 플레이어는 지면이 아니라 방 바닥에 선다.
            float y = GroundY(cx, cz) - VisualSliceBuilder.DungeonDepth;
            var player = new Vector3(cx, y + 1.0f, cz);
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
