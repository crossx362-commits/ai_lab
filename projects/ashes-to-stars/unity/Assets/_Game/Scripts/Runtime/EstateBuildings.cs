using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 영지 기능 건물 전용 프랍(§16). village_house_* 는 필드 장식이다.
    /// 본성·광산·창고·수비대·대장간·영묘·탑·경매장. QA_NO면 옛 집·헛간·우물·등불·수레.
    /// </summary>
    public static class EstateBuildings
    {
        public const string EnvShow = "QA_ESTATE_BUILDINGS";
        public const string EnvNo = "QA_NO_ESTATE_BUILDINGS";
        public const string Keep = "estate_keep_0";
        public const string Mine = "estate_mine_0";
        public const string Warehouse = "estate_warehouse_0";
        public const string Barracks = "estate_barracks_0";
        public const string Smith = "estate_smith_0";
        public const string Mausoleum = "estate_mausoleum_0";
        public const string Tower = "estate_tower_0";
        public const string Auction = "estate_auction_0";

        static bool _qaSeeded;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShowQa
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvShow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string DedicatedOf(EstateGrid.Cell c) => c switch
        {
            EstateGrid.Cell.Keep => Keep,
            EstateGrid.Cell.Mine => Mine,
            EstateGrid.Cell.Warehouse => Warehouse,
            EstateGrid.Cell.Barracks => Barracks,
            EstateGrid.Cell.Smith => Smith,
            EstateGrid.Cell.Mausoleum => Mausoleum,
            EstateGrid.Cell.Auction => Auction,
            EstateGrid.Cell.Arrow => Tower,
            EstateGrid.Cell.Magic => Tower,
            _ => null,
        };

        public static string OldOf(EstateGrid.Cell c) => c switch
        {
            EstateGrid.Cell.Keep => "village_house_1",
            EstateGrid.Cell.Mine => "village_barn_0",
            EstateGrid.Cell.Warehouse => "village_house_0",
            EstateGrid.Cell.Smith => "village_house_2",
            EstateGrid.Cell.Auction => "village_cart_0",
            EstateGrid.Cell.Mausoleum => "village_well_0",
            EstateGrid.Cell.Barracks => "village_barn_0",
            EstateGrid.Cell.Arrow => "village_lamp_0",
            EstateGrid.Cell.Magic => "village_lamp_0",
            EstateGrid.Cell.Wall => "village_fence_0",
            EstateGrid.Cell.Trap => "village_haystack_0",
            _ => null,
        };

        public static bool HasDedicated(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return Resources.Load<Texture2D>("props/" + name) != null;
        }

        /// <summary>막히면 옛 집. 아니면 전용 그림이 있을 때만 그 이름.</summary>
        public static string PropOf(EstateGrid.Cell c)
        {
            if (Blocked) return OldOf(c);
            string d = DedicatedOf(c);
            if (d != null && HasDedicated(d)) return d;
            return OldOf(c);
        }

        public static string Line() => Blocked
            ? "경매장이 수레다"
            : "경매장은 전용 그림이다(§16)";

        public static void ResetForTest()
        {
            _qaSeeded = false;
        }

        /// <summary>시각 QA. 화살탑 한 칸을 앞에 세워 전용 탑이 보이게 한다.</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa || Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            EstateGrid.EnsureHubBuildings();
            if (EstateGrid.Count(EstateGrid.Cell.Arrow) == 0
                && EstateGrid.Count(EstateGrid.Cell.Magic) == 0)
                EstateGrid.SetCellForTest(4, 6, EstateGrid.Cell.Arrow);
        }
    }
}
