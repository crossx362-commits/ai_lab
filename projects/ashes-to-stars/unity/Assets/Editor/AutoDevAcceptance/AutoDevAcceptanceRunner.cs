#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// AutoDev task acceptance runner.
///
/// Usage from command line:
/// Unity -batchmode -quit -projectPath ...
///   -executeMethod AutoDevAcceptanceRunner.Run
///   -autodevTask T0001 -autodevResult /path/result.txt
///
/// Each task owns a class named AutoDev_T0001_Acceptance with
/// public static void Run(). Failure is signalled by throwing an exception.
/// </summary>
public static class AutoDevAcceptanceRunner
{
    public static void Run()
    {
        var taskId = GetArg("-autodevTask");
        var resultPath = GetArg("-autodevResult");
        try
        {
            if (string.IsNullOrWhiteSpace(taskId))
                throw new InvalidOperationException("-autodevTask argument is missing.");
            if (string.IsNullOrWhiteSpace(resultPath))
                throw new InvalidOperationException("-autodevResult argument is missing.");

            var safeId = Sanitize(taskId);
            var expectedClass = $"AutoDev_{safeId}_Acceptance";
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeTypes)
                .FirstOrDefault(t => t != null && t.Name == expectedClass);
            if (type == null)
                throw new InvalidOperationException($"Acceptance class not found: {expectedClass}");

            var method = type.GetMethod(
                "Run",
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            if (method == null || method.ReturnType != typeof(void))
                throw new InvalidOperationException($"{expectedClass}.Run must be public static void Run().");

            try
            {
                method.Invoke(null, null);
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                throw new InvalidOperationException(tie.InnerException.Message, tie.InnerException);
            }

            WriteResult(resultPath, "PASS", $"task={taskId}", $"class={expectedClass}");
            Debug.Log($"AUTODEV_ACCEPTANCE_PASS:{taskId}");
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(resultPath))
                    WriteResult(resultPath, "FAIL", ex.GetType().Name, ex.Message, ex.StackTrace ?? string.Empty);
            }
            catch (Exception writeEx)
            {
                Debug.LogError($"AUTODEV_ACCEPTANCE_RESULT_WRITE_FAIL:{writeEx}");
            }

            Debug.LogError($"AUTODEV_ACCEPTANCE_FAIL:{taskId}\n{ex}");
            EditorApplication.Exit(1);
        }
    }

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null).Cast<Type>();
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }

    private static string GetArg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return string.Empty;
    }

    private static string Sanitize(string value)
    {
        return new string(value.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
    }

    private static void WriteResult(string path, params string[] lines)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllLines(path, lines);
    }
}

/// <summary>
/// Tiny assertion helpers deliberately independent of NUnit so the existing
/// Assembly-CSharp / Assembly-CSharp-Editor project structure does not need
/// an asmdef migration just to verify AutoDev tasks.
/// </summary>
public static class AutoDevAssert
{
    public static void True(bool condition, string message = "Expected true.")
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void False(bool condition, string message = "Expected false.")
    {
        if (condition) throw new InvalidOperationException(message);
    }

    public static void NotNull(object value, string message = "Expected non-null value.")
    {
        if (value == null) throw new InvalidOperationException(message);
    }

    public static void Equal<T>(T expected, T actual, string message = "Values are not equal.")
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message} expected={expected} actual={actual}");
    }

    public static void Greater(float actual, float threshold, string message = "Value is not greater than threshold.")
    {
        if (!(actual > threshold))
            throw new InvalidOperationException($"{message} actual={actual} threshold={threshold}");
    }

    public static void Nearly(float expected, float actual, float tolerance = 0.001f, string message = "Values are not nearly equal.")
    {
        if (Mathf.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"{message} expected={expected} actual={actual} tolerance={tolerance}");
    }
}
#endif
