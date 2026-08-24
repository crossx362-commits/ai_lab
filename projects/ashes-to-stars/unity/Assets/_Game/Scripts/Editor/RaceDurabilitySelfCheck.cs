using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-9 드워프 표 「방어 건물 내구 +20%」 — RaceDef.건물내구배율 소비처.
    /// QA_NO_RACE_DURABILITY면 줄을 비운다(옛 화면 = 내구 줄 없음).
    /// </summary>
    public static class RaceDurabilitySelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Race Durability Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(RaceInfo.EnvNoDurability);
            string show = Environment.GetEnvironmentVariable(RaceInfo.EnvShowDurability);
            Environment.SetEnvironmentVariable(RaceInfo.EnvNoDurability, null);
            Environment.SetEnvironmentVariable(RaceInfo.EnvShowDurability, null);

            var defs = Resources.LoadAll<RaceDef>("races");
            Check(defs != null && defs.Length >= 4,
                $"Resources/races 로드 ({(defs == null ? 0 : defs.Length)}종)");

            RaceDef dwarf = RaceInfo.For(RaceId.드워프);
            RaceDef human = RaceInfo.For(RaceId.인간);
            RaceDef elf = RaceInfo.For(RaceId.엘프);
            RaceDef beast = RaceInfo.For(RaceId.수인);
            Check(dwarf != null && Mathf.Abs(dwarf.건물내구배율 - 1.2f) < 0.0001f,
                $"드워프 건물내구배율 1.2 (실제 {dwarf?.건물내구배율})");
            Check(human != null && Mathf.Abs(human.건물내구배율 - 1f) < 0.0001f,
                $"인간 건물내구배율 1 (실제 {human?.건물내구배율})");
            Check(elf != null && Mathf.Abs(elf.건물내구배율 - 1f) < 0.0001f,
                $"엘프 건물내구배율 1 (실제 {elf?.건물내구배율})");
            Check(beast != null && Mathf.Abs(beast.건물내구배율 - 1f) < 0.0001f,
                $"수인 건물내구배율 1 (실제 {beast?.건물내구배율})");

            Check(!RaceInfo.DurabilityBlocked, "기본은 켜짐");

            string dLine = RaceInfo.DurabilityLine(RaceId.드워프);
            Check(dLine.Contains("×1.2"),
                $"드워프 DurabilityLine이 건물내구배율 필드를 읽는다 (×1.2) — 「{dLine}」");
            Check(dLine.Contains("+20%"),
                $"드워프 +20% 표기 — 「{dLine}」");
            Check(dLine.StartsWith("건물 내구 — ", StringComparison.Ordinal),
                $"드워프 DurabilityLine 접두 — 「{dLine}」");

            Check(RaceInfo.DurabilityLine(RaceId.인간) == "",
                "인간(×1)은 빈 문자열(기준이면 줄 안 그림)");
            Check(RaceInfo.DurabilityLine(RaceId.엘프) == "",
                "엘프(×1)은 빈 문자열");
            Check(RaceInfo.DurabilityLine(RaceId.수인) == "",
                "수인(×1)은 빈 문자열");

            Environment.SetEnvironmentVariable(RaceInfo.EnvNoDurability, "1");
            Check(RaceInfo.DurabilityBlocked, "QA_NO면 차단");
            Check(RaceInfo.DurabilityLine(RaceId.드워프) == "",
                "차단하면 드워프 DurabilityLine도 빈 문자열");
            Environment.SetEnvironmentVariable(RaceInfo.EnvNoDurability, null);
            Check(!RaceInfo.DurabilityBlocked && RaceInfo.DurabilityLine(RaceId.드워프).Contains("×1.2"),
                "차단을 풀면 다시 내구 조각");

            string charSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            Check(charSrc.Contains("RaceInfo.DurabilityLine"),
                "CharacterScreen이 DurabilityLine을 속성 탭에 그린다");
            Check(charSrc.Contains("SeedRaceDurabilityQaIfRequested"),
                "CharacterScreen이 내구 QA 시드를 부른다");

            string raceSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/RaceInfo.cs"));
            Check(raceSrc.Contains("d.건물내구배율"),
                "RaceInfo가 d.건물내구배율을 읽는다 — 지우면 소비처 0곳으로 되돌아간다");

            string setupSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Editor/ProjectSetup.cs"));
            Check(setupSrc.Contains("건물내구배율 = r.Item1 == RaceId.드워프 ? 1.20f"),
                "ProjectSetup이 드워프 1.20을 심는다");

            _ = nameof(RaceInfo.DurabilityLine);
            _ = nameof(RaceDef.건물내구배율);
            _ = nameof(CharacterScreen);

            Environment.SetEnvironmentVariable(RaceInfo.EnvNoDurability, no);
            Environment.SetEnvironmentVariable(RaceInfo.EnvShowDurability, show);

            if (_fail == 0) Debug.Log("[RaceDurabilitySelfCheck] PASS\n" + _log);
            else Debug.LogError($"[RaceDurabilitySelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[RaceDurabilitySelfCheck] FAIL {_fail}건");
        }
    }
}
