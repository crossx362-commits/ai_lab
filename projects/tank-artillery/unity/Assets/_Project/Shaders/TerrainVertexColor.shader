// 지형 셰이더 — 정점 색 + **플랫 셰이딩**(로우폴리 캐주얼).
//
// 출처(조사 2026-09-17): 로우폴리 캐주얼 아트의 핵심 세 가지 —
//   (1) flat shading: 면마다 단일 색. 부드러운 블렌딩 없이 지오메트리를 드러낸다
//   (2) 제한된 색 팔레트: 형태가 읽히고 화면이 시끄럽지 않다
//   (3) directional light: 면과 모서리를 갈라 보여준다
//   retrostylegames.com/blog/low-poly-game-art-an-ultimate-guide · sundaysundae.co/how-to-make-low-poly-look-good
//
// ⚠️ 정점을 삼각형마다 쪼개서 플랫 셰이딩을 만들지 마라.
//    지형은 삼각형 45만 개다(§7-7) — 정점이 3배가 되면 폭발 프레임(§7-6-3)이 무너진다.
//    여기서는 **픽셀 셰이더가 화면 미분(ddx/ddy)으로 면 법선을 복원한다.** 정점 수가 안 변한다.
//
// ⚠️ Surface Shader 로는 못 한다 — `o.Normal` 이 접선 공간을 기대하는데 이 메시엔 탄젠트가 없다.
//    그래서 ForwardBase 를 직접 쓰고, 그림자 드리우기는 FallBack 의 ShadowCaster 에 맡긴다.
//
// ⚠️ 이 셰이더가 빌드에서 빠지면 지형이 분홍색이 된다 — BuildScript.EnsureShadersIncluded 에 등록돼 있어야 한다.
Shader "Tankfall/TerrainVertexColor"
{
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
        _Ramp ("Shade Ramp (계단 수, 0=끄기)", Range(0,8)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            struct v2f
            {
                float4 pos       : SV_POSITION;
                fixed4 color     : COLOR;
                float3 worldPos  : TEXCOORD0;
                float3 worldNrm  : TEXCOORD1;
                LIGHTING_COORDS(2, 3)
            };

            fixed4 _Tint;
            float _Ramp;

            v2f vert(appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNrm = UnityObjectToWorldNormal(v.normal);
                TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 면 법선 — 이 픽셀이 속한 삼각형 평면에서 바로 구한다(보간된 법선은 방향 확인용으로만 쓴다).
                //
                // ⚠️ `cross(ddy, ddx)` 의 부호는 플랫폼·좌표계에 따라 뒤집힌다.
                //    처음 이대로 뒀더니 **평지 전체가 빛을 등져 회백색으로 떴다**(맥/Metal 에서 확인).
                //    그래서 보간된 정점 법선과 내적해 **부호를 맞춘다** — 어느 플랫폼에서도 안 뒤집힌다.
                //    (동굴 천장처럼 아래를 향한 면도 정점 법선이 이미 아래를 향하므로 그대로 보존된다.)
                float3 n = normalize(cross(ddy(i.worldPos), ddx(i.worldPos)));
                if (dot(n, normalize(i.worldNrm)) < 0) n = -n;

                float ndl = saturate(dot(n, normalize(_WorldSpaceLightPos0.xyz)));
                // 계단 그림자(셀 셰이딩). 0 이면 부드럽게 — 맵 테마에 따라 고를 수 있게 남겨 둔다.
                if (_Ramp >= 1.0) ndl = floor(ndl * _Ramp + 0.5) / _Ramp;

                float atten = LIGHT_ATTENUATION(i);
                float3 ambient = ShadeSH9(float4(n, 1));
                float3 albedo = i.color.rgb * _Tint.rgb;
                float3 col = albedo * (_LightColor0.rgb * ndl * atten + ambient);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
