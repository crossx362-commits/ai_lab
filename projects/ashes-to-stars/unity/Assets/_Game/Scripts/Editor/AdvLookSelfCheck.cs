using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 기획서 §3 1차 전직이 그림을 어디서 가져오는지 검사한다.
    /// 오너 지시(2026-08-18, f23da4ca·f753d979)로 **기본은 전직 전 기본 5직업 그림**이다 —
    /// 전직 폴더(guardian 등) 이름 계약만 유지하고, 캐릭터는 전직해도 기본 시트를 쓴다.
    /// 마법사는 기본 직업이라 전용 폴더가 없어도 된다.
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
            for (int i = 0; i < Need.Length; i++)
            {
                Check(UiPages.DedicatedLookDir(Need[i].job) == Need[i].dir,
                    Need[i].job + " 전용 폴더명 계약은 " + Need[i].dir);
                Check(UiPages.BaseLookDir(Need[i].job) == Need[i].baseDir,
                    Need[i].job + " 기본 폴더는 " + Need[i].baseDir);
                Check(UiPages.LookDir(Need[i].job) == Need[i].baseDir
                      && UiPages.LookDir(Need[i].job, AdvancementTier.First) == Need[i].baseDir,
                    Need[i].job + " 도 전직해도 기본 " + Need[i].baseDir + " 그림을 본다(오너 2026-08-18)");
                string path = $"sprites/{Need[i].baseDir}/{Need[i].baseDir}_idle_00";
                Check(Resources.Load<Texture2D>(path) != null, path + " 없음 — 화면이 못 그린다");
            }

            if (fail == 0) Debug.Log("[AdvLookSelfCheck] PASS");
            else Debug.LogError("[AdvLookSelfCheck] FAIL " + fail);
        }
    }
}
