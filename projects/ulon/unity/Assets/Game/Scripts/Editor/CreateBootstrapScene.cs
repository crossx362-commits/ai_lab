using Ulon.Client;
using Ulon.Server;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Ulon.Editor
{
    public static class CreateBootstrapScene
    {
        const string ScenePath = "Assets/Game/Scenes/Bootstrap.unity";

        [MenuItem("Ulon/Bootstrap And Self-Check")]
        public static void BootstrapAndCheck()
        {
            SliceSelfCheck.Run();
            VisualSliceBuilder.Build();
        }

        [MenuItem("Ulon/Create Bootstrap Scene")]
        public static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            VisualSliceBuilder.EnsureWorldAtmosphere();   // 대기는 원장 하나에서 — 여기 다시 적지 않는다
            Light sun = Object.FindAnyObjectByType<Light>();
            if (sun != null)
            {
                sun.type = LightType.Directional;
                sun.color = new Color(1f, 0.95f, 0.85f);
                sun.intensity = 1.05f;
                sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                sun.shadows = LightShadows.Soft;
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            Tint(ground, new Color(0.45f, 0.52f, 0.32f));

            var player = MakeCapsule("Player", new Vector3(0f, 1f, 0f), Vector3.one, new Color(0.25f, 0.42f, 0.78f));
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            var cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.5f;
            cc.center = Vector3.zero;
            player.AddComponent<ClickMotor>();
            player.AddComponent<LocalAvatar>();
            var playerBody = player.AddComponent<WorldBody>();
            playerBody.IsEnemy = false;
            playerBody.IsAvatar = true;
            playerBody.DisplayName = "나";
            playerBody.MaxHp = 50f;

            var monster = MakeCapsule("Goblin", new Vector3(4.5f, 0.65f, 3.2f), Vector3.one * 0.65f, new Color(0.32f, 0.55f, 0.28f));
            var monsterBody = monster.AddComponent<WorldBody>();
            monsterBody.IsEnemy = true;
            monsterBody.DisplayName = "고블린";
            monsterBody.MaxHp = 30f;

            var world = new GameObject("OfflineWorld");
            world.AddComponent<OfflineWorld>();
            world.AddComponent<SliceHud>();

            Camera cam = Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                var qv = cam.GetComponent<QuarterViewCamera>() ?? cam.gameObject.AddComponent<QuarterViewCamera>();
                // 실내 차폐 페이드도 씬에 박아 둔다 — 런타임에만 붙이면 에디터 검증이 못 본다(검수 2026-09-06 P0).
                if (cam.GetComponent<DungeonSightFade>() == null)
                    cam.gameObject.AddComponent<DungeonSightFade>();
                qv.SetFollow(player.transform);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[Ulon] Bootstrap scene saved: " + ScenePath);
        }

        static GameObject MakeCapsule(string name, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            Tint(go, color);
            return go;
        }

        static void Tint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
                return;
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", color);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            renderer.sharedMaterial = mat;
        }
    }
}
