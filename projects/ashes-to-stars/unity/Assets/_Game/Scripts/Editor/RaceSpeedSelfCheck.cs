using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-9 드워프 표 「이속 -15%」 — RaceDef.이속배율 소비처.
    /// QA_NO_RACE_SPEED면 줄을 비운다(옛 화면 = 이속 줄 없음).
    /// </summary>
    public static class RaceSpeedSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Race Speed Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(RaceInfo.EnvNoSpeed);
            Environment.SetEnvironmentVariable(RaceInfo.EnvNoSpeed, null);

            var defs = Resources.LoadAll<RaceDef>("races");
            Check(defs != null && defs.Length >= 4,
                $"Resources/races 로드 ({(defs == null ? 0 : defs.Length)}종)");

            RaceDef dwarf = RaceInfo.For(RaceId.드워프);
            RaceDef human = RaceInfo.For(RaceId.인간);
            RaceDef elf = RaceInfo.For(RaceId.엘프);
            RaceDef beast = RaceInfo.For(RaceId.수인);
            Check(dwarf != null && Mathf.Abs(dwarf.이속배율 - 0.85f) < 0.0001f,
                $"드워프 이속배율 0.85 (실제 {dwarf?.이속배율})");
            Check(human != null && Mathf.Abs(human.이속배율 - 1f) < 0.0001f,
                $"인간 이속배율 1 (실제 {human?.이속배율})");
            Check(elf != null && Mathf.Abs(elf.이속배율 - 1f) < 0.0001f,
                $"엘프 이속배율 1 (실제 {elf?.이속배율})");
            Check(beast != null && Mathf.Abs(beast.이속배율 - 1f) < 0.0001f,
                $"수인 이속배율 1 (실제 {beast?.이속배율})");

            Check(!RaceInfo.SpeedBlocked, "기본은 켜짐");

            string dLine = RaceInfo.SpeedLine(RaceId.드워프);
            Check(dLine.Contains("×0.85"),
                $"드워프 SpeedLine이 이속배율 필드를 읽는다 (×0.85) — 「{dLine}」");
            Check(dLine.Contains("-15%"),
                $"드워프 −15% 표기 — 「{dLine}」");
            Check(dLine.StartsWith("종족 이속 — ", StringComparison.Ordinal),
                $"드워프 SpeedLine 접두 — 「{dLine}」");

            Check(RaceInfo.SpeedLine(RaceId.인간) == "",
                "인간(×1)은 빈 문자열(기준이면 줄 안 그림)");
            Check(RaceInfo.SpeedLine(RaceId.엘프) == "",
                "엘프(×1)은 빈 문자열");
            Check(RaceInfo.SpeedLine(RaceId.수인) == "",
                "수인(×1)은 빈 문자열");

            // 네거티브: QA_NO면 드워프도 빈 줄(옛 화면).
            Environment.SetEnvironmentVariable(RaceInfo.EnvNoSpeed, "1");
            Check(RaceInfo.SpeedBlocked, "QA_NO면 차단");
            Check(RaceInfo.SpeedLine(RaceId.드워프) == "",
                "차단하면 드워프 SpeedLine도 빈 문자열");
            Environment.SetEnvironmentVariable(RaceInfo.EnvNoSpeed, null);
            Check(!RaceInfo.SpeedBlocked && RaceInfo.SpeedLine(RaceId.드워프).Contains("×0.85"),
                "차단을 풀면 다시 이속 조각");

            string charSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            Check(charSrc.Contains("RaceInfo.SpeedLine"),
                "CharacterScreen이 SpeedLine을 속성 탭에 그린다");

            string raceSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/RaceInfo.cs"));
            Check(raceSrc.Contains("d.이속배율"),
                "RaceInfo가 d.이속배율을 읽는다 — 지우면 소비처 0곳으로 되돌아간다");

            _ = nameof(RaceInfo.SpeedLine);
            _ = nameof(RaceDef.이속배율);
            _ = nameof(CharacterScreen);

            Environment.SetEnvironmentVariable(RaceInfo.EnvNoSpeed, no);

            if (_fail == 0) Debug.Log("[RaceSpeedSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[RaceSpeedSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[RaceSpeedSelfCheck] FAIL {_fail}건");
        }
    }
}
