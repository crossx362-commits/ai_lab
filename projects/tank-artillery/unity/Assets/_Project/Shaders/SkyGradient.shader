// 하늘 — 위아래 두 색 그라디언트. 카메라를 감싸는 안쪽 구에 칠한다.
//
// 출처(조사 2026-09-17, 나무위키 「포트리스2/맵」): 원작은 맵마다 하늘이 달랐고 시간대까지 갈랐다 —
// The Sphinx 는 저녁노을, The Night 은 거대한 달이 뜬 밤, The Valley of City 는 푸른 하늘.
// 단색 배경(지금까지의 `Camera.backgroundColor`)으로는 그 축이 통째로 없다.
//
// ⚠️ 컬링을 뒤집고(Cull Front) 깊이를 쓰지 않는다 — 안쪽에서 보는 구이고 항상 제일 뒤에 있어야 한다.
Shader "Tankfall/SkyGradient"
{
    Properties
    {
        _Top ("Top", Color) = (0.33, 0.58, 0.86, 1)
        _Bottom ("Bottom", Color) = (0.76, 0.87, 0.95, 1)
        _Exp ("Blend Exponent", Range(0.2, 4)) = 1.0
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

            fixed4 _Top, _Bottom;
            float _Exp;

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.local = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float h = saturate(normalize(i.local).y * 0.5 + 0.5);
                return lerp(_Bottom, _Top, pow(h, _Exp));
            }
            ENDCG
        }
    }
    FallBack Off
}
