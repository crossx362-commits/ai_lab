using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 기획서 §3 1차 전직이 그림을 어디서 가져오는지 검사한다.
    /// 기본 단계는 5직업 시트, 1차·2차는 전용 폴더(guardian 등). 마법사는 mage.
    /// </summary>
    public static class AdvLookSelfCheck
    {
        static readonly (string job, string dir, string baseDir)[] Need =
        {
            ("수호기사", "guardian", "tank"), ("광전사", "berserker", "tank"),
            ("검사", "swordsman", "dps"), ("궁수", "archer", "dps"), ("소환사", "summoner", "mage"),
            ("사제", "priest", "healer"), ("드루이드", "druid", "healer"),
            ("음유시인", "bard", "buffer"), ("주술사", "shaman", "buffer"), ("정령사", "elemental", "buffer"),
        };

        [MenuItem("Ashes to Stars/QA/Adv Look Self Check")]
        public static void Run()
        {
            int fail = 0;
            void Check(bool cond, string what)
            {
                if (!cond)
                {
                    fail++;
                    Debug.LogError("[AdvLookSelfCheck] FAIL  " + what);
                }
            }

            Check(UiPages.LookDir("마법사") == "mage", "마법사는 기본 mage");
            Check(UiPages.LookDir("탱") == "tank", "기본 탱은 tank");
            Check(UiPages.DedicatedLookDir("사신") == "reaper"
                  && UiPages.DedicatedLookDir("성기사") == "paladin"
                  && UiPages.DedicatedLookDir("시간술사") == "chrono"
                  && UiPages.DedicatedLookDir("용기사") == "dragonknight",
                "특수 4종이 전용 폴더를 갖는다");
            Check(Resources.Load<Texture2D>("sprites/reaper/reaper_idle_00") != null
                  && Resources.Load<Texture2D>("sprites/paladin/paladin_idle_00") != null
                  && Resources.Load<Texture2D>("sprites/chrono/chrono_idle_00") != null
                  && Resources.Load<Texture2D>("sprites/dragonknight/dragonknight_idle_00") != null,
                "특수 4종 idle이 Resources에 있다");
            for (int i = 0; i < Need.Length; i++)
            {
                Check(UiPages.DedicatedLookDir(Need[i].job) == Need[i].dir,
                    Need[i].job + " 전용 폴더명 계약은 " + Need[i].dir);
                Check(UiPages.BaseLookDir(Need[i].job) == Need[i].baseDir,
                    Need[i].job + " 기본 폴더는 " + Need[i].baseDir);
                Check(UiPages.LookDir(Need[i].job) == Need[i].baseDir,
                    Need[i].job + " 티어 없는 호출은 기본 " + Need[i].baseDir);
                Check(UiPages.LookDir(Need[i].job, AdvancementTier.Basic) == Need[i].baseDir,
                    Need[i].job + " 기본 단계는 " + Need[i].baseDir);
                string dedicated = $"sprites/{Need[i].dir}/{Need[i].dir}_idle_00";
                Check(Resources.Load<Texture2D>(dedicated) != null, dedicated + " 없음 — 전직 그림을 못 그린다");
                Check(UiPages.LookDir(Need[i].job, AdvancementTier.First) == Need[i].dir,
                    Need[i].job + " 1차는 전용 " + Need[i].dir);
            }

            if (fail == 0) Debug.Log("[AdvLookSelfCheck] PASS");
            else Debug.LogError("[AdvLookSelfCheck] FAIL " + fail);
        }
    }
}
