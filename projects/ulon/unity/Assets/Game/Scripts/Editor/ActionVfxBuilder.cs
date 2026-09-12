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

        public static void EnsureActionVfx()
        {
            var old = GameObject.Find(ActionVfx.RootObject);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);

            var root = new GameObject(ActionVfx.RootObject);
            var specs = ActionVfx.Specs;
            for (int i = 0; i < specs.Length; i++)
                ActionVfx.BuildChild(root.transform, specs[i], MaterialFor(specs[i]));
            Debug.Log("[Ulon] 행동 VFX 템플릿 " + specs.Length + "종 — 타격(주황 불티·튐)·회복(초록 원·떠오름)·제작(청보라 별)");
        }

        static Material MaterialFor(ActionVfx.Spec spec)
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
