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
    ///   ③ 장애물 배치 — 노이즈 봉우리에 바위·덤불을 심는다. 위치 선정(§10-2)이 의미를 갖는 엄폐물
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
        public int 시드 = 0;
        [Tooltip("큰 지형 덩어리 크기")] public float 대형스케일 = 0.035f;
        [Tooltip("잔무늬 크기")] public float 소형스케일 = 0.16f;
        [Range(0f, 1f)] public float 잔무늬비중 = 0.35f;

        [Header("구역 색 (노이즈 임계로 갈린다)")]
        public Color 저지대 = new Color(0.16f, 0.22f, 0.15f);
        public Color 초지 = new Color(0.22f, 0.30f, 0.18f);
        public Color 마른땅 = new Color(0.30f, 0.28f, 0.19f);
        public Color 바위 = new Color(0.26f, 0.26f, 0.28f);

        [Header("장애물")]
        [Tooltip("이 값을 넘는 노이즈 봉우리에 장애물을 심는다")]
        [Range(0.5f, 0.95f)] public float 장애물임계 = 0.78f;
        public int 장애물상한 = 120;
        public Sprite[] 장애물스프라이트;
        public Material 스프라이트머티리얼;

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
            ScatterProps(rng);
        }

        /// <summary>노이즈 값 0~1 — 대형 지형 + 잔무늬를 섞는다</summary>
        public float Sample(float x, float y)
        {
            float big = Mathf.PerlinNoise(_ox + x * 대형스케일, _oy + y * 대형스케일);
            float small = Mathf.PerlinNoise(_ox + 100f + x * 소형스케일, _oy + 100f + y * 소형스케일);
            return Mathf.Clamp01(big * (1f - 잔무늬비중) + small * 잔무늬비중);
        }

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

        /// <summary>노이즈 봉우리에 장애물을 심는다 — 위치 선정(§10-2)이 의미를 갖는 엄폐물</summary>
        void ScatterProps(System.Random rng)
        {
            if (장애물스프라이트 == null || 장애물스프라이트.Length == 0) return;

            var root = new GameObject("Props").transform;
            root.SetParent(transform, false);
            int placed = 0;
            float step = 격자간격 * 1.5f;

            for (float y = -반경; y <= 반경 && placed < 장애물상한; y += step)
                for (float x = -반경; x <= 반경 && placed < 장애물상한; x += step)
                {
                    if (x * x + y * y > 반경 * 반경) continue;
                    if (Sample(x, y) < 장애물임계) continue;
                    if (rng.NextDouble() > 0.45) continue;          // 봉우리마다 다 심으면 벽이 된다

                    float jx = x + (float)(rng.NextDouble() - 0.5) * step;
                    float jy = y + (float)(rng.NextDouble() - 0.5) * step;

                    var go = new GameObject("prop", typeof(SpriteRenderer));
                    go.transform.SetParent(root, false);
                    go.transform.position = new Vector3(jx, jy * ISO_Y, 0f);
                    var sr = go.GetComponent<SpriteRenderer>();
                    sr.sprite = 장애물스프라이트[rng.Next(장애물스프라이트.Length)];
                    if (스프라이트머티리얼 != null) sr.sharedMaterial = 스프라이트머티리얼;
                    sr.sortingOrder = (int)(-jy * 16f);
                    go.transform.localScale = Vector3.one * (1.4f + (float)rng.NextDouble() * 0.8f);
                    placed++;
                }
        }
    }
}
