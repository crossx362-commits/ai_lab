using System;
using System.IO;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        [Serializable]
        class RecipeFileProbe
        {
            public RecipeStat[] recipes;
        }

        /// <summary>
        /// 기획서 12.2 — 제작법(재료·개수·결과·스킬·난이도)이 코드가 아니라 recipes.json에서 온다는 것을 증명한다.
        /// 문자열 치환이 아니라 **레코드를 구조체로 고쳐 다시 써서** `CraftRecipes.Find`가 따라오는지 본다.
        /// </summary>
        static void AssertRecipeDataFile()
        {
            RecipeData.Reload();
            if (string.IsNullOrEmpty(RecipeData.LoadedFrom))
                throw new InvalidOperationException("제작법 원장을 못 읽었습니다: " + RecipeData.FullPath + " (" + RecipeData.LoadError + ")");
            if (RecipeData.Count < CraftRecipes.CodeDefaults.Length)
                throw new InvalidOperationException("제작법 원장이 " + RecipeData.Count + "종입니다 — 코드 기본값 " +
                    CraftRecipes.CodeDefaults.Length + "종이 모두 원장에 있어야 합니다(12.2).");

            // 밸런스 무변경 이관 — 원장 값이 코드 기본값과 같은가.
            var defaults = CraftRecipes.CodeDefaults;
            for (int i = 0; i < defaults.Length; i++)
            {
                var d = defaults[i];
                var now = CraftRecipes.Find(d.Id);
                if (now == null)
                    throw new InvalidOperationException("제작법 " + d.Id + "이(가) 사라졌습니다.");
                if (now.Ingredient != d.Ingredient || now.Count != d.Count || now.Output != d.Output ||
                    now.Skill != d.Skill || Mathf.Abs(now.Difficulty - d.Difficulty) > 0.001f || now.CanRepair != d.CanRepair)
                    throw new InvalidOperationException("제작법 " + d.Id + "의 원장 값이 코드 기본값과 다릅니다 — " +
                        "이관은 밸런스를 바꾸지 않아야 합니다(원장 " + now.Ingredient + "×" + now.Count + "→" + now.Output +
                        "/" + now.Skill + "/" + now.Difficulty + ", 코드 " + d.Ingredient + "×" + d.Count + "→" + d.Output +
                        "/" + d.Skill + "/" + d.Difficulty + ").");
            }

            string path = RecipeData.FullPath;
            string backup = File.ReadAllText(path);
            try
            {
                // ① 원장 증명 — 파일을 고치면 제작법이 따라온다.
                var probe = JsonUtility.FromJson<RecipeFileProbe>(backup);
                if (probe == null || probe.recipes == null || probe.recipes.Length == 0)
                    throw new InvalidOperationException("제작법 원장을 구조체로 못 읽었습니다: " + path);
                int hit = -1;
                for (int i = 0; i < probe.recipes.Length; i++)
                    if (probe.recipes[i].id == "iron_sword") hit = i;
                if (hit < 0)
                    throw new InvalidOperationException("원장에 iron_sword 제작법이 없습니다 — 증명할 대상이 없습니다.");
                probe.recipes[hit].count = 7;
                probe.recipes[hit].difficulty = 42f;
                File.WriteAllText(path, JsonUtility.ToJson(probe, true));
                RecipeData.Reload();
                var changed = CraftRecipes.Find("iron_sword");
                if (changed == null || changed.Count != 7 || Mathf.Abs(changed.Difficulty - 42f) > 0.001f)
                    throw new InvalidOperationException("파일을 고쳐도 CraftRecipes가 안 따라옵니다 — 제작법이 아직 코드에 묶여 있습니다(12.2).");

                // ② 레코드 검증 — 불량 레코드만 버리고 코드 기본값으로 떨어지는가.
                probe = JsonUtility.FromJson<RecipeFileProbe>(backup);
                probe.recipes[hit].count = 0;                     // 재료 없이 무한 제작
                int other = hit == 0 ? 1 : 0;
                probe.recipes[other].skill = "NotASkill";          // SkillId에 없는 이름
                File.WriteAllText(path, JsonUtility.ToJson(probe, true));
                RecipeData.Reload();
                if (RecipeData.LoadError.IndexOf("불량 레코드 2건", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("불량 제작법 2건이 사유와 함께 걸러지지 않았습니다: " + RecipeData.LoadError);
                var fell = CraftRecipes.Find("iron_sword");
                if (fell == null || fell.Count != 2)
                    throw new InvalidOperationException("불량 레코드가 코드 기본값(iron_ore×2)으로 안 떨어집니다: " +
                        (fell == null ? "null" : fell.Count.ToString()));
            }
            finally
            {
                File.WriteAllText(path, backup);
                RecipeData.Reload();
            }

            var restored = CraftRecipes.Find("iron_sword");
            if (restored == null || restored.Count != 2 || Mathf.Abs(restored.Difficulty - 15f) > 0.001f)
                throw new InvalidOperationException("원장 복구 실패 — iron_sword가 iron_ore×2/난이도 15로 안 돌아왔습니다.");

            // 실행 중 반영 경로(12.2) — GM 「원장 다시 읽기」가 제작법도 다시 읽어야 한다.
            string worldPath = Path.Combine(Application.dataPath, "Game/Scripts/Server/OfflineWorld.Travel.cs");
            if (!File.Exists(worldPath) || File.ReadAllText(worldPath).IndexOf("RecipeData.Reload()", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("GmReloadLedgers가 제작법 원장을 다시 읽지 않습니다 — 파일을 고쳐도 실행 중에는 캐시가 그대로입니다(12.2).");

            Debug.Log("[Ulon] 제작법 원장 " + RecipeData.Count + "종 — " + RecipeData.LoadedFrom + " (불량 레코드 폐기·코드 폴백·GM 재적재 확인)");
        }
    }
}
