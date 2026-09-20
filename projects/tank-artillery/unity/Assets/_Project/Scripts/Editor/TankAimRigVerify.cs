using System;
using UnityEditor;
using UnityEngine;
using Tankfall.Sim;
using Tankfall.View;
namespace Tankfall.EditorTools
{
    public static class TankAimRigVerify
    {
        public static void Run()
        {
            var material=new Material(Shader.Find("Unlit/Color"));
            int poses=0;
            bool original=ProceduralTank.ForceProcedural;
            try
            {
                for(int i=0;i<TankStats.Count;i++)
                {
                    var kind=(TankKind)i;var shape=TankShape.Of(kind);
                    ProceduralTank.ForceProcedural=false;
                    var live=ProceduralTank.Build(shape,material,material,material,out var lt,out var lb,out var lf);
                    ProceduralTank.ForceProcedural=true;
                    var baseline=ProceduralTank.Build(shape,material,material,material,out var bt,out var bb,out var bf);
                    ProceduralTank.ForceProcedural=false;
                    try
                    {
                        foreach(float yaw in new[]{-135f,0f,70f}) foreach(float pitch in new[]{-10f,35f,80f})
                        {
                            lt.localRotation=bt.localRotation=Quaternion.Euler(0,yaw,0);
                            lb.localRotation=bb.localRotation=Quaternion.Euler(-pitch,0,0);
                            Require(Vector3.Distance(lf.position,bf.position)<.0001f,"발사점 불일치 "+kind);
                            poses++;
                        }
                        var native=live.GetComponent<NativeTankRig>();
                        var before=lf.position;
                        native.FirePoint.localPosition+=Vector3.one*100;
                        Require(Vector3.Distance(before,lf.position)<.0001f,"시각 포구가 탄도에 영향을 줌 "+kind);
                        if(kind==TankKind.Carrot)
                        {
                            Require(native.Turret.childCount==1 && native.Turret.GetChild(0)==native.Barrel,"당근 몸통 일부가 상하 회전 밖에 남음");
                            live.SendMessage("LateUpdate");int pupils=0;
                            foreach(var child in live.GetComponentsInChildren<Transform>()) if(child.name=="PupilGaze")
                            {pupils++;Require(child.localPosition.magnitude<.13f,"눈동자가 눈 밖으로 이동");Require(child.IsChildOf(native.Barrel),"눈이 몸통과 분리됨");}
                            Require(pupils==2,"당근 눈동자 리그 누락");
                        }
                    }
                    finally {UnityEngine.Object.DestroyImmediate(live.gameObject);UnityEngine.Object.DestroyImmediate(baseline.gameObject);}
                }
                Debug.Log("TANK_AIM_RIG_PASS "+poses+" poses; 13 authored-muzzle perturbations isolated; carrot body/pupils rig verified");
            }
            finally {ProceduralTank.ForceProcedural=original;UnityEngine.Object.DestroyImmediate(material);}
        }
        static void Require(bool ok,string reason){if(!ok)throw new Exception(reason);}
    }
}
