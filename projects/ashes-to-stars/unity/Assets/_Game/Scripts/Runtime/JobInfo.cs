using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §4 직업 특성 상세 — JobDef 에셋(컨셉·고유메커니즘·스킬)을 화면에 읽어주는 소비처.
    ///
    /// 이 데이터는 오래도록 <c>Assets/_Game/Data/Jobs/</c>(=Resources 밖)에 authored돼 있어
    /// **어떤 런타임 코드도 못 읽는 소비처 0곳** 상태였다(races·styles가 겪은 「Resources 밖 자산」
    /// 함정과 동일). 에셋을 <c>Assets/Resources/jobs/</c>로 옮기고 여기서 읽어 CharacterScreen이
    /// 표시한다. StyleScreen과 같은 원칙 — **에셋을 못 읽으면 숫자·문구를 지어내지 않는다**(빈 문자열).
    /// </summary>
    public static class JobInfo
    {
        static JobDef[] _cache;

        static JobDef[] Defs => _cache ??= Resources.LoadAll<JobDef>("jobs");

        /// <summary>현재 직업명과 일치하는 JobDef. 없거나 에셋을 못 읽으면 null.</summary>
        public static JobDef For(string jobName)
        {
            if (string.IsNullOrEmpty(jobName)) return null;
            var defs = Defs;
            if (defs == null) return null;
            foreach (var d in defs)
                if (d != null && d.직업명 == jobName) return d;
            return null;
        }

        /// <summary>
        /// "직업 특성 — {컨셉} · {고유메커니즘}". 매칭 직업이 없으면(기본직 탱/딜/… 또는
        /// 에셋 미로드) 빈 문자열 — 호출부는 이때 줄을 그리지 않는다.
        /// </summary>
        public static string ConceptLine(string jobName)
        {
            var d = For(jobName);
            if (d == null) return "";
            string concept = string.IsNullOrEmpty(d.컨셉) ? "" : d.컨셉;
            string mech = string.IsNullOrEmpty(d.고유메커니즘) ? "" : d.고유메커니즘;
            if (concept == "" && mech == "") return "";
            if (mech == "") return $"직업 특성 — {concept}";
            if (concept == "") return $"직업 특성 — {mech}";
            return $"직업 특성 — {concept} · {mech}";
        }

        /// <summary>§3·SkillDef.쿨다운. QA_NO면 이름만 남긴다(옛 SkillLine).</summary>
        public const string EnvNoSkillCd = "QA_NO_SKILL_CD";

        public static bool SkillCdBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoSkillCd);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>§3·SkillDef.위력배율. QA_NO면 위력 조각을 뺀다(옛 SkillLine = 이름·쿨만).</summary>
        public const string EnvNoSkillPow = "QA_NO_SKILL_POW";

        public static bool SkillPowBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoSkillPow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>§3·SkillDef.반경. QA_NO면 반경 조각을 뺀다(옛 SkillLine = 이름·쿨·위력).</summary>
        public const string EnvNoSkillRad = "QA_NO_SKILL_RAD";

        /// <summary>기적 등 authored 99는 전투 반경이 아니라 전역 표식 — 숫자를 붙이지 않는다.</summary>
        public const float SkillRadDisplayCap = 50f;

        public static bool SkillRadBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoSkillRad);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>§3·SkillDef.자원소모. QA_NO면 소모 조각을 뺀다(옛 SkillLine = 이름·쿨·위력·반경).</summary>
        public const string EnvNoSkillCost = "QA_NO_SKILL_COST";

        public static bool SkillCostBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoSkillCost);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>§3·SkillDef.설명. QA_NO면 설명 줄을 비운다(옛 화면 = 설명 행 없음).</summary>
        public const string EnvNoSkillDesc = "QA_NO_SKILL_DESC";

        public static bool SkillDescBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoSkillDesc);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// 설명 줄을 두 줄로 접지 않고 옛 Info(LabelClip 한 줄)로 그린다.
        /// 마법사 「빙결: 광」에서 우측이 잘리던 화면. QA_NO_SKILL_DESC(줄 자체 없음)와 별개.
        /// </summary>
        public const string EnvNoSkillDescWrap = "QA_NO_SKILL_DESC_WRAP";

        public static bool SkillDescWrapBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoSkillDescWrap);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// "보유 스킬 — {이름}(N초·×P·반경R·소모C) · …". JobDef.스킬의 이름·쿨다운·위력배율·반경·**자원소모** 소비처.
        /// 이름·쿨·위력만 읽던 SkillLine(50202ce5) 옆에, ProjectSetup이 스킬마다 authored한
        /// <c>SkillDef.반경</c>(화염폭풍 3.2·빙결 4·도발 4.5·진군가 8…)이 정의·에셋만 있고
        /// grep 소비처 0곳이었다 — 형제 위력은 SkillLine이 「×P」로 읽는데 같은 SkillDef 행의
        /// 반경만 죽어 있던 함정(위력·쿨다운·전투당발동과 동일 계열).
        /// 원장 SkillDef 툴팁 「효과 반경(0이면 단일)」. 0 &lt; 반경 &lt; 50만 「반경R」
        /// (기적 authored 99는 전역 표식이라 숫자를 안 붙임). 쿨과 같이 있으면 괄호 안
        /// 「(N초·×P·반경R)」, 쿨 0이면 괄호 없이 「 이름 ×P 반경R」(SkillCd 「이름( 금지」).
        /// QA_NO_SKILL_RAD면 반경 조각만 빼고 이름·쿨·위력 옛 줄로 회귀.
        /// 같은 행의 <c>SkillDef.자원소모</c>(검사 일섬 5·사제 기적 100)는 정의·에셋만 있고
        /// grep 소비처 0곳이었다 — 형제 반경은 「반경R」로 읽는데 소모만 죽어 있던 함정.
        /// 원장 툴팁 「고유 자원 소모량(0이면 미사용)」. 0보다 클 때만 「소모C」.
        /// 쿨과 같이 있으면 괄호 안 「·소모C」, 쿨 0이면 괄호 없이 「 소모C」.
        /// QA_NO_SKILL_COST면 소모 조각만 빼고 이름·쿨·위력·반경 옛 줄로 회귀. 표시 전용 —
        /// W3Party·전투 수치 무접촉.
        /// </summary>
        public static string SkillLine(string jobName)
        {
            var d = For(jobName);
            if (d == null || d.스킬 == null || d.스킬.Length == 0) return "";
            var parts = new System.Collections.Generic.List<string>();
            foreach (var s in d.스킬)
            {
                if (s == null || string.IsNullOrEmpty(s.이름)) continue;
                string piece = s.이름;
                bool showCd = !SkillCdBlocked && s.쿨다운 > 0f;
                bool showPow = !SkillPowBlocked && s.위력배율 > 0f
                    && Mathf.Abs(s.위력배율 - 1f) >= 0.0001f;
                bool showRad = !SkillRadBlocked && s.반경 > 0f && s.반경 < SkillRadDisplayCap;
                bool showCost = !SkillCostBlocked && s.자원소모 > 0f;
                if (showCd)
                {
                    string inside = s.쿨다운.ToString("0.#") + "초";
                    if (showPow) inside += "·×" + s.위력배율.ToString("0.##");
                    if (showRad) inside += "·반경" + s.반경.ToString("0.#");
                    if (showCost) inside += "·소모" + s.자원소모.ToString("0.#");
                    piece += "(" + inside + ")";
                }
                else
                {
                    if (showPow) piece += " ×" + s.위력배율.ToString("0.##");
                    if (showRad) piece += " 반경" + s.반경.ToString("0.#");
                    if (showCost) piece += " 소모" + s.자원소모.ToString("0.#");
                }
                parts.Add(piece);
            }
            if (parts.Count == 0) return "";
            return "보유 스킬 — " + string.Join(" · ", parts);
        }

        /// <summary>
        /// "스킬 설명 — {이름}: {설명} · …". JobDef.스킬의 <c>SkillDef.설명</c> **유일한 런타임 소비처**.
        /// 같은 SkillDef 행의 이름·쿨·위력·반경·자원소모는 SkillLine이 읽는데, ProjectSetup이
        /// 스킬마다 authored한 설명(화염폭풍 「장판 광역 — 4체 이상 밀집 시」·일섬 「스택 5 전량
        /// 소모 단일 폭딜」…)만 정의·에셋에 있고 grep 소비처 0곳이었다 — 숫자 형제는 SkillLine에
        /// 붙고 같은 행의 TextArea만 죽어 있던 함정(반경·자원소모와 동일 계열). 원장 §3 직업 스킬
        /// 표의 스킬 설명 열. SkillLine에 이어붙이면 LabelClip이 뒷 스킬 이름부터 자르므로
        /// **별도 한 줄**로 낸다(IdentityLine이 MechanicLine과 갈라진 이유와 같음). 값이 빈
        /// 스킬은 건너뛴다(지어내지 않음). QA_NO_SKILL_DESC면 항상 빈 문자열로 옛 화면(설명
        /// 행 없음)에 회귀. 표시 전용 — W3Party·전투 수치 무접촉.
        /// </summary>
        public static string SkillDescLine(string jobName)
        {
            if (SkillDescBlocked) return "";
            var d = For(jobName);
            if (d == null || d.스킬 == null || d.스킬.Length == 0) return "";
            var parts = new System.Collections.Generic.List<string>();
            foreach (var s in d.스킬)
            {
                if (s == null || string.IsNullOrEmpty(s.이름) || string.IsNullOrEmpty(s.설명))
                    continue;
                parts.Add(s.이름 + ": " + s.설명);
            }
            if (parts.Count == 0) return "";
            return "스킬 설명 — " + string.Join(" · ", parts);
        }

        /// <summary>
        /// "사망 리스크 — {최저/낮음/중/중상/높음}". JobDef.사망리스크의 **유일한 런타임 소비처**.
        /// 이 값은 §4 전직 표(원장 169·176·185·192)의 ✅ 확정 컬럼 「사망 리스크」인데
        /// ProjectSetup이 직업마다 authored(수호기사 최저·광전사 높음…)하고도 어떤 코드도 읽지
        /// 않아 소비처 0곳이었다(컨셉·스킬이 겪은 함정과 동일 계열). 매칭 직업이 없거나 값이 비면
        /// 빈 문자열 — 호출부는 이때 줄을 그리지 않는다(지어내지 않음).
        /// </summary>
        public static string RiskLine(string jobName)
        {
            var d = For(jobName);
            if (d == null || string.IsNullOrEmpty(d.사망리스크)) return "";
            return "사망 리스크 — " + d.사망리스크;
        }

        /// <summary>
        /// "이동기 — {구르기/방패 돌진/점멸/백스텝/짧은 스텝/위치 교체}". JobDef.이동기형태의
        /// **유일한 런타임 소비처**. §5 이동기(원장 369·381 「✅ 오너 결정 2026-08-13」)의 계열별
        /// 형태 컬럼인데 ProjectSetup이 직업마다 authored(수호기사 방패 돌진·궁수 백스텝…)하고도
        /// 어떤 코드도 읽지 않아 소비처 0곳이었다(컨셉·스킬·사망리스크가 겪은 함정과 동일 계열).
        /// 매칭 직업이 없거나 값이 비면 빈 문자열 — 호출부는 이때 줄을 그리지 않는다(지어내지 않음).
        /// </summary>
        public static string MovementLine(string jobName)
        {
            var d = For(jobName);
            if (d == null || string.IsNullOrEmpty(d.이동기형태)) return "";
            return "이동기 — " + d.이동기형태;
        }

        /// <summary>
        /// "거리 {3}초분 · 무적 {0.3}초 · 쿨 {6}초". JobDef.이동기거리·무적시간·이동기쿨의
        /// **유일한 런타임 소비처**. §5 이동기 스펙 표(원장 374~377 — 이동 거리 「캐릭터 기본 이동
        /// 3초분」·무적 프레임 0.3초(원장 379 「이 게임 조작의 핵심 기술」)·쿨다운 6초. 헤더 369
        /// ✅ 오너 결정 2026-08-13). 무적·쿨 두 수치는 5cc0a8ee가 이미 배선했으나 같은 표 1행인
        /// **이동기거리**만 소비처 0곳으로 남아 있었다(정의 JobDef.cs:29 default 3f, 전 직업 공통 —
        /// §5는 이동기 *형태*만 직업별 변주·수치는 공통). 형제 이동기형태는 MovementLine에서 읽는데
        /// 이 거리 필드만 죽어 있던, 이 저장소가 반복해 겪은 「정의만 있고 부르는 곳이 0곳」 함정.
        /// 거리를 무적·쿨 **앞**에 둔다: 표 순서(거리→무적→쿨)와 맞고, InfoAt LabelClip(우측 잘림)이
        /// 이 행 맨 뒤 조각(mobility)을 넘칠 때 자르므로 뒤에 둔 쿨부터 잘려 새로 배선한 거리는 보존된다.
        /// 세 값 모두 0 이하면 빈 문자열 — 호출부는 이때 조각을 붙이지 않는다(지어내지 않음).
        /// 밸런스·전투 수치는 안 건드린다(표시 전용, 커밋/기본 값 그대로 읽음).
        /// </summary>
        public static string MobilityStatLine(string jobName)
        {
            var d = For(jobName);
            if (d == null) return "";
            if (d.이동기거리 <= 0f && d.무적시간 <= 0f && d.이동기쿨 <= 0f) return "";
            string s = "";
            if (d.이동기거리 > 0f) s = "거리 " + d.이동기거리.ToString("0.#") + "초분";
            if (d.무적시간 > 0f || d.이동기쿨 > 0f)
            {
                string mc = "무적 " + d.무적시간.ToString("0.#") + "초 · 쿨 " + d.이동기쿨.ToString("0.#") + "초";
                s = string.IsNullOrEmpty(s) ? mc : s + " · " + mc;
            }
            return s;
        }

        /// <summary>
        /// "자동사냥 {최상/상/중/하}". JobDef.자동사냥적합도(float 0~1)의 **유일한 런타임 소비처**.
        /// 이 값은 §6 필드/자동사냥 적합도(원장 204 💡)의 등급인데 ProjectSetup이 직업마다
        /// authored(소환사 0.95·마법사 0.90·… 사제 0.20)하고도 어떤 코드도 읽지 않아 소비처 0곳이었다
        /// (컨셉·스킬·사망리스크·이동기가 겪은 함정과 동일 계열). 밴드 임계값은 원장 204의 명시 등급
        /// (소환사·마법사 최상 / 검사·광전사 상 / 궁수 중 / 힐·버퍼 하)에 authored 11종이 전부 맞도록 잡았다
        /// — 지어낸 게 아니라 문서 등급을 authored 값에 되짚은 것: ≥0.85 최상(마법사·소환사),
        /// ≥0.60 상(검사·광전사), ≥0.50 중(궁수), 그 아래 하(힐·버퍼·탱). 매칭 직업이 없으면 빈 문자열
        /// — 호출부는 이때 줄을 그리지 않는다(지어내지 않음). 밸런스 수치는 안 건드린다(표시 전용).
        /// </summary>
        public static string AutoHuntLine(string jobName)
        {
            var d = For(jobName);
            if (d == null) return "";
            float f = d.자동사냥적합도;
            string band = f >= 0.85f ? "최상" : f >= 0.60f ? "상" : f >= 0.50f ? "중" : "하";
            return "자동사냥 " + band;
        }

        /// <summary>
        /// "HP {115} · 공격력 {24} · 사거리 {8} · 공격 {0.4}초". JobDef 기본 스탯 블록
        /// (최대체력·공격력·사거리·공격간격)의 **유일한 런타임 소비처**. §3·§4 직업 표의 직업별
        /// HP·공격 컬럼인데 ProjectSetup이 직업마다 authored(수호기사 320/10, 검사 130/26,
        /// 궁수 115/24·사거리 8·간격 0.4…)하고 Resources/jobs/*.asset에 committed까지 됐으면서
        /// 읽는 코드 0곳이었다(컨셉·스킬·사망리스크·이동기·자동사냥적합도가 겪은 「정의만 있고 부르는
        /// 곳 0」 함정과 동일 계열 — 전투 W3Party는 자체 상수표를 쓰고 asset 필드를 안 읽는다).
        /// 매칭 직업이 없으면(기본직 탱/딜/… 또는 에셋 미로드) 빈 문자열 — 호출부는 이때 줄을
        /// 그리지 않는다(지어내지 않음). 밸런스·전투 수치는 안 건드린다(표시 전용, committed 값 그대로).
        /// </summary>
        public static string StatLine(string jobName)
        {
            var d = For(jobName);
            if (d == null) return "";
            return "HP " + d.최대체력.ToString("0.#")
                 + " · 공격력 " + d.공격력.ToString("0.#")
                 + " · 사거리 " + d.사거리.ToString("0.#")
                 + " · 공격 " + d.공격간격.ToString("0.#") + "초";
        }
    }
}
