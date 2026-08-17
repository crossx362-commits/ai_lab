using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 클로드가 그린 할로우 배경 6장이 화면이 읽는 자리에 있는지 검사한다.
    /// PNG는 이 검사가 고치지 않는다 — 빈 자리만 말한다.
    /// </summary>
    public static class HollowBgSelfCheck
    {
        static readonly string[] Keys =
        {
            "bg_estate", "bg_field", "bg_tower",
            "bg_worldmap", "bg_character", "bg_party",
        };

        [MenuItem("Ashes to Stars/QA/Hollow Bg Self Check")]
        public static void Run()
        {
            int fail = 0;
            void Check(bool cond, string what)
            {
                if (!cond)
                {
                    fail++;
                    Debug.LogError("[HollowBgSelfCheck] FAIL  " + what);
                }
            }

            for (int i = 0; i < Keys.Length; i++)
            {
                var tex = Resources.Load<Texture2D>("bg/" + Keys[i]);
                Check(tex != null, Keys[i] + " 없음");
                if (tex == null) continue;
                Check(tex.width >= 512 && tex.height >= 512,
                    Keys[i] + " 너무 작다 " + tex.width + "x" + tex.height);
                Check(tex.filterMode == FilterMode.Bilinear,
                    Keys[i] + " 필터가 Point라 손그림에 계단이 생긴다");
            }

            if (fail == 0) Debug.Log("[HollowBgSelfCheck] PASS");
            else Debug.LogError("[HollowBgSelfCheck] FAIL " + fail);
        }
    }
}
