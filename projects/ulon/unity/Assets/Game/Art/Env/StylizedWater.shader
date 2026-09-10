// **수면 — 깊이 색 + 물가 거품**(오너 지시 2026-09-11 「물이 너무 이상하다. 물 만드는 법을
// 웹서치해 제대로 적용하라」, 검수 확정 기준 그림 Roystan A(거품)·B(깊이 색)).
//
// 왜 셰이더인가: 지금까지 물은 **Standard + 잡음 텍스처 한 장**이었다. 그래서 물의 색이
// 어디서나 같고, 물가에서 뭍과 **칼로 자른 선**으로 만난다 — 화면에서 「물」이 아니라
// 「파란 판때기」로 읽힌 본체가 이것이다. 무늬를 한 자씩 고치는 길로는 못 닫는다
// (되풀이·직조·톱니 랩 넷이 그 자리에서 돌았다).
//
// 무엇을: **씬 깊이**를 읽어 ①물이 깊을수록 진하게 ②물이 얕을수록 거품을 얹는다.
// 이 프로젝트는 **URP가 아니라 Built-in RP**다(`m_CustomRenderPipeline: 0`, SRP 패키지 없음) —
// Shader Graph 구현은 전부 탈락이고, 깊이는 `_CameraDepthNormalsTexture`로 얻는다.
// DepthNormals를 고른 이유는 **아래 표면의 법선이 같이 필요**해서다(아래 참조).
//
// **거품 폭은 「가로 미터」로 정하고 깊이 문턱은 바닥 기울기에서 유도한다**(1.5단계에서 고침).
// 같은 깊이 문턱은 완경사에서 수 미터로 퍼지고 벽에서는 한 픽셀 안에서 끝난다 — 그래서 첫 판은
// 「누우면 얕게 · 서면 깊게」로 `lerp`를 줬는데, 그것으로도 **화면에서 보이는 폭**은 제각각이었다
// (바다 해안 44°에서 가로 0.42m → `15`의 해안선이 맨 선). 지금은
//   문턱(m) = `_FoamWidthM` × tan(바닥 기울기), 하한·상한으로 자름
// 이라 호수(완경사)·모래 둑·바다 해안·부두 벽이 **같은 굵기의 띠**를 갖는다.
//
// 깊이 차는 **시선 방향 거리**지 수직 수심이 아니다 — 쿼터뷰에서 대략 1.4배로 읽힌다.
// 문턱값은 그 눈금 위에서 고른 값이니 카메라 각도를 바꾸면 다시 봐야 한다.
Shader "Ulon/StylizedWater"
{
    Properties
    {
        _Color ("색 보정", Color) = (1,1,1,1)
        _MainTex ("잔무늬(옛 자와 공용)", 2D) = "white" {}
        _ShallowColor ("얕은 물빛", Color) = (0.32, 0.68, 0.74, 1)
        _DeepColor ("깊은 물빛", Color) = (0.07, 0.24, 0.40, 1)
        _DepthMax ("깊은 색까지의 깊이(m)", Float) = 2.2
        _ShallowAlpha ("얕은 곳 불투명도", Range(0,1)) = 0.55
        _DeepAlpha ("깊은 곳 불투명도", Range(0,1)) = 1.0
        _FoamColor ("거품 색", Color) = (1,1,1,1)
        _FoamWidthM ("거품 띠 가로폭(m)", Float) = 2.0
        _FoamDepth ("거품 문턱 하한(m)", Float) = 0.05
        _FoamDepthSteep ("거품 문턱 상한(m)", Float) = 0.60
        _FoamNoiseScale ("거품 잡음 타일(1/m)", Float) = 0.6
        _FoamJitter ("거품 가장자리 흐트러짐", Range(0,1)) = 0.40
        _FoamEdgeSoft ("거품 가장자리 부드러움", Range(0.01,1)) = 0.22
        _RippleScale ("잔물결 타일(1/m)", Float) = 0.35
        _RippleSpeed ("잔물결 속도", Float) = 0.06
        _RippleTint ("잔물결 밝기 폭", Range(0,1)) = 0.18
        _RippleCrest ("잔물결 마루 문턱", Range(0,1)) = 0.72
        _RippleCrestStrength ("잔물결 마루 세기", Range(0,1)) = 0.05
        _TintStrength ("잔무늬 세기", Range(0,1)) = 0.18
        _Glossiness ("매끄러움", Range(0,1)) = 0.25
        _Metallic ("금속기", Range(0,1)) = 0.1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 200
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:fade fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _CameraDepthNormalsTexture;

        struct Input
        {
            float2 uv_MainTex;
            float4 screenPos;
            float3 worldPos;
        };

        fixed4 _Color, _ShallowColor, _DeepColor, _FoamColor;
        float _DepthMax, _ShallowAlpha, _DeepAlpha;
        float _FoamDepth, _FoamDepthSteep, _FoamWidthM, _FoamNoiseScale, _FoamJitter, _FoamEdgeSoft, _TintStrength;
        float _RippleScale, _RippleSpeed, _RippleTint, _RippleCrest, _RippleCrestStrength;
        half _Glossiness, _Metallic;

        // **거품 잡음은 제 것을 쓴다** — 처음엔 물 무늬 텍스처(`_MainTex`)로 띠를 끊으려 했는데
        // 화면이 안 바뀌었다. 그 무늬는 **대비가 거의 없다**(0.5 언저리로 뭉친 값 — 되풀이를
        // 죽이려고 13텍셀 문지름을 넣은 무늬라 당연하다). 자기 자리에서 좋은 값이 다른 자리에서도
        // 좋은 것은 아니다. 그래서 여기서 칸 잡음을 직접 만든다.
        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float VNoise(float2 p)
        {
            float2 i = floor(p), f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = Hash21(i), b = Hash21(i + float2(1, 0));
            float c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // 아래에 있는 것의 깊이와 법선을 한 번에 읽는다(DepthNormals는 16비트라
            // 500m 파플레인에서 눈금이 1cm쯤 — 물 깊이를 재기엔 충분하다).
            float4 dn = tex2Dproj(_CameraDepthNormalsTexture, UNITY_PROJ_COORD(IN.screenPos));
            float depth01;
            float3 normalVS;
            DecodeDepthNormal(dn, depth01, normalVS);
            float sceneZ = depth01 * _ProjectionParams.z;
            float diff = max(0.0, sceneZ - IN.screenPos.w);   // 수면과 바닥 사이 거리(시선 방향)

            // **아래 바닥의 기울기** — 거품 띠의 폭을 정하는 것이 이것이다(1.5단계에서 다시 지음).
            // 첫 판은 「누우면 얕게 · 서면 깊게」를 `lerp`로 줬는데, 그러면 **화면에서 보이는 폭**은
            // 여전히 제각각이다: 바다 해안은 44°(10m에 9.6m 내려간다)라 0.41m 문턱이 가로로
            // **0.42m**밖에 안 돼 `15`의 해안선이 **맨 선**으로 남았다(검수 관찰 2).
            // 그래서 눈금을 바꾼다 — **가로 폭을 상수로** 두고 깊이 문턱을 기울기에서 **유도**한다.
            //   문턱(m) = 가로폭 × tan(바닥 기울기),  아래위로 자른다(평지에서 0으로 죽지 않게)
            // **상한이 실제로 폭을 정한다**: 물밑 바닥은 잔잡음이 있어 국소 기울기가 20~30°로
            // 튄다 — `tan`만 믿으면 완만한 호수에서도 문턱이 1m를 넘어 **호수 절반이 흰 웅덩이**가
            // 된다(그 판을 두 번 찍었다). 그래서 상한으로 자르고, 상한이 사실상 「띠의 굵기」다.
            // **하한은 아주 낮아야 한다**: 0.18m로 뒀더니 호수처럼 **완만한 바닥에서 그 하한이
            // 가로로 몇 미터**가 되어 호수 절반이 흰 웅덩이가 됐다. 하한은 「완전 평지에서 0이
            // 되지 않게」만 하는 값이지 폭을 정하는 값이 아니다.
            float3 normalWS = mul((float3x3)unity_CameraToWorld, normalVS);
            float ny = max(0.05, abs(normalWS.y));
            float tanS = sqrt(saturate(1.0 - ny * ny)) / ny;
            float foamMax = clamp(_FoamWidthM * tanS, _FoamDepth, _FoamDepthSteep);

            float t = saturate(diff / max(0.01, _DepthMax));
            fixed4 water = lerp(_ShallowColor, _DeepColor, t);

            // 잔무늬는 **지우지 않는다** — 텍스처를 재는 자 둘(되풀이 봉우리·방향 쏠림)이
            // 잴 것을 잃지 않게 하고, 물빛에 아주 옅은 얼룩만 준다.
            fixed grain = tex2D(_MainTex, IN.uv_MainTex).g;
            water.rgb *= lerp(1.0, 0.75 + grain * 0.5, _TintStrength);

            // **잔물결 — 두 겹 패닝**(1.5단계). 옛 판에는 잡음 텍스처라도 있었는데 셰이더로
            // 옮기면서 물이 **단색 판때기**가 됐다(검수 관찰 3). 방향과 굵기가 다른 두 겹을
            // 서로 다른 속도로 흘려 겹치면 어느 한 겹의 무늬가 「도장」으로 읽히지 않는다 —
            // 교차 사인이 반드시 십자 격자를 만들던 옛 실패 넷을 되풀이하지 않으려고
            // **파열이 아니라 값 잡음**을 쓴다.
            float2 w1 = IN.worldPos.xz * _RippleScale + _Time.y * _RippleSpeed * float2(1.0, 0.35);
            float2 w2 = IN.worldPos.xz * _RippleScale * 1.7 - _Time.y * _RippleSpeed * float2(0.4, 1.0);
            float rip = VNoise(w1) * 0.6 + VNoise(w2) * 0.4;
            // **멀어지면 끈다.** 절차 잡음은 밉맵이 없어 먼 거리에서 픽셀마다 튄다 — 첫 판에서
            // `15`의 바다가 **흰 반짝이로 뒤덮였다**(3m 무늬가 200m 밖에서 한 픽셀 아래로 들어간
            // 것이다). 가까운 데서만 물결을 주고 먼 바다는 매끈한 색으로 둔다.
            float rippleFade = saturate(1.0 - (IN.screenPos.w - 30.0) / 70.0);
            water.rgb *= 1.0 + (rip - 0.5) * _RippleTint * rippleFade;
            // 마루만 희게 — 잔물결이 「밝기 얼룩」이 아니라 **물결**로 읽히려면 끝이 서야 한다.
            float crest = smoothstep(_RippleCrest, _RippleCrest + 0.12, rip) * _RippleCrestStrength * rippleFade;

            // 거품 — 얕을수록 짙다. 문턱은 위에서 바닥 기울기로 유도한 값이다.
            float foam = 1.0 - saturate(diff / max(0.01, foamMax));
            // **잡음은 문턱에 곱하지 않는다**(검수 판정): 곱하면 띠의 **폭 자체**가 잡음만큼
            // 커졌다 작아져 호수가 흰 웅덩이가 된다. 가장자리에 **더한다** — 그러면 폭은
            // 그대로고 경계만 우글거린다. 안쪽(foam이 1에 가까운 곳)은 그대로 하얗다.
            float2 fuv = IN.worldPos.xz * _FoamNoiseScale;
            float fnoise = VNoise(fuv) * 0.6 + VNoise(fuv * 2.7) * 0.4;
            // **더하지 말고 빼라.** 「가장자리에 더한다」로 지었더니 잡음이 양수인 자리에서는
            // `foam`이 0인 **열린 바다까지 거품**이 됐다(`15`의 바다가 흰 반짝이로 덮인 판).
            // 빼면 물가 바깥은 반드시 0이고, 띠는 **안쪽으로만** 우글거린다.
            float foamMask = smoothstep(0.0, _FoamEdgeSoft, foam - fnoise * _FoamJitter);

            o.Albedo = lerp(water.rgb + crest, _FoamColor.rgb, foamMask) * _Color.rgb;
            o.Metallic = _Metallic * (1.0 - foamMask);
            o.Smoothness = _Glossiness * (1.0 - foamMask);
            o.Alpha = max(lerp(_ShallowAlpha, _DeepAlpha, t), foamMask) * _Color.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
