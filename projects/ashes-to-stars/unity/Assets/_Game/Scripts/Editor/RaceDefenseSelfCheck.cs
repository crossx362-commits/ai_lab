using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-9 엘프 표 「방어 -20%」 — RaceDef.방어배율 소비처.
    /// QA_NO_RACE_DEFENSE면 줄을 비운다(옛 화면 = 방어 줄 없음).
    /// </summary>
    public static class RaceDefenseSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Race Defense Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(RaceInfo.EnvNoDefense);
            Environment.SetEnvironmentVariable(RaceInfo.EnvNoDefense, null);

            var defs = Resources.LoadAll<RaceDef>("races");
            Check(defs != null && defs.Length >= 4,
                $"Resources/races 로드 ({(defs == null ? 0 : defs.Length)}종)");

            RaceDef dwarf = RaceInfo.For(RaceId.드워프);
            RaceDef human = RaceInfo.For(RaceId.인간);
            RaceDef elf = RaceInfo.For(RaceId.엘프);
            RaceDef beast = RaceInfo.For(RaceId.수인);
            Check(elf != null && Mathf.Abs(elf.방어배율 - 0.8f) < 0.0001f,
                $"엘프 방어배율 0.8 (실제 {elf?.방어배율})");
            Check(human != null && Mathf.Abs(human.방어배율 - 1f) < 0.0001f,
                $"인간 방어배율 1 (실제 {human?.방어배율})");
            Check(dwarf != null && Mathf.Abs(dwarf.방어배율 - 1f) < 0.0001f,
                $"드워프 방어배율 1 (실제 {dwarf?.방어배율})");
            Check(beast != null && Mathf.Abs(beast.방어배율 - 1f) < 0.0001f,
                $"수인 방어배율 1 (실제 {beast?.방어배율})");

            Check(!RaceInfo.DefenseBlocked, "기본은 켜짐");

            string eLine = RaceInfo.DefenseLine(RaceId.엘프);
            Check(eLine.Contains("×0.8"),
                $"엘프 DefenseLine이 방어배율 필드를 읽는다 (×0.8) — 「{eLine}」");
            Check(eLine.Contains("-20%"),
                $"엘프 −20% 표기 — 「{eLine}」");
            Check(eLine.StartsWith("종족 방어 — ", StringComparison.Ordinal),
                $"엘프 DefenseLine 접두 — 「{eLine}」");

            Check(RaceInfo.DefenseLine(RaceId.인간) == "",
                "인간(×1)은 빈 문자열(기준이면 줄 안 그림)");
            Check(RaceInfo.DefenseLine(RaceId.드워프) == "",
                "드워프(×1)은 빈 문자열");
            Check(RaceInfo.DefenseLine(RaceId.수인) == "",
                "수인(×1)은 빈 문자열");

            // 네거티브: QA_NO면 엘프도 빈 줄(옛 화면).
            Environment.SetEnvironmentVariable(RaceInfo.EnvNoDefense, "1");
            Check(RaceInfo.DefenseBlocked, "QA_NO면 차단");
            Check(RaceInfo.DefenseLine(RaceId.엘프) == "",
                "차단하면 엘프 DefenseLine도 빈 문자열");
            Environment.SetEnvironmentVariable(RaceInfo.EnvNoDefense, null);
            Check(!RaceInfo.DefenseBlocked && RaceInfo.DefenseLine(RaceId.엘프).Contains("×0.8"),
                "차단을 풀면 다시 방어 조각");

            string charSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            Check(charSrc.Contains("RaceInfo.DefenseLine"),
                "CharacterScreen이 DefenseLine을 속성 탭에 그린다");

            string raceSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/RaceInfo.cs"));
            Check(raceSrc.Contains("d.방어배율"),
                "RaceInfo가 d.방어배율을 읽는다 — 지우면 소비처 0곳으로 되돌아간다");

            _ = nameof(RaceInfo.DefenseLine);
            _ = nameof(RaceDef.방어배율);
            _ = nameof(CharacterScreen);

            Environment.SetEnvironmentVariable(RaceInfo.EnvNoDefense, no);

            if (_fail == 0) Debug.Log("[RaceDefenseSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[RaceDefenseSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[RaceDefenseSelfCheck] FAIL {_fail}건");
        }
    }
}
