using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// AttachWithAwake Editor 공용 유틸 — 헬퍼 존재·4파일 소비·원본 Invoke 0건·
    /// 더미 MonoBehaviour Awake 플래그(QA_NO면 안 켜짐).
    /// </summary>
    public static class AttachWithAwakeSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static readonly string[] ConsumerFiles =
        {
            "BossBattleAoeSelfCheck.cs",
            "BossAutoAttackSelfCheck.cs",
            "BossBattleRunSelfCheck.cs",
            "TowerClimbCurveMeasure.cs",
        };

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Attach With Awake Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string old = Environment.GetEnvironmentVariable(TestAttach.EnvNo);
            GameObject go = null;
            try
            {
                Environment.SetEnvironmentVariable(TestAttach.EnvNo, null);

                string editor = Path.Combine(Application.dataPath, "_Game/Scripts/Editor");
                string helperPath = Path.Combine(editor, "TestAttach.cs");
                Check(File.Exists(helperPath), "TestAttach.cs 헬퍼가 있다");
                if (File.Exists(helperPath))
                {
                    string helper = File.ReadAllText(helperPath);
                    Check(helper.IndexOf("public static T AttachWithAwake<T>(GameObject go)", StringComparison.Ordinal) >= 0,
                        "AttachWithAwake<T>(GameObject go) 시그니처가 있다");
                    Check(helper.IndexOf("QA_NO_ATTACH_AWAKE", StringComparison.Ordinal) >= 0,
                        "헬퍼가 QA_NO_ATTACH_AWAKE 게이트를 갖는다");
                    Check(helper.IndexOf("AddComponent<T>()", StringComparison.Ordinal) >= 0,
                        "헬퍼가 AddComponent<T>를 부른다");
                    Check(helper.IndexOf("GetMethod(\"Awake\"", StringComparison.Ordinal) >= 0,
                        "헬퍼가 Awake를 BindingFlags로 찾는다");
                }

                foreach (string file in ConsumerFiles)
                {
                    string path = Path.Combine(editor, file);
                    Check(File.Exists(path), $"{file} 소스 발견");
                    if (!File.Exists(path)) continue;
                    string src = File.ReadAllText(path);
                    Check(src.IndexOf("TestAttach.AttachWithAwake", StringComparison.Ordinal) >= 0,
                        $"{file}이 TestAttach.AttachWithAwake를 부른다");
                    Check(CountToken(src, "Invoke(\"Awake\")") == 0
                          && CountToken(src, "Invoke(\"awake\")") == 0
                          && CountToken(src, "Invoke(party, \"Awake\")") == 0
                          && CountToken(src, "Invoke(boss, \"Awake\")") == 0,
                        $"{file}에 원본 Invoke(\"Awake\")가 0건");
                    Check(CountToken(src, "GetMethod(\"Awake\"") == 0,
                        $"{file}에 원본 GetMethod(\"Awake\")가 0건");
                }

                string sweepPath = Path.Combine(editor, "GameSweepSelfCheck.cs");
                Check(File.Exists(sweepPath), "GameSweepSelfCheck.cs 소스 발견");
                if (File.Exists(sweepPath))
                {
                    string sweep = File.ReadAllText(sweepPath);
                    Check(sweep.IndexOf("AttachWithAwakeSelfCheck.Run", StringComparison.Ordinal) >= 0,
                        "GameSweep 등록부에 AttachWithAwake 행이 있다");
                }

                // ── 런타임: 비활성 GO + 더미 MonoBehaviour Awake 플래그 ──
                Check(!TestAttach.Blocked, "기본은 QA_NO_ATTACH_AWAKE가 꺼져 있다");
                go = new GameObject("AttachWithAwakeProbe");
                go.SetActive(false);
                var live = TestAttach.AttachWithAwake<AwakeProbe>(go);
                Check(live != null && go.GetComponent<AwakeProbe>() == live,
                    "헬퍼가 AddComponent로 붙인다");
                Check(live.Called, "Awake 플래그는 QA_NO가 아닐 때만 켜진다");
                UnityEngine.Object.DestroyImmediate(go);
                go = null;

                Environment.SetEnvironmentVariable(TestAttach.EnvNo, "1");
                Check(TestAttach.Blocked, "QA_NO_ATTACH_AWAKE=1이면 차단");
                go = new GameObject("AttachWithAwakeProbeNo");
                go.SetActive(false);
                var blocked = TestAttach.AttachWithAwake<AwakeProbe>(go);
                Check(blocked != null && go.GetComponent<AwakeProbe>() == blocked,
                    "QA_NO여도 AddComponent는 한다(옛 깨진 배치 경로)");
                Check(!blocked.Called, "QA_NO면 Awake Invoke를 건너뛰어 플래그가 꺼져 있다");
                UnityEngine.Object.DestroyImmediate(go);
                go = null;
                Environment.SetEnvironmentVariable(TestAttach.EnvNo, null);
                Check(!TestAttach.Blocked, "차단을 풀면 다시 Awake를 띄운다");

                _ = nameof(TestAttach.AttachWithAwake);
                _ = nameof(TestAttach.Blocked);
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                Environment.SetEnvironmentVariable(TestAttach.EnvNo, old);
            }

            if (_fail == 0) Debug.Log("[AttachWithAwakeSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[AttachWithAwakeSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[AttachWithAwakeSelfCheck] FAIL {_fail}건");
        }

        static int CountToken(string src, string token)
        {
            int n = 0, i = 0;
            while ((i = src.IndexOf(token, i, StringComparison.Ordinal)) >= 0)
            {
                n++;
                i += token.Length;
            }
            return n;
        }

        public class AwakeProbe : MonoBehaviour
        {
            public bool Called;
            void Awake() { Called = true; }
        }
    }
}
