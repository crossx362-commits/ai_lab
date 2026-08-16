using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 플레이어의 지속 상태 — 지갑·소지품·탑 진행도.
    ///
    /// 왜 필요한가:
    ///   Economy에 `Wallet`·`LifeItemInventory` **클래스**는 있었지만 그것을 **가진 주체**가 없어서,
    ///   전투 보상이 계산만 되고 어디에도 쌓이지 않았다(계산 ≠ 반영).
    ///   경제가 순환하려면 "번 돈이 남아 있고, 그 돈으로 다음 판에 들어간다"가 성립해야 한다(§2 코어 루프).
    ///
    /// 저장:
    ///   프로토타입이라 PlayerPrefs로 충분하다. 본 게임에서는 서버가 필요하다 —
    ///   §4의 회복 시간과 §12의 재화는 클라이언트가 들고 있으면 시간·수치 조작에 그대로 노출된다.
    /// </summary>
    public static class GameState
    {
        const string K_COPPER = "ats.copper";
        const string K_FLOOR = "ats.tower_floor";
        const string K_ITEM = "ats.item.";
        // 대출 상태(§12·§18-5) — 부채 잔액·마지막 이자 가산 시각·상환 기한(전부 유닉스초).
        const string K_DEBT = "ats.loan.debt";
        const string K_LOAN_AT = "ats.loan.accrued_at";
        const string K_LOAN_DUE = "ats.loan.due_at";

        static Economy.Wallet _wallet;
        static Economy.LifeItemInventory _bag;
        static bool _loaded;
        static long _debt;          // 부채 잔액(쿠퍼) — 이자 포함
        static long _loanAccruedAt; // 마지막으로 이자를 가산한 시각(유닉스초)
        static long _loanDueAt;     // 상환 기한(유닉스초). 부채 없으면 0

        /// <summary>지갑. §12의 쿠퍼–실버–골드 3단계는 Economy가 환산한다.</summary>
        public static Economy.Wallet Wallet { get { Load(); return _wallet; } }

        /// <summary>목숨 아이템 소지품. 상한(§18-4)은 Economy가 강제한다.</summary>
        public static Economy.LifeItemInventory Bag { get { Load(); return _bag; } }

        /// <summary>도달한 탑 최고 층(§8). 10층 돌파마다 필드·던전 티어가 오른다(§10-6).</summary>
        public static int TowerFloor
        {
            get { Load(); return _floor; }
            private set { _floor = Mathf.Max(1, value); Save(); }
        }
        static int _floor = 1;

        /// <summary>
        /// 현재 콘텐츠 티어. §10-6이 "탑 10층 돌파마다 한 단계"라고 정했다.
        /// 수익 곡선(§18-1)과 진입 비용(§18-2)이 이 값을 쓴다.
        /// </summary>
        public static int Tier => Mathf.Clamp((TowerFloor - 1) / 10, 0, 9);

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _wallet = new Economy.Wallet();
            _bag = new Economy.LifeItemInventory();

            long copper = 0;
            long.TryParse(PlayerPrefs.GetString(K_COPPER, "0"), out copper);
            if (copper > 0) _wallet.TryAdd(copper);

            _floor = Mathf.Max(1, PlayerPrefs.GetInt(K_FLOOR, 1));

            foreach (Economy.LifeItem it in System.Enum.GetValues(typeof(Economy.LifeItem)))
            {
                int n = PlayerPrefs.GetInt(K_ITEM + it, 0);
                if (n > 0) _bag.TryAdd(it, n);
            }

            long.TryParse(PlayerPrefs.GetString(K_DEBT, "0"), out _debt);
            long.TryParse(PlayerPrefs.GetString(K_LOAN_AT, "0"), out _loanAccruedAt);
            long.TryParse(PlayerPrefs.GetString(K_LOAN_DUE, "0"), out _loanDueAt);
            if (_debt < 0) _debt = 0;
        }

        static void Save()
        {
            if (!_loaded) return;
            PlayerPrefs.SetString(K_COPPER, _wallet.Copper.ToString());
            PlayerPrefs.SetInt(K_FLOOR, _floor);
            foreach (Economy.LifeItem it in System.Enum.GetValues(typeof(Economy.LifeItem)))
                PlayerPrefs.SetInt(K_ITEM + it, _bag.GetCount(it));
            PlayerPrefs.SetString(K_DEBT, _debt.ToString());
            PlayerPrefs.SetString(K_LOAN_AT, _loanAccruedAt.ToString());
            PlayerPrefs.SetString(K_LOAN_DUE, _loanDueAt.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 보상 지급. 실제로 지갑에 들어가고 저장된다.
        /// §18-5: **부채 보유 중에는 수입의 50%가 자동 상환**된다 — 이것이 대출 상태의
        /// 상시 소비처다(전투 보상이 이 경로로 들어온다). 빚이 없으면 종전과 동일하게 전액 입금.
        /// </summary>
        public static void Earn(long copper)
        {
            Load();
            if (copper <= 0) return;
            if (_debt > 0)
            {
                long toDebt = System.Math.Min((long)(copper * Economy.LoanAutoRepayRate), _debt);
                if (toDebt > 0)
                {
                    _debt -= toDebt;
                    copper -= toDebt;
                    if (_debt == 0) _loanDueAt = 0;
                }
            }
            _wallet.TryAdd(copper);
            Save();
        }

        // ========== 대출 (§12 · §18-5) ==========

        /// <summary>현재 부채 잔액(쿠퍼) — 이자 포함.</summary>
        public static long Debt { get { Load(); return _debt; } }

        /// <summary>
        /// 대출 총 한도(쿠퍼). 순자산 30%와 20G/h·티어 중 작은 값(§18-5).
        /// 순자산 = 지갑 − 부채. **빌린 돈은 순자산을 늘리지 않는다**(안 그러면 대출→한도↑→
        /// 대출의 무한 피드백 루프가 생긴다 — 자가검사 ⑩이 이 경계를 지킨다).
        /// </summary>
        public static long LoanLimit { get { Load(); return Economy.LoanLimitCopper(_wallet.Copper - _debt, Tier); } }

        /// <summary>지금 더 빌릴 수 있는 금액(쿠퍼) = 한도 − 현재 부채(음수면 0).</summary>
        public static long LoanBorrowable { get { Load(); long left = LoanLimit - _debt; return left < 0 ? 0 : left; } }

        static long NowUnix() => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>이자 가산 — 마지막 가산 이후 경과한 정수 시간만큼(§18-5 0.5%/h). 부채 없으면 무동작.</summary>
        public static void AccrueLoan(long nowUnix)
        {
            Load();
            if (_debt <= 0) return;
            long hours = (nowUnix - _loanAccruedAt) / 3600;
            if (hours <= 0) return;
            _debt = Economy.AccrueLoan(_debt, hours);
            _loanAccruedAt += hours * 3600;
            Save();
        }
        public static void AccrueLoan() => AccrueLoan(NowUnix());

        /// <summary>
        /// 대출 — 한도 내에서만 성공. 성공 시 지갑에 들어가고 부채로 잡힌다(§12).
        /// 한도 초과·무자산이면 **아무것도 하지 않고 false**(부분 대출 없음).
        /// </summary>
        public static bool Borrow(long copper, long nowUnix)
        {
            Load();
            if (copper <= 0) return false;
            AccrueLoan(nowUnix);
            if (_debt + copper > LoanLimit) return false;
            if (_debt == 0) { _loanAccruedAt = nowUnix; _loanDueAt = nowUnix + Economy.LoanTermHours * 3600; }
            _debt += copper;
            _wallet.TryAdd(copper);
            Save();
            return true;
        }
        public static bool Borrow(long copper) => Borrow(copper, NowUnix());

        /// <summary>수동 상환 — 지갑에서 부채를 갚는다. 실제로 갚은 금액(쿠퍼)을 반환.</summary>
        public static long Repay(long copper, long nowUnix)
        {
            Load();
            AccrueLoan(nowUnix);
            if (_debt <= 0 || copper <= 0) return 0;
            long pay = System.Math.Min(copper, System.Math.Min(_debt, _wallet.Copper));
            if (pay <= 0) return 0;
            _wallet.TrySubtract(pay);
            _debt -= pay;
            if (_debt == 0) _loanDueAt = 0;
            Save();
            return pay;
        }
        public static long Repay(long copper) => Repay(copper, NowUnix());

        /// <summary>
        /// 진입 비용 차감. 모자라면 **차감하지 않고 false**를 돌려준다 —
        /// 부분 차감은 "돈은 냈는데 못 들어갔다"가 되어 최악이다.
        /// </summary>
        public static bool Pay(long copper)
        {
            Load();
            if (copper <= 0) return true;
            if (!_wallet.TrySubtract(copper)) return false;
            Save();
            return true;
        }

        /// <summary>드랍 획득. 상한(§18-4)에 걸리면 false — **소실이 아니라 획득 거부**다.</summary>
        public static bool Gain(Economy.LifeItem item, int amount = 1)
        {
            Load();
            bool ok = _bag.TryAdd(item, amount);
            if (ok) Save();
            return ok;
        }

        /// <summary>아이템 사용(부활초 등). 없으면 false.</summary>
        public static bool Consume(Economy.LifeItem item, int amount = 1)
        {
            Load();
            bool ok = _bag.TryConsume(item, amount);
            if (ok) Save();
            return ok;
        }

        /// <summary>전직 원자 커밋 전용: 메모리에서만 차감하고 PlayerPrefs.Save는 호출하지 않는다.</summary>
        internal static bool TryConsumeDeferred(Economy.LifeItem item, int amount)
        {
            Load();
            return _bag.TryConsume(item, amount);
        }

        /// <summary>전직 로스터와 같은 PlayerPrefs.Save에 묶기 위해 가방 키만 스테이징한다.</summary>
        internal static void StageBagForAtomicSave()
        {
            Load();
            if (_failNextAtomicStageForTest)
            {
                _failNextAtomicStageForTest = false;
                throw new System.InvalidOperationException("전직 원자 저장 실패 주입");
            }
            foreach (Economy.LifeItem it in System.Enum.GetValues(typeof(Economy.LifeItem)))
                PlayerPrefs.SetInt(K_ITEM + it, _bag.GetCount(it));
        }
        static bool _failNextAtomicStageForTest;
        public static void FailNextAtomicStageForTest() => _failNextAtomicStageForTest = true;

        /// <summary>탑 층 돌파. 최고 기록만 올라간다(재도전으로 내려가지 않는다).</summary>
        public static void ClearFloor(int floor)
        {
            Load();
            if (floor >= _floor) TowerFloor = floor + 1;
        }

        /// <summary>지금 보유량을 화면에 쓸 문자열로. 예: "12골드 30실버".</summary>
        public static string WalletText => Economy.FormatCurrency(Wallet.Copper);

        /// <summary>소지품 요약. 0개인 것은 빼고 보여준다.</summary>
        public static string BagText()
        {
            Load();
            var parts = new List<string>();
            foreach (Economy.LifeItem it in System.Enum.GetValues(typeof(Economy.LifeItem)))
            {
                int n = _bag.GetCount(it);
                if (n > 0) parts.Add($"{Label(it)} {n}/{Economy.ItemCapacity[it]}");
            }
            return parts.Count == 0 ? "소지품 없음" : string.Join(" · ", parts);
        }

        /// <summary>아이템 표시 이름(§4·§18-4의 우리말 명칭).</summary>
        public static string Label(Economy.LifeItem it) => it switch
        {
            Economy.LifeItem.RevivalTea => "부활초",
            Economy.LifeItem.ScrollOfReturn => "귀환의 두루마리",
            Economy.LifeItem.RebornStone => "환생석",
            Economy.LifeItem.AdvancementMaterial => "전직 재료",
            _ => "특수 직업 증표",
        };

        /// <summary>디버그·초기화용. 프로토타입 테스트에서 상태를 되돌릴 때 쓴다.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(K_COPPER);
            PlayerPrefs.DeleteKey(K_FLOOR);
            foreach (Economy.LifeItem it in System.Enum.GetValues(typeof(Economy.LifeItem)))
                PlayerPrefs.DeleteKey(K_ITEM + it);
            PlayerPrefs.DeleteKey(K_DEBT);
            PlayerPrefs.DeleteKey(K_LOAN_AT);
            PlayerPrefs.DeleteKey(K_LOAN_DUE);
            PlayerPrefs.Save();
            _debt = _loanAccruedAt = _loanDueAt = 0;
            _loaded = false;
        }

        /// <summary>테스트 전용 — 메모리 캐시를 버려 다음 접근이 PlayerPrefs에서 다시 읽게 한다.
        /// (LifeSystem.ForgetInMemoryForTest와 같은 목적: 저장→재기동 유지를 자가검사가 확인)</summary>
        public static void ForgetInMemoryForTest() => _loaded = false;

        /// <summary>테스트 전용 — 탑 층을 임의 값으로 되돌린다. `TowerFloor`는 단조 증가(ClearFloor로만
        /// 오른다)라 자가검사가 원래 층을 복원할 수단이 없으므로, 검사 전후로 층을 세팅/복원하는 데 쓴다.</summary>
        public static void SetTowerFloorForTest(int floor) { Load(); _floor = Mathf.Max(1, floor); Save(); }
    }
}
