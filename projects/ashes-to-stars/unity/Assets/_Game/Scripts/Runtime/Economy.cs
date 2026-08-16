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
            RevivalTea = 0,

            /// <summary>귀환의 두루마리 - 긴급 탈출 아이템</summary>
            ScrollOfReturn = 1,

            /// <summary>환생석 - 삭제된 캐릭터 복구</summary>
            RebornStone = 2,

            /// <summary>전직 재료 - 던전에서 파밍, 1차 전직 성공 확인 시 5개 소비</summary>
            AdvancementMaterial = 4,

            /// <summary>특수 직업 전직 증표 - 50층 이상 보스 드랍</summary>
            SpecialJobToken = 3,

            /// <summary>사냥 가죽 — 야수 계열 제작 재료(§11). 별도 채집 없음.</summary>
            CraftHide = 5,

            /// <summary>송곳니 — 야수 계열. 무기 제작.</summary>
            CraftFang = 6,

            /// <summary>유골 — 언데드 계열. 투구 제작.</summary>
            CraftBone = 7,

            /// <summary>부품 — 기계 계열. 장갑 제작.</summary>
            CraftPart = 8,

            /// <summary>원소결정 — 정령 계열. 신발 제작.</summary>
            CraftCrystal = 9,

            /// <summary>마정석 — 마족 계열. 장신구 제작.</summary>
            CraftDemonite = 10,

            /// <summary>강화석 — 던전·레이드 드랍. 장비 강화(§11·§12).</summary>
            EnhanceStone = 11
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

            // 전직 재료(§3·§6·§18-6) — 일반 던전 파밍이 주 공급처다.
            // 확정 절대 드랍률은 아직 없어 프로토타입 검증값으로 둔다.
            { (DropSource.FieldDungeonBoss, LifeItem.AdvancementMaterial), 0.35f },
            { (DropSource.RaidDungeon, LifeItem.AdvancementMaterial), 1.00f },

            // 특수 직업 전직 증표 드랍률 (§18-4)
            { (DropSource.Tower10Boss, LifeItem.SpecialJobToken), 0.02f },      // 50층 이상 보스 2% (여기선 10층으로 임시)

            // 사냥 가죽(§11) — 확정 드랍률이 없어 프로토타입 검증값. 희귀 고유템이 아니다.
            { (DropSource.FieldDungeonBoss, LifeItem.CraftHide), 0.50f },
            { (DropSource.Tower5Boss, LifeItem.CraftHide), 0.50f },
            { (DropSource.Tower10Boss, LifeItem.CraftHide), 0.80f },
            { (DropSource.RaidDungeon, LifeItem.CraftHide), 1.00f },

            // 계열 재료(§11 💡) — 사냥 드랍만. 확정 드랍률이 없어 가죽과 같은 프로토타입 값.
            { (DropSource.FieldDungeonBoss, LifeItem.CraftFang), 0.35f },
            { (DropSource.RaidDungeon, LifeItem.CraftFang), 0.80f },
            { (DropSource.FieldDungeonBoss, LifeItem.CraftBone), 0.35f },
            { (DropSource.RaidDungeon, LifeItem.CraftBone), 0.80f },
            { (DropSource.FieldDungeonBoss, LifeItem.CraftPart), 0.35f },
            { (DropSource.RaidDungeon, LifeItem.CraftPart), 0.80f },
            { (DropSource.FieldDungeonBoss, LifeItem.CraftCrystal), 0.35f },
            { (DropSource.RaidDungeon, LifeItem.CraftCrystal), 0.80f },
            { (DropSource.FieldDungeonBoss, LifeItem.CraftDemonite), 0.35f },
            { (DropSource.RaidDungeon, LifeItem.CraftDemonite), 0.80f },

            // 강화석(§10-8 정예·던전, §12 강화). 희귀 고유템이 아니라 개체별 판정.
            { (DropSource.FieldDungeonBoss, LifeItem.EnhanceStone), 0.40f },
            { (DropSource.Tower5Boss, LifeItem.EnhanceStone), 0.50f },
            { (DropSource.Tower10Boss, LifeItem.EnhanceStone), 0.70f },
            { (DropSource.RaidDungeon, LifeItem.EnhanceStone), 1.00f },
        };

        /// <summary>희귀 고유템 — ⚠️ §10-8 "전투당 1회만 판정".</summary>
        public static bool IsRare(LifeItem it) =>
            it == LifeItem.RebornStone || it == LifeItem.SpecialJobToken;

        /// <summary>
        /// 한 전투의 드랍을 전부 판정한다 (✅ §10-8 드랍 판정 규칙).
        ///
        /// ⚠️ 규칙이 두 갈래다 — 이걸 지키지 않으면 §18-4의 리롤 억제 검산이 깨진다:
        ///   · 일반 드랍(부활초·두루마리)은 **보스 개체별로** 굴린다 → 다중 등장이 "벌이가 좋은 판"이 된다
        ///   · 희귀 고유템(환생석·전직 증표)은 **전투당 1회만** 굴린다 →
        ///     3체 전투의 기대값이 3배가 되면 "리롤 노가다는 수지가 안 맞는다"가 무너진다
        ///
        /// 난수는 호출부가 넘긴 스트림을 쓴다. 예전에는 `Random.InitState`로 **전역 난수를 덮어써**
        /// 던전 생성의 결정성을 밖에서 깨뜨렸다(§3-2 규칙 1).
        /// 그리고 딕셔너리를 순회하며 **첫 히트 하나만** 돌려줘, 한 판에 두 종류가 나올 수 없었고
        /// 우선순위가 딕셔너리 순회 순서에 달려 있었다(§3-2 규칙 3이 금지한 것).
        /// </summary>
        public static List<LifeItem> RollBattleDrops(DropSource source, int bossCount, ref Rng rng)
        {
            var results = new List<LifeItem>();
            if (bossCount < 1) bossCount = 1;

            // 판정 순서를 열거형 순서로 고정한다 — 딕셔너리 순회 순서에 기대지 않는다
            foreach (LifeItem it in System.Enum.GetValues(typeof(LifeItem)))
            {
                if (!DropRates.TryGetValue((source, it), out float rate)) continue;
                int rolls = IsRare(it) ? 1 : bossCount;
                for (int i = 0; i < rolls; i++)
                    if (rng.Value01() < rate) results.Add(it);
            }
            return results;
        }

        /// <summary>
        /// 필드 사냥(보스 테이블이 아닌 잡몹 웨이브)에서 가죽만 판정한다.
        /// 보스 테이블을 그대로 쓰면 필드에서 부활초가 나와 목숨 경제가 풀린다.
        /// 프로토타입 검증값: 생존 1회에 가죽 1장 — 대장간 루프가 화면에 보여야 한다.
        /// </summary>
        public static int FieldHuntHideCount() => 1;

        // ========== 소지 상한 (§4, §18-4) ==========

        /// <summary>
        /// 목숨 아이템별 소지 상한 (§4, §18-4)
        /// </summary>
        public static readonly Dictionary<LifeItem, int> ItemCapacity = new Dictionary<LifeItem, int>()
        {
            { LifeItem.RevivalTea, 3 },                 // 부활초 3개 (§4, §18-4)
            { LifeItem.ScrollOfReturn, 5 },             // 귀환의 두루마리 5개 (§18-4)
            { LifeItem.RebornStone, int.MaxValue },     // 환생석 무제한 (§18-4)
            { LifeItem.AdvancementMaterial, int.MaxValue }, // 전직 요구량 누적 파밍
            { LifeItem.SpecialJobToken, int.MaxValue },  // 특수 직업 증표 무제한 (§18-4)
            { LifeItem.CraftHide, int.MaxValue },
            { LifeItem.CraftFang, int.MaxValue },
            { LifeItem.CraftBone, int.MaxValue },
            { LifeItem.CraftPart, int.MaxValue },
            { LifeItem.CraftCrystal, int.MaxValue },
            { LifeItem.CraftDemonite, int.MaxValue },
            { LifeItem.EnhanceStone, int.MaxValue },
        };

        /// <summary>
        /// 인벤토리 - 목숨 아이템 보유 현황
        /// </summary>
        public class LifeItemInventory
        {
            private readonly Dictionary<LifeItem, int> items = NewBag();

            static Dictionary<LifeItem, int> NewBag()
            {
                var bag = new Dictionary<LifeItem, int>();
                foreach (LifeItem it in System.Enum.GetValues(typeof(LifeItem)))
                    bag[it] = 0;
                return bag;
            }

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
            { "InvasionAttackDefeat", 0.08f },  // 침략 패배 추가: +0.08 G/h
            { "Fusion", 2.0f }                  // 합성: 2 G/h (§18-7)
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

        // ========== 대출 (§12 · §18-5) ==========
        // "골드 = 목숨"이므로 대출은 곧 목숨을 빌리는 것이다(§12). 아래는 순수 계산만 —
        // 상태(부채·기한)는 GameState가 들고, 실제 소비처는 TowerScreen "대출받고 입장"과
        // GameState.Earn의 수입 50% 자동 상환이다.
        //
        // 연체 제재의 **계산 상수**. 상태·화면 잠금은 GameState / EstateScreen / WorldMapScreen.
        // 정직한 미완(소비 시스템 없음): 영지 생산 압류 · 건물 -1레벨 · 비장착 아이템 30% 압류.
        // 거래서버·침략 본게임은 OUT — 문은 잠그되 그 안은 열지 않는다.

        /// <summary>시간당 이자 (§18-5: 0.5%/h = 일 12%).</summary>
        public const double LoanHourlyInterest = 0.005;

        /// <summary>연체 1회부터 이자 배율 (§12·§18-5: ×1.5).</summary>
        public const double LoanOverdueInterestFactor = 1.5;

        /// <summary>파산 1회당 한도 배율 (§18-5: -50%).</summary>
        public const double LoanBankruptcyLimitFactor = 0.5;

        /// <summary>파산 1회당 이자 추가 배율 (§18-5: +50%).</summary>
        public const double LoanBankruptcyInterestFactor = 1.5;

        /// <summary>파산 후 경매장 정지 일수 (§18-5: 7일).</summary>
        public const int LoanBankruptcyAuctionBanDays = 7;

        /// <summary>파산 후 재대출 유예 일수 (§18-5: 7일).</summary>
        public const int LoanReloanCooldownDays = 7;

        /// <summary>상환 기한 (§18-5: 72시간).</summary>
        public const long LoanTermHours = 72;

        /// <summary>수입 자동 상환 비율 (§18-5: 부채 보유 중 수입의 50% 자동 차감).</summary>
        public const double LoanAutoRepayRate = 0.50;

        /// <summary>절대 한도 기준 (§18-5: 20 G/h, T1 기준 20골드).</summary>
        public const long LoanBaseGoldPerTier = 20;

        /// <summary>
        /// 대출 한도(쿠퍼). §18-5: **순자산의 30%**와 **20 G/h**(티어 비례) 중 **작은 값**.
        /// "무자산 대출 방지"가 핵심 — 순자산 0이면 한도 0이라 못 빌린다.
        ///
        /// ⚠️ 순자산 근사(정직): 장비·영지 평가액 시스템이 아직 없어 순자산을 보유 골드로
        ///    근사한다. 그래도 ✅ 원칙("무자산이면 못 빌린다")은 그대로 성립한다(잔고 0 → 한도 0).
        ///    평가액 시스템이 생기면 netWorthCopper 인자만 실제 순자산으로 바꾸면 된다.
        /// </summary>
        public static long LoanLimitCopper(long netWorthCopper, int tier, int bankruptcyCount = 0)
        {
            if (tier < 0) tier = 0;
            if (bankruptcyCount < 0) bankruptcyCount = 0;
            long netCap = (long)(netWorthCopper * 0.30);
            long absCap = LoanBaseGoldPerTier * (tier + 1) * COPPER_PER_GOLD;
            long limit = System.Math.Min(netCap, absCap);
            for (int i = 0; i < bankruptcyCount; i++)
                limit = (long)(limit * LoanBankruptcyLimitFactor);
            return limit < 0 ? 0 : limit;
        }

        /// <summary>연체·파산이 이자에 곱해지는 배율. 연체 0·파산 0이면 1.</summary>
        public static double LoanInterestFactor(int overdueCount, int bankruptcyCount)
        {
            double f = 1.0;
            if (overdueCount >= 1) f *= LoanOverdueInterestFactor;
            if (bankruptcyCount < 0) bankruptcyCount = 0;
            for (int i = 0; i < bankruptcyCount; i++)
                f *= LoanBankruptcyInterestFactor;
            return f;
        }

        /// <summary>
        /// 이자 가산 — 잔액에 시간당 0.5%×배율 복리(§18-5). 정수 반올림.
        /// 결정론적이라(초월함수 1회) 자가검사가 배속·대기 없이 값을 검증할 수 있다.
        /// interestFactor 기본 1 — 기존 SelfCheck ⑩(만기 정각 = 연체 전)이 이 경로다.
        /// </summary>
        public static long AccrueLoan(long balanceCopper, long hours, double interestFactor = 1.0)
        {
            if (balanceCopper <= 0) return 0;
            if (hours <= 0) return balanceCopper;
            if (interestFactor <= 0) interestFactor = 1.0;
            double grown = balanceCopper * System.Math.Pow(1.0 + LoanHourlyInterest * interestFactor, hours);
            return (long)System.Math.Round(grown);
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
