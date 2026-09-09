using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class VisualSliceBuilder
    {
        /// <summary>
        /// **나무 색을 가른다**(검수 랩 ⑤ 「초록 한 톤」). 세어 보니 야외 나무 **144그루가 전부 한 색**
        /// (`colormap` 아틀라스 흰 tint)이었다 — 종류를 넷 섞고 크기를 0.65~1.6으로 벌려도 색이 하나면
        /// 화면은 한 덩이 초록이다.
        ///
        /// 왜 tint인가: Kenney 킷 모델은 **UV가 한 점**이라 무늬를 못 받는다(광장 돌포장에서 실측했다).
        /// 그러니 나무는 **색**으로, 지표는 **지형 도포**로 가른다 — 같은 병에 수단이 다르다.
        /// 자리 해시로 고르므로 매 판 같은 그림이다(검수가 같은 화면을 봐야 한다).
        /// </summary>
        public static readonly (string Name, Color Tint, string Why)[] TreeTones =
        {
            ("TreeLush", new Color(1.00f, 1.00f, 1.00f), "본래 잎 — 가장 밝은 초록"),
            // **어두운 쪽은 명도가 아니라 색으로 간다.** (0.44,0.62,0.46)으로 내렸더니 야외 색조 자가
            // 「대낮에 검은 덩어리 0.24(하한 0.30)」로 잡았다 — 나무 텍스처 자체가 luma 0.44라 tint를
            // 0.55 아래로 내리면 곧장 하한을 깬다. 그래서 짙은 쪽은 **청록으로 틀어** 갈린 티를 낸다.
            ("TreeDeep", new Color(0.58f, 0.80f, 0.66f), "그늘에 든 푸른 수관"),
            ("TreeDry", new Color(1.00f, 0.76f, 0.34f), "볕에 마른 누런 잎"),
            ("TreeYoung", new Color(0.72f, 1.00f, 0.40f), "어린 연둣빛"),
        };

        internal static bool IsTreeName(string n) =>
            n != null && n.IndexOf("tree", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// 자리로 정하는 색 — 같은 자리는 늘 같은 색이다(난수 씨앗을 따로 두지 않는다).
        /// **그루마다 따로 굴리지 않는다.** 첫 판이 그랬더니 네 색이 뒤섞여 눈에서 평균이 나
        /// 화면은 여전히 한 톤이었다(픽셀로 셌다: 색조 분포만 넓어지고 갈린 티는 안 났다).
        /// 실제 숲도 수종이 **군락**으로 뭉친다 — 그러니 9m 격자로 자리를 묶어 한 덩이를 한 색으로 칠한다.
        /// </summary>
        /// 칸 크기는 **군락 크기에서 유도한다**: 숲 군락은 중심에서 0.6~5.2m 안에 심긴다(`BuildForest`).
        /// 9m로 잡았더니 숲 전체가 두어 칸에 들어가 **2색·한 색 76%**로 자가 울었다 — 칸이 군락보다
        /// 크면 「군락별 색」이 아니라 「숲 전체 한 색」이 된다.
        /// 5m(군락과 같은 크기)에서는 한 군락이 통째로 한 색이 돼 **한 색이 51%**를 먹었고, 누런 잎
        /// 군락은 화면 밖에만 섰다. 군락보다 **조금 작게** 잡아 한 군락 안에서도 두어 색이 섞이게 한다.
        const float TreeToneCell = 4.2f;

        internal static int TreeToneIndex(Vector3 p)
        {
            int cx = Mathf.FloorToInt(p.x / TreeToneCell);
            int cz = Mathf.FloorToInt(p.z / TreeToneCell);
            // **하위 비트로 나누면 색이 접힌다.** 곱하고 XOR한 값을 그대로 4로 나눴더니 하위 2비트만
            // 결정에 쓰여 숲이 통째로 **2색**이 됐다(자가 두 판 연속 잡았다). 섞은 뒤 **상위 비트**를 쓴다 —
            // 이 분포는 유니티를 돌리기 전에 같은 식으로 미리 세어 확인했다(전역 421/408/415/437).
            uint u = (uint)(cx * 73856093) ^ (uint)(cz * 19349663);
            u ^= u >> 13;
            u *= 2654435761u;
            u ^= u >> 16;
            return (int)((u >> 8) % (uint)TreeTones.Length);
        }

        /// <summary>
        /// 씬의 나무를 네 색으로 갈라 칠한다. **멱등**이다 — 이미 칠해진 나무도 같은 색으로 다시 칠한다
        /// (셀프체크가 매 판 씬을 되돌리므로 보수 패스는 몇 번 불려도 같은 결과여야 한다).
        /// 저장 **앞**에서 불러야 한다 — 저장 뒤면 자는 초록인데 QA 샷은 옛 색을 찍는다(2026-09-09 사고).
        /// </summary>
        public static void SpreadTreeTones()
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Game/Art/Env"));
            var mats = new Material[TreeTones.Length];
            int painted = 0;
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (!IsTreeName(t.name) || (t.parent != null && IsTreeName(t.parent.name)))
                    continue;                       // 나무 하나당 한 번 — 자식 조각까지 세지 않는다
                var rends = t.GetComponentsInChildren<Renderer>(true);
                if (rends.Length == 0)
                    continue;
                Texture tex = null;
                for (int r = 0; r < rends.Length && tex == null; r++)
                    if (rends[r].sharedMaterial != null)
                        tex = rends[r].sharedMaterial.mainTexture;
                if (tex == null)
                    continue;                       // 텍스처를 못 찾으면 칠하지 않는다(민무늬로 만들지 않는다)
                int k = TreeToneIndex(t.position);
                if (mats[k] == null)
                    mats[k] = EnsureTreeToneMat(k, tex);
                for (int r = 0; r < rends.Length; r++)
                    rends[r].sharedMaterial = mats[k];
                painted++;
            }
            // **재질은 에셋이다 — 저장하지 않으면 디스크에는 흰 기본값이 남는다.**
            // 첫 판이 그랬다: 씬은 새 재질을 가리키고 자는 「4색」으로 초록이었는데, QA 샷 프로세스가
            // 읽은 디스크 재질은 아직 안 칠해져 있어 **화면은 한 톤 그대로**였다(지형 TerrainData와 같은 함정).
            AssetDatabase.SaveAssets();
            Debug.Log("[Ulon] 나무 색 가르기 — " + painted + "그루를 " + TreeTones.Length + "색으로 칠했습니다.");
        }

        static Material EnsureTreeToneMat(int k, Texture tex)
        {
            string path = "Assets/Game/Art/Env/" + TreeTones[k].Name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.mainTexture = tex;
            mat.color = TreeTones[k].Tint;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
