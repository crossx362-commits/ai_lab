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
        _FoamDepthSteep ("거품 문턱 상한(m)", Float) = 0.30
        _FoamMaxAlpha ("거품 최대 덮음", Range(0,1)) = 0.75
        _FoamBreak ("거품 이음선 깨기", Range(0,1)) = 0.0
        _FoamBreakScale ("거품 깨기 타일(1/m)", Float) = 2.5
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
        float _FoamDepthSteep, _FoamWidthM, _FoamNoiseScale, _FoamJitter, _FoamEdgeSoft, _TintStrength;
        float _FoamMaxAlpha, _FoamBreak, _FoamBreakScale;
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
            //
            // **1.6단계에서 두 곳을 더 고쳤다**(검수 반려):
            // ⓐ **하한을 없앴다**(0.05 → 0). 하한이 있으면 **벽에도 띠가 생긴다** — `64` 우안
            //   절개면이 거의 수직인데도 0.05m짜리 띠가 남아 **계단 윤곽을 흰 선으로 그렸다**.
            //   물가 톱니의 정답은 「윤곽을 따라가되 가늘게」가 아니라 **「그 자리엔 거품이 없다」**다.
            // ⓑ **45°가 넘으면 폭을 0으로 끈다**(`tan45 = 1`). 벽에 부딪히는 물은 얕은 자리가
            //   아예 없다 — 거기 거품을 그리면 그건 파도가 아니라 **경계선을 칠한 것**이다.
            // 그리고 상한을 0.60 → 0.30으로 내렸다. **상한이 곧 띠의 굵기**인데(아래 참조)
            // 셈이 그 이유를 못박았다(`RunFoamExtent`): `15` 호수의 **가장 깊은 흰 픽셀 0.57m**로
            // 상한에 딱 붙어 있었고, 그 자리의 **원장 기울기는 평균 8°**였다. 8°면 가로폭 눈금이
            // 내는 문턱은 0.28m인데 화면은 0.57m까지 하였다 = **화면 법선이 실제보다 가파르게
            // 튄다**(물밑 잔잡음). 그래서 가로폭 눈금은 벽 쪽에서만 물고, 완경사에서는 늘
            // 상한이 지배한다. 호수 흰 몫 18.9%(흰 도넛)의 정체가 이것이다.
            // ⓒ **법선을 고르게 편다**(1.6단계 셋째 수리). 한 픽셀짜리 법선은 물밑 잔잡음 때문에
            //   실제 8° 바닥에서도 **20~40°로 튄다** — 이걸 그대로 쓰면 상한이 늘 지배하고(옛 판의
            //   흰 도넛), 45° 차단을 넣자 이번엔 **튄 픽셀마다 거품이 꺼져** 띠가 통째로 사라졌다
            //   (호수 흰 몫 18.9% → 1.2%). 같은 잡음이 양쪽으로 다 나쁘게 작동한 것이다.
            //   그래서 **주변 네 점을 같이 읽어 평균 낸다** — 몇 px 넓이의 기울기는 바닥의 성질이고,
            //   한 픽셀의 기울기는 잡음의 성질이다.
            float2 suv = IN.screenPos.xy / max(1e-5, IN.screenPos.w);
            float2 sofs = 2.0 / _ScreenParams.xy;
            float3 nAcc = normalVS;
            [unroll] for (int s = 0; s < 4; s++)
            {
                float2 o2 = float2(s == 0 ? sofs.x : (s == 1 ? -sofs.x : 0.0),
                                   s == 2 ? sofs.y : (s == 3 ? -sofs.y : 0.0));
                float dTmp; float3 nTmp;
                DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, suv + o2), dTmp, nTmp);
                nAcc += nTmp;
            }
            // **뷰 공간은 −z가 앞이다.** `DecodeDepthNormal`이 내는 법선을 그대로
            // `unity_CameraToWorld`에 넣으면 z 부호가 뒤집혀 **평평한 바닥도 벽으로 읽힌다** —
            // 1.5단계의 「화면 법선이 실제보다 가파르게 튄다」가 잡음이 아니라 이 부호였다
            // (그 판에서는 상한이 늘 물려 티가 안 났고, 1.6에서 45° 차단을 넣자 **거품이 통째로
            // 사라져** 드러났다. 폭 0과 2가 픽셀까지 같은 것이 증거였다).
            float3 nv = normalize(nAcc);
            nv.z = -nv.z;
            float3 normalWS = normalize(mul((float3x3)unity_CameraToWorld, nv));
            float ny = max(0.05, abs(normalWS.y));
            float tanS = sqrt(saturate(1.0 - ny * ny)) / ny;
            float steepCut = 1.0 - smoothstep(0.85, 1.0, tanS);   // 40°부터 줄어 45°에서 0
            // ⓓ **벽은 법선보다 「한 픽셀에 깊이가 얼마나 변하나」로 잡는 게 확실하다.**
            //   법선 차단만으로는 `64` 우안 절개면의 흰 계단선이 남았다(그 판을 찍어 봤다) —
            //   깎인 면의 법선이 45°를 넘나들어 픽셀마다 살아났다 껐다 한다. 깊이의 화면 기울기는
            //   그런 애매함이 없다: 벽이면 옆 픽셀과 깊이가 크게 벌어진다.
            float wallCut = 1.0 - smoothstep(0.06, 0.20, fwidth(diff));
            float cut = steepCut * wallCut;
            float foamMax = min(_FoamWidthM * tanS * cut, _FoamDepthSteep * cut);

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
            // **거품 안에서도 물색이 비쳐야 한다**(검수 판정 1.6단계). 마스크를 1까지 올리면
            // 띠가 **순백 한 겹**이 되어 과노출로 읽힌다(`64` 좌안). 덮음에 상한을 둔다 —
            // 물빛 위에 거품을 **얹는** 것이지 물을 흰색으로 **갈아치우는** 것이 아니다.
            // **물가 순백 선 — 원인은 폭이 아니라 「고르게 이어짐」이다**(1.7b).
            // 폭을 죽이는 길을 네 번 갔다(①`fwidth(diff)` ②`1/fwidth(foam)` ③가로폭×px/m
            // ④월드 가로폭 `foamMax/tanS`). 넷째 판을 찍고 **셈이 가설을 부쉈다**:
            //   문제의 `64` 좌안 선 = 흰 픽셀 **세로런 중앙값 3px**
            //   검수가 받은 `15` 호수 띠 = **1px**
            // **나쁜 쪽이 더 두껍다.** 좁아서 나쁜 게 아니라 **끊기지 않고 이어져** 계단 노치를
            // 그대로 그리기 때문에 나쁘다. 그래서 폭이 아니라 **이어짐**을 끊는다 — 거품 덮음에
            // 잔칸 잡음을 곱해 얼룩지게 만들면 같은 자리가 「선」이 아니라 「거품 무리」로 읽힌다.
            // 값이 0이면 이 깨기가 꺼진다(NC 경로 — 그 판에서 선이 되살아나야 한다).
            float2 buv = IN.worldPos.xz * _FoamBreakScale;
            float bnoise = VNoise(buv) * 0.6 + VNoise(buv * 3.1) * 0.4;
            float outer = lerp(1.0 - _FoamBreak, 1.0, bnoise);
            float foamCover = foamMask * _FoamMaxAlpha * outer;

            o.Albedo = lerp(water.rgb + crest, _FoamColor.rgb, foamCover) * _Color.rgb;
            o.Metallic = _Metallic * (1.0 - foamCover);
            o.Smoothness = _Glossiness * (1.0 - foamCover);
            o.Alpha = max(lerp(_ShallowAlpha, _DeepAlpha, t), foamMask * outer) * _Color.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
