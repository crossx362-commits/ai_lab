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
        static RaceDef[] _cache;

        static RaceDef[] Defs => _cache ??= Resources.LoadAll<RaceDef>("races");

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
        /// "종족 특성 — {고유메커니즘} (발동 {N}%)". 계정 종족의 RaceDef.고유메커니즘·고유발동확률의
        /// **유일한 런타임 소비처**. 고유발동확률(드워프 불굴 0.25·수인 야성감각 1.0·엘프 0.15,
        /// 인간 적응 0)은 에셋에 authored·committed돼 있으면서도 읽는 코드 0곳이었다 —
        /// 형제 필드 고유메커니즘은 여기서 읽는데 발동확률만 죽어 있었다. 발동확률 > 0일 때만
        /// "(발동 N%)"를 붙인다(0인 상시 패시브 적응엔 안 붙임). 에셋을 못 읽거나 값이 비면
        /// 빈 문자열 — 호출부는 이때 줄을 그리지 않는다(지어내지 않음).
        /// </summary>
        public static string MechanicLine(RaceId id)
        {
            var d = For(id);
            if (d == null || string.IsNullOrEmpty(d.고유메커니즘)) return "";
            string line = "종족 특성 — " + d.고유메커니즘;
            if (d.고유발동확률 > 0f)
                line += " (발동 " + Mathf.RoundToInt(d.고유발동확률 * 100f) + "%)";
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
    }
}
