using System;
using System.IO;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 기획 §3.2 숙련 칭호 구간. 원장·검프 줄·페이퍼돌 TitleOf.
    /// NC: NcOff 이면 접두사가 비고 게이트가 실패한다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertSkillTitleRanks()
        {
            if (SkillTitleRanks.NcOff)
                throw new InvalidOperationException("SkillTitleRanks.NcOff 가 켜져 있으면 숙련 칭호가 없습니다.");

            string path = SkillTitleRanks.FullPath;
            if (!File.Exists(path))
                throw new InvalidOperationException("숙련 칭호 원장이 없습니다: " + path);
            SkillTitleRanks.Reload();
            if (Math.Abs(SkillTitleRanks.FileNeophyte - 30f) > 0.01f
                || Math.Abs(SkillTitleRanks.FileNovice - 40f) > 0.01f
                || Math.Abs(SkillTitleRanks.FileApprentice - 50f) > 0.01f
                || Math.Abs(SkillTitleRanks.FileJourneyman - 60f) > 0.01f
                || Math.Abs(SkillTitleRanks.FileExpert - 70f) > 0.01f
                || Math.Abs(SkillTitleRanks.FileAdept - 80f) > 0.01f
                || Math.Abs(SkillTitleRanks.FileMaster - 90f) > 0.01f
                || Math.Abs(SkillTitleRanks.FileGrandmaster - 100f) > 0.01f)
                throw new InvalidOperationException("숙련 칭호 원장 구간이 30/40/50/60/70/80/90/100이 아닙니다.");

            if (SkillTitles.RankOf(29.9f) != "" || SkillTitles.RankOf(30f) != "초심자"
                || SkillTitles.RankOf(40f) != "수습" || SkillTitles.RankOf(50f) != "견습"
                || SkillTitles.RankOf(60f) != "숙련" || SkillTitles.RankOf(70f) != "전문가"
                || SkillTitles.RankOf(80f) != "달인" || SkillTitles.RankOf(90f) != "대가"
                || SkillTitles.RankOf(100f) != "그랜드마스터")
                throw new InvalidOperationException("SkillTitles.RankOf 가 원장 구간과 다릅니다.");

            if (SkillTitles.GumpLine(SkillId.Swordsmanship, 29.9f) != "검술 29.9")
                throw new InvalidOperationException("30 미만 검프는 칭호 없이 검술이어야 합니다: " +
                                                    SkillTitles.GumpLine(SkillId.Swordsmanship, 29.9f));
            if (SkillTitles.GumpLine(SkillId.Swordsmanship, 80f) != "달인 검술 80.0")
                throw new InvalidOperationException("검술 80 검프는 달인 검술 80.0 이어야 합니다: " +
                                                    SkillTitles.GumpLine(SkillId.Swordsmanship, 80f));
            if (SkillTitles.GumpLine(SkillId.Mining, 30f) != "초심자 채광 30.0")
                throw new InvalidOperationException("채광 30 검프는 초심자 채광 30.0 이어야 합니다: " +
                                                    SkillTitles.GumpLine(SkillId.Mining, 30f));

            string titles = File.ReadAllText(Path.Combine(Application.dataPath, "Game/Scripts/Shared/SkillId.cs"));
            if (titles.IndexOf("SkillTitleRanks.Of", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("SkillTitles.RankOf 가 SkillTitleRanks.Of 를 안 부릅니다.");

            string hud = HudSourceText();
            if (hud.IndexOf("SkillTitles.GumpLine", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("스킬 검프가 GumpLine 을 안 씁니다.");
            if (hud.IndexOf("world.TitleOf(me)", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD 직업명이 서버 TitleOf 가 아닙니다.");
            string paper = File.ReadAllText(Path.Combine(Application.dataPath, "Game/Scripts/Client/SliceHud.Paperdoll.cs"));
            if (paper.IndexOf("TitleOf(me)", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("페이퍼돌이 TitleOf 를 안 그립니다.");

            Debug.Log("[Ulon] 숙련 칭호 — 원장 8구간 · 검프 GumpLine · 페이퍼돌 TitleOf");
        }

        static void AssertSkillTitleRanksNegativeControl()
        {
            bool was = SkillTitleRanks.NcOff;
            bool rankRed;
            bool gateRed = false;
            try
            {
                SkillTitleRanks.NcOff = true;
                rankRed = SkillTitles.RankOf(80f) == ""
                          && SkillTitles.GumpLine(SkillId.Swordsmanship, 80f) == "검술 80.0";
                try { AssertSkillTitleRanks(); }
                catch (InvalidOperationException) { gateRed = true; }
            }
            finally { SkillTitleRanks.NcOff = was; }
            if (!rankRed)
                throw new InvalidOperationException("숙련 칭호 NC 실패 — NcOff 인데 달인이 남았습니다.");
            if (!gateRed)
                throw new InvalidOperationException("숙련 칭호 NC 실패 — NcOff 인데 게이트가 통과했습니다.");
            if (SkillTitleRanks.NcOff)
                throw new InvalidOperationException("숙련 칭호 NC 실패 — NcOff 가 켜진 채로 남았습니다.");
            if (SkillTitles.GumpLine(SkillId.Swordsmanship, 80f) != "달인 검술 80.0")
                throw new InvalidOperationException("숙련 칭호 NC 실패 — 끈 뒤 달인 검술이 안 돌아왔습니다.");
            Debug.Log("[Ulon] 숙련 칭호 네거티브 컨트롤 통과 — NcOff 이면 접두사 없음");
        }
    }
}
