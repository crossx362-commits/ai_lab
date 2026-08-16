using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 침략 본게임 한 슬라이스(§15). 다른 유저 서버가 아니라 로컬 별 수비대와 싸운다.
    /// 출정 비용·승리 약탈·패배 추가 소모·PvP 목숨 예외가 생산 경계다.
    /// 진입 면은 EstateGrid가 고른 최단 4면이다. 랭킹·동맹·경로 전투는 여기서 열지 않는다.
    /// </summary>
    public static class InvasionState
    {
        /// <summary>
        /// 침략당한 직후 보호막 = 수비대 회복(§15 ✅). 한쪽만 바꾸면
        /// "보호막은 끝났는데 수비대는 아직"인 무방비 창이 생긴다.
        /// </summary>
        public const long GuardHours = 12;
        public const long GuardSeconds = GuardHours * 3600;
        public const long DefenseRecoverSeconds = GuardSeconds;
        public const string EnvShow = "QA_INVASION_SHIELD";
        public const string EnvNo = "QA_NO_INVASION_SHIELD";
        public const string EnvShowLoot = "QA_RACE_LOOT";
        public const string EnvNoLoot = "QA_NO_RACE_LOOT";
        public const string EnvShowCap = "QA_LOOT_CAP";
        public const string EnvNoCap = "QA_NO_LOOT_CAP";
        public const int HumanLootPercent = 100;
        public const int BeastLootPercent = 120;
        /// <summary>§18-13 약탈 상한. 티어 1시간 수익의 6시간치.</summary>
        public const int LootCapHours = 6;

        const string K_PENDING = "ats.invasion.pending";
        const string K_PAID = "ats.invasion.paid";
        const string K_LAST = "ats.invasion.last_loot";
        const string K_SIDE = "ats.invasion.side";
        const string K_SHIELD = "ats.invasion.shield_until";

        static bool _loaded;
        static bool _pending;
        static long _paid;
        static long _lastLoot;
        static long _shieldUntil;
        static EstateGrid.Side _approach;

        public static Func<long> NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public static bool Pending { get { Load(); return _pending; } }
        public static long LastLoot { get { Load(); return _lastLoot; } }
        public static EstateGrid.Side ApproachSide { get { Load(); return _approach; } }
        public static long ShieldUntil { get { Load(); return _shieldUntil; } }
        public static bool ShieldActive => RemainingSeconds() > 0;

        public static bool ShieldBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _pending = PlayerPrefs.GetInt(K_PENDING, 0) == 1;
            long.TryParse(PlayerPrefs.GetString(K_PAID, "0"), out _paid);
            long.TryParse(PlayerPrefs.GetString(K_LAST, "0"), out _lastLoot);
            long.TryParse(PlayerPrefs.GetString(K_SHIELD, "0"), out _shieldUntil);
            int side = PlayerPrefs.GetInt(K_SIDE, (int)EstateGrid.Side.북);
            _approach = (EstateGrid.Side)Mathf.Clamp(side, 0, 3);
        }

        static void Save()
        {
            PlayerPrefs.SetInt(K_PENDING, _pending ? 1 : 0);
            PlayerPrefs.SetString(K_PAID, _paid.ToString());
            PlayerPrefs.SetString(K_LAST, _lastLoot.ToString());
            PlayerPrefs.SetString(K_SHIELD, _shieldUntil.ToString());
            PlayerPrefs.SetInt(K_SIDE, (int)_approach);
            PlayerPrefs.Save();
        }

        public static long RemainingSeconds()
        {
            Load();
            long left = _shieldUntil - NowUnix();
            return left > 0 ? left : 0;
        }

        public static string ShieldText()
        {
            long s = RemainingSeconds();
            if (s <= 0) return "";
            if (s >= 3600) return $"{s / 3600}시간 {(s % 3600) / 60}분";
            if (s >= 60) return $"{s / 60}분";
            return $"{s}초";
        }

        public static string ShieldBlockReason()
        {
            if (!ShieldActive) return "";
            return $"보호막 {ShieldText()} — 수비대 회복과 같은 12시간(§15)";
        }

        public static void ArmShield()
        {
            if (ShieldBlocked) return;
            _shieldUntil = NowUnix() + GuardSeconds;
        }

        public static long SortieCost() =>
            Economy.GetActionCost("InvasionAttack", GameState.Tier);

        public static long DefeatCost() =>
            Economy.GetActionCost("InvasionAttackDefeat", GameState.Tier);

        /// <summary>SelfCheck가 종족 배율을 고정할 때만. 0이면 RaceDef·계정 종족을 본다.</summary>
        public static float ForceRaceLootMul;

        /// <summary>SelfCheck가 상한 앞 금액을 고정할 때만. 0이면 출정×3 공식.</summary>
        public static long ForceLootBeforeCap;

        public static bool LootRaceBlocked =>
            Environment.GetEnvironmentVariable(EnvNoLoot) == "1";

        public static bool LootCapBlocked =>
            Environment.GetEnvironmentVariable(EnvNoCap) == "1";

        /// <summary>§18-9 수인 약탈량 +20%. 에셋이 없으면 표로 폴백한다.</summary>
        public static int RaceLootPercent()
        {
            if (LootRaceBlocked) return HumanLootPercent;
            if (ForceRaceLootMul > 0f) return Math.Max(1, (int)Math.Round(ForceRaceLootMul * 100f));
            try
            {
                var races = Resources.LoadAll<RaceDef>("races");
                RaceId id = RacePrefs.Get();
                for (int i = 0; i < races.Length; i++)
                {
                    if (races[i] != null && races[i].Id == id && races[i].약탈량배율 > 0f)
                        return Math.Max(1, (int)Math.Round(races[i].약탈량배율 * 100f));
                }
            }
            catch
            {
                // 배치 검사 중 에셋 DB가 비면 표로 간다.
            }
            return RacePrefs.Get() == RaceId.수인 ? BeastLootPercent : HumanLootPercent;
        }

        public static long ApplyRaceLoot(long copper) => copper * RaceLootPercent() / 100;

        public static string RaceLootLine()
        {
            if (RaceLootPercent() == BeastLootPercent && RacePrefs.Get() == RaceId.수인)
                return "수인 약탈 +20%(§18-9)";
            return "종족 약탈 배율 없음";
        }

        /// <summary>§18-13 같은 티어 6시간치. T1=60000.</summary>
        public static long CapCopper(int tier)
        {
            var mul = Economy.TierRevenueMultiplier;
            if (mul == null || mul.Length == 0) return LootCapHours * Economy.COPPER_PER_GOLD;
            if (tier < 0) tier = 0;
            if (tier >= mul.Length) tier = mul.Length - 1;
            return (long)(LootCapHours * mul[tier] * Economy.COPPER_PER_GOLD);
        }

        public static long CapCopper() => CapCopper(GameState.Tier);

        public static long ApplyLootCap(long copper)
        {
            if (LootCapBlocked) return copper;
            if (copper < 0) return 0;
            long cap = CapCopper();
            return copper > cap ? cap : copper;
        }

        public static string LootCapLine()
        {
            if (LootCapBlocked) return "약탈 상한 없음";
            return $"약탈 상한 6 G/h(§18-13) · {Economy.FormatCurrency(CapCopper())}";
        }

        /// <summary>승자 보상은 상대 영지 레벨(여기선 내 탑 층) 기준. 창고를 비워도 준다(§15 1-b).
        /// 수인은 그 금액의 120%(§18-9). 방어 감소 뒤에 곱한다.
        /// 영공 적 디버프가 켜져 있으면 그 위에 95%(§14).
        /// 마지막에 같은 티어 6 G/h로 자른다(§18-13). QA_NO_LOOT_CAP=1이면 안 자른다.</summary>
        public static long LootCopper()
        {
            long raw;
            if (ForceLootBeforeCap > 0)
            {
                raw = ForceLootBeforeCap;
            }
            else
            {
                long baseLoot = Economy.GetActionCostBase("InvasionAttack", GameState.Tier) * 3;
                if (baseLoot < 1000) baseLoot = 1000;
                int empty = DefenseState.MaxSlots - DefenseState.Count;
                raw = ApplyRaceLoot(EstateDefense.ApplyToLoot(baseLoot + baseLoot * empty / 10));
                raw = WorldStar.ApplyEnemy(raw);
            }
            return ApplyLootCap(raw);
        }

        public static bool TryBegin()
        {
            Load();
            if (_pending) return false;
            if (ShieldActive) return false;
            if (!GameState.CanInvade()) return false;
            // 출정 순간에도 영공을 읽는다. 약탈 공식은 LootCopper가 EnemyMul을 쓴다.
            _ = WorldStar.EnemyMul;
            long cost = SortieCost();
            if (!GameState.Pay(cost)) return false;
            _pending = true;
            _paid = cost;
            _lastLoot = 0;
            _approach = EstateGrid.InvaderSide();
            Save();
            return true;
        }

        public static long Settle(bool won)
        {
            Load();
            if (!_pending) return 0;
            _pending = false;
            long loot = 0;
            if (won)
            {
                loot = LootCopper();
                _lastLoot = GameState.Earn(loot);
            }
            else
            {
                GameState.Pay(DefeatCost());
                _lastLoot = 0;
            }
            _paid = 0;
            ArmShield();
            Save();
            return loot;
        }

        public static void SeedQaIfRequested()
        {
            string raw = Environment.GetEnvironmentVariable(EnvShow);
            if (string.IsNullOrEmpty(raw)) return;
            if (ShieldBlocked) return;
            bool show = raw == "1"
                || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            if (!show) return;
            Load();
            if (GameState.TowerFloor < WorldMapScreen.InvasionUnlockFloor)
                GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            _pending = false;
            ArmShield();
            Save();
        }

        /// <summary>시각 QA. QA_RACE_LOOT=1이면 수인·30층·보호막 없음으로 침략 카드를 연다.</summary>
        public static void SeedRaceLootQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShowLoot) != "1") return;
            if (LootRaceBlocked) return;
            Load();
            RacePrefs.Set(RaceId.수인);
            if (GameState.TowerFloor < WorldMapScreen.InvasionUnlockFloor)
                GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            if (GameState.Wallet.Copper < SortieCost())
                GameState.Earn(SortieCost());
            _pending = false;
            _shieldUntil = 0;
            Save();
        }

        /// <summary>시각 QA. QA_RACE_COST=1이면 드워프·30층·보호막 없음으로 침략 카드를 연다.</summary>
        public static void SeedRaceCostQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(Economy.EnvShowCost) != "1") return;
            if (Economy.CostRaceBlocked) return;
            Load();
            RacePrefs.Set(RaceId.드워프);
            if (GameState.TowerFloor < WorldMapScreen.InvasionUnlockFloor)
                GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            if (GameState.Wallet.Copper < SortieCost())
                GameState.Earn(SortieCost());
            _pending = false;
            _shieldUntil = 0;
            Save();
        }

        /// <summary>시각 QA. QA_AURA_DEBUFF=1이면 30층·보호막 없음으로 침략 카드를 연다.</summary>
        public static void SeedAuraDebuffQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(WorldStar.EnvShowDebuff) != "1") return;
            if (WorldStar.AuraDebuffBlocked) return;
            Load();
            RacePrefs.Set(RaceId.인간);
            if (GameState.TowerFloor < WorldMapScreen.InvasionUnlockFloor)
                GameState.SetTowerFloorForTest(WorldMapScreen.InvasionUnlockFloor);
            if (GameState.Wallet.Copper < SortieCost())
                GameState.Earn(SortieCost());
            _pending = false;
            _shieldUntil = 0;
            Save();
            WorldStar.SeedAuraDebuffQaIfRequested();
        }

        /// <summary>시각 QA. QA_LOOT_CAP=1이면 T10·보호막 없음·디버프 꺼짐으로 침략 카드를 연다.
        /// 이전 QA가 고른 티어·영공을 남기면 상한이 T1(6골드)로 보이므로 여기서 고친다.</summary>
        public static void SeedLootCapQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShowCap) != "1") return;
            if (LootCapBlocked) return;
            Load();
            RacePrefs.Set(RaceId.인간);
            WorldStar.EnemyDebuff = false;
            WorldStar.AllyBuff = false;
            if (GameState.TowerFloor < 100)
                GameState.SetTowerFloorForTest(100);
            GameState.TrySelectTier(9);
            if (GameState.Wallet.Copper < SortieCost())
                GameState.Earn(SortieCost());
            _pending = false;
            _shieldUntil = 0;
            Save();
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_PENDING);
            PlayerPrefs.DeleteKey(K_PAID);
            PlayerPrefs.DeleteKey(K_LAST);
            PlayerPrefs.DeleteKey(K_SIDE);
            PlayerPrefs.DeleteKey(K_SHIELD);
            PlayerPrefs.Save();
            NowUnix = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            ForceRaceLootMul = 0f;
            ForceLootBeforeCap = 0;
            _pending = false;
            _paid = 0;
            _lastLoot = 0;
            _shieldUntil = 0;
            _approach = EstateGrid.Side.북;
            _loaded = false;
        }

        public static void ForgetInMemoryForTest()
        {
            _pending = false;
            _paid = _lastLoot = _shieldUntil = 0;
            _approach = EstateGrid.Side.북;
            _loaded = false;
        }
    }
}
