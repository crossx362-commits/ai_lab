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
        static bool HasLandscapeScreenShape(int width, int height)
        {
            if (width <= 0 || height <= 0) return false;
            float aspect = width / (float)height;
            return aspect >= 1.75f && aspect <= 1.80f;
        }

        static readonly string[] Keys =
        {
            "bg_estate", "bg_field", "bg_tower",
            "bg_worldmap", "bg_character", "bg_party",
            "bg_title", "bg_dungeon", "bg_result",
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

            var character = Resources.Load<Texture2D>("bg/bg_character");
            int sourceWidth = 0;
            int sourceHeight = 0;
            if (character != null)
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(character)) as TextureImporter;
                importer?.GetSourceTextureWidthAndHeight(out sourceWidth, out sourceHeight);
            }
            Check(HasLandscapeScreenShape(sourceWidth, sourceHeight),
                "캐릭터 배경 원본은 16:9 가로 화면이어야 한다 (실제 "
                + sourceWidth + "x" + sourceHeight + ")");
            Check(!HasLandscapeScreenShape(800, 800),
                "정사각형 캐릭터 배경 대표 결함을 거부한다");

            var party = Resources.Load<Texture2D>("bg/bg_party");
            sourceWidth = 0;
            sourceHeight = 0;
            if (party != null)
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(party)) as TextureImporter;
                importer?.GetSourceTextureWidthAndHeight(out sourceWidth, out sourceHeight);
            }
            Check(HasLandscapeScreenShape(sourceWidth, sourceHeight),
                "파티·전투 스타일 공용 배경 원본은 16:9 가로 화면이어야 한다 (실제 "
                + sourceWidth + "x" + sourceHeight + ")");
            Check(!HasLandscapeScreenShape(720, 1280),
                "세로형 파티 배경 대표 결함을 거부한다");

            var worldmap = Resources.Load<Texture2D>("bg/bg_worldmap");
            sourceWidth = 0;
            sourceHeight = 0;
            if (worldmap != null)
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(worldmap)) as TextureImporter;
                importer?.GetSourceTextureWidthAndHeight(out sourceWidth, out sourceHeight);
            }
            Check(HasLandscapeScreenShape(sourceWidth, sourceHeight),
                "월드맵 배경 원본은 16:9 가로 화면이어야 한다 (실제 "
                + sourceWidth + "x" + sourceHeight + ")");
            Check(!HasLandscapeScreenShape(1280, 800),
                "16:10 월드맵 배경 대표 결함을 거부한다");

            if (fail == 0) Debug.Log("[HollowBgSelfCheck] PASS");
            else Debug.LogError("[HollowBgSelfCheck] FAIL " + fail);
        }
    }
}
