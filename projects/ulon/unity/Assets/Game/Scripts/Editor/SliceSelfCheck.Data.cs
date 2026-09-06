using System;
using System.IO;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 기획서 12.2 — 아이템 수치가 코드가 아니라 데이터 파일에서 온다는 것을 증명한다.
        /// 파일 값을 실제로 바꿔 ItemCatalog가 따라오는지 보고(원장 증명), 끝나면 원본을 되돌린다.
        /// </summary>
        static void AssertItemDataFile()
        {
            AssertDungeon3Leftover();

            ItemData.Reload();
            if (string.IsNullOrEmpty(ItemData.LoadedFrom))
                throw new InvalidOperationException("아이템 수치 원장을 못 읽었습니다: " + ItemData.FullPath + " (" + ItemData.LoadError + ")");
            if (ItemData.Count < 27)
                throw new InvalidOperationException("아이템 수치 원장 항목이 27개 미만입니다: " + ItemData.Count);

            // 파일 값이 지금 코드 기본값과 같은지(밸런스 무변경 이관)
            if (ItemCatalog.WeightOf(ItemCatalog.IronSword) != 8f)
                throw new InvalidOperationException("철검 무게는 8이어야 합니다: " + ItemCatalog.WeightOf(ItemCatalog.IronSword));
            if (ItemCatalog.BuyPrice(ItemCatalog.IronSword) != 40)
                throw new InvalidOperationException("철검 구매가는 40이어야 합니다.");
            if (ItemCatalog.MaxUsesOf(ItemCatalog.IronSword) != 40)
                throw new InvalidOperationException("철검 내구는 40이어야 합니다.");
            if (ItemCatalog.StrReqOf(ItemCatalog.IronSword) != 25)
                throw new InvalidOperationException("철검 StrReq는 25여야 합니다.");
            if (!ItemCatalog.IsContainer(ItemCatalog.Pouch) || ItemCatalog.IsContainer(ItemCatalog.Cloth))
                throw new InvalidOperationException("pouch만 컨테이너여야 합니다.");
            if (ItemCatalog.WeightOf("iron_ore") != 2f || ItemCatalog.BuyPrice("resin") != 4)
                throw new InvalidOperationException("자원 수치가 원장과 다릅니다.");

            // 원장 증명 — 파일을 고치면 재빌드 없이 밸런스가 바뀐다.
            string path = ItemData.FullPath;
            string backup = File.ReadAllText(path);
            try
            {
                File.WriteAllText(path, backup.Replace("\"weight\": 8.0", "\"weight\": 99.0"));
                ItemData.Reload();
                if (ItemCatalog.WeightOf(ItemCatalog.IronSword) != 99f)
                    throw new InvalidOperationException("파일을 고쳐도 ItemCatalog가 안 따라옵니다 — 수치가 아직 코드에 묶여 있습니다.");
            }
            finally
            {
                File.WriteAllText(path, backup);
                ItemData.Reload();
            }

            if (ItemCatalog.WeightOf(ItemCatalog.IronSword) != 8f)
                throw new InvalidOperationException("원장 복구 실패 — 철검 무게가 8로 안 돌아왔습니다.");

            Debug.Log("[Ulon] 아이템 수치 원장 " + ItemData.Count + "종 — " + ItemData.LoadedFrom);
        }
    }
}
