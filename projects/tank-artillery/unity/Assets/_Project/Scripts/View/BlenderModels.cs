// 게임은 TankModels의 저장된 Unity 프리팹/메시를 사용한다.
// JSON 로더는 Blender에서 내보낸 원본을 에디터에서 베이크하는 경로다.
using System;
using System.Collections.Generic;
using UnityEngine;
using Tankfall.Sim;

namespace Tankfall.View
{
    public static class BlenderModels
    {
        [Serializable] public sealed class Node
        {
            public string name, material;
            public int parent;
            public float[] position, rotation, scale, vertices, normals, uv;
            public int[] triangles;
            [NonSerialized] public Mesh mesh;
        }
        [Serializable] public sealed class Model
        {
            public int version;
            public string name;
            public float wheelRadius;
            public bool hover;
            public Node[] nodes;
        }
        static readonly Dictionary<string, Model> Models = new Dictionary<string, Model>();
        static readonly Dictionary<string, Mesh> Shells = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material> Details = new Dictionary<string, Material>();
        static Material _shellMaterial;
        static bool _reportedRoster;
        public static string RosterLabel
        {
            get
            {
                int models=0;
                for(int i=0;i<TankStats.Count;i++)
                    if(Resources.Load<GameObject>("TankModels/Tanks/"+(TankKind)i)!=null) models++;
                return $"모델 {models}종 · 코드 {TankStats.Count-models}종";
            }
        }
#if UNITY_EDITOR
        public static void ResetImportCache() { Models.Clear(); Shells.Clear(); }
#endif


        public static Model Load(string name)
        {
            if (Models.TryGetValue(name, out var found)) return found;
            var text = Resources.Load<TextAsset>("BlenderModels/" + name);
            if (text == null) return null;
            var model = JsonUtility.FromJson<Model>(text.text);
            if (model == null || model.version != 1 || model.nodes == null || model.nodes.Length == 0)
                throw new InvalidOperationException("Blender 모델 형식 오류: " + name);
            Models.Add(name, model);
            return model;
        }
        static Vector3 V(float[] f, int i = 0) => new Vector3(f[i], f[i + 1], f[i + 2]);
        static Vector3[] Vectors(float[] f)
        {
            var a = new Vector3[f.Length / 3];
            for (int i = 0; i < a.Length; i++) a[i] = V(f, i * 3);
            return a;
        }
        static Mesh MeshOf(Node n)
        {
            if (n.mesh != null) return n.mesh;
            var m = new Mesh { name = "Blender_" + n.name };
            if (n.vertices.Length / 3 > 65535) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices = Vectors(n.vertices); m.normals = Vectors(n.normals); m.triangles = n.triangles;
            if(n.uv != null && n.uv.Length > 0)
            {
                if(n.uv.Length != m.vertexCount*2) throw new InvalidOperationException("Blender UV 길이 오류: "+n.name);
                var coords=new Vector2[m.vertexCount];
                for(int i=0;i<coords.Length;i++) coords[i]=new Vector2(n.uv[i*2],n.uv[i*2+1]);
                m.uv=coords;
            }
            m.RecalculateBounds(); n.mesh = m; return m;
        }
        public static bool TryBuild(TankShape shape, Material body, Material track, Material team,
                                    Material wood, Material snow, out Transform root, out Transform turret,
                                    out Transform barrel, out Transform fire, bool usePrefab = true)
        {
            root = turret = barrel = fire = null;
            var toyShader = Resources.Load<Shader>("Art/SoftToy");
            foreach(var material in new[] { body, track, team, wood, snow })
                if(material!=null) material.shader=toyShader;
            if(usePrefab)
            {
                if(!_reportedRoster) { Debug.Log("[Tankfall] "+RosterLabel); _reportedRoster=true; }
                var prefab=Resources.Load<GameObject>("TankModels/Tanks/"+shape.Kind);
                if(prefab==null)
                {
                    if(Resources.Load<TextAsset>("BlenderModels/"+shape.Kind)!=null)
                        throw new InvalidOperationException("모델 원본은 있지만 Unity 프리팹 누락: "+shape.Kind+". 코드 폴백 금지; 네이티브 베이크 필요");
                    Debug.LogWarning("모델 파일 없음 — 임시 코드 표시: "+shape.Kind+" · "+RosterLabel);
                    return false;
                }
                var instance=UnityEngine.Object.Instantiate(prefab); instance.name="Tank";
                var rig=instance.GetComponent<NativeTankRig>();
                if(rig==null || rig.Turret==null || rig.Barrel==null || rig.FirePoint==null)
                { UnityEngine.Object.DestroyImmediate(instance); throw new InvalidOperationException("프리팹 리그 누락: "+shape.Kind); }
                rig.Bind(body,track,team,wood,snow);
                root=instance.transform; turret=rig.Turret; barrel=rig.Barrel; fire=rig.FirePoint;
                return true;
            }
            var model = Load(shape.Kind.ToString());
            if (model == null) { Debug.LogWarning("Blender 모델 없음, 기존 모델 사용: " + shape.Kind); return false; }
            var toy = Resources.Load<Shader>("Art/SoftToy");
            if (toy == null) throw new InvalidOperationException("장난감 재질 셰이더 누락");
            foreach (var material in new[] { body, track, team, wood, snow })
                if (material != null) material.shader = toy;
            var transforms = new Transform[model.nodes.Length];
            for (int i = 0; i < model.nodes.Length; i++)
            {
                var n = model.nodes[i];
                var go = new GameObject(n.name);
                var t = go.transform; transforms[i] = t;
                if (n.parent >= 0) t.SetParent(transforms[n.parent], false);
                t.localPosition = V(n.position);
                t.localRotation = n.rotation != null && n.rotation.Length==4 ? new Quaternion(n.rotation[0],n.rotation[1],n.rotation[2],n.rotation[3]) : Quaternion.identity;
                t.localScale = n.scale != null && n.scale.Length==3 ? V(n.scale) : Vector3.one;
                if (n.name == "Turret") turret = t;
                else if (n.name == "Barrel") barrel = t;
                else if (n.name == "FirePoint") fire = t;
                if (n.vertices.Length == 0) continue;
                if (n.material == "Snow" && snow == null) { go.SetActive(false); continue; }
                go.AddComponent<MeshFilter>().sharedMesh = MeshOf(n);
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.receiveShadows = false; // 작은 곡면에서 자기 그림자 얼룩을 피하고 캐릭터 색을 유지한다.
                renderer.sharedMaterial = n.material == "Body" ? body : n.material == "Team" ? team
                    : n.material == "Rubber" ? track : n.material == "Wood" ? (wood != null ? wood : Detail("Wood"))
                    : n.material == "Snow" ? snow : Detail(n.material);
            }
            root = transforms[0]; root.name = "Tank";
            if (turret == null || barrel == null || fire == null)
                throw new InvalidOperationException("Blender 회전축/발사점 누락: " + shape.Kind);
            var drive = root.gameObject.AddComponent<TankDrive>();
            drive.Hover = model.hover; drive.WheelRadius = model.wheelRadius;
            for (int i = 0; i < model.nodes.Length; i++)
                if (model.nodes[i].name == "Wheel") drive.Wheels.Add(transforms[i]);
            return true;
        }
        public static Transform BuildDecoration(string name)
        {
            var model=Load(name);
            if(model==null) throw new InvalidOperationException("맵 장식 누락: "+name);
            var nodes=new Transform[model.nodes.Length];
            for(int i=0;i<model.nodes.Length;i++)
            {
                var node=model.nodes[i]; var go=new GameObject(node.name); nodes[i]=go.transform;
                if(node.parent>=0) nodes[i].SetParent(nodes[node.parent],false);
                nodes[i].localPosition=V(node.position);
                if(node.vertices.Length==0) continue;
                go.AddComponent<MeshFilter>().sharedMesh=MeshOf(node);
                var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=Detail(node.material);
            }
            return nodes[0];
        }
        public static Mesh Shell(TankKind kind, ShellKind shell, bool useAsset = true)
        {
            string name = kind + "_" + shell;
            if(useAsset)
            {
                var saved=Resources.Load<Mesh>("TankModels/Shells/"+name);
                if(saved==null) throw new InvalidOperationException("Unity 발사체 메시 누락: "+name);
                return saved;
            }
            if (Shells.TryGetValue(name, out var cached) && cached != null) return cached;
            var model = Load(name); if (model == null) return null;
            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var colors = new List<Color>(); var triangles = new List<int>();
            var positions = new Vector3[model.nodes.Length];
            for (int i = 0; i < model.nodes.Length; i++)
            {
                var n = model.nodes[i]; positions[i] = V(n.position) + (n.parent < 0 ? Vector3.zero : positions[n.parent]);
                int start = vertices.Count;
                Color color = n.material == "Body" ? TankShape.BodyColor(kind) : Palette(n.material);
                for (int j = 0; j < n.vertices.Length; j += 3)
                {
                    vertices.Add(V(n.vertices, j) + positions[i]); normals.Add(V(n.normals, j)); colors.Add(color);
                }
                foreach (int index in n.triangles) triangles.Add(start + index);
            }
            var mesh = new Mesh { name = "Blender_" + name };
            if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); Shells[name] = mesh; return mesh;
        }
        public static Material ShellMaterial
        {
            get
            {
                if (_shellMaterial != null) return _shellMaterial;
                var shader = Resources.Load<Shader>("BlenderModels/ShellVertexColor");
                if (shader == null) throw new InvalidOperationException("Blender 발사체 셰이더 누락");
                _shellMaterial = new Material(shader) { name = "Blender shell palette" };
                return _shellMaterial;
            }
        }
        static Material Detail(string name)
        {
            if (Details.TryGetValue(name, out var m) && m != null) return m;
            if(name.EndsWith("Decal", StringComparison.Ordinal))
            {
                var shader=Resources.Load<Shader>("Art/ArmorDecal");
                var kind=name.Substring(0,name.Length-"Decal".Length);
                var texture=Resources.Load<Texture2D>("Art/"+kind+"ArmorDecal");
                if(shader==null || texture==null) throw new InvalidOperationException(kind+" 데칼 셰이더/텍스처 누락");
                m=new Material(shader){name="Blender "+name,mainTexture=texture};
                Details[name]=m; return m;
            }
            m = new Material(Resources.Load<Shader>("Art/SoftToy")) { name = "Blender " + name, color = Palette(name) };
            m.SetFloat("_Glossiness", .36f);
            m.SetFloat("_Metallic", name == "Metal" || name == "Edge" ? .55f : .08f);
            if (name == "Energy" || name == "Hot" || name == "Poison")
            { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", m.color * .35f); }
            if(name=="GlassBottle")
            {
                m.shader=Shader.Find("Standard");m.color=new Color(.68f,.94f,.76f,.16f);
                m.SetFloat("_Mode",3);m.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);m.SetInt("_ZWrite",0);
                m.EnableKeyword("_ALPHABLEND_ON");m.DisableKeyword("_ALPHATEST_ON");m.renderQueue=3000;
                m.SetFloat("_Glossiness",.83f);
            }
            Details[name] = m; return m;
        }
        static Color Palette(string name)
        {
            switch (name)
            {
                case "LeafGreen": return new Color(.29f,.48f,.10f);
                case "Stone": return new Color(.44f,.415f,.38f);
                case "Rocket": return new Color(.92f,.23f,.075f);
                case "EyeWhite": return Color.white;
                case "Crimson": return new Color(.55f,.12f,.16f);
                case "Teal": return new Color(.08f,.43f,.43f);
                case "Navy": return new Color(.12f,.19f,.34f);
                case "Gold": return new Color(.88f,.57f,.16f);
                case "Plum": return new Color(.38f,.18f,.43f);
                case "Team": return new Color(.18f,.52f,.94f);
                case "Rubber": return new Color(.065f,.085f,.105f);
                case "Metal": return new Color(.22f,.28f,.33f);
                case "Edge": return new Color(.58f,.66f,.69f);
                case "Wood": return new Color(.34f,.17f,.07f);
                case "WoodLight": return new Color(.64f,.39f,.17f);
                case "Ivory": return new Color(.91f,.88f,.72f);
                case "Glass": return new Color(.1f,.3f,.36f);
                case "Energy": return new Color(.15f,.95f,.8f);
                case "Poison": return new Color(.48f,.88f,.13f);
                case "Hot": return new Color(1f,.22f,.035f);
                case "Snow": return new Color(.91f,.96f,1f);
                default: return new Color(.65f,.52f,.28f);
            }
        }
    }
}
