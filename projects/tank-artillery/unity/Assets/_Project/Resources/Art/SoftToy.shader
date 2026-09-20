// 게임 규모에서도 색과 둥근 형태를 읽을 수 있는 부드러운 장난감 재질.
Shader "Tankfall/SoftToy"
{
 Properties {
  _Color("Color",Color)=(1,1,1,1)
  _Glossiness("Smoothness",Range(0,1))=.35
  _Metallic("Metallic",Range(0,1))=0
  _EmissionColor("Emission",Color)=(0,0,0,0)
 }
 SubShader {
  Tags {"RenderType"="Opaque"}
  Pass {
   Tags {"LightMode"="ForwardBase"}
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fwdbase
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   #include "Lighting.cginc"
   #include "AutoLight.cginc"
   struct v2f { float4 pos:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; LIGHTING_COORDS(2,3) UNITY_FOG_COORDS(4) float3 local:TEXCOORD5; };
   fixed4 _Color,_EmissionColor; float _Glossiness,_Metallic;
   v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.local=v.vertex.xyz; o.world=mul(unity_ObjectToWorld,v.vertex).xyz; o.normal=UnityObjectToWorldNormal(v.normal); TRANSFER_VERTEX_TO_FRAGMENT(o); UNITY_TRANSFER_FOG(o,o.pos); return o; }
   float grain(float3 p) {
    float3 cell=floor(p), f=frac(p); f=f*f*(3-2*f);
    float4 a=frac(sin(float4(dot(cell,float3(17,59,113)),dot(cell+float3(1,0,0),float3(17,59,113)),dot(cell+float3(0,1,0),float3(17,59,113)),dot(cell+float3(1,1,0),float3(17,59,113))))*43758.5453);
    float4 b=frac(sin(float4(dot(cell+float3(0,0,1),float3(17,59,113)),dot(cell+float3(1,0,1),float3(17,59,113)),dot(cell+float3(0,1,1),float3(17,59,113)),dot(cell+float3(1,1,1),float3(17,59,113))))*43758.5453);
    return lerp(lerp(lerp(a.x,a.y,f.x),lerp(a.z,a.w,f.x),f.y),lerp(lerp(b.x,b.y,f.x),lerp(b.z,b.w,f.x),f.y),f.z);
   }
   fixed4 frag(v2f i):SV_Target {
    float3 n=normalize(i.normal), l=normalize(_WorldSpaceLightPos0.xyz), eye=normalize(_WorldSpaceCameraPos-i.world);
    float diffuse=smoothstep(-.20,.80,dot(n,l));
    float shadow=lerp(.64,1.0,LIGHT_ATTENUATION(i));
    float3 hue=lerp(float3(.45,.58,.76),float3(1.08,1.03,.92),diffuse);
    float3 col=_Color.rgb*hue*(.86+.20*diffuse)*shadow;
    col*=lerp(.91,1.04,grain(i.local*9));
    float highlight=pow(saturate(dot(n,normalize(l+eye))),lerp(18,64,_Glossiness));
    col+=highlight*(.07+.21*_Glossiness)*_LightColor0.rgb*lerp(float3(1,1,1),_Color.rgb,_Metallic*.65);
    float rim=pow(1-saturate(dot(n,eye)),3)*smoothstep(-.4,.8,n.y);
    col+=rim*float3(.62,.82,1)*.19+_EmissionColor.rgb;
    fixed4 result=fixed4(col,1); UNITY_APPLY_FOG(i.fogCoord,result); return result;
   }
   ENDCG
  }
 }
 FallBack "Diffuse"
}
