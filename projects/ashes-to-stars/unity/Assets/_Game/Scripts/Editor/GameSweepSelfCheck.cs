using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>게임 주요 화면·전투·HUD 자가검사를 한 번에 돌리고 보드에 남긴다.</summary>
    public static class GameSweepSelfCheck
    {
        struct Row
        {
            public string Name;
            public bool Ok;
            public string Note;
        }

        [MenuItem("Ashes to Stars/QA/Game Sweep Self Check")]
        public static void Run()
        {
            var rows = new List<Row>();
            var errors = new List<string>();
            Application.LogCallback hook = (msg, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                    errors.Add(msg);
            };
            Application.logMessageReceived += hook;
            try
            {
                One("사냥 강화 3택", HuntBoonSelfCheck.Run, rows, errors);
                One("전투 오디오 문법", SfxSelfCheck.Run, rows, errors);
                One("전투 HUD", CombatHudSelfCheck.Run, rows, errors);
                One("계열 상성", FamilyAdvSelfCheck.Run, rows, errors);
                One("UI 아틀라스·카드 여백", UiAtlasSelfCheck.Run, rows, errors);
                One("필드 허브 HUD", FieldHudSelfCheck.Run, rows, errors);
                One("탑 허브 HUD", TowerHudSelfCheck.Run, rows, errors);
                One("월드맵 HUD", WorldMapHudSelfCheck.Run, rows, errors);
                One("파티 출전 HUD", PartyHudSelfCheck.Run, rows, errors);
                One("파티 편성 HUD", PartyFormHudSelfCheck.Run, rows, errors);
                One("사냥 편성 HUD", HuntPickHudSelfCheck.Run, rows, errors);
                One("필드 경고 HUD", FieldWarnHudSelfCheck.Run, rows, errors);
                One("탑 경고 HUD", TowerWarnHudSelfCheck.Run, rows, errors);
                One("월드맵 안개", WorldExploreSelfCheck.Run, rows, errors);
                One("영지 마을 HUD", EstateHudSelfCheck.Run, rows, errors);
                One("영지 현황 HUD", EstateStatusHudSelfCheck.Run, rows, errors);
                One("허브 제목판", HubHeaderSelfCheck.Run, rows, errors);
                One("사냥 시작 두 단계", HuntStartSelfCheck.Run, rows, errors);
                One("스킬 자동/수동", SkillUseSelfCheck.Run, rows, errors);
                One("시작 직업 선택", StarterPickSelfCheck.Run, rows, errors);
                One("캐릭터 명부", CharacterRosterSelfCheck.Run, rows, errors);
                One("캐릭터 3열·장비 라벨", CharHudSelfCheck.Run, rows, errors);
                One("전투 스타일 HUD", StyleHudSelfCheck.Run, rows, errors);
                One("본문 내비 절단", BodyNavSelfCheck.Run, rows, errors);
                One("부활초 소지 상한", ReviveCapSelfCheck.Run, rows, errors);
                One("사망 상한", DeathCapSelfCheck.Run, rows, errors);
                One("PvP 회복", PvpRecoverSelfCheck.Run, rows, errors);
                One("PvE 회복", PveRecoverSelfCheck.Run, rows, errors);
                One("잡몹 상한", PerfCapSelfCheck.Run, rows, errors);
                One("소환수 상한", SummonCapSelfCheck.Run, rows, errors);
                One("투사체 상한", ProjCapSelfCheck.Run, rows, errors);
                One("G/h 앵커", GhAnchorSelfCheck.Run, rows, errors);
                One("티어 배율", TierMulSelfCheck.Run, rows, errors);
                One("소각 목표", BurnTargetSelfCheck.Run, rows, errors);
                One("플레이어 이동속도", MoveSpdSelfCheck.Run, rows, errors);
                One("잡몹 이동속도", MobSpeedSelfCheck.Run, rows, errors);
                One("잡몹 HP", MobHpSelfCheck.Run, rows, errors);
                One("잡몹 피해", MobDmgSelfCheck.Run, rows, errors);
                One("원거리 유지거리", MobRangedDistanceSelfCheck.Run, rows, errors);
                One("근접 공격 주기", MobMeleeCadenceSelfCheck.Run, rows, errors);
                One("원거리 발사 주기", MobShotCadenceSelfCheck.Run, rows, errors);
                One("원거리 투사체 속도", MobProjectileSpeedSelfCheck.Run, rows, errors);
                One("초상 아틀라스", PortraitAtlasSelfCheck.Run, rows, errors);
                One("아이템 아틀라스", ItemAtlasSelfCheck.Run, rows, errors);
                One("영지 격자", EstateGridSelfCheck.Run, rows, errors);
                One("영지 창고 경로", EstateStoreSelfCheck.Run, rows, errors);
                One("영지 자리", EstateFootprintSelfCheck.Run, rows, errors);
                One("영지 마당", EstateYardSelfCheck.Run, rows, errors);
                One("캐릭터 겹침", UnitSeparationSelfCheck.Run, rows, errors);
                One("집·나무 겹침", FieldDecorOverlapSelfCheck.Run, rows, errors);
                One("집 돌아나가기", ArenaLayoutSelfCheck.Run, rows, errors);
                One("길 한가운데 금지", FieldDecorRoadSelfCheck.Run, rows, errors);
                One("로컬 테스트 시드", LocalPlayKitSelfCheck.Run, rows, errors);
                One("사냥 경험치", HuntExpSelfCheck.Run, rows, errors);
                One("보스 사망 애니", BossDeathAnimSelfCheck.Run, rows, errors);
                One("직업 애니 13장", JobAnimSelfCheck.Run, rows, errors);
                One("할로우 배경 6장", HollowBgSelfCheck.Run, rows, errors);
                One("전직 11종 모습", AdvLookSelfCheck.Run, rows, errors);
                One("필드·던전 바닥", GroundHollowSelfCheck.Run, rows, errors);
            }
            finally
            {
                Application.logMessageReceived -= hook;
                HuntBoon.End();
            }

            int fail = 0;
            for (int i = 0; i < rows.Count; i++)
                if (!rows[i].Ok) fail++;
            WriteReport(rows, fail);
            if (fail == 0) Debug.Log($"[GameSweepSelfCheck] PASS {rows.Count}/{rows.Count}");
            else Debug.LogError($"[GameSweepSelfCheck] FAIL {fail}/{rows.Count}");
        }

        static void One(string name, Action run, List<Row> rows, List<string> errors)
        {
            errors.Clear();
            try { run(); }
            catch (Exception e) { errors.Add(e.Message); }
            bool ok = true;
            string note = "통과";
            for (int i = 0; i < errors.Count; i++)
            {
                if (errors[i].IndexOf("FAIL", StringComparison.OrdinalIgnoreCase) >= 0
                    || errors[i].IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0
                    || errors[i].IndexOf("Assert", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ok = false;
                    note = errors[i];
                    if (note.Length > 160) note = note.Substring(0, 160);
                    break;
                }
            }
            rows.Add(new Row { Name = name, Ok = ok, Note = note });
        }

        static void WriteReport(List<Row> rows, int fail)
        {
            string root = FindRoot();
            if (string.IsNullOrEmpty(root))
            {
                Debug.LogWarning("[GameSweepSelfCheck] 저장소 루트를 못 찾아 JSON을 못 씀");
                return;
            }
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"at\": \"").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("\",\n");
            sb.Append("  \"ok\": ").Append(fail == 0 ? "true" : "false").Append(",\n");
            sb.Append("  \"summary\": \"").Append(fail == 0
                ? rows.Count + "개 전부 통과"
                : fail + "개 실패 / " + rows.Count).Append("\",\n");
            sb.Append("  \"items\": [\n");
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                sb.Append("    {\"name\":\"").Append(Esc(r.Name))
                  .Append("\",\"ok\":").Append(r.Ok ? "true" : "false")
                  .Append(",\"note\":\"").Append(Esc(r.Note)).Append("\"}");
                sb.Append(i + 1 < rows.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            File.WriteAllText(Path.Combine(root, "loop", "last_test_report.json"),
                sb.ToString(), Encoding.UTF8);
        }

        static string FindRoot()
        {
            var d = new DirectoryInfo(Application.dataPath);
            while (d != null)
            {
                if (File.Exists(Path.Combine(d.FullName, "loop", "board.py")))
                    return d.FullName;
                d = d.Parent;
            }
            return null;
        }

        static string Esc(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
    }
}
