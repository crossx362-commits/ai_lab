using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 기획서 §3 1차 전직 11종이 기본 5장과 같은 그림을 쓰는지 검사한다.
    /// 마법사는 기본 직업이라 전용 폴더가 없어도 된다.
    /// </summary>
    public static class AdvLookSelfCheck
    {
        static readonly (string job, string dir)[] Need =
        {
            ("수호기사", "guardian"), ("광전사", "berserker"),
            ("검사", "swordsman"), ("궁수", "archer"), ("소환사", "summoner"),
            ("사제", "priest"), ("드루이드", "druid"),
            ("음유시인", "bard"), ("주술사", "shaman"), ("정령사", "elemental"),
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
            Check(UiPages.DedicatedLookDir("수호기사") == "guardian", "수호기사 전용 폴더명");
            for (int i = 0; i < Need.Length; i++)
            {
                string path = $"sprites/{Need[i].dir}/{Need[i].dir}_idle_00";
                var tex = Resources.Load<Texture2D>(path);
                Check(tex != null, path + " 없음 — 전직이 기본형과 같은 그림");
                if (tex == null) continue;
                Check(UiPages.LookDir(Need[i].job) == Need[i].dir,
                    Need[i].job + " 이 " + UiPages.LookDir(Need[i].job) + " 를 본다");
                string baseDir = UiPages.BaseLookDir(Need[i].job);
                var baseTex = Resources.Load<Texture2D>(
                    $"sprites/{baseDir}/{baseDir}_idle_00");
                Check(baseTex == null || tex != baseTex,
                    Need[i].job + " 전용이 기본 " + baseDir + " 과 같다");
            }

            if (fail == 0) Debug.Log("[AdvLookSelfCheck] PASS");
            else Debug.LogError("[AdvLookSelfCheck] FAIL " + fail);
        }
    }
}
