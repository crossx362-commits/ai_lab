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
        const string K_TIER = "ats.world_tier";
        const string K_ITEM = "ats.item.";
        // 대출 상태(§12·§18-5) — 부채 잔액·마지막 이자 가산 시각·상환 기한(전부 유닉스초).
        const string K_DEBT = "ats.loan.debt";
        const string K_LOAN_AT = "ats.loan.accrued_at";
        const string K_LOAN_DUE = "ats.loan.due_at";
        const string K_OVERDUE = "ats.loan.overdue";
        const string K_BANKRUPT = "ats.loan.bankrupt";
        const string K_BANKRUPT_LOAN = "ats.loan.bankrupt_this";
        const string K_AUCTION_BAN = "ats.loan.auction_ban_until";
        const string K_RELOAN = "ats.loan.reloan_until";

        static Economy.Wallet _wallet;
        static Economy.LifeItemInventory _bag;
        static bool _loaded;
        static long _debt;          // 부채 잔액(쿠퍼) — 이자 포함
        static long _loanAccruedAt; // 마지막으로 이자를 가산한 시각(유닉스초)
        static long _loanDueAt;     // 상환 기한(유닉스초). 부채 없으면 0
        static int _overdueCount;   // 이번 대출에서 넘긴 만기 횟수(갚으면 0)
        static int _bankruptcyCount;
        static bool _bankruptThisLoan;
        static long _auctionBanUntil;
        static long _reloanUntil;
        static bool _qaLoanSeeded;

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
        // -1 = 한 번도 고르지 않음 → 해금 최고를 따른다. 옛 저장에 키가 없어도 같다.
        static int _selectedTier = -1;

        /// <summary>
        /// 해금된 최고 티어(0=T1). 탑 최고 기록에서만 온다. 선택을 내려도 이 값은 안 내려간다(§6·§10-6).
        /// </summary>
        public static int UnlockedTier
        {
            get { Load(); return Mathf.Clamp((_floor - 1) / 10, 0, 9); }
        }

        /// <summary>
        /// 지금 세계의 콘텐츠 티어. 필드·던전·하위 레이드 난이도와 일반 보상·비용이 이걸 본다(§6).
        /// 탑 도전 비용은 <see cref="UnlockedTier"/>를 쓴다 — 낮춘 세계로 고층 입장이 싸지면 안 된다.
        /// </summary>
        public static int Tier
        {
            get
            {
                Load();
                int unlocked = UnlockedTier;
                if (_selectedTier < 0) return unlocked;
                return Mathf.Clamp(_selectedTier, 0, unlocked);
            }
        }

        /// <summary>
        /// 해금된 티어 중 하나를 고른다(§6). 해금보다 높거나 음수면 아무것도 안 바꾸고 false.
        /// </summary>
        public static bool TrySelectTier(int tier)
        {
            Load();
            if (tier < 0 || tier > UnlockedTier) return false;
            _selectedTier = tier;
            Save();
            return true;
        }

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
            _selectedTier = PlayerPrefs.GetInt(K_TIER, -1);
            ApplyQaWorldTierSeed();

            foreach (Economy.LifeItem it in System.Enum.GetValues(typeof(Economy.LifeItem)))
            {
                int n = PlayerPrefs.GetInt(K_ITEM + it, 0);
                if (n > 0) _bag.TryAdd(it, n);
            }

            long.TryParse(PlayerPrefs.GetString(K_DEBT, "0"), out _debt);
            long.TryParse(PlayerPrefs.GetString(K_LOAN_AT, "0"), out _loanAccruedAt);
            long.TryParse(PlayerPrefs.GetString(K_LOAN_DUE, "0"), out _loanDueAt);
            int.TryParse(PlayerPrefs.GetString(K_OVERDUE, "0"), out _overdueCount);
            int.TryParse(PlayerPrefs.GetString(K_BANKRUPT, "0"), out _bankruptcyCount);
            _bankruptThisLoan = PlayerPrefs.GetInt(K_BANKRUPT_LOAN, 0) == 1;
            long.TryParse(PlayerPrefs.GetString(K_AUCTION_BAN, "0"), out _auctionBanUntil);
            long.TryParse(PlayerPrefs.GetString(K_RELOAN, "0"), out _reloanUntil);
            if (_debt < 0) _debt = 0;
            if (_overdueCount < 0) _overdueCount = 0;
            if (_bankruptcyCount < 0) _bankruptcyCount = 0;
            ApplyQaLoanSeed();
        }

        static void Save()
        {
            if (!_loaded) return;
            PlayerPrefs.SetString(K_COPPER, _wallet.Copper.ToString());
            PlayerPrefs.SetInt(K_FLOOR, _floor);
            PlayerPrefs.SetInt(K_TIER, _selectedTier);
            foreach (Economy.LifeItem it in System.Enum.GetValues(typeof(Economy.LifeItem)))
                PlayerPrefs.SetInt(K_ITEM + it, _bag.GetCount(it));
            PlayerPrefs.SetString(K_DEBT, _debt.ToString());
            PlayerPrefs.SetString(K_LOAN_AT, _loanAccruedAt.ToString());
            PlayerPrefs.SetString(K_LOAN_DUE, _loanDueAt.ToString());
            PlayerPrefs.SetString(K_OVERDUE, _overdueCount.ToString());
            PlayerPrefs.SetString(K_BANKRUPT, _bankruptcyCount.ToString());
            PlayerPrefs.SetInt(K_BANKRUPT_LOAN, _bankruptThisLoan ? 1 : 0);
            PlayerPrefs.SetString(K_AUCTION_BAN, _auctionBanUntil.ToString());
            PlayerPrefs.SetString(K_RELOAN, _reloanUntil.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 보상 지급. 실제로 지갑에 들어가고 저장된다.
        /// §18-14: 시간당 수익 소프트캡을 먼저 읽는다 — 사냥·약탈·광산이 전부 여기를 탄다.
        /// §18-5: **부채 보유 중에는 수입의 50%가 자동 상환**된다 — 이것이 대출 상태의
        /// 상시 소비처다(전투 보상이 이 경로로 들어온다). 빚이 없으면 종전과 동일하게 전액 입금.
        /// 돌려주는 값은 캡 적용 뒤 금액(빚으로 간 분도 포함).
        /// </summary>
        public static long Earn(long copper)
        {
            Load();
            if (copper <= 0) return 0;
            copper = SoftCap.Apply(copper);
            return Deposit(copper);
        }

        /// <summary>
        /// 소프트캡을 타지 않는 입금. 환급·QA 시드·검사 준비금.
        /// 대출 자동상환은 Earn과 같다 — 캡만 건너뛴다.
        /// </summary>
        public static long Grant(long copper)
        {
            Load();
            if (copper <= 0) return 0;
            return Deposit(copper);
        }

        static long Deposit(long copper)
        {
            long credited = copper;
            if (_debt > 0)
            {
                long toDebt = System.Math.Min((long)(copper * Economy.LoanAutoRepayRate), _debt);
                if (toDebt > 0)
                {
                    _debt -= toDebt;
                    copper -= toDebt;
                    if (_debt == 0)
                    {
                        _loanDueAt = 0;
                        _overdueCount = 0;
                        _bankruptThisLoan = false;
                    }
                }
            }
            _wallet.TryAdd(copper);
            Save();
            return credited;
        }

        // ========== 대출 (§12 · §18-5) ==========

        /// <summary>현재 부채 잔액(쿠퍼) — 이자 포함.</summary>
        public static long Debt { get { Load(); return _debt; } }

        /// <summary>
        /// 대출 총 한도(쿠퍼). 순자산 30%와 20G/h·티어 중 작은 값(§18-5).
        /// 순자산 = 지갑+장비+영지 − 부채. **빌린 돈은 순자산을 늘리지 않는다**(안 그러면 대출→한도↑→
        /// 대출의 무한 피드백 루프가 생긴다 — 자가검사 ⑩이 이 경계를 지킨다).
        /// </summary>
        public static long LoanLimit { get { Load(); return Economy.LoanLimitCopper(NetWorth.Assets() - _debt, Tier, _bankruptcyCount); } }

        /// <summary>이번 대출의 연체 횟수. 전액 상환하면 0. 파산 누적과 별개다.</summary>
        public static int OverdueCount { get { Load(); return _overdueCount; } }

        /// <summary>파산 누적 횟수(§18-5 신용도). 상환해도 줄지 않는다.</summary>
        public static int BankruptcyCount { get { Load(); return _bankruptcyCount; } }

        /// <summary>파산 경매 정지 종료 시각(유닉스초). 없으면 0.</summary>
        public static long AuctionBanUntil { get { Load(); return _auctionBanUntil; } }

        /// <summary>지금 더 빌릴 수 있는 금액(쿠퍼) = 한도 − 현재 부채(음수면 0).</summary>
        public static long LoanBorrowable { get { Load(); long left = LoanLimit - _debt; return left < 0 ? 0 : left; } }

        static long NowUnix() => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        /// <summary>이자 가산 — 마지막 가산 이후 경과한 정수 시간만큼(§18-5 0.5%/h). 부채 없으면 무동작.</summary>
        public static void AccrueLoan(long nowUnix)
        {
            Load();
            if (_debt <= 0) return;
            RefreshSanctions(nowUnix);
            long hours = (nowUnix - _loanAccruedAt) / 3600;
            if (hours <= 0) return;
            _debt = Economy.AccrueLoan(_debt, hours, Economy.LoanInterestFactor(_overdueCount, _bankruptcyCount));
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
            if (_debt == 0 && _reloanUntil > nowUnix) return false;
            if (_debt + copper > LoanLimit) return false;
            if (_debt == 0) { _loanAccruedAt = nowUnix; _loanDueAt = nowUnix + Economy.LoanTermHours * 3600; _overdueCount = 0; _bankruptThisLoan = false; }
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
            if (_debt == 0)
            {
                _loanDueAt = 0;
                _overdueCount = 0;
                _bankruptThisLoan = false;
            }
            Save();
            return pay;
        }

        /// <summary>
        /// 수입에서 빚을 갚는다. 지갑은 안 건드린다.
        /// 연체 2회 영지 생산 압류(§18-5)가 여기를 읽는다 — Earn의 50%와 다르다.
        /// </summary>
        public static long RepayFromIncome(long copper)
        {
            Load();
            if (_debt <= 0 || copper <= 0) return 0;
            long pay = System.Math.Min(copper, _debt);
            _debt -= pay;
            if (_debt == 0)
            {
                _loanDueAt = 0;
                _overdueCount = 0;
                _bankruptThisLoan = false;
            }
            Save();
            return pay;
        }

        /// <summary>
        /// 만기가 지난 만큼 연체 횟수를 올린다. 정각은 아직 연체가 아니다(SelfCheck ⑩ 만기 이자).
        /// 3회에서 파산 1회 — 건물 −1·비장착 30%는 BankruptcySeize가 읽는다.
        /// </summary>
        public static void RefreshSanctions(long nowUnix)
        {
            Load();
            if (_debt <= 0)
            {
                if (_overdueCount != 0)
                {
                    _overdueCount = 0;
                    _bankruptThisLoan = false;
                    Save();
                }
                return;
            }
            if (_loanDueAt <= 0) return;
            bool changed = false;
            while (nowUnix > _loanDueAt && _debt > 0)
            {
                _overdueCount++;
                _loanDueAt += Economy.LoanTermHours * 3600;
                changed = true;
                if (_overdueCount >= 3 && !_bankruptThisLoan)
                    ApplyBankruptcy(nowUnix);
            }
            if (changed) Save();
        }
        public static void RefreshSanctions() => RefreshSanctions(NowUnix());

        static void ApplyBankruptcy(long nowUnix)
        {
            _bankruptThisLoan = true;
            _bankruptcyCount++;
            long ban = (long)Economy.LoanBankruptcyAuctionBanDays * 86400L;
            long cool = (long)Economy.LoanReloanCooldownDays * 86400L;
            _auctionBanUntil = nowUnix + ban;
            _reloanUntil = nowUnix + cool;
            BankruptcySeize.Apply();
        }

        /// <summary>§12·§18-5: 부채 보유·연체 1회·파산 7일 정지 중이면 경매장 문을 잠근다.</summary>
        public static bool CanUseAuction(long nowUnix)
        {
            Load();
            ApplyQaLoanSeed();
            RefreshSanctions(nowUnix);
            if (_auctionBanUntil > nowUnix) return false;
            if (_overdueCount >= 1) return false;
            if (_debt > 0) return false;
            return true;
        }
        public static bool CanUseAuction() => CanUseAuction(NowUnix());

        public static string AuctionBlockReason(long nowUnix)
        {
            Load();
            RefreshSanctions(nowUnix);
            if (_auctionBanUntil > nowUnix)
            {
                long left = _auctionBanUntil - nowUnix;
                long days = (left + 86399) / 86400;
                if (days < 1) days = 1;
                return $"파산 — 경매장 {days}일 정지(§18-5)";
            }
            if (_overdueCount >= 1)
                return $"연체 {_overdueCount}회 — 경매장 이용 정지(§12)";
            if (_debt > 0)
                return "부채 보유 중 — 경매 등록·구매 금지(§18-5)";
            return "";
        }
        public static string AuctionBlockReason() => AuctionBlockReason(NowUnix());

        /// <summary>§18-5: 연체 2회부터 침략 불가. 침략 본게임은 열지 않는다.</summary>
        public static bool CanInvade(long nowUnix)
        {
            Load();
            ApplyQaLoanSeed();
            RefreshSanctions(nowUnix);
            return _overdueCount < 2;
        }
        public static bool CanInvade() => CanInvade(NowUnix());

        public static string InvasionBlockReason(long nowUnix)
        {
            Load();
            RefreshSanctions(nowUnix);
            if (_overdueCount >= 2)
                return $"연체 {_overdueCount}회 — 침략 불가(§18-5)";
            return "";
        }
        public static string InvasionBlockReason() => InvasionBlockReason(NowUnix());

        /// <summary>
        /// 시각 QA. QA_LOAN_OVERDUE=1|2|3이면 연체와 30층을 심는다.
        /// DebugAutoPilot.Start가 Earn(500000)을 먼저 호출하면 자동상환이 빚·연체를 지운다 —
        /// 잠금 API가 읽을 때마다 다시 심어 그 경로를 탄다.
        /// </summary>
        static void ApplyQaLoanSeed()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_LOAN_OVERDUE");
            if (string.IsNullOrEmpty(raw)) return;
            int n;
            if (!int.TryParse(raw, out n) || n <= 0) return;
            if (n > 3) n = 3;
            bool ok = _overdueCount == n && _debt > 0 && _floor >= 30
                      && (n < 3 || _auctionBanUntil > NowUnix());
            if (_qaLoanSeeded && ok) return;
            _qaLoanSeeded = true;
            if (_wallet.Copper < 100000) _wallet.TryAdd(100000 - _wallet.Copper);
            if (_debt <= 0) _debt = 10000;
            _overdueCount = n;
            _loanDueAt = NowUnix() + Economy.LoanTermHours * 3600;
            _loanAccruedAt = NowUnix();
            if (_floor < 30) _floor = 30;
            if (n >= 3)
            {
                _bankruptThisLoan = true;
                if (_bankruptcyCount < 1) _bankruptcyCount = 1;
                if (_auctionBanUntil <= NowUnix())
                    _auctionBanUntil = NowUnix() + (long)Economy.LoanBankruptcyAuctionBanDays * 86400L;
            }
            Save();
        }

        /// <summary>시각 QA. 연체 2회·빚 1만·지갑 0. 광산 시드가 1시간을 넣는다.</summary>
        public static void SeedMineSeizeLoan()
        {
            Load();
            if (_wallet.Copper > 0) _wallet.TrySubtract(_wallet.Copper);
            _debt = 10000;
            _overdueCount = 2;
            _loanDueAt = NowUnix() + Economy.LoanTermHours * 3600;
            _loanAccruedAt = NowUnix();
            _bankruptThisLoan = false;
            if (_floor < 30) _floor = 30;
            _selectedTier = 0;
            Save();
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

        /// <summary>드랍 획득. 상한(§18-4)에 걸리면 false — **소실이 아니라 획득 거부**다.
        /// 새 종류는 가방 60칸(§11)도 본다.</summary>
        public static bool Gain(Economy.LifeItem item, int amount = 1)
        {
            Load();
            if (!BagSlots.CanGain(item, amount)) return false;
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

        /// <summary>탑 층 돌파. 최고 기록만 올라간다(재도전으로 내려가지 않는다).
        /// 새 티어가 열리면 그 최고가 기본 선택이다(§6 "새로 해금한 최고 티어가 기본값").</summary>
        public static void ClearFloor(int floor)
        {
            Load();
            if (floor < _floor) return;
            int before = UnlockedTier;
            // §8: 100층이 본편 결말. 101층·무한층은 열지 않고 재도전만 남긴다.
            TowerFloor = floor >= TowerEnding.FinaleFloor
                ? TowerEnding.FinaleFloor
                : floor + 1;
            AuctionState.NoteUnlock(TowerFloor);
            if (UnlockedTier > before)
            {
                _selectedTier = UnlockedTier;
                Save();
            }
        }

        /// <summary>시각 QA. QA_WORLD_TIER=1이면 해금 T3(21층)·선택 T1을 심는다.</summary>
        public static void SeedWorldTierQaIfRequested()
        {
            Load();
            ApplyQaWorldTierSeed();
        }

        static void ApplyQaWorldTierSeed()
        {
            string raw = System.Environment.GetEnvironmentVariable("QA_WORLD_TIER");
            if (string.IsNullOrEmpty(raw) || raw == "0") return;
            if (_floor < 21) _floor = 21;
            _selectedTier = 0;
            Save();
        }

        public const string EnvShowWallet = "QA_WALLET_TEXT";
        public const string EnvNoWallet = "QA_NO_WALLET_TEXT";
        /// <summary>FormatCurrency면 「123골드 45실버 67쿠퍼」가 되는 혼합 단위. ShortCopper는 「123골드」.</summary>
        public const long MixCopper = 1_234_567;

        static bool _qaWalletSeeded;

        public static bool WalletTextBlocked
        {
            get
            {
                string raw = System.Environment.GetEnvironmentVariable(EnvNoWallet);
                return raw == "1" || string.Equals(raw, "true", System.StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool WalletTextShowQa
        {
            get
            {
                string raw = System.Environment.GetEnvironmentVariable(EnvShowWallet);
                return raw == "1" || string.Equals(raw, "true", System.StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>옛 줄은 FormatCurrency 풀표기(553골드 30실버 8쿠퍼)라 필드 자막이 잘렸다.</summary>
        public static string OldWalletText => Economy.FormatCurrency(Wallet.Copper);

        /// <summary>
        /// 지금 보유량을 화면에 쓸 짧은 문자열. 예: "12골드".
        /// 필드·던전·탑 자막의 「보유 {WalletText}」와 레이드 미출현 시 필드 카드 제목이 읽는다.
        /// HuntGoldHourLine·TokenPrice와 같이 ShortCopper만. QA_NO면 옛 풀표기.
        /// </summary>
        public static string WalletText => WalletTextBlocked
            ? OldWalletText
            : EstateStatusHud.ShortCopper(Wallet.Copper);

        /// <summary>시각 QA. 혼합 단위 1234567쿠퍼를 심어 자막이 「보유 123골드」가 보이게 한다.</summary>
        public static void SeedWalletTextQaIfRequested()
        {
            if (!WalletTextShowQa) return;
            if (WalletTextBlocked) return;
            if (_qaWalletSeeded) return;
            _qaWalletSeeded = true;
            Load();
            if (Wallet.Copper < MixCopper)
                Grant(MixCopper - Wallet.Copper);
        }

        /// <summary>소지품 요약. 0개인 것은 빼고 보여준다.</summary>
        public static string BagText()
        {
            Load();
            var parts = new List<string>();
            foreach (Economy.LifeItem it in System.Enum.GetValues(typeof(Economy.LifeItem)))
            {
                string part = BagTextFmt.Format(it, _bag.GetCount(it));
                if (part.Length > 0) parts.Add(part);
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
            Economy.LifeItem.CraftHide => "사냥 가죽",
            Economy.LifeItem.CraftFang => "송곳니",
            Economy.LifeItem.CraftBone => "유골",
            Economy.LifeItem.CraftPart => "부품",
            Economy.LifeItem.CraftCrystal => "원소결정",
            Economy.LifeItem.CraftDemonite => "마정석",
            Economy.LifeItem.EnhanceStone => "강화석",
            _ => "특수 직업 증표",
        };

        /// <summary>디버그·초기화용. 프로토타입 테스트에서 상태를 되돌릴 때 쓴다.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(K_COPPER);
            PlayerPrefs.DeleteKey(K_FLOOR);
            PlayerPrefs.DeleteKey(K_TIER);
            foreach (Economy.LifeItem it in System.Enum.GetValues(typeof(Economy.LifeItem)))
                PlayerPrefs.DeleteKey(K_ITEM + it);
            PlayerPrefs.DeleteKey(K_DEBT);
            PlayerPrefs.DeleteKey(K_LOAN_AT);
            PlayerPrefs.DeleteKey(K_LOAN_DUE);
            PlayerPrefs.DeleteKey(K_OVERDUE);
            PlayerPrefs.DeleteKey(K_BANKRUPT);
            PlayerPrefs.DeleteKey(K_BANKRUPT_LOAN);
            PlayerPrefs.DeleteKey(K_AUCTION_BAN);
            PlayerPrefs.DeleteKey(K_RELOAN);
            PlayerPrefs.Save();
            _debt = _loanAccruedAt = _loanDueAt = 0;
            _overdueCount = _bankruptcyCount = 0;
            _bankruptThisLoan = false;
            _auctionBanUntil = _reloanUntil = 0;
            _qaLoanSeeded = false;
            _qaWalletSeeded = false;
            _selectedTier = -1;
            _loaded = false;
            Equipment.ResetAll();
            AuctionState.ResetForTest();
            InvasionState.ResetForTest();
            InvasionApproach.ResetForTest();
            LowHpReturn.ResetForTest();
            TowerEnding.ResetForTest();
            SoloRaidClear.ResetForTest();
            FloorRecruit.ResetForTest();
            StarterSecond.ResetForTest();
            EstateBuild.ResetForTest();
            EstateMine.ResetForTest();
            EstateDefense.ResetForTest();
            EstateGrid.ResetForTest();
            EstateStore.ResetForTest();
            SoftCap.ResetForTest();
            Honor.ResetForTest();
            DeathTraining.ResetForTest();
            RaidScale.ResetForTest();
            RaidBossPool.ResetForTest();
            RaidReroll.ResetForTest();
            RaidCost.ResetForTest();
            BankruptcySeize.ResetForTest();
            Rebirth.ResetForTest();
            RebirthSkill.ResetForTest();
            Memorial.ResetForTest();
            HuntSchedule.ResetForTest();
            NetWorth.ResetForTest();
            BagSlots.ResetForTest();
            GearDrop.ResetForTest();
            EliteDrop.ResetForTest();
            EliteKinds.ResetForTest();
        }

        /// <summary>테스트 전용 — 메모리 캐시를 버려 다음 접근이 PlayerPrefs에서 다시 읽게 한다.
        /// (LifeSystem.ForgetInMemoryForTest와 같은 목적: 저장→재기동 유지를 자가검사가 확인)</summary>
        public static void ForgetInMemoryForTest()
        {
            _loaded = false;
            Equipment.ForgetInMemoryForTest();
            AuctionState.ForgetInMemoryForTest();
            InvasionState.ForgetInMemoryForTest();
            TowerEnding.ForgetInMemoryForTest();
            SoloRaidClear.ForgetInMemoryForTest();
            FloorRecruit.ForgetInMemoryForTest();
            StarterSecond.ForgetInMemoryForTest();
            EstateMine.ForgetInMemoryForTest();
            EstateDefense.ForgetInMemoryForTest();
            EstateGrid.ForgetInMemoryForTest();
            SoftCap.ForgetInMemoryForTest();
            Honor.ForgetInMemoryForTest();
            DeathTraining.ForgetInMemoryForTest();
            EstateBuild.ForgetInMemoryForTest();
            BankruptcySeize.ForgetInMemoryForTest();
            Memorial.ForgetInMemoryForTest();
            HuntSchedule.ForgetInMemoryForTest();
            NetWorth.ResetForTest();
        }

        /// <summary>테스트 전용 — 탑 층을 임의 값으로 되돌린다. `TowerFloor`는 단조 증가(ClearFloor로만
        /// 오른다)라 자가검사가 원래 층을 복원할 수단이 없으므로, 검사 전후로 층을 세팅/복원하는 데 쓴다.</summary>
        public static void SetTowerFloorForTest(int floor) { Load(); _floor = Mathf.Max(1, floor); Save(); }
    }
}
