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
    }
}
