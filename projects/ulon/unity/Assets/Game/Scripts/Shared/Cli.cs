using System;

namespace Ulon.Shared
{
    public static class Cli
    {
        public static bool Has(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == flag)
                    return true;
            return false;
        }

        public static string Get(string flag, string fallback = "")
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == flag)
                    return args[i + 1];
            return fallback;
        }
    }
}
