using System;
using System.Collections.Generic;
using System.IO;
using Ulon.Client;
using Ulon.Server;
using Ulon.Shared;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// **VisualSliceBuilder 분할**(오너 상시 지시 2026-09-08, 파일 비대화 정리).
// 이 파일이 담는 것: 세계 지역(테스트장·초원·숲·광산)·평야 산포·집 조립 — 마을 밖 장소.
// 동작 변경 0 — 구간을 순서 그대로 옮기기만 했다(순서를 바꾸면 주석과 몸통의 짝이 깨진다).
namespace Ulon.Editor
{
    public static partial class VisualSliceBuilder
    {
        /// <summary>
        /// 기획서 §6.1 지역을 **실제 장소로** 만든다(검수 2026-09-06 반려 4: 「지역 이름만 있고 화면은 빈 초록」).
        /// 배치는 결정적이다 — 매번 흔들리면 검수가 같은 화면을 못 본다.
        /// </summary>
        public static void EnsureWorldRegions()
        {
            const string TreeH = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-high.fbx";
            const string TreeR = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-high-round.fbx";
            const string TreeC = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree-crooked.fbx";
            const string Tree = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/tree.fbx";
            const string Fence = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/fence.fbx";
            const string Cart = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/cart.fbx";
            const string RockL = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-large.fbx";
            const string RockW = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-wide.fbx";
            const string Poles = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/poles.fbx";
            const string Bush = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bush.fbx";
            const string BushL = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bushLarge.fbx";
            const string Tuft = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_large.fbx";
            const string Crop = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/grass_leafs.fbx";
            string[] models = { TreeH, TreeR, TreeC, Tree, Fence, Cart, RockL, RockW, Poles, Bush, BushL, Tuft, Crop };
            for (int i = 0; i < models.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(models[i]) == null)
                    ConfigureProp(models[i]);
            }

            BuildMeadow(WorldRegions.Meadow, Fence, Crop, Tuft, Cart, Bush);
            BuildForest(WorldRegions.Forest, new[] { TreeH, TreeR, TreeC, Tree }, Bush, BushL, Tuft);
            BuildMine(WorldRegions.Mine, RockL, RockW, Poles, Tuft);
            ScatterPlain(new[] { Tuft, Bush, BushL, RockW });
            BuildTestChamber(WorldRegions.TestChamber, Fence, Poles, Cart, RockW);
        }

        /// <summary>
        /// §6.1 테스트 공간 — 표적·장비 거치대·연습 기둥을 둔 울타리 마당. GM 패널 워프로만 간다.
        /// 살아 있는 몹은 두지 않는다 — GM 패널의 스켈레톤 소환으로 그 자리에서 만들어 쓴다
        /// (상시 몹을 두면 몹 수를 세는 다른 판정들이 흔들린다).
        /// </summary>
        static void BuildTestChamber(WorldRegions.Region r, string fence, string poles, string cart, string rock)
        {
            const string Lantern = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/lantern.fbx";
            const string Banner = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/banner-red.fbx";
            const string Planks = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/planks.fbx";
            const string Stall = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/stall.fbx";
            string[] models = { fence, poles, cart, rock, Lantern, Banner, Planks, Stall };
            for (int i = 0; i < models.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(models[i]) == null)
                    ConfigureProp(models[i]);
            }

            var parent = FreshRegion(r);
            float half = 10f;
            FenceRun(parent, fence, new Vector2(r.X - half, r.Z - half), new Vector2(r.X + half, r.Z - half));
            FenceRun(parent, fence, new Vector2(r.X - half, r.Z + half), new Vector2(r.X + half, r.Z + half));
            FenceRun(parent, fence, new Vector2(r.X - half, r.Z - half), new Vector2(r.X - half, r.Z + half));
            FenceRun(parent, fence, new Vector2(r.X + half, r.Z - half), new Vector2(r.X + half, r.Z + half));

            // 표적 3개 — 스킬/무기 피해를 눈으로 보는 자리.
            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector3(r.X - 4f + i * 4f, 0f, r.Z + 6f);
                var target = Place(poles, pos, new Vector3(0f, 0f, 0f));
                if (target == null)
                    continue;
                target.name = "TestTarget" + (i + 1);
                target.transform.SetParent(parent, true);
                Decor(parent, Planks, pos + new Vector3(0f, 0f, 0.6f), new Vector3(0f, 90f, 0f));
            }
            // 장비 거치대와 작업대.
            Decor(parent, Stall, new Vector3(r.X - 6f, 0f, r.Z - 5f), new Vector3(0f, 20f, 0f));
            Decor(parent, cart, new Vector3(r.X + 6f, 0f, r.Z - 5f), new Vector3(0f, -30f, 0f));
            Decor(parent, Banner, new Vector3(r.X, 0f, r.Z - half + 0.6f), new Vector3(0f, 180f, 0f));
            for (int i = 0; i < 4; i++)
            {
                float sx = (i == 0 || i == 3) ? 1f : -1f;
                float sz = (i == 0 || i == 1) ? 1f : -1f;
                Decor(parent, Lantern, new Vector3(r.X + (half - 1.2f) * sx, 0f, r.Z + (half - 1.2f) * sz), Vector3.zero);
                var lightGo = new GameObject("TestChamberLight");
                lightGo.transform.SetParent(parent, true);
                lightGo.transform.position = OnGround(new Vector3(r.X + (half - 1.2f) * sx, 2.2f, r.Z + (half - 1.2f) * sz));
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.98f, 0.85f, 0.6f);
                light.intensity = 1.1f;
                light.range = 9f;
                light.shadows = LightShadows.None;
            }
            for (int i = 0; i < 6; i++)
            {
                float a = WorldRegions.Rand(i, 61, 0f, 360f) * Mathf.Deg2Rad;
                Decor(parent, rock, new Vector3(r.X + Mathf.Cos(a) * (half + 3f), 0f, r.Z + Mathf.Sin(a) * (half + 3f)), Vector3.zero);
            }
        }


        static Transform FreshRegion(WorldRegions.Region region)
        {
            var old = GameObject.Find(region.Object);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            var go = new GameObject(region.Object);
            return go.transform;
        }

        /// <summary>농경지 — 울타리로 두른 밭 네 뙈기와 작물 이랑.</summary>
        static void BuildMeadow(WorldRegions.Region r, string fence, string crop, string tuft, string cart, string bush)
        {
            var parent = FreshRegion(r);
            var plots = new[]
            {
                new Vector2(r.X - 11f, r.Z - 9f), new Vector2(r.X + 10f, r.Z - 10f),
                new Vector2(r.X - 10f, r.Z + 10f), new Vector2(r.X + 11f, r.Z + 9f),
            };
            // 뙈기 크기를 갈라 놓는다 — 넷이 같은 정사각형이면 화면은 「스프레드시트」로 읽힌다(검수).
            float[] halves = { 7.5f, 5.5f, 6.0f, 8.0f };
            for (int p = 0; p < plots.Length; p++)
            {
                float half = halves[p];
                // 울타리는 **폭을 재서 이어 붙인다**. 간격을 추측해 띄우면 화면에서 「밭을 두른 울타리」가 아니라
                // 들판에 말뚝이 흩어진 것으로 읽힌다(검수 반려 4의 첫 샷이 그랬다).
                FenceRun(parent, fence, new Vector2(plots[p].x - half, plots[p].y - half), new Vector2(plots[p].x + half, plots[p].y - half));
                FenceRun(parent, fence, new Vector2(plots[p].x - half, plots[p].y + half), new Vector2(plots[p].x + half, plots[p].y + half));
                FenceRun(parent, fence, new Vector2(plots[p].x - half, plots[p].y - half), new Vector2(plots[p].x - half, plots[p].y + half));
                FenceRun(parent, fence, new Vector2(plots[p].x + half, plots[p].y - half), new Vector2(plots[p].x + half, plots[p].y + half));
                // **고랑으로 심는다** — 8×8 균일 격자는 밭이 아니라 바둑판이었다. 흙 이랑 무늬(pattern 2)가
                // z를 따라 교차하므로 줄은 **x축 방향**이다: x는 촘촘히(0.95m), z는 이랑 간격(2.1m).
                int rows = Mathf.Max(2, Mathf.FloorToInt((half * 2f - 1.6f) / 2.1f));
                int cols = Mathf.Max(3, Mathf.FloorToInt((half * 2f - 1.6f) / 0.95f));
                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        var pos = new Vector3(plots[p].x - (cols - 1) * 0.475f + col * 0.95f, 0f,
                                              plots[p].y - (rows - 1) * 1.05f + row * 2.1f);
                        int seed = p * 131 + row * 11 + col;
                        var go = Place(crop, pos, new Vector3(0f, WorldRegions.Rand(seed, 1, 0f, 360f), 0f));
                        if (go == null)
                            continue;
                        go.transform.SetParent(parent, true);
                        // 작물 한 포기는 원래 무릎 아래다 — 조망에서 흙 얼룩으로 보여 이랑이 안 읽힌다. 키운다.
                        go.transform.localScale = go.transform.localScale * WorldRegions.Rand(seed, 2, 1.7f, 2.4f);
                    }
                }
            }
            Decor(parent, cart, new Vector3(r.X, 0f, r.Z), new Vector3(0f, 35f, 0f));
            for (int i = 0; i < 10; i++)
            {
                float a = WorldRegions.Rand(i, 2, 0f, 360f) * Mathf.Deg2Rad;
                float d = WorldRegions.Rand(i, 3, 8f, r.Radius);
                Decor(parent, i % 2 == 0 ? tuft : bush, new Vector3(r.X + Mathf.Cos(a) * d, 0f, r.Z + Mathf.Sin(a) * d), new Vector3(0f, a * Mathf.Rad2Deg, 0f));
            }
        }

        static float _fenceWidth = -1f;
        static bool _fenceRunsAlongZ;

        /// <summary>
        /// 울타리 한 장의 실제 폭과 **어느 축으로 누워 있는지**(모델 크기를 추측하지 않는다).
        ///
        /// 폭을 x로만 재던 옛 판은 이 킷 울타리(길이가 z축)에서 0.5m 하한에 걸려 32m 둘레에
        /// 64조각을 깔았고, yaw도 반대라 조각이 밭을 **가로질러 빗살처럼** 섰다(55 첫 샷).
        /// 「이 모델은 x로 길다」는 추측이었다 — 재서 쓴다.
        /// </summary>
        static float FenceWidth(string path)
        {
            if (_fenceWidth > 0f)
                return _fenceWidth;
            var probe = Place(path, new Vector3(0f, 0f, 0f), Vector3.zero);
            if (probe == null)
                return 2f;
            var size = CombinedBounds(probe).size;
            _fenceRunsAlongZ = size.z > size.x;
            _fenceWidth = Mathf.Max(0.5f, Mathf.Max(size.x, size.z));
            UnityEngine.Object.DestroyImmediate(probe);
            return _fenceWidth;
        }

        /// <summary>두 점을 잇는 연속된 울타리 줄.</summary>
        static void FenceRun(Transform parent, string path, Vector2 from, Vector2 to)
        {
            Vector2 d = to - from;
            float len = d.magnitude;
            if (len < 0.5f)
                return;
            float w = FenceWidth(path);
            int count = Mathf.Max(2, Mathf.CeilToInt(len / w));
            // 줄의 방향과 **모델이 누운 축**을 맞춘다(둘 중 하나만 보면 조각이 가로로 선다).
            bool runAlongX = Mathf.Abs(d.x) >= Mathf.Abs(d.y);
            float yaw = (runAlongX == _fenceRunsAlongZ) ? 90f : 0f;
            for (int i = 0; i < count; i++)
            {
                Vector2 p = from + d * ((i + 0.5f) / count);
                Decor(parent, path, new Vector3(p.x, 0f, p.y), new Vector3(0f, yaw, 0f));
            }
        }

        /// <summary>숲 — 나무 군락. 균일 격자로 심으면 과수원으로 보이니 군집을 이룬다.</summary>
        static void BuildForest(WorldRegions.Region r, string[] trees, string bush, string bushLarge, string tuft)
        {
            var parent = FreshRegion(r);
            int index = 0;
            // **빈터**(검수 2026-09-09: 「한 종 한 색 균일 밀집이라 크리스마스트리 창고」).
            // 숲이 숲으로 읽히려면 **나무가 없는 자리**가 있어야 한다 — 빈터가 있어야 밀집이 밀집으로 보인다.
            var glade = new Vector2(r.X - 4.5f, r.Z + 3f);
            const float GladeRadius = 7f;
            // 군락 7개·나무 5~9그루로는 「나무가 좀 있는 들판」이다(첫 샷). 숲으로 읽히려면 수관이 서로 겹쳐야 한다.
            for (int cluster = 0; cluster < 14; cluster++)
            {
                float ca = WorldRegions.Rand(cluster, 11, 0f, 360f) * Mathf.Deg2Rad;
                float cd = WorldRegions.Rand(cluster, 12, 3f, r.Radius - 5f);
                var center = new Vector2(r.X + Mathf.Cos(ca) * cd, r.Z + Mathf.Sin(ca) * cd);
                // 바깥 군락은 성기게, 안쪽은 빽빽하게 — 밀도가 어디나 같으면 격자처럼 읽힌다.
                float edge = Mathf.InverseLerp(r.Radius * 0.4f, r.Radius, cd);
                int count = Mathf.RoundToInt(WorldRegions.Rand(cluster, 13, 9f, 15f) * Mathf.Lerp(1f, 0.45f, edge));
                for (int i = 0; i < count; i++)
                {
                    index++;
                    float a = WorldRegions.Rand(index, 14, 0f, 360f) * Mathf.Deg2Rad;
                    float d = WorldRegions.Rand(index, 15, 0.6f, 5.2f);
                    var pos = new Vector3(center.x + Mathf.Cos(a) * d, 0f, center.y + Mathf.Sin(a) * d);
                    if (new Vector2(pos.x - glade.x, pos.z - glade.y).magnitude < GladeRadius)
                        continue;                       // 빈터에는 나무가 안 선다
                    var go = Place(trees[index % trees.Length], pos, new Vector3(0f, WorldRegions.Rand(index, 16, 0f, 360f), 0f));
                    if (go == null)
                        continue;
                    go.transform.SetParent(parent, true);
                    // 크기 편차를 넓힌다(0.85~1.35 → 0.65~1.6) — 같은 키가 늘어서면 창고 선반이 된다.
                    float sc = WorldRegions.Rand(index, 17, 0.65f, 1.6f);
                    go.transform.localScale = go.transform.localScale * sc;
                }
                Decor(parent, cluster % 2 == 0 ? bush : bushLarge, new Vector3(center.x + 3.2f, 0f, center.y - 2.4f), new Vector3(0f, 40f, 0f));
                Decor(parent, tuft, new Vector3(center.x - 2.6f, 0f, center.y + 3.1f), new Vector3(0f, 120f, 0f));
            }

            // 빈터는 **비워 두기만 하면 맨땅**이다 — 사람이 벌목하는 자리로 읽히게 장작과 수레를 둔다.
            const string Planks = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/planks.fbx";
            const string CartWood = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/cart.fbx";
            Decor(parent, Planks, new Vector3(glade.x - 1.4f, 0f, glade.y + 0.8f), new Vector3(0f, 25f, 0f));
            Decor(parent, Planks, new Vector3(glade.x + 1.6f, 0f, glade.y - 0.6f), new Vector3(0f, 100f, 0f));
            Decor(parent, CartWood, new Vector3(glade.x + 0.2f, 0f, glade.y + 2.6f), new Vector3(0f, 200f, 0f));
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f * Mathf.Deg2Rad;
                Decor(parent, tuft, new Vector3(glade.x + Mathf.Cos(a) * 4.2f, 0f, glade.y + Mathf.Sin(a) * 4.2f),
                    new Vector3(0f, i * 63f, 0f));
            }
        }

        /// <summary>광산/산지 — 산자락 바위 노두와 채광 광맥.</summary>
        static void BuildMine(WorldRegions.Region r, string rockLarge, string rockWide, string poles, string tuft)
        {
            var parent = FreshRegion(r);
            // **갱구 자리**(산자락 쪽, 마을을 보게) — 아래 바위 산포가 이 앞을 비운다.
            // 자리는 **지역 한가운데**다 — 처음엔 산자락(중심에서 8m 동쪽)에 세웠더니 화면 구석에서
            // 손톱만 하게 잡혔다. 「있는데 안 보이면 그건 배치·크기 문제」(검수).
            // 자리는 **지표 원장과 같은 좌표**를 읽는다(`WorldSplat.MinePit`) — 파낸 흙과 현장이
            // 각자 좌표를 들고 있으면 언젠가 갈린다. 「한 값은 한 곳에서만 정해진다」.
            var mouth = new Vector3(WorldSplat.MinePit.x, 0f, WorldSplat.MinePit.y);
            // 각도를 완전 무작위로 뽑으면 한쪽에 몰린다(사분면 게이트가 2개로 잡아냈다) — 사분면을 돌아가며 놓는다.
            // 바위는 **28개 → 18개, 최대 2.1배 → 1.5배**로 줄였다. 예전 판은 갱구·광차·광맥을 통째로
            // 가렸고, 사람 1.8m보다 세 배 큰 돌덩이가 스물여덟 개 서 있으면 화면은 채석장으로 읽힌다.
            for (int i = 0; i < 18; i++)
            {
                float a = ((i % 4) * 90f + WorldRegions.Rand(i, 21, 5f, 85f)) * Mathf.Deg2Rad;
                float d = WorldRegions.Rand(i, 22, 8f, r.Radius - 2f);
                var pos = new Vector3(r.X + Mathf.Cos(a) * d, 0f, r.Z + Mathf.Sin(a) * d);
                // **갱구 앞과 광차 길은 비운다** — 여기가 막히면 화면은 다시 「들판에 부은 돌」이 된다
                // (검수 2026-09-09: 바위 28개가 갱구 기둥 둘과 광맥 셋을 통째로 삼키고 있었다).
                if (new Vector2(pos.x - mouth.x, pos.z - mouth.z).magnitude < 7.5f)
                    continue;
                if (pos.x > r.X && pos.x < mouth.x && Mathf.Abs(pos.z - r.Z) < 3.2f)
                    continue;
                var go = Place(i % 3 == 0 ? rockWide : rockLarge, pos, new Vector3(0f, WorldRegions.Rand(i, 23, 0f, 360f), 0f));
                if (go == null)
                    continue;
                go.transform.SetParent(parent, true);
                go.transform.localScale = go.transform.localScale * WorldRegions.Rand(i, 24, 0.8f, 1.5f);
            }
            // **갱구**(검수 지시 2026-09-09: 「20_mine이 광산이 아니라 회색 돌무더기다」).
            // 세어 보니 물건은 있었다 — 기둥 2·광맥 3 — 그런데 최대 2.1배로 커진 바위 28개에 묻혀
            // 화면에서 하나도 안 읽혔다. 그래서 **지역이 그 지역으로 읽히는 물건**을 세운다:
            // 바위 절벽 두 짝 사이의 갱도 입구 + 버팀목·널판 더미 + 등불, 그 앞에 광차와 광석 더미.
            // 조각은 전부 **땅에 서는 것만** 쓴다 — `Place`는 뿌리를 지표에 붙이므로(SnapRootToGround)
            // 상인방처럼 띄우는 조각은 발 높이 게이트와 싸우게 된다.
            const string Cart = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/cart.fbx";
            const string CartHigh = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/cart-high.fbx";
            const string Planks = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/planks.fbx";
            const string RockSmall = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/rock-small.fbx";
            // **미결: 갱도 입구 조형이 없다**(2026-09-09, 검수 「20_mine이 광산이 아니다」 랩).
            // 시도 셋이 이렇게 깨졌다. ㉠ `wall-arch`·`wall-block`은 Kenney 흰 재질 그대로라 잔디 위
            // 흰 상자로 읽혔다(이 저장소 단골 반려). ㉡ 암반 면을 큐브로 세우니 소품 자격 게이트가
            // 물었다(「색칠 큐브는 소품이 아니다」 §8.2 — 옳다). ㉢ 등록된 바위 두 짝을 세워 그 사이를
            // 비워 봤지만, 볼록한 바위로는 **구멍이 안 만들어진다** — 화면엔 큰 돌이 둘 더 늘 뿐이었다.
            // 자산 전수: 이 저장소에 mine/cave/tunnel/entrance 메시는 **0건**이다. 그러니 지금은
            // **갱구 대신 「채광 현장」으로 읽히게** 한다(버팀목·광차·널판·등불·광석 더미·광맥).
            // MegaKit(대장간 모루와 같은 대기 건)이 도착하면 갱도 입구가 최우선 후보다.
            // 갱도 문틀 — 버팀목 두 짝이 틈 앞에 서서 「사람이 판 구멍」으로 읽히게 한다.
            Decor(parent, poles, mouth + new Vector3(-1.1f, 0f, -0.9f), Vector3.zero);
            Decor(parent, poles, mouth + new Vector3(1.1f, 0f, -0.9f), Vector3.zero);
            Decor(parent, poles, mouth + new Vector3(-0.8f, 0f, -2.6f), Vector3.zero);
            Decor(parent, poles, mouth + new Vector3(-0.8f, 0f, 2.6f), Vector3.zero);
            Decor(parent, Planks, mouth + new Vector3(-2.4f, 0f, 3.4f), new Vector3(0f, 20f, 0f));
            Decor(parent, Planks, mouth + new Vector3(-3.6f, 0f, -3.2f), new Vector3(0f, 70f, 0f));
            // **마을 가로등을 갱도 옆에 세우지 않는다**(검수 2026-09-09 반려: 「갱도 옆 도시 가로등」).
            // 광산의 불은 버팀목에 건 횃불이다 — 던전 벽 등불 메시를 쓰고, 야외 던전텍스처 정리 패스가
            // 마을 나무 톤으로 갈아 끼운다(`EnsureOutdoorPropMaterials` — 이름이 아니라 규칙으로 잡는다).
            const string TorchMounted = "Assets/_ThirdParty/KayKit/Dungeon/RAW/Models/torch_mounted.obj";
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var torch = Place(TorchMounted, mouth + new Vector3(-1.1f, 0f, side * 1.5f),
                    new Vector3(0f, side * 90f, 0f));
                if (torch == null)
                    continue;
                torch.name = "MineTorch" + (i + 1);
                torch.transform.SetParent(parent, true);
                torch.transform.localScale *= 1.6f;
            }
            // 광차 둘 — 「여기서 캐서 실어 나간다」가 한 장면에 들어온다.
            Decor(parent, Cart, mouth + new Vector3(-4.6f, 0f, -0.9f), new Vector3(0f, 90f, 0f));
            Decor(parent, CartHigh, mouth + new Vector3(-6.4f, 0f, 1.6f), new Vector3(0f, 70f, 0f));
            // 광석 더미 — 광맥과 같은 철광 재질로 칠해진다(`EnsureWorldPropMaterials`의 이름 목록).
            for (int i = 0; i < 4; i++)
            {
                var pile = Place(RockSmall, mouth + new Vector3(-3.2f - i * 0.9f, 0f, -2.2f + i * 1.3f),
                    new Vector3(0f, i * 55f, 0f));
                if (pile == null)
                    continue;
                pile.name = "MineOrePile" + (i + 1);
                pile.transform.SetParent(parent, true);
                pile.transform.localScale *= 0.55f;
            }
            for (int i = 0; i < 4; i++)
                Decor(parent, tuft, new Vector3(r.X + (i - 1.5f) * 3f, 0f, r.Z + 6f), new Vector3(0f, i * 40f, 0f));

            // 채광 광맥 3개 — 지역이 「장소」이려면 할 일이 있어야 한다(§6.1 광산은 채광 지역이다).
            // **갱구 옆으로 옮겼다** — 예전 자리(지역 한가운데)는 바위 밭 속이라 안 읽혔다.
            var veinSpots = new[]
            {
                mouth + new Vector3(-1.6f, 0f, -4.6f),
                mouth + new Vector3(-2.0f, 0f, 4.8f),
                mouth + new Vector3(-6.2f, 0f, -3.8f),
            };
            for (int i = 0; i < 3; i++)
            {
                var pos = veinSpots[i];
                var vein = Place(rockLarge, pos, new Vector3(0f, 25f * i, 0f));
                if (vein == null)
                    continue;
                vein.name = "MineVein" + (i + 1);
                vein.transform.SetParent(parent, true);
                var node = vein.GetComponent<ResourceNode>() ?? vein.AddComponent<ResourceNode>();
                node.ResourceId = "iron_ore";
                node.DisplayName = "광산 철광맥";
                node.GatherSkill = SkillId.Mining;
                node.Remaining = 14;
                node.Capacity = 14;
                node.RespawnSeconds = 10f;
                node.Difficulty = 14f;
                EnsureCollider(vein);
            }
        }

        /// <summary>평지 산포 — 마을 밖 빈 초록을 깨는 잡초·덤불·돌. 지역·마을·던전 자리는 피한다.</summary>
        static void ScatterPlain(string[] props)
        {
            var old = GameObject.Find("PlainScatter");
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            var parent = new GameObject("PlainScatter").transform;
            var regions = WorldRegions.All;
            int placed = 0;
            for (int i = 0; i < 460; i++)
            {
                float x = WorldRegions.Rand(i, 31, -84f, 84f);
                float z = WorldRegions.Rand(i, 32, -84f, 84f);
                float distVillage = Mathf.Sqrt(x * x + z * z);
                if (distVillage < 26f * KitScale)          // 마을 가드도 마을과 같은 배로(랩 B)
                    continue;                                  // 마을·광장은 그대로 둔다
                bool inRegion = false;
                for (int k = 0; k < regions.Length; k++)
                {
                    if (new Vector2(x - regions[k].X, z - regions[k].Z).magnitude < regions[k].Radius)
                        inRegion = true;
                }
                if (inRegion)
                    continue;
                if (Mathf.Abs(Mathf.Abs(x) - 68f) < 16f && Mathf.Abs(Mathf.Abs(z) - 68f) < 16f)
                    continue;                                  // 던전 뚜껑 자리
                float h = WorldTerrain.HeightAt(x, z);
                if (h < WorldTerrain.SeaLevel + 1f || h > WorldTerrain.LandBase + 6f)
                    continue;                                  // 물가·산비탈은 제외
                var go = Place(props[i % props.Length], new Vector3(x, 0f, z), new Vector3(0f, WorldRegions.Rand(i, 33, 0f, 360f), 0f));
                if (go == null)
                    continue;
                go.transform.SetParent(parent, true);
                go.transform.localScale = go.transform.localScale * WorldRegions.Rand(i, 34, 0.8f, 1.6f);
                placed++;
            }
            Debug.Log("[Ulon] 평지 산포 " + placed + "개");
        }

        static void Decor(Transform parent, string path, Vector3 pos, Vector3 euler)
        {
            var go = Place(path, pos, Quaternion.Euler(0f, euler.y, 0f));
            if (go == null)
                return;
            go.transform.SetParent(parent, true);
        }

        /// <summary>
        /// **모듈 좌표 → 월드 좌표**(랩 B). 마을은 통째로 킷의 1m 격자 위에 설계돼 있다 —
        /// 집 벽은 `x=0.5·1.5`, 도로는 `x+0.5`, 좌판·울타리도 정수 자리다. 킷이 `KitScale`배가 되면
        /// **간격도 같은 배로 늘어나야** 벽이 서로 파고들지 않는다. 그래서 눈대중으로 좌표를 다시
        /// 찍지 않고, 모듈 좌표를 그대로 두고 **여기 한 곳에서** 월드로 옮긴다(자가 하나면 안 갈린다).
        /// 지형·던전 방·사냥터는 실 미터라 이 문을 지나가지 않는다.
        /// </summary>
        public static Vector3 Module(Vector3 modulePos) => modulePos * KitScale;

        public static Vector3 Module(float x, float y, float z) => new Vector3(x, y, z) * KitScale;

        /// <summary>모듈 좌표로 놓는 `Decor` — 마을 채우기·도로·울타리가 쓴다.</summary>
        static void DecorM(Transform parent, string path, Vector3 modulePos, Vector3 euler)
        {
            Decor(parent, path, Module(modulePos), euler);
        }

        static GameObject DecorLocal(Transform parent, string path, Vector3 localPos, Vector3 localEuler)
        {
            var go = Place(path, Vector3.zero, Quaternion.Euler(localEuler));
            if (go == null)
                return null;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Module(localPos);   // 집 안쪽도 모듈 격자다
            go.transform.localRotation = Quaternion.Euler(localEuler);
            return go;
        }

        static void PlaceHouse(Transform parent, Vector3 sw, float yaw, int depth, string wall, string door, string roof, string chimney, bool tall)
        {
            var root = new GameObject("House");
            root.transform.SetParent(parent, false);
            sw = Module(sw);                                 // 집이 서는 자리도 마을 격자다(랩 B)
            root.transform.SetPositionAndRotation(OnGround(new Vector3(sw.x, 0f, sw.z)), Quaternion.Euler(0f, yaw, 0f));
            Transform hp = root.transform;
            int width = 2;
            int floors = tall ? 2 : 1;
            const string Overhang = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/overhang.fbx";
            const string Banner = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/banner-red.fbx";
            string gable = roof.IndexOf("roof-high", StringComparison.Ordinal) >= 0
                ? "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/roof-high-gable-end.fbx"
                : "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/roof-gable-end.fbx";
            for (int floor = 0; floor < floors; floor++)
            {
                float y = floor;
                for (int z = 0; z < depth; z++)
                {
                    DecorLocal(hp, wall, new Vector3(0.5f, y, z + 0.5f), Vector3.zero);
                    DecorLocal(hp, wall, new Vector3(width - 0.5f, y, z + 0.5f), new Vector3(0f, 180f, 0f));
                }
                for (int x = 0; x < width; x++)
                {
                    string south = floor == 0 && x == 1 ? door : wall;
                    DecorLocal(hp, south, new Vector3(x + 0.5f, y, 0.5f), new Vector3(0f, 90f, 0f));
                    DecorLocal(hp, wall, new Vector3(x + 0.5f, y, depth - 0.5f), new Vector3(0f, 270f, 0f));
                }
            }
            float roofY = floors;
            PlaceHouseRoof(hp, roof, gable, roofY, depth);
            DecorLocal(hp, chimney, new Vector3(1.65f, roofY, depth - 0.55f), Vector3.zero);
            // 차양은 **처마 아래**다 — 벽 꼭대기(= 지붕이 시작하는 높이)에 붙였더니 널판이 지붕면을
            // 뚫고 나와 「지붕에서 판때기가 튀어나온 집」으로 읽혔다(검수 관찰 2026-09-09 `62_house_roof`).
            DecorLocal(hp, Overhang, new Vector3(1.5f, floors - 0.32f, 0.05f), new Vector3(0f, 90f, 0f));
            if (tall)
                DecorLocal(hp, Banner, new Vector3(1f, floors + 0.35f, 0.15f), new Vector3(0f, 180f, 0f));
            SnapRootToGround(root);
        }

        /// <summary>
        /// 민가 지붕 한 채를 놓는 **단 하나의 규칙** — 새로 짓는 집(`PlaceHouse`)과 이미 지어진 집을
        /// 고치는 패스(`EnsureHouseRoofs`)가 같은 함수를 부른다(자가 둘이면 언젠가 갈린다).
        ///
        /// 박공(gable-end)은 **용마루가 끝나는 칸**을 지붕 조각 대신 채우는 마감재다 — 지붕 칸과 같은
        /// 방향으로 놓는다. 2026-09-09까지는 용마루 한가운데(x=1.0)에 90°/270°로 돌려 세워 지붕을
        /// 가로지르는 판때기가 됐다(실측: 박공 1.13×1.16×1.12가 z 0.0~1.1·1.05~2.15를 덮어 네 지붕
        /// 조각과 겹침 → `48_person_Healer`의 「한 장짜리 판」).
        /// </summary>
        static void PlaceHouseRoof(Transform hp, string roof, string gable, float roofY, int depth, int width = 2)
        {
            // 조각 하나가 **제 용마루를 가진 1칸짜리 지붕**이다(실측: 앞에서 보면 삼각형이 선다).
            // 그래서 x로 둘을 나란히 놓으면 용마루가 둘이 되어 지붕이 **M자**로 읽히고 그 골에 벽·굴뚝이
            // 드러난다(검수 지적 2026-09-09). 한 채는 **용마루 하나**여야 하므로, 폭 방향으로는 조각을
            // 나누지 않고 **한 장을 집 폭만큼 늘려** 덮고, 깊이(z) 방향으로만 칸을 잇는다.
            for (int z = 0; z < depth; z++)
            {
                bool end = z == 0 || z == depth - 1;
                string piece = end ? gable : roof;
                float yaw = z == depth - 1 && depth > 1 ? 180f : 0f;   // 뒤쪽 마감은 반대로 돌려 닫는다
                var go = DecorLocal(hp, piece, new Vector3(width * 0.5f, roofY, z + 0.5f), new Vector3(0f, yaw, 0f));
                if (go != null)
                    go.transform.localScale = new Vector3(width, 1f, 1f) * KitScale;   // 킷 배율 위에 폭만 늘린다
            }
        }

        /// <summary>
        /// 이미 씬에 박혀 있는 민가의 지붕을 **위 규칙으로 다시 얹는다**(멱등).
        ///
        /// 씬은 커밋된 산출물이라, 배치 코드만 고치면 **마을을 통째로 다시 드레싱할 때까지** 화면은
        /// 그대로다. 그런데 재드레싱은 랜드마크(대장간·잡화)를 잃는 등 부작용이 커서 지붕 하나 고치자고
        /// 돌릴 것이 못 된다(2026-09-09 실측). 그래서 다른 `Ensure*` 패스처럼 **자기 자리만 고치는**
        /// 패스로 둔다 — 셀프체크가 매 판 부르므로 커밋된 씬이 무엇이든 지붕은 규칙대로 수렴한다.
        /// </summary>
        public static void EnsureHouseRoofs()
        {
            const string Town = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/";
            const int Width = 2;
            // 집 목록을 **먼저 다 모으고** 나서 고친다 — 훑는 배열에는 지울 조각들도 들어 있어서,
            // 훑으면서 지우면 다음 원소가 이미 죽은 참조가 된다(MissingReferenceException, 2026-09-09).
            var found = new List<Transform>();
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "House" || t.name == Ulon.Shared.HousingPlot.HouseObject)
                    found.Add(t);
            int fixedHouses = 0, houses = found.Count;
            for (int hi = 0; hi < found.Count; hi++)
            {
                var t = found[hi];
                var pieces = new List<Transform>();
                bool high = false;
                float roofY = 0f, maxZ = 0f;
                foreach (Transform c in t)
                {
                    if (!c.name.StartsWith("roof", StringComparison.Ordinal))
                        continue;
                    pieces.Add(c);
                    high |= c.name.IndexOf("roof-high", StringComparison.Ordinal) >= 0;
                    // 씬의 로컬 좌표는 **모듈 × KitScale**이다 — 규칙은 모듈 단위로 세워져 있으니 되돌려 읽는다.
                    roofY = Mathf.Max(roofY, c.localPosition.y / KitScale);
                    maxZ = Mathf.Max(maxZ, c.localPosition.z / KitScale);
                }
                if (pieces.Count == 0)
                    continue;
                int depth = Mathf.Max(2, Mathf.RoundToInt(maxZ + 0.5f));
                // **증상이 아니라 목표 상태로 잰다**(검수 조건 2026-09-09 — 곱하지 말고 맞춰라):
                // 성한 지붕은 깊이 칸마다 **한 장씩**, 용마루가 집 한가운데(x=폭/2)를 지나고, 그 한 장이
                // 집 폭만큼 늘어나 있다. 증상 목록으로 재면 고친 뒤의 모양이 또 증상으로 걸린다.
                bool ok = pieces.Count == depth;
                for (int i = 0; ok && i < pieces.Count; i++)
                    ok = Mathf.Abs(pieces[i].localPosition.x - Width * 0.5f * KitScale) < 0.05f
                         && Mathf.Abs(pieces[i].localScale.x - Width * KitScale) < 0.05f;
                if (ok)
                    continue;
                for (int i = 0; i < pieces.Count; i++)
                    UnityEngine.Object.DestroyImmediate(pieces[i].gameObject);
                PlaceHouseRoof(t, Town + (high ? "roof-high.fbx" : "roof.fbx"),
                    Town + (high ? "roof-high-gable-end.fbx" : "roof-gable-end.fbx"), roofY, depth, Width);
                fixedHouses++;
            }
            if (houses == 0)
                throw new InvalidOperationException("민가를 한 채도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            Debug.Log("[Ulon] 민가 지붕 — " + houses + "채 중 " + fixedHouses + "채를 용마루 한 줄로 다시 얹음(고칠 것이 없으면 0채)");
        }

        static void ReplaceNamedWithModel(string name, string fbx, System.Action<GameObject> setup)
        {
            var old = GameObject.Find(name);
            Vector3 pos = old != null ? old.transform.position : Vector3.zero;
            Vector3 euler = old != null ? old.transform.eulerAngles : Vector3.zero;
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);
            var go = Place(fbx, pos, euler);
            if (go == null)
                throw new InvalidOperationException("모델 없음: " + fbx);
            go.name = name;
            EnsureCollider(go);
            setup(go);
        }

        static void EnsureCollider(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>() != null)
                return;
            var filters = go.GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 0)
            {
                go.AddComponent<BoxCollider>();
                return;
            }
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh == null)
                    continue;
                var mc = filters[i].gameObject.GetComponent<MeshCollider>();
                if (mc == null)
                    mc = filters[i].gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = filters[i].sharedMesh;
            }
        }
    }
}
