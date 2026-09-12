namespace Ulon.Shared
{
    /// <summary>기획 §18.13 Context Menu 대상. HUD·우클릭·게이트가 같이 읽는다.</summary>
    public enum ContextKind
    {
        None = 0,
        Trainer,
        Pet,
        HousePlot,
        HouseChest,
        HouseVendor
    }

    /// <summary>
    /// 우클릭 메뉴 원장. 라벨은 여기만. 판정은 서버(<c>TryTrainer</c>·<c>TryPet*</c>·<c>TryLockdown</c>).
    /// </summary>
    public static class ContextKinds
    {
        /// <summary>네거티브 컨트롤 — 켜면 메뉴가 비고 열리지도 않는다.</summary>
        public static bool NcHide;

        public const string TrainOpen = "훈련 열기";
        public const string Follow = "따라와";
        public const string Stay = "기다려";
        public const string Guard = "지켜라";
        public const string Attack = "펫공격";
        public const string Come = "이리와";
        public const string Release = "놓아준다";
        public const string Claim = "토지 청구";
        public const string Lockdown = "잠금";
        public const string Take = "꺼내기";
        public const string VendorList = "진열";
        public const string VendorBuy = "벤더 구매";

        public static string TitleOf(ContextKind kind)
        {
            switch (kind)
            {
                case ContextKind.Trainer: return "훈련";
                case ContextKind.Pet: return "펫 명령";
                case ContextKind.HousePlot: return "집터";
                case ContextKind.HouseChest: return "집 보안";
                case ContextKind.HouseVendor: return "집 벤더";
                default: return "";
            }
        }

        public static string[] LabelsOf(ContextKind kind)
        {
            if (NcHide)
                return System.Array.Empty<string>();
            switch (kind)
            {
                case ContextKind.Trainer:
                    return new[] { TrainOpen };
                case ContextKind.Pet:
                    return new[] { Follow, Stay, Guard, Attack, Come, Release };
                case ContextKind.HousePlot:
                    return new[] { Claim };
                case ContextKind.HouseChest:
                    return new[] { Lockdown, Take };
                case ContextKind.HouseVendor:
                    return new[] { VendorList, VendorBuy };
                default:
                    return System.Array.Empty<string>();
            }
        }
    }
}
