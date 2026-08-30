using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
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

        static void GetSourceSize(Texture2D texture, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (texture == null) return;

            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
            importer?.GetSourceTextureWidthAndHeight(out width, out height);
        }

        static readonly string[] Keys =
        {
            "bg_estate", "bg_field", "bg_tower",
            "bg_worldmap", "bg_character", "bg_party",
            "bg_title", "bg_dungeon", "bg_result",
        };

        static HashSet<string> RuntimeBackgroundKeys()
        {
            var keys = new HashSet<string>();
            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            foreach (string path in Directory.GetFiles(runtime, "*.cs", SearchOption.TopDirectoryOnly))
            {
                string source = File.ReadAllText(path);
                foreach (Match match in Regex.Matches(source,
                    "BackgroundArt\\s*=>\\s*\\\"([^\\\"]+)\\\""))
                    keys.Add(match.Groups[1].Value);
            }
            return keys;
        }

        static bool CoversSameKeys(IEnumerable<string> consumers, IEnumerable<string> checkedKeys)
        {
            return new HashSet<string>(consumers).SetEquals(checkedKeys);
        }

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

            Check(HasLandscapeScreenShape(1750, 1000),
                "16:9 화면 비율 하한 1.75를 포함해야 한다");
            Check(HasLandscapeScreenShape(1800, 1000),
                "16:9 화면 비율 상한 1.80을 포함해야 한다");
            Check(!HasLandscapeScreenShape(1749, 1000),
                "16:9 화면 비율 하한 바로 아래 1.749를 거부한다");
            Check(!HasLandscapeScreenShape(1801, 1000),
                "16:9 화면 비율 상한 바로 위 1.801을 거부한다");

            var runtimeKeys = RuntimeBackgroundKeys();
            Check(CoversSameKeys(runtimeKeys, Keys),
                "화면 BackgroundArt 실소비 키와 배경 검사 키가 같아야 한다 (실소비 "
                + string.Join(", ", runtimeKeys) + ")");
            Check(!CoversSameKeys(new[] { "bg_field", "bg_unchecked" }, new[] { "bg_field" }),
                "새 화면 배경이 검사 목록에서 빠진 대표 결함을 거부한다");

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
            GetSourceSize(character, out int sourceWidth, out int sourceHeight);
            Check(HasLandscapeScreenShape(sourceWidth, sourceHeight),
                "캐릭터 배경 원본은 16:9 가로 화면이어야 한다 (실제 "
                + sourceWidth + "x" + sourceHeight + ")");
            Check(!HasLandscapeScreenShape(800, 800),
                "정사각형 캐릭터 배경 대표 결함을 거부한다");

            var party = Resources.Load<Texture2D>("bg/bg_party");
            GetSourceSize(party, out sourceWidth, out sourceHeight);
            Check(HasLandscapeScreenShape(sourceWidth, sourceHeight),
                "파티·전투 스타일 공용 배경 원본은 16:9 가로 화면이어야 한다 (실제 "
                + sourceWidth + "x" + sourceHeight + ")");
            Check(!HasLandscapeScreenShape(720, 1280),
                "세로형 파티 배경 대표 결함을 거부한다");

            var worldmap = Resources.Load<Texture2D>("bg/bg_worldmap");
            GetSourceSize(worldmap, out sourceWidth, out sourceHeight);
            Check(HasLandscapeScreenShape(sourceWidth, sourceHeight),
                "월드맵 배경 원본은 16:9 가로 화면이어야 한다 (실제 "
                + sourceWidth + "x" + sourceHeight + ")");
            Check(!HasLandscapeScreenShape(1280, 800),
                "16:10 월드맵 배경 대표 결함을 거부한다");

            var title = Resources.Load<Texture2D>("bg/bg_title");
            GetSourceSize(title, out sourceWidth, out sourceHeight);
            Check(HasLandscapeScreenShape(sourceWidth, sourceHeight),
                "타이틀 배경 원본은 16:9 가로 화면이어야 한다 (실제 "
                + sourceWidth + "x" + sourceHeight + ")");
            Check(!HasLandscapeScreenShape(1024, 768),
                "4:3 타이틀 배경 대표 결함을 거부한다");

            var dungeon = Resources.Load<Texture2D>("bg/bg_dungeon");
            GetSourceSize(dungeon, out sourceWidth, out sourceHeight);
            Check(HasLandscapeScreenShape(sourceWidth, sourceHeight),
                "던전 노드맵 배경 원본은 16:9 가로 화면이어야 한다 (실제 "
                + sourceWidth + "x" + sourceHeight + ")");
            Check(!HasLandscapeScreenShape(1200, 800),
                "3:2 던전 노드맵 배경 대표 결함을 거부한다");

            var result = Resources.Load<Texture2D>("bg/bg_result");
            GetSourceSize(result, out sourceWidth, out sourceHeight);
            Check(HasLandscapeScreenShape(sourceWidth, sourceHeight),
                "전투 결과 배경 원본은 16:9 가로 화면이어야 한다 (실제 "
                + sourceWidth + "x" + sourceHeight + ")");
            Check(!HasLandscapeScreenShape(1600, 1200),
                "4:3 전투 결과 배경 대표 결함을 거부한다");

            var estate = Resources.Load<Texture2D>("bg/bg_estate");
            GetSourceSize(estate, out sourceWidth, out sourceHeight);
            Check(HasLandscapeScreenShape(sourceWidth, sourceHeight),
                "영지 허브 배경 원본은 16:9 가로 화면이어야 한다 (실제 "
                + sourceWidth + "x" + sourceHeight + ")");
            Check(!HasLandscapeScreenShape(1200, 1200),
                "정사각형 영지 허브 배경 대표 결함을 거부한다");

            var field = Resources.Load<Texture2D>("bg/bg_field");
            GetSourceSize(field, out sourceWidth, out sourceHeight);
            Check(HasLandscapeScreenShape(sourceWidth, sourceHeight),
                "필드 자동사냥 배경 원본은 16:9 가로 화면이어야 한다 (실제 "
                + sourceWidth + "x" + sourceHeight + ")");
            Check(!HasLandscapeScreenShape(1024, 1024),
                "정사각형 필드 자동사냥 배경 대표 결함을 거부한다");

            var tower = Resources.Load<Texture2D>("bg/bg_tower");
            GetSourceSize(tower, out sourceWidth, out sourceHeight);
            Check(HasLandscapeScreenShape(sourceWidth, sourceHeight),
                "탑 등반 배경 원본은 16:9 가로 화면이어야 한다 (실제 "
                + sourceWidth + "x" + sourceHeight + ")");
            Check(!HasLandscapeScreenShape(1280, 960),
                "4:3 탑 등반 배경 대표 결함을 거부한다");

            if (fail == 0) Debug.Log("[HollowBgSelfCheck] PASS");
            else Debug.LogError("[HollowBgSelfCheck] FAIL " + fail);
        }
    }
}
