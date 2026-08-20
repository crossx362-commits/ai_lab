// RpgSpriteAutoBuilder 일괄 실행 — Assets/TestSpriteSheets 의 모든 시트를
// 파일명 자동 추정으로 슬라이스하고 클립·컨트롤러를 만든다.
// 실행: Unity -batchmode -executeMethod RpgSpriteBatchRunner.Run
using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class RpgSpriteBatchRunner
{
    public static void Run()
    {
        int ok = 0, fail = 0;
        Type t = typeof(RpgSpriteAutoBuilder);
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
        var builder = ScriptableObject.CreateInstance<RpgSpriteAutoBuilder>();

        string[] paths = AssetDatabase.FindAssets(
                "t:Texture2D", new[] { "Assets/TestSpriteSheets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => p)
            .ToArray();

        foreach (string p in paths)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(p);
            if (tex == null)
                continue;

            string name = System.IO.Path.GetFileNameWithoutExtension(p);

            try
            {
                t.GetField("spriteSheet", F).SetValue(builder, tex);
                t.GetMethod("GuessAnimationSettingsFromFilename", F)
                    .Invoke(builder, null);

                // idle_01 같은 변형 시트는 추정 상태명이 원본과 겹쳐
                // 마지막 빌드가 컨트롤러 상태를 덮는다 — 변형 번호를 상태명에 붙인다.
                Match m = Regex.Match(name, @"[_\- ](\d+)$");
                if (m.Success)
                {
                    FieldInfo an = t.GetField("animationName", F);
                    an.SetValue(builder,
                        (string)an.GetValue(builder) + "_" + m.Groups[1].Value);
                }

                string msg = (string)t.GetMethod("BuildCore", F).Invoke(builder, null);
                Debug.Log("[BatchRunner] " + name + " → " + msg.Replace("\n", " / "));
                ok++;
            }
            catch (Exception ex)
            {
                Exception root = ex.InnerException ?? ex;
                Debug.LogError("[BatchRunner] FAIL " + name + " : " + root.Message);
                fail++;
            }
        }

        Debug.Log("[BatchRunner] done ok=" + ok + " fail=" + fail);
        EditorApplication.Exit(fail == 0 && ok > 0 ? 0 : 1);
    }
}
