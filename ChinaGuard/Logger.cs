using System;
using System.IO;

namespace ChinaGuard
{
    static class Logger
    {
        static readonly object Sync = new object();
        public static readonly string LogPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "chinaguard.log");

        public static void Log(string message)
        {
            try
            {
                lock (Sync)
                {
                    File.AppendAllText(LogPath,
                        string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}\r\n", DateTime.Now, message));
                }
            }
            catch { }
        }
    }
}
