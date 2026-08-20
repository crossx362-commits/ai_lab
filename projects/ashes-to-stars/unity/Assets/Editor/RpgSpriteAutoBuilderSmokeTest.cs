// RpgSpriteAutoBuilder 스모크 테스트 — 배치 모드에서 빌더 코어를 실제로 돌려
// 슬라이스·클립·컨트롤러가 진짜 생기는지 본다(로그 성공 ≠ 산출물 존재 계열 방지).
// 실행: Unity -batchmode -executeMethod RpgSpriteAutoBuilderSmokeTest.Run
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class RpgSpriteAutoBuilderSmokeTest
{
    public static void Run()
    {
        int code = 1;
        try
        {
            const string texPath = "Assets/TestSpriteSheets/healer_run.png";
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) throw new Exception("테스트 텍스처 없음: " + texPath);

            var builder = ScriptableObject.CreateInstance<RpgSpriteAutoBuilder>();
            Type t = typeof(RpgSpriteAutoBuilder);
            const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;

            t.GetField("spriteSheet", F).SetValue(builder, tex);
            t.GetMethod("GuessAnimationSettingsFromFilename", F).Invoke(builder, null);
            string msg = (string)t.GetMethod("BuildCore", F).Invoke(builder, null);
            Debug.Log("[SmokeTest] BuildCore: " + msg.Replace("\n", " / "));

            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(texPath)
                .OfType<Sprite>().ToArray();
            Debug.Log("[SmokeTest] sprites=" + sprites.Length);
            foreach (Sprite s in sprites.Take(3))
                Debug.Log("[SmokeTest] " + s.name +
                    " rect=" + s.rect + " pivot(px)=" + s.pivot);

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                "Assets/GeneratedAnimations/healer_run_Run.anim");
            var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(
                "Assets/GeneratedAnimations/healer.controller");
            Debug.Log("[SmokeTest] clip=" + (clip != null) +
                " length=" + (clip != null ? clip.length : 0f) +
                " controller=" + (ctrl != null));

            if (sprites.Length >= 2 && clip != null && ctrl != null)
            {
                Debug.Log("[SmokeTest] PASS");
                code = 0;
            }
            else
            {
                Debug.LogError("[SmokeTest] FAIL — 산출물 불충분");
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        EditorApplication.Exit(code);
    }
}
