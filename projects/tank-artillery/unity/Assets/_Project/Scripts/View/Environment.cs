// 환경 — 일러스트 원경과 이어지는 열린 구릉 배경. 이미지가 없으면 기존 원경으로 폴백한다.
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
            // 완성된 원경은 스카이돔에 그린다. 이미지가 없는 경우에만 기존 배경을 쓴다.
            if(Resources.Load<Texture2D>(theme.PanoramaResource)==null)
            {
                BuildClouds(mapSize, theme, ref rng);
                BuildRidges(mapSize, theme, ref rng);
                BuildSun(mapSize, theme, sunDir);
            }
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
            // 🚨 **2026-09-19: 이게 전장을 덮고 있었다.**
            //    예전에는 `PrimitiveType.Plane` 한 장을 맵 9배로 키워 **맵 한가운데에** 깔았다.
            //    그래서 지형이 `groundY - 0.35m` 아래로 내려간 곳은 **어디든 이 판때기가 대신 보였다** —
            //    즉 **모든 크레이터의 바닥**이 그랬다. 화면에서 구덩이가 "평평한 흰 원반"으로 보이던 것이
            //    색 문제가 아니라 이것이었다. 판별: 지형 정점색을 통째로 검게 칠했더니(임시 진단)
            //    **구덩이 안쪽만 검어지지 않고 그대로 남았다** — 지형이 아니었다는 증거다.
            //    (그 전에 "굴착면 색" 가설로 두 번 헛짚었다. 색을 아무리 바꿔도 안 보이는 면을 칠하고 있었다.)
            //
            //    → 이제 **가운데를 뚫은 고리**로 만든다. 이름 그대로 "맵 **밖** 평지"다.
            //    ⚠️ 가운데를 다시 메우지 마라. 판 하나가 싸다고 되돌리면 크레이터가 다시 사라진다.
            //    ⚠️ 안쪽 경계는 맵 경계에 **0.5m 만 겹친다** — 겹침이 없으면 가장자리에 틈이 보이고,
            //       많이 겹치면 그만큼 다시 전장을 덮는다.
            var go = new GameObject("Apron");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, groundY - 0.35f, 0f);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildApronMesh(mapSize, groundY, theme);

            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // 플레이 영역보다 어둡게 — "여기는 전장이 아니다"가 색으로 읽혀야 한다.
            mr.sharedMaterial = new Material(Shader.Find("Tankfall/TerrainVertexColor"));
            mr.sharedMaterial.SetTexture("_DetailTex",Resources.Load<Texture2D>("Art/ground-grass"));
            mr.sharedMaterial.SetFloat("_Grass",0f); // Distant slopes use broad atmospheric color, not tiled foreground grass.
            _apron = go.transform;
            ApronTopY = groundY - 0.35f;

            // ── 가운데 메움판 — **갤러리 전용, 평소에는 꺼져 있다** ──
            // 갤러리(-gallery)는 지형을 통째로 끄고 탱크만 세운다. 고리로 바꾸면서 가운데가 비어
            // 탱크가 허공에 뜬 그림이 됐다(실측). 그래서 "지형이 없을 때만" 까는 판을 따로 둔다.
            // ⚠️ 전투에서는 절대 켜지 마라 — 이게 켜지면 크레이터 바닥이 다시 이 판으로 덮인다.
            //    그 고장을 고치려고 고리로 바꾼 것이다.
            var fill = GameObject.CreatePrimitive(PrimitiveType.Plane);
            fill.name = "ApronFill(갤러리 전용)";
            Destroy(fill.GetComponent<Collider>());
            fill.transform.SetParent(transform, false);
            fill.transform.localScale = Vector3.one * (mapSize * 0.11f);   // Plane 한 변 = 10 단위
            fill.transform.position = new Vector3(mapSize * 0.5f, ApronTopY, mapSize * 0.5f);
            var fr = fill.GetComponent<MeshRenderer>();
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fr.sharedMaterial = Lit(theme.Mid);
            fill.SetActive(false);
            _apronFill = fill;
        }

        GameObject _apronFill;

        /// <summary>갤러리처럼 **지형을 끈 화면**에서만 가운데를 메운다. 전투에서 부르지 마라(크레이터가 덮인다).</summary>
        public void SetApronFill(bool on) { if (_apronFill != null) _apronFill.SetActive(on); }

        /// <summary>자체검사용 — 전투 중에 켜져 있으면 크레이터가 다시 덮인다. 주석이 아니라 코드가 지킨다.</summary>
        public bool ApronFillOn => _apronFill != null && _apronFill.activeSelf;

        /// <summary>
        /// 가운데가 뚫린 사각 고리. 안쪽 구멍 = 플레이 영역 `[-overlap, mapSize+overlap]`,
        /// 바깥 = 그 밖으로 `out` 만큼. 사다리꼴 4장으로 만든다.
        /// </summary>
        public static Mesh BuildApronMesh(float span, float groundY, in MapTheme theme)
        {
            // 열린 중앙은 그대로 유지한다. 바깥만 낮은 구릉으로 이어 평평한 판의 수평선을 없앤다.
            const int sides=128, rings=48;
            var vertices=new Vector3[(rings+1)*sides];
            var colors=new Color[vertices.Length];
            var triangles=new int[rings*sides*6];
            float half=span*.5f;
            for(int ring=0;ring<=rings;ring++) for(int i=0;i<sides;i++)
            {
                float angle=i*Mathf.PI*2/sides;
                Vector2 dir=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                dir/=Mathf.Max(Mathf.Abs(dir.x),Mathf.Abs(dir.y));
                float distance=ring*30f;
                float x=half+dir.x*(half-.5f+distance), z=half+dir.y*(half-.5f+distance);
                float edge=MapHeightFunction.Height(theme.Map,Mathf.Clamp(x,0,span),Mathf.Clamp(z,0,span));
                float rolling=8f+9f*Mathf.Sin(x*.015f+1f)*Mathf.Cos(z*.018f)+4f*Mathf.Sin(z*.035f+x*.021f);
                float h=Mathf.Lerp(edge,rolling,Mathf.SmoothStep(0,1,distance/70f));
                vertices[ring*sides+i]=new Vector3(x,h-(groundY-.35f),z);
                float haze=Mathf.SmoothStep(0,1,distance/480f)*.30f;
                colors[ring*sides+i]=Color.Lerp(theme.Mid,theme.Fog,haze)*(1f+.05f*Mathf.Sin(x*.03f)*Mathf.Cos(z*.025f));
            }
            int t=0;
            for(int ring=0;ring<rings;ring++) for(int i=0;i<sides;i++)
            {
                int a=ring*sides+i,b=ring*sides+(i+1)%sides,c=a+sides,d=b+sides;
                triangles[t++]=a; triangles[t++]=b; triangles[t++]=c;
                triangles[t++]=b; triangles[t++]=d; triangles[t++]=c;
            }
            var mesh=new Mesh{name="Open rolling landscape"};
            mesh.vertices=vertices; mesh.triangles=triangles; mesh.colors=colors;
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        /// <summary>구름 — 납작한 구 덩어리. 하늘이 비어 있으면 높이 감각이 없어진다.</summary>
        void BuildClouds(float mapSize, in MapTheme theme, ref Rng rng)
        {
            _clouds = new GameObject("Clouds").transform;
            _clouds.SetParent(transform, false);
            var mat = Unlit(Color.Lerp(Color.white, theme.SkyBottom, 0.18f));

            // 🚨 **구름이 «화면에 한 번도 없었다»**(2026-09-19 계측: `구름 0/60 보임`).
            //    예전엔 사각 범위(±mapSize*1.7 = ±476m)에 y 90~160 으로 흩뿌렸다. 그러면 대부분이
            //    **카메라에서 가까운데 높아서** 지평선 위 15~25° 에 앉는다 — 그런데 전투 프레임이
            //    보여주는 건 지평선 위 **6.9°** 뿐이다(피치 24° · FOV 60°). 즉 전부 **프레임 위 바깥**.
            //    ⇒ 높이를 «미터»로 정하지 말고 **«각도»로 정한다.** 링에 올려 거리를 고정하고,
            //       높이를 그 거리의 비율로 잡으면 어떤 맵에서도 같은 각도에 앉는다.
            //    목표 띠: 능선 꼭대기(≈4°) 위 ~ 프레임 위 끝(6.9°) 사이.
            //    ⚠️ 카메라가 맵 안에서 ±140m 움직이므로 각도는 폭을 갖는다 — 중앙값이 띠에 들면 된다.
            // 링이라 대부분은 카메라 뒤·옆이다(계측: 뒤 35 · 좌우 14 / 60). 앞쪽 호에 몇 개를 확보하려고 22개를 둔다.
            const int CloudCount = 22;
            for (int i = 0; i < CloudCount; i++)
            {
                var cloud = new GameObject("Cloud").transform;
                cloud.SetParent(_clouds, false);
                float ca = (i / (float)CloudCount) * Mathf.PI * 2f + rng.Float01() * 0.30f;
                float cr = 640f + rng.Float01() * 220f;                 // 최원 능선(660)보다 **바깥** — 가려질 일이 없다
                // 높이 = 거리 × 계수 ⇒ 고도각 고정. 계수는 **실측으로 맞춘 값**이지 유도한 값이 아니다:
                //   0.085~0.115 로 뒀더니 고도각 4.8~8.4° 가 나왔고 **앞쪽 11개가 전부 «위로 넘침»** 이었다.
                //   ⚠️ 이유: 화면 위 끝 6.9° 는 **가운데 세로선** 기준이다. 축에서 벗어난 구름은
                //      `y_screen ∝ Y/Z` 이고 좌우로 갈수록 Z 가 작아져 **같은 고도각이라도 더 위에 찍힌다.**
                //      그래서 「6.9° 아래면 보인다」가 아니라 **여유를 두고 그 아래**여야 한다.
                //   ⚠️ 카메라가 맵 안에서 ±140m 움직여 거리 D 가 cr±140 이 된다 → 고도각이 ±20% 벌어진다.
                //      띠(능선 3.6° ~ 프레임 6.9°)의 **가운데에 중앙값을 놓아야** 양끝이 다 안 샌다.
                cloud.position = new Vector3(
                    mapSize * 0.5f + Mathf.Cos(ca) * cr,
                //   2차 실측: 0.072~0.086 → 보임 14/85 인데 화면 y 가 **879~900**(화면 높이 900)이라
                //   띠 위쪽에 붙어 **윗부분이 잘렸다.** 능선 851 ~ 프레임 900 사이 한가운데(≈865~885)로 내린다.
                //   ⚠️ 고도각으로 맞추려 하지 마라 — 보이는 구름은 대부분 **축에서 벗어나 있어**
                //      같은 고도각이 화면에선 20px 쯤 더 위에 찍힌다. **화면 y 를 보고 맞춘다.**
                    28f + cr * (0.061f + rng.Float01() * 0.012f),
                    mapSize * 0.5f + Mathf.Sin(ca) * cr);

                int puffs = 3 + (int)(rng.Float01() * 3f);
                for (int j = 0; j < puffs; j++)
                {
                    var p = Prim(PrimitiveType.Sphere, mat, cloud);
                    // 멀어진 만큼 키운다 — 안 키우면 «보이긴 하는데 점»이 된다.
                    float s = 30f + rng.Float01() * 34f;
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
            // 🚨 **층을 2 → 3 으로 (2026-09-19).** 원경이 "회색 판때기 몇 장"으로 읽혔다 —
            //    겹이 둘뿐이라 깊이 단서가 하나밖에 없었다. 층이 늘면 **높이를 안 올리고도** 깊이가 생긴다.
            //    ⚠️ 위 경고(하늘을 덮지 마라)는 그대로다. 그래서 **높이/거리 비를 유지**한다 —
            //       먼 층일수록 반경이 커지는 만큼만 높아진다. 비를 키우면 또 하늘이 사라진다.
            for (int layer = 0; layer < 3; layer++)
            {
                float radius = 380f + layer * 140f;
                // ⚠️ 두 번 틀린 자리다. 지형 색에 가까우면 배경이 **하늘에 떠 보이고**,
                //    안개 색에 너무 가까우면 **하늘에 묻혀 아예 안 보인다**(0.72/0.88 로 뒀다가 사라졌다).
                //    🚨 **여기 «선형 안개가 40~75% 를 더 섞어 준다»고 적혀 있었다 — 거짓이다**(2026-09-19 실측).
                //       배경 재질은 `Unlit()` = `Unlit/Color`, 이 셰이더는 `Fog { Mode Off }` 다.
                //       `RenderSettings.fog`(220~620m)는 **원경 산맥에 한 번도 닿은 적이 없다.**
                //       판별: 아래 Lerp 를 손으로 계산해 스크린샷 픽셀과 대조 →
                //         0.52→(145,154,160) · 0.66→(157,172,182) · 0.80→(170,189,203),
                //         3층 × 3채널 **아홉 개 전부 정확히 일치** = 런타임이 더한 것이 0.
                //       ⇒ **여기 적는 색이 화면에 나오는 색 그대로다.** 안개가 보정해 주리라 기대하고
                //          값을 밀지 마라 — 위 「0.72/0.88 로 뒀다가 사라졌다」가 바로 그 사고다.
                // 먼 층일수록 «안개 색 쪽으로» 칠한다(0.52 · 0.68 · 0.80) — 안개에 잠기는 게 아니라 **그렇게 보이게 칠하는** 것이다.
                // 이게 층을 갈라 주는 유일한 단서다.
                float fogMix = 0.52f + layer * 0.14f;
                // ⚠️ **눈 날씨에서 원경만 회색 판이었다**(2026-09-19 스크린샷). 지면·나무·바위는 흰데
                //    산은 `RockDark` 기반이라 어두운 회색으로 남아 세상과 따로 놀았다.
                //    눈이면 안개 쪽으로 한 단 더 민다 — 지우는 게 아니라 **같은 세상에 놓는** 것이다.
                if (theme.Snowy) fogMix = Mathf.Min(0.92f, fogMix + 0.16f);
                var col = Color.Lerp(theme.RockDark, theme.Fog, fogMix);   // 회색 돌덩이가 아니라 "먼 산"으로
                // 🚨 **공기원근**(2026-09-19). 하늘이 생기고 나서야 보였다 — Crater·Ridge·Badlands 는
                //    `Fog` 자체가 땅 색과 비슷한 탄색이라, 위 Lerp 를 아무리 밀어도 산이 **「먼 산」이 아니라
                //    「같은 흙더미」**로 읽혔다. 층을 갈라 주는 단서가 사실상 없었던 것이다.
                //    ⚠️ 엔진 안개가 이걸 해 줄 거라 기대하지 마라 — `Unlit/Color` 는 `Fog { Mode Off }` 다(위 참조).
                //       **여기서 직접 섞어야 한다.**
                //    ⚠️ 층마다 **다른 양**으로 섞는다. 같은 양이면 색만 변하고 **거리 단서는 안 생긴다** —
                //       먼 것일수록 하늘에 가까워지는 게 공기원근이다.
                //    ⚠️ 기준색은 `SkyTop` 이 아니라 **`SkyBottom`**(지평선 쪽 하늘) — 산 뒤에 실제로 있는 색이다.
                //       테마색을 쓰므로 **황무지는 따뜻한 쪽으로, 설산은 푸른 쪽으로** 섞인다(정체성 유지).
                col = Color.Lerp(col, theme.SkyBottom, 0.12f + layer * 0.17f);   // 0.12 · 0.29 · 0.46
                var mat = Unlit(col);
                int n = 34 + layer * 10;
                for (int i = 0; i < n; i++)
                {
                    float a = (i / (float)n) * Mathf.PI * 2f + rng.Float01() * 0.12f;
                    var peak = Prim(PrimitiveType.Cube, mat, _ridges);
                    // 낮게. 지평선 위로 뾰족하게 솟으면 전장보다 배경이 먼저 눈에 든다.
                    // 근경 h≤66 → 꼭대기(회전 포함 ≈ h*0.32+h*0.5+w*0.3 ≈ 100m)/380m ≈ 15° 상한. 원경은 안개가 지운다.
                    // 높이는 **반경에 비례**시킨다 — 각 층의 «수평선 위 각도»가 같아야 하늘을 안 덮는다.
                    // (380m 기준 h, 먼 층은 radius/380 배. 위 주석의 h/r ≤ 0.2 규칙을 층이 늘어도 지킨다.)
                    // 🚨 **여기가 하늘을 다 먹고 있었다**(2026-09-19 계측). 위 주석의 *"≈15° 상한"* 은
                    //    **손계산이고 기준자가 틀렸다** — 세로 FOV 60° 니 지평선 위 30° 가 보인다고
                    //    암묵 가정했는데, 전투 카메라는 18~24° **숙이고 있다.**
                    //    `SkyReport` 실측(피치 24°, 최악 조건): 프레임이 보여주는 건 지평선 위 **6.9°**,
                    //    그런데 능선 꼭대기는 **8.6°** → **화면 위로 넘쳐** 하늘띠가 **0px** 이었다.
                    //    ⇒ h·w 를 **같이 절반**으로. 둘 다 줄이므로 **실루엣 비율은 그대로** —
                    //       「회색 판때기」가 되는 건 «납작해질» 때지 «작아질» 때가 아니다.
                    //       3겹·층별 fogMix 도 그대로라 깊이 단서는 안 잃는다.
                    //    ⚠️ 이 값을 키우려거든 `SkyReport` 의 `하늘띠` 가 **0 이 되는지 먼저 봐라.**
                    //       0 이면 구름·해·하늘 그라디언트가 **전부 같이** 사라진다(그때 그랬다).
                    float h = (13f + rng.Float01() * 20f) * (radius / 380f);
                    float w = 35f + rng.Float01() * 45f;
                    // 🚨 **하늘 띠에 «하한»을 준다**(2026-09-19). 6맵 실측에서 띠가 1.5°(Badlands)~3.9°(Crater)로
                    //    갈렸다 — 맵마다 시드가 다른 h·w 를 뽑기 때문이다. 어떤 맵에서만 하늘이 반쯤 사라지는 건 결함이다.
                    //    ⚠️ **여섯을 같은 값으로 누르지 마라.** 봉우리 높이가 맵마다 다른 건 개성이다.
                    //       그래서 **평탄화가 아니라 상한**이다 — 상한 아래인 봉우리는 **하나도 안 건드린다.**
                    //       결과: 낮은 맵은 그대로 낮고, 튀어나온 맵만 깎인다 ⇒ 분포는 남고 최악만 묶인다.
                    //    ⚠️ h·w 를 **같은 비율로** 줄인다 — 한쪽만 줄이면 납작해져 「회색 판때기」가 된다.
                    //    상한값은 유도가 아니라 **실측으로 맞춘 것**이다(`SkyReport` 의 `하늘띠` 를 보고).
                    //    꼭대기 근사 = 굴린 상자의 반높이 + 중심높이 ≈ 0.703h + 0.321w (회전 40~48°).
                    //   1차 0.060 은 **너무 깎았다**: 띠가 4.6~6.3° 로 올라간 대신 Crater 능선이
                    //   지평선 위 **9px** 만 남아 산이 «선»이 됐다 — 하늘을 얻고 원경을 잃으면 ⑥ 의 실패를 뒤집어 반복하는 것이다.
                    //   근사식이 실제보다 큰 값을 주므로(굴린 상자의 축정렬 바운즈) 상한도 그만큼 느슨해야 한다.
                    const float TopTanCap = 0.090f;      // 실측으로 맞춤 — 낮은 맵은 안 건드리고 튀는 맵만 깎인다
                    float top = 0.703f * h + 0.321f * w;
                    if (top > TopTanCap * radius)
                    {
                        float k = TopTanCap * radius / top;
                        h *= k; w *= k;
                    }
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

        /// <summary>
        /// 하늘 계측기 — **각도를 손으로 계산하지 말고 카메라에게 물어본다.**
        ///
        /// 🚨 왜 있나(2026-09-19). 「구름이 안 보인다」의 원인은 높이·크기·대비·개수가 아니라
        ///    **하늘이 원경 산맥에 통째로 가려져 있던 것**이었다. 그런데 `BuildRidges` 주석은
        ///    봉우리를 *"≈15° 상한"* 으로 **손으로 계산해** 놓고 그걸 합격으로 적어 뒀다 —
        ///    세로 FOV 60° 니 지평선 위 30° 가 보인다고 암묵 가정했는데, 전투 카메라는
        ///    18~24° **숙이고 있어서** 실제로 보이는 건 그 차이뿐이다.
        ///    손계산이 틀린 기준자였으므로 **손계산을 하나 더 하는 것으로는 못 고친다.**
        ///    그래서 실제 투영(`WorldToScreenPoint`)으로 재서 찍는다.
        ///
        /// 읽는 법: `하늘띠`가 0 이면 하늘이 없는 것이고, `구름보임`이 0 이면 구름은 프레임 밖이다.
        /// **둘 다 양수여야 「구름이 보인다」가 성립한다** — 하나만 고치면 화면은 그대로다.
        /// </summary>
        public string SkyReport(Camera cam)
        {
            if (cam == null) return "하늘계측: 카메라 없음";
            float H = cam.pixelHeight;

            // 진짜 지평선 = 카메라 높이에서 **수평으로** 아주 멀리 간 점. 원경 메시의 위쪽 끝이 아니다
            // (에이프런 바깥 테두리를 지평선으로 착각해 4.8° 를 잃은 적이 있다 — 그래서 무한점으로 잡는다).
            var fwd = cam.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.forward; else fwd.Normalize();
            var hp = cam.WorldToScreenPoint(cam.transform.position + fwd * 100000f);
            float horizonY = hp.z > 0f ? hp.y : -1f;      // 스크린 y 는 아래가 0

            // 능선 꼭대기가 화면에서 얼마나 올라오나 — 각 조각의 **월드 바운즈 윗면**을 투영한다.
            float ridgeTopY = -1f; int ridgeN = 0;
            if (_ridges != null)
            {
                foreach (var mr in _ridges.GetComponentsInChildren<MeshRenderer>())
                {
                    var b = mr.bounds;
                    var sp = cam.WorldToScreenPoint(new Vector3(b.center.x, b.max.y, b.center.z));
                    if (sp.z <= 0f) continue;             // 카메라 뒤 — 투영이 뒤집힌다
                    // 🚨 `sp.z > 0` 만으로는 부족하다(2026-09-19, 이 계측기의 **첫 판독이 거짓이었다**).
                    //    카메라 «옆»에 있어 z 가 겨우 양수인 조각은 원근 나눗셈이 터져 y 가 폭발한다 —
                    //    처음 찍힌 `능선꼭대기 y=12451` 은 900px 화면의 13.8배로, **화면에 있지도 않은**
                    //    조각이 만든 숫자였다. 화면 밖 조각이 「하늘띠 0」을 만들면 그건 거짓 빨간불이다.
                    //    ⇒ **가로도 프레임 안**인 것만 센다. 화면에 없는 것은 하늘을 가리지 않는다.
                    if (sp.x < 0f || sp.x > cam.pixelWidth) continue;
                    ridgeN++;
                    if (sp.y > ridgeTopY) ridgeTopY = sp.y;
                }
            }

            // 구름은 «프레임 안에 있나»를 센다. 하나라도 0 이면 위치가 문제지 색이 문제가 아니다.
            // ⚠️ 「0개 보임」만 찍으면 **왜** 0 인지 모른다 — 한 번 그래서 각도 계산을 헛짚었다.
            //    탈락 사유를 나눠 센다: 뒤 / 좌우 밖 / 위로 넘침 / 아래(능선에 묻힘) / far clip.
            int cloudSeen = 0, cloudN = 0; float cloudLowY = -1f, cloudHighY = -1f;
            int cBehind = 0, cSideX = 0, cTooHigh = 0, cTooLow = 0, cFar = 0;
            float elevMin = 999f, elevMax = -999f;
            if (_clouds != null)
            {
                foreach (var mr in _clouds.GetComponentsInChildren<MeshRenderer>())
                {
                    cloudN++;
                    var wc = mr.bounds.center;
                    float dist = Vector3.Distance(wc, cam.transform.position);
                    // 지평선 위 «진짜» 고도각 — 화면 밖이어도 잰다. 이게 있어야 「얼마나 빗나갔나」를 안다.
                    var flat = new Vector3(wc.x - cam.transform.position.x, 0f, wc.z - cam.transform.position.z);
                    float elev = Mathf.Atan2(wc.y - cam.transform.position.y, Mathf.Max(0.01f, flat.magnitude)) * Mathf.Rad2Deg;
                    if (elev < elevMin) elevMin = elev;
                    if (elev > elevMax) elevMax = elev;

                    var sp = cam.WorldToScreenPoint(wc);
                    if (dist > cam.farClipPlane) { cFar++; continue; }
                    if (sp.z <= 0f) { cBehind++; continue; }
                    if (sp.x < 0f || sp.x > cam.pixelWidth) { cSideX++; continue; }
                    if (sp.y > H) { cTooHigh++; continue; }
                    if (sp.y < 0f) { cTooLow++; continue; }
                    cloudSeen++;
                    if (cloudLowY < 0f || sp.y < cloudLowY) cloudLowY = sp.y;
                    if (sp.y > cloudHighY) cloudHighY = sp.y;
                }
            }

            // 하늘 띠 = 지평선 위로 «능선이 안 덮은» 픽셀. 능선이 화면 위로 넘치면 음수가 아니라 0 이다.
            float aboveHorizon = Mathf.Max(0f, H - horizonY);
            float band = horizonY < 0f ? -1f : Mathf.Max(0f, H - Mathf.Max(ridgeTopY, horizonY));
            float ppd = cam.pixelHeight / Mathf.Max(1e-3f, cam.fieldOfView);   // 픽셀/도(세로)

            return string.Format(
                "하늘계측: FOV {0:F1}° · 카메라피치 {1:F1}° · 카메라y {2:F1}m | " +
                "지평선 y={3:F0} (위로 {4:F0}px = {5:F1}°) · 능선꼭대기 y={6:F0} ({7}조각) | " +
                "하늘띠 {8:F0}px = {9:F1}° | 구름 {10}/{11} 보임 (y {12:F0}~{13:F0}) " +
                "· 고도각 {14:F1}~{15:F1}° · 탈락 뒤{16} 좌우{17} 위{18} 아래{19} 멀리{20} · farClip {21:F0}m",
                cam.fieldOfView, cam.transform.eulerAngles.x, cam.transform.position.y,
                horizonY, aboveHorizon, aboveHorizon / ppd, ridgeTopY, ridgeN,
                band, band / ppd, cloudSeen, cloudN, cloudLowY, cloudHighY,
                elevMin, elevMax, cBehind, cSideX, cTooHigh, cTooLow, cFar, cam.farClipPlane);
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
