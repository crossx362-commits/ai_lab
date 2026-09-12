using System;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획 §3.2 복합 직업명. 원장: StreamingAssets/Data/job_combos.json.
    /// 대표 스킬(최고값) + 보조 스킬이 min_secondary 이상이면 직업명만 바꾼다. 숙련 접두사는 TitleOf가 붙인다.
    /// 파일이 없으면 기획서 표 7행으로 폴백한다 — 데이터 사고로 칭호가 비지 않는다.
    /// </summary>
    public static class SkillJobCombos
    {
        public const string FileName = "job_combos.json";
        /// <summary>§3.2 「30 미만은 별도 숙련 칭호 없음」 — 파일 min_secondary가 비면 이 값.</summary>
        public const float DefaultMinSecondary = 30f;

        /// <summary>네거티브 컨트롤 — 켜면 조합을 안 보고 기본 직업명만 쓴다.</summary>
        public static bool NcDisable;

        [Serializable]
        public class Row
        {
            public string primary;
            public string secondary;
            public string job;
        }

        [Serializable]
        class File_
        {
            public float min_secondary;
            public string source;
            public Row[] combos;
        }

        struct Combo
        {
            public SkillId Primary;
            public SkillId Secondary;
            public string Job;
        }

        static Combo[] rows;
        static float minSecondary;
        static string loadError = "";
        static string loadedFrom = "";

        public static string LoadError
        {
            get { EnsureLoaded(); return loadError; }
        }

        public static string LoadedFrom
        {
            get { EnsureLoaded(); return loadedFrom; }
        }

        public static float MinSecondary
        {
            get { EnsureLoaded(); return minSecondary; }
        }

        public static int Count
        {
            get { EnsureLoaded(); return rows != null ? rows.Length : 0; }
        }

        public static string FullPath => DataLedger.PathOf(FileName);

        public static void Reload()
        {
            rows = null;
            EnsureLoaded();
        }

        public static bool Has(SkillId primary, SkillId secondary, string job)
        {
            EnsureLoaded();
            if (rows == null)
                return false;
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].Primary == primary && rows[i].Secondary == secondary && rows[i].Job == job)
                    return true;
            }
            return false;
        }

        public static string JobOf(SkillId primary, SkillSet skills)
        {
            if (skills == null || NcDisable)
                return SkillTitles.JobOf(primary);
            EnsureLoaded();
            if (rows == null)
                return SkillTitles.JobOf(primary);
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].Primary != primary)
                    continue;
                if (skills.Get(rows[i].Secondary) + 0.0001f < minSecondary)
                    continue;
                return rows[i].Job;
            }
            return SkillTitles.JobOf(primary);
        }

        static void EnsureLoaded()
        {
            if (rows != null)
                return;
            minSecondary = DefaultMinSecondary;
            loadError = "";
            loadedFrom = "";
            if (DataLedger.TryRead(FileName, out File_ parsed, out loadError) &&
                parsed != null && parsed.combos != null && parsed.combos.Length > 0)
            {
                var parsedRows = Parse(parsed.combos);
                if (parsedRows.Length > 0)
                {
                    rows = parsedRows;
                    if (parsed.min_secondary > 0.0001f)
                        minSecondary = parsed.min_secondary;
                    loadedFrom = DataLedger.PathOf(FileName);
                    return;
                }
                loadError = string.IsNullOrEmpty(loadError) ? "combos empty" : loadError;
            }
            rows = Fallback();
        }

        static Combo[] Parse(Row[] src)
        {
            var list = new Combo[src.Length];
            int n = 0;
            for (int i = 0; i < src.Length; i++)
            {
                var r = src[i];
                if (r == null || string.IsNullOrEmpty(r.job))
                    continue;
                if (!Enum.TryParse(r.primary, out SkillId p) || !Enum.TryParse(r.secondary, out SkillId s))
                    continue;
                if (p == s)
                    continue;
                list[n++] = new Combo { Primary = p, Secondary = s, Job = r.job };
            }
            if (n == list.Length)
                return list;
            var trimmed = new Combo[n];
            Array.Copy(list, trimmed, n);
            return trimmed;
        }

        static Combo[] Fallback()
        {
            return new[]
            {
                new Combo { Primary = SkillId.Swordsmanship, Secondary = SkillId.Magery, Job = "마검사" },
                new Combo { Primary = SkillId.Magery, Secondary = SkillId.Swordsmanship, Job = "마검사" },
                new Combo { Primary = SkillId.Archery, Secondary = SkillId.Tracking, Job = "레인저" },
                new Combo { Primary = SkillId.Healing, Secondary = SkillId.Magery, Job = "성직자" },
                new Combo { Primary = SkillId.Mining, Secondary = SkillId.Blacksmithing, Job = "광물 장인" },
                new Combo { Primary = SkillId.Blacksmithing, Secondary = SkillId.Mining, Job = "무기 장인" },
                new Combo { Primary = SkillId.AnimalTaming, Secondary = SkillId.AnimalLore, Job = "야수조련사" }
            };
        }
    }
}
