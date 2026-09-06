using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획서 12.2 — 제작법도 코드가 아니라 ID 기반 데이터 파일이 원장이다.
    /// 원장: StreamingAssets/Data/recipes.json. 파일이 없거나 레코드가 불량이면 그 레코드만 버리고
    /// `CraftRecipes`의 코드 기본값으로 떨어진다(데이터 사고로 제작이 통째로 죽지 않는다).
    /// items.json·mobs.json과 같은 형식이다 — 로더와 Assert가 같은 `ReasonInvalid`를 쓴다.
    /// </summary>
    [Serializable]
    public struct RecipeStat
    {
        public string id;
        public string ingredient;
        public int count;
        public string output;
        public string skill;        // SkillId 이름(문자열) — 숫자로 두면 열거형을 바꿀 때 조용히 어긋난다
        public float difficulty;
        public bool canRepair;
    }

    public static class RecipeData
    {
        public const string FileName = "recipes.json";

        [Serializable]
        class File_
        {
            public RecipeStat[] recipes;
        }

        static Dictionary<string, RecipeStat> map;
        static string loadedFrom = "";
        static string loadError = "";

        public static string LoadedFrom { get { EnsureLoaded(); return loadedFrom; } }
        public static string LoadError { get { EnsureLoaded(); return loadError; } }
        public static int Count { get { EnsureLoaded(); return map.Count; } }
        public static string FullPath => DataLedger.PathOf(FileName);

        public static void Reload()
        {
            map = null;
            EnsureLoaded();
        }

        public static void EnsureLoaded()
        {
            if (map != null)
                return;
            map = new Dictionary<string, RecipeStat>(StringComparer.Ordinal);
            loadedFrom = "";
            loadError = "";
            if (!DataLedger.TryRead(FileName, out File_ parsed, out loadError))
                return;
            if (parsed.recipes == null)
            {
                loadError = "no recipes array: " + FullPath;
                return;
            }
            int bad = 0;
            for (int i = 0; i < parsed.recipes.Length; i++)
            {
                var rec = parsed.recipes[i];
                if (string.IsNullOrEmpty(rec.id))
                    continue;
                string why = ReasonInvalid(rec);
                if (why != "")
                {
                    bad++;
                    loadError = (loadError == "" ? "" : loadError + "; ") + rec.id + ": " + why;
                    Debug.LogError("[Ulon] recipes.json 레코드 무시 — " + rec.id + ": " + why + " (코드 기본값으로 떨어집니다)");
                    continue;
                }
                map[rec.id] = rec;
            }
            loadedFrom = FullPath;
            if (bad > 0)
                loadError = "불량 레코드 " + bad + "건 — " + loadError;
        }

        /// <summary>불량이면 사유, 정상이면 빈 문자열. 로더와 Assert가 같은 판정을 쓴다.</summary>
        public static string ReasonInvalid(RecipeStat rec)
        {
            if (rec.count <= 0)
                return "count " + rec.count + " (0 이하면 재료 없이 무한 제작)";
            if (string.IsNullOrEmpty(rec.ingredient))
                return "ingredient 없음 (재료 없는 제작법)";
            if (string.IsNullOrEmpty(rec.output))
                return "output 없음 (결과물 없는 제작법)";
            if (rec.difficulty < 0f)
                return "difficulty " + rec.difficulty + " (음수 난이도는 항상 성공)";
            if (!Enum.TryParse(rec.skill, false, out SkillId _))
                return "skill \"" + rec.skill + "\" (SkillId에 없는 이름)";
            return "";
        }

        public static bool TryGet(string id, out RecipeStat stat)
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(id) && map.TryGetValue(id, out stat))
                return true;
            stat = default;
            return false;
        }
    }
}
