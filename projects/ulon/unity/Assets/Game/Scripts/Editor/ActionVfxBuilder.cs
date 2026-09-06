using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Ulon.Client;

namespace Ulon.Editor
{
    /// <summary>
    /// §11.2·§18.15 — 행동 결과 파티클 템플릿을 씬에 만든다(멱등: `ActionVfx`를 지우고 다시 짓는다).
    /// 스프라이트는 등록된 Kenney Particle Pack(CC0, 오너 승인 2026-09-06)에서 가져온다.
    /// 종류마다 **텍스처·색·형태**가 달라야 한다 — 같은 반짝임 세 개는 화면에서 한 가지로 읽힌다(검수 요구).
    /// </summary>
    public static class ActionVfxBuilder
    {
        const string PackTex = "Assets/_ThirdParty/Kenney/Particles/RAW/Textures/";

        public struct Spec
        {
            public ActionVfx.Kind Kind;
            public string Texture;      // 형태 축 — 스프라이트가 다르면 실루엣이 다르다
            public Color Tint;          // 색 축
            public float Size;
            public int Count;
            public float Speed;
            public float Gravity;       // 움직임 축 — 타격은 튀고, 회복은 떠오른다
            public float Spread;        // 방출 구 반경 — 크면 한 덩어리가 아니라 **흩어진다**
            public float Alpha;         // 불투명도 — 1이면 뒤가 안 비친다(대상을 덮는다)
        }

        public static readonly Spec[] Specs =
        {
            // 크기·개수는 **플레이 거리 기준**이다 — 검은 배경 코앞에서 잘 보이던 값(0.30~0.42m)은
            // 야외 12m 쿼터뷰에서 화면의 0.02~0.07%밖에 안 덮어 사실상 안 보였다(검수 랩 D 실측).
            new Spec { Kind = ActionVfx.Kind.Hit,   Texture = "spark_01.png", Tint = new Color(1f, 0.42f, 0.16f), Size = 1.00f, Count = 44, Speed = 5.5f, Gravity = 0.9f, Spread = 0.25f, Alpha = 1.00f },
            // 회복은 **대상을 보면서** 쓰는 것이다 — 불투명한 초록 덩어리가 대상을 덮으면 안 된다(검수 반려).
            // 크기를 줄이는 대신 **반투명하게, 넓게 흩어** 뒤가 비치게 한다.
            new Spec { Kind = ActionVfx.Kind.Heal,  Texture = "circle_05.png", Tint = new Color(0.36f, 1f, 0.55f), Size = 0.62f, Count = 64, Speed = 1.4f, Gravity = -0.35f, Spread = 0.95f, Alpha = 0.45f },
            new Spec { Kind = ActionVfx.Kind.Craft, Texture = "star_01.png",  Tint = new Color(0.60f, 0.62f, 1f), Size = 1.35f, Count = 40, Speed = 2.2f, Gravity = 0.2f, Spread = 0.35f, Alpha = 0.85f },
        };

        public static void EnsureActionVfx()
        {
            var old = GameObject.Find(ActionVfx.RootObject);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);

            var root = new GameObject(ActionVfx.RootObject);
            for (int i = 0; i < Specs.Length; i++)
                Build(root.transform, Specs[i]);
            Debug.Log("[Ulon] 행동 VFX 템플릿 " + Specs.Length + "종 — 타격(주황 불티·튐)·회복(초록 원·떠오름)·제작(청보라 별)");
        }

        static void Build(Transform parent, Spec spec)
        {
            var go = new GameObject(ActionVfx.ObjectFor(spec.Kind));
            go.transform.SetParent(parent, false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.55f;
            main.startSpeed = spec.Speed;
            main.startSize = spec.Size;
            main.startColor = new Color(spec.Tint.r, spec.Tint.g, spec.Tint.b, spec.Alpha);
            main.gravityModifier = spec.Gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 128;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)spec.Count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = spec.Spread > 0f ? spec.Spread : 0.25f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = MaterialFor(spec);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            go.SetActive(false);            // 템플릿은 꺼 둔다 — 재생은 복제본이 한다
        }

        static Material MaterialFor(Spec spec)
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Game/Art/VFX"));
            string matPath = "Assets/Game/Art/VFX/Vfx" + spec.Kind + ".mat";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PackTex + spec.Texture);
            if (tex == null)
                throw new InvalidOperationException("파티클 스프라이트가 없습니다: " + PackTex + spec.Texture +
                    " (등록 CC0 팩에서만 쓴다 — 자격 원장 docs/ASSET_REGISTER.md)");
            var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = shader;
            mat.mainTexture = tex;
            mat.color = spec.Tint;
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", spec.Tint);
            if (mat.HasProperty("_Mode"))
                mat.SetFloat("_Mode", 4f);          // Additive — 어두운 던전에서 불티가 읽히게
            mat.EnableKeyword("_ALPHABLEND_ON");
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
