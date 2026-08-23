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

        /// <summary>
        /// "보유 스킬 — {이름}(N초) · …". JobDef.스킬의 이름·**쿨다운** 소비처.
        /// 이름만 읽던 SkillLine(이전 배선) 옆에, ProjectSetup이 스킬마다 authored한
        /// <c>SkillDef.쿨다운</c>(도발의 함성 6·화염폭풍 5·최후의 보루 40…)이 정의·에셋만 있고
        /// grep 소비처 0곳이었다 — 형제 이름 필드는 읽는데 같은 SkillDef 행의 쿨다운만 죽어 있던
        /// 함정(이동기거리·전투당발동과 동일 계열). 툴팁 「마나 없음, 쿨다운 단일 체계」·원장 §3
        /// 직업 스킬 표(✅)의 수치 칸. 쿨다운 &gt; 0일 때만 「(N초)」를 이름 뒤에 붙인다(0=패시브/
        /// 게이지형은 이름만). QA_NO_SKILL_CD면 옛 줄(이름만)로 회귀. 매칭 직업이 없거나 스킬
        /// 배열이 비면 빈 문자열 — 호출부는 이때 줄을 그리지 않는다(지어내지 않음).
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
                if (!SkillCdBlocked && s.쿨다운 > 0f)
                    piece += "(" + s.쿨다운.ToString("0.#") + "초)";
                parts.Add(piece);
            }
            if (parts.Count == 0) return "";
            return "보유 스킬 — " + string.Join(" · ", parts);
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
