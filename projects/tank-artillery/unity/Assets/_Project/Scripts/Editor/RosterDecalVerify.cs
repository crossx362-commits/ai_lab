using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Tankfall.View;
using Tankfall.Sim;
namespace Tankfall.EditorTools {
 public static class RosterDecalVerify {
  public static void Run(){
   int verified=0,decalsCount=0;
   foreach(TankKind kind in Enum.GetValues(typeof(TankKind))){
    var prefab=Resources.Load<GameObject>("TankModels/Tanks/"+kind);
    if(prefab==null)throw new Exception("Tank prefab missing: "+kind);
    var go=UnityEngine.Object.Instantiate(prefab);
    try {
     var rig=go.GetComponent<NativeTankRig>();
     var decals=go.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterial.shader.name=="Tankfall/ArmorDecal").ToArray();
     if(decals.Length==0)throw new Exception("No UV decal surfaces: "+kind);
     var texture=Resources.Load<Texture2D>("Art/"+kind+"ArmorDecal");
     foreach(var decal in decals){
      var mesh=decal.GetComponent<MeshFilter>().sharedMesh;
      if(mesh.uv.Length!=mesh.vertexCount || mesh.uv.Distinct().Count()<10)throw new Exception("Lost UV: "+kind);
      if(texture==null || decal.sharedMaterial.mainTexture!=texture || !EditorUtility.IsPersistent(decal.sharedMaterial))throw new Exception("Lost persistent decal asset: "+kind);
      if(rig.Team.Contains(decal) || rig.Body.Contains(decal))throw new Exception("Decal paint role collision: "+kind);
     }
     if(rig.Team.Length==0)throw new Exception("No team mark: "+kind);
     foreach(var color in new[]{Color.red,Color.blue}){
      var team=new Material(Resources.Load<Shader>("Art/SoftToy")){color=color};rig.Bind(null,null,team,null,null);
      foreach(var r in rig.Team){var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);if(block.GetColor("_Color")!=color)throw new Exception("Team MPB failed: "+kind);}
      foreach(var decal in decals)if(decal.sharedMaterial.mainTexture!=texture)throw new Exception("Team tint destroyed decal: "+kind);
      UnityEngine.Object.DestroyImmediate(team);
     }
     Debug.Log("ROSTER_UV_TEAM "+kind+" decal_renderers="+decals.Length+" team_renderers="+rig.Team.Length);
     verified++;decalsCount+=decals.Length;
    }finally{UnityEngine.Object.DestroyImmediate(go);}
   }
   Debug.Log("ROSTER_DECAL_VERIFY_PASS tanks="+verified+" decal_renderers="+decalsCount);
  }
 }
}
