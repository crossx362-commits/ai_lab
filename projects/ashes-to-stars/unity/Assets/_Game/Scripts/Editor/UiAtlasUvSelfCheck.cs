using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 아틀라스 UV는 설계 크기(1448×1086)로 나눈다.
    /// texture.width(NPOT 1024)로 나누면 tower→지구본·heart→깨진 하트.
    /// </summary>
    public static class UiAtlasUvSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Ui Atlas Uv Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(UiAtlas.EnvShow);
            Environment.SetEnvironmentVariable(UiAtlas.EnvShow, null);
            UiAtlas.ResetForTest();

            Check(UiAtlas.Width == 1448 && UiAtlas.Height == 1086,
                $"설계 크기 {UiAtlas.Width}×{UiAtlas.Height}");

            var heart = UiAtlas.RectFor("heart");
            var tower = UiAtlas.RectFor("tower");
            var broken = UiAtlas.RectFor("heart_broken");
            var heartUv = UiAtlas.UvOf("heart");
            var towerUv = UiAtlas.UvOf("tower");
            var brokenUv = UiAtlas.UvOf("heart_broken");

            Check(Approx(heartUv.x, heart.x / UiAtlas.Width),
                $"heart UV.x {heartUv.x:0.0000} = {heart.x}/{UiAtlas.Width}");
            Check(Approx(towerUv.x, tower.x / UiAtlas.Width),
                $"tower UV.x {towerUv.x:0.0000} = {tower.x}/{UiAtlas.Width}");
            Check(Approx(brokenUv.x, broken.x / UiAtlas.Width),
                $"heart_broken UV.x {brokenUv.x:0.0000} = {broken.x}/{UiAtlas.Width}");

            // 옛 식: 임포트 폭으로 나눔. 1024면 tower 0.268 → 지구본.
            float imported = 1024f;
            Check(!Approx(towerUv.x, tower.x / imported),
                $"tower UV가 옛 {tower.x}/1024={tower.x / imported:0.0000}가 아니다");
            Check(!Approx(heartUv.x, heart.x / imported),
                $"heart UV가 옛 {heart.x}/1024={heart.x / imported:0.0000}가 아니다");
            Check(towerUv.x + 1e-4f < 407f / UiAtlas.Width,
                $"tower UV가 지구본({407f / UiAtlas.Width:0.0000})보다 왼쪽");
            Check(heartUv.xMax + 1e-4f < broken.x / UiAtlas.Width,
                $"heart UV 오른쪽이 깨진 하트({broken.x / UiAtlas.Width:0.0000})를 안 문다");

            var chrome = UiAtlas.UvOf("button_normal");
            Check(chrome.width > 0.99f && chrome.height > 0.99f,
                $"크롬 솔로 UV는 자기 장 {chrome.width:0.00}×{chrome.height:0.00}");

            Check(UiAtlas.Line().Contains("이웃"), $"줄 (실제 {UiAtlas.Line()})");
            Environment.SetEnvironmentVariable(UiAtlas.EnvShow, "1");
            Check(UiAtlas.ShowQa, "QA 켜짐");
            Environment.SetEnvironmentVariable(UiAtlas.EnvShow, null);
            UiAtlas.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string atlas = File.ReadAllText(Path.Combine(runtime, "UiAtlas.cs"));
            string field = File.ReadAllText(Path.Combine(runtime, "FieldScreen.cs"));
            Check(atlas.Contains("ReferenceEquals(texture, Texture)"),
                "TextureCoords가 아틀라스를 설계 크기로 가른다");
            Check(atlas.Contains("atlas || texture == null ? Width"),
                "아틀라스 UV가 Width를 읽는다");
            Check(field.Contains("UiAtlas.Line"), "필드 자막이 Line을 읽는다");
            Check(field.Contains("UiAtlas.SeedQaIfRequested"), "필드가 시드를 읽는다");

            Environment.SetEnvironmentVariable(UiAtlas.EnvShow, show);
            if (_fail == 0) Debug.Log("[UiAtlasUvSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[UiAtlasUvSelfCheck] FAIL {_fail}건\n" + _log);
            if (Application.isBatchMode && _fail > 0)
                EditorApplication.Exit(1);
        }

        static bool Approx(float a, float b) => Mathf.Abs(a - b) < 1e-4f;
    }
}
