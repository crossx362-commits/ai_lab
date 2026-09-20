using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Tankfall.Sim;
using Tankfall.View;

namespace Tankfall.EditorTools
{
    public static class NativeTankAssetsVerify
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        [MenuItem("Tankfall/Verify Native 3D Assets")]
        public static void Run()
        {
            int tanks=0, shells=0;
            var body=new Material(Shader.Find("Tankfall/SoftToy"));
            var track=new Material(body); var team=new Material(body);
            try
            {
                foreach(TankKind kind in Enum.GetValues(typeof(TankKind)))
                {
                    string folder="Assets/_Project/Resources/TankModels/";
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"Tanks/"+kind+".prefab");
                    Require(prefab!=null,"Unity에 탱크 프리팹이 없습니다: "+kind);
                    var fbx=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Tanks/FBX/"+kind+".fbx");
                    Require(fbx!=null && fbx.GetComponentsInChildren<MeshFilter>().Length>0,"FBX 메시 임포트 실패: "+kind);
                    var root=ProceduralTank.Build(TankShape.Of(kind),body,track,team,out var turret,out var barrel,out var fire);
                    try
                    {
                        var imported=BlenderModels.Load(kind.ToString());
                        Require(root.GetComponent<TankAimRig>()!=null && turret.parent==root && barrel.parent==turret && fire.parent==barrel,
                            "TankShape 조준 계층 누락: "+kind);
                        Require(turret.childCount==1 && barrel.childCount==1 && fire.childCount==0 &&
                            turret.GetComponentsInChildren<Renderer>(true).Length==0,"조준 기준에 모델 지오메트리가 들어옴: "+kind);
                        Require(root.GetComponentsInChildren<Transform>(true).Length==imported.nodes.Length+3,"모델/조준 계층 수 불일치: "+kind);
                        int expectedTriangles=0,actualTriangles=0;
                        foreach(var node in imported.nodes) expectedTriangles+=node.triangles.Length;
                        foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true)) actualTriangles+=mf.sharedMesh.triangles.Length;
                        Require(expectedTriangles==actualTriangles,"원본과 프리팹 메시가 다름: "+kind);
                        var mapped=new Transform[imported.nodes.Length]; mapped[0]=root;
                        var used=new HashSet<Transform>{root};
                        for(int n=1;n<imported.nodes.Length;n++)
                        {
                            var node=imported.nodes[n]; var parent=mapped[node.parent];
                            foreach(Transform child in parent)
                                if(child.name==node.name && used.Add(child)) { mapped[n]=child; break; }
                            Require(mapped[n]!=null,"원본 노드 연결 누락: "+kind+"/"+node.name);
                            var pos=new Vector3(node.position[0],node.position[1],node.position[2]);
                            Require((mapped[n].localPosition-pos).sqrMagnitude<1e-8f,"부품 위치 불일치: "+kind+"/"+node.name);
                            var rot=node.rotation!=null && node.rotation.Length==4 ? new Quaternion(node.rotation[0],node.rotation[1],node.rotation[2],node.rotation[3]) : Quaternion.identity;
                            var scale=node.scale!=null && node.scale.Length==3 ? new Vector3(node.scale[0],node.scale[1],node.scale[2]) : Vector3.one;
                            Require(Quaternion.Angle(mapped[n].localRotation,rot)<.05f,"부품 회전 불일치: "+kind+"/"+node.name);
                            Require((mapped[n].localScale-scale).sqrMagnitude<1e-8f,"부품 크기 불일치: "+kind+"/"+node.name);
                            if(node.vertices.Length==0) continue;
                            var vertices=mapped[n].GetComponent<MeshFilter>().sharedMesh.vertices;
                            Require(vertices.Length*3==node.vertices.Length,"부품 메시 교차 연결: "+kind+"/"+node.name);
                            for(int v=0;v<vertices.Length;v++)
                            {
                                var expected=new Vector3(node.vertices[v*3],node.vertices[v*3+1],node.vertices[v*3+2]);
                                Require((vertices[v]-expected).sqrMagnitude<1e-8f,"부품 정점 불일치: "+kind+"/"+node.name+"/"+v);
                            }
                        }
                        foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))
                            Require(mf.sharedMesh!=null && EditorUtility.IsPersistent(mf.sharedMesh),"런타임 생성 메시를 사용함: "+kind+"/"+mf.name);
                        Require(turret!=null && barrel!=null && fire!=null,"프리팹 조준축 누락: "+kind);
                        var drive=root.GetComponent<TankDrive>();
                        Require(drive!=null && (drive.Hover || drive.Wheels.Count>0),"저장된 바퀴 연결 누락: "+kind);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
                    foreach(ShellKind shell in Enum.GetValues(typeof(ShellKind)))
                    {
                        var shellPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"Shells/"+kind+"_"+shell+".prefab");
                        Require(shellPrefab!=null,"발사체 프리팹 누락: "+kind+"/"+shell);
                        Require(EditorUtility.IsPersistent(ProceduralTank.Shell(kind,shell)),"발사체가 저장된 메시를 사용하지 않음: "+kind+"/"+shell);
                        shells++;
                    }
                    tanks++;
                }
                Require(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Project/Scenes/TankArtGallery.unity")!=null,"모델 비교 씬 누락");
                Debug.Log($"[NativeTankAssets] PASS: {tanks} FBX/tank prefabs, {shells} shell prefabs; runtime uses persistent meshes; gallery scene exists");
            }
            finally { UnityEngine.Object.DestroyImmediate(body); UnityEngine.Object.DestroyImmediate(track); UnityEngine.Object.DestroyImmediate(team); }
        }
    }
}
