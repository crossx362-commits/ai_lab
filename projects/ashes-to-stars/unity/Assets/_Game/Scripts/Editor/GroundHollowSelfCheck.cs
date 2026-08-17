using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>필드·던전 바닥이 할로우 타일인지, 런타임이 Point로 도로 뭉개지 않는지.</summary>
    public static class GroundHollowSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Ground Hollow Self Check")]
        public static void Run()
        {
            int fail = 0;
            void Check(bool cond, string what)
            {
                if (!cond)
                {
                    fail++;
                    Debug.LogError("[GroundHollowSelfCheck] FAIL  " + what);
                }
            }

            string src = System.IO.File.ReadAllText(
                "Assets/Scripts/GroundBuilder.cs");
            Check(src.Contains("FilterMode.Bilinear"),
                "GroundBuilder가 바닥을 Point로 강제한다");
            Check(!src.Contains("FilterMode.Point"),
                "GroundBuilder에 Point가 남아 있다");

            foreach (var key in new[] { "ground/field_plain_albedo", "ground/dungeon_rock_albedo" })
            {
                var tex = Resources.Load<Texture2D>(key);
                Check(tex != null, key + " 없음");
                if (tex == null) continue;
                Check(tex.width >= 256 && tex.height >= 256,
                    key + " 너무 작다 " + tex.width + "x" + tex.height);
            }

            if (fail == 0) Debug.Log("[GroundHollowSelfCheck] PASS");
            else Debug.LogError("[GroundHollowSelfCheck] FAIL " + fail);
        }
    }
}
