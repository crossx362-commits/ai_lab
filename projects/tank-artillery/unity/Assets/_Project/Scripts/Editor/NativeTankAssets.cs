using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Tankfall.Sim;
using Tankfall.View;

namespace Tankfall.EditorTools
{
    public static class NativeTankAssets
    {
        const string Root="Assets/_Project/Resources/TankModels";
        const string Art="Assets/_Project/Art/Tanks";
        static T Save<T>(T source,string path) where T:UnityEngine.Object
        {
            var existing=AssetDatabase.LoadAssetAtPath<T>(path);
            if(existing!=null)
            {
                if(source is Mesh from && existing is Mesh to)
                {
                    // CopySerialized changes CPU data without reliably invalidating Metal's old buffers.
                    to.Clear(); to.indexFormat=from.indexFormat;
                    to.vertices=from.vertices; to.normals=from.normals; to.colors=from.colors;
                    to.uv=from.uv; to.triangles=from.triangles; to.bounds=from.bounds;
                    to.UploadMeshData(false);
                }
                else EditorUtility.CopySerialized(source,existing);
                existing.name=Path.GetFileNameWithoutExtension(path);
                EditorUtility.SetDirty(existing); return existing;
            }
            var copy=UnityEngine.Object.Instantiate(source); copy.name=source.name;
            AssetDatabase.CreateAsset(copy,path); return copy;
        }
        static Renderer[] WithMaterial(Transform root,Material material) => root.GetComponentsInChildren<Renderer>(true).Where(r=>r.sharedMaterial==material).ToArray();

        [MenuItem("Tankfall/Rebuild Native 3D Assets")]
        public static void Rebuild()
        {
            if(EditorApplication.isPlaying) throw new InvalidOperationException("에셋 생성은 편집 모드에서 실행해야 합니다.");
            foreach(string dir in new[]{Root+"/Tanks",Root+"/Meshes",Root+"/Shells",Art+"/FBX",Art+"/Materials",Art+"/Concepts"}) Directory.CreateDirectory(dir);
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            foreach(var file in Directory.GetFiles(Path.Combine(project,"art/blender/exports"),"*.fbx"))
                File.Copy(file,Art+"/FBX/"+Path.GetFileName(file),true);
            File.Copy(Path.Combine(project,"art/concepts/tankfall-character-sheet.png"),Art+"/Concepts/CharacterSheet.png",true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BlenderModels.ResetImportCache();
            var shader=Resources.Load<Shader>("Art/SoftToy");
            var shellMaterial=Save(BlenderModels.ShellMaterial,Art+"/Materials/ShellPalette.mat");
            foreach(TankKind kind in Enum.GetValues(typeof(TankKind)))
            {
                var body=new Material(shader){name="Body",color=TankShape.BodyColor(kind)};
                var track=new Material(shader){name="Rubber",color=new Color(.065f,.085f,.105f)};
                var team=new Material(shader){name="Team",color=new Color(.18f,.52f,.94f)};
                var wood=new Material(shader){name="Wood",color=new Color(.34f,.17f,.07f)};
                var snow=new Material(shader){name="Snow",color=new Color(.91f,.96f,1)};
                BlenderModels.TryBuild(TankShape.Of(kind),body,track,team,wood,snow,out var root,out var turret,out var barrel,out var fire,false);
                try
                {
                    root.name=kind.ToString();
                    var rig=root.gameObject.AddComponent<NativeTankRig>();
                    rig.Turret=turret; rig.Barrel=barrel; rig.FirePoint=fire;
                    var drive=root.GetComponent<TankDrive>();
                    rig.Wheels=drive.Wheels.ToArray(); rig.WheelRadius=drive.WheelRadius; rig.Hover=drive.Hover;
                    rig.Body=WithMaterial(root,body); rig.Tracks=WithMaterial(root,track); rig.Team=WithMaterial(root,team);
                    rig.Wood=WithMaterial(root,wood); rig.Snow=WithMaterial(root,snow);
                    var materials=new Dictionary<Material,Material>();
                    int index=0;
                    foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        mf.sharedMesh=Save(mf.sharedMesh,Root+"/Meshes/"+kind+"_"+(index++)+".asset");
                        var renderer=mf.GetComponent<Renderer>(); var source=renderer.sharedMaterial;
                        if(!materials.TryGetValue(source,out var saved))
                        {
                            string key=source.name.Replace("Blender ","").Replace(" ","_");
                            saved=Save(source,Art+"/Materials/"+kind+"_"+key+".mat"); materials.Add(source,saved);
                        }
                        renderer.sharedMaterial=saved;
                    }
                    foreach(var r in rig.Snow) r.gameObject.SetActive(false);
                    PrefabUtility.SaveAsPrefabAsset(root.gameObject,Root+"/Tanks/"+kind+".prefab");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root.gameObject);
                    foreach(var m in new[]{body,track,team,wood,snow}) UnityEngine.Object.DestroyImmediate(m);
                }
                foreach(ShellKind shell in Enum.GetValues(typeof(ShellKind)))
                {
                    string name=kind+"_"+shell;
                    var mesh=Save(BlenderModels.Shell(kind,shell,false),Root+"/Shells/"+name+".asset");
                    var go=new GameObject(name); go.AddComponent<MeshFilter>().sharedMesh=mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial=shellMaterial;
                    PrefabUtility.SaveAsPrefabAsset(go,Root+"/Shells/"+name+".prefab");
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            // Finish after the editor applies prefab hierarchy replacement, not against loaded old instances.
            EditorApplication.delayCall += FinishRebuild;
        }
        [MenuItem("Tankfall/Render Native Gallery")]
        public static void FinishRebuild()
        {
            CreateGallery();
            NativeTankAssetsVerify.Run();
            BlenderModelVerify.Run();
            Debug.Log("[NativeTankAssets] REBUILD COMPLETE — 13 tank prefabs / 26 shell prefabs / FBX / materials / gallery");
        }

        static void Layer(Transform root,int layer)
        { foreach(var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=layer; }

        static void CreateGallery()
        {
            // Additive scene avoids replacing or saving the user's open scene.
            var previous=SceneManager.GetActiveScene();
            // Unity refuses additive scenes while an untitled scene exists. Preserve it first.
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var open=SceneManager.GetSceneAt(i);
                if(string.IsNullOrEmpty(open.path))
                {
                    Directory.CreateDirectory("Assets/_Project/Scenes/EditorRecovery");
                    AssetDatabase.Refresh();
                    string recovery=AssetDatabase.GenerateUniqueAssetPath("Assets/_Project/Scenes/EditorRecovery/UntitledPreserved.unity");
                    if(!EditorSceneManager.SaveScene(open,recovery)) throw new IOException("열린 씬 보존 실패: "+recovery);
                    Debug.Log("[NativeTankAssets] 기존 미저장 씬 보존: "+recovery);
                }
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            const int layer=30;
            var camera=new GameObject("Gallery camera").AddComponent<Camera>();
            camera.orthographic=true; camera.orthographicSize=16;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.055f,.075f,.11f);
            camera.cullingMask=1<<layer;
            camera.transform.position=new Vector3(0,28,47); camera.transform.LookAt(new Vector3(0,0,0));
            var light=new GameObject("Studio key").AddComponent<Light>(); light.type=LightType.Directional;
            light.intensity=1.25f; light.transform.rotation=Quaternion.Euler(42,-32,0); light.cullingMask=1<<layer;
            var fill=new GameObject("Studio fill").AddComponent<Light>(); fill.type=LightType.Directional;
            fill.intensity=.55f; fill.transform.rotation=Quaternion.Euler(30,135,0); fill.cullingMask=1<<layer;
            var standMaterial=Save(new Material(Shader.Find("Standard")){name="Gallery stand",color=new Color(.08f,.12f,.19f)},Art+"/Materials/GalleryStand.mat");
            var tanks=new List<GameObject>();
            var staging=new List<GameObject>();
            for(int i=0;i<TankStats.Count;i++)
            {
                var kind=(TankKind)i;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Tanks/"+kind+".prefab");
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                var pos=new Vector3(i==12?0:(1.5f-i%4)*9f,0,(i/4-1.5f)*9f);
                go.transform.position=pos; go.transform.rotation=Quaternion.Euler(0,24,0);
                go.GetComponent<NativeTankRig>().Barrel.localRotation=Quaternion.Euler(-12,0,0);
                Layer(go.transform,layer); tanks.Add(go);
                var stand=GameObject.CreatePrimitive(PrimitiveType.Cylinder); stand.name=kind+" display plinth";
                staging.Add(stand);
                stand.layer=layer; stand.transform.position=pos+new Vector3(0,-.27f,0); stand.transform.localScale=new Vector3(6.8f,.2f,6.8f);
                stand.GetComponent<Renderer>().sharedMaterial=standMaterial;
                UnityEngine.Object.DestroyImmediate(stand.GetComponent<Collider>());
                var label=new GameObject(kind+" label"); label.layer=layer; label.transform.position=pos+new Vector3(0,.1f,3.6f);
                staging.Add(label);
                label.transform.rotation=Quaternion.Euler(65,0,0);
                var text=label.AddComponent<TextMesh>(); text.text=kind.ToString(); text.anchor=TextAnchor.MiddleCenter;
                text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.GetComponent<Renderer>().sharedMaterial=text.font.material;
                text.fontSize=64; text.characterSize=.11f; text.color=new Color(.94f,.91f,.79f);
            }
            EditorSceneManager.SaveScene(scene,"Assets/_Project/Scenes/TankArtGallery.unity");
            string output=Path.Combine(Application.dataPath,"../../output/native-models"); Directory.CreateDirectory(output);
            var target=new RenderTexture(1920,1600,24); camera.targetTexture=target;
            var prior=RenderTexture.active;
            try
            {
                camera.Render(); RenderTexture.active=target;
                var pixels=new Texture2D(1920,1600,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,1920,1600),0,0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(output,"gallery.png"),pixels.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(pixels);
                // Portraits use these same saved prefab assets, on an isolated layer.
                foreach(var go in tanks) go.SetActive(false);
                foreach(var go in staging) go.SetActive(false);
                camera.backgroundColor=Color.clear;
                RenderTexture.active=prior; camera.targetTexture=null;
                target.Release(); UnityEngine.Object.DestroyImmediate(target);
                target=new RenderTexture(480,380,24); camera.targetTexture=target; camera.orthographicSize=4.7f;
                for(int i=0;i<tanks.Count;i++)
                {
                    var go=tanks[i]; go.SetActive(true);
                    var renderers=go.GetComponentsInChildren<Renderer>();
                    var bounds=renderers[0].bounds;
                    foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    Vector3 center=bounds.center;
                    camera.transform.position=center+new Vector3(10,8,12); camera.transform.LookAt(center);
                    float halfWidth=0,halfHeight=0;
                    foreach(var filter in go.GetComponentsInChildren<MeshFilter>())
                    {
                        var meshBounds=filter.sharedMesh.bounds;
                        for(int corner=0;corner<8;corner++)
                        {
                            var extent=meshBounds.extents;
                            var point=meshBounds.center+new Vector3((corner&1)==0?-extent.x:extent.x,(corner&2)==0?-extent.y:extent.y,(corner&4)==0?-extent.z:extent.z);
                            var view=camera.transform.InverseTransformPoint(filter.transform.TransformPoint(point));
                            halfWidth=Mathf.Max(halfWidth,Mathf.Abs(view.x));halfHeight=Mathf.Max(halfHeight,Mathf.Abs(view.y));
                        }
                    }
                    camera.orthographicSize=Mathf.Max(halfHeight,halfWidth/(480f/380))*1.07f;
                    camera.Render(); RenderTexture.active=target;
                    var portrait=new Texture2D(480,380,TextureFormat.RGBA32,false);
                    portrait.ReadPixels(new Rect(0,0,480,380),0,0); portrait.Apply();
                    File.WriteAllBytes("Assets/_Project/Resources/Gui/Portraits/"+(TankKind)i+".png",portrait.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(portrait); go.SetActive(false);
                }
            }
            finally
            {
                RenderTexture.active=prior; camera.targetTexture=null; target.Release(); UnityEngine.Object.DestroyImmediate(target);
                SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene,true);
            }
            AssetDatabase.Refresh();
        }
    }
}
