using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// §18-9·원장 160 「발동을 전투당 1회로 묶어」 — RaceDef.전투당발동 소비처.
    /// QA_NO_RACE_BATTLE_CAP면 필드 조각을 빼고 옛 문장만 남긴다.
    /// </summary>
    public static class RaceBattleCapSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Race Battle Cap Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(RaceInfo.EnvNo);
            Environment.SetEnvironmentVariable(RaceInfo.EnvNo, null);

            var defs = Resources.LoadAll<RaceDef>("races");
            Check(defs != null && defs.Length >= 4,
                $"Resources/races 로드 ({(defs == null ? 0 : defs.Length)}종)");

            RaceDef dwarf = RaceInfo.For(RaceId.드워프);
            RaceDef beast = RaceInfo.For(RaceId.수인);
            RaceDef elf = RaceInfo.For(RaceId.엘프);
            RaceDef human = RaceInfo.For(RaceId.인간);
            Check(dwarf != null && dwarf.전투당발동 == 1 && dwarf.고유발동확률 > 0f,
                $"드워프 전투당발동·발동확률 (cap={dwarf?.전투당발동} proc={dwarf?.고유발동확률})");
            Check(beast != null && beast.전투당발동 == 1 && beast.고유발동확률 > 0f,
                $"수인 전투당발동·발동확률 (cap={beast?.전투당발동} proc={beast?.고유발동확률})");
            Check(elf != null && elf.전투당발동 == 1 && elf.고유발동확률 > 0f,
                $"엘프 전투당발동·발동확률 (cap={elf?.전투당발동} proc={elf?.고유발동확률})");
            Check(human != null && human.고유발동확률 <= 0f,
                $"인간 상시 패시브(발동 0) — 전투당 조각 대상 아님 (proc={human?.고유발동확률})");

            Check(!RaceInfo.Blocked, "기본은 켜짐");

            string dLine = RaceInfo.MechanicLine(RaceId.드워프);
            string wantD = "전투당 " + dwarf.전투당발동 + "회";
            string wantProcD = "발동 " + Mathf.RoundToInt(dwarf.고유발동확률 * 100f) + "%";
            Check(dLine.Contains(wantD),
                $"드워프 MechanicLine이 전투당발동 필드를 읽는다 ({wantD}) — 「{dLine}」");
            Check(dLine.Contains(wantProcD),
                $"드워프 형제 발동% 보존 ({wantProcD}) — 「{dLine}」");
            // 필드 조각을 붙일 때 문장 속 「 (전투당 N회)」는 벗겨 단일 출처로 둔다.
            Check(dLine.IndexOf(wantD, StringComparison.Ordinal)
                  == dLine.LastIndexOf(wantD, StringComparison.Ordinal),
                $"드워프 전투당 조각이 한 번만 나온다 — 「{dLine}」");
            int procIdx = dLine.IndexOf(wantProcD, StringComparison.Ordinal);
            int capIdx = dLine.IndexOf(wantD, StringComparison.Ordinal);
            Check(procIdx >= 0 && capIdx > procIdx,
                $"드워프: 발동%가 전투당 앞 (proc {procIdx} · cap {capIdx})");

            string bLine = RaceInfo.MechanicLine(RaceId.수인);
            Check(bLine.Contains("전투당 " + beast.전투당발동 + "회"),
                $"수인 MechanicLine이 전투당발동 필드를 읽는다 — 「{bLine}」");

            string eLine = RaceInfo.MechanicLine(RaceId.엘프);
            Check(eLine.Contains("전투당 " + elf.전투당발동 + "회"),
                $"엘프 MechanicLine이 전투당발동 필드를 읽는다 — 「{eLine}」");

            string hLine = RaceInfo.MechanicLine(RaceId.인간);
            Check(!string.IsNullOrEmpty(hLine) && hLine.IndexOf("전투당", StringComparison.Ordinal) < 0,
                $"인간(발동 0)에는 전투당 조각을 안 붙인다 — 「{hLine}」");

            // 네거티브: QA_NO면 필드 조각을 빼고 옛 문장(에셋 속 전투당)만.
            Environment.SetEnvironmentVariable(RaceInfo.EnvNo, "1");
            Check(RaceInfo.Blocked, "QA_NO면 차단");
            string dOld = RaceInfo.MechanicLine(RaceId.드워프);
            Check(dOld.Contains(wantProcD),
                $"차단해도 발동%는 남는다 — 「{dOld}」");
            // 차단 시 필드 append는 없고, 에셋 문장에 박힌 「 (전투당 1회)」만 남을 수 있다.
            // 괄호 안 「발동 N% · 전투당」 형태는 없어야 한다.
            Check(dOld.IndexOf(wantProcD + " · 전투당", StringComparison.Ordinal) < 0,
                $"차단하면 필드 이어붙이기(발동% · 전투당)가 없다 — 「{dOld}」");
            Check(dwarf.고유메커니즘.Contains("전투당")
                  ? dOld.Contains("전투당")
                  : dOld.IndexOf("전투당", StringComparison.Ordinal) < 0,
                $"차단 시 문장 속 전투당은 에셋 그대로 — 「{dOld}」");
            Environment.SetEnvironmentVariable(RaceInfo.EnvNo, null);
            Check(!RaceInfo.Blocked && RaceInfo.MechanicLine(RaceId.드워프).Contains(wantProcD + " · 전투당"),
                "차단을 풀면 다시 필드 조각");

            string charSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/CharacterScreen.cs"));
            Check(charSrc.Contains("RaceInfo.MechanicLine"),
                "CharacterScreen이 MechanicLine을 속성 탭에 그린다");

            string raceSrc = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/RaceInfo.cs"));
            Check(raceSrc.Contains("d.전투당발동"),
                "RaceInfo가 d.전투당발동을 읽는다 — 지우면 소비처 0곳으로 되돌아간다");

            _ = nameof(RaceInfo.MechanicLine);
            _ = nameof(RaceDef.전투당발동);
            _ = nameof(CharacterScreen);

            Environment.SetEnvironmentVariable(RaceInfo.EnvNo, no);

            if (_fail == 0) Debug.Log("[RaceBattleCapSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[RaceBattleCapSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException(
                $"[RaceBattleCapSelfCheck] FAIL {_fail}건");
        }
    }
}
