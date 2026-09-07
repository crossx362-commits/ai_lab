using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 「무엇이 땅에 서 있어야 하는가」의 **공용 원장**. 보수 패스(빌더)와 게이트(Assert)가 같은 함수를 쓴다 —
    /// 판정과 수리가 서로 다른 목록을 보면 한쪽이 조용히 어긋난다(검수 2026-09-06 A).
    /// </summary>
    public static class GroundFit
    {
        /// <summary>
        /// 씬에 놓인 배치물(캐릭터·건물·소품)의 **배치 단위** 전수.
        ///
        /// **왜 다시 짰나**(검수 지시 2026-09-07, 부양/매몰 랩): 산포 바위 259개가 지표에서 4.3m 떠
        /// 있었는데 발 게이트가 **초록불**이었다. 옛 판은 「루트에 렌더러가 (자식까지 훑어서) 있으면
        /// 루트 하나를 대상으로 넣는다」였고, 그래서 `PlainScatter`·`Region_*` 같은 **담는 통**이
        /// 통째로 한 덩어리로 재졌다 — 259개의 부양이 **합쳐진 바운드 안에서 상쇄돼** 사라졌다.
        /// 「대상 집합이 좁으면 결함은 그 밖에서 산다」의 네 번째 사례다.
        ///
        /// **단위 규칙(구조로 정한다, 이름 목록이 아니다)**: 어떤 노드의 렌더러 바운드가 수평으로
        /// `UnitSpreadMax`(8m)보다 넓게 퍼져 있으면 그건 한 물건이 아니라 **통**이다 → 자식으로 내려간다.
        /// 그보다 좁으면 한 배치 단위다(집은 지붕·굴뚝까지 한 단위로 같이 내려앉아야 한다).
        /// </summary>
        public const float UnitSpreadMax = 8f;

        /// <summary>직전 `Candidates()`가 **왜 뺐는지** — 침묵으로 빼지 않는다(검수 조건 2).</summary>
        public static readonly List<string> LastExcluded = new List<string>();

        public static List<Transform> Candidates()
        {
            var list = new List<Transform>();
            LastExcluded.Clear();
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
                Collect(list, roots[r].transform, 0);
            return list;
        }

        static void Collect(List<Transform> list, Transform t, int depth)
        {
            if (!t.gameObject.activeInHierarchy)
                return;
            if (SkipContainer(t.name) || SkipItem(t.name))
            {
                if (HasRenderer(t))
                    LastExcluded.Add(t.name + "(선언 제외: " + (SkipItem(t.name) ? "지하가 설계" : "지형·물·관리자") + ")");
                return;
            }
            // **이름이 아니라 자리로도 뺀다**(검수 지시 2026-09-07: 이름으로 거르는 자리를 성질로
            // 바꿀 수 있는지 보라). 던전 방 안에 있고 지표보다 확실히 아래면, 이름이 무엇이든
            // 지하가 정상이다 — 위의 이름 규칙(`Dungeon*`)은 이제 이 판정의 겹띠일 뿐이다.
            if (HasRenderer(t) && UnitBounds(t, out Bounds probe) && InDungeonRoom(probe))
            {
                LastExcluded.Add(NodePath(t) + "(자리로 제외: 던전 방 안·지표 아래)");
                return;
            }
            if (!HasRenderer(t))
                return;
            if (!UnitBounds(t, out Bounds b))
            {
                LastExcluded.Add(NodePath(t) + "(공중에 있는 것이 정상: 파티클뿐)");
                return;
            }
            if (IsWorldScalePlane(b))
            {
                LastExcluded.Add(NodePath(t) + "(월드 규모 판 " + b.size.x.ToString("0") + "×" + b.size.z.ToString("0") + "m)");
                return;
            }
            if (depth < 4 && t.childCount > 0 && Mathf.Max(b.size.x, b.size.z) > UnitSpreadMax)
            {
                for (int c = 0; c < t.childCount; c++)
                    Collect(list, t.GetChild(c), depth + 1);
                return;
            }
            list.Add(t);
        }

        /// <summary>
        /// **자리로 판정하는 지하** — 던전 방 중심에서 수평 12m 안이고 꼭대기가 지표보다 1m 아래면
        /// 그 물건은 지하에 있는 것이 정상이다(§8.2). 이름 규약이 바뀌어도 안 샌다.
        /// </summary>
        public static bool InDungeonRoom(Bounds b)
        {
            var rooms = new[]
            {
                (Dungeon1.InteriorX, Dungeon1.InteriorZ),
                (Dungeon2.InteriorX, Dungeon2.InteriorZ),
                (Dungeon3.InteriorX, Dungeon3.InteriorZ),
            };
            for (int i = 0; i < rooms.Length; i++)
            {
                float dx = b.center.x - rooms[i].Item1, dz = b.center.z - rooms[i].Item2;
                if ((dx * dx) + (dz * dz) > 12f * 12f)
                    continue;
                if (b.max.y < TerrainY(b.center.x, b.center.z) - 1f)
                    return true;
            }
            return false;
        }

        /// <summary>세계만 한 판(바다 수면 등)의 가로폭 하한.</summary>
        public const float WorldScaleSize = 200f;

        /// <summary>
        /// **세계만 한 판은 배치물도 구조물도 아니다** — 이름이 아니라 크기로 뺀다.
        /// 같은 규칙이 두 곳에 살면 재발하므로(사냥터 이격 게이트가 이걸 따로 적었다가 첫 판에서
        /// 전원 0.0m를 냈다) **여기 한 곳**에 두고 부르기만 한다.
        /// </summary>
        public static bool IsWorldScalePlane(Bounds b)
        {
            return b.size.x > WorldScaleSize || b.size.z > WorldScaleSize;
        }

        /// <summary>
        /// 단위 바운드 — **파티클은 뺀다**. 불꽃·연기는 공중에 있는 것이 정상이라
        /// (검수가 명시적으로 허용한 제외) 이것까지 넣으면 화톳불이 땅으로 끌려 내려간다.
        /// </summary>
        public static bool UnitBounds(Transform t, out Bounds bounds)
        {
            bounds = new Bounds();
            bool first = true;
            var rends = t.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] is ParticleSystemRenderer || rends[i] is TrailRenderer || rends[i] is LineRenderer)
                    continue;
                if (first) { bounds = rends[i].bounds; first = false; }
                else bounds.Encapsulate(rends[i].bounds);
            }
            return !first;
        }

        static void Add(List<Transform> list, Transform t)
        {
            if (t.gameObject.activeInHierarchy && !SkipItem(t.name) && HasRenderer(t))
                list.Add(t);
        }

        /// <summary>
        /// 지형·물·관리자·카메라·조명 — 배치물이 아니다.
        /// **이름 「포함」으로 거르지 않는다**(검수 랩 B): `Contains("Light")`는 「StreetLight」·「Lighthouse」처럼
        /// 이름에 그 단어가 든 **배치물을 통째로** 눈 밖으로 내보낸다. 씬 루트는 우리가 만든 것뿐이니
        /// **정확한 이름 목록**으로 적는다 — 새 루트가 생기면 여기 적어야 검사에서 빠진다(빠뜨림이 눈에 띈다).
        /// </summary>
        static readonly string[] WaterRoots = { "SeaWater", "Water", "River", "Lake", "LakeWater", "RiverWater" };

        static readonly string[] SkipRoots =
        {
            "Terrain", "Ground", "Managers", "GameManager", "NetworkManager", "Main Camera", "PlayCamera",
            "Directional Light", "Sun", "Canvas", "EventSystem", "Global Volume", "PostProcessVolume",
        };

        public static bool SkipContainer(string n)
        {
            // **접두사로 거르면 「Watermill」이 물이 된다** — 실제로 그랬다(검수 2026-09-07):
            // 마을 물레방아 두 채가 10m 지하에 묻힌 채 「Water…」라는 이유로 검사 대상 밖이었다.
            // 「이름 포함으로 거르지 마라」의 접두사 판(같은 함정 두 번째). 수면은 **정확한 이름**으로
            // 적고, 이름이 바뀌어도 새는 일이 없게 크기 규칙(월드 규모 판)이 한 번 더 받는다.
            for (int i = 0; i < WaterRoots.Length; i++)
                if (string.Equals(n, WaterRoots[i], StringComparison.Ordinal))
                    return true;
            for (int i = 0; i < SkipRoots.Length; i++)
                if (string.Equals(n, SkipRoots[i], StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>
        /// 방 구조물·뚜껑 — 지하에 묻힌 것이 정상이다.
        ///
        /// **`Dungeon*`는 이제 겹띠다** — 실제 판정은 자리로 한다(`InDungeonRoom`: 방 중심 12m 안 +
        /// 꼭대기가 지표보다 1m 아래). 이름 규약이 바뀌어도 안 새게 성질로 옮겼다(검수 지시 2026-09-07).
        ///
        /// **`CapDress*`는 이름으로 남긴다(사유)**: 뚜껑 장식은 지표 바로 위·아래를 오가는 물건이라
        /// 「자리」만으로는 정상과 결함이 안 갈린다 — 0.2m 솟은 것이 설계이고 0.35m 솟으면 결함이다
        /// (`AssertWorldMaterials`가 그 축을 따로 잰다). 그래서 여기서는 발 높이 판정에서만 빼고,
        /// 높이 판정은 전용 게이트에 맡긴다. 이름으로 빼는 유일하게 남은 자리다.
        /// </summary>
        public static bool SkipItem(string n)
        {
            return n.StartsWith("Dungeon", StringComparison.Ordinal) || n.StartsWith("CapDress", StringComparison.Ordinal);
        }

        /// <summary>
        /// **발 밑 표면**(검수 판정 2026-09-07 ①) — 보수 패스와 게이트가 **같은 자**로 잰다.
        /// 지표 y가 아니라 광선이 맞는 첫 표면이므로 마을·지하 방을 규칙 하나로 다룬다.
        ///
        /// 시작 높이는 **예상 최대 매몰 깊이보다 커야 한다** — 0.6m에서 쏘았더니 1.10m 묻힌 보스의
        /// 광선이 **바닥 밑에서 시작**해 「발 밑에 바닥이 없다」로 읽혔다.
        /// **계측기가 결함보다 얕으면 결함을 못 잰다.** 보스 크기(≤2.6m)를 감안해 2m로 둔다.
        /// </summary>
        public const float SurfaceProbeUp = 2.0f;

        public static bool SurfaceUnder(Transform actor, Bounds b, out float y, out string what)
        {
            y = 0f;
            what = "";
            var from = new Vector3(b.center.x, b.min.y + SurfaceProbeUp, b.center.z);
            var hits = UnityEngine.Physics.RaycastAll(from, Vector3.down, SurfaceProbeUp + 8f, ~0,
                UnityEngine.QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (u, v) => u.distance.CompareTo(v.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null || hits[i].collider.transform.IsChildOf(actor))
                    continue;
                y = hits[i].point.y;
                what = hits[i].collider.transform.root.name + "/" + hits[i].collider.name;
                return true;
            }
            return false;
        }

        /// <summary>
        /// **장비는 발이 아니다** — 무기·망토를 발 높이 계산에 넣으면 「발」이 칼끝·망토 자락이 된다.
        /// 액터 바로 밑까지만 거슬러 올라간다(액터 이름이 우연히 장비 이름을 닮아도 안 걸리게).
        /// </summary>
        public static bool IsGear(Transform actor, Transform t)
        {
            for (var p = t; p != null && p != actor; p = p.parent)
                if (VisualSliceBuilder.IsWeaponName(p.name) || VisualSliceBuilder.IsCapeName(p.name))
                    return true;
            return false;
        }

        /// <summary>액터의 **몸** 바운드(장비 제외).</summary>
        public static bool BodyBounds(Transform actor, out Bounds bounds)
        {
            return WorldBounds(actor, out bounds, t => IsGear(actor, t));
        }

        public static bool HasRenderer(Transform t)
        {
            return t.GetComponentInChildren<Renderer>(false) != null;
        }

        /// <summary>
        /// 월드 바운드 — 메시 정점을 직접 변환한다. 스킨드 메시의 `Renderer.bounds`는 에디터 포즈에서
        /// 부풀어 있어 발 높이가 실제보다 낮게 나오기 때문이다(BossFit과 같은 이유, `BakeMesh(mesh, false)`).
        ///
        /// 정정(2026-09-06): 처음엔 「배치모드에서 `Renderer.bounds`가 프리팹 원점 값으로 남는다」고 적었으나
        /// **틀렸다.** 마을이 y=0으로 읽힌 것은 계측 오류가 아니라 실제로 10m 지하에 묻혀 있었기 때문이다
        /// (이 함수로 옮겼더니 화면에 마을이 돌아왔다 = 두 방식이 같은 값을 봤다는 뜻). 다른 게이트의
        /// `Renderer.bounds` 사용은 이 이유로는 의심할 필요가 없다.
        /// </summary>
        public static bool WorldBounds(Transform t, out Bounds bounds) => WorldBounds(t, out bounds, null);

        /// <param name="skip">
        /// 이 렌더러 오브젝트는 바운드에서 뺀다. **발 높이는 몸으로 재야 한다** — 무기까지 포함하면
        /// 「발」이 사실은 **칼끝**이 되어, 칼끝을 바닥에 대는 순간 발이 떠도 게이트가 0.00m로 읽는다
        /// (검수 의심 「41 검이 바닥을 뚫는다」를 재다 드러난 구멍, 2026-09-07).
        /// </param>
        public static bool WorldBounds(Transform t, out Bounds bounds, System.Func<Transform, bool> skip)
        {
            bounds = new Bounds();
            bool first = true;
            var filters = t.GetComponentsInChildren<MeshFilter>(false);
            for (int i = 0; i < filters.Length; i++)
            {
                var mesh = filters[i].sharedMesh;
                var rend = filters[i].GetComponent<Renderer>();
                if (mesh == null || rend == null || !rend.enabled)
                    continue;
                if (skip != null && skip(filters[i].transform))
                    continue;
                Encapsulate(ref bounds, ref first, mesh.bounds, filters[i].transform.localToWorldMatrix);
            }
            var skins = t.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            for (int i = 0; i < skins.Length; i++)
            {
                if (!skins[i].enabled || skins[i].sharedMesh == null)
                    continue;
                if (skip != null && skip(skins[i].transform))
                    continue;
                var baked = new Mesh();
                skins[i].BakeMesh(baked, false);           // true면 스케일이 두 번 곱해진다
                Encapsulate(ref bounds, ref first, baked.bounds, skins[i].transform.localToWorldMatrix);
                UnityEngine.Object.DestroyImmediate(baked);
            }
            return !first;
        }

        static void Encapsulate(ref Bounds bounds, ref bool first, Bounds local, Matrix4x4 m)
        {
            for (int k = 0; k < 8; k++)
            {
                var c = local.center + Vector3.Scale(local.extents,
                    new Vector3((k & 1) == 0 ? -1f : 1f, (k & 2) == 0 ? -1f : 1f, (k & 4) == 0 ? -1f : 1f));
                var w = m.MultiplyPoint3x4(c);
                if (first) { bounds = new Bounds(w, Vector3.zero); first = false; }
                else bounds.Encapsulate(w);
            }
        }

        /// <summary>이 배치물이 서 있어야 할 높이 — 던전 방 안이면 방 바닥, 아니면 그 자리 지표.</summary>
        public static float ExpectedGroundY(Transform t, Bounds b)
        {
            var interiors = new[]
            {
                (Dungeon1.InteriorObject, Dungeon1.InteriorX, Dungeon1.InteriorZ),
                (Dungeon2.InteriorObject, Dungeon2.InteriorX, Dungeon2.InteriorZ),
                (Dungeon3.InteriorObject, Dungeon3.InteriorX, Dungeon3.InteriorZ),
            };
            for (int i = 0; i < interiors.Length; i++)
            {
                var go = GameObject.Find(interiors[i].Item1);
                if (go != null && (t == go.transform || t.IsChildOf(go.transform)))
                    return TerrainY(interiors[i].Item2, interiors[i].Item3) - WorldTerrain.DungeonDepth + 0.2f;
            }
            return TerrainY(b.center.x, b.center.z);
        }

        public static float TerrainY(float x, float z)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
                return 0f;
            return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
        }

        public static string NodePath(Transform t)
        {
            string p = t.name;
            var q = t.parent;
            while (q != null) { p = q.name + "/" + p; q = q.parent; }
            return p;
        }
    }
}
