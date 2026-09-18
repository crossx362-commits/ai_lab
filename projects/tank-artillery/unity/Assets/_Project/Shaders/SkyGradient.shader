// 하늘 — 위아래 두 색 그라디언트 + 절차적 구름 + 태양. 카메라를 감싸는 안쪽 구에 칠한다.
//
// 출처(조사 2026-09-17, 나무위키 「포트리스2/맵」): 원작은 맵마다 하늘이 달랐고 시간대까지 갈랐다 —
// The Sphinx 는 저녁노을, The Night 은 거대한 달이 뜬 밤, The Valley of City 는 푸른 하늘.
// 단색 배경(지금까지의 `Camera.backgroundColor`)으로는 그 축이 통째로 없다.
//
// 구름·태양 추가(2026-09-18, 오너 지시 "그래픽적 요소 개발"): 화면 위쪽 1/3 이 민무늬 그라디언트라
// 하늘이 "배경색"으로만 읽혔다. 텍스처를 들이지 않고 **절차적으로** 굽는다 —
// 이 프로젝트는 지형(§7)·탱크(§9)·UI 까지 전부 코드로 그린다(에셋을 들이면 라이선스·용량이 거기서만 갈린다).
//
// ⚠️ 구름 색을 여기에 박지 마라. 맵 테마(MapTheme)의 하늘색에서 C# 이 만들어 넘긴다 —
//    안 그러면 노을 맵에 흰 구름이 떠서 테마가 깨진다(맵마다 하늘을 가른 이유가 사라진다).
// ⚠️ 컬링을 뒤집고(Cull Front) 깊이를 쓰지 않는다 — 안쪽에서 보는 구이고 항상 제일 뒤에 있어야 한다.
Shader "Tankfall/SkyGradient"
{
    Properties
    {
        _Top ("Top", Color) = (0.33, 0.58, 0.86, 1)
        _Bottom ("Bottom", Color) = (0.76, 0.87, 0.95, 1)
        _Exp ("Blend Exponent", Range(0.2, 4)) = 1.0

        _CloudColor ("Cloud Color", Color) = (1, 1, 1, 1)
        _CloudDark ("Cloud Shadow Color", Color) = (0.72, 0.76, 0.84, 1)
        _CloudCover ("Cloud Cover", Range(0, 1)) = 0.42      // 낮을수록 구름이 많다
        _CloudSoft ("Cloud Edge Softness", Range(0.01, 0.6)) = 0.12
        _CloudScale ("Cloud Scale", Range(0.5, 20)) = 5.0    // 작게 두면 구름 하나가 하늘을 다 덮어 "구름 없음"으로 보인다
        _CloudSquash ("Cloud Vertical Squash", Range(1, 6)) = 2.5   // y 를 눌러 가로로 늘린다(원근 흉내)
        _CloudSpeed ("Cloud Drift", Range(0, 0.05)) = 0.006

        _SunDir ("Sun Direction", Vector) = (0.5, 0.6, 0.6, 0)
        _SunColor ("Sun Color", Color) = (1, 0.97, 0.88, 1)
        _SunSize ("Sun Size", Range(0.99, 0.9999)) = 0.9975
        _SunGlow ("Sun Glow", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" }
        Cull Front
        ZWrite Off
        ZTest LEqual
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct v2f { float4 pos : SV_POSITION; float3 local : TEXCOORD0; };

            fixed4 _Top, _Bottom, _CloudColor, _CloudDark, _SunColor;
            float _Exp, _CloudCover, _CloudSoft, _CloudScale, _CloudSpeed, _CloudSquash;
            float _SunSize, _SunGlow;
            float4 _SunDir;

            // 값 노이즈 — 텍스처 없이 구름을 만들려면 해시가 유일한 통로다.
            //
            // 🚨 **방향 벡터(3D)에 직접 건다.** 처음엔 하늘 반구를 평면에 투영해서(`d.xz / d.y`) 2D 노이즈를
            //    썼는데, 이 게임의 전투 카메라는 지평선 근처를 보므로 d.y 가 작다 → uv 가 발산해
            //    구름이 **방사형 흰 줄무늬**로 늘어났다(2026-09-18 화면에서 확인).
            //    3D 로 가면 이음매도 발산도 없다. 원근은 y 를 눌러(아래 CloudSquash) 가로로 늘려서 흉내 낸다.
            float hash31(float3 p)
            {
                return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
            }

            float vnoise3(float3 p)
            {
                float3 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);                  // smoothstep — 격자 자국을 없앤다
                float c000 = hash31(i), c100 = hash31(i + float3(1, 0, 0));
                float c010 = hash31(i + float3(0, 1, 0)), c110 = hash31(i + float3(1, 1, 0));
                float c001 = hash31(i + float3(0, 0, 1)), c101 = hash31(i + float3(1, 0, 1));
                float c011 = hash31(i + float3(0, 1, 1)), c111 = hash31(i + float3(1, 1, 1));
                float x00 = lerp(c000, c100, f.x), x10 = lerp(c010, c110, f.x);
                float x01 = lerp(c001, c101, f.x), x11 = lerp(c011, c111, f.x);
                return lerp(lerp(x00, x10, f.y), lerp(x01, x11, f.y), f.z);
            }

            // 4 옥타브면 뭉게구름 실루엣이 선다. 더 올리면 프레임만 먹고 로우폴리 톤에서 안 읽힌다.
            float fbm(float3 p)
            {
                float s = 0.0, amp = 0.5;
                for (int k = 0; k < 4; k++) { s += vnoise3(p) * amp; p *= 2.03; amp *= 0.5; }
                return s;
            }

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.local = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.local);
                float h = saturate(d.y * 0.5 + 0.5);
                float3 sky = lerp(_Bottom.rgb, _Top.rgb, pow(h, _Exp));

                // ── 태양 ──────────────────────────────────────
                // 글로를 먼저 얹고 원반을 덮는다. 순서를 바꾸면 원반 테두리에 글로가 얹혀 뭉갠다.
                float sd = saturate(dot(d, normalize(_SunDir.xyz)));
                sky += _SunColor.rgb * pow(sd, 220.0) * _SunGlow;              // 좁은 후광
                sky += _SunColor.rgb * pow(sd, 8.0) * _SunGlow * 0.18;         // 넓고 옅은 산란
                float disc = smoothstep(_SunSize, _SunSize + 0.0006, sd);
                sky = lerp(sky, _SunColor.rgb * 1.15, disc);

                // ── 구름 ──────────────────────────────────────
                // ⚠️ 띠를 높이 잡지 마라. 전투 카메라는 지형을 내려다봐서 **하늘이 보이는 구간이 d.y ≈ 0.05~0.5** 다 —
                //    처음에 0.26 부터 깔았더니 구름이 화면 밖(천정)에만 떠서 게임에서 한 조각도 안 보였다(2026-09-18).
                float up = saturate(d.y);
                float band = smoothstep(0.035, 0.11, up) * (1.0 - smoothstep(0.85, 1.0, up));

                // y 를 눌러 가로로 늘린다 — 평면 투영 없이 원근만 흉내 낸다(발산하지 않는다).
                float3 p = float3(d.x, d.y * _CloudSquash, d.z) * _CloudScale
                         + float3(_Time.y * _CloudSpeed, 0, _Time.y * _CloudSpeed * 0.27);

                float n = fbm(p);
                float c = smoothstep(_CloudCover, _CloudCover + _CloudSoft, n) * band;

                // 결을 주려고 살짝 어긋난 두 번째 샘플을 그늘로 쓴다 — 구름이 납작한 판때기로 안 보이게.
                float shade = smoothstep(_CloudCover, _CloudCover + _CloudSoft, fbm(p + float3(0.35, 0.12, 0.22)));
                float3 cloud = lerp(_CloudDark.rgb, _CloudColor.rgb, saturate(shade));

                // 태양 쪽 구름은 가장자리가 밝다(림).
                cloud += _SunColor.rgb * pow(sd, 6.0) * 0.25;

                sky = lerp(sky, cloud, saturate(c));
                return fixed4(sky, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
