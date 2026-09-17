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

        Material _trunk, _leafA, _leafB, _rock, _bush;

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
            _rock  = Mat(theme.Snowy ? Color.Lerp(theme.Rock, Color.white, 0.45f) : theme.Rock, 0.06f);
            _bush  = Mat(Color.Lerp(theme.Mid, leafA, 0.6f), 0.03f);

            var rng = new Rng((uint)seed);
            int placed = 0, tries = 0;
            int want = Mathf.Max(40, theme.ScatterCount);
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
            var root = new GameObject("Tree").transform;
            root.position = p;
            float h = 3.2f + rng.Float01() * 2.6f;

            var trunk = Prim(PrimitiveType.Cylinder, _trunk, root);
            trunk.localScale = new Vector3(_theme.Tree == TreeKind.Cactus ? 0.62f : 0.42f, h * 0.34f, _theme.Tree == TreeKind.Cactus ? 0.62f : 0.42f);
            trunk.localPosition = new Vector3(0f, h * 0.34f, 0f);

            switch (_theme.Tree)
            {
                case TreeKind.Pine:
                    // 침엽수: 위로 갈수록 좁아지는 층. 원뿔 프리미티브가 없어 납작한 구를 쌓는다.
                    for (int i = 0; i < 3; i++)
                    {
                        var leaf = Prim(PrimitiveType.Sphere, i % 2 == 0 ? _leafA : _leafB, root);
                        float s = (2.6f - i * 0.72f) * (0.9f + rng.Float01() * 0.2f);
                        leaf.localScale = new Vector3(s, s * 0.62f, s);
                        leaf.localPosition = new Vector3(0f, h * 0.55f + i * 0.92f, 0f);
                    }
                    break;

                case TreeKind.Cactus:
                    // 선인장: 기둥 + 팔 1~2개. 사막 테마(원작 The Sphinx 계열)에서 쓴다.
                    for (int i = 0; i < 1 + (rng.Float01() < 0.6f ? 1 : 0); i++)
                    {
                        var arm = Prim(PrimitiveType.Cylinder, _trunk, root);
                        float side = i == 0 ? 1f : -1f;
                        arm.localScale = new Vector3(0.34f, 0.72f, 0.34f);
                        arm.localPosition = new Vector3(side * 0.62f, h * 0.44f + rng.Float01() * 0.5f, 0f);
                        arm.localRotation = Quaternion.Euler(0f, 0f, side * 34f);
                    }
                    break;

                default:
                    // 활엽수: 둥근 덩어리 — 토이 톤에 맞는 실루엣(§9 "귀여움은 비례 + 색").
                    int blobs = 2 + (rng.Float01() < 0.5f ? 1 : 0);
                    for (int i = 0; i < blobs; i++)
                    {
                        var leaf = Prim(PrimitiveType.Sphere, i % 2 == 0 ? _leafA : _leafB, root);
                        float s = (2.5f - i * 0.45f) * (0.85f + rng.Float01() * 0.3f);
                        leaf.localScale = new Vector3(s, s * 0.88f, s);
                        leaf.localPosition = new Vector3((rng.Float01() - 0.5f) * 0.5f, h * 0.62f + i * 0.85f, (rng.Float01() - 0.5f) * 0.5f);
                    }
                    break;
            }
            root.localRotation = Quaternion.Euler(0f, rng.Float01() * 360f, 0f);
            return root;
        }

        Transform Rock(Vector3 p, ref Rng rng)
        {
            var root = new GameObject("Rock").transform;
            root.position = p;
            int n = 1 + (rng.Float01() < 0.6f ? 1 : 0);
            for (int i = 0; i < n; i++)
            {
                var r = Prim(PrimitiveType.Cube, _rock, root);
                float s = 0.9f + rng.Float01() * 1.6f;
                r.localScale = new Vector3(s, s * (0.55f + rng.Float01() * 0.5f), s * (0.7f + rng.Float01() * 0.6f));
                r.localPosition = new Vector3((rng.Float01() - 0.5f) * 1.4f, s * 0.3f, (rng.Float01() - 0.5f) * 1.4f);
                r.localRotation = Quaternion.Euler(rng.Float01() * 24f - 12f, rng.Float01() * 360f, rng.Float01() * 24f - 12f);
            }
            return root;
        }

        Transform Bush(Vector3 p, ref Rng rng)
        {
            var root = new GameObject("Bush").transform;
            root.position = p;
            int n = 2 + (rng.Float01() < 0.5f ? 1 : 0);
            for (int i = 0; i < n; i++)
            {
                var b = Prim(PrimitiveType.Sphere, _bush, root);
                float s = 0.8f + rng.Float01() * 0.9f;
                b.localScale = new Vector3(s, s * 0.7f, s);
                b.localPosition = new Vector3((rng.Float01() - 0.5f) * 1.2f, s * 0.3f, (rng.Float01() - 0.5f) * 1.2f);
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
        public int DestroyNear(Vector3 center, float radius)
        {
            int n = 0;
            float r2 = radius * radius;
            for (int i = _items.Count - 1; i >= 0; i--)
                if ((_items[i].Pos - center).sqrMagnitude <= r2)
                {
                    if (_items[i].T != null) Destroy(_items[i].T.gameObject);
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
