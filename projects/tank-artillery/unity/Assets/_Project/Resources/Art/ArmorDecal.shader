Shader "Tankfall/ArmorDecal"
{
 Properties { _MainTex("Panel decals",2D)="white" {} }
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent"}
  Cull Off
  ZWrite Off
  Blend SrcAlpha OneMinusSrcAlpha
  Offset -1,-1
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   sampler2D _MainTex;
   struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; UNITY_FOG_COORDS(1) };
   v2f vert(appdata_base v) { v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord.xy; UNITY_TRANSFER_FOG(o,o.pos); return o; }
   fixed4 frag(v2f i):SV_Target { fixed4 c=tex2D(_MainTex,i.uv); UNITY_APPLY_FOG(i.fogCoord,c); return c; }
   ENDCG
  }
 }
}
