using System;

namespace Ulon.Shared
{
    /// <summary>
    /// GM 계정 원장. 기획 §13.2 운영 명령은 서버가 계정으로 연다 — 클라 F1·<c>-ulon-gm</c>은 패널만.
    /// 파일: StreamingAssets/Data/gm_accounts.json. 비어 있으면 전용 서버에서는 아무도 못 연다.
    /// </summary>
    public static class GmAccounts
    {
        public const string FileName = "gm_accounts.json";

        [Serializable]
        class File_
        {
            public string[] accounts;
        }

        static string[] listed;
        static string loadError = "";

        /// <summary>검사·에디터가 원장 없이 한 계정을 넣는 자리. 프로덕션 경로는 파일·CLI.</summary>
        public static string ExtraAccount;

        public static string LoadError
        {
            get { EnsureLoaded(); return loadError; }
        }

        public static int Count
        {
            get { EnsureLoaded(); return listed != null ? listed.Length : 0; }
        }

        public static void Reload()
        {
            listed = null;
            EnsureLoaded();
        }

        public static bool Listed(string account)
        {
            if (string.IsNullOrEmpty(account))
                return false;
            string cli = Cli.Get("-ulon-gm-account", "");
            if (!string.IsNullOrEmpty(cli) && cli == account)
                return true;
            if (!string.IsNullOrEmpty(ExtraAccount) && ExtraAccount == account)
                return true;
            EnsureLoaded();
            if (listed == null)
                return false;
            for (int i = 0; i < listed.Length; i++)
            {
                if (listed[i] == account)
                    return true;
            }
            return false;
        }

        static void EnsureLoaded()
        {
            if (listed != null)
                return;
            listed = Array.Empty<string>();
            loadError = "";
            if (!DataLedger.TryRead(FileName, out File_ parsed, out loadError))
                return;
            if (parsed.accounts == null)
                return;
            listed = parsed.accounts;
        }
    }
}
