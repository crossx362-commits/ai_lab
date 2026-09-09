using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class VisualSliceBuilder
    {
        /// <summary>
        /// **절벽에 물건을 얹어 세로줄을 끊는다**(검수 랩 ⑦ — 랩 ⑥ ①이 미결로 남았다).
        ///
        /// 지형 도포는 평면(XZ) 투영이라 80° 절벽에서 무늬가 세로로 늘어난다. 무늬를 두 겹으로
        /// 갈라 봤지만(랩 ⑥) **수치만 39%→2%로 움직이고 화면은 그대로 세로줄**이었다 — 무늬끼리
        /// 겹쳐도 늘어난 방향이 같으면 눈에는 한 방향 결이다. 그래서 이번엔 **가로로 얹히는 실루엣**을
        /// 세운다: 암벽에 박힌 큰 바위와 그 아래 너덜(작은 돌 무리).
        ///
        /// 자리는 **코드가 유도한다**(사냥터 엄폐물에서 배운 것): 산 띠를 결정적 순서로 훑어
        /// 급경사 지점을 고르고 최소 간격을 둔다. 무작위 산포는 하필 필요한 면을 비운다.
        /// </summary>
        public const float CliffRockSlopeMin = 32f;   // 이보다 가파른 자리에만 — 완만한 데 놓으면 그냥 들바위다
        public const float CliffRockGap = 11f;        // 바위끼리 최소 간격(m)
        // **상한이 낮으면 훑는 순서대로 한쪽 면만 채워진다.** 34개로 끊었더니 격자를 서쪽(x=−136)부터
        // 훑는 순서 그대로 동북 사면만 바위가 서고 **가장 크게 보이는 앞쪽 절벽이 비었다**(샷으로 확인).
        // 자리를 고르는 자에 「먼저 찾은 것이 이긴다」가 들어 있으면 상한은 곧 편향이다.
        public const int CliffRockMax = 90;

        /// <summary>절벽 바위가 설 자리 — 굽는 쪽·재는 쪽이 **같은 함수**를 읽는다.</summary>
        public static List<Vector3> CliffRockSpots()
        {
            var spots = new List<Vector3>();
            // 격자를 훑는 순서가 곧 우선순위다 — 같은 판이면 같은 자리가 나온다.
            for (float x = -136f; x <= 136f; x += 4f)
            {
                for (float z = -136f; z <= 136f; z += 4f)
                {
                    float h = WorldTerrain.HeightAt(x, z);
                    if (h < WorldTerrain.LandBase + 8f)
                        continue;
                    float dh = Mathf.Max(Mathf.Abs(WorldTerrain.HeightAt(x + 2f, z) - h),
                                         Mathf.Abs(WorldTerrain.HeightAt(x, z + 2f) - h));
                    if (Mathf.Atan2(dh, 2f) * Mathf.Rad2Deg < CliffRockSlopeMin)
                        continue;
                    bool near = false;
                    for (int i = 0; i < spots.Count && !near; i++)
                        near = new Vector2(spots[i].x - x, spots[i].z - z).magnitude < CliffRockGap;
                    if (near)
                        continue;
                    spots.Add(new Vector3(x, h, z));
                    if (spots.Count >= CliffRockMax)
                        return spots;
                }
            }
            return spots;
        }

        /// <summary>
        /// 멱등 보수 패스 — 매 판 지우고 같은 자리에 다시 세운다(셀프체크가 씬을 되돌리므로).
        /// **저장 앞에서** 불러야 한다: 뒤에 두면 자는 초록인데 QA 샷은 옛 산을 찍는다.
        /// </summary>
        public static void ScatterCliffRocks()
        {
            // **암석 재질이 이미 꽂힌 킷 셋만 쓴다.** Nature 킷 바위(`rock_largeA`)를 섞었더니
            // 그 모델의 dirt·grass 서브메시가 무텍스처라 재질 자가 228개로 울었다 —
            // `EnsureWorldPropMaterials`가 칠하는 목록은 FantasyTown 셋이고, 새 모델을 쓰려면
            // 그 목록부터 늘려야 한다. 여기서 예외를 만들지 않는다.
            const string Wide = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-wide.fbx";
            const string Large = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-large.fbx";
            const string Small = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-small.fbx";

            var old = GameObject.Find("CliffRocks");
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            var parent = new GameObject("CliffRocks").transform;

            var spots = CliffRockSpots();
            int placed = 0;
            for (int i = 0; i < spots.Count; i++)
            {
                var p = spots[i];
                // 가로로 넓은 덩어리와 둥근 덩어리를 번갈아 — 같은 실루엣이 반복되면 그 자체가 무늬다.
                string model = (i % 3 == 0) ? Large : Wide;
                var go = Place(model, new Vector3(p.x, 0f, p.z), new Vector3(0f, WorldRegions.Rand(i, 81, 0f, 360f), 0f));
                if (go == null)
                    continue;
                go.name = "CliffRock" + (i + 1);
                go.transform.SetParent(parent, true);
                // 절벽 크기에 맞추되 **너무 키우지 않는다** — 2.2~4.0배로 세웠더니 암벽에 박힌 바위가
                // 아니라 「붙여 놓은 흰 상자」로 보였다(샷). 실루엣을 끊는 데 필요한 것은 크기가 아니라
                // 가로로 얹힌 모양과 **개수**다.
                go.transform.localScale = go.transform.localScale * WorldRegions.Rand(i, 82, 1.3f, 2.3f);
                PaintCliffRock(go);
                BedInSlope(go, i);
                placed++;

                // 그 아래 너덜 — 큰 덩어리 하나만 놓으면 「붙여 놓은 것」이고, 부스러기가 있어야
                // 「무너져 쌓인 자리」로 읽힌다. 경사 아래쪽(중심에서 먼 쪽)으로 흘린다.
                // 너덜은 큰 덩어리 셋에 하나꼴로 — 전부에 딸리면 산 전체가 자갈밭이 된다.
                if (i % 3 != 0)
                    continue;
                var outward = new Vector2(p.x, p.z).normalized;
                for (int k = 0; k < 3; k++)
                {
                    float d = 3.5f + k * 2.6f;
                    var q = new Vector3(p.x + outward.x * d + WorldRegions.Rand(i * 4 + k, 83, -1.8f, 1.8f), 0f,
                                        p.z + outward.y * d + WorldRegions.Rand(i * 4 + k, 84, -1.8f, 1.8f));
                    var s = Place(Small, q, new Vector3(0f, WorldRegions.Rand(i * 4 + k, 85, 0f, 360f), 0f));
                    if (s == null)
                        continue;
                    s.name = "CliffScree" + (i + 1) + "_" + (k + 1);
                    s.transform.SetParent(parent, true);
                    s.transform.localScale = s.transform.localScale * WorldRegions.Rand(i * 4 + k, 86, 0.8f, 1.8f);
                    PaintCliffRock(s);
                    BedInSlope(s, i * 4 + k);
                }
            }
            Debug.Log("[Ulon] 절벽 바위 — 자리 " + spots.Count + "곳에 큰 덩어리 " + placed +
                      "개 + 너덜, 모두 " + parent.childCount + "개");
        }

        /// <summary>
        /// **산과 같은 톤으로 칠한다.** 마을 바위 재질(`MountainRockProp`, 0.34~0.58)을 그대로 쓰면
        /// 암벽(도포 0.16~0.48)보다 훨씬 밝아 **흰 상자를 붙여 놓은 것**으로 보인다(샷 두 판으로 확인).
        /// 같은 모델이라도 **어디에 서느냐에 따라 밝기가 달라야** 그 자리의 돌로 읽힌다.
        /// 야외 색조 자의 하한(텍스처까지 곱한 밝기 0.30)은 지킨다 — 더 어둡게는 못 간다.
        /// </summary>
        static void PaintCliffRock(GameObject go)
        {
            var mat = MakeNoiseMat("CliffRockProp", new Color(0.26f, 0.25f, 0.23f), new Color(0.50f, 0.47f, 0.43f), 1);
            if (mat == null)
                return;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
                rends[i].sharedMaterial = mat;
        }

        /// <summary>
        /// **경사면에 박아 넣는다.** `Place`는 바운드 바닥을 지표에 맞추는데(SnapRootToGround),
        /// 경사면에서는 그 「지표」가 바운드 중심의 한 점이라 위쪽 모서리가 허공에 뜬다 —
        /// 급경사일수록 심하다. 그래서 제 높이를 **직접 읽고 일부러 묻는다**.
        /// </summary>
        static void BedInSlope(GameObject go, int seed)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0)
                return;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
                b.Encapsulate(rends[i].bounds);
            var p = go.transform.position;
            float ground = WorldTerrain.HeightAt(p.x, p.z);
            // 발치를 지표에 맞춘 뒤 높이의 1/4을 묻는다 — 경사면에서 위쪽 모서리가 뜨는 만큼을 상쇄한다.
            float footOffset = p.y - b.min.y;
            go.transform.position = new Vector3(p.x, ground + footOffset - b.size.y * 0.25f, p.z);
            // 비탈에 얹힌 바위는 반듯이 서 있지 않다 — 결정적으로 조금 기울인다.
            // 많이 기울이면 상자가 굴러떨어지는 모양이 된다 — 비탈에 얹힌 만큼만.
            go.transform.rotation = Quaternion.Euler(WorldRegions.Rand(seed, 87, -8f, 8f),
                                                     go.transform.eulerAngles.y,
                                                     WorldRegions.Rand(seed, 88, -8f, 8f));
        }
    }
}
