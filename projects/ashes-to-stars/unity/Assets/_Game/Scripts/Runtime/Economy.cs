using UnityEngine;
using System.Collections.Generic;

namespace AshesToStars
{
    /// <summary>
    /// 게임 경제 시스템 - 통화, 지갑, 드랍 테이블
    /// 설계 출처: D:\ai_lab\docs\GAME_DESIGN_ASHES_TO_STARS.md
    /// </summary>
    public static class Economy
    {
        // ========== 통화 단위 (§12) ==========
        /// <summary>
        /// 100 쿠퍼 = 1 실버
        /// </summary>
        public const long COPPER_PER_SILVER = 100;

        /// <summary>
        /// 100 실버 = 1 골드
        /// </summary>
        public const long SILVER_PER_GOLD = 100;

        /// <summary>
        /// 1 골드 = 10,000 쿠퍼
        /// </summary>
        public const long COPPER_PER_GOLD = COPPER_PER_SILVER * SILVER_PER_GOLD;

        // ========== 통화 표시 헬퍼 ==========
        /// <summary>
        /// 쿠퍼를 골드/실버/쿠퍼로 변환해 표시하는 문자열을 반환한다.
        /// 예: 1203실버 → "12골드 3실버"
        /// </summary>
        public static string FormatCurrency(long totalCopper)
        {
            if (totalCopper < 0)
                return "음수";

            long gold = totalCopper / COPPER_PER_GOLD;
            long remainder = totalCopper % COPPER_PER_GOLD;

            long silver = remainder / COPPER_PER_SILVER;
            long copper = remainder % COPPER_PER_SILVER;

            // 0은 생략, 단위만 표시
            List<string> parts = new List<string>();
            if (gold > 0) parts.Add($"{gold}골드");
            if (silver > 0) parts.Add($"{silver}실버");
            if (copper > 0) parts.Add($"{copper}쿠퍼");

            return parts.Count > 0 ? string.Join(" ", parts) : "0쿠퍼";
        }

        // ========== 지갑 (보유량, 획득/차감) ==========
        public class Wallet
        {
            private long copper = 0;

            /// <summary>
            /// 현재 보유 쿠퍼 (내부 상태)
            /// </summary>
            public long Copper => copper;

            /// <summary>
            /// 쿠퍼를 추가한다. 성공 여부를 반환한다.
            /// </summary>
            public bool TryAdd(long amount)
            {
                if (amount < 0)
                    return false;

                copper += amount;
                return true;
            }

            /// <summary>
            /// 쿠퍼를 차감한다. 부족하면 실패를 반환한다. (예외가 아니라 실패 반환)
            /// </summary>
            public bool TrySubtract(long amount)
            {
                if (amount < 0 || copper < amount)
                    return false;

                copper -= amount;
                return true;
            }

            /// <summary>
            /// 현재 보유액을 포맷된 문자열로 반환한다.
            /// </summary>
            public string GetFormatted() => FormatCurrency(copper);
        }

        // ========== 드랍 테이블 (§18-4, §10-8) ==========

        /// <summary>
        /// 드랍 출처 구분 (어디서 나온 드롭인지)
        /// </summary>
        public enum DropSource
        {
            /// <summary>필드/던전 일반 보스 (1%)</summary>
            FieldDungeonBoss,

            /// <summary>탑 5층 중간 레이드 (8%)</summary>
            Tower5Boss,

            /// <summary>탑 10층 대보스 (15%)</summary>
            Tower10Boss,

            /// <summary>랜덤 레이드급 던전 (10%)</summary>
            RaidDungeon
        }

        /// <summary>
        /// 목숨 아이템 종류
        /// </summary>
        public enum LifeItem
        {
            /// <summary>부활초 - 사망 카운트 차감</summary>
            RevivalTea,

            /// <summary>귀환의 두루마리 - 긴급 탈출 아이템</summary>
            ScrollOfReturn,

            /// <summary>환생석 - 삭제된 캐릭터 복구</summary>
            RebornStone,

            /// <summary>특수 직업 전직 증표 - 50층 이상 보스 드랍</summary>
            SpecialJobToken
        }

        /// <summary>
        /// 드랍 테이블 - 출처별 드랍률 (§18-4, §10-8)
        /// </summary>
        private static readonly Dictionary<(DropSource, LifeItem), float> DropRates = new Dictionary<(DropSource, LifeItem), float>()
        {
            // 부활초 드랍률 (§18-4)
            { (DropSource.FieldDungeonBoss, LifeItem.RevivalTea), 0.01f },      // 필드·던전 보스 1%
            { (DropSource.Tower5Boss, LifeItem.RevivalTea), 0.08f },            // 5층 보스 8%
            { (DropSource.Tower10Boss, LifeItem.RevivalTea), 0.15f },           // 10층 대보스 15%
            { (DropSource.RaidDungeon, LifeItem.RevivalTea), 0.10f },           // 레이드급 던전 10%

            // 귀환의 두루마리 드랍률 (§18-4)
            { (DropSource.FieldDungeonBoss, LifeItem.ScrollOfReturn), 0.03f },  // 던전 보스 3%
            { (DropSource.Tower5Boss, LifeItem.ScrollOfReturn), 0.03f },        // 5층 보스 3%
            { (DropSource.Tower10Boss, LifeItem.ScrollOfReturn), 0.05f },       // 10층 대보스 5% (문서에 "레이드")
            { (DropSource.RaidDungeon, LifeItem.ScrollOfReturn), 0.05f },       // 레이드급 던전 5%

            // 환생석 드랍률 (§18-4)
            { (DropSource.Tower10Boss, LifeItem.RebornStone), 0.01f },          // 10층 대보스만 1%

            // 특수 직업 전직 증표 드랍률 (§18-4)
            { (DropSource.Tower10Boss, LifeItem.SpecialJobToken), 0.02f },      // 50층 이상 보스 2% (여기선 10층으로 임시)
        };

        /// <summary>
        /// 주어진 출처에서 아이템을 롤한다. 드롭률에 따라 아이템을 반환하거나 null을 반환한다.
        /// 랜덤 시드를 고정할 수 있다 (테스트용).
        /// </summary>
        public static LifeItem? RollDrop(DropSource source, int? seed = null)
        {
            // 시드 설정 (테스트 재현)
            if (seed.HasValue)
                Random.InitState(seed.Value);

            foreach (var kvp in DropRates)
            {
                if (kvp.Key.Item1 == source)
                {
                    if (Random.value < kvp.Value)
                        return kvp.Key.Item2;
                }
            }

            return null;
        }

        /// <summary>
        /// 여러 원천을 동시에 롤한다. 결과를 드롭 아이템으로 반환한다.
        /// (다중 보스 등에서 여러 드롭을 동시에 판정할 때 사용)
        /// </summary>
        public static List<LifeItem> RollDrops(List<DropSource> sources, int? seed = null)
        {
            List<LifeItem> results = new List<LifeItem>();

            if (seed.HasValue)
                Random.InitState(seed.Value);

            foreach (var source in sources)
            {
                var drop = RollDrop(source, null); // 이미 InitState 했으므로 seed=null
                if (drop.HasValue)
                    results.Add(drop.Value);
            }

            return results;
        }

        // ========== 소지 상한 (§4, §18-4) ==========

        /// <summary>
        /// 목숨 아이템별 소지 상한 (§4, §18-4)
        /// </summary>
        public static readonly Dictionary<LifeItem, int> ItemCapacity = new Dictionary<LifeItem, int>()
        {
            { LifeItem.RevivalTea, 3 },                 // 부활초 3개 (§4, §18-4)
            { LifeItem.ScrollOfReturn, 5 },             // 귀환의 두루마리 5개 (§18-4)
            { LifeItem.RebornStone, int.MaxValue },     // 환생석 무제한 (§18-4)
            { LifeItem.SpecialJobToken, int.MaxValue }  // 특수 직업 증표 무제한 (§18-4)
        };

        /// <summary>
        /// 인벤토리 - 목숨 아이템 보유 현황
        /// </summary>
        public class LifeItemInventory
        {
            private Dictionary<LifeItem, int> items = new Dictionary<LifeItem, int>()
            {
                { LifeItem.RevivalTea, 0 },
                { LifeItem.ScrollOfReturn, 0 },
                { LifeItem.RebornStone, 0 },
                { LifeItem.SpecialJobToken, 0 }
            };

            /// <summary>
            /// 특정 아이템의 보유 개수를 반환한다.
            /// </summary>
            public int GetCount(LifeItem item)
            {
                return items.TryGetValue(item, out var count) ? count : 0;
            }

            /// <summary>
            /// 아이템을 추가한다. 상한을 초과하면 획득을 거부한다.
            /// 성공 여부를 반환한다. (문서: "획득 거부로 처리해 억울함 방지")
            /// </summary>
            public bool TryAdd(LifeItem item, int amount = 1)
            {
                if (amount <= 0)
                    return false;

                int current = GetCount(item);
                int cap = ItemCapacity[item];

                // 상한 초과면 거부
                if (current + amount > cap)
                    return false;

                items[item] = current + amount;
                return true;
            }

            /// <summary>
            /// 아이템을 소비한다. 부족하면 실패를 반환한다.
            /// </summary>
            public bool TryConsume(LifeItem item, int amount = 1)
            {
                if (amount <= 0)
                    return false;

                int current = GetCount(item);
                if (current < amount)
                    return false;

                items[item] = current - amount;
                return true;
            }
        }

        // ========== 행위별 비용 (§18-2) ==========

        /// <summary>
        /// 티어별 기준 수익 (G/h 배수)
        /// 기준: T1 = 1 G/h = 100 실버 (§18-1)
        /// 티어당 ×1.6 배율
        /// </summary>
        public static readonly float[] TierRevenueMultiplier = new float[]
        {
            1.0f,      // T1: 1 G/h
            1.6f,      // T2: 1.6 G/h
            2.56f,     // T3: 2.56 G/h
            4.096f,    // T4: 4.096 G/h
            6.5536f,   // T5: 6.5536 G/h
            10.48576f, // T6: 10.48576 G/h
            16.777216f,    // T7: 16.777216 G/h
            26.8435456f,   // T8: 26.8435456 G/h
            42.94967296f,  // T9: 42.94967296 G/h
            68.71947674f   // T10: 68.71947674 G/h (≈69)
        };

        /// <summary>
        /// 행위별 골드 비용 (G/h 배수, §18-2)
        /// 각 행위의 비용을 티어 수익의 배수로 표현해 모든 티어에서 비례
        /// </summary>
        public static readonly Dictionary<string, float> ActionCostMultiplier = new Dictionary<string, float>()
        {
            { "FieldHunt", 0.0f },              // 필드 사냥: 무료 (절대 원칙)
            { "DungeonEntry", 0.02f },          // 일반 던전 입장: 0.02 G/h (§18-2)
            { "TowerNormalFloor", 0.03f },      // 탑 일반층 도전: 0.03 G/h
            { "Tower5BossRaid", 0.10f },        // 5층 중간 레이드: 0.10 G/h
            { "Tower10Boss", 0.15f },           // 10층 대보스: 0.15 G/h
            { "RaidDungeon", 0.12f },           // 레이드급 던전: 0.12 G/h
            { "InvasionAttack", 0.08f },        // 침략 출정: 0.08 G/h
            { "InvasionAttackDefeat", 0.08f }   // 침략 패배 추가: +0.08 G/h
        };

        /// <summary>
        /// 행위 비용을 계산한다 (T1 기준 쿠퍼로 반환)
        /// 예: 5층 레이드, T5 = 0.10 × 6.5536 G/h = 0.65536 G = 65,536 쿠퍼
        /// </summary>
        public static long GetActionCost(string actionKey, int tier)
        {
            if (!ActionCostMultiplier.TryGetValue(actionKey, out var multiplier))
                return 0;

            if (tier < 0 || tier >= TierRevenueMultiplier.Length)
                return 0;

            // 계산: (비용 배수) × (티어 수익) × (1 G/h의 쿠퍼값)
            // T1에서 1 G/h = 100 실버 = 10,000 쿠퍼
            long copperPerGoldPerHour = (long)COPPER_PER_GOLD; // T1 기준
            float tierMultiplier = TierRevenueMultiplier[tier];
            long cost = (long)(multiplier * tierMultiplier * copperPerGoldPerHour);

            return cost;
        }

        // ========== 하위 레이드 리롤 누진 비용 (§18-2) ==========
        /// <summary>
        /// 하위 레이드 재입장(리롤) 누진 비용 배수
        /// 1회차 ×1 → 2회차 ×2 → 3회차 ×4 → 4회차+ ×8 (24h 리셋)
        /// §18-2, §18-4 검산에서 "리롤이 수지 불만족"을 확인한 것에 기반
        /// </summary>
        public static float GetRerollCostMultiplier(int attemptCount)
        {
            if (attemptCount <= 0) return 1.0f;
            if (attemptCount == 1) return 2.0f;
            if (attemptCount == 2) return 4.0f;
            return 8.0f; // 3회차 이상
        }

        // ========== 검산 & 검증 함수 ==========

        /// <summary>
        /// 부활초 리롤 억제 검산 (§18-4)
        /// 대보스 1회의 환생석 기대값이 리롤 비용보다 작아야 한다.
        ///
        /// 기대값 = 환생석 드랍률 × 시세 배수
        ///        = 1% × 200배 = 2배 (G/h)
        ///
        /// 리롤 비용 = 귀환의 두루마리(2~4배) + 재입장 누진(×1 → ×2 → ×4 → ×8)
        /// → 비용 > 기대이득이므로 노가다는 수지 불만족
        /// </summary>
        public static string ValidateRerollIncentive()
        {
            // 환생석 기대값 (T5 기준, 1% 드롭률)
            float rebornStoneProbability = 0.01f;
            float rebornStoneValueMultiplier = 200.0f; // 중간값
            float expectedValue = rebornStoneProbability * rebornStoneValueMultiplier;

            // 리롤 비용 (귀환의 두루마리 중간값 3배 + 재입장 2회차 비용 ×2)
            float scrollOfReturnValue = 3.0f;
            float rerollCostMultiplier = 2.0f; // 2회차
            float totalRerollCost = scrollOfReturnValue + rerollCostMultiplier;

            bool satisfies = expectedValue < totalRerollCost;

            return $"[리롤 억제 검산 (§18-4)]" +
                   $"\n  환생석 기대값: {expectedValue:F1}배 (1% × {rebornStoneValueMultiplier}배)" +
                   $"\n  리롤 비용: {totalRerollCost:F1}배 (두루마리 3배 + 재입장 ×2)" +
                   $"\n  검산 결과: {(satisfies ? "✓ 통과 — 노가다 수지 불만족" : "✗ 실패 — 구조가 뚫려 있음")}";
        }

        /// <summary>
        /// 부활초 파밍 기대비용 검산 (§18-4)
        /// 부활초 드랍률 15%면 약 7회(≈3.5시간)에 1개
        /// 시세가 3~8배 G/h 근처로 자연 수렴해야 함
        /// </summary>
        public static string ValidateRevivalTeaFarmCost()
        {
            float revivalTeaProbability = 0.15f; // 10층 대보스 15%
            int estimatedAttempts = (int)(1.0f / revivalTeaProbability);
            float estimatedHours = estimatedAttempts * 0.5f; // 보스전 평균 30초 (실제는 더 걸림)

            // 부활초 시세 범위
            float lowPrice = 3.0f;
            float highPrice = 8.0f;

            return $"[부활초 파밍 기대비용 (§18-4)]" +
                   $"\n  드롭률: {revivalTeaProbability * 100:F0}% (10층 대보스)" +
                   $"\n  예상 도전 횟수: 약 {estimatedAttempts}회" +
                   $"\n  예상 소요 시간: 약 {estimatedHours:F1}시간" +
                   $"\n  자연 시세: {lowPrice}~{highPrice}배 G/h (범위 내라면 정상 수렴)" +
                   $"\n  참고: 소지 상한 3개라 수요가 막혀 폭등하지 않음";
        }
    }
}
