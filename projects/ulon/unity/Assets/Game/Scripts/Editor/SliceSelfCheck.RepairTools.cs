using System;
using System.IO;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 검수 구멍 표(2026-09-06)에서 나온 「서버에만 있고 화면에 없던 것」 세 가지를 닫는다:
        /// 도구 사용 횟수(§18.8), 동료 슬롯(§18.12), **수리 전용 행동**(§5.1).
        /// 판정은 두 겹이다 — 서버 행동이 실제로 되는가(TryRepair 성공/실패), 그리고 그 값을 **HUD가 읽는가**.
        /// 값이 서버에만 있으면 플레이어에게는 없는 기능이다(이 프로젝트의 도달 기준).
        /// </summary>
        static void AssertRepairAndToolReadouts()
        {
            var forgeGo = GameObject.Find("Forge");
            var forge = forgeGo != null ? forgeGo.GetComponent<CraftStation>() : null;
            if (forge == null)
                throw new InvalidOperationException("마을 대장간(Forge)이 없어 수리를 검사할 수 없습니다.");

            var worldGo = new GameObject("selfcheck-repair-world");
            GameObject bodyGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                bodyGo = new GameObject("selfcheck-repair-body");
                bodyGo.transform.position = forgeGo.transform.position;
                var body = bodyGo.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.CharacterId = "repair-body";
                body.RecalcFromStr(30);
                body.ResetHp();
                var bag = bodyGo.AddComponent<InventoryBag>();

                int max = ItemCatalog.MaxUsesOf(ItemCatalog.IronSword);
                if (max <= 2)
                    throw new InvalidOperationException("철검 최대 사용 횟수가 " + max + "입니다 — 수리를 검사할 수 없습니다.");
                bag.Add(new ItemRecord { TemplateId = ItemCatalog.IronSword, Amount = 1, Uses = max - 5 });

                // ① 재료가 없으면 수리도 실패해야 한다(네거티브 컨트롤 — 공짜 수리 금지).
                var free = world.TryRepair(body, forge);
                if (free.Applied)
                    throw new InvalidOperationException("재료 없이 수리가 됐습니다 — 수리는 재료 1개를 먹어야 합니다(§5.1).");

                // ② 재료가 있으면 수리되고 재료가 줄어야 한다.
                var recipe = CraftRecipes.Find(forge.RecipeId);
                if (recipe == null)
                    throw new InvalidOperationException("대장간 제작법을 못 찾았습니다: " + forge.RecipeId);
                bag.Add(new ItemRecord { TemplateId = recipe.Ingredient, Amount = 1 });
                int before = UsesOf(bag, ItemCatalog.IronSword);
                var done = world.TryRepair(body, forge);
                if (!done.Applied)
                    throw new InvalidOperationException("재료가 있는데 수리가 실패했습니다: " + done.FailReason);
                int after = UsesOf(bag, ItemCatalog.IronSword);
                if (after <= before)
                    throw new InvalidOperationException("수리했는데 사용 횟수가 안 늘었습니다: " + before + " → " + after);
                if (bag.ToolUses(recipe.Ingredient) != 0 && CountOf(bag, recipe.Ingredient) != 0)
                    throw new InvalidOperationException("수리가 재료를 안 먹었습니다.");

                // ③ 사거리 밖에서는 안 된다(제작대 없이 길에서 수리 금지).
                bodyGo.transform.position = forgeGo.transform.position + Vector3.right * (forge.InteractRange + 5f);
                bag.Add(new ItemRecord { TemplateId = recipe.Ingredient, Amount = 1 });
                var far = world.TryRepair(body, forge);
                if (far.Applied || far.FailReason != "range")
                    throw new InvalidOperationException("사거리 밖 수리가 막히지 않았습니다: " + far.FailReason);
            }
            finally
            {
                if (bodyGo != null)
                    UnityEngine.Object.DestroyImmediate(bodyGo);
                UnityEngine.Object.DestroyImmediate(worldGo);
            }

            // 화면에서 읽히는가 — HUD가 그 값을 실제로 부르는지 본다. 서버에만 있으면 없는 기능이다.
            string hud = Path.Combine(Application.dataPath, "Game/Scripts/Client/SliceHud.cs");
            if (!File.Exists(hud))
                throw new InvalidOperationException("SliceHud.cs가 없습니다 — 화면 도달을 검사할 수 없습니다.");
            string src = File.ReadAllText(hud);
            string[] need = { "ToolUses(", "CountFollowers(", "RepairAt(" };
            string[] why =
            {
                "도구 사용 횟수(§18.8) — 곡괭이가 언제 닳는지 화면에 없다",
                "동료 슬롯(§18.12) — 몇 마리까지 데리고 다닐 수 있는지 화면에 없다",
                "수리 버튼(§5.1) — 수리가 「재료 모자란 제작」에만 얹혀 있으면 플레이어는 못 한다",
            };
            for (int i = 0; i < need.Length; i++)
                if (src.IndexOf(need[i], StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("HUD가 " + need[i] + "을(를) 안 읽습니다 — " + why[i] + ".");

            Debug.Log("[Ulon] 수리·도구·동료 표시 통과 — TryRepair(재료 소비·사거리 제한, 재료 없으면 실패) + HUD 표시 3종");
        }

        static int UsesOf(InventoryBag bag, string templateId)
        {
            for (int i = 0; i < bag.Items.Count; i++)
                if (bag.Items[i].TemplateId == templateId)
                    return bag.Items[i].Uses;
            return -1;
        }

        static int CountOf(InventoryBag bag, string templateId)
        {
            int n = 0;
            for (int i = 0; i < bag.Items.Count; i++)
                if (bag.Items[i].TemplateId == templateId)
                    n += Mathf.Max(1, bag.Items[i].Amount);
            return n;
        }
    }
}
