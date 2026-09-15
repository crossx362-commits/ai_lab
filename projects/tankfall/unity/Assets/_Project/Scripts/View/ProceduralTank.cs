// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §9
//
// 오너 요구: "탱크는 코드로 그리게" — 모델링 에셋 없이 파라미터에서 메시를 만든다.
// 부수 효과로 실루엣(§72)을 데이터로 조정할 수 있다: 포신 길이/구경만 바꿔도 역할이 읽힌다.
//
// 계층은 포탑 야우 / 포신 피치 회전 때문에 분리해야 한다:
//   Body → Turret(yaw) → Barrel(pitch) → FirePoint(발사 원점 P₀)

using System.Collections.Generic;
using UnityEngine;

namespace Tankfall.View
{
    [System.Serializable]
    public struct TankShape
    {
        public float BodyLength, BodyWidth, BodyHeight;
        public float TrackWidth, TrackHeight;
        public float TurretRadius, TurretHeight;
        public float BarrelLength, BarrelCaliber;
        public bool MuzzleBrake;

        /// <summary>밸런스형(§43) — 기준 실루엣</summary>
        public static TankShape Balanced => new TankShape
        {
            BodyLength = 5.2f, BodyWidth = 2.6f, BodyHeight = 1.1f,
            TrackWidth = 0.7f, TrackHeight = 0.8f,
            TurretRadius = 1.15f, TurretHeight = 0.85f,
            BarrelLength = 3.4f, BarrelCaliber = 0.26f,
            MuzzleBrake = true,
        };

        /// <summary>박격포형(§44) — 굵고 짧은 포신, 낮고 넓은 차체</summary>
        public static TankShape Mortar => new TankShape
        {
            BodyLength = 4.4f, BodyWidth = 3.0f, BodyHeight = 1.0f,
            TrackWidth = 0.8f, TrackHeight = 0.75f,
            TurretRadius = 1.3f, TurretHeight = 0.6f,
            BarrelLength = 1.7f, BarrelCaliber = 0.47f,
            MuzzleBrake = false,
        };

        /// <summary>정밀포격형(§48) — 바늘처럼 긴 포신</summary>
        public static TankShape Sniper => new TankShape
        {
            BodyLength = 5.6f, BodyWidth = 2.2f, BodyHeight = 0.95f,
            TrackWidth = 0.55f, TrackHeight = 0.7f,
            TurretRadius = 0.95f, TurretHeight = 0.7f,
            BarrelLength = 6.1f, BarrelCaliber = 0.18f,
            MuzzleBrake = true,
        };
    }

    public static class ProceduralTank
    {
        /// <summary>탱크 계층을 통째로 만들어 루트를 돌려준다. FirePoint 는 out 으로.</summary>
        public static Transform Build(TankShape s, Material bodyMat, Material trackMat,
                                      out Transform turret, out Transform barrel, out Transform firePoint)
        {
            var root = new GameObject("Tank").transform;

            // --- 차체 ---
            var body = MakeMesh("Body", root, bodyMat);
            var bm = new MeshBuilder();
            bm.Box(new Vector3(0, s.BodyHeight * 0.5f + s.TrackHeight * 0.4f, 0),
                   new Vector3(s.BodyWidth, s.BodyHeight, s.BodyLength));
            // 앞쪽 경사장갑 — 실루엣에서 앞뒤를 구분해준다
            bm.Wedge(new Vector3(0, s.BodyHeight * 0.5f + s.TrackHeight * 0.4f, s.BodyLength * 0.5f),
                     s.BodyWidth, s.BodyHeight * 0.9f, s.BodyLength * 0.28f);
            body.mesh = bm.ToMesh("BodyMesh");

            // --- 궤도 2개 + 보기륜 ---
            var tracks = MakeMesh("Tracks", root, trackMat);
            var tm = new MeshBuilder();
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (s.BodyWidth * 0.5f + s.TrackWidth * 0.5f - 0.05f);
                tm.Box(new Vector3(x, s.TrackHeight * 0.5f, 0),
                       new Vector3(s.TrackWidth, s.TrackHeight, s.BodyLength * 1.04f));
                int wheels = 5;
                for (int w = 0; w < wheels; w++)
                {
                    float t = wheels == 1 ? 0.5f : w / (float)(wheels - 1);
                    float z = Mathf.Lerp(-s.BodyLength * 0.42f, s.BodyLength * 0.42f, t);
                    tm.CylinderX(new Vector3(x, s.TrackHeight * 0.45f, z), s.TrackHeight * 0.42f, s.TrackWidth * 1.08f, 10);
                }
            }
            tracks.mesh = tm.ToMesh("TrackMesh");

            // --- 포탑 (yaw) ---
            turret = new GameObject("Turret").transform;
            turret.SetParent(root, false);
            turret.localPosition = new Vector3(0, s.BodyHeight + s.TrackHeight * 0.4f, -s.BodyLength * 0.05f);
            var tur = MakeMesh("TurretMesh", turret, bodyMat);
            var um = new MeshBuilder();
            um.CylinderY(Vector3.zero, s.TurretRadius, s.TurretHeight, 14);
            um.Box(new Vector3(0, s.TurretHeight * 0.5f, -s.TurretRadius * 0.9f),
                   new Vector3(s.TurretRadius * 1.1f, s.TurretHeight * 0.7f, s.TurretRadius * 0.7f)); // 뒤쪽 바스켓
            tur.mesh = um.ToMesh("TurretMesh");

            // --- 포신 (pitch) ---
            barrel = new GameObject("Barrel").transform;
            barrel.SetParent(turret, false);
            barrel.localPosition = new Vector3(0, s.TurretHeight * 0.55f, s.TurretRadius * 0.55f);
            var bar = MakeMesh("BarrelMesh", barrel, bodyMat);
            var am = new MeshBuilder();
            am.CylinderZ(new Vector3(0, 0, s.BarrelLength * 0.5f), s.BarrelCaliber * 0.5f, s.BarrelLength, 12);
            if (s.MuzzleBrake)
                am.Box(new Vector3(0, 0, s.BarrelLength * 0.93f),
                       new Vector3(s.BarrelCaliber * 2.1f, s.BarrelCaliber * 1.5f, s.BarrelLength * 0.12f));
            bar.mesh = am.ToMesh("BarrelMesh");

            firePoint = new GameObject("FirePoint").transform;
            firePoint.SetParent(barrel, false);
            firePoint.localPosition = new Vector3(0, 0, s.BarrelLength);

            return root;
        }

        /// <summary>포탄·마커용 저해상도 구. 프리미티브를 쓰면 Collider(PhysicsModule) 의존이 생긴다 —
        /// 지형에 콜라이더를 두지 않는 설계(§7-5)라 물리 모듈 자체를 끌어들이지 않는다.</summary>
        public static Mesh Ball(float radius, int seg = 8)
        {
            var b = new MeshBuilder();
            b.CylinderY(Vector3.zero, radius * 0.72f, radius * 1.6f, seg);
            b.Box(Vector3.zero, Vector3.one * (radius * 1.15f));
            return b.ToMesh("Ball");
        }

        static MeshFilter MakeMesh(string name, Transform parent, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return mf;
        }

        /// <summary>박스·실린더·쐐기만으로 충분하다(§9-2). 목표 정점 500~1,500.</summary>
        sealed class MeshBuilder
        {
            readonly List<Vector3> _v = new List<Vector3>();
            readonly List<int> _t = new List<int>();

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(_v);
                m.SetTriangles(_t, 0);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }

            public void Box(Vector3 c, Vector3 size)
            {
                Vector3 h = size * 0.5f;
                int b = _v.Count;
                for (int i = 0; i < 8; i++)
                    _v.Add(c + new Vector3((i & 1) == 0 ? -h.x : h.x,
                                           (i & 2) == 0 ? -h.y : h.y,
                                           (i & 4) == 0 ? -h.z : h.z));
                // 면마다 정점을 공유하면 노멀이 뭉개지지만, 세미 스타일라이즈드(§71)에선 이 정도가 낫다
                int[] q = { 0,2,3,1, 4,5,7,6, 0,1,5,4, 2,6,7,3, 0,4,6,2, 1,3,7,5 };
                for (int i = 0; i < q.Length; i += 4) Quad(b + q[i], b + q[i+1], b + q[i+2], b + q[i+3]);
            }

            /// <summary>앞쪽 경사장갑. +z 면이 위로 깎여 올라간다.</summary>
            public void Wedge(Vector3 c, float width, float height, float length)
            {
                int b = _v.Count;
                float hw = width * 0.5f, hh = height * 0.5f;
                _v.Add(c + new Vector3(-hw, -hh, 0)); _v.Add(c + new Vector3(hw, -hh, 0));
                _v.Add(c + new Vector3(-hw, hh, 0));  _v.Add(c + new Vector3(hw, hh, 0));
                _v.Add(c + new Vector3(-hw, -hh, length)); _v.Add(c + new Vector3(hw, -hh, length));
                Quad(b + 0, b + 2, b + 3, b + 1);
                Quad(b + 4, b + 5, b + 3, b + 2);
                Quad(b + 0, b + 1, b + 5, b + 4);
                Tri(b + 0, b + 4, b + 2); Tri(b + 1, b + 3, b + 5);
            }

            public void CylinderY(Vector3 c, float r, float h, int seg) => Cylinder(c, r, h, seg, 1);
            public void CylinderX(Vector3 c, float r, float len, int seg) => Cylinder(c, r, len, seg, 0);
            public void CylinderZ(Vector3 c, float r, float len, int seg) => Cylinder(c, r, len, seg, 2);

            void Cylinder(Vector3 c, float r, float len, int seg, int axis)
            {
                int b = _v.Count;
                float hl = len * 0.5f;
                for (int i = 0; i < seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    float u = Mathf.Cos(a) * r, w = Mathf.Sin(a) * r;
                    _v.Add(c + Axis(axis, -hl, u, w));
                    _v.Add(c + Axis(axis, hl, u, w));
                }
                for (int i = 0; i < seg; i++)
                {
                    int i0 = b + i * 2, i1 = b + ((i + 1) % seg) * 2;
                    Quad(i0, i0 + 1, i1 + 1, i1);
                }
                // 뚜껑
                int capA = _v.Count; _v.Add(c + Axis(axis, -hl, 0, 0));
                int capB = _v.Count; _v.Add(c + Axis(axis, hl, 0, 0));
                for (int i = 0; i < seg; i++)
                {
                    int i0 = b + i * 2, i1 = b + ((i + 1) % seg) * 2;
                    Tri(capA, i1, i0);
                    Tri(capB, i0 + 1, i1 + 1);
                }
            }

            static Vector3 Axis(int axis, float along, float u, float w)
                => axis == 0 ? new Vector3(along, u, w)
                 : axis == 1 ? new Vector3(u, along, w)
                             : new Vector3(u, w, along);

            void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }
            void Tri(int a, int b, int c) { _t.Add(a); _t.Add(b); _t.Add(c); }
        }
    }
}
