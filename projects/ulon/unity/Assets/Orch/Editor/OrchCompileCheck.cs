using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Orch.EditorTools
{
    /// <summary>
    /// 배치 모드 컴파일 판정용 진입점.
    ///
    /// 중요: 이 메서드가 "실행됐다"는 사실 자체가 신호다 — Unity는 스크립트를 먼저 컴파일한 뒤
    /// executeMethod를 부르므로, 컴파일이 깨지면 이 메서드는 아예 불리지 않는다.
    /// 그래서 오케스트레이터는 (1) 아래 마커 출력 (2) 종료 코드 (3) 로그의 CS 오류
    /// 셋을 모두 본 뒤에야 PASS를 준다. 하나라도 어긋나면 UNKNOWN 또는 FAILED다.
    /// </summary>
    public static class OrchCompileCheck
    {
        private const string OkMarker = "ORCH_COMPILE_OK";
        private const string FailMarker = "ORCH_COMPILE_FAIL";

        public static void Run()
        {
            var reportPath = ArgValue("-orchReport");
            var sb = new StringBuilder();
            var ok = true;
            var assemblyCount = 0;

            try
            {
                var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
                assemblyCount = assemblies.Length;
                foreach (var asm in assemblies)
                {
                    // 어셈블리 산출물이 실제로 디스크에 있는지 — "컴파일했다"의 물증.
                    if (!File.Exists(asm.outputPath))
                    {
                        ok = false;
                        sb.AppendLine("missing output: " + asm.name + " -> " + asm.outputPath);
                    }
                }
            }
            catch (Exception e)
            {
                ok = false;
                sb.AppendLine("exception: " + e);
            }

            var json = "{\"ok\":" + (ok ? "true" : "false")
                       + ",\"assemblies\":" + assemblyCount
                       + ",\"unity\":\"" + Application.unityVersion + "\""
                       + ",\"notes\":\"" + sb.ToString().Replace("\\", "/").Replace("\"", "'").Replace("\n", " | ").Trim() + "\"}";

            if (!string.IsNullOrEmpty(reportPath))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                    File.WriteAllText(reportPath, json);
                }
                catch (Exception e)
                {
                    Debug.LogError("Orch: report write failed: " + e.Message);
                    ok = false;
                }
            }

            Debug.Log((ok ? OkMarker : FailMarker) + " " + json);
            Console.Out.Write("\n" + (ok ? OkMarker : FailMarker) + " " + json + "\n");
            Console.Out.Flush();
            EditorApplication.Exit(ok ? 0 : 2);
        }

        private static string ArgValue(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == key) return args[i + 1];
            }
            return null;
        }
    }
}
