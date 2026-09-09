using System;
using System.Collections.Generic;
using System.IO;
using Ulon.Client;
using Ulon.Server;
using Ulon.Shared;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// **VisualSliceBuilder 분할**(오너 상시 지시 2026-09-08, 파일 비대화 정리).
// 이 파일이 담는 것: 배치 보고·액터 애니메이터·Build() 전체 파이프라인·조명·지형·물 — 세계 조립 순서.
// 동작 변경 0 — 구간을 순서 그대로 옮기기만 했다(순서를 바꾸면 주석과 몸통의 짝이 깨진다).
namespace Ulon.Editor
{
    public static partial class VisualSliceBuilder
    {

        public static void BatchFixAndReport()
        {
            FixCharacterAnimation();
            var sb = new System.Text.StringBuilder();
            var importer = AssetImporter.GetAtPath(KnightFbx) as ModelImporter;
            sb.AppendLine("type=" + importer.animationType);
            var humans = importer.humanDescription.human;
            for (int i = 0; i < humans.Length; i++)
                sb.AppendLine(humans[i].humanName + "->" + humans[i].boneName);
            AnimationClip[] clips = LoadClips(KnightFbx);
            AnimationClip idle = BestClip(clips, new[] { "idle" }, new[] { "attack", "walk", "run", "combat" });
            if (idle != null)
            {
                var binds = AnimationUtility.GetCurveBindings(idle);
                sb.AppendLine("idle=" + idle.name + " humanMotion=" + idle.isHumanMotion + " binds=" + binds.Length);
                int n = Mathf.Min(12, binds.Length);
                for (int i = 0; i < n; i++)
                    sb.AppendLine("  " + binds[i].path + " / " + binds[i].propertyName);
            }
            var player = GameObject.Find("Player");
            var anim = player != null ? player.GetComponentInChildren<Animator>() : null;
            if (anim != null && anim.avatar != null)
                sb.AppendLine("playerAvatar=" + anim.avatar.name + " human=" + anim.avatar.isHuman + " valid=" + anim.avatar.isValid);
            var companion = GameObject.Find("Companion");
            var canim = companion != null ? companion.GetComponentInChildren<Animator>() : null;
            if (canim != null && canim.avatar != null)
                sb.AppendLine("companionAvatar=" + canim.avatar.name + " human=" + canim.avatar.isHuman);
            if (anim != null && canim != null)
                sb.AppendLine("sharedCtrl=" + (anim.runtimeAnimatorController == canim.runtimeAnimatorController));
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../builds/humanoid-report.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log("[Ulon] " + sb.ToString());
        }

        /// <summary>
        /// **애니메이터를 붙일 대상**을 씬 전수로 고른다(랩 ③, 2026-09-08).
        ///
        /// 여기 있던 것은 `GameObject.Find("Player"|"Companion"|…)` **14줄짜리 이름 명단**이었다.
        /// 명단은 모델이 하나 늘 때마다 새는 자다 — 새 액터는 아무 오류 없이 명단 밖에 남아
        /// 게임에서 T포즈로 선다(오류가 안 나므로 아무도 못 센다). **이름이 아니라 자리·성질로**:
        /// 「몸을 가진 것」(CharacterController)이 배우다. 사슴·멧돼지 같은 야생은 몸이 없어 저절로 빠진다.
        ///
        /// **부작용이 없다** — 게이트가 이 함수를 그대로 불러 NC를 걸 수 있게(세계를 안 바꾸고) 뺐다.
        /// 처음엔 게이트가 빌더 본체(`FixCharacterAnimation`)를 다시 불렀는데, 그것이 모든 보정 패스
        /// **뒤에** 액터·프리팹·FBX 임포트를 다시 손대 QA 샷 「마구간 사람이 가려진다」를 만들었다
        /// (판별 테스트: 게이트만 끄면 통과). 재는 자는 재는 동안 세계를 바꾸면 안 된다.
        /// </summary>
        public static GameObject[] ActorsToDress()
        {
            var actors = UnityEngine.Object.FindObjectsByType<CharacterController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var list = new List<GameObject>(actors.Length);
            for (int i = 0; i < actors.Length; i++)
                list.Add(actors[i].gameObject);
            return list.ToArray();
        }

        /// <summary>
        /// **모든 사람형이 실제로 움직이게 한다**(검수 2026-09-07 (a) — 53의 T포즈).
        ///
        /// 스탠드얼론 실측에서 훈련사와 스켈레톤이 **애니메이터 없이** 서 있었다. 런타임
        /// `CharacterAnim.StripEmptyAnimators`가 컨트롤러 없는 애니메이터를 지우는데, 그 액터에는
        /// 컨트롤러 달린 애니메이터가 **하나도 없어** 결국 아무것도 안 남았다 — 게임 화면에서 T포즈다.
        /// 만드는 경로가 여럿이라(생성·모델 교체·프리팹 인스턴스) 한 곳을 고쳐선 또 샌다.
        /// 그래서 **전수 보정 패스**로 두고, 게이트가 이 성질을 지킨다(`AssertActorsAnimated`).
        /// 멱등: 이미 컨트롤러가 있으면 손대지 않는다.
        /// </summary>
        public static void EnsureActorAnimators()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogWarning("[Ulon] 공용 로코모션 컨트롤러가 없습니다: " + ControllerPath);
                return;
            }
            var actors = ActorsToDress();      // 대상 선정은 한 곳에서만 — 자가 둘이면 또 갈린다
            var fixedNames = new List<string>();
            for (int i = 0; i < actors.Length; i++)
            {
                var go = actors[i];
                var anim = go.GetComponentInChildren<Animator>(true);
                if (anim != null && anim.runtimeAnimatorController != null)
                    continue;
                // **계층을 건드리지 않는다.** 처음엔 `StripAndAssign`을 재사용했는데 그것이 Visual을
                // 다시 찾아 이름을 바꾸고 아바타를 덮어써서 자객의 발이 지표 아래 10m로 튀었다
                // (게이트가 잡았다). 여기서 고칠 것은 **컨트롤러가 없다** 하나뿐이다.
                if (anim == null)
                {
                    var visual = go.transform.Find("Visual");
                    anim = (visual != null ? visual.gameObject : go).AddComponent<Animator>();
                }
                if (anim.avatar == null)
                {
                    var own = AvatarFromVisual(go);
                    anim.avatar = own != null ? own : AvatarFor(go.name);
                }
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                fixedNames.Add(go.name);
            }
            if (fixedNames.Count > 0)
                Debug.Log("[Ulon] 액터 애니메이터 보정 — " + fixedNames.Count + "체에 공용 컨트롤러를 달았다(" +
                          string.Join(", ", fixedNames) + "). 컨트롤러 없는 액터는 게임에서 T포즈로 선다");
        }

        static void StripAndAssign(GameObject root, RuntimeAnimatorController controller)
        {
            if (root == null)
                return;
            var rootAnim = root.GetComponent<Animator>();
            if (rootAnim != null)
                UnityEngine.Object.DestroyImmediate(rootAnim);
            Transform visual = root.transform.Find("Visual");
            if (visual == null)
                visual = FindMeshChildToNameVisual(root.transform);
            GameObject host = visual != null ? visual.gameObject : root;
            var anim = host.GetComponentInChildren<Animator>(true);
            if (anim == null)
                anim = host.AddComponent<Animator>();
            // **아바타도 이름표가 아니라 그 배우가 입고 있는 모델에서 고른다**(랩 ③).
            // 이름 표(`AvatarFor`)는 모르는 이름을 전부 Knight로 돌려주므로, 명단 밖에서 들어온
            // 새 액터가 해골 몸에 기사 뼈대를 쓰게 된다 — 스윕으로 고르기 시작한 이상 위험한 폴백이다.
            var own = AvatarFromVisual(host);
            anim.avatar = own != null ? own : AvatarFor(root.name);
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var smrs = host.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < smrs.Length; i++)
                smrs[i].updateWhenOffscreen = true;
        }

        /// <summary>그 배우가 실제로 입은 살가죽(FBX)에서 사람 아바타를 찾는다 — 못 찾으면 null.</summary>
        static Avatar AvatarFromVisual(GameObject host)
        {
            if (host == null)
                return null;
            var skins = host.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skins.Length; i++)
            {
                if (skins[i] == null || skins[i].sharedMesh == null)
                    continue;
                string path = AssetDatabase.GetAssetPath(skins[i].sharedMesh);
                if (string.IsNullOrEmpty(path))
                    continue;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    var av = o as Avatar;
                    if (av != null && av.isHuman)
                        return av;
                }
            }
            return null;
        }

        static Avatar AvatarFor(string rootName)
        {
            string fbx = KnightFbx;
            if (rootName == "Companion")
                fbx = KnightFbx;
            else if (rootName == "Skeleton" || rootName == Dungeon1.MobObject || rootName == Dungeon1.BossObject)
                fbx = SkeletonFbx;
            else if (rootName == "Trainer" || rootName == FieldBoss.Object)
                fbx = MageFbx;
            else if (rootName == "Bandit" || rootName == Dungeon2.MobObject)
                fbx = RogueFbx;      // 「도적」이 마법사 차림이던 이름-외형 어긋남(검수 승인 2026-09-07)
            else if (rootName == "Raider" || rootName == Dungeon3.MobObject)
                fbx = KnightFbx;
            else if (rootName == "Rogue" || rootName == Dungeon2.BossObject)
                fbx = RogueFbx;
            else if (rootName == "Knight")
                fbx = KnightFbx;
            else if (rootName == "Acolyte")
                fbx = SkeletonMageFbx;
            else if (rootName == "Minion")
                fbx = SkeletonMinionFbx;
            else if (rootName == "SkelRogue")
                fbx = SkeletonRogueFbx;
            Avatar human = null;
            Avatar any = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx))
            {
                var av = o as Avatar;
                if (av == null)
                    continue;
                any = av;
                if (av.isHuman)
                    human = av;
            }
            return human != null ? human : any;
        }

        [MenuItem("Ulon/Build Visual Slice")]
        public static void Build()
        {
            if (!File.Exists(Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, KnightFbx)))
            {
                Debug.LogWarning("[Ulon] KayKit Knight.fbx 없음. 캡슐 부트스트랩으로 폴백.");
                CreateBootstrapScene.Create();
                return;
            }

            ConfigureHumanoid(KnightFbx, true);
            ConfigureHumanoid(MageFbx, true);
            ConfigureHumanoid(RogueFbx, true);
            ConfigureHumanoid(SkeletonFbx, true);
            ConfigureHumanoid(SkeletonMageFbx, true);
            ConfigureHumanoid(SkeletonRogueFbx, true);
            ConfigureProp(SwordFbx);
            ConfigureProp(ShieldFbx);
            foreach (string prop in KenneyProps())
                ConfigureProp(prop);

            AnimationClip[] clips = LoadClips(KnightFbx);
            AnimationClip idle = BestClip(clips, new[] { "idle" }, new[] { "attack", "walk", "run", "combat" });
            AnimationClip walk = BestClip(clips, new[] { "walking", "walk" }, new[] { "attack", "strafe" });
            AnimationClip run = BestClip(clips, new[] { "running", "run" }, new[] { "attack" });
            AnimationClip attack = BestClip(clips, new[] { "1h_melee_attack", "attack_chop", "melee_attack", "attack" }, new[] { "idle" });
            if (idle == null)
                throw new InvalidOperationException("Knight FBX에서 Idle 클립을 찾지 못했습니다. 클립: " + ClipNames(clips));

            AnimatorController controller = BuildController(idle, walk, run, attack);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            SetupLighting();
            MakeGround();
            PlaceKenney();

            var player = SpawnActor("Player", KnightFbx, new Vector3(0f, 0f, 0f), PlayerHeight, controller, true, false, "나", 50f);
            AttachGear(player, SwordFbx, ShieldFbx);
            HideExtraGear(player);
            var companion = SpawnActor("Companion", KnightFbx, new Vector3(-2.2f, 0f, 1.4f), 1.85f, controller, false, false, "동료", 50f);
            HideExtraGear(companion);
            var skeleton = SpawnActor("Skeleton", SkeletonFbx, new Vector3(5.2f, 0f, 3.6f), MobCatalog.HeightOf(MobCatalog.Skeleton), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Skeleton), MobCatalog.MaxHpOf(MobCatalog.Skeleton));
            BindMob(skeleton, MobCatalog.Skeleton);
            HideExtraGear(skeleton);
            var bandit = SpawnActor("Bandit", MageFbx, new Vector3(7.4f, 0f, 3.6f), MobCatalog.HeightOf(MobCatalog.Bandit), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Bandit), MobCatalog.MaxHpOf(MobCatalog.Bandit));
            BindMob(bandit, MobCatalog.Bandit);
            HideExtraGear(bandit);
            var raider = SpawnActor("Raider", KnightFbx, new Vector3(2.4f, 0f, 13.2f), MobCatalog.HeightOf(MobCatalog.Raider), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Raider), MobCatalog.MaxHpOf(MobCatalog.Raider));
            BindMob(raider, MobCatalog.Raider);
            HideExtraGear(raider);
            var rogue = SpawnActor("Rogue", RogueFbx, new Vector3(-3.8f, 0f, 13.2f), MobCatalog.HeightOf(MobCatalog.Rogue), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Rogue), MobCatalog.MaxHpOf(MobCatalog.Rogue));
            BindMob(rogue, MobCatalog.Rogue);
            HideExtraGear(rogue);
            var knight = SpawnActor("Knight", KnightFbx, new Vector3(4.4f, 0f, 13.2f), MobCatalog.HeightOf(MobCatalog.Knight), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Knight), MobCatalog.MaxHpOf(MobCatalog.Knight));
            BindMob(knight, MobCatalog.Knight);
            HideExtraGear(knight);
            var acolyte = SpawnActor("Acolyte", SkeletonMageFbx, new Vector3(6.4f, 0f, 13.2f), MobCatalog.HeightOf(MobCatalog.Acolyte), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Acolyte), MobCatalog.MaxHpOf(MobCatalog.Acolyte));
            BindMob(acolyte, MobCatalog.Acolyte);
            HideExtraGear(acolyte);
            var minion = SpawnActor("Minion", SkeletonMinionFbx, new Vector3(8.4f, 0f, 13.2f), MobCatalog.HeightOf(MobCatalog.Minion), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Minion), MobCatalog.MaxHpOf(MobCatalog.Minion));
            BindMob(minion, MobCatalog.Minion);
            HideExtraGear(minion);
            var skelRogue = SpawnActor("SkelRogue", SkeletonRogueFbx, new Vector3(10.4f, 0f, 13.2f), MobCatalog.HeightOf(MobCatalog.SkelRogue), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.SkelRogue), MobCatalog.MaxHpOf(MobCatalog.SkelRogue));
            BindMob(skelRogue, MobCatalog.SkelRogue);
            HideExtraGear(skelRogue);
            var hexarch = SpawnActor(FieldBoss.Object, MageFbx, new Vector3(FieldBoss.X, 0f, FieldBoss.Z), MobCatalog.HeightOf(MobCatalog.Hexarch), controller, false, true, MobCatalog.DisplayNameOf(MobCatalog.Hexarch), MobCatalog.MaxHpOf(MobCatalog.Hexarch));
            BindMob(hexarch, MobCatalog.Hexarch);
            HideExtraGear(hexarch);
            DressBoss(hexarch, new Color(0.35f, 1f, 0.6f));       // 독기 어린 녹빛 — 헥사크(§10.2)

            var world = new GameObject("OfflineWorld");
            world.AddComponent<OfflineWorld>();
            world.AddComponent<SliceHud>();
            world.AddComponent<PersistDriver>();

            Camera cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                var qv = cam.GetComponent<QuarterViewCamera>() ?? cam.gameObject.AddComponent<QuarterViewCamera>();
                // 실내 차폐 페이드도 씬에 박아 둔다 — 런타임에만 붙이면 에디터 검증이 못 본다(검수 2026-09-06 P0).
                if (cam.GetComponent<DungeonSightFade>() == null)
                    cam.gameObject.AddComponent<DungeonSightFade>();
                qv.SetFollow(player.transform);
            }

            SetupSky();
            DressVillageInOpenScene();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[Ulon] Visual slice 씬 저장. clips idle=" + idle.name
                      + " walk=" + (walk != null ? walk.name : "-")
                      + " run=" + (run != null ? run.name : "-")
                      + " attack=" + (attack != null ? attack.name : "-"));
        }

        // 해 원장 — 밝기·색·각도는 여기 한 줄씩만 있다. 조도 계산(감마: 1.18×sin50° + 앰비언트)이
        // 이 값을 유도 근거로 쓰므로 두 곳에 적히면 화면값 계산이 거짓이 된다.
        public static readonly Color SunColor = new Color(1f, 0.95f, 0.85f);
        public const float SunIntensity = 1.18f;
        public static readonly Vector3 SunEuler = new Vector3(50f, -30f, 0f);

        /// <summary>해 하나를 원장대로 맞추고, 해가 된 등불은 점광으로 되돌린다.</summary>
        public static Light EnsureSun()
        {
            Light sun = FindSun();
            if (sun == null)
                return null;
            sun.type = LightType.Directional;
            sun.color = SunColor;
            sun.intensity = SunIntensity;
            sun.transform.rotation = Quaternion.Euler(SunEuler);
            sun.shadows = LightShadows.Soft;
            DemoteExtraSuns(sun);
            return sun;
        }

        static void SetupLighting()
        {
            EnsureWorldAtmosphere();      // 앰비언트는 대기 원장이 정한다 — 여기서 따로 적지 않는다
            if (EnsureSun() == null)
                return;
        }

        static void DemoteExtraSuns(Light sun)
        {

            // 이미 해가 돼 버린 등불을 되돌린다 — 옛 버그가 구워 놓은 씬이 커밋돼 있어서,
            // 고른 자를 고치는 것만으로는 세계가 안 낫는다(방향광은 자리와 무관하게 세계 전체를 비춘다).
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == sun || lights[i].type != LightType.Directional)
                    continue;
                lights[i].type = LightType.Point;
                Debug.Log("[Ulon] 해가 둘이었다 — " + lights[i].name + "을(를) 점광으로 되돌렸다(방향광은 " + SunObject + " 하나뿐이다).");
            }
        }

        /// <summary>
        /// **해는 골라 오는 게 아니라 지목한다.** 옛 코드는 `FindAnyObjectByType&lt;Light&gt;()`로
        /// 씬의 **아무 등불이나** 집어 해 설정을 덮어썼다 — 굽는 순번에 따라 화덕 점광(CampfireLight)과
        /// 테스트 공간 점광(TestChamberLight)이 각각 **또 하나의 태양**(Directional 1.18)이 돼 있었다.
        /// 실측 2026-09-09: 마을 광장이 해 셋(합 3.54)을 받아 화면의 24~39%가 순백(255)으로 포화 →
        /// 그 위의 VFX는 픽셀 차 0이라 「효과가 실화면에서 안 읽힘」으로 게이트가 울었다.
        /// 「고르는 자는 틀려도 빨간불이 안 난다」의 재발 — 이제 이름으로 지목하고, 없으면 만든다.
        /// </summary>
        public const string SunObject = "Directional Light";

        static Light FindSun()
        {
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].gameObject.name == SunObject)
                    return lights[i];
            var go = new GameObject(SunObject);
            go.transform.position = new Vector3(0f, 3f, 0f);
            return go.AddComponent<Light>();
        }

        static void MakeGround()
        {
            EnsureVillageTerrain();
        }

        /// <summary>지형은 셀프체크에서도 다시 만든다 — 안 그러면 원장(WorldTerrain)을 고쳐도
        /// 씬에는 디스크의 옛 지형이 남아 Assert가 옛 값을 본다(2026-09-06 실측: 180m가 계속 잡혔다).</summary>
        /// <summary>
        /// 안개 범위는 월드 크기를 따라간다 — 42~115m로는 300m 월드에서 산·바다가 통째로 안개에 묻혀
        /// §8.1 「멀리서도 즉시 읽히는 실루엣」이 성립하지 않는다(2026-09-06 조망 샷 실측).
        /// </summary>
        // ── 대기 원장 ─────────────────────────────────────────────────────────────
        // 하늘·앰비언트·안개는 **한 곳에서만** 정해진다. 셋이 갈라져 있던 자리다(2026-09-09):
        // 앰비언트가 `SetupLighting`(0.55,0.58,0.52) · `SetupSky`(0.58,0.62,0.55) ·
        // `CreateBootstrapScene`(0.55,0.58,0.52) 세 곳에 각각 적혀 마지막에 부른 쪽이 이겼다.
        // 「해가 셋」과 같은 결의 결함이다 — **값이 여럿이면 화면은 그중 하나만 보여 주고 나머지는 거짓말이다.**
        public static readonly Color AmbientLight = new Color(0.55f, 0.58f, 0.52f);
        public static readonly Color FogColor = new Color(0.55f, 0.70f, 0.86f);
        public const float FogStart = 90f;
        public static readonly Color SkyTint = new Color(0.4f, 0.58f, 0.95f);
        public static readonly Color SkyGroundColor = new Color(0.58f, 0.72f, 0.88f);
        public const float SkyExposure = 1.15f;
        public const float SkySunSize = 0.04f;
        /// <summary>
        /// 대기 두께 — **하늘 색을 정하는 것은 이 값 하나다**(검수 관찰 2026-09-09 `14_world_vista`:
        /// 「수평선 위 하늘의 넓은 띠가 형광 연두」). 판별 테스트로 원인을 갈랐다: 노출·해 색·안개를
        /// 각각 바꿔도 띠 색은 rgb(178,227,101) 그대로였고, **두께만** 움직였다
        /// (0.30→(51,75,171) · 0.40→(70,104,215) · 0.50→(90,133,235) · 0.60→(111,161,230) ·
        ///  0.75→(141,196,185) · 0.95→(178,227,101)). 감마 공간에서 두꺼운 대기는 산란이 녹색으로 넘어간다.
        /// 0.60을 고른 근거: 띠 색 (111,161,230)이 안개 색 (140,178,219)과 같은 하늘 계열이고,
        /// 0.75부터 g가 b를 넘어 다시 초록으로 기운다.
        /// </summary>
        public const float SkyAtmosphere = 0.60f;

        /// <summary>하늘·앰비언트·안개를 원장 값으로 맞춘다 — 이 함수 밖에서 대기를 건드리지 마라.</summary>
        public static void EnsureWorldAtmosphere()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = AmbientLight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogStartDistance = FogStart;
            RenderSettings.fogEndDistance = WorldTerrain.Span * 1.6f;
            ApplySkyLedger(RenderSettings.skybox);
        }

        /// <summary>스카이박스 재질에 원장 값을 새긴다(재질이 없으면 아무것도 안 한다).</summary>
        public static void ApplySkyLedger(Material sky)
        {
            if (sky == null || sky.shader == null || sky.shader.name != "Skybox/Procedural")
                return;
            sky.SetFloat("_SunSize", SkySunSize);
            sky.SetFloat("_AtmosphereThickness", SkyAtmosphere);
            sky.SetColor("_SkyTint", SkyTint);
            sky.SetColor("_GroundColor", SkyGroundColor);
            sky.SetFloat("_Exposure", SkyExposure);
            EditorUtility.SetDirty(sky);
        }

        public static void EnsureVillageTerrain()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Game/Art/Env"));
            var grass = KenneyGrassMat();
            var tex = grass != null ? grass.mainTexture as Texture2D : null;
            if (tex == null)
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Art/Env/KenneyGrass.png");
            const string DataPath = "Assets/Game/Art/Env/VillageTerrain.asset";
            const string LayerPath = "Assets/Game/Art/Env/VillageGrass.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, LayerPath);
            }
            layer.diffuseTexture = tex;
            layer.tileSize = new Vector2(12f, 12f);
            EditorUtility.SetDirty(layer);
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(DataPath);
            if (data == null)
            {
                data = new TerrainData();
                AssetDatabase.CreateAsset(data, DataPath);
            }
            // 무채색 한 장으로 보이던 바위에 갈색기·명암 폭을 준다(§8.2).
            // 풀(잡음·타일 12)과 **다른 무늬·다른 타일링**이어야 산이 별개의 지질로 읽힌다(검수 재반려).
            // 타일 7m는 **세로로 늘어난 줄무늬를 그만큼 길게** 만든다(82° 절벽에서 실측). 무늬를 잘게
            // 하면 같은 늘어남도 눈에 「긴 스미어」가 아니라 「거친 암면」으로 읽힌다.
            // **톤은 벌리고 주기는 그대로 둔다**(검수 랩 ⑪ — 주기 쪽은 화면이 반대로 답했다).
            // 옛 두 겹은 0.41 대 0.32(1.28배)라 화면에서 구분이 안 됐다(자는 「한 겹 2%」로 초록이었다 —
            // **자는 가중치를 보고 눈은 색을 본다**). 밝고 따뜻한 암면 ↔ 어둡고 찬 절벽으로 벌린다.
            //
            // **주기를 키우는 안은 돌려 보고 걷었다**(가설: 「60m 벽에 2m 주기면 잔모래로 읽힌다」).
            // 3.0→9m, 1.8→5.5m로 키웠더니 ①무늬 1의 `band` 항(`&255` 되감김)이 폭 30cm짜리 **검은 사선
            // 막대**로 자랐고 ②대신 넣은 층리 무늬(무늬 5)는 벽 전체가 **거대한 체커보드**가 됐다.
            // 작게 깔 때 안 보이던 것이 크게 깔면 무늬가 된다. **화면 증거는 「주기를 키우지 마라」다.**
            var rockLayer = EnsureTerrainLayer("MountainRock", new Color(0.30f, 0.27f, 0.23f), new Color(0.72f, 0.66f, 0.55f), 3.0f, 1);
            // 그늘진 절벽 — 같은 층리 무늬(pattern 1)에 **더 잘게·더 어둡게**. 두 겹이 서로 다른
            // 주기로 반복해야 늘어난 줄이 한 줄로 이어지지 않는다.
            var cliffLayer = EnsureTerrainLayer("CliffDark", new Color(0.14f, 0.14f, 0.17f), new Color(0.34f, 0.33f, 0.37f), 1.8f, 1);
            var sandLayer = EnsureTerrainLayer("ShoreSand", new Color(0.74f, 0.68f, 0.50f), new Color(0.85f, 0.80f, 0.62f), 8f);
            // §6.1 지역이 **지표로** 구분돼야 한다 — 바닥이 전부 같은 초록이면 소품만 얹힌 모양이다(검수 2026-09-06 관찰).
            var tilledLayer = EnsureTerrainLayer("FarmTilled", new Color(0.30f, 0.21f, 0.13f), new Color(0.47f, 0.34f, 0.21f), 3.5f, 2);
            // 숲 바닥은 **제 무늬**를 쓴다(pattern 3 부엽토) — 잔풀 잡음이면 마을 광장 흙과 같아 보인다.
            var soilLayer = EnsureTerrainLayer("ForestSoil", new Color(0.13f, 0.11f, 0.07f), new Color(0.34f, 0.28f, 0.16f), 6f, 3);
            var gravelLayer = EnsureTerrainLayer("MineGravel", new Color(0.28f, 0.26f, 0.24f), new Color(0.55f, 0.52f, 0.47f), 4.5f, 1);
            // 광장은 **돌포장**(pattern 4) — 길 흙(0)과 같은 무늬면 십자로가 아스팔트로 읽힌다(검수 랩 ②).
            var cobbleLayer = EnsureTerrainLayer("PlazaCobble", new Color(0.30f, 0.28f, 0.26f), new Color(0.60f, 0.58f, 0.53f), 2f, 4);
            var roadLayer = EnsureTerrainLayer("DirtRoad", new Color(0.38f, 0.31f, 0.22f), new Color(0.58f, 0.50f, 0.37f), 5f);
            // **마른 풀**(검수 랩 ⑤) — 잔디와 **같은 잔풀 무늬**(pattern 0)에 색만 누렇게 뺀다.
            // 무늬까지 다르면 다른 지질로 읽혀 「초원 안의 얼룩」이 아니라 「밭이 번진 것」이 된다.
            // 타일링도 12가 아닌 9로 어긋내 두 겹이 같은 자리에서 같은 격자로 반복되지 않게 한다.
            var dryLayer = EnsureTerrainLayer("MeadowDry", new Color(0.46f, 0.45f, 0.24f), new Color(0.70f, 0.66f, 0.38f), 9f);

            int res = 513;
            data.heightmapResolution = res;
            data.size = new Vector3(WorldTerrain.Span, WorldTerrain.MaxHeight, WorldTerrain.Span);
            data.terrainLayers = new[] { layer, rockLayer, sandLayer, tilledLayer, soilLayer, gravelLayer, roadLayer, cobbleLayer, dryLayer, cliffLayer };
            float[,] heights = new float[res, res];
            float half = WorldTerrain.Span * 0.5f;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float wx = (x / (float)(res - 1)) * WorldTerrain.Span - half;
                    float wz = (z / (float)(res - 1)) * WorldTerrain.Span - half;
                    heights[z, x] = WorldTerrain.HeightAt(wx, wz) / WorldTerrain.MaxHeight;
                }
            }
            data.SetHeights(0, 0, heights);
            int ar = data.alphamapResolution;
            var alpha = new float[ar, ar, WorldSplat.LayerCount];
            for (int z = 0; z < ar; z++)
            {
                for (int x = 0; x < ar; x++)
                {
                    float wx = (x / (float)(ar - 1)) * WorldTerrain.Span - half;
                    float wz = (z / (float)(ar - 1)) * WorldTerrain.Span - half;
                    float h = WorldTerrain.HeightAt(wx, wz);
                    // 경사도 — 가파른 곳이 바위다. 높이만 보면 산이 회색 한 장, 밑동이 칼로 자른 듯 끊긴다(§8.2).
                    float d = WorldTerrain.Span / (ar - 1);
                    float hx = WorldTerrain.HeightAt(wx + d, wz) - WorldTerrain.HeightAt(wx - d, wz);
                    float hz = WorldTerrain.HeightAt(wx, wz + d) - WorldTerrain.HeightAt(wx, wz - d);
                    float slope = Mathf.Sqrt(hx * hx + hz * hz) / (2f * d);

                    // 경계에 노이즈를 섞어 직선으로 끊기지 않게 한다.
                    float edgeNoise = (Mathf.PerlinNoise(wx * 0.09f + 17f, wz * 0.09f + 5f) - 0.5f) * 6f;
                    float rock = Mathf.Clamp01((h - (WorldTerrain.LandBase + 10f) + edgeNoise * 1.6f) / 16f);
                    rock = Mathf.Max(rock, Mathf.Clamp01((slope - 0.58f) * 1.6f));      // 급경사는 고도와 무관하게 바위
                    float mottle = Mathf.PerlinNoise(wx * 0.021f + 3.1f, wz * 0.021f + 8.9f);
                    rock = Mathf.Max(rock, Mathf.Clamp01((mottle - 0.5f) * 2.6f) * 0.55f);   // 평지 흙·바위 얼룩
                    // 산 중턱까지 풀이 올라간다 — 상한을 얼룩으로 흔들어 풀·바위가 섞이게 한다(중턱 풀 0.19 재반려).
                    // **다만 이 상한은 경사를 봐야 한다**(검수 랩 ⑨): 안 보면 80° 암벽에도 풀을 남겨
                    // 초록 커튼이 흘러내린다. 급경사에서만 상한을 풀어 준다(원장: `WorldSplat.WallRockAt`).
                    // **여기 `slope`(알파맵 한 칸 = 0.587m)를 넘기지 마라** — 이 지형은 잔주름이 심해
                    // 그 자가 큰 형태보다 평균 15° 높게 읽고, 그래서 둥근 흙산까지 맨바위가 됐다(검수 반려).
                    // 원장이 좌표를 받아 **8m 자로 다시 잰다**.
                    if (h < WorldTerrain.LandBase + 22f)
                        rock = Mathf.Min(rock, Mathf.Lerp(0.42f + mottle * 0.45f, 1f, WorldSplat.WallRockAt(wx, wz)));

                    // **둥근 능선에 풀 한 포기 없는 건 상식 모순이다**(검수 랩 ⑪ ④). 여기 바위는 대부분
                    // **높이**로 깔린다(LandBase+26m 위는 경사와 무관하게 rock=1) — 그래서 완만한 산꼭대기가
                    // 맨바위가 된다. 벽은 그대로 두고 **완만한 데만** 마른 풀·이끼를 얇게 남긴다:
                    // 남기는 몫은 벽 규칙의 반대값이라 60°↑ 벽에서는 0이 되고 「암벽 풀」 자와 안 부딪힌다.
                    float gentle = 1f - WorldSplat.WallRockAt(wx, wz);
                    rock = Mathf.Min(rock, 1f - 0.22f * gentle);

                    // 물가 — 수면 언저리는 모래. 잔디가 물에 수직으로 잘리면 §8.2 위반이다.
                    // **폭이 어디나 같으면 「해안선」이 아니라 「띠를 두른 것」이다**(검수 랩 ⑥ —
                    // 세어 보니 방위 16곳 모래띠가 1.5~4.5m로 사실상 균일했다). 해안을 따라 도는
                    // 저주파 노이즈로 **너른 모래사장과 바위가 물까지 내려온 구간**을 갈라 만든다.
                    float beach = Mathf.PerlinNoise(wx * 0.012f + 29.3f, wz * 0.012f + 64.1f);
                    float flatBand = Mathf.Lerp(0.15f, 2.0f, beach);     // 물가에서 이만큼은 온전히 모래
                    float sandFade = Mathf.Lerp(0.7f, 4.2f, beach);      // 그 바깥으로 이만큼 옅어진다
                    float sand = 1f - Mathf.Clamp01((Mathf.Abs(h - WorldTerrain.SeaLevel) - flatBand) / sandFade);
                    if (h < WorldTerrain.SeaLevel)
                        sand = 1f;                                   // 물속 바닥도 모래
                    sand = Mathf.Max(sand, 0f);

                    float grassW = Mathf.Max(0f, 1f - sand) * Mathf.Max(0f, 1f - rock);
                    float rockW = Mathf.Max(0f, 1f - sand) * rock;

                    // 지역 지표·길 — 원장(WorldSplat)이 계산하고 Assert도 같은 함수를 읽는다.
                    int cover = WorldSplat.CoverAt(wx, wz, out float coverW);
                    coverW *= Mathf.Max(0f, 1f - sand) * Mathf.Max(0f, 1f - rock * 0.45f);
                    float keep = Mathf.Max(0f, 1f - coverW);

                    var w = new float[WorldSplat.LayerCount];
                    // **풀은 한 겹이 아니라 두 겹이다**(검수 랩 ⑤). 총량 `grassW * keep`은 그대로 두고
                    // 짙은 풀·마른 풀로 **나눠서만** 칠한다 — 흙 비율을 건드리지 않고 초록을 가른다.
                    float dryShare = WorldSplat.DryGrassAt(wx, wz);
                    // 높은 데 남는 풀은 잔디밭이 아니라 **마른 풀·이끼**다 — 능선에 초록 융단이 깔리면
                    // 바로 그것이 다시 상식 모순이다.
                    if (h > WorldTerrain.LandBase + 18f)
                        dryShare = Mathf.Max(dryShare, 0.72f);
                    w[WorldSplat.Grass] = grassW * keep * (1f - dryShare);
                    w[WorldSplat.DryGrass] = grassW * keep * dryShare;
                    // **바위도 두 겹이다**(검수 랩 ⑥). 총량은 그대로, 밝은 암면과 그늘진 절벽으로 나눈다.
                    // **포화를 막던 0.14~0.86 조임도 걷는다**(랩 ⑪): 그 이유였던 「겹쳐야 세로줄이 끊긴다」는
                    // 랩 ⑧에서 투영을 고쳐 사라졌고, 남은 것은 **자리마다 중간값 = 화면에서 한 톤**이라는
                    // 부작용뿐이었다. 이제 덩이가 서로 다른 톤으로 서야 한다(원장 `DarkCliffAt`).
                    // 급경사에 그늘 쪽을 더 섞던 항도 걷었다 — 그 이유(「늘어난 줄무늬가 한 줄로
                    // 이어지지 않게」) 역시 랩 ⑧에서 사라졌고, 남은 효과는 **어두운 덩이 쪽으로 쏠림**뿐이었다
                    // (자 실측 밝은 덩이 17% / 어두운 52%). 덩이는 원장 노이즈 하나로만 가른다.
                    float darkShare = WorldSplat.DarkCliffAt(wx, wz);
                    w[WorldSplat.Rock] = rockW * keep * (1f - darkShare);
                    w[WorldSplat.CliffDark] = rockW * keep * darkShare;
                    w[WorldSplat.Sand] = sand;
                    if (cover >= 0)
                        w[cover] += coverW;
                    float sum = 0.0001f;
                    for (int c = 0; c < WorldSplat.LayerCount; c++)
                        sum += w[c];
                    for (int c = 0; c < WorldSplat.LayerCount; c++)
                        alpha[z, x, c] = w[c] / sum;
                }
            }
            data.SetAlphamaps(0, 0, alpha);
            EditorUtility.SetDirty(data);
            // TerrainData는 에셋이다 — 저장하지 않으면 씬을 다시 열 때 디스크의 옛 지형이 돌아온다.
            // **생성물이 에셋이면 저장까지가 수리다**(두 번 밟았다 — 지형, 그리고 나무 tint 재질).
            // 자는 메모리를 보고 화면은 디스크를 본다: `SetDirty`만 하면 셀프체크는 초록인데 QA 샷은
            // 옛 값을 찍는다. 새 재질·새 레이어를 만드는 패스는 반드시 이 호출까지 함께 넣어라.
            AssetDatabase.SaveAssets();
            var found = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = found.Length - 1; i >= 0; i--)
            {
                if (found[i] == null || found[i].name != "Ground")
                    continue;
                if (found[i].GetComponent<Terrain>() != null)
                    continue;
                UnityEngine.Object.DestroyImmediate(found[i].gameObject);
            }
            var go = GameObject.Find("Ground");
            if (go == null)
            {
                go = Terrain.CreateTerrainGameObject(data);
                go.name = "Ground";
            }
            var terrain = go.GetComponent<Terrain>();
            terrain.terrainData = data;
            go.transform.position = new Vector3(-half, 0f, -half);
            terrain.heightmapPixelError = 5f;
            terrain.basemapDistance = 160f;
            terrain.shadowCastingMode = ShadowCastingMode.On;
            var col = go.GetComponent<TerrainCollider>();
            if (col != null)
                col.terrainData = data;

            // **투영을 바꾼다**(랩 ⑧) — 기본 지형 셰이더의 XZ 평면 투영이 82° 절벽에서 무늬를
            // 세로로 늘린다. 알파맵을 다 칠한 **뒤에** 굽는다: 컨트롤 텍스처는 알파맵의 사본이다.
            ApplyTriplanarTerrain(terrain, data, alpha);

            EnsureWater();
            EnsureWorldAtmosphere();
        }

        /// <summary>
        /// 바다·강·호수는 같은 수면 하나로 만든다 — 지형이 SeaLevel 아래로 파인 곳에서만 물이 보인다.
        /// 단색 파란 판은 §8.2 위반이라 노이즈 텍스처 재질을 쓴다(Default-Material 프리미티브 금지).
        /// </summary>
        static void EnsureWater()
        {
            var mat = MakeNoiseMat("SeaWater", new Color(0.10f, 0.28f, 0.42f), new Color(0.18f, 0.44f, 0.58f));
            if (mat != null)
            {
                mat.SetFloat("_Glossiness", 0.85f);
                mat.SetFloat("_Metallic", 0.1f);
                if (mat.HasProperty("_MainTex"))
                    mat.mainTextureScale = new Vector2(24f, 24f);
                EditorUtility.SetDirty(mat);
            }
            var go = GameObject.Find(WaterObject);
            if (go == null)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Plane);
                go.name = WaterObject;
                var c = go.GetComponent<Collider>();
                if (c != null)
                    UnityEngine.Object.DestroyImmediate(c);
            }
            go.transform.position = new Vector3(0f, WorldTerrain.SeaLevel, 0f);
            // 지형보다 훨씬 넓게 — 수면 끝이 화면에 보이면 "판때기"로 읽힌다.
            go.transform.localScale = new Vector3(WorldTerrain.Span * 0.3f, 1f, WorldTerrain.Span * 0.3f);
            var rend = go.GetComponent<Renderer>();
            if (rend != null && mat != null)
                rend.sharedMaterial = mat;
        }

        static TerrainLayer EnsureTerrainLayer(string name, Color a, Color b, float tile)
        {
            return EnsureTerrainLayer(name, a, b, tile, 0);
        }

        static TerrainLayer EnsureTerrainLayer(string name, Color a, Color b, float tile, int pattern)
        {
            var mat = MakeNoiseMat(name, a, b, pattern);
            var tex = mat != null ? mat.mainTexture as Texture2D : null;
            string path = "Assets/Game/Art/Env/" + name + ".terrainlayer";
            var tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (tl == null)
            {
                tl = new TerrainLayer();
                AssetDatabase.CreateAsset(tl, path);
            }
            tl.diffuseTexture = tex;
            tl.tileSize = new Vector2(tile, tile);
            EditorUtility.SetDirty(tl);
            return tl;
        }

        static void PlaceKenney()
        {
            var millGo = Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/windmill.fbx", new Vector3(-10.5f, 0f, 8.5f), Vector3.zero);
            if (millGo != null)
            {
                millGo.name = "Banker";
                var bank = millGo.AddComponent<BankStation>();
                bank.DisplayName = "은행";
            }
            Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall.fbx", new Vector3(-5.2f, 0f, 3.4f), new Vector3(0f, 90f, 0f));
            Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fountain-round.fbx", new Vector3(-3.6f, 0f, -3.6f), Vector3.zero);
            Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-high.fbx", new Vector3(9f, 0f, 7f), Vector3.zero);
            Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree.fbx", new Vector3(-7f, 0f, -6f), Vector3.zero);
            Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/cart.fbx", new Vector3(-7.4f, 0f, 6.4f), Vector3.zero);
            Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-large.fbx", new Vector3(7.5f, 0f, -3f), Vector3.zero);
            Place("Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bushLarge.fbx", new Vector3(4.6f, 0f, -3.6f), Vector3.zero);
            Place("Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bush.fbx", new Vector3(-6.2f, 0f, -5.4f), Vector3.zero);
            Place("Assets/_ThirdParty/Kenney/Nature/RAW/Models/rock_largeA.fbx", new Vector3(8.5f, 0f, 2.4f), Vector3.zero);
            var veinGo = Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-large.fbx", new Vector3(9.8f, 0f, -3.4f), Vector3.zero);
            if (veinGo != null)
            {
                veinGo.name = "IronVein";
                var node = veinGo.AddComponent<ResourceNode>();
                node.ResourceId = "iron_ore";
                node.DisplayName = "철 광맥";
            }
            var forgeGo = Place("Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall.fbx", new Vector3(-6.8f, 0f, 3.4f), new Vector3(0f, 90f, 0f));
            if (forgeGo != null)
            {
                forgeGo.name = "Forge";
                var station = forgeGo.AddComponent<CraftStation>();
                station.RecipeId = "iron_sword";
                station.DisplayName = "대장간";
            }
            EnsureCarpenterLandmark();
        }
    }
}
