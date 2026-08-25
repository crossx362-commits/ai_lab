using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class AuctionHudPlayerCopySelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Auction Hud Player Copy Self Check")]
        public static void Run()
        {
            string saved = Environment.GetEnvironmentVariable(AuctionHud.EnvNoPlayerCopy);
            Environment.SetEnvironmentVariable(AuctionHud.EnvNoPlayerCopy, null);

            string cleaned = AuctionHud.PlayerCopy("HUD는 경매 배경을 가리지 않는다(§16)");
            if (cleaned != "HUD는 경매 배경을 가리지 않는다")
                throw new InvalidOperationException("경매 HUD 안내 문구에서 절 번호를 제거하지 못했다: " + cleaned);

            Environment.SetEnvironmentVariable(AuctionHud.EnvNoPlayerCopy, "1");
            if (AuctionHud.PlayerCopy("HUD는 경매 배경을 가리지 않는다(§16)") != "HUD는 경매 배경을 가리지 않는다(§16)")
                throw new InvalidOperationException("QA_NO가 옛 절 번호 문구를 복원하지 못했다");
            Environment.SetEnvironmentVariable(AuctionHud.EnvNoPlayerCopy, saved);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime/AuctionHud.cs");
            string source = File.ReadAllText(runtime);
            if (!source.Contains("PlayerCopy(\"HUD는 경매 배경을 가리지 않는다(§16)\")"))
                throw new InvalidOperationException("경매 HUD 안내 문구가 PlayerCopy를 소비하지 않는다");

            Debug.Log("[AuctionHudPlayerCopySelfCheck] PASS — 경매 HUD 안내 문구 절 번호 제거 + QA_NO 복원");
        }
    }
}
