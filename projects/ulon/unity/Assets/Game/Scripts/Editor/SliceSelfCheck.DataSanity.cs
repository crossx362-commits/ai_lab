using System;
using System.IO;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 기획서 12.2 — 원장이 코드 바깥에 있으면 **누구나 고칠 수 있다**. 그래서 불량 수치가 조용히 들어오는 길도
        /// 같이 열린다: 무게 0 아이템은 무한 적재, 음수 가격은 무한 골드, HP 0 몹은 스폰 즉시 사망이다.
        /// 여기서는 「불량 레코드를 넣으면 그 레코드만 버려지고 코드 폴백이 쓰이며 사유가 남는다」를 잰다.
        ///
        /// 판정은 문자열 치환이 아니라 **원장을 통째로 다시 써서** 한다(검수 지적 — 치환은 파일 형식이
        /// 조금만 바뀌어도 아무것도 안 바꾸고 조용히 통과한다). 끝나면 원본 바이트를 되돌린다.
        ///
        /// 이 판정 자체의 네거티브 컨트롤: 로더에서 ReasonInvalid 분기를 빼면 여기서 FAIL이 나야 한다.
        /// </summary>
        [Serializable]
        class ItemFileProbe
        {
            public ItemStat[] items;
        }

        [Serializable]
        class MobFileProbe
        {
            public MobStat[] mobs;
        }

        static void AssertDataRecordSanity()
        {
            // 지금 원장에는 불량이 없어야 한다 — 있으면 게임이 이미 코드 폴백으로 조용히 돌고 있다.
            ItemData.Reload();
            MobData.Reload();
            if (!string.IsNullOrEmpty(ItemData.LoadError))
                throw new InvalidOperationException("items.json에 불량 레코드가 있습니다: " + ItemData.LoadError);
            if (!string.IsNullOrEmpty(MobData.LoadError))
                throw new InvalidOperationException("mobs.json에 불량 레코드가 있습니다: " + MobData.LoadError);

            string itemPath = ItemData.FullPath;
            string mobPath = MobData.FullPath;
            string itemBackup = File.ReadAllText(itemPath);
            string mobBackup = File.ReadAllText(mobPath);
            try
            {
                // ── 아이템: 무게 0 / 음수 가격 / 필드 없는 레코드 ────────────────────────────
                var probe = new ItemFileProbe
                {
                    items = new[]
                    {
                        new ItemStat { id = ItemCatalog.IronSword, weight = 0f, buy = 40, uses = 40, strReq = 25 },
                        new ItemStat { id = "resin", weight = 0.2f, buy = -3 },
                        new ItemStat { id = ItemCatalog.Cloth },   // 필드 없는 레코드 = weight 0
                    }
                };
                File.WriteAllText(itemPath, JsonUtility.ToJson(probe, true));
                ItemData.Reload();

                if (ItemData.Count != 0)
                    throw new InvalidOperationException("불량 레코드 3건을 넣었는데 " + ItemData.Count + "건이 원장에 들어왔습니다 — 검증이 안 걸립니다.");
                if (ItemData.LoadError.IndexOf("불량 레코드 3건", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("불량 레코드가 LoadError에 안 남습니다: '" + ItemData.LoadError + "'");
                // 코드 폴백 — 원장이 못 쓸 값이어도 게임 수치는 코드 기본값으로 살아 있어야 한다.
                if (ItemCatalog.WeightOf(ItemCatalog.IronSword) != 8f)
                    throw new InvalidOperationException("무게 0 레코드를 버린 뒤 코드 기본값(8)이 아니라 " + ItemCatalog.WeightOf(ItemCatalog.IronSword) + "이 나옵니다 — 무게 0은 무한 적재입니다.");
                if (ItemCatalog.BuyPrice("resin") < 0)
                    throw new InvalidOperationException("음수 가격 레코드가 그대로 쓰입니다 — 팔면 골드가 늘어납니다.");
                if (ItemCatalog.WeightOf(ItemCatalog.Cloth) <= 0f)
                    throw new InvalidOperationException("필드 없는 레코드가 무게 0으로 통과했습니다 — 무한 적재입니다.");

                // ── 몹: HP 0 / 이름 없음 / 뒤집힌 피해대 ─────────────────────────────────────
                var mobProbe = new MobFileProbe
                {
                    mobs = new[]
                    {
                        new MobStat { id = MobCatalog.IronTyrant, name = "", hp = 0f, height = 2.65f },
                        new MobStat { id = MobCatalog.Skeleton, name = "해골", hp = 30f, height = 1.55f, dmgMin = 5, dmgMax = 2 },
                    }
                };
                File.WriteAllText(mobPath, JsonUtility.ToJson(mobProbe, true));
                MobData.Reload();

                if (MobData.Count != 0)
                    throw new InvalidOperationException("불량 몹 레코드 2건을 넣었는데 " + MobData.Count + "건이 원장에 들어왔습니다 — 검증이 안 걸립니다.");
                if (MobData.LoadError.IndexOf("불량 레코드 2건", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("불량 몹 레코드가 LoadError에 안 남습니다: '" + MobData.LoadError + "'");
                if (MobCatalog.MaxHpOf(MobCatalog.IronTyrant) != 210f)
                    throw new InvalidOperationException("HP 0 레코드를 버린 뒤 코드 기본값(210)이 아니라 " + MobCatalog.MaxHpOf(MobCatalog.IronTyrant) + "이 나옵니다 — 보스가 스폰 즉시 죽습니다.");
                MobCatalog.LoreStats(MobCatalog.Skeleton, out _, out _, out int dmgMin, out int dmgMax);
                if (dmgMax < dmgMin)
                    throw new InvalidOperationException("뒤집힌 피해대(dmgMax " + dmgMax + " < dmgMin " + dmgMin + ")가 그대로 쓰입니다.");
            }
            finally
            {
                File.WriteAllText(itemPath, itemBackup);
                File.WriteAllText(mobPath, mobBackup);
                ItemData.Reload();
                MobData.Reload();
            }

            if (ItemData.Count < 27 || MobData.Count != 14 || ItemData.LoadError != "" || MobData.LoadError != "")
                throw new InvalidOperationException("원장 복구 실패 — 아이템 " + ItemData.Count + "종/몹 " + MobData.Count + "종.");

            Debug.Log("[Ulon] 원장 레코드 검증 통과 — 불량 아이템 3건·몹 2건 폐기 후 코드 폴백(철검 8·강철폭군 210) 유지, 사유 로그 적재");
        }
    }
}
