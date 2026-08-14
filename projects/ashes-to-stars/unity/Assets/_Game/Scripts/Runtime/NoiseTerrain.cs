using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 노이즈맵 기반 지형 생성 (✅ 오너 결정 2026-08-13 — "바닥이랑 지형만 노이즈맵으로 터레인 생성")
    ///
    /// 왜 지형만 절차 생성인가:
    ///   캐릭터·몬스터는 픽셀아트로 손수 그리지만, 지형까지 손으로 그리면
    ///   오픈월드(§6) + 랜덤 던전(§7) + 티어 10단계를 감당할 수 없다.
    ///   지형은 **규칙만 정하면 무한히 뽑히는** 영역이라 절차 생성이 정확히 맞는다.
    ///
    /// 무엇을 노이즈로 정하는가:
    ///   ① 바닥 색조 — 같은 텍스처를 쓰되 노이즈로 톤을 흔들어 "같은 타일 반복" 느낌을 없앤다
    ///   ② 지형 구역 — 노이즈 임계로 초지/바위/모래 같은 구역을 나눈다
    ///   ③ ~~장애물 배치~~ → **`FieldDecor`로 이관**(2026-08-14). 같은 노이즈를 보되
    ///      아틀라스·배칭·엄폐물 등록까지 거기서 함께 처리한다. 노이즈 규칙은 `TerrainNoise` 공용.
    ///
    /// 성능 원칙(§10-9): 바닥은 **메시 1장 + 정점 색**으로 처리해 드로우콜 1을 유지한다.
    /// 타일을 수백 장 깔면 잡몹 500체 예산을 바닥이 먹는다.
    /// </summary>
    public class NoiseTerrain : MonoBehaviour
    {
        [Header("범위")]
        public float 반경 = 40f;
        [Tooltip("정점 간격 — 작을수록 색 변화가 곱지만 정점이 늘어난다")]
        public float 격자간격 = 2f;

        [Header("노이즈")]
        [Tooltip("같은 시드 = 같은 지형. 노이즈 계수는 TerrainNoise가 갖는다 — " +
                 "여기서 따로 노출하면 프랍 배치와 다른 값이 되어 두 규칙이 어긋난다")]
        public int 시드 = 0;

        [Header("구역 색 (노이즈 임계로 갈린다)")]
        public Color 저지대 = new Color(0.16f, 0.22f, 0.15f);
        public Color 초지 = new Color(0.22f, 0.30f, 0.18f);
        public Color 마른땅 = new Color(0.30f, 0.28f, 0.19f);
        public Color 바위 = new Color(0.26f, 0.26f, 0.28f);

        // ⚠️ 「장애물」 필드 4종(임계·상한·스프라이트·머티리얼)을 제거했다(2026-08-14).
        //    인스펙터에 보이는데 아무도 안 채워서 배치가 **한 번도 돈 적이 없었다**.
        //    보이는 설정은 "쓰이는 설정"이어야 한다 — 안 그러면 다음 사람이 채우고
        //    동작을 기대하다 조용히 배신당한다. 배치는 이제 FieldDecor 하나가 맡는다.

        [Header("쿼터뷰")]
        public float ISO_Y = 0.5f;

        float _ox, _oy;

        void Awake() => Build();

        public void Build()
        {
            var rng = new System.Random(시드);
            _ox = (float)rng.NextDouble() * 10000f;
            _oy = (float)rng.NextDouble() * 10000f;

            BuildGroundMesh();
            // 프랍 배치는 여기서 하지 않는다 — `FieldDecor`가 같은 노이즈(TerrainNoise)로
            // 심고 아틀라스까지 묶어 배칭을 지킨다(2026-08-14 통합).
            //   구 `ScatterProps`는 `장애물스프라이트`를 인스펙터로 채워야 했는데
            //   아무도 채우지 않아 **한 번도 실행된 적이 없었다** — 살아 있는 줄 알았던
            //   죽은 경로가 랜덤 배치(FieldDecor)와 공존하며 "노이즈로 심고 있다"는
            //   착각을 만들었다. 지우는 것이 수리다.
        }

        /// <summary>노이즈 값 0~1 — 바닥 색조와 프랍 배치가 **같은 규칙**을 본다.</summary>
        public float Sample(float x, float y)
            => TerrainNoise.Sample(new Vector2(_ox, _oy), x, y);

        Color ZoneColor(float n)
        {
            if (n < 0.38f) return Color.Lerp(저지대, 초지, n / 0.38f);
            if (n < 0.62f) return Color.Lerp(초지, 마른땅, (n - 0.38f) / 0.24f);
            return Color.Lerp(마른땅, 바위, Mathf.Clamp01((n - 0.62f) / 0.30f));
        }

        /// <summary>
        /// 바닥을 격자 메시 한 장으로 만들고 **정점 색**에 노이즈를 굽는다.
        /// 타일 스프라이트를 수백 장 까는 대신 드로우콜 1로 끝난다.
        /// </summary>
        void BuildGroundMesh()
        {
            int n = Mathf.Max(2, Mathf.RoundToInt(반경 * 2f / 격자간격));
            var verts = new Vector3[(n + 1) * (n + 1)];
            var cols = new Color[verts.Length];
            var uvs = new Vector2[verts.Length];
            var tris = new List<int>(n * n * 6);

            for (int y = 0; y <= n; y++)
                for (int x = 0; x <= n; x++)
                {
                    float wx = -반경 + x * 격자간격;
                    float wy = -반경 + y * 격자간격;
                    int i = y * (n + 1) + x;
                    verts[i] = new Vector3(wx, wy * ISO_Y, 0f);   // 쿼터뷰 압축
                    cols[i] = ZoneColor(Sample(wx, wy));
                    uvs[i] = new Vector2(wx / 6f, wy / 6f);       // 텍스처는 6m마다 반복
                }

            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = y * (n + 1) + x;
                    tris.Add(i); tris.Add(i + n + 1); tris.Add(i + 1);
                    tris.Add(i + 1); tris.Add(i + n + 1); tris.Add(i + n + 2);
                }

            var mesh = new Mesh { name = "NoiseGround" };
            mesh.indexFormat = verts.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.colors = cols;
            mesh.uv = uvs;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            var go = new GameObject("Ground");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0, 0, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            var tex = Resources.Load<Texture2D>("ground/field_plain_albedo");
            // 정점 색을 곱하는 셰이더가 필요하다 — Sprites/Default가 정점 색을 반영한다
            var mat = new Material(Shader.Find("Sprites/Default"));
            if (tex != null) { tex.wrapMode = TextureWrapMode.Repeat; mat.mainTexture = tex; }
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

    }
}
