using System;
using System.IO;
using System.Text;

namespace GeoGuard
{
    static class Logger
    {
        static readonly object Sync = new object();
        // BOM 없이 쓰면 메모장/PowerShell이 ANSI로 오인해 한글이 깨진다.
        static readonly Encoding Utf8Bom = new UTF8Encoding(true);

        public static readonly string LogPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GeoGuard.log");

        public static void Log(string message)
        {
            try
            {
                lock (Sync)
                {
                    File.AppendAllText(LogPath,
                        string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}\r\n", DateTime.Now, message),
                        Utf8Bom);
                }
            }
            catch { }
        }
    }
}
