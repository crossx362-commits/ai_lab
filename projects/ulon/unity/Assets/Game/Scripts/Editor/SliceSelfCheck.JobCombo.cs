using System;
using System.IO;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 기획 §3.2 복합 직업명. 원장 파일·7행·보조 하한 30·HUD가 서버 TitleOf를 쓰는지.
    /// NC: NcDisable 이면 마검사가 검사로 돌아온다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertJobComboTitles()
        {
            if (SkillJobCombos.NcDisable)
                throw new InvalidOperationException("SkillJobCombos.NcDisable 가 켜져 있으면 조합 직업명이 안 나옵니다.");

            SkillJobCombos.Reload();
            if (!File.Exists(SkillJobCombos.FullPath))
                throw new InvalidOperationException("job_combos.json 이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            if (string.IsNullOrEmpty(SkillJobCombos.LoadedFrom))
                throw new InvalidOperationException("job_combos.json 을 못 읽었습니다: " + SkillJobCombos.LoadError);
            if (Math.Abs(SkillJobCombos.MinSecondary - SkillJobCombos.DefaultMinSecondary) > 0.0001f)
                throw new InvalidOperationException("보조 하한은 §3.2 초심자 선 " + SkillJobCombos.DefaultMinSecondary +
                                                    " 이어야 합니다. 지금 " + SkillJobCombos.MinSecondary);
            if (SkillJobCombos.Count < 7)
                throw new InvalidOperationException("기획서 확장 조합 7행이 원장에 없습니다. 지금 " + SkillJobCombos.Count);

            MustHave(SkillId.Swordsmanship, SkillId.Magery, "마검사");
            MustHave(SkillId.Magery, SkillId.Swordsmanship, "마검사");
            MustHave(SkillId.Archery, SkillId.Tracking, "레인저");
            MustHave(SkillId.Healing, SkillId.Magery, "성직자");
            MustHave(SkillId.Mining, SkillId.Blacksmithing, "광물 장인");
            MustHave(SkillId.Blacksmithing, SkillId.Mining, "무기 장인");
            MustHave(SkillId.AnimalTaming, SkillId.AnimalLore, "야수조련사");

            Expect("달인 마검사", SkillId.Swordsmanship, 80f, SkillId.Magery, 50f);
            Expect("달인 검사", SkillId.Swordsmanship, 80f, SkillId.Magery, 29.9f);
            Expect("달인 마검사", SkillId.Magery, 80f, SkillId.Swordsmanship, 50f);
            Expect("전문가 레인저", SkillId.Archery, 70f, SkillId.Tracking, 30f);
            Expect("숙련 성직자", SkillId.Healing, 60f, SkillId.Magery, 30f);
            Expect("견습 광물 장인", SkillId.Mining, 50f, SkillId.Blacksmithing, 40f);
            Expect("견습 무기 장인", SkillId.Blacksmithing, 50f, SkillId.Mining, 40f);
            Expect("전문가 야수조련사", SkillId.AnimalTaming, 70f, SkillId.AnimalLore, 30f);
            Expect("전문가 궁수", SkillId.Archery, 70f, SkillId.Mining, 40f);

            string titles = File.ReadAllText(Path.Combine(Application.dataPath, "Game/Scripts/Shared/SkillId.cs"));
            if (titles.IndexOf("SkillJobCombos.JobOf", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("SkillTitles.Of 가 SkillJobCombos.JobOf 를 안 부릅니다.");
            string hud = HudSourceText();
            if (hud.IndexOf("world.TitleOf(me)", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD 직업명이 서버 TitleOf 가 아닙니다.");

            Debug.Log("[Ulon] 복합 직업명 — 원장 7행·보조 " + SkillJobCombos.MinSecondary + " · 마검사/레인저");
        }

        static void MustHave(SkillId primary, SkillId secondary, string job)
        {
            if (!SkillJobCombos.Has(primary, secondary, job))
                throw new InvalidOperationException("원장에 " + primary + "+" + secondary + " → " + job + " 이 없습니다.");
        }

        static void Expect(string want, SkillId a, float av, SkillId b, float bv)
        {
            var set = new SkillSet();
            set.ForceSet(a, av, SkillLock.Up);
            set.ForceSet(b, bv, SkillLock.Up);
            string got = SkillTitles.Of(set);
            if (got != want)
                throw new InvalidOperationException(a + "=" + av + " " + b + "=" + bv + " 는 " + want + " 여야 하는데 " + got);
        }

        static void AssertJobComboTitlesNegativeControl()
        {
            bool was = SkillJobCombos.NcDisable;
            bool titleRed;
            bool gateRed = false;
            try
            {
                SkillJobCombos.NcDisable = true;
                var set = new SkillSet();
                set.ForceSet(SkillId.Swordsmanship, 80f, SkillLock.Up);
                set.ForceSet(SkillId.Magery, 50f, SkillLock.Up);
                titleRed = SkillTitles.Of(set) == "달인 검사";
                try { AssertJobComboTitles(); }
                catch (InvalidOperationException) { gateRed = true; }
            }
            finally { SkillJobCombos.NcDisable = was; }
            if (!titleRed)
                throw new InvalidOperationException("복합 직업명 NC 실패 — NcDisable 인데 마검사가 남았습니다.");
            if (!gateRed)
                throw new InvalidOperationException("복합 직업명 NC 실패 — NcDisable 인데 게이트가 통과했습니다.");
            if (SkillJobCombos.NcDisable)
                throw new InvalidOperationException("복합 직업명 NC 실패 — NcDisable 이 켜진 채로 남았습니다.");
            Expect("달인 마검사", SkillId.Swordsmanship, 80f, SkillId.Magery, 50f);
            Debug.Log("[Ulon] 복합 직업명 네거티브 컨트롤 통과 — NcDisable 이면 달인 검사");
        }
    }
}
