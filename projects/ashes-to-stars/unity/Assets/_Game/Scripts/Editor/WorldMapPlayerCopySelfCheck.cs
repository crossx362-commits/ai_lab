using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class WorldMapPlayerCopySelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool condition, string message)
        {
            if (!condition) _fail++;
            _log.AppendLine((condition ? "  PASS  " : "  FAIL  ") + message);
        }

        [MenuItem("Ashes to Stars/QA/World Map Player Copy Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string old = Environment.GetEnvironmentVariable(WorldMapScreen.EnvNoPlayerCopy);
            Environment.SetEnvironmentVariable(WorldMapScreen.EnvNoPlayerCopy, null);

            string cleaned = WorldMapScreen.PlayerCopy(
                "엘프 인식 +20%(§18-9) · 층을 오를수록 내 별이 커진다(§14)");
            Check(cleaned == "엘프 인식 +20% · 층을 오를수록 내 별이 커진다",
                $"플레이어 문구에서 원장 절 번호 제거 (실제 '{cleaned}')");
            Check(WorldMapScreen.PlayerCopy("영공 11.00(§18-13)") == "영공 11.00",
                "영공 QA 문구도 절 번호 제거");
            Check(WorldMapScreen.PlayerCopy("내 별 3층 · 침략은 탑 30층(§14·§15)")
                    == "내 별 3층 · 침략은 탑 30층",
                "헤더 해금 문구의 복합 절 번호 제거");

            Environment.SetEnvironmentVariable(WorldMapScreen.EnvNoPlayerCopy, "1");
            string legacy = WorldMapScreen.PlayerCopy("별 3/3(§18-9)");
            Check(legacy == "별 3/3(§18-9)",
                "QA_NO_WORLD_MAP_PLAYER_COPY는 옛 문구 복원");

            Environment.SetEnvironmentVariable(WorldMapScreen.EnvNoPlayerCopy, old);
            if (_fail == 0) Debug.Log("[WorldMapPlayerCopySelfCheck] PASS\n" + _log);
            else Debug.LogError($"[WorldMapPlayerCopySelfCheck] FAIL {_fail}건\n{_log}");
            if (_fail > 0) throw new InvalidOperationException(
                $"[WorldMapPlayerCopySelfCheck] FAIL {_fail}건");
        }
    }
}
