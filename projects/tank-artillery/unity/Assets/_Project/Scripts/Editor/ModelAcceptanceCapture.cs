using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Tankfall.Sim;
using Tankfall.View;

namespace Tankfall.EditorTools
{
    // 고정 카메라/배율. 자동 외곽 맞춤이나 모델별 왜곡을 하지 않는다.
    public static class ModelAcceptanceCapture
    {
        public static void Run()
        {
            var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../output/model-acceptance"));
            Directory.CreateDirectory(output);
            var shader=Resources.Load<Shader>("Art/SoftToy");
            var camera=new GameObject("AcceptanceCamera").AddComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.96f,.94f,.88f);
            camera.nearClipPlane=.1f; camera.farClipPlane=100; camera.fieldOfView=60;
            var light=new GameObject("AcceptanceKey").AddComponent<Light>();
            light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(45,-35,0);
            RenderSettings.ambientLight=new Color(.5f,.5f,.5f);
            var target=new RenderTexture(640,640,24);camera.targetTexture=target;
            string selected=System.Environment.GetEnvironmentVariable("TANKFALL_CAPTURE_KIND");
            if(!string.IsNullOrEmpty(selected) && !Enum.IsDefined(typeof(TankKind),selected))
                throw new ArgumentException("Unknown capture kind: "+selected);
            int count=0;
            try
            {
                for(int i=0;i<TankStats.Count;i++)
                {
                    var kind=(TankKind)i;
                    if(!string.IsNullOrEmpty(selected) && selected!=kind.ToString()) continue;
                    var body=new Material(shader){color=TankShape.BodyColor(kind)};
                    var tracks=new Material(shader){color=new Color(.065f,.085f,.105f)};
                    var team=new Material(shader){color=new Color(.18f,.52f,.94f)};
                    var root=ProceduralTank.Build(TankShape.Of(kind),body,tracks,team,out var turret,out var barrel,out _);
                    try
                    {
                        foreach(var view in new[]{"concept","concept35","concept60","combat22","combat40"})
                        {
                            turret.localRotation=Quaternion.identity;
                            barrel.localRotation=Quaternion.Euler(view=="concept60"?-60:view=="concept35"?-35:-12,0,0);
                            root.SendMessage("LateUpdate",SendMessageOptions.DontRequireReceiver);
                            camera.orthographic=view.StartsWith("concept");
                            if(camera.orthographic)
                            {
                                camera.orthographicSize=3.3f;
                                camera.transform.position=new Vector3(7,4.4f,10);
                                camera.transform.LookAt(new Vector3(0,1.6f,0));
                            }
                            else
                            {
                                var focus=Vector3.up*2.2f;
                                camera.transform.position=focus+Quaternion.Euler(18,0,0)*Vector3.back*(view=="combat40"?40:22);
                                camera.transform.LookAt(focus);
                            }
                            camera.Render();RenderTexture.active=target;
                            var image=new Texture2D(640,640,TextureFormat.RGB24,false);
                            image.ReadPixels(new Rect(0,0,640,640),0,0);image.Apply();
                            File.WriteAllBytes(Path.Combine(output,kind+"-"+view+".png"),image.EncodeToPNG());
                            UnityEngine.Object.DestroyImmediate(image);
                        }
                        count++;
                    }
                    finally {UnityEngine.Object.DestroyImmediate(root.gameObject);UnityEngine.Object.DestroyImmediate(body);UnityEngine.Object.DestroyImmediate(tracks);UnityEngine.Object.DestroyImmediate(team);}
                }
                File.WriteAllText(Path.Combine(output,"capture.txt"),BlenderModels.RosterLabel+"\n"+count+" kinds x concept12/concept35/concept60/combat22/combat40. 640x640. Combat pitch12. Combat FOV60, camera pitch18, focus height2.2. Art fit NOT individually adjusted. This is a diagnostic capture, not visual approval.");
                Debug.Log("MODEL_ACCEPTANCE_CAPTURE "+count+" kinds / "+count*5+" frames / "+BlenderModels.RosterLabel);
            }
            finally {RenderTexture.active=null;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(camera.gameObject);UnityEngine.Object.DestroyImmediate(light.gameObject);}
        }
    }
}
