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
// **거품 깊이를 아래 표면의 법선 각도로 달리한다**(Roystan이 지적한 함정의 처방):
// 거품 폭을 깊이 하나로 정하면 **완경사에서 과하고 수직면에서 사라진다** — 같은 깊이 차가
// 모래 둑에서는 수 미터로 퍼지고 부두 벽에서는 한 픽셀 안에서 끝나기 때문이다.
// `63`(부두, 수직면)과 `64`(모래 둑, 완경사)가 정확히 그 두 경우라, 아래 면이 설수록
// 문턱을 키운다(`_FoamDepthSteep`), 누울수록 줄인다(`_FoamDepth`).
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
        _FoamDepth ("거품 문턱 — 완경사(m)", Float) = 0.22
        _FoamDepthSteep ("거품 문턱 — 수직면(m)", Float) = 0.90
        _FoamNoiseScale ("거품 잡음 타일(1/m)", Float) = 0.6
        _FoamCutoff ("거품 잡음 문턱", Range(0,1)) = 0.55
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
        float _FoamDepth, _FoamDepthSteep, _FoamNoiseScale, _FoamCutoff, _TintStrength;
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

            // 아래 면이 얼마나 서 있나 — 0이면 바닥(모래 둑), 1이면 벽(부두 옆면).
            float3 normalWS = mul((float3x3)unity_CameraToWorld, normalVS);
            float steep = saturate(1.0 - abs(normalWS.y));

            float t = saturate(diff / max(0.01, _DepthMax));
            fixed4 water = lerp(_ShallowColor, _DeepColor, t);

            // 잔무늬는 **지우지 않는다** — 텍스처를 재는 자 둘(되풀이 봉우리·방향 쏠림)이
            // 잴 것을 잃지 않게 하고, 물빛에 아주 옅은 얼룩만 준다.
            fixed grain = tex2D(_MainTex, IN.uv_MainTex).g;
            water.rgb *= lerp(1.0, 0.75 + grain * 0.5, _TintStrength);

            // 거품 — 얕을수록 짙다. 문턱은 아래 면의 각도로 달라진다(머리말 참조).
            float foamMax = lerp(_FoamDepth, _FoamDepthSteep, steep);
            float foam = 1.0 - saturate(diff / max(0.01, foamMax));
            // 잡음으로 경계를 흐트러뜨린다 — 곧은 띠는 물가가 아니라 **테두리**로 읽힌다.
            // 첫 판이 정확히 그랬다(호수 둘레에 균일한 흰 도넛). 굵기가 다른 두 겹을 곱해
            // 띠를 끊는다 — 한 겹만으로는 잡음이 띠 폭보다 굵어 통째로 밝아지거나 통째로 죽는다.
            float2 fuv = IN.worldPos.xz * _FoamNoiseScale;
            float fnoise = saturate(VNoise(fuv) * 0.70 + VNoise(fuv * 2.7) * 0.45);
            // 곱은 **평균 1 언저리**로 맞춘다 — 잡음을 키워 놓고 그걸 곱하면 문턱 자체가 커져서
            // 띠가 통째로 넓어진다(호수가 흰 웅덩이가 됐던 판이 그것이다). 잡음은 띠를 **끊는**
            // 것이지 넓히는 것이 아니다.
            float foamMask = smoothstep(_FoamCutoff - 0.18, _FoamCutoff + 0.18, foam * (0.60 + fnoise * 0.70));

            o.Albedo = lerp(water.rgb, _FoamColor.rgb, foamMask) * _Color.rgb;
            o.Metallic = _Metallic * (1.0 - foamMask);
            o.Smoothness = _Glossiness * (1.0 - foamMask);
            o.Alpha = max(lerp(_ShallowAlpha, _DeepAlpha, t), foamMask) * _Color.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
