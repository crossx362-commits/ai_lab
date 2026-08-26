using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// ORDERS ② 파티 멤버별(per-member) 전투 스타일 실소비 배선 검사.
    /// W3Party의 전투 소비부(TickParty·TickMobs·TickShots)가 파티 단일 `_style` 대신
    /// StyleFor(멤버별)를 읽는지, UseFixedStyle=true일 때 측정용 단일 경로가 보존되는지,
    /// QA_NO_MEMBER_STYLE=1이 옛 단일 경로로 되돌리는 네거티브인지 판정한다.
    /// </summary>
    public static class MemberStyleSelfCheck
    {
        const string EnvNo = "QA_NO_MEMBER_STYLE";
        const int MinConsumers = 3;   // TickParty·TickMobs·TickShots

        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Member Style Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string savedNo = Environment.GetEnvironmentVariable(EnvNo);
            Environment.SetEnvironmentVariable(EnvNo, null);

            try
            {
                // ── 1. 삼항 본체(W3Party.ResolveStyle) — 배선의 두뇌 ──
                // 게임 모드: 멤버별 값이 그대로 나온다
                Check(W3Party.ResolveStyle(false, false, W3Party.Style.Balanced, W3Party.Style.Aggressive)
                          == W3Party.Style.Aggressive,
                    "게임 모드는 멤버별 스타일을 그대로 쓴다");
                // 측정 고정(UseFixedStyle): 옛 단일 경로 보존 — 구성 비교 결정론
                Check(W3Party.ResolveStyle(true, false, W3Party.Style.Balanced, W3Party.Style.Defensive)
                          == W3Party.Style.Balanced,
                    "UseFixedStyle=true면 파티 단일 `_style`(측정 결정론 보존)");
                // 네거티브: QA_NO_MEMBER_STYLE=1은 옛 단일 경로 재현
                Check(W3Party.ResolveStyle(false, true, W3Party.Style.Survival, W3Party.Style.Aggressive)
                          == W3Party.Style.Survival,
                    $"QA_NO_MEMBER_STYLE=1이면 옛 단일 경로({W3Party.Style.Survival})");

                // ── 2. Spec 배율표가 스타일별로 실제로 다른가(멤버별 적용이 의미를 갖려면) ──
                var specM = typeof(W3Party).GetMethod("Spec",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                Check(specM != null, "W3Party.Spec 리플렉션 접근");
                if (specM != null)
                {
                    object a = specM.Invoke(null, new object[] { W3Party.Style.Aggressive });
                    object d = specM.Invoke(null, new object[] { W3Party.Style.Defensive });
                    float dmgA = (float)a.GetType().GetField("DmgMul").GetValue(a);
                    float dmgD = (float)d.GetType().GetField("DmgMul").GetValue(d);
                    float keepA = (float)a.GetType().GetField("KeepDist").GetValue(a);
                    float keepD = (float)d.GetType().GetField("KeepDist").GetValue(d);
                    Check(dmgA != dmgD && keepA != keepD,
                        $"공격형 vs 방어형 배율표 상이 (딜 {dmgA:0.00} vs {dmgD:0.00} · 거리 {keepA:0.0} vs {keepD:0.0})");
                }

                // ── 3. StyleOf(Job) — 직업별 저장 선택을 멤버에 배정한다 ──
                var styleOfM = typeof(W3Party).GetMethod("StyleOf",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var jobT = typeof(W3Party).GetNestedType("Job", BindingFlags.NonPublic);
                Check(styleOfM != null && jobT != null, "W3Party.StyleOf·Job 리플렉션 접근");
                if (styleOfM != null && jobT != null)
                {
                    object guard = Enum.Parse(jobT, "수호기사");
                    string key = guard.ToString();
                    int before = PlayerPrefs.GetInt("ats.style." + key, (int)CombatStylePrefs.Default);
                    CombatStylePrefs.Set(key, StyleId.공격형);
                    var got = styleOfM.Invoke(null, new[] { guard });
                    Check(got.Equals(W3Party.Style.Aggressive),
                        $"StyleOf(수호기사)=저장 선택 반영(공격형 지정 후) — 실제 {got}");
                    CombatStylePrefs.Set(key, (StyleId)before);   // 원복 — 세이브 오염 금지

                    object bard = Enum.Parse(jobT, "음유시인");
                    // 「미선택」 전제를 가정하지 않고 직접 만든다 — 이 저장소의 PlayerPrefs는
                    // unity/와 unity_meas/가 같은 product 이름을 공유해 실측 오염이 흘러든다
                    // (2026-08-26 실측: ats.style.음유시인=공격형 잔재가 기본값 검사를 깼다).
                    PlayerPrefs.DeleteKey("ats.style." + bard);
                    var def = styleOfM.Invoke(null, new[] { bard });
                    Check(def.Equals(W3Party.Style.Balanced),
                        $"미선택 직업은 균형형 기본 — 실제 {def}");
                }

                // ── 4. 소비처 소스 검사 — 옛 단일 경로 잔존 0건 ──
                string src = File.ReadAllText(Path.Combine(Application.dataPath,
                    "Scripts/W3Party.cs"));
                int consumers = Count(src, "Spec(StyleFor(");
                Check(consumers >= MinConsumers,
                    $"전투 소비부가 StyleFor를 읽는 곳 {consumers}곳 ≥ {MinConsumers}(TickParty·TickMobs·TickShots)");
                Check(!src.Contains("var sp = Spec(_style);"),
                    "옛 단일 소비 `var sp = Spec(_style);` 잔존 0건");
                Check(src.Contains("m.Style = UseFixedStyle ? _style : StyleOf(m.Job);"),
                    "멤버 생성 시 배정 경로 유지(UseFixedStyle 삼항)");
                Check(src.Contains("public static string MemberStyleSummaryOnActive()"),
                    "플레이모드 실측 프로브(MemberStyleSummaryOnActive) 존재");

                // ── 5. 활성 판 없으면 요약은 빈 문자열(예외 대신) ──
                Check(W3Party.MemberStyleSummaryOnActive() == "",
                    "활성 판 없음 — 요약 빈 문자열");

                // ── 5-B. 배선 판정 함수(MemberStyleVerdict) — ON/NEG 양쪽 기준 ──
                const string mixed = "수호기사=Aggressive(딜×1.20) 검사=Defensive(딜×0.80) 사제=Balanced(딜×1.00)";
                const string same = "수호기사=Balanced(딜×1.00) 검사=Balanced(딜×1.00) 사제=Balanced(딜×1.00)";
                Check(W3Party.MemberStyleVerdict(mixed, false),
                    "판정: 스타일 2종 이상 혼합 → 게임 모드 PASS");
                Check(!W3Party.MemberStyleVerdict(mixed, true),
                    "판정: 혼합 요약은 네거티브(차단)에서 FAIL");
                Check(W3Party.MemberStyleVerdict(same, true),
                    "판정: 전원 동일 스타일 → 네거티브 PASS");
                Check(!W3Party.MemberStyleVerdict(same, false),
                    "판정: 전원 동일 스타일은 게임 모드 FAIL(배선 안 됨)");
                Check(!W3Party.MemberStyleVerdict("", false) && !W3Party.MemberStyleVerdict("형식불일치", false),
                    "판정: 빈 요약·파싱 불가는 FAIL(조용한 통과 금지)");

                // ── 6. Sweep 등록 — 스윕이 이 검사를 돌린다 ──
                string sweepSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                    "_Game/Scripts/Editor/GameSweepSelfCheck.cs"));
                Check(sweepSrc.Contains("MemberStyleSelfCheck"),
                    "GameSweepSelfCheck에 등록됨");
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvNo, savedNo);
            }

            Check(Environment.GetEnvironmentVariable(EnvNo) == savedNo,
                "네거티브 env 원복");

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "member_style_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS MemberStyleSelfCheck" : "FAIL MemberStyleSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[MemberStyleSelfCheck] PASS → " + path);
            else Debug.LogError("[MemberStyleSelfCheck] FAIL " + _fail + " → " + path);
            if (_fail > 0) throw new InvalidOperationException(
                $"[MemberStyleSelfCheck] FAIL {_fail}건");
        }

        static int Count(string haystack, string needle)
        {
            int n = 0, i = 0;
            while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
            {
                n++;
                i += needle.Length;
            }
            return n;
        }
    }
}
