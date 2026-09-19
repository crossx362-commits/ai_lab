// 지형 위 오브젝트 — 나무·바위·덤불. 전부 코드로 만든다(에셋 없음, §9 와 같은 방식).
//
// 왜 넣나: 빈 언덕은 **거리 감각을 지운다.** 3D 포격에서 "저기가 120m" 를 눈으로 재려면
// 크기를 아는 물체가 중간에 있어야 한다(§2-6 은 거리계를 주기로 했지만, 숫자만으로는 조준이 안 는다).
// 덤으로 지형 파괴가 훨씬 잘 읽힌다 — 나무가 쓸려나간 자리가 곧 폭발 반경이다.
//
// ⚠️ 규칙 세 가지를 지킨다:
//   1. **콜라이더 없음** — 지형과 같은 판단(§7-5). 포탄은 SDF 레이마칭으로만 맞는다.
//      오브젝트에 충돌을 주면 탄도가 장식에 막히고, 그건 §5 의 결정론을 깬다.
//   2. **스폰 자리 회피** — 탱크가 나무 속에서 시작하면 안 된다.
//   3. **폭발에 쓸려나간다** — 안 지우면 크레이터 위에 나무가 공중에 뜬다(§7-6-1 부유 덩어리와 같은 문제).

using System.Collections.Generic;
using UnityEngine;
using Tankfall.Sim;

namespace Tankfall.View
{
    public sealed class Scatter : MonoBehaviour
    {
        struct Item { public Transform T; public Vector3 Pos; }
        readonly List<Item> _items = new List<Item>();

        Material _trunk, _leafA, _leafB, _leafC, _rock, _rockDark, _bush, _flower;

        public int Count => _items.Count;

        MapTheme _theme;

        /// <param name="avoid">탱크 스폰 자리(이 반경 안에는 아무것도 두지 않는다)</param>
        public void Build(SdfVolume vol, float mapSize, int seed, IList<Vector3> avoid, in MapTheme theme)
        {
            Clear();
            _theme = theme;
            // 나무 색은 맵 테마를 따른다 — 사막에 초록 활엽수가 서 있으면 테마가 깨진다(MapTheme.cs 조사 근거).
            bool dry = theme.Tree == TreeKind.Cactus;
            _trunk = Mat(dry ? new Color(0.50f, 0.40f, 0.28f) : new Color(0.42f, 0.30f, 0.20f), 0f);
            var leafA = theme.Tree == TreeKind.Pine ? new Color(0.20f, 0.42f, 0.30f)
                      : dry ? new Color(0.38f, 0.56f, 0.34f) : new Color(0.30f, 0.60f, 0.28f);
            var leafB = theme.Tree == TreeKind.Pine ? new Color(0.26f, 0.50f, 0.34f)
                      : dry ? new Color(0.44f, 0.62f, 0.38f) : new Color(0.40f, 0.72f, 0.33f);
            // ⚠️ 눈이 오면 나무에도 눈이 앉아야 한다. 설원에 초록 나무가 서 있으면 날씨 표시가 거짓말이 된다.
            if (theme.Snowy)
            {
                leafA = Color.Lerp(leafA, Color.white, 0.55f);
                leafB = Color.Lerp(leafB, Color.white, 0.68f);
            }
            _leafA = Mat(leafA, 0.05f);
            _leafB = Mat(leafB, 0.05f);
            // 세 번째 잎색(노랗게 물든 개체) — 같은 초록만 200그루면 벽지가 된다. 눈이면 흰색 쪽으로.
            _leafC = Mat(theme.Snowy ? Color.Lerp(leafB, Color.white, 0.3f) : Color.Lerp(leafB, new Color(0.85f, 0.70f, 0.25f), dry ? 0.25f : 0.45f), 0.05f);
            _rock  = Mat(theme.Snowy ? Color.Lerp(theme.Rock, Color.white, 0.45f) : theme.Rock, 0.06f);
            _rockDark = Mat(theme.Snowy ? Color.Lerp(theme.RockDark, Color.white, 0.3f) : theme.RockDark, 0.04f);
            _bush  = Mat(Color.Lerp(theme.Mid, leafA, 0.6f), 0.03f);
            _flower = Mat(dry ? new Color(0.95f, 0.55f, 0.35f) : new Color(0.95f, 0.45f, 0.55f), 0.1f);

            var rng = new Rng((uint)seed);
            int placed = 0, tries = 0;
            // ⚠️ 개수는 **면적 비례**다. 맵을 280m 로 넓히고 고정 수를 쓰면 밀도가 절반이 되어 휑해진다.
            float areaK = (mapSize / 200f) * (mapSize / 200f);
            int want = Mathf.Max(40, Mathf.RoundToInt(theme.ScatterCount * areaK));
            while (placed < want && tries < want * 24)
            {
                tries++;
                float x = rng.Float01() * mapSize, z = rng.Float01() * mapSize;
                float y = TankGroundProbe.GroundBelow(vol, x, z, 90f);
                if (float.IsNegativeInfinity(y)) continue;

                // 경사가 급하면 안 심는다 — 절벽에 붙은 나무는 즉시 가짜로 보인다.
                if (Slope(vol, x, z, y) > 0.55f) continue;

                var p = new Vector3(x, y, z);
                bool near = false;
                foreach (var a in avoid) if ((a - p).sqrMagnitude < 14f * 14f) { near = true; break; }
                if (near) continue;
                foreach (var it in _items) if ((it.Pos - p).sqrMagnitude < 5.2f * 5.2f) { near = true; break; }
                if (near) continue;

                // 종류 배분. 나무·바위·덤불만 돌리면 같은 실루엣 셋이 반복돼 **벽지처럼 보인다** —
                // 저폴리 환경 팩의 표준 구성(바위/나무/덤불 + 그루터기·쓰러진 통나무·풀포기 + 인공물)을 따라
                // 작은 것들을 섞는다. 작은 것은 거리 감각에도 도움이 된다(머리말 "크기를 아는 물체").
                float roll = rng.Float01();
                float treeR = _theme.TreeRatio;
                float rest = 1f - treeR;
                Transform t;
                if (roll < treeR) t = Tree(p, ref rng);
                else
                {
                    float r2 = (roll - treeR) / Mathf.Max(0.0001f, rest);
                    t = r2 < 0.42f ? Rock(p, ref rng)
                      : r2 < 0.64f ? Bush(p, ref rng)
                      : r2 < 0.76f ? GrassTuft(p, ref rng)
                      : r2 < 0.86f ? Stump(p, ref rng)
                      : r2 < 0.94f ? FallenLog(p, ref rng)
                      : Debris(p, ref rng);
                }
                t.SetParent(transform, true);
                _items.Add(new Item { T = t, Pos = p });
                placed++;
            }
        }

        /// <summary>지면 기울기(0=평지). 네 점을 찍어 가장 큰 높이차로 잰다 — SDF 기울기를 View 에서 다시 풀지 않기 위해서다.</summary>
        static float Slope(SdfVolume vol, float x, float z, float y)
        {
            float max = 0f;
            for (int i = 0; i < 4; i++)
            {
                float dx = i < 2 ? (i == 0 ? 2f : -2f) : 0f;
                float dz = i < 2 ? 0f : (i == 2 ? 2f : -2f);
                float g = TankGroundProbe.GroundBelow(vol, x + dx, z + dz, y + 8f);
                if (float.IsNegativeInfinity(g)) return 1f;
                max = Mathf.Max(max, Mathf.Abs(g - y) / 2f);
            }
            return max;
        }

        Transform Tree(Vector3 p, ref Rng rng)
        {
            // 변주(2026-09-17): 활엽수 한 종만 심었더니 화면이 막대사탕 벽지가 됐다. 같은 테마 안에서
            // 네 형태(둥근·키 큰 포플러·옆으로 퍼진·고사목)를 섞고 잎색 셋을 돌린다. 침엽 테마도 굵기·층수를 흔든다.
            var root = new GameObject("Tree").transform;
            root.position = p;
            float h = 3.0f + rng.Float01() * 3.2f;
            float form = rng.Float01();

            float tw = _theme.Tree == TreeKind.Cactus ? 0.62f : 0.34f + rng.Float01() * 0.16f;
            var trunk = Prim(PrimitiveType.Cylinder, _trunk, root);
            trunk.localScale = new Vector3(tw, h * 0.34f, tw);
            trunk.localPosition = new Vector3(0f, h * 0.34f, 0f);

            switch (_theme.Tree)
            {
                case TreeKind.Pine:
                    // 침엽수: 위로 갈수록 좁아지는 층. 층수 3~5, 굵기 흔들림.
                    int layers = 3 + (int)(rng.Float01() * 2.99f);
                    float wide = 0.8f + rng.Float01() * 0.5f;
                    for (int i = 0; i < layers; i++)
                    {
                        var leaf = Blob(LeafMat(i, ref rng), root, ref rng);
                        float s = (2.8f - i * (1.9f / layers)) * wide;
                        leaf.localScale = new Vector3(s, s * 0.55f, s);
                        leaf.localPosition = new Vector3(0f, h * 0.5f + i * 0.85f, 0f);
                    }
                    break;

                case TreeKind.Cactus:
                    // 선인장: 기둥 + 팔 0~3개, 꽃 가끔.
                    int arms = (int)(rng.Float01() * 3.99f);
                    for (int i = 0; i < arms; i++)
                    {
                        var arm = Prim(PrimitiveType.Cylinder, _trunk, root);
                        float ang = rng.Float01() * 360f;
                        arm.localScale = new Vector3(0.32f, 0.5f + rng.Float01() * 0.5f, 0.32f);
                        arm.localPosition = Quaternion.Euler(0f, ang, 0f) * new Vector3(0.62f, 0f, 0f) + new Vector3(0f, h * 0.36f + rng.Float01() * 0.9f, 0f);
                        arm.localRotation = Quaternion.Euler(0f, ang, -34f);
                    }
                    if (rng.Float01() < 0.3f) { var f = Blob(_flower, root, ref rng); f.localScale = Vector3.one * 0.4f; f.localPosition = new Vector3(0f, h * 0.7f, 0f); }
                    break;

                default:
                    if (form < 0.12f)
                    {
                        // 고사목: 잎 없이 가지 둘. 숲에 죽은 나무가 섞이면 "살아 있는 숲"으로 읽힌다.
                        trunk.localScale = new Vector3(tw * 0.9f, h * 0.42f, tw * 0.9f); trunk.localPosition = new Vector3(0f, h * 0.42f, 0f);
                        for (int i = 0; i < 2; i++)
                        {
                            var br = Prim(PrimitiveType.Cylinder, _trunk, root);
                            float side = i == 0 ? 1f : -1f;
                            br.localScale = new Vector3(tw * 0.45f, h * 0.18f, tw * 0.45f);
                            br.localPosition = new Vector3(side * h * 0.12f, h * (0.55f + i * 0.15f), 0f);
                            br.localRotation = Quaternion.Euler(0f, 0f, side * -48f);
                        }
                    }
                    else if (form < 0.40f)
                    {
                        // 키 큰 포플러: 좁고 긴 덩어리를 세로로 쌓는다.
                        h *= 1.35f;
                        trunk.localScale = new Vector3(tw * 0.9f, h * 0.30f, tw * 0.9f); trunk.localPosition = new Vector3(0f, h * 0.30f, 0f);
                        int n = 3;
                        for (int i = 0; i < n; i++)
                        {
                            var leaf = Blob(LeafMat(i, ref rng), root, ref rng);
                            float s = (1.9f - i * 0.35f) * (0.9f + rng.Float01() * 0.2f);
                            leaf.localScale = new Vector3(s, s * 1.5f, s);
                            leaf.localPosition = new Vector3(0f, h * 0.5f + i * 1.15f, 0f);
                        }
                    }
                    else if (form < 0.62f)
                    {
                        // 옆으로 퍼진 나무: 덩어리 셋이 수평으로 벌어지고 가지가 받친다.
                        var br = Prim(PrimitiveType.Cylinder, _trunk, root);
                        br.localScale = new Vector3(tw * 0.5f, h * 0.16f, tw * 0.5f);
                        br.localPosition = new Vector3(h * 0.13f, h * 0.6f, 0f); br.localRotation = Quaternion.Euler(0f, 0f, -55f);
                        for (int i = 0; i < 3; i++)
                        {
                            var leaf = Blob(LeafMat(i, ref rng), root, ref rng);
                            float s = (2.2f - i * 0.3f) * (0.85f + rng.Float01() * 0.3f);
                            float ang = i * 120f + rng.Float01() * 40f;
                            leaf.localScale = new Vector3(s, s * 0.7f, s);
                            leaf.localPosition = Quaternion.Euler(0f, ang, 0f) * new Vector3(s * 0.45f, 0f, 0f) + new Vector3(0f, h * 0.62f + rng.Float01() * 0.5f, 0f);
                        }
                    }
                    else
                    {
                        // 둥근 활엽수(기본): 덩어리 2~4.
                        int blobs = 2 + (int)(rng.Float01() * 2.99f);
                        for (int i = 0; i < blobs; i++)
                        {
                            var leaf = Blob(LeafMat(i, ref rng), root, ref rng);
                            float s = (2.5f - i * 0.4f) * (0.85f + rng.Float01() * 0.3f);
                            leaf.localScale = new Vector3(s, s * 0.88f, s);
                            leaf.localPosition = new Vector3((rng.Float01() - 0.5f) * 0.6f, h * 0.62f + i * 0.8f, (rng.Float01() - 0.5f) * 0.6f);
                        }
                    }
                    break;
            }
            root.localRotation = Quaternion.Euler(0f, rng.Float01() * 360f, 0f);
            return root;
        }

        Material LeafMat(int i, ref Rng rng) { float r = rng.Float01(); return r < 0.15f ? _leafC : (i % 2 == 0 ? _leafA : _leafB); }

        Transform Rock(Vector3 p, ref Rng rng)
        {
            // 바위 — **정육면체를 쓰지 마라**(오너 지적 2026-09-19 "원통 박스 이런거 넣지말고").
            //   `PrimitiveType.Cube` 는 모서리가 전부 직각·같은 길이라 아무리 굴리고 늘려도 **택배 상자로 보인다.**
            //   실제로 회전·비율을 흔들어 뒀는데도 화면에서 회색 상자로 읽혔다.
            //   저폴리 환경 에셋의 바위는 **면 수가 적되 모서리 길이가 제각각인 불규칙 다면체**다(조사 근거:
            //   Synty POLYGON·KayKit 류 프롭 팩의 rock/boulder 공통 형태). 그래서 여기서 직접 굽는다.
            //
            //   덩어리 하나가 아니라 **무리**로 둔다 — 큰 덩어리 하나 + 작은 조각 몇. 자연에서 바위는 혼자 안 있다.
            var root = new GameObject("Rock").transform;
            root.position = p;
            int n = 1 + (int)(rng.Float01() * 2.99f);
            float big = 0f;
            for (int i = 0; i < n; i++)
            {
                float s = (i == 0 ? 1.0f : 0.42f) + rng.Float01() * (i == 0 ? 1.5f : 0.6f);
                if (i == 0) big = s;
                var r = Boulder(root, i > 0 && rng.Float01() < 0.5f ? _rockDark : _rock, s, ref rng);
                r.localPosition = new Vector3((rng.Float01() - 0.5f) * (1.6f + big),
                                              0f,
                                              (rng.Float01() - 0.5f) * (1.6f + big));
                r.localRotation = Quaternion.Euler(rng.Float01() * 16f - 8f, rng.Float01() * 360f, rng.Float01() * 16f - 8f);
            }
            // 눈·이끼 덮개 — 위쪽만 살짝. 덮개까지 구 하나로 두면 바위 위에 공이 얹힌 것처럼 보여
            // **납작하게** 눌러 얹는다.
            if (rng.Float01() < 0.45f)
            {
                var cap = Blob(_theme.Snowy ? _leafC : _bush, root, ref rng);
                cap.localScale = new Vector3(big * 0.92f, big * 0.22f, big * 0.82f);
                cap.localPosition = new Vector3(0f, big * 0.62f, 0f);
            }
            return root;
        }

        /// <summary>
        /// 불규칙 바위 한 덩어리. 옆면 5~7 각의 **각 모서리 반지름·높이를 따로 흔든** 기둥에
        /// 위·아래 꼭짓점을 얹어 닫는다(면 수는 적게 유지 — 저폴리 실루엣).
        /// ⚠️ 정다각형으로 두면 "원통"이 되고, 반지름을 전부 같게 두면 "상자"가 된다.
        ///    **제각각인 게 핵심이다** — 그래야 바위로 읽힌다.
        /// </summary>
        Transform Boulder(Transform parent, Material mat, float size, ref Rng rng)
        {
            int sides = 5 + (int)(rng.Float01() * 2.99f);          // 5~7
            var verts = new List<Vector3>();
            var tris = new List<int>();

            float belt = 0.30f + rng.Float01() * 0.22f;            // 허리 높이
            float topY = size * (0.55f + rng.Float01() * 0.45f);
            float botY = -size * 0.22f;                            // 조금 땅에 묻힌다 — 공중에 뜬 바위는 즉시 가짜다

            for (int i = 0; i < sides; i++)
            {
                float a = (i / (float)sides) * Mathf.PI * 2f + rng.Float01() * 0.22f;   // 각도도 흔든다(정다각형 금지)
                float rad = size * (0.55f + rng.Float01() * 0.55f);
                float y = size * belt * (0.6f + rng.Float01() * 0.8f);
                verts.Add(new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad));
            }
            int top = verts.Count; verts.Add(new Vector3((rng.Float01() - 0.5f) * size * 0.5f, topY, (rng.Float01() - 0.5f) * size * 0.5f));
            int bot = verts.Count; verts.Add(new Vector3((rng.Float01() - 0.5f) * size * 0.3f, botY, (rng.Float01() - 0.5f) * size * 0.3f));

            for (int i = 0; i < sides; i++)
            {
                int a = i, b = (i + 1) % sides;
                tris.Add(a); tris.Add(top); tris.Add(b);     // 윗면
                tris.Add(b); tris.Add(bot); tris.Add(a);     // 아랫면
            }

            var go = new GameObject("Boulder");
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var mesh = new Mesh { name = "BoulderMesh" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();                      // 평면 노멀이 아니라 부드럽게 — 면이 적어 각이 살아 있다
            mesh.RecalculateBounds();
            mf.mesh = mesh;
            return go.transform;
        }

        Transform Bush(Vector3 p, ref Rng rng)
        {
            var root = new GameObject("Bush").transform;
            root.position = p;
            int n = 2 + (int)(rng.Float01() * 2.99f);
            for (int i = 0; i < n; i++)
            {
                var b = Blob(rng.Float01() < 0.2f ? _leafC : _bush, root, ref rng);
                float s = 0.7f + rng.Float01() * 1.0f;
                b.localScale = new Vector3(s, s * 0.7f, s);
                b.localPosition = new Vector3((rng.Float01() - 0.5f) * 1.4f, s * 0.3f, (rng.Float01() - 0.5f) * 1.4f);
            }
            // 꽃: 덤불 넷 중 하나에 작은 점 몇 개 — 색 점 하나가 풀밭을 살린다.
            if (!_theme.Snowy && rng.Float01() < 0.25f)
                for (int i = 0; i < 3; i++)
                {
                    var f = Blob(_flower, root, ref rng);
                    f.localScale = Vector3.one * 0.22f;
                    f.localPosition = new Vector3((rng.Float01() - 0.5f) * 1.4f, 0.75f + rng.Float01() * 0.3f, (rng.Float01() - 0.5f) * 1.4f);
                }
            return root;
        }

        /// <summary>
        /// 풀포기 — 얇은 판 몇 장을 세워 교차시킨다. 저폴리 환경의 기본 바닥 채움이고,
        /// **작아서 거리 감각의 잣대**가 된다(큰 것만 있으면 원근이 안 읽힌다).
        /// </summary>
        Transform GrassTuft(Vector3 p, ref Rng rng)
        {
            var root = new GameObject("Grass").transform;
            root.position = p;
            if (_theme.Snowy) { /* 눈 덮인 맵에서는 성기게 — 아래 개수로 조절한다 */ }
            int n = (_theme.Snowy ? 2 : 4) + (int)(rng.Float01() * 2.99f);
            for (int i = 0; i < n; i++)
            {
                var b = Prim(PrimitiveType.Cube, rng.Float01() < 0.25f ? _leafC : _bush, root);
                float h = 0.45f + rng.Float01() * 0.55f;
                b.localScale = new Vector3(0.07f, h, 0.30f + rng.Float01() * 0.25f);
                b.localPosition = new Vector3((rng.Float01() - 0.5f) * 0.9f, h * 0.5f, (rng.Float01() - 0.5f) * 0.9f);
                b.localRotation = Quaternion.Euler(rng.Float01() * 16f - 8f, rng.Float01() * 360f, rng.Float01() * 16f - 8f);
            }
            return root;
        }

        /// <summary>그루터기 — 잘린 나무. 나무가 쓸려나간 전장이라는 이야기를 한 조각으로 말한다.</summary>
        Transform Stump(Vector3 p, ref Rng rng)
        {
            var root = new GameObject("Stump").transform;
            root.position = p;
            float r = 0.34f + rng.Float01() * 0.26f, h = 0.5f + rng.Float01() * 0.6f;
            var body = Prim(PrimitiveType.Cylinder, _trunk, root);
            body.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);       // 유니티 실린더는 높이가 2 다
            body.localPosition = new Vector3(0f, h * 0.5f, 0f);
            body.localRotation = Quaternion.Euler(rng.Float01() * 8f - 4f, rng.Float01() * 360f, rng.Float01() * 8f - 4f);
            // 잘린 단면 — 속살이 밝다. 이게 없으면 그냥 짧은 기둥으로 보인다.
            var cut = Prim(PrimitiveType.Cylinder, _rock, root);
            cut.localScale = new Vector3(r * 1.86f, 0.04f, r * 1.86f);
            cut.localPosition = new Vector3(0f, h + 0.01f, 0f);
            return root;
        }

        /// <summary>쓰러진 통나무 — 옆으로 누운 기둥 + 가끔 잔가지. 바닥에 수평선을 하나 놓아 준다.</summary>
        Transform FallenLog(Vector3 p, ref Rng rng)
        {
            var root = new GameObject("Log").transform;
            root.position = p;
            root.localRotation = Quaternion.Euler(0f, rng.Float01() * 360f, 0f);
            float r = 0.26f + rng.Float01() * 0.18f, len = 2.2f + rng.Float01() * 2.2f;
            var body = Prim(PrimitiveType.Cylinder, _trunk, root);
            body.localScale = new Vector3(r * 2f, len * 0.5f, r * 2f);
            body.localPosition = new Vector3(0f, r, 0f);
            body.localRotation = Quaternion.Euler(90f, 0f, 0f);            // 눕힌다
            if (rng.Float01() < 0.5f)
            {
                var br = Prim(PrimitiveType.Cylinder, _trunk, root);
                br.localScale = new Vector3(r * 0.7f, 0.4f, r * 0.7f);
                br.localPosition = new Vector3(0f, r * 1.5f, len * 0.25f);
                br.localRotation = Quaternion.Euler(60f, rng.Float01() * 360f, 0f);
            }
            return root;
        }

        /// <summary>
        /// 인공물 — 드럼통·나무상자. **전장이라는 정체성**을 준다(저폴리 밀리터리 팩의 공통 프롭).
        /// 아주 드물게만 둔다 — 자주 나오면 자연 지형이 창고처럼 보인다.
        /// </summary>
        Transform Debris(Vector3 p, ref Rng rng)
        {
            var root = new GameObject("Debris").transform;
            root.position = p;
            root.localRotation = Quaternion.Euler(0f, rng.Float01() * 360f, 0f);
            bool barrel = rng.Float01() < 0.5f;
            if (barrel)
            {
                var b = Prim(PrimitiveType.Cylinder, _rockDark, root);
                float h = 0.9f + rng.Float01() * 0.25f;
                b.localScale = new Vector3(0.62f, h * 0.5f, 0.62f);
                bool tipped = rng.Float01() < 0.4f;                        // 굴러 누운 것도 섞는다
                b.localPosition = new Vector3(0f, tipped ? 0.31f : h * 0.5f, 0f);
                if (tipped) b.localRotation = Quaternion.Euler(90f, 0f, 0f);
                // 테 두 줄 — 없으면 그냥 원통이다(오너 지적의 바로 그 문제)
                for (int i = 0; i < 2; i++)
                {
                    var ring = Prim(PrimitiveType.Cylinder, _rock, root);
                    ring.localScale = new Vector3(0.66f, 0.04f, 0.66f);
                    float t2 = i == 0 ? 0.32f : 0.68f;
                    ring.localPosition = tipped ? new Vector3(0f, 0.31f, (t2 - 0.5f) * h) : new Vector3(0f, h * t2, 0f);
                    ring.localRotation = tipped ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
                }
            }
            else
            {
                var c = Prim(PrimitiveType.Cube, _trunk, root);
                float s2 = 0.7f + rng.Float01() * 0.4f;
                c.localScale = new Vector3(s2, s2 * 0.85f, s2);
                c.localPosition = new Vector3(0f, s2 * 0.42f, 0f);
                c.localRotation = Quaternion.Euler(0f, rng.Float01() * 40f - 20f, rng.Float01() * 10f - 5f);
                // 널판 — 상자 면에 결을 준다. 이게 없으면 또 "박스"다.
                for (int i = 0; i < 2; i++)
                {
                    var pl = Prim(PrimitiveType.Cube, _rockDark, root);
                    pl.localScale = new Vector3(s2 * 1.02f, s2 * 0.09f, s2 * 1.02f);
                    pl.localPosition = c.localPosition + new Vector3(0f, (i == 0 ? -0.22f : 0.22f) * s2, 0f);
                    pl.localRotation = c.localRotation;
                }
            }
            return root;
        }

        /// <summary>
        /// 잎 덩어리 — **매끈한 구를 쓰지 마라**(2026-09-19 오너 지적 "그래픽 좀더 디테일하게").
        /// 유니티 기본 Sphere 는 매끈하고 완벽히 둥글어서 쌓으면 **막대사탕**으로 보인다.
        /// 저폴리 수목은 면이 적고 **모서리가 살아 있는** 덩어리다 — 여기서 직접 굽는다.
        ///
        /// 반지름 0.5 로 맞춘다(유니티 Sphere 와 같다) — 호출부의 `localScale` 계산을 그대로 쓰기 위해서다.
        /// 링 두 개(위·아래)라 위아래가 좁아지는 덩어리가 되고, 반지름을 제각각 흔들어 같은 나무가 두 번 안 나온다.
        /// </summary>
        Transform Blob(Material mat, Transform parent, ref Rng rng)
        {
            int sides = 6 + (int)(rng.Float01() * 2.99f);          // 6~8
            var v = new List<Vector3>();
            var tri = new List<int>();
            float a0 = rng.Float01() * 6.28f;

            for (int ring = 0; ring < 2; ring++)
            {
                float ry = ring == 0 ? -0.17f : 0.17f;
                float baseR = ring == 0 ? 0.46f : 0.44f;
                for (int i = 0; i < sides; i++)
                {
                    float a = a0 + (i / (float)sides) * Mathf.PI * 2f + (rng.Float01() - 0.5f) * 0.22f;
                    float rad = baseR * (0.82f + rng.Float01() * 0.36f);
                    v.Add(new Vector3(Mathf.Cos(a) * rad, ry * (0.85f + rng.Float01() * 0.3f), Mathf.Sin(a) * rad));
                }
            }
            int top = v.Count; v.Add(new Vector3((rng.Float01() - 0.5f) * 0.12f, 0.5f * (0.85f + rng.Float01() * 0.3f), (rng.Float01() - 0.5f) * 0.12f));
            int bot = v.Count; v.Add(new Vector3((rng.Float01() - 0.5f) * 0.12f, -0.5f * (0.85f + rng.Float01() * 0.3f), (rng.Float01() - 0.5f) * 0.12f));

            for (int i = 0; i < sides; i++)
            {
                int a = i, b = (i + 1) % sides;                    // 아래 링
                int c = sides + i, d = sides + (i + 1) % sides;    // 위 링
                tri.Add(a); tri.Add(c); tri.Add(b);                // 허리 띠
                tri.Add(b); tri.Add(c); tri.Add(d);
                tri.Add(c); tri.Add(top); tri.Add(d);              // 윗 뚜껑
                tri.Add(b); tri.Add(bot); tri.Add(a);              // 아랫 뚜껑
            }

            var go = new GameObject("Leaf");
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var mesh = new Mesh { name = "LeafMesh" };
            mesh.SetVertices(v);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.mesh = mesh;
            return go.transform;
        }

        Transform Prim(PrimitiveType type, Material m, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());     // ⚠️ 콜라이더 금지(머리말 규칙 1) — 탄도가 장식에 막히면 안 된다
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static Material Mat(Color c, float smooth)
        {
            var sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            var m = new Material(sh) { color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return m;
        }

        /// <summary>폭발이 쓸어낸다. 안 지우면 파인 자리 위에 나무가 공중에 뜬다(§7-6-1 과 같은 문제).</summary>
        /// <summary>
        /// 폭발 반경 안의 지물을 없앤다.
        /// ⚠️ 예전엔 `Destroy` 만 해서 **나무·바위가 소리도 파편도 없이 증발했다** — 폭발이 지형을 쓸었다는
        ///    순간이 안 보였다. 반경 가장자리 것은 쓰러뜨리고(WreckPiece), 가운데 것만 즉시 없앤다.
        ///    쓰러지는 것까지 전부 남기면 큰 굴착탄에서 파편이 수십 개가 되므로 개수를 막는다.
        /// </summary>
        public int DestroyNear(Vector3 center, float radius)
        {
            int n = 0, tossed = 0;
            const int MaxToss = 6;                 // 파편 상한 — 굴착 16m 에서 화면이 파편으로 덮이지 않게
            float r2 = radius * radius;
            for (int i = _items.Count - 1; i >= 0; i--)
                if ((_items[i].Pos - center).sqrMagnitude <= r2)
                {
                    var t = _items[i].T;
                    if (t != null)
                    {
                        var away = t.position - center; away.y = 0f;
                        if (tossed < MaxToss && away.sqrMagnitude > 0.01f)
                        {
                            // 폭심 반대쪽으로 날아가며 쓰러진다. 원본을 그대로 쓰므로 목록에서만 뺀다.
                            tossed++;
                            t.gameObject.AddComponent<WreckPiece>()
                             .Launch(away.normalized * Random.Range(4f, 9f) + Vector3.up * Random.Range(4f, 8f),
                                     new Vector3(Random.Range(-200f, 200f), Random.Range(-90f, 90f), Random.Range(-200f, 200f)), 3.2f);
                        }
                        else Destroy(t.gameObject);
                    }
                    _items.RemoveAt(i);
                    n++;
                }
            return n;
        }

        public void Clear()
        {
            foreach (var it in _items) if (it.T != null) Destroy(it.T.gameObject);
            _items.Clear();
        }
    }
}
