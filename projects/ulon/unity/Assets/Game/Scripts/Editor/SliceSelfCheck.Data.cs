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

            // 원장 증명 — 파일을 고치면 게임을 다시 빌드하지 않아도 밸런스가 바뀐다
            // (실행 중 반영은 GM 패널 「원장 다시 읽기」 = OfflineWorld.GmReloadLedgers).
            // **문자열 치환으로 하지 않는다** — 파일 서식이 조금만 바뀌면 아무것도 안 고치고 조용히 통과한다
            // (검수 2026-09-06 지적). 레코드를 구조체로 읽어 고친 뒤 다시 쓴다.
            string path = ItemData.FullPath;
            string backup = File.ReadAllText(path);
            try
            {
                var probe = JsonUtility.FromJson<ItemFileProbe>(backup);
                if (probe == null || probe.items == null || probe.items.Length == 0)
                    throw new InvalidOperationException("아이템 원장을 구조체로 못 읽었습니다: " + path);
                int hit = -1;
                for (int i = 0; i < probe.items.Length; i++)
                    if (probe.items[i].id == ItemCatalog.IronSword) hit = i;
                if (hit < 0)
                    throw new InvalidOperationException("원장에 " + ItemCatalog.IronSword + " 레코드가 없습니다 — 증명할 대상이 없습니다.");
                probe.items[hit].weight = 99f;
                File.WriteAllText(path, JsonUtility.ToJson(probe, true));
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

            // 「파일만 고치면 된다」가 실행 중에도 참이려면 **다시 읽을 길**이 있어야 한다.
            // 로더는 한 번 읽고 캐시하므로 배선이 없으면 껐다 켜야 반영된다 — 그건 12.2가 약속한 것과 다르다.
            string hudPath = System.IO.Path.Combine(Application.dataPath, "Game/Scripts/Client/SliceHud.cs");
            if (!System.IO.File.Exists(hudPath) || System.IO.File.ReadAllText(hudPath).IndexOf("GmReloadLedgers", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("GM 패널에 「원장 다시 읽기」 배선이 없습니다 — 파일을 고쳐도 실행 중에는 캐시가 그대로입니다(12.2).");

            Debug.Log("[Ulon] 아이템 수치 원장 " + ItemData.Count + "종 — " + ItemData.LoadedFrom + " (GM 원장 다시 읽기 배선)");
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
            if (MobCatalog.HeightOf(MobCatalog.IronTyrant) != 2.65f)
                throw new InvalidOperationException("강철폭군 키는 2.65여야 합니다.");
            if (MobCatalog.DisplayNameOf(MobCatalog.Hart) != "야생하트" ||
                MobCatalog.DisplayNameOf(MobCatalog.Boar) != "야생멧돼지")
                throw new InvalidOperationException("조련 대상 이름이 원장 값과 다릅니다: " +
                    MobCatalog.DisplayNameOf(MobCatalog.Hart) + "/" + MobCatalog.DisplayNameOf(MobCatalog.Boar));
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

            // 원장 증명 — 구조체로 읽어 고친 뒤 다시 쓴다(문자열 치환 금지, 위와 같은 이유).
            string path = MobData.FullPath;
            string backup = File.ReadAllText(path);
            try
            {
                var probe = JsonUtility.FromJson<MobFileProbe>(backup);
                if (probe == null || probe.mobs == null || probe.mobs.Length == 0)
                    throw new InvalidOperationException("몹 원장을 구조체로 못 읽었습니다: " + path);
                int hit = -1;
                for (int i = 0; i < probe.mobs.Length; i++)
                    if (probe.mobs[i].id == MobCatalog.IronTyrant) hit = i;
                if (hit < 0)
                    throw new InvalidOperationException("원장에 " + MobCatalog.IronTyrant + " 레코드가 없습니다 — 증명할 대상이 없습니다.");
                probe.mobs[hit].hp = 999f;
                File.WriteAllText(path, JsonUtility.ToJson(probe, true));
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
