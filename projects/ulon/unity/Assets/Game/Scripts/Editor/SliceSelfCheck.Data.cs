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

        /// <summary>
        /// 기획서 12.2 — 몬스터 수치(HP·키·STR·저항·피해대)가 mobs.json에서 온다는 것을 증명한다.
        /// </summary>
        static void AssertMobDataFile()
        {
            AssertDungeon3Leftover();

            MobData.Reload();
            if (string.IsNullOrEmpty(MobData.LoadedFrom))
                throw new InvalidOperationException("몬스터 수치 원장을 못 읽었습니다: " + MobData.FullPath + " (" + MobData.LoadError + ")");
            if (MobData.Count != 14)
                throw new InvalidOperationException("몬스터 수치 원장은 14종이어야 합니다: " + MobData.Count);

            // 파일 값이 옮기기 전 코드 값과 같은지(밸런스 무변경 이관)
            if (MobCatalog.MaxHpOf(MobCatalog.Skeleton) != 30f || MobCatalog.MaxHpOf(MobCatalog.IronTyrant) != 210f)
                throw new InvalidOperationException("몬스터 HP가 원장과 다릅니다.");
            if (MobCatalog.HeightOf(MobCatalog.IronTyrant) != 2.95f)
                throw new InvalidOperationException("강철폭군 키는 2.95여야 합니다.");
            if (MobCatalog.DisplayNameOf(MobCatalog.Hart) != TameCritter.DisplayName ||
                MobCatalog.DisplayNameOf(MobCatalog.Boar) != TameBoar.DisplayName)
                throw new InvalidOperationException("조련 대상 이름이 코드 상수와 어긋납니다.");
            if (!MobCatalog.IsBoss(MobCatalog.BoneWarden) || MobCatalog.IsBoss(MobCatalog.Skeleton))
                throw new InvalidOperationException("보스 판정이 원장과 다릅니다.");
            if (MobCatalog.KillDropOf(MobCatalog.IronTyrant) != ItemCatalog.TyrantCore ||
                MobCatalog.KillDropOf(MobCatalog.Skeleton) != "")
                throw new InvalidOperationException("보스 드랍이 원장과 다릅니다.");
            if (!MobCatalog.TamableOf(MobCatalog.Hart) || MobCatalog.TamableOf(MobCatalog.Knight))
                throw new InvalidOperationException("조련 가능 판정이 원장과 다릅니다.");
            MobCatalog.LoreStats(MobCatalog.Knight, out int str, out int resist, out int dmgMin, out int dmgMax);
            if (str != 45 || resist != 5 || dmgMin != 6 || dmgMax != 12)
                throw new InvalidOperationException("기사 LoreStats가 원장과 다릅니다.");

            // 원장 증명 — 파일을 고치면 재빌드 없이 밸런스가 바뀐다.
            string path = MobData.FullPath;
            string backup = File.ReadAllText(path);
            try
            {
                File.WriteAllText(path, backup.Replace("\"hp\": 210.0", "\"hp\": 999.0"));
                MobData.Reload();
                if (MobCatalog.MaxHpOf(MobCatalog.IronTyrant) != 999f)
                    throw new InvalidOperationException("파일을 고쳐도 MobCatalog가 안 따라옵니다 — 수치가 아직 코드에 묶여 있습니다.");
            }
            finally
            {
                File.WriteAllText(path, backup);
                MobData.Reload();
            }

            if (MobCatalog.MaxHpOf(MobCatalog.IronTyrant) != 210f)
                throw new InvalidOperationException("원장 복구 실패 — 강철폭군 HP가 210으로 안 돌아왔습니다.");

            Debug.Log("[Ulon] 몬스터 수치 원장 " + MobData.Count + "종 — " + MobData.LoadedFrom);
        }
    }
}
