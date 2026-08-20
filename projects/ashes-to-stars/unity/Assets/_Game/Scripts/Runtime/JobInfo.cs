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

        /// <summary>
        /// "보유 스킬 — {이름} · {이름} · …". JobDef.스킬(SkillDef[])의 **유일한 런타임 소비처**.
        /// 이 배열은 ProjectSetup이 직업마다 authored(도발의 함성·성채 방패…)하고도 어떤 코드도
        /// 읽지 않아 소비처 0곳이었다(ConceptLine이 겪은 함정과 동일 계열). 매칭 직업이 없거나
        /// 스킬 배열이 비면 빈 문자열 — 호출부는 이때 줄을 그리지 않는다(지어내지 않음).
        /// </summary>
        public static string SkillLine(string jobName)
        {
            var d = For(jobName);
            if (d == null || d.스킬 == null || d.스킬.Length == 0) return "";
            var names = new System.Collections.Generic.List<string>();
            foreach (var s in d.스킬)
                if (s != null && !string.IsNullOrEmpty(s.이름)) names.Add(s.이름);
            if (names.Count == 0) return "";
            return "보유 스킬 — " + string.Join(" · ", names);
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
        /// "무적 {0.3}초 · 쿨 {6}초". JobDef.무적시간·이동기쿨의 **유일한 런타임 소비처**.
        /// §5 이동기 스펙 표(원장 376·377 ✅ 오너 결정 2026-08-13)의 무적 프레임 0.3초
        /// (원장 379 「이 게임 조작의 핵심 기술」)·쿨다운 6초 두 수치는 Resources/jobs/*.asset에
        /// committed(무적시간 0.3·이동기쿨 6, 전 직업 동일 — §5는 직업별로 이동기 *형태*만 변주하고
        /// 수치는 공통)돼 있으면서도 읽는 코드 0곳이었다(형제 필드 이동기형태는 MovementLine에서
        /// 읽는데 이 두 수치만 죽어 있었다 — 컨셉·스킬·사망리스크·이동기형태·자동사냥적합도가 겪은
        /// 함정과 동일 계열). 화면엔 이동기 *형태*("이동기 — 백스텝")만 있고 정작 핵심인 무적·쿨
        /// 수치는 안 보였다. 매칭 직업이 없으면 빈 문자열 — 호출부는 이때 조각을 붙이지 않는다
        /// (지어내지 않음). 밸런스·전투 수치는 안 건드린다(표시 전용, 커밋 값 그대로 읽음).
        /// </summary>
        public static string MobilityStatLine(string jobName)
        {
            var d = For(jobName);
            if (d == null) return "";
            if (d.무적시간 <= 0f && d.이동기쿨 <= 0f) return "";
            return "무적 " + d.무적시간.ToString("0.#") + "초 · 쿨 " + d.이동기쿨.ToString("0.#") + "초";
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
