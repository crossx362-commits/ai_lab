using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    public static partial class OutdoorCensus
    {
        /// <summary>
        /// **밭이 비탈에 걸쳐 있나**(`18_meadow` 화면, 2026-09-09).
        ///
        /// 농경지 네 뙈기는 지역 중심에서 **고정 좌표**로 놓인다 — 그 자리 지형이 비탈이어도 그대로 앉는다.
        /// 화면에서는 울타리와 이랑이 언덕을 타고 넘어 「경작지」가 아니라 「비탈에 친 울타리」로 읽힌다.
        ///
        /// 재는 것: 뙈기마다 지형 **고저차**(최고−최저)와 평균·최대 경사. 대조군으로 지역 안에서
        /// 가장 평평한 자리(같은 크기 창을 훑어 고저차 최소)를 같이 찍는다 — 옮길 데가 있는지 먼저 본다.
        /// </summary>
        public static void RunFieldFlatness()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != scenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            var r = WorldRegions.Meadow;
            var plots = new[]
            {
                new Vector2(r.X - 11f, r.Z - 9f), new Vector2(r.X + 10f, r.Z - 10f),
                new Vector2(r.X - 10f, r.Z + 10f), new Vector2(r.X + 11f, r.Z + 9f),
            };
            float[] halves = { 7.5f, 5.5f, 6.0f, 8.0f };
            Debug.Log("[Census] 농경지 뙈기 평탄도 — 고저차·경사");
            for (int i = 0; i < plots.Length; i++)
            {
                Flatness("뙈기 " + (i + 1) + " (반폭 " + halves[i] + "m)", plots[i].x, plots[i].y, halves[i]);
            }
            // 대조군 — 지역 안에서 같은 크기로 가장 평평한 자리.
            for (int i = 0; i < plots.Length; i++)
            {
                float half = halves[i];
                float bestSpread = float.MaxValue, bx = plots[i].x, bz = plots[i].y;
                for (float x = r.X - r.Radius + half; x <= r.X + r.Radius - half; x += 2f)
                for (float z = r.Z - r.Radius + half; z <= r.Z + r.Radius - half; z += 2f)
                {
                    float spread = Spread(x, z, half);
                    if (spread < bestSpread) { bestSpread = spread; bx = x; bz = z; }
                }
                Debug.Log("[Census] 뙈기 " + (i + 1) + " 가장 평평한 자리 (" + bx.ToString("0") + "," + bz.ToString("0") +
                          ") 고저차 " + bestSpread.ToString("0.0") + "m (지금 자리 대비)");
            }
            if (Application.isBatchMode)
                UnityEditor.EditorApplication.Exit(0);
        }

        static float Spread(float cx, float cz, float half)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (float x = cx - half; x <= cx + half; x += 1f)
            for (float z = cz - half; z <= cz + half; z += 1f)
            {
                float h = WorldTerrain.HeightAt(x, z);
                if (h < lo) lo = h;
                if (h > hi) hi = h;
            }
            return hi - lo;
        }

        static void Flatness(string tag, float cx, float cz, float half)
        {
            float lo = float.MaxValue, hi = float.MinValue, slope = 0f, worst = 0f;
            int n = 0;
            for (float x = cx - half; x <= cx + half; x += 1f)
            for (float z = cz - half; z <= cz + half; z += 1f)
            {
                float h = WorldTerrain.HeightAt(x, z);
                if (h < lo) lo = h;
                if (h > hi) hi = h;
                float deg = Mathf.Atan(WorldSplat.MacroSlopeTan(x, z)) * Mathf.Rad2Deg;
                slope += deg;
                if (deg > worst) worst = deg;
                n++;
            }
            Debug.Log("[Census] " + tag + " — 고저차 " + (hi - lo).ToString("0.0") + "m · 평균 경사 " +
                      (slope / n).ToString("0.0") + "° · 최급 " + worst.ToString("0.0") + "°");
        }
    }
}
