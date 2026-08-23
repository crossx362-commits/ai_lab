using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-9 엘프 표 「HP -15%」 — RaceDef.체력배율 소비처.
    /// QA_NO_RACE_HEALTH면 줄을 비운다(옛 화면 = 체력 줄 없음).
    /// </summary>
    public static class RaceHealthSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Race Health Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(RaceInfo.EnvNoHealth);
            Environment.SetEnvironmentVariable(RaceInfo.EnvNoHealth, null);

            var defs = Resources.LoadAll<RaceDef>("races");
            Check(defs != null && defs.Length >= 4,
                $"Resources/races 로드 ({(defs == null ? 0 : defs.Length)}종)");

            RaceDef dwarf = RaceInfo.For(RaceId.드워프);
            RaceDef human = RaceInfo.For(RaceId.인간);
            RaceDef elf = RaceInfo.For(RaceId.엘프);
            RaceDef beast = RaceInfo.For(RaceId.수인);
            Check(elf != null && Mathf.Abs(elf.체력배율 - 0.85f) < 0.0001f,
                $"엘프 체력배율 0.85 (실제 {elf?.체력배율})");
            Check(human != null && Mathf.Abs(human.체력배율 - 1f) < 0.0001f,
                $"인간 체력배율 1 (실제 {human?.체력배율})");
            Check(dwarf != null && Mathf.Abs(dwarf.체력배율 - 1f) < 0.0001f,
                $"드워프 체력배율 1 (실제 {dwarf?.체력배율})");
            Check(beast != null && Mathf.Abs(beast.체력배율 - 1f) < 0.0001f,
                $"수인 체력배율 1 (실제 {beast?.체력배율})");

            Check(!RaceInfo.HealthBlocked, "기본은 켜짐");

            string eLine = RaceInfo.HealthLine(RaceId.엘프);
            Check(eLine.Contains("×0.85"),
                $"엘프 HealthLine이 체력배율 필드를 읽는다 (×0.85) — 「{eLine}」");
            Check(eLine.Contains("-15%"),
                $"엘프 −15% 표기 — 「{eLine}」");
            Check(eLine.StartsWith("종족 체력 — ", StringComparison.Ordinal),
                $"엘프 HealthLine 접두 — 「{eLine}」");

            Check(RaceInfo.HealthLine(RaceId.인간) == "",
                "인간(×1)은 빈 문자열(기준이면 줄 안 그림)");
            Check(RaceInfo.HealthLine(RaceId.드워프) == "",
                "드워프(×1)은 빈 문자열");
            Check(RaceInfo.HealthLine(RaceId.수인) == "",
                "수인(×1)은 빈 문자열");

            // 네거티브: QA_NO면 엘프도 빈 줄(옛 화면).
            Environment.SetEnvironmentVariable(RaceInfo.EnvNoHealth, "1");
            Check(RaceInfo.HealthBlocked, "QA_NO면 차단");
            Check(RaceInfo.HealthLine(RaceId.엘프) == "",
                "차단하면 엘프 HealthLine도 빈 문자열");
            Environment.SetEnvironmentVariable(RaceInfo.EnvNoHealth, null);
            Check(!RaceInfo.HealthBlocked && RaceInfo.HealthLine(RaceId.엘프).Contains("×0.85"),
                "차단을 풀면 다시 체력 조각");

            string charSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            Check(charSrc.Contains("RaceInfo.HealthLine"),
                "CharacterScreen이 HealthLine을 속성 탭에 그린다");

            string raceSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/RaceInfo.cs"));
            Check(raceSrc.Contains("d.체력배율"),
                "RaceInfo가 d.체력배율을 읽는다 — 지우면 소비처 0곳으로 되돌아간다");

            _ = nameof(RaceInfo.HealthLine);
            _ = nameof(RaceDef.체력배율);
            _ = nameof(CharacterScreen);

            Environment.SetEnvironmentVariable(RaceInfo.EnvNoHealth, no);

            if (_fail == 0) Debug.Log("[RaceHealthSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[RaceHealthSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[RaceHealthSelfCheck] FAIL {_fail}건");
        }
    }
}
