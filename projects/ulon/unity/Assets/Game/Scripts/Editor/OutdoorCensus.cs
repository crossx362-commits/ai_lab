using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Editor
{
    /// <summary>
    /// **세는 도구**(검수 지시 2026-09-09 — 「먼저 세십시오」). 야외 소품이 몇 개이고 어디 있고
    /// 서로 물려 있는지만 로그로 내놓는다. **아무것도 고치지 않고 아무것도 판정하지 않는다** —
    /// 게이트는 세고 난 뒤에 만든다(무엇을 재야 하는지 모르는 채 만든 자는 엉뚱한 것을 잰다).
    /// </summary>
    public static class OutdoorCensus
    {
        [UnityEditor.MenuItem("Ulon/Count Outdoor Props")]
        public static void RunMenu() { Run(); }

        public static void Run()
        {
            const string scenePath = "Assets/Game/Scenes/Bootstrap.unity";
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != scenePath)
                EditorSceneManager.OpenScene(scenePath);

            foreach (var wb in UnityEngine.Object.FindObjectsByType<Ulon.Server.WorldBody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (GroundFit.WorldBounds(wb.transform, out Bounds ab))
                    Debug.Log("[Census] 사람·짐승 " + wb.name + " @(" + ab.center.x.ToString("0.0") + "," +
                              ab.center.z.ToString("0.0") + ") 크기 " + ab.size.ToString("0.00"));

            // **무엇이 무슨 재질을 쓰나** — 「지역 구분이 없다」류의 병은 대개 재질 공유가 원인이다(숲 사례).
            var matUse = new Dictionary<string, int>();
            foreach (var r0 in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var b0 = r0.bounds;
                if (Mathf.Abs(b0.center.x) > VisualSliceBuilder.VillageFadeRadius ||
                    Mathf.Abs(b0.center.z) > VisualSliceBuilder.VillageFadeRadius)
                    continue;
                foreach (var m in r0.sharedMaterials)
                {
                    if (m == null)
                        continue;
                    string k = m.name + " ← " + r0.transform.name;
                    matUse.TryGetValue(k, out int n);
                    matUse[k] = n + 1;
                }
            }
            var mk = new List<string>(matUse.Keys);
            mk.Sort(StringComparer.Ordinal);
            foreach (var k in mk)
                if (matUse[k] >= 3)
                    Debug.Log("[Census] 마을 재질 " + k + " × " + matUse[k]);

            // **지표부터 센다**(검수 지시 2026-09-09 — 「형광 초록 당구대」). 소품을 세기 전에
            // 그 자리에 무엇이 깔려 있는지 본다: 도포가 한 겹이면 화면은 단색이 된다.
            {
                var sum = new float[Ulon.Shared.WorldSplat.LayerCount];
                int n = 0;
                for (int gx = -6; gx <= 6; gx++)
                    for (int gz = -6; gz <= 6; gz++)
                    {
                        float wx = VisualSliceBuilder.HuntViewTarget.x + gx * 4f;
                        float wz = VisualSliceBuilder.HuntViewTarget.y + gz * 4f;
                        int layer = Ulon.Shared.WorldSplat.CoverAt(wx, wz, out float weight);
                        if (layer >= 0 && layer < sum.Length)
                            sum[layer] += weight;
                        sum[0] += Mathf.Max(0f, 1f - (layer >= 0 ? weight : 0f));   // 나머지는 풀이다
                        n++;
                    }
                var names = new[] { "Grass0", "Rock1", "Sand2", "Tilled3", "Soil4", "Gravel5", "Road6", "Cobble7" };
                for (int L = 0; L < sum.Length; L++)
                    if (sum[L] / n > 0.005f)
                        Debug.Log("[Census] 사냥터 지표 " + (L < names.Length ? names[L] : "L" + L) + " 평균 " +
                                  (sum[L] / n).ToString("0.000"));
            }

            // **묻는 것은 「얼룩이 어디 있나」가 아니라 「몹이 밟고 선 것이 무엇인가」다**(검수 지시 2026-09-09).
            {
                var names = new[] { "Grass0", "Rock1", "Sand2", "Tilled3", "Soil4", "Gravel5", "Road6", "Cobble7" };
                int onDirt = 0;
                for (int i = 0; i < VisualSliceBuilder.HuntSpots.Length; i++)
                {
                    var w = VisualSliceBuilder.HuntSpotWorld(i);
                    int layer = Ulon.Shared.WorldSplat.CoverAt(w.x, w.z, out float weight);
                    bool dirt = layer == Ulon.Shared.WorldSplat.Soil || layer == Ulon.Shared.WorldSplat.Gravel;
                    if (dirt && weight >= 0.5f)
                        onDirt++;
                    Debug.Log("[Census] 몹 발밑 " + VisualSliceBuilder.HuntSpots[i].Name + " @(" +
                              w.x.ToString("0.0") + "," + w.z.ToString("0.0") + ") — " +
                              (layer >= 0 && layer < names.Length ? names[layer] : "Grass0") + " " +
                              weight.ToString("0.00"));
                }
                Debug.Log("[Census] 몹 발밑 요약 — 흙·자갈을 절반 넘게 밟은 몹 " + onDirt + "/" +
                          VisualSliceBuilder.HuntSpots.Length + "마리");
            }

            // **「초록 한 톤」을 센다**(검수 랩 ⑤). 증상은 흙이 적은 것이 아니라 초록이 한 톤인 것이다 —
            // 그러니 세는 것도 「흙 비율」이 아니라 **초록이 몇 가지인가**다: 평지 표본이 밟는 잔디 계열
            // 레이어 종수와, 야외 나무가 쓰는 **재질 색의 종수**.
            {
                // 세는 것은 **칠해진 결과**다(알파맵) — 함수가 무엇을 계산하든 화면에 깔린 것이 기준이다.
                var terr = Terrain.activeTerrain;
                if (terr != null && terr.terrainData != null)
                {
                    var td = terr.terrainData;
                    int ar2 = td.alphamapResolution;
                    var alpha2 = td.GetAlphamaps(0, 0, ar2, ar2);
                    float half2 = WorldTerrain.Span * 0.5f;
                    int plain = 0, oneTone = 0;
                    for (float x = -80f; x <= 80f; x += 8f)
                        for (float z = -80f; z <= 80f; z += 8f)
                        {
                            float h = WorldTerrain.HeightAt(x, z);
                            if (h < WorldTerrain.SeaLevel + 2f || h > WorldTerrain.LandBase + 6f)
                                continue;
                            if (Ulon.Shared.WorldSplat.CoverAt(x, z, out float cover) >= 0 && cover > 0.05f)
                                continue;
                            int ix = Mathf.Clamp(Mathf.RoundToInt((x + half2) / WorldTerrain.Span * (ar2 - 1)), 0, ar2 - 1);
                            int iz = Mathf.Clamp(Mathf.RoundToInt((z + half2) / WorldTerrain.Span * (ar2 - 1)), 0, ar2 - 1);
                            plain++;
                            int tones = 0;
                            for (int L = 0; L < td.alphamapLayers; L++)
                                if (alpha2[iz, ix, L] > 0.12f)
                                    tones++;
                            if (tones <= 1)
                                oneTone++;      // 이 자리에는 초록이 한 겹뿐이다
                        }
                    Debug.Log("[Census] 평지 잔디 톤 — 표본 " + plain + "곳 중 한 겹뿐 " + oneTone + "곳(" +
                              (plain == 0 ? 0f : oneTone * 100f / plain).ToString("0") + "%) · 지형 레이어 " +
                              td.alphamapLayers + "종");
                }

                var treeTone = new Dictionary<string, int>();
                int trees = 0;
                foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (t.name.IndexOf("tree", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    var rr = t.GetComponentInChildren<Renderer>(true);
                    if (rr == null || rr.sharedMaterial == null)
                        continue;
                    trees++;
                    string key = rr.sharedMaterial.name + " " + ColorUtility.ToHtmlStringRGB(rr.sharedMaterial.color);
                    treeTone.TryGetValue(key, out int n2);
                    treeTone[key] = n2 + 1;
                }
                var tk = new List<string>(treeTone.Keys);
                tk.Sort(StringComparer.Ordinal);
                Debug.Log("[Census] 나무 " + trees + "그루 · 색 " + tk.Count + "종");
                foreach (var k in tk)
                    Debug.Log("[Census] 나무 색 " + k + " × " + treeTone[k]);
            }

            // **산과 해안을 센다**(검수 랩 ⑥). 증상은 「절벽 무늬가 세로로 늘어난다」와 「해안이
            // 균일한 모래띠」다 — 둘 다 눈대중으로 고치면 엉뚱한 데를 만진다. 재는 것은 셋:
            // ①경사면이 얼마나 가파른가 ②그 자리가 무슨 레이어를 몇 겹 쓰는가(산도 한 톤인가)
            // ③해안 모래띠 폭이 방위마다 정말 같은가.
            {
                var terr2 = Terrain.activeTerrain;
                if (terr2 != null && terr2.terrainData != null)
                {
                    var td = terr2.terrainData;
                    for (int L = 0; L < td.terrainLayers.Length; L++)
                        Debug.Log("[Census] 지형 레이어 " + L + " " + td.terrainLayers[L].name + " 타일 " +
                                  td.terrainLayers[L].tileSize.x.ToString("0.0") + "m");

                    int ar3 = td.alphamapResolution;
                    var alpha3 = td.GetAlphamaps(0, 0, ar3, ar3);
                    float half3 = WorldTerrain.Span * 0.5f;
                    int steep = 0, steepOneTone = 0;
                    float steepestSlope = 0f;
                    for (float x = -140f; x <= 140f; x += 5f)
                        for (float z = -140f; z <= 140f; z += 5f)
                        {
                            float h = WorldTerrain.HeightAt(x, z);
                            if (h < WorldTerrain.LandBase + 6f)
                                continue;                                   // 산비탈만 본다
                            float dh = Mathf.Max(Mathf.Abs(WorldTerrain.HeightAt(x + 2f, z) - h),
                                                 Mathf.Abs(WorldTerrain.HeightAt(x, z + 2f) - h));
                            float slope = Mathf.Atan2(dh, 2f) * Mathf.Rad2Deg;
                            if (slope < 30f)
                                continue;                                   // 「절벽」이라 부를 만한 것만
                            steep++;
                            if (slope > steepestSlope)
                                steepestSlope = slope;
                            int ix = Mathf.Clamp(Mathf.RoundToInt((x + half3) / WorldTerrain.Span * (ar3 - 1)), 0, ar3 - 1);
                            int iz = Mathf.Clamp(Mathf.RoundToInt((z + half3) / WorldTerrain.Span * (ar3 - 1)), 0, ar3 - 1);
                            int tones = 0;
                            for (int L = 0; L < td.alphamapLayers; L++)
                                if (alpha3[iz, ix, L] > 0.12f)
                                    tones++;
                            if (tones <= 1)
                                steepOneTone++;
                        }
                    Debug.Log("[Census] 산 경사면 — 30° 넘는 표본 " + steep + "곳 · 가장 가파른 " +
                              steepestSlope.ToString("0") + "° · 그중 지표 한 겹뿐 " + steepOneTone + "곳(" +
                              (steep == 0 ? 0f : steepOneTone * 100f / steep).ToString("0") + "%)");

                    // **경사 분포(히스토그램)** — 검수 반려 2026-09-09: 「그 면이 정말 55° 이상인지부터
                    // 재라. 수치 없이는 양쪽 다 추측이다.」 사냥터 뒤 산을 10° 칸으로 세고, 칸마다
                    // **풀 계열 평균 가중치**를 같이 찍는다(도포가 어디서 걷혔는지 한 줄로 보인다).
                    {
                        Vector2 eye = VisualSliceBuilder.HuntViewEye;
                        Vector2 tgt = VisualSliceBuilder.HuntViewTarget;
                        Vector2 fwd = (tgt - eye).normalized;
                        Vector2 side = new Vector2(-fwd.y, fwd.x);
                        var n = new int[10];
                        var g = new float[10];
                        var mac = new int[10];
                        var fineSum = new float[10];
                        int total = 0;
                        for (float f = 10f; f <= 140f; f += 1.5f)
                            for (float sdist = -70f; sdist <= 70f; sdist += 1.5f)
                            {
                                float x = tgt.x + fwd.x * f + side.x * sdist;
                                float z = tgt.y + fwd.y * f + side.y * sdist;
                                if (Mathf.Abs(x) > 148f || Mathf.Abs(z) > 148f)
                                    continue;
                                float h = WorldTerrain.HeightAt(x, z);
                                if (h < WorldTerrain.LandBase + 4f)
                                    continue;       // 평지·물가는 이 물음의 대상이 아니다
                                const float d4 = 1.5f;
                                float hx4 = WorldTerrain.HeightAt(x + d4, z) - WorldTerrain.HeightAt(x - d4, z);
                                float hz4 = WorldTerrain.HeightAt(x, z + d4) - WorldTerrain.HeightAt(x, z - d4);
                                float slope4 = Mathf.Atan(Mathf.Sqrt(hx4 * hx4 + hz4 * hz4) / (2f * d4)) * Mathf.Rad2Deg;
                                // **어느 자로 잰 경사인가**가 곧 무엇을 잰 것인가다. 굽는 쪽은 알파맵 간격
                                // (0.587m)으로 재고, 눈은 산의 큰 형태를 본다. 잔주름이 심하면 둘이 갈린다.
                                float dm = 8f;
                                float hxm = WorldTerrain.HeightAt(x + dm, z) - WorldTerrain.HeightAt(x - dm, z);
                                float hzm = WorldTerrain.HeightAt(x, z + dm) - WorldTerrain.HeightAt(x, z - dm);
                                float macro = Mathf.Atan(Mathf.Sqrt(hxm * hxm + hzm * hzm) / (2f * dm)) * Mathf.Rad2Deg;
                                const float dp = 0.587f;   // 알파맵 한 칸 = 굽는 쪽이 쓰는 자
                                float hxp = WorldTerrain.HeightAt(x + dp, z) - WorldTerrain.HeightAt(x - dp, z);
                                float hzp = WorldTerrain.HeightAt(x, z + dp) - WorldTerrain.HeightAt(x, z - dp);
                                float fine = Mathf.Atan(Mathf.Sqrt(hxp * hxp + hzp * hzp) / (2f * dp)) * Mathf.Rad2Deg;
                                mac[Mathf.Clamp((int)(macro / 10f), 0, 9)]++;
                                fineSum[Mathf.Clamp((int)(macro / 10f), 0, 9)] += fine;
                                int b = Mathf.Clamp((int)(slope4 / 10f), 0, 9);
                                int ix4 = Mathf.Clamp(Mathf.RoundToInt((x + half3) / WorldTerrain.Span * (ar3 - 1)), 0, ar3 - 1);
                                int iz4 = Mathf.Clamp(Mathf.RoundToInt((z + half3) / WorldTerrain.Span * (ar3 - 1)), 0, ar3 - 1);
                                n[b]++;
                                total++;
                                g[b] += alpha3[iz4, ix4, WorldSplat.Grass] + alpha3[iz4, ix4, WorldSplat.DryGrass];
                            }
                        var sb = new System.Text.StringBuilder("[Census] 사냥터 뒤 산 경사 분포(표본 " + total + ") — ");
                        for (int b = 0; b < 10; b++)
                            if (n[b] > 0)
                                sb.Append(b * 10).Append("~").Append(b * 10 + 10).Append("° ")
                                  .Append(n[b]).Append("곳(").Append((n[b] * 100f / Mathf.Max(1, total)).ToString("0"))
                                  .Append("%) 풀 ").Append((g[b] / n[b]).ToString("0.00")).Append(" · ");
                        Debug.Log(sb.ToString());
                        var sb2 = new System.Text.StringBuilder("[Census] 같은 표본, 8m 자로 잰 큰 경사 — ");
                        for (int b = 0; b < 10; b++)
                            if (mac[b] > 0)
                                sb2.Append(b * 10).Append("~").Append(b * 10 + 10).Append("° ")
                                   .Append(mac[b]).Append("곳(").Append((mac[b] * 100f / Mathf.Max(1, total)).ToString("0"))
                                   .Append("%) 그 자리 0.587m 자로는 평균 ").Append((fineSum[b] / mac[b]).ToString("0")).Append("° · ");
                        Debug.Log(sb2.ToString());

                        // **고지대에 지역 지표(밭·부엽토·자갈)가 칠해진 자리** — 산비탈에 밭 무늬가
                        // 얹히면 화면에서 「갈색 판때기」로 읽힌다. 어느 겹이 어디에 있는지만 찍는다.
                        {
                            var hi = new Dictionary<int, int>();
                            var where = new Dictionary<int, Vector2>();
                            for (float x = -145f; x <= 145f; x += 2f)
                                for (float z = -145f; z <= 145f; z += 2f)
                                {
                                    float h5 = WorldTerrain.HeightAt(x, z);
                                    if (h5 < WorldTerrain.LandBase + 16f)
                                        continue;
                                    int ix5 = Mathf.Clamp(Mathf.RoundToInt((x + half3) / WorldTerrain.Span * (ar3 - 1)), 0, ar3 - 1);
                                    int iz5 = Mathf.Clamp(Mathf.RoundToInt((z + half3) / WorldTerrain.Span * (ar3 - 1)), 0, ar3 - 1);
                                    int[] layers = { WorldSplat.Tilled, WorldSplat.Soil, WorldSplat.Gravel, WorldSplat.Road, WorldSplat.Cobble };
                                    for (int q = 0; q < layers.Length; q++)
                                        if (alpha3[iz5, ix5, layers[q]] > 0.5f)
                                        {
                                            hi.TryGetValue(layers[q], out int n5);
                                            hi[layers[q]] = n5 + 1;
                                            if (!where.ContainsKey(layers[q]))
                                                where[layers[q]] = new Vector2(x, z);
                                        }
                                }
                            var names5 = new[] { "풀", "바위", "모래", "밭", "부엽토", "자갈", "길", "돌포장", "마른풀", "절벽" };
                            var sb3 = new System.Text.StringBuilder("[Census] 고지대(16m↑) 지역 지표 — ");
                            if (hi.Count == 0)
                                sb3.Append("없음");
                            foreach (var kv in hi)
                                sb3.Append(names5[kv.Key]).Append(" ").Append(kv.Value).Append("곳(첫 자리 ")
                                   .Append(where[kv.Key].x.ToString("0")).Append(",").Append(where[kv.Key].y.ToString("0")).Append(") · ");
                            Debug.Log(sb3.ToString());
                        }
                    }
                }

                // 해안 모래띠 — 중심에서 방위마다 바깥으로 훑어 물가를 찾고, 거기서 육지 쪽으로
                // 모래가 몇 미터나 이어지는지 잰다. 폭이 어디나 같으면 「띠를 두른 것」으로 읽힌다.
                float wsum = 0f, wmin = 999f, wmax = 0f;
                int wn = 0;
                for (float a = 0f; a < 360f; a += 15f)
                {
                    float ca = Mathf.Cos(a * Mathf.Deg2Rad), sa = Mathf.Sin(a * Mathf.Deg2Rad);
                    float shore = -1f;
                    for (float d = 40f; d < 150f; d += 0.5f)
                        if (WorldTerrain.HeightAt(ca * d, sa * d) < WorldTerrain.SeaLevel)
                        { shore = d; break; }
                    if (shore < 0f)
                        continue;
                    float back = 0f;
                    for (float d = shore; d > shore - 30f; d -= 0.5f)
                    {
                        float h = WorldTerrain.HeightAt(ca * d, sa * d);
                        if (h > WorldTerrain.SeaLevel + 2.8f)
                            break;                                          // 모래로 칠하는 띠의 끝
                        back = shore - d;
                    }
                    wsum += back; wn++;
                    wmin = Mathf.Min(wmin, back);
                    wmax = Mathf.Max(wmax, back);
                }
                if (wn > 0)
                    Debug.Log("[Census] 해안 모래띠 — 방위 " + wn + "곳 평균 " + (wsum / wn).ToString("0.0") +
                              "m · 가장 좁은 " + wmin.ToString("0.0") + "m · 가장 넓은 " + wmax.ToString("0.0") + "m");
            }

            var nodes = new List<Transform>();
            var boxes = new List<Bounds>();
            Collect(nodes, boxes);

            var zoneCount = new Dictionary<string, int>();
            for (int i = 0; i < nodes.Count; i++)
            {
                string z = Zone(boxes[i].center);
                zoneCount.TryGetValue(z, out int n);
                zoneCount[z] = n + 1;
            }
            var keys = new List<string>(zoneCount.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var k in keys)
                Debug.Log("[Census] 구역 " + k + " — 소품 " + zoneCount[k] + "개");

            // 서로 물린 쌍 — 얇은 쪽 두께의 절반보다 깊게 물리면 「파고들었다」로 본다(던전 자와 같은 기준).
            var pairs = new List<string>();
            for (int a = 0; a < boxes.Count; a++)
                for (int b = a + 1; b < boxes.Count; b++)
                {
                    if (!boxes[a].Intersects(boxes[b]))
                        continue;
                    float deep = float.MaxValue;
                    for (int ax = 0; ax < 3; ax++)
                        deep = Mathf.Min(deep, Mathf.Max(0f,
                            Mathf.Min(boxes[a].max[ax], boxes[b].max[ax]) - Mathf.Max(boxes[a].min[ax], boxes[b].min[ax])));
                    float thin = Mathf.Min(Thickness(boxes[a]), Thickness(boxes[b]));
                    if (deep < thin * 0.5f)
                        continue;
                    pairs.Add(Zone(boxes[a].center) + " | " + nodes[a].name + " ↔ " + nodes[b].name +
                              " 물린 깊이 " + deep.ToString("0.00") + "m (얇은 쪽 " + thin.ToString("0.00") + "m) @(" +
                              boxes[a].center.x.ToString("0.0") + "," + boxes[a].center.z.ToString("0.0") + ")");
                }
            pairs.Sort(StringComparer.Ordinal);
            Debug.Log("[Census] 물린 쌍 " + pairs.Count + "건");
            foreach (var s in pairs)
                Debug.Log("[Census] " + s);

            // **지역 반경 안에 서 있는데 그 지역이 놓지 않은 것** — 남의 원장이 잡은 자리다.
            for (int i = 0; i < nodes.Count; i++)
            {
                string z = Zone(boxes[i].center);
                if (z == "마을" || z == "그밖")
                    continue;
                var root = nodes[i];
                while (root.parent != null)
                    root = root.parent;
                if (root.name.StartsWith("Region_", StringComparison.Ordinal))
                    continue;
                // 그 지역이 놓은 것과 얼마나 가까운가 — 「지역 안」보다 「지역 물건 사이」가 실제 증상이다.
                float near = float.MaxValue;
                string nearName = "(없음)";
                for (int j = 0; j < nodes.Count; j++)
                {
                    var jr = nodes[j];
                    while (jr.parent != null)
                        jr = jr.parent;
                    if (!jr.name.StartsWith("Region_", StringComparison.Ordinal))
                        continue;
                    float d = Vector2.Distance(new Vector2(boxes[i].center.x, boxes[i].center.z),
                                               new Vector2(boxes[j].center.x, boxes[j].center.z));
                    if (d < near) { near = d; nearName = nodes[j].name; }
                }
                Debug.Log("[Census] 남의 것 " + z + " 안에 " + root.name + "/" + nodes[i].name + " @(" +
                          boxes[i].center.x.ToString("0.0") + "," + boxes[i].center.z.ToString("0.0") +
                          ") 크기 " + boxes[i].size.ToString("0.00") + " · 그 지역 물건까지 " +
                          near.ToString("0.0") + "m(" + nearName + ")");
            }

            // 한 구역의 물건을 이름으로 못 박는다 — 어느 패스가 놓았는지 추적하려면 이름이 필요하다.
            // 볼 구역은 환경변수 `ULON_CENSUS_ZONE`로 고른다(기본 밭). 「무엇이 있나」를 세는 자리다.
            string want = Environment.GetEnvironmentVariable("ULON_CENSUS_ZONE");
            if (string.IsNullOrEmpty(want))
                want = "밭";
            for (int i = 0; i < nodes.Count; i++)
            {
                if (Zone(boxes[i].center) != want)
                    continue;
                Debug.Log("[Census] " + want + " 소품 " + nodes[i].name + " @(" + boxes[i].center.x.ToString("0.0") + "," +
                          boxes[i].center.z.ToString("0.0") + ") 크기 " + boxes[i].size.ToString("0.00") +
                          " 부모 " + (nodes[i].parent == null ? "(루트)" : nodes[i].parent.name));
            }
            if (UnityEngine.Application.isBatchMode)
                EditorApplication.Exit(0);
        }

        static float Thickness(Bounds b) => Mathf.Min(b.size.x, Mathf.Min(b.size.y, b.size.z));

        static string Zone(Vector3 p)
        {
            // **광장은 마을 안의 또 다른 마당이다**(검수 2026-09-09 — 「울타리가 광장을 가로지른다」).
            // 마을로 뭉뚱그리면 349개에 묻혀 안 보인다 — 먼저 이 자리만 따로 센다.
            if (Mathf.Abs(p.x) <= VisualSliceBuilder.PlazaYard && Mathf.Abs(p.z) <= VisualSliceBuilder.PlazaYard)
                return "광장";
            if (Mathf.Abs(p.x) <= VisualSliceBuilder.VillageFadeRadius &&
                Mathf.Abs(p.z) <= VisualSliceBuilder.VillageFadeRadius)
                return "마을";
            if (new Vector2(p.x - VisualSliceBuilder.HuntViewTarget.x,
                            p.z - VisualSliceBuilder.HuntViewTarget.y).magnitude <= 30f)
                return "사냥터";
            if (Near(p, WorldRegions.Meadow)) return "밭";
            if (Near(p, WorldRegions.Forest)) return "숲";
            if (Near(p, WorldRegions.Mine)) return "광산";
            if (Near(p, WorldRegions.TestChamber)) return "테스트";
            return "그밖";
        }

        static bool Near(Vector3 p, WorldRegions.Region r) =>
            new Vector2(p.x - r.X, p.z - r.Z).magnitude <= r.Radius;

        /// <summary>`CollectVillage`와 같은 규칙(묶음은 한 겹 내려간다·사람은 뺀다)이되 **마을 밖도 센다**.</summary>
        static void Collect(List<Transform> nodes, List<Bounds> boxes)
        {
            var stack = new Stack<Transform>();
            var all = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].scene.IsValid() && all[i].transform.parent == null)
                    stack.Push(all[i].transform);

            while (stack.Count > 0)
            {
                var t = stack.Pop();
                if (t.name == "Ground" || t.name.StartsWith("Terrain", StringComparison.Ordinal))
                    continue;
                // **건물 판정을 사람 판정보다 먼저 한다.** 은행처럼 NPC를 자식으로 품은 건물은
                // 「사람이 들어 있으니 건너뛴다」에 걸려 **한 번도 안 세어졌다** — 그래서 `50_villagers`의
                // 반투명 판이 무엇인지 물었을 때 목록에 은행이 없었다(실측 2026-09-09).
                if (VisualSliceBuilder.IsBuildingObject(t.name))
                {
                    if (GroundFit.WorldBounds(t, out Bounds bb0))
                        Debug.Log("[Census] 건물 " + t.name + " @(" + bb0.center.x.ToString("0.0") + "," +
                                  bb0.center.z.ToString("0.0") + ") 크기 " + bb0.size.ToString("0.00") +
                                  " · 안에 든 사람 " + t.GetComponentsInChildren<Ulon.Server.WorldBody>(true).Length + "명");
                    continue;
                }
                if (t.GetComponentInChildren<Ulon.Server.WorldBody>(true) != null)
                    continue;
                if (t.GetComponentInChildren<Renderer>(true) == null)
                    continue;
                if (false)
                {
                    if (GroundFit.WorldBounds(t, out Bounds bb))
                        Debug.Log("[Census] 건물 " + t.name + " @(" + bb.center.x.ToString("0.0") + "," +
                                  bb.center.z.ToString("0.0") + ") 크기 " + bb.size.ToString("0.00"));
                    continue;
                }
                if (t.GetComponent<Renderer>() == null && t.childCount > 1)
                {
                    for (int c = 0; c < t.childCount; c++)
                        stack.Push(t.GetChild(c));
                    continue;
                }
                if (!t.gameObject.activeInHierarchy)
                    continue;
                if (!GroundFit.WorldBounds(t, out Bounds wb) || SliceSelfCheck.IsFlatMat(wb))
                    continue;
                nodes.Add(t);
                boxes.Add(wb);
            }
        }
    }
}
