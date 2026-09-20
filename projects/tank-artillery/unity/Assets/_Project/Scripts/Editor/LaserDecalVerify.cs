using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Tankfall.View;
namespace Tankfall.EditorTools {
 public static class LaserDecalVerify {
  public static void Run() {
   var prefab=Resources.Load<GameObject>("TankModels/Tanks/Laser");
   var go=UnityEngine.Object.Instantiate(prefab);
   try {
    var decals=go.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterial.shader.name=="Tankfall/ArmorDecal").ToArray();
    if(decals.Length!=1) throw new Exception("Laser UV decal material missing or not packed");
    var decal=decals[0]; var mesh=decal.GetComponent<MeshFilter>().sharedMesh;
    if(mesh.uv.Length!=mesh.vertexCount || mesh.uv.Distinct().Count()<100) throw new Exception("Laser decal UV lost");
    var texture=Resources.Load<Texture2D>("Art/LaserArmorDecal");
    if(decal.sharedMaterial.mainTexture!=texture || texture==null || !EditorUtility.IsPersistent(decal.sharedMaterial)) throw new Exception("Laser decal texture/material not persistent");
    var rig=go.GetComponent<NativeTankRig>();
    if(rig.Team.Length==0 || rig.Team.Contains(decal) || rig.Body.Contains(decal)) throw new Exception("Decal/team material roles overlap");
    var material=decal.sharedMaterial;
    foreach(var color in new[]{Color.red,Color.blue}) {
     var team=new Material(Resources.Load<Shader>("Art/SoftToy")){color=color};
     rig.Bind(null,null,team,null,null);
     foreach(var r in rig.Team) {var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);if(block.GetColor("_Color")!=color) throw new Exception("Team MPB failed");}
     if(decal.sharedMaterial!=material || decal.sharedMaterial.mainTexture!=texture) throw new Exception("Team paint replaced decal");
     UnityEngine.Object.DestroyImmediate(team);
    }
    Debug.Log("LASER_DECAL_VERIFY_PASS UV="+mesh.uv.Length+" texture="+texture.width+"x"+texture.height+" team_red_blue=PASS");
   } finally {UnityEngine.Object.DestroyImmediate(go);}
  }
 }
}
