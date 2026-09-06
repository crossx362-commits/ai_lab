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
        /// <summary>씬에 놓인 배치물(캐릭터·건물·소품)의 루트들.</summary>
        public static List<Transform> Candidates()
        {
            var list = new List<Transform>();
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                var root = roots[r];
                if (!root.activeInHierarchy || SkipContainer(root.name))
                    continue;
                if (HasRenderer(root.transform))
                {
                    Add(list, root.transform);
                    continue;
                }
                for (int c = 0; c < root.transform.childCount; c++)
                    Add(list, root.transform.GetChild(c));
            }
            return list;
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
        static readonly string[] SkipRoots =
        {
            "Terrain", "Ground", "Managers", "GameManager", "NetworkManager", "Main Camera", "PlayCamera",
            "Directional Light", "Sun", "Canvas", "EventSystem", "Global Volume", "PostProcessVolume",
        };

        public static bool SkipContainer(string n)
        {
            if (n.StartsWith("Sea", StringComparison.Ordinal) || n.StartsWith("Water", StringComparison.Ordinal))
                return true;                                  // 바다·수면은 지표가 아니다
            for (int i = 0; i < SkipRoots.Length; i++)
                if (string.Equals(n, SkipRoots[i], StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>방 구조물·뚜껑 — 지하에 묻힌 것이 정상이다.</summary>
        public static bool SkipItem(string n)
        {
            return n.StartsWith("Dungeon", StringComparison.Ordinal) || n.StartsWith("CapDress", StringComparison.Ordinal);
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
        public static bool WorldBounds(Transform t, out Bounds bounds)
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
                Encapsulate(ref bounds, ref first, mesh.bounds, filters[i].transform.localToWorldMatrix);
            }
            var skins = t.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            for (int i = 0; i < skins.Length; i++)
            {
                if (!skins[i].enabled || skins[i].sharedMesh == null)
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
