// 파괴 가능한 지형: 부드러운 법선, 넓은 명암, 실제 거리 안개.
Shader "Tankfall/TerrainVertexColor"
{
    Properties
    {
        _BiomeTex ("Biome atlas",2D)="gray" {}
        _Biome ("Biome index",Float)=0
        _BiomeStrength ("Biome strength",Float)=0
        _DetailTex ("Painted grass",2D)="gray" {}
        _Grass ("Grass detail",Float)=0
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
            #pragma multi_compile_fog
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
                UNITY_FOG_COORDS(4)
            };

            sampler2D _DetailTex, _BiomeTex; float _Grass, _Biome, _BiomeStrength;
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
                UNITY_TRANSFER_FOG(o,o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNrm);

                float ndl = saturate(dot(n, normalize(_WorldSpaceLightPos0.xyz)));
                // 계단 그림자(셀 셰이딩). 0 이면 부드럽게 — 맵 테마에 따라 고를 수 있게 남겨 둔다.
                if (_Ramp >= 1.0) ndl = floor(ndl * _Ramp + 0.5) / _Ramp;

                float atten = LIGHT_ATTENUATION(i);
                float3 ambient = ShadeSH9(float4(n, 1));
                float3 albedo = i.color.rgb * _Tint.rgb;
                float softLight = smoothstep(-.25, .9, dot(n, normalize(_WorldSpaceLightPos0.xyz)));
                float3 shade = lerp(float3(.59,.72,.77),float3(1.04,1.04,1.0),softLight);
                float grain=tex2D(_DetailTex,i.worldPos.xz*.05).g;
                float meadow = (sin(i.worldPos.x*.047+sin(i.worldPos.z*.031)*1.4) * cos(i.worldPos.z*.038))*.5+.5;
                float grassMask = _Grass*smoothstep(.4,.85,n.y);
                albedo *= 1+(grain-.60)*.28*grassMask;
                albedo *= lerp(float3(1,1,1),lerp(float3(.80,.94,.92),float3(1.04,1.02,.88),meadow),grassMask);
                float2 tile=frac(i.worldPos.xz*.055);
                tile=tile*.97+.015;
                float2 cell=float2(fmod(_Biome,3),1-floor(_Biome/3));
                float2 gradX=ddx(i.worldPos.xz*.055)/float2(3,2);
                float2 gradY=ddy(i.worldPos.xz*.055)/float2(3,2);
                float3 surface=tex2Dgrad(_BiomeTex,(tile+cell)/float2(3,2),gradX,gradY).rgb;
                float materialMask=_BiomeStrength*smoothstep(.35,.85,n.y);
                // Moderate material color, preserving exposed-earth vertex colors and slope shading.
                float3 painted=albedo*(.65+surface*.75);
                albedo=lerp(albedo,painted,materialMask);
                float3 col = albedo * shade * lerp(.64,1.0,atten);
                fixed4 result = fixed4(col,1);
                UNITY_APPLY_FOG(i.fogCoord,result);
                return result;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
