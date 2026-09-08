using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ulon.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 성능 **계측만** 한다(검수 2026-09-07: 숫자를 먼저 보고 상한을 정한다 — 상한을 먼저 정하면
    /// 그건 근거 없는 숫자다). 게이트도, 상한도 여기 없다. 보고서는 `docs/PERF_BASELINE.md`.
    ///
    /// 재는 것: 지점마다 반경 안의 **렌더러 수·삼각형 수·MeshCollider 수·머티리얼 종류 수**(드로우콜 추정).
    /// 개수는 어디까지나 **대리 지표**다 — 진짜 지표는 프레임 시간이고, 그 측정 가능성은 보고서 끝에 적는다.
    /// </summary>
    public static class PerfReport
    {
        public struct Spot
        {
            public string Name;
            public Vector3 Center;
            public float Radius;
        }

        public struct Count
        {
            public int Renderers;
            public long Triangles;
            public int MeshColliders;
            public int OtherColliders;
            public int Materials;         // **에셋 한 벌** 기준 종류 수(자는 MaterialKey)
            public int MaterialInstances; // 참고용 — 옛 자(인스턴스 개수). 게이트는 이 값을 안 쓴다.
            public int Skinned;
        }

        [MenuItem("Ulon/Perf Report")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Bootstrap.unity");

            var spots = Spots();

            var sb = new StringBuilder();
            sb.AppendLine("# 성능 기준 실측 (Ulon)");
            sb.AppendLine();
            sb.AppendLine("측정: `bash tools/perf_report.sh` (배치모드, 씬 Bootstrap). 게이트 없음 — **숫자를 먼저 본다**.");
            sb.AppendLine("개수는 대리 지표다. 진짜 지표(프레임 시간) 측정 가능성은 맨 아래에 적는다.");
            sb.AppendLine();
            sb.AppendLine("| 지점 | 렌더러 | 삼각형 | 머티리얼 종류 | MeshCollider | 그 외 콜라이더 | SkinnedMesh | 렌더 제출(ms, **대리 지표**) |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");

            for (int i = 0; i < spots.Length; i++)
            {
                var c = Measure(spots[i]);
                float ms = RenderMs(spots[i]);
                sb.AppendLine("| " + spots[i].Name + " | " + c.Renderers + " | " + c.Triangles.ToString("N0") +
                              " | " + c.Materials + " | " + c.MeshColliders + " | " + c.OtherColliders + " | " + c.Skinned +
                              " | " + ms.ToString("0.0") + " |");
                Debug.Log("[Ulon] 성능 " + spots[i].Name + " — 렌더러 " + c.Renderers + ", 삼각형 " + c.Triangles +
                          ", 머티리얼 " + c.Materials + ", MeshCollider " + c.MeshColliders +
                          ", 그 외 콜라이더 " + c.OtherColliders + ", Skinned " + c.Skinned +
                          ", 렌더 제출 " + ms.ToString("0.0") + "ms");
            }

            sb.AppendLine();
            sb.AppendLine("## 움직이는 물체의 MeshCollider");
            sb.AppendLine();
            var movers = MovingMeshColliders();
            if (movers.Count == 0)
                sb.AppendLine("없음 — MeshCollider는 전부 정적 구조물·소품에 붙어 있다(바꿀 대상 없음).");
            else
                foreach (var m in movers)
                    sb.AppendLine("- " + m);
            Debug.Log("[Ulon] 움직이는 물체의 MeshCollider " + movers.Count + "개");

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "PERF_BASELINE.md");
            string old = File.Exists(path) ? File.ReadAllText(path) : "";
            int keep = old.IndexOf("## 프레임 시간", StringComparison.Ordinal);
            if (keep >= 0)
                sb.Append("\n").Append(old.Substring(keep));      // 손으로 적은 조사 결과는 보존한다
            File.WriteAllText(path, sb.ToString());
            Debug.Log("[Ulon] 성능 보고서 — " + path);
        }

        /// <summary>재는 지점 — 보고서와 회귀 경보 게이트가 **같은 목록**을 쓴다(다른 자를 쓰면 비교가 무의미하다).</summary>
        public static Spot[] Spots()
        {
            float floor1 = GroundY(Dungeon1.InteriorX, Dungeon1.InteriorZ) - VisualSliceBuilder.DungeonDepth;
            return new[]
            {
                new Spot { Name = "던전 1 방(실내 1개)", Center = new Vector3(Dungeon1.InteriorX, floor1 + 1f, Dungeon1.InteriorZ), Radius = 12f },
                new Spot { Name = "마을 광장(반경 40m)", Center = new Vector3(0f, WorldTerrain.LandBase, 0f), Radius = 40f },
                new Spot { Name = "월드 조망(반경 200m)", Center = new Vector3(0f, WorldTerrain.LandBase, 0f), Radius = 200f },
            };
        }

        /// <summary>
        /// `Camera.Render()`를 여러 번 불러 **CPU 렌더 제출 시간**의 중앙값을 잰다.
        /// 이것은 프레임 시간이 **아니다** — GPU 대기·물리·스크립트·VSync가 빠져 있다.
        /// 진짜 프레임 시간 측정 경로는 보고서 「프레임 시간」 절에 따로 적는다.
        /// </summary>
        static float RenderMs(Spot spot)
        {
            var camGo = new GameObject("PerfProbeCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.farClipPlane = Mathf.Max(150f, spot.Radius * 2f);
            camGo.transform.position = spot.Center + new Vector3(spot.Radius * 0.6f, spot.Radius * 0.5f, spot.Radius * 0.6f);
            camGo.transform.LookAt(spot.Center);
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var times = new List<double>();
            try
            {
                cam.targetTexture = rt;
                cam.Render();                       // 첫 렌더는 셰이더 준비가 섞인다 — 버린다
                var sw = new System.Diagnostics.Stopwatch();
                for (int i = 0; i < 9; i++)
                {
                    sw.Restart();
                    cam.Render();
                    sw.Stop();
                    times.Add(sw.Elapsed.TotalMilliseconds);
                }
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(rt);
            }
            times.Sort();
            return (float)times[times.Count / 2];
        }

        /// <summary>
        /// 반경 안의 렌더러·삼각형·머티리얼·콜라이더를 센다.
        /// 위치 판정은 `Renderer.bounds`가 아니라 **transform.position**으로 한다 —
        /// 배치모드에서 bounds가 프리팹 원점 값으로 남는 함정이 있었다(2026-09-06 원장).
        /// </summary>
        /// <summary>직전 `Measure`가 센 머티리얼 이름 — 경보가 났을 때 원인을 대기 위한 것.</summary>
        public static readonly System.Collections.Generic.List<string> LastMaterialNames = new System.Collections.Generic.List<string>();

        /// <summary>
        /// 머티리얼 「종류」의 **자**(검수 판정 2026-09-08): 인스턴스 개수가 아니라 **에셋 한 벌**로 센다.
        /// 왜: 옛 자는 `HashSet&lt;Material&gt;`(인스턴스 동일성)이라, 같은 에셋을 가리키는 사본이
        /// 늘기만 해도 숫자가 올라갔다 — 실측에서 `colormap` 34개·`grass` 7개·`dirt` 3개가 그렇게 세어졌다.
        /// 그건 「무거워졌다」가 아니라 **같은 것을 여러 번 셌다**는 뜻이다.
        /// 「에셋 경로+이름」으로 먼저 재 봤지만 숫자가 그대로 84였다 — 34개 `colormap`은 **34개의 서로 다른
        /// FBX가 각자 품고 있는 같은 재질**이라 경로가 다 다르기 때문이다. 그래서 자를 한 번 더 바꿨다:
        /// 키는 **재질이 그리는 그림**(셰이더 + 메인 텍스처 에셋 + 색)이다. Kenney 킷 33개 프롭이
        /// 같은 `colormap.png`를 같은 셰이더로 그리면 그건 **한 종류**다.
        /// 한계는 선언한다: 광택·타일링만 다른 두 재질은 한 종류로 센다(그림이 사실상 같다).
        /// 텍스처가 없는 재질은 색으로 갈린다. 이 자는 **인스턴스 수를 안 본다** — 사본이 늘어도 안 는다.
        /// </summary>
        public static string MaterialKey(Material m)
        {
            if (m == null)
                return "null";
            string shader = m.shader != null ? m.shader.name : "(no shader)";
            var tex = m.mainTexture;
            string texKey = tex != null ? UnityEditor.AssetDatabase.GetAssetPath(tex) : "(no tex)";
            if (tex != null && string.IsNullOrEmpty(texKey))
                texKey = "mem:" + tex.name;
            string col = m.HasProperty("_Color") ? ColorUtility.ToHtmlStringRGBA(m.color) : "-";
            return shader + "|" + texKey + "|" + col;
        }

        public static Count Measure(Spot spot)
        {
            var c = new Count();
            var mats = new HashSet<Material>();
            var matKeys = new HashSet<string>();
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r.enabled) continue;
                if ((r.transform.position - spot.Center).magnitude > spot.Radius) continue;
                c.Renderers++;
                if (r is SkinnedMeshRenderer smr)
                {
                    c.Skinned++;
                    c.Triangles += Tris(smr.sharedMesh);
                }
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf != null) c.Triangles += Tris(mf.sharedMesh);
                }
                var shared = r.sharedMaterials;
                for (int m = 0; m < shared.Length; m++)
                    if (shared[m] != null) { mats.Add(shared[m]); matKeys.Add(MaterialKey(shared[m])); }
            }
            c.Materials = matKeys.Count;
            c.MaterialInstances = mats.Count;
            // **넘었을 때 「무엇이 늘었나」를 말할 수 있어야 한다** — 숫자만 남기면 다음 사람이
            // 원인을 못 찾고 상한부터 올린다(그게 자를 헐겁게 하는 길이다).
            LastMaterialNames.Clear();
            foreach (var k in matKeys)
                LastMaterialNames.Add(k);
            LastMaterialNames.Sort(System.StringComparer.Ordinal);

            var cols = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < cols.Length; i++)
            {
                if ((cols[i].transform.position - spot.Center).magnitude > spot.Radius) continue;
                if (cols[i] is MeshCollider) c.MeshColliders++;
                else c.OtherColliders++;
            }
            return c;
        }

        static long Tris(Mesh mesh)
        {
            if (mesh == null) return 0;
            long n = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
                n += (long)(mesh.GetIndexCount(s) / 3);
            return n;
        }

        /// <summary>움직이는(리지드바디·캐릭터 컨트롤러가 붙은) 것에 달린 MeshCollider — 규격상 Box/Capsule로 가야 할 대상.</summary>
        static List<string> MovingMeshColliders()
        {
            var list = new List<string>();
            var mcs = UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < mcs.Length; i++)
            {
                var t = mcs[i].transform;
                bool moving = mcs[i].GetComponentInParent<Rigidbody>() != null
                              || mcs[i].GetComponentInParent<CharacterController>() != null;
                if (moving)
                    list.Add(t.root.name + "/" + t.name);
            }
            return list;
        }

        static float GroundY(float x, float z)
        {
            if (Physics.Raycast(new Vector3(x, 500f, z), Vector3.down, out RaycastHit hit, 1000f))
                return hit.point.y;
            return WorldTerrain.LandBase;
        }
    }
}
