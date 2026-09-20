Shader "Tankfall/EnergyCurtain"
{
 Properties { _Color("Tint",Color)=(1,.76,.26,1) }
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent"}
  ZWrite Off Cull Back Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   fixed4 _Color;
   struct v2f {float4 pos:SV_POSITION; float3 world:TEXCOORD0;};
   v2f vert(appdata_base v) {v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.world=mul(unity_ObjectToWorld,v.vertex).xyz;return o;}
   fixed4 frag(v2f i):SV_Target {
    float2 uv=i.world.zy*.22;
    float2 cell=abs(frac(uv)-.5);
    float gridLine=smoothstep(.455,.49,max(cell.x,cell.y));
    float pulse=pow(saturate(.5+.5*sin(i.world.y*.42-_Time.y*1.3)),12);
    return fixed4(lerp(_Color.rgb,float3(1,1,.8),pulse*.5),.055+gridLine*.13+pulse*.10);
   }
   ENDCG
  }
 }
}
