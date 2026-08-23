using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>필드 도크 지갑 제목. QA_NO면 옛 FormatCurrency 풀표기.</summary>
    public static class WalletTextSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Wallet Text Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(GameState.EnvShowWallet);
            string no = Environment.GetEnvironmentVariable(GameState.EnvNoWallet);
            Environment.SetEnvironmentVariable(GameState.EnvShowWallet, null);
            Environment.SetEnvironmentVariable(GameState.EnvNoWallet, null);

            GameState.ResetAll();
            GameState.Grant(GameState.MixCopper);
            Check(GameState.Wallet.Copper == GameState.MixCopper,
                $"시드 1234567 (실제 {GameState.Wallet.Copper})");
            Check(GameState.WalletText == "123골드",
                $"짧은 표기 123골드 (실제 {GameState.WalletText})");
            Check(GameState.OldWalletText.IndexOf("123골드", StringComparison.Ordinal) >= 0
                  && GameState.OldWalletText.IndexOf("45실버", StringComparison.Ordinal) >= 0
                  && GameState.OldWalletText.IndexOf("67쿠퍼", StringComparison.Ordinal) >= 0,
                $"옛 풀표기 혼합 단위 (실제 {GameState.OldWalletText})");
            Check(GameState.WalletText.IndexOf("실버", StringComparison.Ordinal) < 0
                  && GameState.WalletText.IndexOf("쿠퍼", StringComparison.Ordinal) < 0,
                "짧은 표기에 실버·쿠퍼가 없다");
            Check(EstateStatusHud.CaptionFits(GameState.WalletText),
                "짧은 표기는 도크 16자 안에 들어간다");
            Check(GameState.OldWalletText.Length > GameState.WalletText.Length,
                $"옛 풀표기가 더 길다 ({GameState.OldWalletText.Length} > {GameState.WalletText.Length})");

            GameState.ResetAll();
            GameState.Grant(12_345);
            Check(GameState.WalletText == "1골드",
                $"12345쿠퍼는 1골드 (실제 {GameState.WalletText})");
            Check(GameState.OldWalletText.IndexOf("23실버", StringComparison.Ordinal) >= 0,
                $"옛 12345는 실버가 남는다 (실제 {GameState.OldWalletText})");

            GameState.ResetAll();
            GameState.Grant(50);
            Check(GameState.WalletText == "50쿠퍼" && GameState.OldWalletText == "50쿠퍼",
                $"50쿠퍼는 둘이 같다 (실제 {GameState.WalletText})");

            Environment.SetEnvironmentVariable(GameState.EnvNoWallet, "1");
            GameState.ResetAll();
            GameState.Grant(GameState.MixCopper);
            Check(GameState.WalletTextBlocked, "QA_NO면 차단");
            Check(GameState.WalletText == GameState.OldWalletText,
                $"차단하면 옛 풀표기 (실제 {GameState.WalletText})");
            Check(GameState.WalletText.IndexOf("45실버", StringComparison.Ordinal) >= 0,
                "차단 문구에 실버가 있다");
            Environment.SetEnvironmentVariable(GameState.EnvNoWallet, null);

            GameState.ResetAll();
            Environment.SetEnvironmentVariable(GameState.EnvShowWallet, "1");
            GameState.SeedWalletTextQaIfRequested();
            Check(GameState.Wallet.Copper >= GameState.MixCopper,
                $"시드 지갑 ≥1234567 (실제 {GameState.Wallet.Copper})");
            Check(GameState.WalletText == "123골드",
                $"시드 문구 123골드 (실제 {GameState.WalletText})");
            Environment.SetEnvironmentVariable(GameState.EnvShowWallet, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string gsSrc = File.ReadAllText(Path.Combine(runtime, "GameState.cs"));
            string fieldSrc = File.ReadAllText(Path.Combine(runtime, "FieldScreen.cs"));
            int wt = gsSrc.IndexOf("public static string WalletText", StringComparison.Ordinal);
            int bag = gsSrc.IndexOf("public static string BagText", StringComparison.Ordinal);
            Check(wt >= 0 && bag > wt
                  && gsSrc.IndexOf("ShortCopper(Wallet.Copper)", wt, bag - wt) >= 0
                  && gsSrc.IndexOf("FormatCurrency(Wallet.Copper)", wt, bag - wt) < 0,
                "WalletText는 ShortCopper만");
            Check(gsSrc.IndexOf("FormatCurrency(Wallet.Copper)", StringComparison.Ordinal) >= 0,
                "OldWalletText는 FormatCurrency를 남긴다");
            Check(fieldSrc.IndexOf("GameState.WalletText", StringComparison.Ordinal) >= 0
                  && fieldSrc.IndexOf("SeedWalletTextQaIfRequested", StringComparison.Ordinal) >= 0,
                "필드가 WalletText·시드를 읽는다");
            Check(File.ReadAllText(Path.Combine(runtime, "DungeonScreen.cs"))
                    .IndexOf("GameState.WalletText", StringComparison.Ordinal) >= 0,
                "던전 부제가 WalletText를 읽는다");
            Check(File.ReadAllText(Path.Combine(runtime, "TowerScreen.cs"))
                    .IndexOf("GameState.WalletText", StringComparison.Ordinal) >= 0,
                "탑 부제가 WalletText를 읽는다");

            _ = nameof(GameState.WalletText);
            _ = nameof(GameState.OldWalletText);
            _ = nameof(GameState.SeedWalletTextQaIfRequested);

            Environment.SetEnvironmentVariable(GameState.EnvShowWallet, show);
            Environment.SetEnvironmentVariable(GameState.EnvNoWallet, no);
            GameState.ResetAll();

            string dir = Path.Combine(Application.dataPath, "../..", "results");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "wallet_text_selfcheck.log");
            var body = new StringBuilder();
            body.AppendLine(_fail == 0 ? "PASS WalletTextSelfCheck" : "FAIL WalletTextSelfCheck");
            body.Append(_log);
            File.WriteAllText(path, body.ToString());
            if (_fail == 0) Debug.Log("[WalletTextSelfCheck] PASS → " + path + "\n" + _log);
            else Debug.LogError("[WalletTextSelfCheck] FAIL " + _fail + " → " + path + "\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[WalletTextSelfCheck] FAIL {_fail}건");
        }
    }
}
