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

                float roll = rng.Float01();
                var t = roll < _theme.TreeRatio ? Tree(p, ref rng)
                      : roll < _theme.TreeRatio + (1f - _theme.TreeRatio) * 0.6f ? Rock(p, ref rng)
                      : Bush(p, ref rng);
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
                        var leaf = Prim(PrimitiveType.Sphere, LeafMat(i, ref rng), root);
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
                    if (rng.Float01() < 0.3f) { var f = Prim(PrimitiveType.Sphere, _flower, root); f.localScale = Vector3.one * 0.4f; f.localPosition = new Vector3(0f, h * 0.7f, 0f); }
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
                            var leaf = Prim(PrimitiveType.Sphere, LeafMat(i, ref rng), root);
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
                            var leaf = Prim(PrimitiveType.Sphere, LeafMat(i, ref rng), root);
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
                            var leaf = Prim(PrimitiveType.Sphere, LeafMat(i, ref rng), root);
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
            // 바위: 큰 덩어리 + 작은 조각들(어두운 톤 섞음) + 이끼/눈 덮개 가끔. 상자 하나면 택배 상자로 보인다.
            var root = new GameObject("Rock").transform;
            root.position = p;
            int n = 1 + (int)(rng.Float01() * 2.99f);
            for (int i = 0; i < n; i++)
            {
                var r = Prim(PrimitiveType.Cube, i > 0 && rng.Float01() < 0.5f ? _rockDark : _rock, root);
                float s = (i == 0 ? 1.1f : 0.5f) + rng.Float01() * (i == 0 ? 1.6f : 0.7f);
                r.localScale = new Vector3(s, s * (0.5f + rng.Float01() * 0.5f), s * (0.7f + rng.Float01() * 0.6f));
                r.localPosition = new Vector3((rng.Float01() - 0.5f) * 1.8f, s * 0.28f, (rng.Float01() - 0.5f) * 1.8f);
                r.localRotation = Quaternion.Euler(rng.Float01() * 30f - 15f, rng.Float01() * 360f, rng.Float01() * 30f - 15f);
                if (i == 0 && rng.Float01() < 0.45f)
                {
                    var cap = Prim(PrimitiveType.Sphere, _theme.Snowy ? _leafC : _bush, root);
                    cap.localScale = new Vector3(s * 0.8f, s * 0.25f, s * 0.7f);
                    cap.localPosition = r.localPosition + new Vector3(0f, s * 0.42f, 0f);
                }
            }
            return root;
        }

        Transform Bush(Vector3 p, ref Rng rng)
        {
            var root = new GameObject("Bush").transform;
            root.position = p;
            int n = 2 + (int)(rng.Float01() * 2.99f);
            for (int i = 0; i < n; i++)
            {
                var b = Prim(PrimitiveType.Sphere, rng.Float01() < 0.2f ? _leafC : _bush, root);
                float s = 0.7f + rng.Float01() * 1.0f;
                b.localScale = new Vector3(s, s * 0.7f, s);
                b.localPosition = new Vector3((rng.Float01() - 0.5f) * 1.4f, s * 0.3f, (rng.Float01() - 0.5f) * 1.4f);
            }
            // 꽃: 덤불 넷 중 하나에 작은 점 몇 개 — 색 점 하나가 풀밭을 살린다.
            if (!_theme.Snowy && rng.Float01() < 0.25f)
                for (int i = 0; i < 3; i++)
                {
                    var f = Prim(PrimitiveType.Sphere, _flower, root);
                    f.localScale = Vector3.one * 0.22f;
                    f.localPosition = new Vector3((rng.Float01() - 0.5f) * 1.4f, 0.75f + rng.Float01() * 0.3f, (rng.Float01() - 0.5f) * 1.4f);
                }
            return root;
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
