using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-9·§14 종족 고유 메커니즘 — RaceDef 에셋(고유메커니즘)을 화면에 읽어주는 소비처.
    ///
    /// 계정 종족(RacePrefs.Get)의 고유 메커니즘(적응·바람의 인도·불굴·야성 감각)은
    /// Resources/races/Race_*.asset에 authored·committed돼 있으면서도 **어떤 런타임 코드도
    /// 읽지 않는 소비처 0곳** 상태였다 — 형제 필드 방어배율(W3Party)·회복시간(LifeSystem)은
    /// 읽는데 고유메커니즘만 죽어 있었다(JobInfo가 읽는 것은 JobDef.고유메커니즘이고
    /// RaceDef 쪽은 리더 없음). JobInfo와 같은 원칙 — **에셋을 못 읽으면 문구를 지어내지
    /// 않는다**(빈 문자열, 호출부는 이때 줄을 그리지 않는다).
    /// </summary>
    public static class RaceInfo
    {
        /// <summary>§18-9 전투당 발동 상한. QA_NO면 필드 조각을 빼고 옛 문장만 남긴다.</summary>
        public const string EnvNo = "QA_NO_RACE_BATTLE_CAP";

        static RaceDef[] _cache;

        static RaceDef[] Defs => _cache ??= Resources.LoadAll<RaceDef>("races");

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>계정 종족과 일치하는 RaceDef. 없거나 에셋을 못 읽으면 null.</summary>
        public static RaceDef For(RaceId id)
        {
            var defs = Defs;
            if (defs == null) return null;
            foreach (var d in defs)
                if (d != null && d.Id == id) return d;
            return null;
        }

        /// <summary>
        /// "종족 특성 — {고유메커니즘} (발동 {N}% · 전투당 {K}회)". 계정 종족의
        /// RaceDef.고유메커니즘·고유발동확률·전투당발동의 **런타임 소비처**.
        /// 고유발동확률(드워프 불굴 0.25·수인 야성감각 1.0·엘프 0.15, 인간 적응 0)은
        /// 08d0de12가 「(발동 N%)」로 이미 읽었고, 같은 표(§18-9)·밸런스 가드(원장 160
        /// 「발동을 전투당 1회로 묶어」)의 형제 필드 <c>전투당발동</c>만 소비처 0곳이었다
        /// (정의 RaceDef.cs:35 default 1 · 에셋에 1로 committed · grep 소비처 0곳).
        /// 발동확률 &gt; 0일 때만 괄호를 붙이고, 그 안에서 전투당발동 &gt; 0이면
        /// 「 · 전투당 K회」를 이어 붙인다(상시 패시브 적응엔 안 붙임). 에셋 문장에 이미
        /// 박혀 있던 「 (전투당 K회)」는 필드 값을 보여줄 때 벗겨, 플레이어가 보는 숫자의
        /// 단일 출처를 Def 필드로 둔다. QA_NO면 벗기·이어붙이기를 모두 건너뛰어 옛 줄
        /// (문장 속 전투당 + 발동%만)로 돌아간다. 에셋을 못 읽거나 값이 비면 빈 문자열
        /// — 호출부는 이때 줄을 그리지 않는다(지어내지 않음).
        /// </summary>
        public static string MechanicLine(RaceId id)
        {
            var d = For(id);
            if (d == null || string.IsNullOrEmpty(d.고유메커니즘)) return "";
            string mech = d.고유메커니즘;
            bool showCap = !Blocked && d.고유발동확률 > 0f && d.전투당발동 > 0;
            if (showCap)
            {
                // 에셋 문장에 박힌 「 (전투당 N회)」는 필드 조각과 겹치니 벗긴다.
                string prose = " (전투당 " + d.전투당발동 + "회)";
                if (mech.EndsWith(prose, StringComparison.Ordinal))
                    mech = mech.Substring(0, mech.Length - prose.Length);
            }
            string line = "종족 특성 — " + mech;
            if (d.고유발동확률 > 0f)
            {
                string proc = "발동 " + Mathf.RoundToInt(d.고유발동확률 * 100f) + "%";
                if (showCap) proc += " · 전투당 " + d.전투당발동 + "회";
                line += " (" + proc + ")";
            }
            return line;
        }

        /// <summary>
        /// "종족 정체성 — {정체성}". 계정 종족 RaceDef.정체성(인간 "빨리 크고 빨리 회복하는 범용형"·
        /// 엘프 "죽지 않으려 움직이는 유리대포"·드워프 "안 죽고 버티며 쌓아올리는 생산가"·
        /// 수인 "먼저 덮치고 먼저 빠지는 사냥꾼")의 **유일한 런타임 소비처**. 이 한 줄 아키타입
        /// 문구는 ProjectSetup이 종족마다 authored(ProjectSetup.cs:134)·에셋에 committed돼 있으면서도
        /// 읽는 코드 0곳이었다 — 형제 필드 고유메커니즘·고유발동확률은 MechanicLine에서 읽는데
        /// 정체성만 죽어 있었다(컨셉·스킬·사망리스크·자동사냥적합도가 겪은 함정과 동일 계열).
        /// 짧은 아키타입 요약이라 별도 한 줄로 낸다(MechanicLine의 긴 메커니즘 설명 뒤에 이어붙이면
        /// LabelClip에 잘려 정작 정체성이 안 보인다). 에셋을 못 읽거나 값이 비면 빈 문자열
        /// — 호출부는 이때 줄을 그리지 않는다(지어내지 않음).
        /// </summary>
        public static string IdentityLine(RaceId id)
        {
            var d = For(id);
            if (d == null || string.IsNullOrEmpty(d.정체성)) return "";
            return "종족 정체성 — " + d.정체성;
        }

        /// <summary>§18-9 종족 이속 배율. QA_NO면 줄을 비운다(옛 화면 = 이속 줄 없음).</summary>
        public const string EnvNoSpeed = "QA_NO_RACE_SPEED";

        public static bool SpeedBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoSpeed);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// "종족 이속 — ×0.85 (−15%)". 계정 종족 RaceDef.이속배율의 **유일한 런타임 소비처**.
        /// 원장 §18-9 드워프 표 「이속 -15%」·에셋 0.85가 ProjectSetup·Race_*.asset에 authored돼
        /// 있으면서도 grep 소비처 0곳이었다 — 형제 영지생산·골드소비·드랍률·인식범위는 Economy/
        /// EstateMine/WorldStar가 읽는데 이속만 죽어 있던 함정(전투당발동·쿨다운과 동일 계열).
        /// 기준(×1)과 같으면 빈 문자열(줄을 안 그려 패널 밀도를 안 늘린다). QA_NO면 항상 빈
        /// 문자열로 옛 화면(이속 줄 없음)에 회귀. 표시 전용 — W3Party·전투 이동 수치 무접촉.
        /// </summary>
        public static string SpeedLine(RaceId id)
        {
            if (SpeedBlocked) return "";
            var d = For(id);
            if (d == null) return "";
            float mul = d.이속배율;
            if (mul <= 0f) return "";
            if (Mathf.Abs(mul - 1f) < 0.0001f) return "";
            int pct = Mathf.RoundToInt((mul - 1f) * 100f);
            string sign = pct > 0 ? "+" : "";
            return "종족 이속 — ×" + mul.ToString("0.##") + " (" + sign + pct + "%)";
        }

        /// <summary>§18-9 종족 체력 배율. QA_NO면 줄을 비운다(옛 화면 = 체력 줄 없음).</summary>
        public const string EnvNoHealth = "QA_NO_RACE_HEALTH";

        public static bool HealthBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoHealth);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// "종족 체력 — ×0.85 (−15%)". 계정 종족 RaceDef.체력배율의 **유일한 런타임 소비처**.
        /// 원장 §18-9 엘프 표 「HP -15%」·에셋 0.85가 ProjectSetup·Race_*.asset에 authored돼
        /// 있으면서도 grep 소비처 0곳이었다 — 형제 이속배율은 SpeedLine이 읽는데 체력만 죽어
        /// 있던 함정(이속·전투당발동·쿨다운과 동일 계열). 기준(×1)과 같으면 빈 문자열(줄을
        /// 안 그려 패널 밀도를 안 늘린다). QA_NO면 항상 빈 문자열로 옛 화면(체력 줄 없음)에
        /// 회귀. 표시 전용 — W3Party·전투 HP 수치 무접촉.
        /// </summary>
        public static string HealthLine(RaceId id)
        {
            if (HealthBlocked) return "";
            var d = For(id);
            if (d == null) return "";
            float mul = d.체력배율;
            if (mul <= 0f) return "";
            if (Mathf.Abs(mul - 1f) < 0.0001f) return "";
            int pct = Mathf.RoundToInt((mul - 1f) * 100f);
            string sign = pct > 0 ? "+" : "";
            return "종족 체력 — ×" + mul.ToString("0.##") + " (" + sign + pct + "%)";
        }

        /// <summary>§18-9 종족 방어 배율. QA_NO면 줄을 비운다(옛 화면 = 방어 줄 없음).</summary>
        public const string EnvNoDefense = "QA_NO_RACE_DEFENSE";

        public static bool DefenseBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoDefense);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// "종족 방어 — ×0.8 (−20%)". 계정 종족 RaceDef.방어배율의 **표시 런타임 소비처**.
        /// 원장 §18-9 엘프 표 「방어 -20%」·에셋 0.8이 ProjectSetup·Race_*.asset에 authored돼
        /// 있으면서도 속성 화면 grep 소비처 0곳이었다 — 형제 체력배율은 HealthLine이 읽는데
        /// 방어만 W3Party 전투 경로에만 있고 화면은 죽어 있던 함정(이속·체력과 동일 계열).
        /// 기준(×1)과 같으면 빈 문자열(줄을 안 그려 패널 밀도를 안 늘린다). QA_NO면 항상 빈
        /// 문자열로 옛 화면(방어 줄 없음)에 회귀. 표시 전용 — W3Party·전투 피해 수치 무접촉.
        /// </summary>
        public static string DefenseLine(RaceId id)
        {
            if (DefenseBlocked) return "";
            var d = For(id);
            if (d == null) return "";
            float mul = d.방어배율;
            if (mul <= 0f) return "";
            if (Mathf.Abs(mul - 1f) < 0.0001f) return "";
            int pct = Mathf.RoundToInt((mul - 1f) * 100f);
            string sign = pct > 0 ? "+" : "";
            return "종족 방어 — ×" + mul.ToString("0.##") + " (" + sign + pct + "%)";
        }
    }
}
