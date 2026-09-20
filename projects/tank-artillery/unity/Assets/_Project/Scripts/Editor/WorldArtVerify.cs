using System;
using UnityEditor;
using UnityEngine;
using Tankfall.Sim;
using Tankfall.View;

namespace Tankfall.EditorTools
{
    public static class WorldArtVerify
    {
        [MenuItem("Tankfall/Verify World Art")]
        public static void Run()
        {
            var shader=Resources.Load<Shader>("Art/SoftToy");
            if(shader==null || !shader.isSupported) throw new Exception("장난감 재질 누락 또는 미지원");
            GuiArt.VerifyAssets();
            if(Resources.Load<Texture2D>("Art/ground-grass")==null || Resources.Load<Shader>("Art/EnergyCurtain")==null)
                throw new Exception("지면 또는 에너지 장막 리소스 누락");
            if(Resources.Load<Texture2D>("Art/ground-biomes")==null) throw new Exception("바이옴 바닥 아틀라스 누락");
            int count=0;
            foreach(MapKind kind in Enum.GetValues(typeof(MapKind))) foreach(bool snow in new[]{false,true})
            {
                var theme=MapTheme.Of(kind,snow);
                var prop=BlenderModels.BuildDecoration(theme.DecorationResource);
                int triangles=0; foreach(var mf in prop.GetComponentsInChildren<MeshFilter>()) triangles+=mf.sharedMesh.triangles.Length/3;
                if(triangles<1000) throw new Exception("맵 오브젝트 불완전: "+theme.DecorationResource);
                if(prop.GetComponentsInChildren<Collider>().Length!=0) throw new Exception("장식이 물리를 막음");
                UnityEngine.Object.DestroyImmediate(prop.gameObject);
                var image=Resources.Load<Texture2D>(theme.PanoramaResource);
                if(image==null || image.width<2000) throw new Exception("원경 이미지 누락: "+theme.PanoramaResource);
                float span=MapHeightFunction.MapSize;
                var mesh=Tankfall.View.Environment.BuildApronMesh(span,0,theme);
                try
                {
                    var vertices=mesh.vertices; var indices=mesh.triangles;
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        var a=vertices[indices[i]]; var b=vertices[indices[i+1]]; var c=vertices[indices[i+2]];
                        var center=(a+b+c)/3f;
                        if(center.x>0 && center.z>0 && center.x<span && center.z<span)
                            throw new Exception("장식 지형이 전장 또는 크레이터를 덮음");
                        if(Vector3.Cross(b-a,c-a).y<=0) throw new Exception("배경 지형 면 방향 오류");
                    }
                    count++;
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
            Debug.Log("[WorldArt] PASS: "+count+" map/weather themes, panorama assets, shader, GUI; open terrain boundary and outward faces");
        }
    }
}
