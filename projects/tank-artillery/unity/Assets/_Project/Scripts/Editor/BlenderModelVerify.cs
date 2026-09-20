using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Tankfall.Sim;
using Tankfall.View;

namespace Tankfall.EditorTools
{
    public static class BlenderModelVerify
    {
        static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        [MenuItem("Tankfall/Verify Blender Models")]
        public static void Run()
        {
            int count = 0, triangles = 0;
            var fingerprints = new HashSet<string>();
            var material = new Material(Shader.Find("Standard"));
            foreach (TankKind kind in Enum.GetValues(typeof(TankKind)))
            {
                foreach (string name in new[] { kind.ToString(), kind + "_Normal", kind + "_Special" })
                {
                    var model = BlenderModels.Load(name);
                    Require(model != null, "Blender 모델 없음: " + name);
                    Require(model.nodes[0].parent == -1, "루트 오류: " + name);
                    for (int ni = 0; ni < model.nodes.Length; ni++)
                    {
                        var n = model.nodes[ni];
                        Require(n.parent < ni && (ni == 0 || n.parent >= 0), "부모 순서 오류: " + name);
                        Require(n.vertices.Length == n.normals.Length && n.vertices.Length % 3 == 0, "노멀 누락: " + name);
                        Require(n.triangles.Length % 3 == 0, "삼각형 오류: " + name);
                        foreach (float v in n.vertices) Require(!float.IsNaN(v) && !float.IsInfinity(v), "정점 수치 오류: " + name);
                        foreach (int t in n.triangles) Require(t >= 0 && t < n.vertices.Length / 3, "정점 인덱스 오류: " + name);
                        // 좌표 변환은 determinant +1인 회전이다. 면 외적과 외향 노멀이 일치해야 한다.
                        for (int t = 0; t < n.triangles.Length; t += 3)
                        {
                            Vector3 Read(float[] data, int index) => new Vector3(data[index*3], data[index*3+1], data[index*3+2]);
                            int a = n.triangles[t], b = n.triangles[t+1], c = n.triangles[t+2];
                            var face = Vector3.Cross(Read(n.vertices,b)-Read(n.vertices,a), Read(n.vertices,c)-Read(n.vertices,a));
                            var normal = Read(n.normals,a)+Read(n.normals,b)+Read(n.normals,c);
                            Require(Vector3.Dot(face,normal) >= -.001f, "뒤집힌 면: " + name + "/" + n.name);
                        }
                        triangles += n.triangles.Length / 3;
                    }
                    count++;
                }
                var shape = TankShape.Of(kind);
                // 이 검사는 authored 시각 리그만 비교한다. 실제 탄도 기준은 TankShapeContract에서 검증한다.
                Require(BlenderModels.TryBuild(shape, material, material, material, null, null,
                    out var root, out var turret, out var barrel, out var fire), "모델 누락: " + kind);
                try
                {
                    Require(turret.parent == root && barrel.parent == turret && fire.parent == barrel, "회전 계층 오류: " + kind);
                    var drive = root.GetComponent<TankDrive>();
                    Require(drive != null && (drive.Hover || drive.Wheels.Count > 0 && drive.WheelRadius > 0), "주행 계층 오류: " + kind);
                    foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
                        Require(EditorUtility.IsPersistent(mf.sharedMesh) && AssetDatabase.GetAssetPath(mf.sharedMesh).StartsWith("Assets/_Project/Resources/TankModels/"), "저장된 모델 메시가 아님: " + kind);
                    float r = Mathf.Max(.42f, Mathf.Min(shape.TrackHeight*.88f,shape.BodyLength*.5f/shape.WheelCount*1.5f));
                    float by = r*.95f;
                    switch (ProceduralTank.ChassisOf(kind))
                    {
                        case Chassis.Cart: by = Mathf.Max(.9f,shape.BodyHeight*.95f)*.75f; break;
                        case Chassis.Carriage: by = Mathf.Max(.8f,shape.BodyHeight*.85f)*.8f; break;
                        case Chassis.Truck: by = Mathf.Max(.5f,shape.TrackHeight*.7f)*1.05f; break;
                        case Chassis.Hover: by = .55f+shape.BodyHeight*.55f*.9f; break;
                    }
                    var expectedTurret = new Vector3(0,by+shape.BodyHeight,-shape.BodyLength*.08f);
                    var expectedBarrel = new Vector3(0,shape.TurretHeight*.52f,shape.TurretRadius*1.45f*.45f);
                    if(kind==TankKind.Catapult) expectedBarrel=new Vector3(0,.87f,-1.82f);
                    if(kind==TankKind.SuperTank) expectedBarrel.y-=.42f;
                    float length = shape.BarrelLength;
                    switch (kind)
                    {
                        case TankKind.Catapult: length = 0; break;
                        case TankKind.Carrot: length *= 1.25f; break;
                        case TankKind.Missile: length = length*.84f+.6f; break;
                        case TankKind.Laser: length += 1; break;
                        case TankKind.Poseidon: length += .35f; break;
                        case TankKind.SecWind: length *= 1.05f; break;
                    }
                    // Authored pivots follow the orthographic construction, not the former generic hull.
                    switch(kind)
                    {
                        case TankKind.Catapult: expectedTurret=new Vector3(0,2.0f,0); expectedBarrel=new Vector3(0,1.03f,-1.30f); length=0; break;
                        case TankKind.CrossBow: expectedTurret=new Vector3(0,1.90f,-.30f); expectedBarrel=new Vector3(0,.25f,-1.05f); length=.70f; break;
                        case TankKind.Cannon: expectedTurret=new Vector3(0,1.80f,0); expectedBarrel=new Vector3(0,-.20f,0); length=1.932f; break;
                        case TankKind.Carrot: expectedTurret=new Vector3(0,1.70f,0); expectedBarrel=new Vector3(0,-.39f,-.52f); length=2.57f; break;
                        case TankKind.Duke: expectedTurret=new Vector3(0,1.60f,0); expectedBarrel=new Vector3(0,.12f,1.0f); length=.35f; break;
                        case TankKind.MineLander: expectedTurret=new Vector3(0,1.93f,-.70f); expectedBarrel=new Vector3(0,.12f,0); length=1.23f; break;
                        case TankKind.Missile: expectedTurret=new Vector3(0,1.65f,0); expectedBarrel=Vector3.zero; length=2.12f; break;
                        case TankKind.MultiMissile: expectedTurret=new Vector3(0,2.48f,-.30f); expectedBarrel=new Vector3(0,.10f,0); length=.88f; break;
                        case TankKind.SuperTank: expectedTurret=new Vector3(0,1.35f,0); expectedBarrel=new Vector3(0,.16f,.94f); length=1.15f; break;
                        case TankKind.Laser: expectedTurret=new Vector3(0,1.24f,0); expectedBarrel=Vector3.zero; length=2.606f; break;
                        case TankKind.IonAttacker: expectedTurret=new Vector3(0,1.89f,0); expectedBarrel=new Vector3(0,0,.80f); length=.65f; break;
                        case TankKind.Poseidon: expectedTurret=new Vector3(0,2.47f,.02f); expectedBarrel=new Vector3(0,0,.44f); length=1.51f; break;
                        case TankKind.SecWind: expectedTurret=new Vector3(0,1.60f,0); expectedBarrel=new Vector3(0,.12f,1.0f); length=.65f; break;
                    }
                    Require(Vector3.Distance(turret.localPosition,expectedTurret)<.001f, "포탑 원점 변경: " + kind);
                    Require(Vector3.Distance(barrel.localPosition,expectedBarrel)<.001f, "포신 원점 변경: " + kind);
                    var expectedFire=new Vector3(0,kind==TankKind.Laser?-.86f:0,length);
                    if(kind==TankKind.Carrot) expectedFire.y=.45f; // authored visual marker only; TankShape aim is independently verified.
                    Require(Vector3.Distance(fire.localPosition,expectedFire)<.001f, "발사 원점 변경: " + kind);
                    foreach (float pitch in new[] { TankStats.Get(kind).MinPitch, TankStats.Get(kind).MaxPitch })
                    {
                        turret.localRotation = Quaternion.Euler(0,73,0); barrel.localRotation = Quaternion.Euler(-pitch,0,0);
                        var expected = expectedTurret + turret.localRotation * (expectedBarrel + barrel.localRotation * expectedFire);
                        Require(Vector3.Distance(fire.position,expected)<.001f, "조준 후 발사점 불일치: " + kind);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
                foreach (ShellKind shell in Enum.GetValues(typeof(ShellKind)))
                {
                    var mesh = ProceduralTank.Shell(kind,shell);
                    Require(EditorUtility.IsPersistent(mesh) && AssetDatabase.GetAssetPath(mesh).StartsWith("Assets/_Project/Resources/TankModels/"), "저장된 발사체 미사용: " + kind);
                    Require(mesh.colors.Length==mesh.vertexCount, "발사체 재질색 누락: " + kind);
                    string fp = mesh.vertexCount + "/" + mesh.bounds.size.ToString("F3");
                    Require(fingerprints.Add(fp), "발사체 형태 중복: " + kind + "/" + shell);
                }
            }
            Require(BlenderModels.ShellMaterial.shader.isSupported,"발사체 셰이더 미지원");
            UnityEngine.Object.DestroyImmediate(material);
            Debug.Log($"[BlenderModels] PASS: {count} assets, {triangles} triangles; 13 rigs/aim anchors/wheels, 26 unique colored shells");
        }

        static void CheckPixels(Texture2D tile, string name)
        {
            int magenta=0, colored=0;
            foreach(var c in tile.GetPixels())
            {
                if(c.r>.95f && c.g<.08f && c.b>.95f) magenta++;
                if(Mathf.Max(c.r,Mathf.Max(c.g,c.b))-Mathf.Min(c.r,Mathf.Min(c.g,c.b))>.12f
                    && Mathf.Max(c.r,Mathf.Max(c.g,c.b))>.25f) colored++;
            }
            Require(magenta<8,"분홍색 오류 재질 감지: "+name);
            Require(colored>8,"재질색이 없는 렌더: "+name);
        }

        [MenuItem("Tankfall/Render Blender Model Review")]
        public static void RenderReview()
        {
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath,"../../output/blender/unity"));
                Directory.CreateDirectory(dir);
                var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                    UnityEditor.SceneManagement.NewSceneMode.Single);
                Run();
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.65f,.67f,.72f);
                var lightObject=new GameObject("Review light"); var light=lightObject.AddComponent<Light>();
                light.type=LightType.Directional; light.intensity=1.4f; light.transform.rotation=Quaternion.Euler(45,-35,0);
                var camObject=new GameObject("Review camera"); var camera=camObject.AddComponent<Camera>();
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.065f,.085f,.12f,0f);
                string portraits=Path.Combine(Application.dataPath,"_Project/Resources/Gui/Portraits");
                Directory.CreateDirectory(portraits);
                camera.orthographic=true; camera.orthographicSize=4.7f; camera.nearClipPlane=.1f; camera.farClipPlane=100;
                const int width=480,height=380;
                var target=new RenderTexture(width,height,24); camera.targetTexture=target;
                var sheet=new Texture2D(width*4,height*4,TextureFormat.RGB24,false);
                var background=new Color[width*4*height*4];
                for(int i=0;i<background.Length;i++) background[i]=camera.backgroundColor;
                sheet.SetPixels(background);
                var body=new Material(Shader.Find("Standard")); var dark=new Material(body){color=new Color(.07f,.08f,.10f)};
                var team=new Material(body){color=new Color(.16f,.48f,.96f)};
                for (int i=0;i<TankStats.Count;i++)
                {
                    var kind=(TankKind)i; body.color=TankShape.BodyColor(kind);
                    var root=ProceduralTank.Build(TankShape.Of(kind),body,dark,team,out var tur,out var bar,out var fp);
                    foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                        Require(renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null,
                            "렌더 재질 누락: " + kind + "/" + renderer.name);
                    bar.localRotation=Quaternion.Euler(-12,0,0);
                    var center=new Vector3(0,1.8f,.7f);
                    camera.transform.position=center+new Vector3(10,8,12); camera.transform.LookAt(center);
                    camera.Render(); RenderTexture.active=target;
                    var tile=new Texture2D(width,height,TextureFormat.RGBA32,false);
                    tile.ReadPixels(new Rect(0,0,width,height),0,0); tile.Apply();
                    CheckPixels(tile,kind.ToString());
                    File.WriteAllBytes(Path.Combine(dir,kind+".png"),tile.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(portraits,kind+".png"),tile.EncodeToPNG());
                    sheet.SetPixels((i%4)*width,(3-i/4)*height,width,height,tile.GetPixels());
                    UnityEngine.Object.DestroyImmediate(tile); UnityEngine.Object.DestroyImmediate(root.gameObject);
                }
                sheet.Apply(); File.WriteAllBytes(Path.Combine(dir,"tank_roster.png"),sheet.EncodeToPNG());
                camera.orthographicSize=1.8f;
                var shells=new Texture2D(width*7,height*4,TextureFormat.RGB24,false);
                var shellBackground=new Color[width*7*height*4];
                for(int i=0;i<shellBackground.Length;i++) shellBackground[i]=camera.backgroundColor;
                shells.SetPixels(shellBackground);
                for(int i=0;i<TankStats.Count;i++) for(int s=0;s<2;s++)
                {
                    var go=new GameObject("Shell review");
                    go.AddComponent<MeshFilter>().sharedMesh=ProceduralTank.Shell((TankKind)i,(ShellKind)s);
                    go.AddComponent<MeshRenderer>().sharedMaterial=BlenderModels.ShellMaterial;
                    camera.transform.position=new Vector3(3,2,4); camera.transform.LookAt(Vector3.zero);
                    camera.Render(); RenderTexture.active=target;
                    var tile=new Texture2D(width,height,TextureFormat.RGBA32,false); tile.ReadPixels(new Rect(0,0,width,height),0,0); tile.Apply();
                    if(i != (int)TankKind.Catapult || s != 0) CheckPixels(tile,((TankKind)i)+"/"+s);
                    shells.SetPixels((i%7)*width,(3-(i/7)*2-s)*height,width,height,tile.GetPixels());
                    UnityEngine.Object.DestroyImmediate(tile); UnityEngine.Object.DestroyImmediate(go);
                }
                shells.Apply(); File.WriteAllBytes(Path.Combine(dir,"projectile_roster.png"),shells.EncodeToPNG());
                RenderTexture.active=null; camera.targetTexture=null; target.Release();
                AssetDatabase.Refresh();
                GuiArt.VerifyAssets();
                Debug.Log("[CasualGUI] PASS: atlas, title art, 13 tank portraits");
                Debug.Log("[BlenderModels] Unity review rendered: "+dir);
            }
            finally { ShaderUtil.allowAsyncCompilation = previousAsync; }
        }
    }
}
