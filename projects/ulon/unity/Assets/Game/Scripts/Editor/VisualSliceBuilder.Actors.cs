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
// 이 파일이 담는 것: 액터 스폰·장비 부착·크기 맞춤·휴머노이드 임포트 설정·애니메이션 클립 — 캐릭터 자산.
// 동작 변경 0 — 구간을 순서 그대로 옮기기만 했다(순서를 바꾸면 주석과 몸통의 짝이 깨진다).
namespace Ulon.Editor
{
    public static partial class VisualSliceBuilder
    {
        static GameObject Place(string path, Vector3 pos, Vector3 euler)
        {
            return Place(path, pos, Quaternion.Euler(euler));
        }

        static GameObject Place(string path, Vector3 pos, Quaternion rot)
        {
            string displayName = Path.GetFileNameWithoutExtension(path);
            // **모델 확장자를 하나만 보면 새 팩에서 조용히 샌다** — OBJ로 배포된 사슴이 RAW 그대로 놓여
            // 「Prefab이어야 한다」 게이트에 걸렸다(2026-09-07). 모델이면 전부 Env 프리팹을 거친다.
            if (IsModelPath(path))
                path = EnsureEnvPrefab(path);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = displayName;
            float yaw = rot.eulerAngles.y;
            go.transform.SetPositionAndRotation(OnGround(new Vector3(pos.x, 0f, pos.z)) + Vector3.up * pos.y, Quaternion.Euler(0f, yaw, 0f));
            SnapRootToGround(go);
            return go;
        }

        static GameObject SpawnActor(string name, string fbx, Vector3 pos, float height, RuntimeAnimatorController controller, bool player, bool enemy, string display, float hp)
        {
            string prefabPath = EnsureKayKitPrefab(fbx);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (model == null)
                throw new InvalidOperationException("모델 없음: " + fbx + " prefab=" + prefabPath);

            var root = new GameObject(name);
            root.transform.position = OnGround(pos);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            FitHeight(visual.transform, root.transform, height);

            var cc = root.AddComponent<CharacterController>();
            cc.height = height;
            cc.radius = Mathf.Clamp(height * 0.18f, 0.22f, 0.4f);
            cc.center = new Vector3(0f, height * 0.5f, 0f);

            foreach (var extra in root.GetComponents<Animator>())
                UnityEngine.Object.DestroyImmediate(extra);
            var anim = visual.GetComponentInChildren<Animator>(true);
            if (anim == null)
                anim = visual.AddComponent<Animator>();
            if (anim.avatar == null)
            {
                var src = model.GetComponent<Animator>();
                if (src != null)
                    anim.avatar = src.avatar;
            }
            if (anim.avatar == null)
                anim.avatar = AvatarFor(name);
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var skins = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skins.Length; i++)
                skins[i].updateWhenOffscreen = true;
            root.AddComponent<CharacterAnim>();
            var sockets = root.AddComponent<EquipmentSockets>();
            sockets.Bind(visual.transform);

            var body = root.AddComponent<WorldBody>();
            body.IsEnemy = enemy;
            body.IsAvatar = player;
            body.DisplayName = display;
            body.MaxHp = hp;

            if (player)
            {
                root.AddComponent<ClickMotor>();
                root.AddComponent<LocalAvatar>();
                root.AddComponent<InventoryBag>();
            }

            return root;
        }

        static void AttachGear(GameObject actor, string swordPath, string shieldPath)
        {
            var sockets = actor.GetComponent<EquipmentSockets>();
            if (sockets == null)
                return;
            AttachIf(sockets, swordPath, sockets.RightHand);
            AttachIf(sockets, shieldPath, sockets.LeftHand);
        }

        /// <summary>
        /// 소켓 컴포넌트가 없는 액터에 무기를 붙인다 — 손 본을 찾아 그 밑에 프리팹을 얹는다.
        /// 손 본도 없으면 몸 옆에 세운다(화면에 무기가 보이는 것이 목적이다).
        /// </summary>
        /// <summary>손 본. 무기를 여기 매달지 않으면 어깨 소켓에 걸려 얼굴 옆에 뜬다(검수 2026-09-06).</summary>
        public static Transform FindHandBone(GameObject actor)
        {
            Transform hand = null;
            var all = actor.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name.ToLowerInvariant();
                if (n.IndexOf("hand") < 0)
                    continue;
                if (hand == null || n.EndsWith(".r") || n.IndexOf("right") >= 0)
                    hand = all[i];
            }
            return hand;
        }

        static Transform AttachWeaponToHand(GameObject actor)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnsureKayKitPrefab(SwordFbx));
            if (prefab == null)
                return null;
            Transform hand = FindHandBone(actor);
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (item == null)
                return null;
            if (hand != null)
            {
                item.transform.SetParent(hand, false);
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;
            }
            else
            {
                var cc = actor.GetComponent<CharacterController>();
                float h = cc != null ? cc.height : 2f;
                item.transform.SetParent(actor.transform, false);
                item.transform.localPosition = new Vector3(0.45f, h * 0.35f, 0.1f);
                item.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            }
            item.name = "Sword_Boss";
            return item.transform;
        }

        /// <summary>손에 든(또는 붙어 있는) 무기 중 **가장 큰 것** — 첫 번째를 잡으면 완드 같은 소품이 걸린다.</summary>
        static Transform FindGearTransform(GameObject actor)
        {
            Transform best = null;
            float bestLen = -1f;
            var all = actor.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (!ContainsGearName(t.name))
                    continue;
                if (t.name.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0
                    || t.name.IndexOf("Quiver", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                var rends = t.GetComponentsInChildren<Renderer>(true);
                if (rends.Length == 0)
                    continue;
                var b = rends[0].bounds;
                for (int k = 1; k < rends.Length; k++)
                    b.Encapsulate(rends[k].bounds);
                float len = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (len > bestLen)
                {
                    bestLen = len;
                    best = t;
                }
            }
            return best;
        }

        /// <summary>렌더가 켜진 자식들의 합 바운드.</summary>
        static bool BoundsOfEnabled(Transform t, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            var rends = t.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || !rends[i].gameObject.activeInHierarchy)
                    continue;
                if (!any) { bounds = rends[i].bounds; any = true; }
                else bounds.Encapsulate(rends[i].bounds);
            }
            return any;
        }

        static void HideExtraGear(GameObject actor)
        {
            string[] keep = { "1H_Sword", "Round_Shield", "sword_1handed", "shield_round" };
            foreach (var t in actor.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                bool gear = ContainsGearName(n);
                if (!gear)
                    continue;
                if (n.StartsWith(BossWeaponPrefix, StringComparison.Ordinal))
                    continue;   // 보스 무기는 여기서 다시 끄면 안 된다(헥사크 완드가 이렇게 사라졌다)
                bool keepIt = false;
                for (int i = 0; i < keep.Length; i++)
                    if (n == keep[i])
                        keepIt = true;
                if (!keepIt)
                    t.gameObject.SetActive(false);
            }
        }

        /// <summary>장비 이름인가(게이트도 같은 판정을 쓴다).</summary>
        public static bool IsGearName(string n) => ContainsGearName(n);

        /// <summary>
        /// **한 사람이 걸친 장비 전수** — 이름 원장 **또는** 손자리로 찾는다(랩 ①, 2026-09-08).
        ///
        /// 이름만 보던 자는 모델을 받을 때마다 샜다: 후드 도적의 칼이 `Knife`라 목록 밖이었고
        /// 「맨손」이어야 할 치유사가 칼을 든 채 화면에 섰다(`48_person_Healer` 실측). 이름을 늘리는
        /// 것은 **다음 팩에서 또 새는 수리**다 — 새 팩이 `Throwable`·`Spellbook`을 들고 오면 그만이다.
        ///
        /// 그래서 자를 하나 더 세운다: **손뼈(또는 킷이 쥐라고 만들어 둔 `handslot`) 밑에 매달린
        /// 비스킨드 렌더러는 이름이 무엇이든 든 것이다.** 자리는 이름과 달리 팩이 바뀌어도 남는다.
        /// 둘의 **합집합**을 쓰는 이유 — 등에 멘 칼처럼 손 밖에 달린 것도 실루엣에서는 장비이고
        /// (그건 이름이 잡는다), 손에 쥔 새 이름은 자리가 잡는다. 한쪽만 쓰면 각각 한 종류씩 샌다.
        /// **부작용 없음** — 게이트가 이 함수를 그대로 불러 NC를 걸 수 있게 순수 함수로 둔다.
        /// </summary>
        public static List<Transform> GearOnActor(GameObject who)
        {
            var found = new List<Transform>();
            if (who == null)
                return found;
            foreach (var t in who.GetComponentsInChildren<Transform>(true))
            {
                var r = t.GetComponent<Renderer>();
                if (r == null || r is ParticleSystemRenderer)
                    continue;
                if (ContainsGearName(t.name) || (!(r is SkinnedMeshRenderer) && UnderHandBone(t)))
                    found.Add(t);
            }
            return found;
        }

        /// <summary>이 노드가 손뼈 밑에 매달려 있는가 — 몸(스킨드)은 손뼈를 품으므로 부르는 쪽에서 뺀다.</summary>
        public static bool UnderHandBone(Transform t)
        {
            for (Transform p = t.parent; p != null; p = p.parent)
                if (p.name.StartsWith("handslot", StringComparison.OrdinalIgnoreCase)
                    || p.name.StartsWith("hand", StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>손뼈에서 이 거리 안에 있으면 「손에 든 것」 — 세우는 자와 게이트가 같이 쓴다.</summary>
        public const float HandHoldRadius = 0.35f;

        /// <summary>
        /// **이름이 아니라 자리로 「든 것」을 찾는다**(2026-09-08). 이름 목록은 모델을 받을 때마다
        /// 샌다 — 후드 도적의 칼은 `Knife`, 그 다음엔 `Throwable`·`Spellbook`이 나왔다.
        /// 손뼈 근처에 켜져 있는 **비스킨드** 렌더러를 전부 센다(몸 자체는 손뼈를 품으므로 제외).
        /// </summary>
        public static List<Renderer> HeldNearHands(GameObject who)
        {
            var held = new List<Renderer>();
            var hands = new List<Transform>();
            foreach (var t in who.GetComponentsInChildren<Transform>(true))
                if (t.name.IndexOf("Hand", StringComparison.OrdinalIgnoreCase) >= 0)
                    hands.Add(t);
            if (hands.Count == 0)
                return held;
            foreach (var r in who.GetComponentsInChildren<Renderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer || r is SkinnedMeshRenderer)
                    continue;
                for (int i = 0; i < hands.Count; i++)
                    if (Vector3.Distance(r.bounds.center, hands[i].position) <= HandHoldRadius)
                    { held.Add(r); break; }
            }
            return held;
        }

        /// <summary>손에 드는 **무기**인가 — 방패·화살통은 무기가 아니다(1몹 1무기 판정용).</summary>
        public static bool IsWeaponName(string n)
        {
            if (!ContainsGearName(n))
                return false;
            return n.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) < 0
                && n.IndexOf("Quiver", StringComparison.OrdinalIgnoreCase) < 0;
        }

        static bool ContainsGearName(string n)
        {
            return n.IndexOf("Sword", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Axe", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Staff", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Bow", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Quiver", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Dagger", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Wand", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Crossbow", StringComparison.OrdinalIgnoreCase) >= 0
                // **이름 원장은 모델을 받을 때마다 새는 자다** — 후드 도적(2026-09-08 도입)의 칼은
                // `Knife`·`Knife_Offhand`라 이 목록에 없었고, 「맨손」이어야 할 치유사가 칼을 든 채
                // 화면에 섰다(48_person_Healer 실측). 그래서 이름을 늘리는 동시에
                // **손에 켜진 것이 있는지 실물로 재는 게이트**(`AssertBarehandVillagers`)를 같이 두었다.
                || n.IndexOf("Knife", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Blade", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void AttachIf(EquipmentSockets sockets, string path, Transform socket)
        {
            if (socket == null)
            {
                Debug.LogWarning("[Ulon] 소켓을 못 찾아 장비를 건너뜁니다: " + path);
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnsureKayKitPrefab(path));
            if (prefab == null)
                return;
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            sockets.Attach(item, socket);
        }

        /// <summary>
        /// 모자·투구는 키 측정에서 뺀다(검수 2026-09-06 P0-3). 헥사크(KayKit Mage)는 챙 넓은 모자가
        /// 전체 바운드를 키워서, 목표 키에 맞추면 몸이 쪼그라들고 모자만 보였다.
        /// </summary>
        static bool IsHeadgear(Transform t)
        {
            var actor = t;
            while (actor != null && actor.GetComponent<CharacterController>() == null)
                actor = actor.parent;
            return GroundFit.IsHeadgear(actor != null ? actor : t.root, t);   // 자는 한 곳(GroundFit)에만 산다
        }

        /// <summary>모자를 몸 비례로 줄인다 — 45° 시점에서 챙이 몸을 덮지 않게.</summary>
        static void SlimHeadgear(Transform visual)
        {
            var all = visual.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == visual)
                    continue;
                if (all[i].name.IndexOf("Hat", StringComparison.OrdinalIgnoreCase) >= 0)
                    all[i].localScale = all[i].localScale * HeadgearScale;
            }
        }

        const float HeadgearScale = 0.62f;

        static bool BoundsOf(Transform visual, bool includeHeadgear, out Bounds bounds)
        {
            bounds = new Bounds();
            var rends = visual.GetComponentsInChildren<Renderer>();
            bool any = false;
            for (int i = 0; i < rends.Length; i++)
            {
                if (!includeHeadgear && IsHeadgear(rends[i].transform))
                    continue;
                if (!any)
                {
                    bounds = rends[i].bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(rends[i].bounds);
                }
            }
            return any;
        }

        /// <summary>
        /// 짐승 모델을 **원장 키**에 맞춘다. 모델마다 제작 단위가 달라(사슴 OBJ, 멧돼지 Blender FBX)
        /// 그냥 놓으면 거인이나 먼지가 된다 — 영지 사고와 같은 계열이라 크기는 코드가 정한다.
        /// </summary>
        /// <summary>
        /// 짐승 모델을 **칠한다**(§8.2 무텍스처 금지). 받은 CC0 모델은 단색 재질(사슴)이거나
        /// 텍스처가 FBX에 안 딸려 왔다(멧돼지) — 소품과 같은 잡음 텍스처 재질로 부위별로 칠한다.
        /// 부위 구분은 원본 재질 이름(deer_skin·deer_horn·Deer_hoaves)으로 한다.
        /// </summary>
        static void PaintCreature(GameObject go, bool deer)
        {
            var hide = MakeNoiseMat(deer ? "DeerHide" : "BoarHide",
                deer ? new Color(0.42f, 0.28f, 0.16f) : new Color(0.24f, 0.19f, 0.16f),
                deer ? new Color(0.55f, 0.38f, 0.22f) : new Color(0.34f, 0.27f, 0.22f));
            var horn = MakeNoiseMat("CreatureHorn", new Color(0.68f, 0.62f, 0.48f), new Color(0.82f, 0.76f, 0.60f));
            var hoof = MakeNoiseMat("CreatureHoof", new Color(0.10f, 0.09f, 0.08f), new Color(0.18f, 0.16f, 0.14f));
            var rends = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                var mats = rends[i].sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    string n = mats[m] != null ? mats[m].name : "";
                    mats[m] = n.IndexOf("horn", StringComparison.OrdinalIgnoreCase) >= 0 ? horn
                            : n.IndexOf("hoav", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              n.IndexOf("hoof", StringComparison.OrdinalIgnoreCase) >= 0 ? hoof
                            : hide;
                }
                rends[i].sharedMaterials = mats;
            }
        }

        /// <summary>
        /// 짐승 키 맞추기 — **사람과 같은 자**(구운 몸)로 잰다(랩 ⑥).
        ///
        /// 옛 판은 `BoundsOf(..., includeInactive: true)`로 쟀다. 꺼져 있는 노드까지 들어가
        /// 실제 몸보다 훨씬 큰 값이 나오면 배율이 그만큼 작아진다 — 멧돼지가 **0.03m**(원장 0.95m)로
        /// 서 있었고, 근접 샷은 피사체에 맞춰 당기니 화면으로는 멀쩡해 보였다. 새 몹 키 게이트가 잡았다.
        /// </summary>
        static void FitCreatureHeight(GameObject go, float target)
        {
            if (target < 0.05f)
                return;
            float got = FitMeasuredHeight(go.transform, go.transform, target);
            if (got > 0.0001f && Mathf.Abs(got - target) / target >= 0.05f)
                Debug.LogWarning("[Ulon] 짐승 키가 수렴하지 않았습니다 — " + go.name + " 목표 " +
                                 target.ToString("0.00") + "m, 마지막 실측 " + got.ToString("0.000") + "m");
        }

        /// <summary>
        /// 키를 맞춘다 — **재는 자와 같은 자로**(검수 조건 2·3, 랩 ⑥).
        ///
        /// 옛 판은 `BoundsOf`(스킨드 렌더러 bounds 그대로 + 장비 포함)로 맞추는데 게이트는
        /// 구운 몸(`GroundFit.BodyBounds`)으로 쟀다. 자가 둘이라 자객이 원장 1.70m인데 2.37m로,
        /// 도적이 1.75m인데 1.53m로 섰다. **맞추는 쪽이 게이트와 같은 자를 쓴다.**
        /// </summary>
        static void FitHeight(Transform visual, Transform root, float target)
        {
            visual.localPosition = Vector3.zero;
            visual.localScale = Vector3.one;
            var rends = visual.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0)
                return;
            SlimHeadgear(visual);
            // 한 번 곱해서 안 끝나는 모델이 있다(본이 스케일된 노드 아래 있으면 배율이 두 번 들어간다) —
            // **반응을 재서** 맞춘다. 안 그러면 같은 모델인데 도적 1.77m·던전 도적 1.53m처럼 갈린다.
            FitMeasuredHeight(visual, visual, target);
            if (!MeasuredBody(visual, out Bounds b))
                return;
            visual.localPosition = new Vector3(0f, visual.localPosition.y - (b.min.y - root.position.y), 0f);
        }

        /// <summary>게이트와 **같은 자** — 스킨드 메시를 구워서 재고 장비는 뺀다.</summary>
        static bool MeasuredBody(Transform visual, out Bounds b)
        {
            if (GroundFit.WorldBounds(visual, out b, t => GroundFit.IsGear(visual, t)) && b.size.y > 0.01f)
                return true;
            return BoundsOf(visual, false, out b) || BoundsOf(visual, true, out b);
        }

        /// <summary>
        /// **몹을 원장 키로 다시 세운다**(멱등, 랩 ⑥). 이미 서 있는 몹은 `EnsureHuntMob`이
        /// 일찍 반환하므로 `FitHeight`를 고쳐도 옛 크기가 그대로 남는다 —
        /// 「Ensure가 일찍 반환할 때 이미 있는 것이 옳은지는 아무도 안 본다」 계열이다.
        /// 목표 비율로 한 번에 맞추므로(곱하기 누적이 아니라) 두 번째 실행은 아무것도 안 바꾼다.
        /// </summary>
        public static void EnsureMobSizes()
        {
            var mobs = UnityEngine.Object.FindObjectsByType<WorldBody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var fixedNames = new List<string>();
            var skipped = new List<string>();
            int seen = 0;
            for (int i = 0; i < mobs.Length; i++)
            {
                string id = mobs[i].MobId;
                if (string.IsNullOrEmpty(id))
                    continue;
                float want = MobCatalog.HeightOf(id);
                if (want < 0.01f)
                    continue;
                seen++;
                var target = SizeTarget(mobs[i].transform);
                if (target == null)
                {
                    skipped.Add(mobs[i].name + "(키울 것이 없음)");
                    continue;
                }
                if (!GroundFit.BodyBounds(mobs[i].transform, out Bounds b) || b.size.y < 0.0001f)
                {
                    skipped.Add(mobs[i].name + "(잴 몸이 없음)");
                    continue;
                }
                if (GroundFit.WorldBounds(mobs[i].transform, out Bounds full) && b.size.y < full.size.y * 0.5f)
                {
                    // **건너뛴 것을 침묵으로 두지 않는다** — 조용히 빠지면 그 몹은 영원히 틀린 크기다.
                    skipped.Add(mobs[i].name + "(몸 자가 무너짐 " + b.size.y.ToString("0.00") + "/" + full.size.y.ToString("0.00") + "m)");
                    continue;
                }
                float before = b.size.y;
                // **게이트가 받아 주는 폭 안이면 손대지 않는다.** 2%로 두었더니 보스가 매 실행
                // 2.68↔2.69를 오가며 멱등 게이트를 깼다(포즈에 따라 구운 바운드가 조금씩 다르다).
                // 고칠 것은 「명백히 틀린 것」이고, 그 판단 기준은 게이트와 같아야 한다.
                if (Mathf.Abs(before - want) / want <= 0.08f)
                    continue;
                // 한 번 곱해서 안 끝나는 모델이 있다(멧돼지: 재고 곱한 뒤 다시 재면 값이 다르다) —
                // **수렴할 때까지 재고 맞춘다**. 목표 비율로만 곱하므로 맞은 것은 첫 회에 빠져나간다.
                float got = FitMeasuredHeight(mobs[i].transform, target, want);
                if (GroundFit.BodyBounds(mobs[i].transform, out Bounds after))
                    target.position += new Vector3(0f, mobs[i].transform.position.y - after.min.y, 0f);
                fixedNames.Add(mobs[i].name + " " + before.ToString("0.00") + "→" + got.ToString("0.00") + "m");
            }
            Debug.Log("[Ulon] 몹 키 보정 — 원장에 묶인 " + seen + "체 중 " + fixedNames.Count + "체 조정·" +
                      skipped.Count + "체 건너뜀. 조정: " + (fixedNames.Count == 0 ? "(전부 이미 맞다)" : string.Join(", ", fixedNames)) +
                      (skipped.Count == 0 ? "" : " / 건너뜀: " + string.Join(", ", skipped)));
        }

        /// <summary>
        /// **그림이 충돌체와 같은 크기인가**(랩 A, 2026-09-09 — 대장 판정).
        ///
        /// 실측: 액터 15체 중 **플레이어만** 몸 2.63m·캡슐 1.80m로 **1.46배**였다(나머지는 전부 1.00).
        /// 원인은 크기를 고치는 패스(`EnsureMobSizes`)가 **몹 원장(`MobId`)에 묶인 것만** 손대기
        /// 때문이다 — 플레이어는 원장 밖이라 어느 패스도 안 봤고, 씬에 저장된 옛 크기가 그대로 남았다.
        /// 「Ensure가 일찍 반환할 때 이미 있는 것이 옳은지는 아무도 안 본다」의 또 한 사례다.
        ///
        /// 그래서 **원장이 아니라 자리로** 잰다: 몸(구운 스킨, 장비 제외)의 키는 그 액터의
        /// `CharacterController` 높이와 같아야 한다. 캡슐은 모든 액터가 반드시 가지는 것이고
        /// (없으면 배우도 아니다 — `ActorsToDress`), 전투 거리·카메라·충돌이 이미 그것을 쓴다.
        /// 멱등: 게이트가 받아 주는 폭(8%) 안이면 손대지 않는다.
        /// </summary>
        public static void EnsureActorBodyMatchesCapsule()
        {
            var actors = ActorsToDress();
            var fixedNames = new List<string>();
            var skipped = new List<string>();
            for (int i = 0; i < actors.Length; i++)
            {
                var root = actors[i].transform;
                var cc = actors[i].GetComponent<CharacterController>();
                if (cc == null)
                    continue;
                float want = cc.height * root.lossyScale.y;
                if (want <= 0.01f)
                {
                    skipped.Add(root.name + "(캡슐 높이 0)");
                    continue;
                }
                // 키를 **어디에 걸어야 하는가**는 만드는 쪽과 같은 곳이어야 한다(멧돼지 3cm 사고).
                var target = root.Find("Visual") ?? root;
                // 사람 몸만 잰다 — 장비도 시설 장식(훈련사 배너 2.0m)도 빼고(GroundFit.PersonBounds).
                if (!GroundFit.PersonBounds(root, out Bounds b) || b.size.y < 0.01f)
                {
                    skipped.Add(root.name + "(사람 몸을 못 쟀다)");
                    continue;
                }
                float before = b.size.y;
                if (Mathf.Abs(before - want) / want <= 0.08f)
                    continue;
                float got = FitSkinHeight(root, target, want);
                if (GroundFit.PersonBounds(root, out Bounds after))
                    target.position += new Vector3(0f, root.position.y - after.min.y, 0f);
                // **메시가 루트 자체에 붙은 액터는 루트를 줄이면 캡슐도 같이 줄어든다**(훈련사 실측:
                // 그림 2.90→1.75인데 캡슐이 1.75→1.06으로 따라 내려가 비율이 그대로 1.89였다).
                // 그림만 줄이는 것이 목적이므로 **캡슐의 월드 높이를 원래대로 되돌린다** —
                // 전투 거리·충돌은 이 값을 쓰니 세계 규칙은 손대지 않는다.
                if (target == root)
                {
                    float lossy = Mathf.Max(0.0001f, root.lossyScale.y);
                    cc.height = want / lossy;
                    cc.radius = Mathf.Clamp(cc.height * 0.18f, 0.22f / lossy, 0.4f / lossy);
                    cc.center = new Vector3(cc.center.x, cc.height * 0.5f, cc.center.z);
                }
                fixedNames.Add(root.name + " " + before.ToString("0.00") + "→" + got.ToString("0.00") +
                               "m(캡슐 " + want.ToString("0.00") + "m" + (target == root ? ", 루트 배율 보정" : "") + ")");
            }
            Debug.Log("[Ulon] 그림-충돌체 크기 맞춤 — 액터 " + actors.Length + "체 중 " + fixedNames.Count +
                      "체 조정" + (skipped.Count > 0 ? "·" + skipped.Count + "체 건너뜀" : "") + ". 조정: " +
                      (fixedNames.Count == 0 ? "(전부 이미 맞다)" : string.Join(", ", fixedNames)) +
                      (skipped.Count == 0 ? "" : " / 건너뜀: " + string.Join(", ", skipped)));
        }

        /// <summary>
        /// 잰 키를 목표에 맞춘다 — **반응을 재서** 맞춘다.
        ///
        /// 「배율을 r배 하면 키도 r배」는 **추측**이다. 멧돼지는 루트를 1/37로 줄이자 키가 1/1170으로
        /// 줄었다(제곱 반응) — 본이 스케일된 루트 아래 있어 구운 메시에 배율이 두 번 들어가는 경로다.
        /// 그래서 비율로 한 번 곱하는 방식은 진동만 하고 영원히 안 맞는다(실제로 35.10→0.03을 오갔다).
        /// 여기서는 한 번 곱해 보고 **지수 e = log(변화)/log(배율)** 를 실측한 뒤 그 지수로 푼다.
        /// 사람형(e≈1)도 짐승(e≈2)도 같은 코드로 두세 번에 수렴한다.
        /// </summary>
        /// <summary>사람 몸만 보고 맞춘다 — 재는 자와 맞추는 자는 하나여야 한다(랩 A).</summary>
        static float FitSkinHeight(Transform actor, Transform target, float want)
        {
            float e = 1f;
            float got = 0f;
            for (int pass = 0; pass < 8; pass++)
            {
                if (!GroundFit.PersonBounds(actor, out Bounds cur) || cur.size.y < 0.0001f)
                    return got;
                got = cur.size.y;
                if (Mathf.Abs(got - want) / want < 0.02f)
                    return got;
                float f = Mathf.Pow(want / got, 1f / Mathf.Clamp(e, 0.5f, 4f));
                f = Mathf.Clamp(f, 0.02f, 50f);
                target.localScale = target.localScale * f;
                if (!GroundFit.PersonBounds(actor, out Bounds next) || next.size.y < 0.0001f)
                    return got;
                float lf = Mathf.Log(f);
                if (Mathf.Abs(lf) > 0.001f)
                    e = Mathf.Clamp(Mathf.Log(next.size.y / got) / lf, 0.5f, 4f);
                got = next.size.y;
            }
            return got;
        }

        static float FitMeasuredHeight(Transform actor, Transform target, float want)
        {
            float e = 1f;
            float got = 0f;
            for (int pass = 0; pass < 8; pass++)
            {
                if (!GroundFit.BodyBounds(actor, out Bounds cur) || cur.size.y < 0.0001f)
                    return got;
                got = cur.size.y;
                if (Mathf.Abs(got - want) / want < 0.02f)
                    return got;
                float f = Mathf.Pow(want / got, 1f / Mathf.Clamp(e, 0.5f, 4f));
                f = Mathf.Clamp(f, 0.02f, 50f);
                target.localScale = target.localScale * f;
                if (!GroundFit.BodyBounds(actor, out Bounds next) || next.size.y < 0.0001f)
                    return got;
                // 반응 지수를 실측해 다음 회에 쓴다(선형이면 1, 제곱이면 2가 나온다).
                float lf = Mathf.Log(f);
                if (Mathf.Abs(lf) > 0.001f)
                    e = Mathf.Clamp(Mathf.Log(next.size.y / got) / lf, 0.5f, 4f);
                got = next.size.y;
            }
            return got;
        }

        /// <summary>
        /// 키를 **어디에 걸어야 하는가** — 사람형은 몸이 `Visual` 자식에 있고, 짐승은 **루트 자체**가 메시다.
        /// 처음엔 둘 다 자식을 키우려다 멧돼지가 루트 배율(0.0037)에 자식 배율(0.027)까지 곱해져
        /// 3cm로 쪼그라들었다 — **만드는 쪽이 어디를 키웠는지와 같은 곳**을 잡아야 한다.
        /// </summary>
        static Transform SizeTarget(Transform actor)
        {
            if (actor.GetComponent<CharacterController>() == null)
                return actor;                                   // 짐승 — 루트가 곧 몸이다(FitCreatureHeight와 같은 자리)
            var visual = actor.Find("Visual");
            if (visual != null)
                return visual;
            for (int c = 0; c < actor.childCount; c++)
                if (actor.GetChild(c).GetComponentInChildren<Renderer>(true) != null)
                    return actor.GetChild(c);
            return null;
        }

        static AnimatorController BuildController(AnimationClip idle, AnimationClip walk, AnimationClip run, AnimationClip attack)
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Game/Art/Characters"));
            if (File.Exists(ControllerPath))
                AssetDatabase.DeleteAsset(ControllerPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var parameters = controller.parameters;
            bool hasSpeed = false, hasAttack = false;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == "Speed") hasSpeed = true;
                if (parameters[i].name == "Attack") hasAttack = true;
            }
            if (!hasSpeed)
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            if (!hasAttack)
                controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            var locomo = sm.AddState("Locomotion");
            var tree = new BlendTree
            {
                name = "Move",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            locomo.motion = tree;
            tree.AddChild(idle, 0f);
            if (walk != null)
                tree.AddChild(walk, 2.3f);
            if (run != null)
                tree.AddChild(run, 4.4f);
            tree.minThreshold = 0f;
            tree.maxThreshold = run != null ? 4.4f : (walk != null ? 2.3f : 1f);
            sm.defaultState = locomo;

            if (attack != null)
            {
                var atk = sm.AddState("Attack");
                atk.motion = attack;
                var toAtk = locomo.AddTransition(atk);
                toAtk.hasExitTime = false;
                toAtk.duration = 0.05f;
                toAtk.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                var back = atk.AddTransition(locomo);
                back.hasExitTime = true;
                back.exitTime = 0.85f;
                back.duration = 0.1f;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        internal static bool ConfigureHumanoid(string path, bool importClips)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("importer 없음: " + path);
            if (IsHumanoidConfigured(importer, importClips))
                return false;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.optimizeGameObjects = false;
            importer.importAnimation = importClips;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            if (importClips)
                LoopLocomotionClips(importer);
            importer.SaveAndReimport();

            importer = AssetImporter.GetAtPath(path) as ModelImporter;
            var desc = importer.humanDescription;
            desc.human = BuildHuman(desc.skeleton);
            desc.hasTranslationDoF = false;
            importer.humanDescription = desc;
            importer.SaveAndReimport();
            return true;
        }

        static bool IsHumanoidConfigured(ModelImporter importer, bool importClips)
        {
            if (importer.animationType != ModelImporterAnimationType.Human
                || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel
                || importer.optimizeGameObjects
                || importer.importAnimation != importClips
                || importer.animationCompression != ModelImporterAnimationCompression.Off
                || importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
                return false;

            if (importClips && !LocomotionClipsConfigured(importer.clipAnimations))
                return false;

            var desc = importer.humanDescription;
            if (desc.hasTranslationDoF)
                return false;
            return SameHumanBones(desc.human, BuildHuman(desc.skeleton));
        }

        static bool LocomotionClipsConfigured(ModelImporterClipAnimation[] clips)
        {
            if (clips == null || clips.Length == 0)
                return false;
            for (int i = 0; i < clips.Length; i++)
            {
                string n = clips[i].name.ToLowerInvariant();
                bool shouldLoop = n.Contains("idle") || n.Contains("walk") || n.Contains("run")
                    || n.Contains("aiming") || n.Contains("shooting");
                if (clips[i].loopTime != shouldLoop || clips[i].loopPose != shouldLoop)
                    return false;
            }
            return true;
        }

        static bool SameHumanBones(HumanBone[] actual, HumanBone[] expected)
        {
            if (actual == null || expected == null || actual.Length != expected.Length)
                return false;
            for (int i = 0; i < actual.Length; i++)
            {
                if (actual[i].humanName != expected[i].humanName
                    || actual[i].boneName != expected[i].boneName
                    || actual[i].limit.useDefaultValues != expected[i].limit.useDefaultValues)
                    return false;
            }
            return true;
        }

        static bool HasSkel(SkeletonBone[] skel, string name)
        {
            for (int i = 0; i < skel.Length; i++)
                if (skel[i].name == name)
                    return true;
            return false;
        }

        static HumanBone Human(string humanName, string boneName)
        {
            return new HumanBone
            {
                humanName = humanName,
                boneName = boneName,
                limit = new HumanLimit { useDefaultValues = true }
            };
        }

        static HumanBone[] BuildHuman(SkeletonBone[] skel)
        {
            var list = new List<HumanBone>();
            void Add(string humanName, params string[] bones)
            {
                for (int i = 0; i < bones.Length; i++)
                {
                    if (!HasSkel(skel, bones[i]))
                        continue;
                    list.Add(Human(humanName, bones[i]));
                    return;
                }
            }

            Add("Hips", "hips");
            Add("Spine", "spine");
            Add("Chest", "chest");
            Add("Head", "head");
            Add("LeftUpperLeg", "upperleg.l");
            Add("RightUpperLeg", "upperleg.r");
            Add("LeftLowerLeg", "lowerleg.l");
            Add("RightLowerLeg", "lowerleg.r");
            Add("LeftFoot", "foot.l");
            Add("RightFoot", "foot.r");
            Add("LeftToes", "toes.l");
            Add("RightToes", "toes.r");
            Add("LeftUpperArm", "upperarm.l");
            Add("RightUpperArm", "upperarm.r");
            Add("LeftLowerArm", "lowerarm.l");
            Add("RightLowerArm", "lowerarm.r");
            Add("LeftHand", "wrist.l", "hand.l");
            Add("RightHand", "wrist.r", "hand.r");
            return list.ToArray();
        }

        static void LoopLocomotionClips(ModelImporter importer)
        {
            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                return;
            for (int i = 0; i < clips.Length; i++)
            {
                string n = clips[i].name.ToLowerInvariant();
                bool loop = n.Contains("idle") || n.Contains("walk") || n.Contains("run")
                    || n.Contains("aiming") || n.Contains("shooting");
                clips[i].loopTime = loop;
                clips[i].loopPose = loop;
            }
            importer.clipAnimations = clips;
        }

        /// <summary>
        /// **배우 안에 매달린 배우를 푼다**(멱등, 2026-09-09).
        ///
        /// 사냥터 기사가 제 밑에 자기 사본을 하나 달고 있었다(`Knight/Knight`, 캡슐 없음, 사냥 구역 밖
        /// z 31.2). 이름이 같으니 `GameObject.Find("Knight")`가 둘 중 아무나 집었고, 게이트는 내내
        /// 껍데기를 재고 통과했다 — 마을을 다시 드레싱해 이름이 흔들리자 그제서야 드러났다.
        ///
        /// 같은 `MobId`를 가진 자식은 **사본**으로 보고 지운다(몹이 여럿인 것은 정상이지만 몹 **안**에
        /// 같은 몹이 있는 것은 정상이 아니다). 다른 몹이면 지우지 않고 루트로 풀어 놓고 크게 남긴다 —
        /// 내용물을 지우는 것은 자의 일이 아니다.
        /// </summary>
        public static void EnsureNoNestedActors()
        {
            var bodies = UnityEngine.Object.FindObjectsByType<WorldBody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var copies = new List<GameObject>();
            var freed = new List<string>();
            for (int i = 0; i < bodies.Length; i++)
            {
                WorldBody host = null;
                for (var p = bodies[i].transform.parent; p != null && host == null; p = p.parent)
                    host = p.GetComponent<WorldBody>();
                if (host == null)
                    continue;
                if (string.Equals(host.MobId, bodies[i].MobId, StringComparison.Ordinal))
                {
                    // **화면에 서 있던 쪽은 사본이었다** — 진짜 루트(캡슐·NetworkObject·NetMob을 가진 쪽)는
                    // 지표 10m 아래에 묻혀 있고, 그 밑에 매달린 사본이 사냥터에 서 있었다(실측 2쌍: 기사·도적).
                    // 게다가 **루트에는 제 그림이 없다** — 사본이 `Visual`을 통째로 갖고 있었다(렌더러 15/12개).
                    // 그래서 사본을 그냥 지우면 몸만 남고 화면에서 사라진다(자격 원장 게이트가 그것을 잡았다).
                    //
                    // 수리는 **몸을 살리고 그림을 물려받는 것**이다(검수 판정 ㉠, 2026-09-09):
                    // ① 사본을 루트에서 떼고(월드 좌표 보존 — 안 떼면 부모를 옮길 때 따라가 두 배로 튄다)
                    // ② 사본이 서 있던 **자리(x·z)만** 물려받고 높이는 **지표에서 다시 유도**하고
                    //    (묻힌 원인이 「옛 평지 시절 y=0」이라 y를 베끼면 원인을 옮겨 심는 것이다)
                    // ③ 사본의 `Visual`을 루트 밑으로 옮긴 뒤 ④ 껍데기를 지운다.
                    var copy = bodies[i].transform;
                    copy.SetParent(null, true);
                    Vector3 spot = copy.position;
                    host.transform.position = new Vector3(spot.x, host.transform.position.y, spot.z);
                    Transform visual = null;
                    foreach (Transform c in copy)
                        if (c.GetComponentsInChildren<Renderer>(true).Length > 0)
                        { visual = c; break; }
                    if (visual != null)
                    {
                        visual.SetParent(host.transform, false);
                        visual.localPosition = Vector3.zero;
                        visual.localRotation = Quaternion.identity;
                        visual.name = "Visual";
                        var sockets = host.GetComponent<EquipmentSockets>();
                        if (sockets != null)
                            sockets.Bind(visual);
                    }
                    // 지표 맞춤은 **그림을 물려받은 뒤에** 한다 — 렌더러가 없는 동안 맞추면 잴 것이 없어
                    // 아무 일도 일어나지 않는다(실측: 먼저 맞췄더니 −10.68m 그대로였다).
                    SnapRootToGround(host.gameObject);
                    copies.Add(copy.gameObject);
                    continue;
                }
                bodies[i].transform.SetParent(null, true);
                freed.Add(bodies[i].name + "(" + bodies[i].MobId + ") ⊂ " + host.name);
            }
            for (int i = 0; i < copies.Count; i++)
            {
                string what = copies[i].name + " " + copies[i].transform.position.ToString("F1");
                UnityEngine.Object.DestroyImmediate(copies[i]);
                Debug.Log("[Ulon] 배우 사본 제거 — " + what + " (제 부모와 같은 MobId, 이름으로 찾는 게이트가 이것을 재고 있었다)");
            }
            if (freed.Count > 0)
                Debug.Log("[Ulon] 배우 겹침 해소 — 루트로 푼 배우 " + freed.Count + "명: " + string.Join(", ", freed));
        }

        static void ConfigureProp(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
                return;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();
        }

        static AnimationClip[] LoadClips(string path)
        {
            var list = new List<AnimationClip>();
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                    list.Add(clip);
            }
            return list.ToArray();
        }

        static AnimationClip BestClip(AnimationClip[] clips, string[] keys, string[] exclude)
        {
            AnimationClip best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < clips.Length; i++)
            {
                string n = clips[i].name.ToLowerInvariant();
                bool banned = false;
                for (int e = 0; e < exclude.Length; e++)
                    if (n.Contains(exclude[e])) { banned = true; break; }
                if (banned)
                    continue;
                for (int k = 0; k < keys.Length; k++)
                {
                    if (!n.Contains(keys[k]))
                        continue;
                    int score = 100 - n.Length - k * 10;
                    if (n == keys[k]) score += 50;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = clips[i];
                    }
                }
            }
            return best;
        }

        static string ClipNames(AnimationClip[] clips)
        {
            var names = new string[Math.Min(clips.Length, 20)];
            for (int i = 0; i < names.Length; i++)
                names[i] = clips[i].name;
            return string.Join(", ", names);
        }
    }
}
