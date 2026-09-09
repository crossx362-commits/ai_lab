using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// 지형 **도포 원장** — 어느 좌표가 무슨 지표인지 한 곳에서 정한다. 빌더가 칠하고 Assert가 같은 함수로 잰다.
    ///
    /// 왜 필요한가: §6.1 지역(농경지·숲·광산)이 소품만 얹힌 채 바닥은 전부 같은 밝은 초록이라
    /// 「지역」으로 안 읽혔다(검수 2026-09-06 관찰). 그리고 지역끼리 잇는 길이 없어 평지에 떠 있었다.
    /// </summary>
    public static class WorldSplat
    {
        // TerrainData.terrainLayers 순서와 같아야 한다(빌더가 이 순서로 배열을 만든다).
        public const int Grass = 0;
        public const int Rock = 1;
        public const int Sand = 2;
        public const int Tilled = 3;    // 농경지 — 갈아엎은 흙
        public const int Soil = 4;      // 숲 — 어두운 부엽토
        public const int Gravel = 5;    // 광산 — 자갈
        public const int Road = 6;      // 지역을 잇는 길
        public const int Cobble = 7;    // 마을 광장 — 돌포장
        /// <summary>
        /// **마른 풀**(검수 랩 ⑤ 「초록 한 톤」). 세어 보니 지역 밖 평지 표본 253곳 중 **184곳(73%)이
        /// 지표 한 겹**이었다 — 흙이 적은 게 아니라 초록이 한 가지인 것이 증상이다. 그래서 흙 비율은
        /// 그대로 두고 **잔디 안에서** 가른다: 같은 풀 자리를 짙은 풀과 마른 풀 둘로 나눠 칠한다.
        /// 두 겹의 합은 언제나 옛 풀 한 겹과 같다 — 「지역 밖 평지 풀 하한 0.80」은 그래서 안 흔들린다.
        /// </summary>
        public const int DryGrass = 8;
        /// <summary>
        /// **그늘진 절벽 바위**(검수 랩 ⑥ 「산 표면이 세로로 늘어난다」). 유니티 지형은 도포를
        /// **평면(XZ)으로 투영**하므로 경사가 설수록 무늬가 세로로 늘어난다 — 세어 보니 이 산은
        /// **최대 82°**다. 투영을 바꾸려면 지형 셰이더를 갈아야 하니, 대신 **늘어난 줄무늬를 끊는다**:
        /// 바위를 두 겹으로 갈라 다른 타일링·다른 색으로 얼룩지게 한다(경사면 39%가 한 겹이었다).
        /// </summary>
        public const int CliffDark = 9;
        public const int LayerCount = 10;

        /// <summary>
        /// **풀은 벽에 안 붙는다**(검수 랩 ⑨ — 상식 모순). 삼면 투영으로 암석 결은 살아났는데
        /// 앞쪽 절벽에 **초록·누런 띠가 커튼처럼 흘러내렸다**. 60m 수직 암벽에 풀은 자라지 않는다.
        ///
        /// 뿌리는 「산 중턱까지 풀이 올라간다」를 위해 걸어 둔 **바위 상한**이었다(고도 22m 아래에서
        /// rock ≤ 0.42~0.87). 그 상한이 **경사를 안 봤다** — 중턱 비탈에도 80° 암벽에도 똑같이 걸려
        /// 풀을 남겼다. 그래서 상한을 없애지 않고 **급경사에서만 풀어 준다**.
        ///
        /// **어느 자로 잰 경사인가가 곧 무엇을 잰 것인가다**(검수 반려 → 재측정 2026-09-09).
        /// 처음엔 알파맵 한 칸(0.587m)으로 쟀는데, 이 지형은 잔주름이 심해 그 자가 **큰 형태보다
        /// 평균 15° 높게** 읽는다 — 사냥터 뒤 산 실측: 8m 자로 30~40°인 자리가 0.587m 자로는 46°,
        /// 40~50°인 자리가 57°, 50~60°인 자리가 66°. 그래서 「55° 위는 벽」 규칙이 **둥근 흙산의
        /// 45° 비탈까지** 맨바위로 만들었다(`03_hunt_mobs`가 베이지 한 장이 됐다 — 검수 반려).
        /// 눈이 보는 것은 큰 형태이므로 **8m 자로 재고**, 문턱도 그 자에 맞춰 다시 유도한다:
        /// 풀은 실제로 45°까지 붙으므로 **50°에서 풀리기 시작해 65°면 상한이 사라진다**.
        ///
        /// 굽는 쪽과 재는 쪽이 **좌표를 넣고 같은 이 함수**를 부른다 — 자의 길이가 갈리면
        /// 다시 같은 병이 난다.
        /// </summary>
        public const float WallMacroSpan = 8f;      // 큰 경사를 재는 자의 길이(m)
        public const float WallMidSpan = 3f;        // 짧은 쪽 자 — 8m 자만으로는 벽 밑 평평한 단까지 벽이 된다
        public const float WallSlopeFrom = 1.19f;   // tan 50°
        public const float WallSlopeTo = 2.14f;     // tan 65°

        /// <summary>
        /// 이 자리의 **큰 경사**(tan) — 잔주름이 아니라 산의 형태를 읽는다.
        ///
        /// **자 하나로는 안 된다.** 짧은 자(0.587m)는 잔주름을 경사로 읽어 둥근 흙산까지 벽으로 만들고,
        /// 긴 자(8m)만 쓰면 **벽 밑 평평한 단**까지 벽이 된다(중앙차분이 8m 밖의 절벽을 끌어온다 —
        /// 실측으로 봤다: 큰 경사 60°↑ 자리의 8.8%에 풀 가중치가 1.00이었다). 그래서 **둘 다 가파른
        /// 자리만** 벽이다 — 짧은 쪽과 긴 쪽 중 **작은 값**을 쓴다.
        /// </summary>
        public static float MacroSlopeTan(float wx, float wz)
        {
            return Mathf.Min(SlopeTanAt(wx, wz, WallMidSpan), SlopeTanAt(wx, wz, WallMacroSpan));
        }

        static float SlopeTanAt(float wx, float wz, float d)
        {
            float hx = WorldTerrain.HeightAt(wx + d, wz) - WorldTerrain.HeightAt(wx - d, wz);
            float hz = WorldTerrain.HeightAt(wx, wz + d) - WorldTerrain.HeightAt(wx, wz - d);
            return Mathf.Sqrt(hx * hx + hz * hz) / (2f * d);
        }

        /// <summary>tan 경사 하나로 판정만 — 반대쪽 한계가 그냥 부를 수 있게 떼어 둔다.</summary>
        public static float WallRockFromTan(float macroTan)
        {
            return Mathf.Clamp01((macroTan - WallSlopeFrom) / (WallSlopeTo - WallSlopeFrom));
        }

        /// <summary>이 자리에서 「중턱 풀」 상한을 얼마나 풀어 줄지(0 = 그대로, 1 = 상한 없음).</summary>
        public static float WallRockAt(float wx, float wz)
        {
            return WallRockFromTan(MacroSlopeTan(wx, wz));
        }

        /// <summary>
        /// **옛 평면 마스크 — 반대쪽 한계 표본으로만 쓴다**(랩 ㉡). 셰이더의 `_PlanarOnly`와 같은 자리다:
        /// 「고쳤다」를 증명하려면 **안 고친 것을 같은 자리로 재서 빨간불**을 봐야 한다.
        /// 굽는 쪽에서 부르지 마라 — 부르면 낙하선 얼룩이 돌아온다.
        /// </summary>
        public static float DarkCliffPlanarAt(float wx, float wz)
        {
            float broad = Mathf.PerlinNoise(wx * 0.032f + 41.7f, wz * 0.032f + 12.9f);
            float mid = Mathf.PerlinNoise(wx * 0.065f + 8.2f, wz * 0.065f + 77.5f);
            return Mathf.Clamp(Mathf.Clamp01((Mathf.Max(broad, mid * 0.94f) - 0.47f) / 0.20f), 0.05f, 0.95f);
        }

        /// <summary>
        /// 이 자리의 지표 법선. 마스크를 섞는 데만 쓰므로 **잔주름이 아니라 면**을 읽어야 한다 —
        /// 2m 자다(0.587m 자는 잡음, 8m 자는 벽 밑 단까지 끌어온다는 것을 `MacroSlopeTan`에서 배웠다).
        /// </summary>
        public static Vector3 SurfaceNormalAt(float wx, float wz) => SurfaceNormal(wx, wz);

        static Vector3 SurfaceNormal(float wx, float wz)
        {
            const float d = 2f;
            float hx = WorldTerrain.HeightAt(wx + d, wz) - WorldTerrain.HeightAt(wx - d, wz);
            float hz = WorldTerrain.HeightAt(wx, wz + d) - WorldTerrain.HeightAt(wx, wz - d);
            return new Vector3(-hx / (2f * d), 1f, -hz / (2f * d)).normalized;
        }

        /// <summary>
        /// 세 평면에서 뽑은 잡음을 법선으로 섞는다 — **셰이더가 무늬를 뽑는 방식과 같다**
        /// (`TerrainTriplanar.shader`, 날카로움 4). 무늬와 마스크가 다른 투영을 쓰면 무늬는 안 늘어나는데
        /// 섞는 규칙만 늘어나 화면에는 **얼룩이 흘러내리는 물때**로 남는다.
        /// </summary>
        static float TriNoise(float wx, float wy, float wz, Vector3 n, float f, float ox, float oz)
        {
            return TriNoise(wx, wy, wz, n, f, ox, oz, 1f);
        }

        /// <summary>
        /// `aniso`는 **벽에서 높이 방향으로만 주파수를 올린다** — 가로로 누운 띠, 곧 **층리**가 된다
        /// (검수 랩 ㉥: 「덩이는 크기만 맞으면 되는 것이 아니다 — 지형이 가진 방향을 안 따르면
        /// 무늬가 아니라 얼룩이 된다」). 등방 잡음은 화면에서 곰팡이·위장 무늬로 읽혔다.
        /// **위에서 본 면(평지)은 건드리지 않는다** — 평지에 층리는 없다.
        /// </summary>
        static float TriNoise(float wx, float wy, float wz, Vector3 n, float f, float ox, float oz, float aniso)
        {
            // **등고선 좌표로 뽑는 판은 재보고 걷었다**(랩 ㉥). 「낙하선에 수직인 방향으로 잰 거리」
            // 하나로 벽 무늬를 뽑으면 격자는 없어지지만, 법선이 자리마다 돌아 그 좌표가 함께 돌기
            // 때문에 무늬가 **낙하선을 따라 이어졌다** — 자도 그렇게 말했다(낙하선/등고선 비율
            // 1.63 → 1.22로 떨어짐 = 등고선 쪽으로 더 많이 변한다 = 세로줄). 화면도 같았다.
            Vector3 bw = new Vector3(Mathf.Pow(Mathf.Abs(n.x), PlaneSharp), Mathf.Pow(Mathf.Abs(n.y), 4f), Mathf.Pow(Mathf.Abs(n.z), PlaneSharp));
            float s = bw.x + bw.y + bw.z;
            bw /= Mathf.Max(s, 1e-4f);
            float top = Mathf.PerlinNoise(wx * f + ox, wz * f + oz);                    // 위에서 본 면
            float side = Mathf.PerlinNoise(wz * f + ox, wy * f * aniso + oz);           // 동서 벽
            float front = Mathf.PerlinNoise(wx * f + ox, wy * f * aniso + oz);          // 남북 벽
            return bw.y * top + bw.x * side + bw.z * front;
        }

        /// <summary>
        /// 벽에서 덩이를 **등고선 방향으로 눕히는 정도**(가로:세로). 실제 암반의 어두운 부분은
        /// 둥근 반점이 아니라 **층리**다 — 등방 덩이는 화면에서 곰팡이로 읽혔다(검수 랩 ㉥).
        /// </summary>
        const float LayerAniso = 4f;

        /// <summary>
        /// 삼면 투영에서 **두 평면이 함께 섞이는 폭**을 정하는 날카로움. 이 값이 낮으면 비스듬한 면에서
        /// 동서 벽과 남북 벽의 무늬가 겹쳐 **격자**가 뜬다(검수 랩 ㉧ — 「눕히는 축을 만지면 그 자리가
        /// 무늬가 된다」). 재는 쪽(`SliceSelfCheck.PlaneMix`)이 같은 값을 읽는다.
        /// </summary>
        public const float PlaneSharp = 16f;

        /// <summary>
        /// 마스크가 0에서 1로 건너가는 **폭**(값이 클수록 중간톤이 늘어 화면 대비가 낮아진다).
        /// `ULON_MASK_SPREAD`로 덮어쓸 수 있다 — **네거티브 컨트롤 전용**이다(좁히면 대비가 올라야 한다).
        /// </summary>
        public static float MaskSpread
        {
            get
            {
                var s = System.Environment.GetEnvironmentVariable("ULON_MASK_SPREAD");
                return !string.IsNullOrEmpty(s) && float.TryParse(s, out float v) && v > 0.001f ? v : 0.20f;
            }
        }

        /// <summary>
        /// 마스크의 **양 끝을 안으로 당기는 정도**(0이면 순수 두 톤, 클수록 두 바위가 늘 섞여 대비가 낮다).
        /// `ULON_MASK_FLOOR`로 덮어쓴다 — **NC 전용**(0.02로 낮추면 화면 대비가 올라야 한다).
        /// </summary>
        public static float MaskFloor
        {
            get
            {
                var s = System.Environment.GetEnvironmentVariable("ULON_MASK_FLOOR");
                return !string.IsNullOrEmpty(s) && float.TryParse(s, out float v) && v >= 0f && v < 0.5f ? v : 0.18f;
            }
        }

        /// <summary>이 자리의 바위 중 **그늘진 절벽 바위가 차지하는 몫**(0~1).</summary>
        public static float DarkCliffAt(float wx, float wz)
        {
            // 잔디와 **다른 주기**를 쓴다 — 같은 주기면 산과 들이 같은 얼룩을 쓰는 것이 눈에 보인다.
            // **전환을 다시 좁힌다**(검수 랩 ⑪). 랩 ⑥에서 넓게 잡은 이유는 「두 무늬가 겹쳐야 늘어난
            // 세로줄이 끊긴다」였는데, 그 줄은 랩 ⑧에서 투영을 고쳐 없어졌다 — **이유가 사라진 값이다.**
            // 지금 필요한 것은 겹침이 아니라 **덩이 단위 갈림**이고(랩 ⑤ 잔디에서 배운 그것), 넓은 전환은
            // 자리마다 중간값을 만들어 화면에서 **한 톤**으로 뭉갠다.
            // **덩이 크기는 한 화면에 서넛**이 들어와야 한다 — 55m 덩이로 잡았더니 앞쪽 절벽 한 면이
            // 통째로 한 덩이에 들어가 화면에서는 여전히 한 톤이었다(샷으로 확인). 31m 위에 15m를 겹친다.
            // **마스크도 삼면으로 뽑는다**(검수 랩 ㉡ 판별 테스트). 랩 ⑧에서 무늬는 삼면으로 고쳤는데
            // **무늬를 섞는 규칙**은 여전히 XZ 평면 좌표였다 — 급경사에서는 XZ로 1m 가는 동안 표면은
            // 7m를 가므로 마스크가 **정확히 낙하선 방향으로 늘어난다.** 빨강 판별 테스트로 봤다:
            // 평면 마스크는 산 정수리에서 아래로 흘러내리는 **빨간 세로 줄**로 떴다(덩이가 아니었다).
            // 그래서 「어두운 덩이가 골에 앉는다」로 보이던 것도 골이 아니라 **낙하선**이었다.
            float wy = WorldTerrain.HeightAt(wx, wz);
            Vector3 n = SurfaceNormal(wx, wz);
            float broad = TriNoise(wx, wy, wz, n, 0.045f, 41.7f, 12.9f, LayerAniso);
            float mid = TriNoise(wx, wy, wz, n, 0.092f, 8.2f, 77.5f, LayerAniso);
            // **두 자리를 같이 놓고 더한 세 번째 주기**(검수 랩 ㉣). 화면에서 세어 보니 한 톤 구간이
            // `16`(가까운 수직 암벽)에서 표면 68m·화면폭 28%, `03`(먼 둥근 능선)에서 34m·36%였다 —
            // **화면 비율은 비슷한데 표면 길이는 두 배**라, 주기 하나로 둘을 맞출 수 없다.
            // 그래서 큰 덩이를 지우고 새로 잡는 대신 **큰 덩이 안을 다시 가르는 잔 주기(7m)**를 얹는다:
            // 먼 능선에서는 세부가 되고, 가까운 벽에서는 68m짜리 한 톤을 끊는다.
            // (텍스처 주기를 키우던 랩 ⑫와 다른 자리다 — 여기는 잡음이라 되감김 막대가 없다.)
            float fine = TriNoise(wx, wy, wz, n, 0.145f, 55.1f, 30.6f);   // 잔 주기는 눕히지 않는다 — 1.7m 간격이 격자로 읽혔다
            // **잔 주기는 벽에서만 얹는다.** 전면에 얹어 보고 두 자리를 같이 재서 알았다:
            // `16`(벽)은 한 톤 68m → 44m로 끊겼는데 `03`(둥근 능선)은 34m → 46m로 **되레 길어졌다**
            // (그 화면은 밝은 쪽이 압도적이라, 잔 주기가 어두운 점을 흩뿌리며 밝은 구간을 되레 이었다).
            // 벽은 화면에 크게 잡히고 능선은 멀리 잡힌다 — **필요한 크기가 다르면 규칙도 경사를 봐야 한다.**
            float steep = Mathf.Clamp01((MacroSlopeTan(wx, wz) - 1.0f) / 1.0f);   // 45° 0 → 63° 1
            // 문턱 0.47은 균형에서 나왔다(0.42 → 밝은 17%·어두운 52%, 0.52 → 54%·16%, 0.47이 그 사이) — 두 잡음의 최댓값은 위로 치우치므로 0.42로 자르면
            // 어두운 덩이가 52%로 쏠린다(자가 잡았다).
            // **반점은 두 자리를 같이 재서 골랐다.** 잔 주기를 값에 그냥 더하면(진폭 0.45·0.70)
            // 문턱 언저리만 흔들려 큰 덩이 안은 안 갈리고, 덩이 안에 **반점**으로 앉히면 `16`은
            // 더 갈리는데 `03`(먼 능선)이 되레 뭉쳤다(한 톤 최장 30.6m → 34.9m). 그래서 **가산 쪽**을
            // 남기고 벽에서만 세게 얹는다 — 두 자리를 같이 보지 않았으면 반점판을 골랐을 것이다.
            // **높이 축은 재보고 걷었다**(랩 ㉤). 「같은 덩이 안에서 아래는 어둡고 위는 바랜다」로
            // 근접면의 한 톤을 깨려 했는데, 화면에서 세니 `16`의 한 톤 최장이 28.1% → **34.4%로
            // 늘었다**(그 면은 고지대라 통째로 밝은 쪽으로 밀렸다 — 톤이 갈린 게 아니라 바뀐 것뿐).
            // `03`은 28.1% → 25.0%로 좋아졌지만 **고치려던 자리가 나빠졌으므로 걷는다.**
            float b = Mathf.Max(broad, mid * 0.94f) + (fine - 0.5f) * 0.70f * steep;
            // **대비 손잡이는 나누는 폭이 아니라 여기다**(2026-09-09, 재보고 알았다). 검수 메모가
            // 예고한 「폭 0.20 → 0.24」를 실제로 해서 두 자리를 쟀더니 **화면 대비는 39.6 → 37.7**로
            // 거의 안 움직였는데(`16`은 74.4 → 74.3), 암면 한 톤 게이트가 **어두운 덩이 20% 미만**으로
            // 빨간불을 냈다. 이유: 잡음 값 대부분이 문턱에서 멀어 **이미 양 끝에 포화**돼 있다 —
            // 폭을 넓혀 봐야 중간톤은 문턱 언저리 좁은 띠에서만 늘고, 대신 **덩이 배분**이 무너진다.
            // 화면 대비를 만드는 것은 마스크가 얼마나 빨리 건너가느냐가 아니라 **양 끝이 얼마나
            // 극단이냐**다. 그래서 폭은 0.20으로 두고 **양 끝을 안으로 당긴다**(`MaskFloor`):
            // 어두운 자리에도 밝은 바위가 조금 섞여 톤 차가 줄되, 어느 쪽이 어두운가는 안 바뀌므로
            // 덩이 배분 게이트는 그대로 선다. 재는 자는 `tools/qa_band.py`(오츠로 가른 두 평균의 차).
            return Mathf.Clamp(Mathf.Clamp01((b - 0.47f) / MaskSpread), MaskFloor, 1f - MaskFloor);
        }

        /// <summary>이 자리의 풀 중 **마른 풀이 차지하는 몫**(0~1). 굽는 쪽·재는 쪽이 같이 읽는다.</summary>
        public static float DryGrassAt(float wx, float wz)
        {
            // 저주파 얼룩이 톤을 가르고(멀리서 보이는 무늬), 고주파가 경계를 흐트러뜨린다(줄무늬 방지).
            // **완만하게 섞으면 화면에서는 그냥 탁해진다.** 첫 판이 그랬다: 마른 풀 몫이 평균 0.27인데
            // 값 대부분이 중간대(0.2~0.4)라 두 겹이 섞여 **한 톤이 조금 흐려졌을 뿐**이었다(마을 넓은
            // 샷에서 확인). 얼룩으로 읽히려면 자리마다 **거의 마른 풀이거나 거의 짙은 풀**이어야 한다 —
            // 그래서 전환 구간을 좁혀(0.34 → 0.13) 경계를 세우고, 얼룩 주기도 38m에서 22m로 줄인다.
            // 주기가 하나면 **얼룩과 얼룩 사이가 통째로 비는 자리**가 생긴다 — 22m 주기 하나로 깔았더니
            // 마을은 갈렸는데 사냥터 앞쪽 30m가 균일한 초록이었다(샷으로 확인). 큰 얼룩 위에 중간 얼룩을
            // 겹쳐 어느 화면에서도 한 톤 구간이 남지 않게 한다.
            float broad = Mathf.PerlinNoise(wx * 0.045f + 17.3f, wz * 0.045f + 53.1f);
            float mid = Mathf.PerlinNoise(wx * 0.090f + 63.9f, wz * 0.090f + 21.4f);
            float fine = Mathf.PerlinNoise(wx * 0.140f + 5.7f, wz * 0.140f + 88.4f) - 0.5f;
            float b = Mathf.Max(broad, mid * 0.92f);
            return Mathf.Clamp01((b + fine * 0.30f - 0.52f) / 0.13f) * 0.90f;
        }

        /// <summary>
        /// **길은 던전 문 뒤 언덕을 넘지 않는다**(2026-09-09, 문 뒤 언덕 랩 화면).
        ///
        /// 스포크 길은 좌표만 보고 그어진다 — 지형이 솟으면 그 위를 그대로 탄다. 언덕을 올리자
        /// `07`·`09`·`11` 위쪽에 돌길 띠가 **정상까지** 뻗어 「산꼭대기로 가는 길」이 됐다
        /// (셈: D2 언덕 반경 안에 길 칸 170개). 사람이 낸 길은 언덕을 넘지 않고 돌아간다.
        ///
        /// 경사로 자르지 않고 **언덕 높이로** 자른다: 셈이 말하길 언덕 경사는 최급 26°라
        /// 경사 임계로는 안 걸린다 — 걸리는 것은 「여기가 문 뒤 언덕인가」다. 문 앞·문턱은
        /// `MoundRise`가 0이라 입구 앞길·광장은 그대로다.
        /// </summary>
        public const float RoadMoundCut = 2.0f;    // 언덕이 이만큼 솟으면 길은 사라진다

        public static float RoadMoundFade(float wx, float wz)
        {
            return 1f - Mathf.Clamp01(EntranceGeom.MoundRise(wx, wz) / RoadMoundCut);
        }

        public const float RoadHalfWidth = 2.4f;   // 길 중심에서 이 폭까지는 온전히 길
        public const float RoadFade = 1.8f;        // 그 바깥으로 이 폭만큼 흙이 옅어진다

        /// <summary>마을 광장에서 각 지역으로 가는 길(직선 스포크). 지역이 평지에 떠 있지 않게 한다.</summary>
        public static Vector4[] Routes => new[]
        {
            new Vector4(0f, 0f, WorldRegions.Meadow.X, WorldRegions.Meadow.Z),
            new Vector4(0f, 0f, WorldRegions.Forest.X, WorldRegions.Forest.Z),
            new Vector4(0f, 0f, WorldRegions.Mine.X, WorldRegions.Mine.Z),
        };

        /// <summary>
        /// **마을 아마밭** — 지역(농경지)과 달리 마을 안에 있는 작은 뙈기라 원형 지역 규칙에 안 걸린다.
        /// 바닥이 초록 잔디인 채로 작물만 얹으면 「밭」이 아니라 「풀밭에 놓인 풀」이다(검수 반려:
        /// 20cm 덤불 하나가 아마밭이었다). 중심·반폭을 여기 한 곳에 적고 빌더·게이트가 같이 읽는다.
        /// </summary>
        public const float FlaxX = 3.4f * WorldScale.Kit;   // 마을 격자 × 킷 배율(랩 B)
        // 북쪽 가장자리를 마을 우리(z≈−17.4)에 물리지 않게 잡는다 — 첫 판은 마을 울타리가
        // 밭 안으로 들어와 「경작지」가 아니라 「우리」로 읽혔다(게이트가 안쪽 침범 6개로 잡았다).
        public const float FlaxZ = -20.6f * WorldScale.Kit;   // 마을 격자 × 킷 배율(랩 B)
        public const float FlaxHalfX = 5.0f * WorldScale.Kit;   // 마을 격자 × 킷 배율(랩 B)
        public const float FlaxHalfZ = 3.0f * WorldScale.Kit;   // 마을 격자 × 킷 배율(랩 B)

        /// <summary>아마밭 뙈기를 덮는 세기(0~1) — 사각 뙈기라 가장자리만 부드럽게 흘린다.</summary>
        public static float FlaxCoverAt(float wx, float wz)
        {
            const float fade = 1.4f;
            float tx = 1f - Mathf.Clamp01((Mathf.Abs(wx - FlaxX) - FlaxHalfX) / fade);
            float tz = 1f - Mathf.Clamp01((Mathf.Abs(wz - FlaxZ) - FlaxHalfZ) / fade);
            float wobble = (Mathf.PerlinNoise(wx * 0.35f + 31f, wz * 0.35f + 7f) - 0.5f) * 0.18f;
            return Mathf.Clamp01(Mathf.Min(tx, tz) + wobble);
        }

        /// <summary>채광 현장(갱구·광차 자리) — 배치 코드와 지표가 **같은 좌표**를 읽는다.</summary>
        public static readonly Vector2 MinePit = new Vector2(WorldRegions.Mine.X + 1.5f, WorldRegions.Mine.Z);
        public const float MinePitRadius = 9f;

        /// <summary>이 지역에 해당하는 도포 레이어.</summary>
        public static int LayerOf(string regionObject)
        {
            if (regionObject == WorldRegions.MeadowObject) return Tilled;
            if (regionObject == WorldRegions.ForestObject) return Soil;
            if (regionObject == WorldRegions.MineObject) return Gravel;
            return Grass;
        }

        /// <summary>
        /// 이 좌표를 덮는 지표(길 &gt; 지역)와 그 세기(0~1). 없으면 -1.
        /// 경계는 노이즈로 흔든다 — 원이 그대로 보이면 화면에서 「도장 찍은 자국」으로 읽힌다.
        /// </summary>
        public static int CoverAt(float wx, float wz, out float weight)
        {
            weight = 0f;
            int layer = -1;

            var regions = WorldRegions.All;
            for (int i = 0; i < regions.Length; i++)
            {
                var r = regions[i];
                float d = new Vector2(wx - r.X, wz - r.Z).magnitude;
                float wobble = (Mathf.PerlinNoise(wx * 0.05f + i * 13.7f, wz * 0.05f + i * 4.3f) - 0.5f) * (r.Radius * 0.22f);
                // **광산은 「바위가 있는 곳」이 아니라 파낸 자리다**(검수·오너 2026-09-09: 「돌이 깔린
                // 지대인데 바닥이 녹색이면 반려」). 자갈을 지역 밖까지 번지게 해 화면에서 잔디가 아니라
                // 파낸 땅으로 읽히게 한다. 다른 지역은 그대로다.
                // 1.35로는 **현장 주변만** 드러나고 서쪽 바위 대여섯은 여전히 잔디에 박혀 있었다
                // (검수 재반려). 바위가 서 있는 범위(반경 R−2m)를 자갈이 덮도록 1.75로 넓힌다 —
                // 바위를 옮기는 게 아니라 **지표를 바위에 맞춘다**.
                float spread = r.Object == WorldRegions.MineObject ? 1.75f : 1f;
                float t = 1f - Mathf.InverseLerp(r.Radius * 0.72f * spread, r.Radius * 1.04f * spread, d + wobble);
                t = Mathf.Clamp01(t) * 0.95f * EntranceClearAt(wx, wz);
                if (t > weight)
                {
                    weight = t;
                    layer = LayerOf(r.Object);
                }
            }

            // 채광 현장 바로 앞은 **파낸 흙**이다 — 자갈만 깔면 「돌밭」이지 「캐낸 자리」로는 안 읽힌다.
            float pitWobble = (Mathf.PerlinNoise(wx * 0.18f + 41f, wz * 0.18f + 12f) - 0.5f) * 2.2f;
            float pit = 1f - Mathf.Clamp01((new Vector2(wx - MinePit.x, wz - MinePit.y).magnitude + pitWobble
                                            - MinePitRadius * 0.6f) / (MinePitRadius * 0.5f));
            pit = Mathf.Clamp01(pit) * 0.95f;
            if (pit > weight)
            {
                weight = pit;
                layer = Soil;
            }

            float flax = FlaxCoverAt(wx, wz);
            if (flax > weight)
            {
                weight = flax;
                layer = Tilled;
            }

            float hunt = HuntCoverAt(wx, wz);
            if (hunt > weight)
            {
                weight = hunt;
                layer = hunt > 0.62f ? Soil : Gravel;   // 짙은 데는 밟혀 드러난 흙, 옅은 데는 마른 자갈
            }

            float plaza = PlazaCoverAt(wx, wz);
            if (plaza > weight)
            {
                weight = plaza;
                layer = Cobble;
            }

            var routes = Routes;
            float mound = RoadMoundFade(wx, wz);
            for (int i = 0; i < routes.Length; i++)
            {
                float d = DistToSegment(wx, wz, routes[i].x, routes[i].y, routes[i].z, routes[i].w);
                float t = (1f - Mathf.Clamp01((d - RoadHalfWidth) / RoadFade)) * mound;
                if (t > weight)
                {
                    weight = t;
                    layer = Road;
                }
            }

            // **입구 앞 길은 맨 끝에서, 다른 겹을 덮는다** — 가장 구체적인 규칙이 이긴다.
            // 세기 비교로 끼워 넣었더니 던전 2가 **0.00**이었다(실측): 그 앞은 농경지 지역(0.95)과
            // 마을 길(1.00)이 지나가 0.88짜리 입구 길이 매번 졌다. 「문 앞」은 지역보다 좁은 이야기다.
            float approach = EntrancePathAt(wx, wz);
            if (approach > 0.25f)
            {
                weight = approach;
                layer = Cobble;
            }
            return layer;
        }

        /// <summary>
        /// **마을 광장 돌포장**(검수 랩 ② — 「01·02가 회색 십자로」).
        /// 왜 지형인가: 광장 바닥을 킷 `road.fbx` 판으로 깔았더니 **무늬가 안 보였다** — Kenney 모델은
        /// 색상 아틀라스라 UV가 한 점에 몰려 있어 어떤 텍스처를 씌워도 단색이 된다(2026-09-09 실측:
        /// 돌포장 무늬를 만들어 104칸을 칠했는데 화면은 회색 한 장). 지형은 UV가 제대로 있어 무늬가 산다.
        /// 모양은 굽는 쪽 `PlazaPath`와 같다: 가운데 네모 마당 + 길 넷.
        /// </summary>
        public static float PlazaCoverAt(float wx, float wz)
        {
            float mx = wx / WorldScale.Kit, mz = wz / WorldScale.Kit;
            float square = Inside(mx, -4f, 4f, mz, -4f, 4f);
            float ew = Inside(mx, -8f, 10f, mz, -1f, 1f);
            float ns = Inside(mx, -1f, 1f, mz, -7f, 11f);
            return Mathf.Max(square, Mathf.Max(ew, ns));
        }

        /// <summary>
        /// **사냥터 — 짐승이 다닌 자리**(검수 랩 ④ 「형광 초록 당구대」).
        /// 세어 보니 그 일대 지표는 **Grass 98%**였다(`OutdoorCensus` 사냥터 지표) — 도포가 한 겹이면
        /// 아무리 몹을 잘 흩어도 화면은 당구대다. 밭·광산처럼 **그 자리에서 일어나는 일**을 바닥에 적는다:
        /// 몹이 도는 안쪽은 밟혀 흙이 드러나고, 바깥으로 가면서 마른 자갈로 성기게 풀린다.
        /// 얼룩은 결정적 노이즈다 — 매 판 같은 그림이어야 검수가 같은 화면을 본다.
        /// </summary>
        public static readonly Vector2 HuntGround = new Vector2(5.88f, 70.35f);
        /// <summary>
        /// **무리 모양대로 닳는다** — 잡몹 여덟은 가로 31m·세로 10m로 서 있다(자리 원장 실측).
        /// 원으로 깔았더니 얼룩이 무리 앞쪽 잔디로 번져 「몹은 잔디에, 흙은 그 앞에」가 됐다.
        /// 다니는 모양이 곧 닳는 모양이다.
        /// </summary>
        public const float HuntRadius = 20f;
        public const float HuntRadiusZ = 11f;

        public static float HuntCoverAt(float wx, float wz)
        {
            float ex = (wx - HuntGround.x) / HuntRadius;
            float ez = (wz - HuntGround.y) / HuntRadiusZ;
            float d = Mathf.Sqrt(ex * ex + ez * ez) * HuntRadius;
            if (d > HuntRadius * 1.35f)
                return 0f;
            // 가장자리를 원으로 끊지 않는다 — 둘레를 흔들어 「자연히 닳은 자리」로 만든다.
            float wobble = (Mathf.PerlinNoise(wx * 0.07f + 91f, wz * 0.07f + 33f) - 0.5f) * 9f;
            float core = 1f - Mathf.Clamp01((d + wobble - HuntRadius * 0.45f) / (HuntRadius * 0.75f));
            if (core <= 0f)
                return 0f;
            // 안에서도 고르게 칠하지 않는다 — 성글게 벗겨진 얼룩이라야 「밟힌 자리」로 읽힌다.
            float patch = Mathf.PerlinNoise(wx * 0.16f + 7f, wz * 0.16f + 61f);
            float cover = Mathf.Clamp01(core * (0.35f + patch * 0.85f));

            // **몹이 선 자리는 확실히 드러난다**(검수 지시: 「몹이 밟고 선 것이 무엇인가」가 증상이다).
            // 얼룩만 깔았을 때 여덟 중 셋이 잔디를, 넷이 옅은 자갈을 밟고 있었다 — 발밑이 초록이면
            // 밟힌 자리를 아무리 넓혀도 「몹은 잔디에 섰다」로 읽힌다. 자리마다 다져진 원을 얹는다.
            for (int i = 0; i < HuntRoster.Spots.Length; i++)
            {
                var p = HuntRoster.World(i);
                float dd = new Vector2(wx - p.x, wz - p.y).magnitude;
                float trample = 1f - Mathf.Clamp01((dd - 1.6f) / 2.6f);
                if (trample > 0f)
                    cover = Mathf.Max(cover, 0.55f + trample * 0.45f);
            }
            return cover;
        }

        /// <summary>
        /// **밭·숲·광산은 던전 문 앞을 비켜 간다**(검수 관찰 2026-09-09: 「`09`의 문틀이 밭 이랑
        /// 한복판에 서 있다」). 세어 보니 던전 2 입구는 **남동 농경지 중심에서 14.3m, 반경 26m 안**이라
        /// 문이 밭 한가운데 서 있는 것이 맞았다(둘레 24m의 지표 59%가 갈아엎은 흙).
        ///
        /// **세계는 안 옮긴다** — 입구를 옮기면 워프·도달 게이트를 건드리고, 지역을 옮기면 그 지역의
        /// 소품 전부가 따라간다. 대신 **사람이 하는 대로** 둔다: 던전 문 앞은 갈지 않는다.
        /// 문에서 7m 안은 지역 도포를 0으로 두고 12m에서 원래대로 돌아온다(그 사이는 페이드).
        ///
        /// **반경을 더 키우지 마라**: 지역 도포 하한 게이트가 중심 반경의 절반까지를 표본으로 삼으므로,
        /// 넓히면 「밭 바닥이 초원과 같다」로 그 자가 운다(밭을 지우는 것이 목적이 아니다).
        /// </summary>
        public const float EntranceFieldClear = 7f;    // 이 안은 갈지 않는다
        public const float EntranceFieldFade = 12f;    // 여기서 원래 밭으로 돌아온다

        public static float EntranceClearAt(float wx, float wz)
        {
            float keep = 1f;
            for (int i = 0; i < 3; i++)
            {
                float ex = i == 0 ? Dungeon1.EntranceX : i == 1 ? Dungeon2.EntranceX : Dungeon3.EntranceX;
                float ez = i == 0 ? Dungeon1.EntranceZ : i == 1 ? Dungeon2.EntranceZ : Dungeon3.EntranceZ;
                float d = new Vector2(wx - ex, wz - ez).magnitude;
                keep = Mathf.Min(keep, Mathf.Clamp01((d - EntranceFieldClear) / (EntranceFieldFade - EntranceFieldClear)));
            }
            return keep;
        }

        /// <summary>
        /// **던전 입구 앞 길**(검수 관찰 2026-09-09: 「`07`의 돌길이 풀밭 위에 얹힌 갈색 깔개로 읽힌다」).
        ///
        /// 옛 길은 킷 판때기(`ground_pathTile`) 넷을 1.6m 간격으로 깐 것이었다 — 타일이 2.10m라
        /// **0.50m씩 겹쳐 6.9×2.1m 한 판**이 됐고, 밑면은 지표에 딱 붙어(±0.00m) 두께 0.11m짜리
        /// **갈색 판**이 잔디 위에 놓였다. 이 킷의 땅 타일은 제 흙바닥을 통째로 들고 다니므로
        /// 「길」이 아니라 「깔개」로 읽힐 수밖에 없다. **광장에서 이미 같은 답을 냈다**(킷 판때기 104칸을
        /// 걷고 지형에 칠했다 — `PlazaCoverAt` 머리말). 같은 처방을 입구에도 쓴다.
        ///
        /// 모양: 입구에서 **진입로 쪽으로** 뻗는 띠. 세 가지가 「땅」과 「깔개」를 가른다 —
        /// ①가장자리를 **노이즈로 흔든다**(지역 규칙과 같은 기법: 직선으로 끝나면 자로 그은 자국이다),
        /// ②끝에서 **가늘어지며 사라진다**(자른 듯 끝나면 그것도 깔개다),
        /// ③세기를 1.0까지 올리지 않는다(0.88) — 풀이 조금 비쳐야 「밟혀 드러난 땅」이 된다.
        /// </summary>
        public const float EntrancePathLength = 9.5f;   // 문 앞에서 진입로 쪽으로 뻗는 길이
        // **폭은 문에서 유도한다**(첫 판 1.5+1.3은 화면에서 「길」이 아니라 **광장 바닥**이었다 —
        // `11`이 프레임을 통째로 덮었다). 문 반폭 1.25m가 곧 사람이 지나는 폭이므로 온전한 반폭은
        // 그보다 좁게 두고(1.15m), 옅어지는 폭을 더해 **문 너비 안팎**에서 끝나게 한다.
        public const float EntrancePathHalf = 1.15f;    // 온전히 길인 반폭
        public const float EntrancePathFade = 0.95f;    // 그 바깥으로 옅어지는 폭

        public static float EntrancePathAt(float wx, float wz)
        {
            float best = 0f;
            for (int i = 0; i < 3; i++)
            {
                float ex = i == 0 ? Dungeon1.EntranceX : i == 1 ? Dungeon2.EntranceX : Dungeon3.EntranceX;
                float ez = i == 0 ? Dungeon1.EntranceZ : i == 1 ? Dungeon2.EntranceZ : Dungeon3.EntranceZ;
                float yaw = i == 0 ? Dungeon1.EntranceYaw : i == 1 ? Dungeon2.EntranceYaw : Dungeon3.EntranceYaw;
                // 진입로 방향 = **오는 사람이 서는 쪽**. 옛 정의(`-Heading(yaw)`)는 이름만 「반대쪽」이라
                // 세 입구 모두 **문 뒤로** 길을 그렸다(문 뒤 언덕을 올리자 정상까지 뻗은 띠로 드러났다,
                // `07`·`09`·`11` 상단). 앞은 이름이 아니라 좌표가 정한다 — 원장 `EntranceGeom.Front`를 읽는다.
                var approachDir = EntranceGeom.Front(ex, ez, yaw);
                float ax = approachDir.x, az = approachDir.y;
                float px = wx - ex, pz = wz - ez;
                float along = px * ax + pz * az;                 // 문에서 진입로 쪽으로 얼마나 갔나
                float side = Mathf.Abs(px * az - pz * ax);       // 길 중심선에서 옆으로 얼마나
                if (along < -1.2f || along > EntrancePathLength + 2f)
                    continue;
                // 가장자리를 흔든다 — 폭이 자리마다 ±0.55m 달라진다(지역 경계와 같은 기법).
                float wobble = (Mathf.PerlinNoise(wx * 0.22f + 7.3f * i, wz * 0.22f + 2.9f * i) - 0.5f) * 1.1f;
                float t = 1f - Mathf.Clamp01((side + wobble - EntrancePathHalf) / EntrancePathFade);
                // 끝에서 가늘어진다: 마지막 3.5m에서 0으로, 문 안쪽으로도 한 걸음만.
                float tail = 1f - Mathf.Clamp01((along - (EntrancePathLength - 3.5f)) / 3.5f);
                float head = Mathf.Clamp01((along + 1.2f) / 1.0f);
                float v = t * tail * head * 0.88f;
                if (v > best)
                    best = v;
            }
            return best;
        }

        /// <summary>
        /// **물가 모래띠 원장**(곁가지 「물가 다듬기」, 2026-09-09). 굽는 쪽에 인라인으로 살던 규칙을
        /// 여기로 옮겼다 — 굽는 쪽·자·게이트가 **같은 함수**를 읽는다(같은 로직이 두 곳에 살면 재발한다).
        ///
        /// **옛 규칙의 결함**: 띠를 **높이차**(|h − 해수면|)로만 깔았다. 그러면 띠의 **수평 폭이
        /// 둑의 경사에 반비례**한다 — 완만한 물가는 넓은 백사장이 되고, 가파른 둑에서는 폭이 0에
        /// 수렴해 **잔디가 물에 수직으로 잘린다**(§8.2). 화면에서 본 것이 그것이다(`15_lake_river`).
        ///
        /// **고친 방식**: 「띠의 폭」은 높이가 아니라 **물가로부터의 수평 거리**다. 수평 거리는
        /// 지표를 훑지 않고 그 자리 경사로 유도한다 — 물가에서 이만큼 올라왔으면 대략 `dh / tan θ`만큼
        /// 걸어온 것이다. 두 규칙 중 **넓은 쪽**을 쓴다: 평평한 여울(높이차 작음)도, 가파른 둑
        /// (수평 거리 작음)도 둘 다 물가다.
        /// </summary>
        public const float ShoreFlatMin = 0.15f;    // 높이 규칙 — 이만큼(m)은 온전히 모래
        public const float ShoreFlatMax = 2.0f;
        public const float ShoreFadeMin = 0.7f;
        public const float ShoreFadeMax = 4.2f;
        /// <summary>수평 규칙의 폭(m) — **문·길과 달리 물가는 미터로 유도할 것이 없다**: 화면에서
        /// 사람(1.4m)이 두어 걸음 걸을 만큼이 「물가」로 읽히는 최소치라 보고 2.2m에서 시작한다.</summary>
        public const float ShoreHorizMin = 1.8f;
        public const float ShoreHorizMax = 4.5f;
        public const float ShoreHorizFadeMin = 2.0f;
        public const float ShoreHorizFadeMax = 5.0f;
        /// <summary>
        /// **수평 규칙에는 뚜껑이 필요하다**(첫 판 실측). 경사가 아주 완만한 물가에서는 「걸어온 거리」가
        /// 좀처럼 안 늘어 모래가 **내륙 30m까지** 번졌다(자의 상한에 닿았다) — 백사장이 아니라 사막이다.
        /// 물가에서 이만큼을 넘으면 무슨 경사든 모래가 끝난다.
        /// </summary>
        public const float ShoreHorizCap = 9f;
        /// <summary>물가를 따라 도는 저주파 — 너른 백사장과 바위가 물까지 내려온 구간을 가른다(랩 ⑥).</summary>
        public static float ShoreBeachAt(float wx, float wz)
        {
            return Mathf.PerlinNoise(wx * 0.012f + 29.3f, wz * 0.012f + 64.1f);
        }

        /// <summary>물가 둑의 경사(tan) — 산 형태를 읽는 `MacroSlopeTan`과 달리 **둑 하나**를 읽는 2m 자.</summary>
        public static float BankSlopeTan(float wx, float wz)
        {
            return SlopeTanAt(wx, wz, 2f);
        }

        /// <summary>
        /// **옛 높이 규칙 — 반대쪽 한계 표본으로만 쓴다**(`DarkCliffPlanarAt`과 같은 자리).
        /// 굽는 쪽에서 부르지 마라: 부르면 가파른 둑에서 잔디가 다시 물에 수직으로 잘린다.
        /// </summary>
        public static float ShoreSandHeightAt(float wx, float wz)
        {
            float h = WorldTerrain.HeightAt(wx, wz);
            if (h < WorldTerrain.SeaLevel)
                return 1f;                                  // 물속 바닥도 모래
            float beach = ShoreBeachAt(wx, wz);
            float flat = Mathf.Lerp(ShoreFlatMin, ShoreFlatMax, beach);
            float fade = Mathf.Lerp(ShoreFadeMin, ShoreFadeMax, beach);
            return Mathf.Max(0f, 1f - Mathf.Clamp01((h - WorldTerrain.SeaLevel - flat) / fade));
        }

        /// <summary>
        /// **모래는 안식각(34°)보다 급한 면에 못 쌓인다**(호수 절개면 셈 + 검수 판정 2026-09-09).
        /// 세어 보니 호수 둘레 30°↑ 사면 723㎡가 **모래 79%**였고 화면에서는 무늬 없는 탄색 민 벽이었다.
        /// 산 절벽은 같은 자로 바위 34%·그늘바위 29%라 결이 산다 — 차이는 **벽 판정이 50°부터**라
        /// 30~50°가 아무도 안 맡은 구간이었던 것이다.
        /// **벽 판정 하한은 안 내린다**(암벽 풀·중턱 풀·결 자가 같이 읽는 원장이다).
        /// 각은 취향이 아니라 마른 모래의 안식각에서 온다: 25°에서 줄기 시작해 34°에서 0.
        /// </summary>
        public const float ShoreSlopeFrom = 0.466f;   // tan 25°
        public const float ShoreSlopeTo = 0.675f;     // tan 34° — 마른 모래의 안식각
        public static float ShoreSlopeLimit(float wx, float wz)
        {
            float tan = MacroSlopeTan(wx, wz);
            return 1f - Mathf.Clamp01((tan - ShoreSlopeFrom) / (ShoreSlopeTo - ShoreSlopeFrom));
        }

        /// <summary>
        /// **비운 자리는 너덜로 채운다 — 무주공산을 만들지 마라**(검수 조건 2026-09-09).
        /// 모래를 걷기만 하면 그 자리에 **잔디**가 도로 올라와 수직 절벽이 된다(무주공산이 자리만
        /// 옮긴다). 걷어낸 몫이 그대로 자갈(너덜)이 되므로 **경계가 저절로 겹친다** — 두 규칙 사이에
        /// 빈 각도가 없다: 25° 아래는 모래 1·너덜 0, 34° 위는 모래 0·너덜 1, 그 사이는 나뉘어 갖는다.
        /// </summary>
        public static float ShoreScreeAt(float wx, float wz)
        {
            return ShoreSandRaw(wx, wz) * (1f - ShoreSlopeLimit(wx, wz));
        }

        public static float ShoreSandAt(float wx, float wz)
        {
            return ShoreSandRaw(wx, wz) * ShoreSlopeLimit(wx, wz);
        }

        /// <summary>
        /// **사면 상한을 안 씌운 값 — 반대쪽 한계 표본으로만 쓴다**(`ShoreSandHeightAt`과 같은 자리).
        /// 굽는 쪽에서 부르지 마라: 부르면 호수 사면이 다시 모래 79%가 된다.
        /// </summary>
        public static float ShoreSandRaw(float wx, float wz)
        {
            float h = WorldTerrain.HeightAt(wx, wz);
            if (h < WorldTerrain.SeaLevel)
                return 1f;
            float beach = ShoreBeachAt(wx, wz);
            float byHeight = ShoreSandHeightAt(wx, wz);
            // 경사가 0에 가까우면 「걸어온 거리」가 무한이 된다 — 그 자리는 높이 규칙이 답한다.
            float tan = Mathf.Max(BankSlopeTan(wx, wz), 0.05f);
            float walked = (h - WorldTerrain.SeaLevel) / tan;
            float flat = Mathf.Lerp(ShoreHorizMin, ShoreHorizMax, beach);
            float fade = Mathf.Lerp(ShoreHorizFadeMin, ShoreHorizFadeMax, beach);
            float byWalk = Mathf.Max(0f, 1f - Mathf.Clamp01((walked - flat) / fade));
            byWalk *= 1f - Mathf.Clamp01((walked - ShoreHorizCap) / 2f);
            // **모래는 벽에 안 붙는다.** 수평 규칙은 가파를수록 넓어지므로, 그대로 두면 물까지 내려온
            // 60~80° 해안 절벽을 통째로 모래로 덮는다 — 첫 판에 암벽 결 자가 0.061→0.050으로 떨어져
            // 그것을 물었다(§8.2: 벽이 점토 면으로 읽힌다). 벽 판정은 원장의 것을 그대로 쓴다.
            byWalk *= 1f - WallRockAt(wx, wz);
            return Mathf.Max(byHeight, byWalk);
        }

        /// <summary>사각형 안이면 1, 가장자리 한 칸에서 0으로 — 돌포장이 칼로 자른 듯 끝나지 않게.</summary>
        static float Inside(float x, float x0, float x1, float z, float z0, float z1)
        {
            float fade = 0.6f;
            float ix = Mathf.Min(x - x0, x1 - x);
            float iz = Mathf.Min(z - z0, z1 - z);
            float i = Mathf.Min(ix, iz);
            return Mathf.Clamp01(i / fade);
        }

        public static float DistToSegment(float px, float pz, float ax, float az, float bx, float bz)
        {
            float dx = bx - ax, dz = bz - az;
            float len2 = dx * dx + dz * dz;
            float t = len2 < 0.0001f ? 0f : Mathf.Clamp01(((px - ax) * dx + (pz - az) * dz) / len2);
            float cx = ax + dx * t, cz = az + dz * t;
            return new Vector2(px - cx, pz - cz).magnitude;
        }
    }
}
