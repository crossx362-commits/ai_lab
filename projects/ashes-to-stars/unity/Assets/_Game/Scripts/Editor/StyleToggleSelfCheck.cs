using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §3 CombatStyleDef.정예우선타겟·소모품자동사용 소비처.
    /// QA_NO_STYLE_TOGGLES면 토글 조각을 뺀 옛 StatLine.
    /// </summary>
    public static class StyleToggleSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Style Toggle Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(StyleScreen.EnvNoToggles);
            Environment.SetEnvironmentVariable(StyleScreen.EnvNoToggles, null);

            var defs = Resources.LoadAll<CombatStyleDef>("styles");
            Check(defs != null && defs.Length >= 4,
                $"Resources/styles 로드 ({(defs == null ? 0 : defs.Length)}종)");
            Check(!StyleScreen.TogglesBlocked, "기본은 켜짐");

            int seen = 0;
            if (defs != null)
                foreach (var d in defs)
                {
                    if (d == null) continue;
                    seen++;
                    string line = StyleScreen.StatLine(d.Id);
                    string tog = StyleScreen.ToggleLine(d);
                    Check(line.Contains($"딜 ×{d.딜배율:0.00}"),
                        $"{d.Id}: 형제 딜 보존 — 「{line}」");
                    Check(!string.IsNullOrEmpty(d.행동설명) && line.Contains(d.행동설명),
                        $"{d.Id}: 형제 행동설명 보존 — 「{line}」");
                    Check(tog.Contains(d.정예우선타겟 ? "정예 우선(§10-2)" : "가까운 적"),
                        $"{d.Id}: ToggleLine이 정예우선타겟을 읽는다 (값={d.정예우선타겟}) — 「{tog}」");
                    Check(tog.Contains(d.소모품자동사용 ? "소모품 자동" : "소모품 자동 불가(§4)"),
                        $"{d.Id}: ToggleLine이 소모품자동사용을 읽는다 (값={d.소모품자동사용}) — 「{tog}」");
                    Check(line.Contains(tog),
                        $"{d.Id}: StatLine이 ToggleLine을 붙인다 — 「{line}」");
                    Check(!d.소모품자동사용,
                        $"{d.Id}: 소모품자동사용은 false(§4 자동 금지) — 실제 {d.소모품자동사용}");
                    Check(!d.정예우선타겟,
                        $"{d.Id}: 정예우선타겟 기본 false(가까운 적) — 실제 {d.정예우선타겟}");
                }
            Check(seen >= 4, $"스타일 {seen}종 검사됨 (0이면 배선 확인 불가)");

            string atk = StyleScreen.StatLine(StyleId.공격형);
            Check(atk.Contains("가까운 적") && atk.Contains("소모품 자동 불가(§4)"),
                $"공격형 토글 조각 — 「{atk}」");
            Check(atk.Contains("회피 시도 안 함"),
                $"공격형 행동설명 유지 — 「{atk}」");

            var fake = ScriptableObject.CreateInstance<CombatStyleDef>();
            fake.정예우선타겟 = true;
            fake.소모품자동사용 = true;
            string fakeTog = StyleScreen.ToggleLine(fake);
            Check(fakeTog.Contains("정예 우선(§10-2)") && fakeTog.IndexOf("가까운 적", StringComparison.Ordinal) < 0,
                $"네거티브 정예 켜짐 — 「{fakeTog}」");
            Check(fakeTog.Contains("소모품 자동") && fakeTog.IndexOf("불가", StringComparison.Ordinal) < 0,
                $"네거티브 소모품 켜짐(명세 위반 값도 읽은 대로) — 「{fakeTog}」");
            UnityEngine.Object.DestroyImmediate(fake);

            Environment.SetEnvironmentVariable(StyleScreen.EnvNoToggles, "1");
            Check(StyleScreen.TogglesBlocked, "QA_NO면 차단");
            string old = StyleScreen.StatLine(StyleId.공격형);
            Check(old.Contains("딜 ×1.15") && old.Contains("회피 시도 안 함"),
                $"차단해도 수치·행동설명은 남는다 — 「{old}」");
            Check(old.IndexOf("가까운 적", StringComparison.Ordinal) < 0
                  && old.IndexOf("소모품 자동", StringComparison.Ordinal) < 0
                  && old.IndexOf("정예 우선", StringComparison.Ordinal) < 0,
                $"차단하면 토글 조각 없음(옛 화면) — 「{old}」");
            Check(StyleScreen.ToggleLine(defs != null && defs.Length > 0 ? defs[0] : null) == "",
                "차단하면 ToggleLine 빈 문자열");
            Environment.SetEnvironmentVariable(StyleScreen.EnvNoToggles, null);
            Check(!StyleScreen.TogglesBlocked
                  && StyleScreen.StatLine(StyleId.공격형).Contains("가까운 적 · 소모품 자동 불가(§4)"),
                "차단을 풀면 다시 토글 조각");

            string styleSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/StyleScreen.cs"));
            Check(styleSrc.Contains("d.정예우선타겟"),
                "StyleScreen이 d.정예우선타겟을 읽는다 — 지우면 소비처 0곳으로 되돌아간다");
            Check(styleSrc.Contains("d.소모품자동사용"),
                "StyleScreen이 d.소모품자동사용을 읽는다 — 지우면 소비처 0곳으로 되돌아간다");
            Check(styleSrc.Contains("StatLine(id)"),
                "StyleScreen 카드가 StatLine을 그린다");

            string setupSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Editor/ProjectSetup.cs"));
            Check(setupSrc.Contains("a.정예우선타겟 = false"),
                "ProjectSetup이 정예우선타겟을 기본 false로 authored");
            Check(setupSrc.Contains("a.소모품자동사용 = false"),
                "ProjectSetup이 소모품자동사용을 false로 authored(§4)");

            _ = nameof(StyleScreen.StatLine);
            _ = nameof(StyleScreen.ToggleLine);
            _ = nameof(CombatStyleDef.정예우선타겟);
            _ = nameof(CombatStyleDef.소모품자동사용);

            Environment.SetEnvironmentVariable(StyleScreen.EnvNoToggles, no);

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "style_toggle_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS StyleToggleSelfCheck" : "FAIL StyleToggleSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[StyleToggleSelfCheck] PASS → " + path);
            else Debug.LogError("[StyleToggleSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[StyleToggleSelfCheck] FAIL {_fail}건");
        }
    }
}
