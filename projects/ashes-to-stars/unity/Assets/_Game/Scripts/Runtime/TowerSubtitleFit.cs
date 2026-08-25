using System;

namespace AshesToStars
{
    /// <summary>탑은 전투 규칙 요약이 길어서 슬림 제목판에서만 한 줄 맞춤이 필요하다.</summary>
    public static class TowerSubtitleFit
    {
        public const string EnvNo = "QA_NO_TOWER_SUBTITLE_FIT";

        public static bool Enabled
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw != "1" && !string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
