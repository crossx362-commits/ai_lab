using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §10-2 정예 분류 — `global::W3Party.IsElite(kind)` 술어가 정예/잡몹을 올바로 가르는지 검증한다.
    /// 배경: 스폰 코드가 오래 `_mKind[i] >= 3` 관용구로 정예를 판정했는데, kind 5(돌진형)가
    /// 정예 3·4 **뒤에** 추가된 잡몹이라 번호가 3 이상이라는 이유만으로 정예 HP(90)·EliteScale·
    /// 필드 정예 드랍·정예 사망 FX를 4바퀴 동안 잘못 받았다(번호 순서 ≠ 의미 순서).
    /// 이 SelfCheck는 ①술어가 5만 잘못 넣던 오분류를 정확히 배제하는지 ②HP·스케일·드랍·FX·힐
    /// 호출부가 관용구가 아니라 IsElite를 소비하는지(소스 계약) ③재발 방지로 `_mKind[..] >= 3`
    /// 관용구가 코드에서 사라졌는지를 본다.
    /// </summary>
    public static class EliteClassifySelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Elite Classify Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;

            // ── 술어: 정예 집합 = {3 치유·4 소환·6 수호자·7 군단장·8 저주술사·9 처형자} ──
            Check(global::W3Party.IsElite(3), "kind 3(치유)은 정예");
            Check(global::W3Party.IsElite(4), "kind 4(소환)은 정예");
            Check(global::W3Party.IsElite(6), "kind 6(수호자)은 정예");
            Check(global::W3Party.IsElite(7), "kind 7(군단장)은 정예");
            Check(global::W3Party.IsElite(8), "kind 8(저주술사)은 정예");
            Check(global::W3Party.IsElite(9), "kind 9(처형자)은 정예");

            // ── 잡몹은 정예가 아니다 — 특히 kind 5(돌진형)는 이번 수리의 핵심 ──
            Check(!global::W3Party.IsElite(0), "kind 0(추적형)은 잡몹");
            Check(!global::W3Party.IsElite(1), "kind 1(포위형)은 잡몹");
            Check(!global::W3Party.IsElite(2), "kind 2(원거리형)은 잡몹");
            Check(!global::W3Party.IsElite(5), "kind 5(돌진형)은 잡몹 — `>= 3` 오분류 수리의 핵심");

            // 범위 밖은 정예로 새지 않는다(번호 확장 대비 상한 방어).
            Check(!global::W3Party.IsElite(10), "kind 10(미정의)은 정예 아님");
            Check(!global::W3Party.IsElite(-1), "kind -1(무효)은 정예 아님");

            // ── 소스 계약: 관용구 소멸 + 호출부가 IsElite 소비 ──
            string w3 = FindSource("W3Party.cs");
            Check(w3 != null, "W3Party.cs 소스 발견");
            if (w3 != null)
            {
                string src = File.ReadAllText(w3);
                Check(src.IndexOf("public static bool IsElite(int kind)", StringComparison.Ordinal) >= 0,
                    "IsElite 술어가 정의돼 있다");
                // 재발 방지: 정예 판정에 `_mKind[..] >= 3` 관용구가 남으면 안 된다(주석 예시는 허용).
                Check(src.IndexOf("_mKind[i] >= 3", StringComparison.Ordinal) < 0,
                    "`_mKind[i] >= 3` 관용구가 코드에서 사라졌다");
                Check(src.IndexOf("_mKind[j] >= 3", StringComparison.Ordinal) < 0,
                    "`_mKind[j] >= 3` 관용구가 코드에서 사라졌다");
                // HP·스케일·드랍·FX·힐 클램프 호출부가 술어를 소비한다.
                Check(src.IndexOf("IsElite(_mKind[i]) ? 90f : 26f", StringComparison.Ordinal) >= 0,
                    "스폰 HP가 IsElite를 소비한다(돌진형은 26)");
                Check(src.IndexOf("IsElite(_mKind[i]) ? EliteScale", StringComparison.Ordinal) >= 0,
                    "스폰 크기가 IsElite를 소비한다(돌진형은 1.0)");
                Check(src.IndexOf("!AshesToStars.DungeonRun.Active && IsElite(_mKind[i]))",
                        StringComparison.Ordinal) >= 0,
                    "필드 정예 드랍 훅이 IsElite를 소비한다(돌진형 제외)");
                Check(src.IndexOf("IsElite(_mKind[j]) ? 90f : 26f", StringComparison.Ordinal) >= 0,
                    "치유 클램프가 IsElite를 소비한다");
            }

            _ = nameof(global::W3Party.IsElite);

            if (_fail == 0) Debug.Log("[EliteClassifySelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EliteClassifySelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EliteClassifySelfCheck] FAIL {_fail}건");
        }

        static string FindSource(string fileName)
        {
            try
            {
                string[] roots =
                {
                    Path.Combine(Application.dataPath, "Scripts"),
                    Path.Combine(Application.dataPath, "_Game/Scripts/Runtime"),
                };
                foreach (var root in roots)
                {
                    if (!Directory.Exists(root)) continue;
                    var hit = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
                    if (hit.Length > 0) return hit[0];
                }
                var all = Directory.GetFiles(Application.dataPath, fileName, SearchOption.AllDirectories);
                return all.Length > 0 ? all[0] : null;
            }
            catch { return null; }
        }
    }
}
