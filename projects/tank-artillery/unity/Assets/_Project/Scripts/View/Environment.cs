// 환경 — 구름·원경 산맥·태양. 전부 코드로 만든다(에셋 없음).
//
// 출처(조사 2026-09-17, 나무위키 「포트리스2/맵」): 원작 맵에는 **항상 큰 배경이 자리했다** —
// 멀리 보이는 스핑크스(The Sphinx), 거대한 달(The Night), 전함 4대(The Yamato Ship),
// 움직이는 용광로(The Factory), 하늘을 나는 고래(The Whale Ship).
// 즉 전장 뒤에 "여기가 어디인지" 말해 주는 실루엣이 있었다. 빈 하늘은 원작에도 없다.
//
// ⚠️ 전부 **콜라이더 없음**(지형·Scatter 와 같은 판단, §7-5). 배경은 탄도에 절대 끼어들지 않는다.
// ⚠️ 맵 밖에 둔다 — 포탄이 닿는 범위(MapSize) 안에 배경을 세우면 조준을 가린다.

using UnityEngine;
using Tankfall.Sim;

namespace Tankfall.View
{
    public sealed class Environment : MonoBehaviour
    {
        Transform _clouds, _ridges, _sun, _apron;

        /// <summary>
        /// 바람에 흐르는 구름. 탄도를 가장 크게 흔드는 변수인데 **세계 안에 존재감이 0 이었다** —
        /// 세기 10 의 바람이 불어도 움직이는 건 눈 파티클뿐이고 구름 16개는 못 박혀 있었다(HUD 화살표만).
        /// 게임 규칙에는 영향이 없다(순수 연출) — 탄도는 `Sim` 이 `_wind` 로 따로 계산한다.
        /// </summary>
        Vector3 _windDrift;
        float _mapSpan;

        /// <summary>전투가 매 라운드 바람을 굴릴 때 불러준다. x·z 는 `_wind` 와 같은 축이다.</summary>
        public void SetWind(float wx, float wz) => _windDrift = new Vector3(wx, 0f, wz);

        void Update()
        {
            if (_clouds == null || _mapSpan <= 0f) return;
            // 구름은 실제 바람보다 **천천히** 흐른다 — 같은 속도면 하늘이 급류처럼 보인다.
            _clouds.position += _windDrift * (Time.deltaTime * 0.55f);
            // 맵 폭의 1.7배를 넘어가면 반대편으로 되돌린다(구름을 새로 만들지 않고 감는다).
            float lim = _mapSpan * 1.7f;
            var p = _clouds.position;
            if (Mathf.Abs(p.x) > lim) p.x -= Mathf.Sign(p.x) * lim * 2f;
            if (Mathf.Abs(p.z) > lim) p.z -= Mathf.Sign(p.z) * lim * 2f;
            _clouds.position = p;
        }

        /// <summary>맵 밖 바닥판(Apron)의 윗면 높이. 갤러리처럼 지형을 끈 채 뭔가를 세울 때는 이 높이에 둬야 판 밑에 안 묻힌다.</summary>
        public float ApronTopY { get; private set; }

        public void Build(float mapSize, in MapTheme theme, int seed, Vector3 sunDir, float groundY)
        {
            Clear();
            var rng = new Rng((uint)(seed * 2654435761u + 17u));

            BuildApron(mapSize, theme, groundY);
            _mapSpan = mapSize;
            BuildClouds(mapSize, theme, ref rng);
            BuildRidges(mapSize, theme, ref rng);
            BuildSun(mapSize, theme, sunDir);
        }

        /// <summary>
        /// 맵 밖 평지. 플레이 영역(SDF 볼륨)은 200m 에서 **칼로 자른 듯 끊긴다** —
        /// 2D 였던 원작은 화면 밖이라 문제가 안 됐지만 3D 에서는 지형이 허공에 뜬 판때기로 보인다.
        /// 넓은 바닥을 깔아 시선이 원경 산맥까지 이어지게 한다.
        ///
        /// ⚠️ 콜라이더도 없고 SDF 에도 없다 — 순수 배경이다. 여기로 주행하거나 포탄이 닿을 일은 없다
        ///    (이동은 SDF 접지, 착탄은 SDF 레이마칭이 판정한다, §7-5).
        /// </summary>
        void BuildApron(float mapSize, in MapTheme theme, float groundY)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Apron";
            Destroy(go.GetComponent<Collider>());
            // Plane 프리미티브는 한 변이 10 단위다. 맵의 9배 폭으로 깐다.
            go.transform.localScale = Vector3.one * (mapSize * 0.9f);
            // 플레이 지형보다 살짝 낮게 — 같은 높이면 경계에서 z-fighting 이 난다.
            go.transform.position = new Vector3(mapSize * 0.5f, groundY - 0.35f, mapSize * 0.5f);

            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // 플레이 영역보다 어둡게 — "여기는 전장이 아니다"가 색으로 읽혀야 한다.
            mr.sharedMaterial = Lit(Color.Lerp(theme.Mid, theme.Fog, 0.35f) * 0.82f);
            _apron = go.transform;
            ApronTopY = groundY - 0.35f;
        }

        /// <summary>구름 — 납작한 구 덩어리. 하늘이 비어 있으면 높이 감각이 없어진다.</summary>
        void BuildClouds(float mapSize, in MapTheme theme, ref Rng rng)
        {
            _clouds = new GameObject("Clouds").transform;
            _clouds.SetParent(transform, false);
            var mat = Unlit(Color.Lerp(Color.white, theme.SkyBottom, 0.18f));

            for (int i = 0; i < 16; i++)
            {
                var cloud = new GameObject("Cloud").transform;
                cloud.SetParent(_clouds, false);
                cloud.position = new Vector3(
                    (rng.Float01() - 0.5f) * mapSize * 3.4f + mapSize * 0.5f,
                    90f + rng.Float01() * 70f,
                    (rng.Float01() - 0.5f) * mapSize * 3.4f + mapSize * 0.5f);

                int puffs = 3 + (int)(rng.Float01() * 3f);
                for (int j = 0; j < puffs; j++)
                {
                    var p = Prim(PrimitiveType.Sphere, mat, cloud);
                    float s = 18f + rng.Float01() * 22f;
                    p.localScale = new Vector3(s, s * 0.42f, s * 0.75f);
                    p.localPosition = new Vector3((j - puffs * 0.5f) * s * 0.55f, rng.Float01() * s * 0.10f, rng.Float01() * s * 0.2f);
                }
            }
        }

        /// <summary>
        /// 원경 산맥 — 맵 바깥을 둘러싸는 실루엣. 안개 색에 가깝게 칠해 "멀다"를 색으로 말한다.
        /// 원작의 큰 배경 구조물이 하던 역할(여기가 어디인지)을 지형으로 대신한다.
        /// </summary>
        void BuildRidges(float mapSize, in MapTheme theme, ref Rng rng)
        {
            _ridges = new GameObject("Ridges").transform;
            _ridges.SetParent(transform, false);

            // 두 겹: 가까운 능선(진함) + 먼 능선(옅음). 겹치면 깊이가 생긴다.
            //
            // ⚠️ 크기를 키우지 마라(2026-09-17 실측). 반경 300/450 에 높이 58~120(×1.32)·폭 110~250 으로 뒀더니
            //    지상 카메라(FOV 60, 수평선 위 ~30°)에서 봉우리 꼭대기가 화면 위 끝(atan(180/300)≈31°)까지 올라와
            //    **하늘이 한 뼘도 안 보였다** — 스크린샷 전부 상단 절반이 회색 판이었고 SkyGradient 는 타이틀 구석에만 남았다.
            //    지평선 위 각도로 계산한다: 봉우리 꼭대기가 수평선 위 **8~12°** 를 넘지 않게(≈ 높이/거리 ≤ 0.2).
            for (int layer = 0; layer < 2; layer++)
            {
                float radius = 380f + layer * 140f;
                // ⚠️ 두 번 틀린 자리다. 지형 색에 가까우면 배경이 **하늘에 떠 보이고**,
                //    안개 색에 너무 가까우면 **하늘에 묻혀 아예 안 보인다**(0.72/0.88 로 뒀다가 사라졌다).
                //    선형 안개(220~620m)가 이 거리에서 40~75% 를 더 섞으므로 기본 색은 이 정도로 남긴다.
                var col = Color.Lerp(theme.RockDark, theme.Fog, layer == 0 ? 0.52f : 0.68f);   // 회색 돌덩이가 아니라 "먼 산"으로 — 안개 쪽으로 한 단 더
                var mat = Unlit(col);
                int n = 34 + layer * 10;
                for (int i = 0; i < n; i++)
                {
                    float a = (i / (float)n) * Mathf.PI * 2f + rng.Float01() * 0.12f;
                    var peak = Prim(PrimitiveType.Cube, mat, _ridges);
                    // 낮게. 지평선 위로 뾰족하게 솟으면 전장보다 배경이 먼저 눈에 든다.
                    // 근경 h≤66 → 꼭대기(회전 포함 ≈ h*0.32+h*0.5+w*0.3 ≈ 100m)/380m ≈ 15° 상한. 원경은 안개가 지운다.
                    float h = (26f + rng.Float01() * 40f) * (layer == 0 ? 1f : 1.35f);
                    float w = 70f + rng.Float01() * 90f;
                    peak.position = new Vector3(
                        mapSize * 0.5f + Mathf.Cos(a) * radius,
                        h * 0.32f,
                        mapSize * 0.5f + Mathf.Sin(a) * radius);
                    peak.localScale = new Vector3(w, h, w * 0.7f);
                    // 45° 회전한 상자 = 각진 봉우리. 원뿔 프리미티브가 없어서 쓰는 수법이다.
                    peak.localRotation = Quaternion.Euler(0f, a * Mathf.Rad2Deg + 45f, 40f + rng.Float01() * 8f);
                }
            }
        }

        /// <summary>태양(눈 날씨면 흐린 해). 광원 방향과 같은 자리에 둬야 그림자 방향과 안 어긋난다.</summary>
        void BuildSun(float mapSize, in MapTheme theme, Vector3 sunDir)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "SunDisc";
            Destroy(go.GetComponent<Collider>());
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Unlit(Color.Lerp(theme.Sun, Color.white, 0.5f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(mapSize * 0.5f, 0f, mapSize * 0.5f) - sunDir * 620f;
            go.transform.localScale = Vector3.one * 74f;
            _sun = go.transform;
        }

        static Transform Prim(PrimitiveType t, Material m, Transform parent)
        {
            var go = GameObject.CreatePrimitive(t);
            Destroy(go.GetComponent<Collider>());       // ⚠️ 배경은 탄도에 끼어들지 않는다(머리말)
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>배경은 조명을 받지 않는다 — 해가 어디 있든 실루엣 색이 그대로여야 "멀다"가 유지된다.</summary>
        /// <summary>바닥은 조명을 받아야 한다 — 안 받으면 플레이 지형과 밝기가 어긋나 단이 진다.</summary>
        static Material Lit(Color c)
        {
            var sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            var m = new Material(sh) { color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.02f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return m;
        }

        static Material Unlit(Color c)
        {
            var sh = Shader.Find("Unlit/Color") ?? Shader.Find("Legacy Shaders/Diffuse") ?? Shader.Find("Standard");
            var m = new Material(sh) { color = c };
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            return m;
        }

        public void Clear()
        {
            if (_clouds != null) Destroy(_clouds.gameObject);
            if (_ridges != null) Destroy(_ridges.gameObject);
            if (_sun != null) Destroy(_sun.gameObject);
            if (_apron != null) Destroy(_apron.gameObject);
        }
    }
}
