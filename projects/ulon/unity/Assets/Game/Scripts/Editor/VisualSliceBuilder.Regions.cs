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
            for (int p = 0; p < plots.Length; p++)
            {
                float half = 6.5f;
                // 울타리는 **폭을 재서 이어 붙인다**. 간격을 추측해 띄우면 화면에서 「밭을 두른 울타리」가 아니라
                // 들판에 말뚝이 흩어진 것으로 읽힌다(검수 반려 4의 첫 샷이 그랬다).
                FenceRun(parent, fence, new Vector2(plots[p].x - half, plots[p].y - half), new Vector2(plots[p].x + half, plots[p].y - half));
                FenceRun(parent, fence, new Vector2(plots[p].x - half, plots[p].y + half), new Vector2(plots[p].x + half, plots[p].y + half));
                FenceRun(parent, fence, new Vector2(plots[p].x - half, plots[p].y - half), new Vector2(plots[p].x - half, plots[p].y + half));
                FenceRun(parent, fence, new Vector2(plots[p].x + half, plots[p].y - half), new Vector2(plots[p].x + half, plots[p].y + half));
                for (int row = 0; row < 8; row++)
                {
                    for (int col = 0; col < 8; col++)
                    {
                        var pos = new Vector3(plots[p].x - 5.25f + col * 1.5f, 0f, plots[p].y - 5.25f + row * 1.5f);
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
            // 군락 7개·나무 5~9그루로는 「나무가 좀 있는 들판」이다(첫 샷). 숲으로 읽히려면 수관이 서로 겹쳐야 한다.
            for (int cluster = 0; cluster < 14; cluster++)
            {
                float ca = WorldRegions.Rand(cluster, 11, 0f, 360f) * Mathf.Deg2Rad;
                float cd = WorldRegions.Rand(cluster, 12, 3f, r.Radius - 5f);
                var center = new Vector2(r.X + Mathf.Cos(ca) * cd, r.Z + Mathf.Sin(ca) * cd);
                int count = Mathf.RoundToInt(WorldRegions.Rand(cluster, 13, 9f, 15f));
                for (int i = 0; i < count; i++)
                {
                    index++;
                    float a = WorldRegions.Rand(index, 14, 0f, 360f) * Mathf.Deg2Rad;
                    float d = WorldRegions.Rand(index, 15, 0.6f, 5.2f);
                    var pos = new Vector3(center.x + Mathf.Cos(a) * d, 0f, center.y + Mathf.Sin(a) * d);
                    var go = Place(trees[index % trees.Length], pos, new Vector3(0f, WorldRegions.Rand(index, 16, 0f, 360f), 0f));
                    if (go == null)
                        continue;
                    go.transform.SetParent(parent, true);
                    float sc = WorldRegions.Rand(index, 17, 0.85f, 1.35f);
                    go.transform.localScale = go.transform.localScale * sc;
                }
                Decor(parent, cluster % 2 == 0 ? bush : bushLarge, new Vector3(center.x + 3.2f, 0f, center.y - 2.4f), new Vector3(0f, 40f, 0f));
                Decor(parent, tuft, new Vector3(center.x - 2.6f, 0f, center.y + 3.1f), new Vector3(0f, 120f, 0f));
            }
        }

        /// <summary>광산/산지 — 산자락 바위 노두와 채광 광맥.</summary>
        static void BuildMine(WorldRegions.Region r, string rockLarge, string rockWide, string poles, string tuft)
        {
            var parent = FreshRegion(r);
            // 각도를 완전 무작위로 뽑으면 한쪽에 몰린다(사분면 게이트가 2개로 잡아냈다) — 사분면을 돌아가며 놓는다.
            for (int i = 0; i < 28; i++)
            {
                float a = ((i % 4) * 90f + WorldRegions.Rand(i, 21, 5f, 85f)) * Mathf.Deg2Rad;
                float d = WorldRegions.Rand(i, 22, 3f, r.Radius - 3f);
                var pos = new Vector3(r.X + Mathf.Cos(a) * d, 0f, r.Z + Mathf.Sin(a) * d);
                var go = Place(i % 3 == 0 ? rockWide : rockLarge, pos, new Vector3(0f, WorldRegions.Rand(i, 23, 0f, 360f), 0f));
                if (go == null)
                    continue;
                go.transform.SetParent(parent, true);
                go.transform.localScale = go.transform.localScale * WorldRegions.Rand(i, 24, 0.9f, 2.1f);
            }
            // 갱구 표시 — 기둥 두 개와 널판(마을에서 「저기가 광산」으로 읽히게).
            Decor(parent, poles, new Vector3(r.X - 2.2f, 0f, r.Z - 1.4f), new Vector3(0f, 0f, 0f));
            Decor(parent, poles, new Vector3(r.X + 2.2f, 0f, r.Z - 1.4f), new Vector3(0f, 0f, 0f));
            for (int i = 0; i < 4; i++)
                Decor(parent, tuft, new Vector3(r.X + (i - 1.5f) * 3f, 0f, r.Z + 6f), new Vector3(0f, i * 40f, 0f));

            // 채광 광맥 3개 — 지역이 「장소」이려면 할 일이 있어야 한다(§6.1 광산은 채광 지역이다).
            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector3(r.X + (i - 1) * 5.5f, 0f, r.Z + 2.4f);
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
                if (distVillage < 26f)
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

        static GameObject DecorLocal(Transform parent, string path, Vector3 localPos, Vector3 localEuler)
        {
            var go = Place(path, Vector3.zero, Quaternion.Euler(localEuler));
            if (go == null)
                return null;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            return go;
        }

        static void PlaceHouse(Transform parent, Vector3 sw, float yaw, int depth, string wall, string door, string roof, string chimney, bool tall)
        {
            var root = new GameObject("House");
            root.transform.SetParent(parent, false);
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
            DecorLocal(hp, Overhang, new Vector3(1.5f, floors, 0.05f), new Vector3(0f, 90f, 0f));
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
                    go.transform.localScale = new Vector3(width, 1f, 1f);
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
                    roofY = Mathf.Max(roofY, c.localPosition.y);
                    maxZ = Mathf.Max(maxZ, c.localPosition.z);
                }
                if (pieces.Count == 0)
                    continue;
                int depth = Mathf.Max(2, Mathf.RoundToInt(maxZ + 0.5f));
                // **증상이 아니라 목표 상태로 잰다**(검수 조건 2026-09-09 — 곱하지 말고 맞춰라):
                // 성한 지붕은 깊이 칸마다 **한 장씩**, 용마루가 집 한가운데(x=폭/2)를 지나고, 그 한 장이
                // 집 폭만큼 늘어나 있다. 증상 목록으로 재면 고친 뒤의 모양이 또 증상으로 걸린다.
                bool ok = pieces.Count == depth;
                for (int i = 0; ok && i < pieces.Count; i++)
                    ok = Mathf.Abs(pieces[i].localPosition.x - Width * 0.5f) < 0.05f
                         && Mathf.Abs(pieces[i].localScale.x - Width) < 0.05f;
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
