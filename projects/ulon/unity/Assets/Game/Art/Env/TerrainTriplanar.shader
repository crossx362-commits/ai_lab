// **지형 삼면 투영**(검수·대장 승인 2026-09-09 — 랩 ⑥①의 근본 수리).
//
// 왜: 기본 지형 셰이더는 도포를 **XZ 평면으로 투영**한다. 그래서 경사가 설수록 무늬가 세로로
// 늘어나고, 이 산은 최대 82°다 — 「늘어난 절벽 텍스처는 아마추어 표식」(오너·대장).
// 무늬를 두 겹으로 갈라도(랩 ⑥), 바위를 104개 박아도(랩 ⑦) 화면에서 줄은 안 끊겼다.
// 늘어남은 **투영의 성질**이라 결과를 손봐서는 안 없어진다.
//
// 무엇을: 세 축(XZ·ZY·XY)으로 각각 샘플하고 **법선 방향으로 섞는다**. 평지는 XZ가 1에 가까워
// 지금 그림이 그대로 유지되고(회귀 금지 조건), 벽면은 옆 축이 이겨 무늬가 벽을 따라 눕는다.
//
// 도포는 **9겹 그대로 받는다**(검수 조건: 못 받으면 겹을 줄이지 말고 셰이더를 고쳐라).
// 컨트롤은 유니티 자동 바인딩에 기대지 않고 **빌더가 알파맵에서 구워 직접 꽂는다** —
// 자동 바인딩은 5겹부터 add pass로 갈라져 커스텀 재질에서 조용히 어긋난다.
Shader "Ulon/TerrainTriplanar"
{
    Properties
    {
        _Ctrl0 ("도포 0-3", 2D) = "black" {}
        _Ctrl1 ("도포 4-7", 2D) = "black" {}
        _Ctrl2 ("도포 8-11", 2D) = "black" {}
        _L0 ("풀", 2D) = "white" {}
        _L1 ("바위", 2D) = "white" {}
        _L2 ("모래", 2D) = "white" {}
        _L3 ("밭", 2D) = "white" {}
        _L4 ("부엽토", 2D) = "white" {}
        _L5 ("자갈", 2D) = "white" {}
        _L6 ("길", 2D) = "white" {}
        _L7 ("돌포장", 2D) = "white" {}
        _L8 ("마른 풀", 2D) = "white" {}
        _L9 ("그늘진 절벽", 2D) = "white" {}
        _Tiles0 ("타일 0-3(m)", Vector) = (12, 3, 8, 3.5)
        _Tiles1 ("타일 4-7(m)", Vector) = (6, 4.5, 5, 2)
        _Tiles2 ("타일 8-11(m)", Vector) = (9, 1, 1, 1)
        _WorldSpan ("지형 한 변(m)", Float) = 300
        // 섞는 날카로움 — 낮으면 평지에도 옆면 무늬가 배어 **평지 그림이 바뀐다**(회귀).
        // 높으면 벽과 바닥 경계가 칼로 자른 듯 갈린다. 4는 그 사이에서 고른 값이다.
        _TriSharp ("삼면 혼합 날카로움", Float) = 4
        // 반대쪽 한계용 — 1이면 XZ 한 축만 쓴다(= 옛 평면 투영). 자가 이것으로 빨간불을 확인한다.
        _PlanarOnly ("평면 투영으로 되돌리기", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry-100" "TerrainCompatible" = "true" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.5

        sampler2D _Ctrl0, _Ctrl1, _Ctrl2;
        sampler2D _L0, _L1, _L2, _L3, _L4, _L5, _L6, _L7, _L8, _L9;
        float4 _Tiles0, _Tiles1, _Tiles2;
        float _WorldSpan, _TriSharp, _PlanarOnly;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        // 한 겹을 세 축으로 샘플해 법선으로 섞는다.
        half3 TriSample(sampler2D tex, float3 wp, float3 bw, float tile)
        {
            half3 x = tex2D(tex, wp.zy / tile).rgb;
            half3 y = tex2D(tex, wp.xz / tile).rgb;
            half3 z = tex2D(tex, wp.xy / tile).rgb;
            return x * bw.x + y * bw.y + z * bw.z;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = normalize(IN.worldNormal);
            float3 bw = pow(abs(n), _TriSharp);
            bw /= max(bw.x + bw.y + bw.z, 1e-4);
            // 평면 투영으로 되돌리기(반대쪽 한계): XZ 한 축만 — 이러면 절벽 무늬가 도로 늘어난다.
            bw = lerp(bw, float3(0, 1, 0), saturate(_PlanarOnly));

            // 컨트롤 UV는 **월드 좌표에서 직접** 만든다 — 지형 UV 규약에 기대지 않는다.
            float2 cuv = IN.worldPos.xz / _WorldSpan + 0.5;
            half4 c0 = tex2D(_Ctrl0, cuv);
            half4 c1 = tex2D(_Ctrl1, cuv);
            half4 c2 = tex2D(_Ctrl2, cuv);

            half3 col = 0;
            col += c0.r * TriSample(_L0, IN.worldPos, bw, _Tiles0.x);
            col += c0.g * TriSample(_L1, IN.worldPos, bw, _Tiles0.y);
            col += c0.b * TriSample(_L2, IN.worldPos, bw, _Tiles0.z);
            col += c0.a * TriSample(_L3, IN.worldPos, bw, _Tiles0.w);
            col += c1.r * TriSample(_L4, IN.worldPos, bw, _Tiles1.x);
            col += c1.g * TriSample(_L5, IN.worldPos, bw, _Tiles1.y);
            col += c1.b * TriSample(_L6, IN.worldPos, bw, _Tiles1.z);
            col += c1.a * TriSample(_L7, IN.worldPos, bw, _Tiles1.w);
            col += c2.r * TriSample(_L8, IN.worldPos, bw, _Tiles2.x);
            // **10겹째를 빠뜨렸었다**(랩 ⑪에서 발견). `WorldSplat.LayerCount`는 10인데 셰이더가 9겹만
            // 더해서 **그늘진 절벽(CliffDark)이 아예 안 그려졌다** — 도포는 두 겹인데 화면은 한 겹.
            // 게다가 그 겹이 우세한 자리는 `wsum`이 0에 가까워져, 나눗셈이 **남은 티끌 가중치를 증폭**해
            // 산비탈에 갈색 판때기가 떴다. 겹을 늘릴 땐 **원장·굽는 쪽·셰이더 셋을 같이** 고쳐라.
            col += c2.g * TriSample(_L9, IN.worldPos, bw, _Tiles2.y);

            half wsum = c0.r + c0.g + c0.b + c0.a + c1.r + c1.g + c1.b + c1.a + c2.r + c2.g;
            o.Albedo = col / max(wsum, 1e-3);
            o.Metallic = 0;
            o.Smoothness = 0.05;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
