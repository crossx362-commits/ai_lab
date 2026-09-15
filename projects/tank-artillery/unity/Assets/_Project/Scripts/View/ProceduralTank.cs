// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §9
//
// 오너 요구: "탱크는 코드로 그리게" — 모델링 에셋 없이 파라미터에서 메시를 만든다.
// 부수 효과로 실루엣(§72)을 데이터로 조정할 수 있다: 포신 길이/구경만 바꿔도 역할이 읽힌다.
//
// 계층은 포탑 야우 / 포신 피치 회전 때문에 분리해야 한다:
//   Body → Turret(yaw) → Barrel(pitch) → FirePoint(발사 원점 P₀)

using System.Collections.Generic;
using UnityEngine;
using Tankfall.Sim;

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

        // --- 실루엣 식별자(§72) ---
        // 색은 팀을 뜻하므로 종 구분에 쓸 수 없다(아군 3대가 전부 파랑이다).
        // **형태만으로** 갈려야 한다. 아래 넷은 정면·측면·위 어디서 봐도 다르게 보이도록 고른 것이다.
        public int WheelCount;          // 보기륜 수 — 측면에서 길이감을 만든다
        public float ShoulderBox;       // 어깨 장갑 두께(0=없음) — 박격포를 넓어 보이게
        public float TailBox;           // 후방 카운터웨이트 길이(0=없음) — 정밀포격을 길어 보이게
        public int MastHeight;          // 포탑 위 식별 기둥 칸수 — 위에서 내려다봐도 종이 읽힌다
        public int BumperStyle;         // 앞 범퍼: 0 없음 · 1 통나무(고전·근대) · 2 쇠막대(현대) · 3 라이트바(미래)

        /// <summary>
        /// === 귀여운 비례(포트리스 참고) ===
        /// 실제 전차 비례(길이:폭 = 2:1)는 진지하고 길쭉해 보인다. 포트리스 탱크가 귀여운 이유는
        ///   · 차체가 **짧고 넓다**(길이:폭 ≈ 1.2:1) — 땅딸막해진다
        ///   · 포탑이 차체에 비해 **크다** — 머리 큰 캐릭터와 같은 원리
        ///   · 궤도가 **두껍다** — 통통해 보인다
        /// 세 가지다. 실루엣 구분은 그대로 유지하면서 이 비례만 적용한다.
        ///
        /// ⚠️ **종류로 조회한다.** 예전엔 `shapes[i]` 와 `kinds[i]` 두 배열을 손으로 맞췄는데,
        ///    3종일 땐 버텼어도 13종에서는 반드시 어긋난다 — 어긋나면 박격포처럼 생긴 저격포가 나오고
        ///    "실루엣으로 상대 성능을 읽는다"는 §72 가 통째로 거짓말이 된다. 배열 짝맞춤을 없앤다.
        /// </summary>
        public static TankShape Of(TankKind k)
        {
            var sh = Base(k);
            // 범퍼는 계열 표식 — 실루엣 넷(바퀴수·어깨·꼬리·기둥) 위에 다섯 번째 단서
            var era = TankStats.EraOf(k);
            sh.BumperStyle = era == TankEra.Classic || era == TankEra.Early ? 1 : era == TankEra.Modern ? 2 : 3;
            return sh;
        }

        static TankShape Base(TankKind k)
        {
            switch (k)
            {
                // ── 고전 ── 작고 투박하다. 보기륜이 적고 기둥이 낮다
                case TankKind.Catapult:      // 투석기 — 포신이 거의 없고 던지는 팔이 크다
                    return Make(2.9f, 3.0f, 1.2f, 1.05f, 1.0f, 1.45f, 1.0f, 1.2f, 0.90f, false, 3, 0.55f, 0f, 3);
                case TankKind.CrossBow:      // 쇠뇌 — 좌우로 벌어진 활대 + 가느다란 볼트
                    return Make(3.2f, 2.5f, 1.0f, 0.85f, 0.9f, 1.00f, 0.75f, 3.6f, 0.18f, false, 3, 0.70f, 0f, 1);
                case TankKind.Cannon:        // 고전 대포 — 기본에 가깝되 포구가 굵다
                    return Make(3.1f, 2.7f, 1.15f, 0.95f, 1.0f, 1.20f, 0.95f, 2.6f, 0.52f, true, 3, 0f, 0f, 2);

                // ── 근대 ── 통통하고 평준하다
                case TankKind.Carrot:        // ★ 기준 실루엣
                    return Make(3.4f, 2.8f, 1.15f, 0.95f, 1.0f, 1.25f, 1.0f, 2.3f, 0.36f, true, 4, 0f, 0f, 1);
                case TankKind.Duke:          // 캐롯보다 한 치수 크고 뒤가 길다
                    return Make(3.6f, 2.9f, 1.2f, 1.0f, 1.05f, 1.30f, 1.05f, 2.6f, 0.42f, true, 4, 0f, 0.5f, 2);
                case TankKind.MineLander:    // 낮고 넓은 공병차 — 보기륜이 제일 많다
                    return Make(3.5f, 3.1f, 0.95f, 1.1f, 0.95f, 1.15f, 0.8f, 1.6f, 0.62f, false, 6, 0.40f, 0f, 3);

                // ── 현대 ── 길쭉한 발사대를 올렸다
                case TankKind.Missile:
                    return Make(3.8f, 2.7f, 1.1f, 0.9f, 1.0f, 1.15f, 0.95f, 3.0f, 0.55f, false, 5, 0f, 0.6f, 2);
                case TankKind.MultiMissile:  // 다연장 — 포신이 아니라 **네모난 상자**다
                    return Make(3.6f, 3.0f, 1.1f, 1.0f, 1.0f, 1.35f, 1.15f, 2.2f, 0.95f, false, 5, 0.60f, 0f, 3);
                case TankKind.SuperTank:     // 전 항목 최대. 원작대로 고를 수 없지만 그려질 수는 있어야 한다
                    return Make(4.0f, 3.1f, 1.3f, 1.1f, 1.1f, 1.40f, 1.15f, 3.2f, 0.60f, true, 6, 0.45f, 0.6f, 3);

                // ── 미래 ── 매끈하고 길다
                case TankKind.Laser:         // 가장 길고 가장 가는 포신. 차체도 낮고 길다
                    return Make(4.1f, 2.4f, 0.9f, 0.8f, 0.85f, 1.00f, 0.7f, 5.0f, 0.16f, false, 6, 0f, 1.0f, 2);
                case TankKind.IonAttacker:   // 포구가 차체만큼 굵다 — "지형이 전부 파인다"가 보이게
                    return Make(3.3f, 3.0f, 1.2f, 1.05f, 1.05f, 1.45f, 1.1f, 2.0f, 1.05f, false, 4, 0f, 0.4f, 1);
                case TankKind.Poseidon:      // 삼지창 — 기둥 셋처럼 보이게 어깨 + 높은 기둥
                    return Make(3.4f, 2.9f, 1.1f, 0.95f, 1.0f, 1.25f, 1.05f, 2.8f, 0.45f, true, 4, 0.55f, 0f, 3);
                default:                     // 세크윈드 — 날렵하고 꼬리가 길다
                    return Make(3.7f, 2.5f, 1.0f, 0.85f, 0.95f, 1.10f, 0.85f, 3.2f, 0.30f, true, 5, 0f, 0.8f, 2);
            }
        }

        static TankShape Make(float bl, float bw, float bh, float tw, float th,
                              float tr, float tuh, float barL, float barC, bool brake,
                              int wheels, float shoulder, float tail, int mast)
            => new TankShape
            {
                BodyLength = bl, BodyWidth = bw, BodyHeight = bh,
                TrackWidth = tw, TrackHeight = th,
                TurretRadius = tr, TurretHeight = tuh,
                BarrelLength = barL, BarrelCaliber = barC, MuzzleBrake = brake,
                WheelCount = wheels, ShoulderBox = shoulder, TailBox = tail, MastHeight = mast,
            };

        /// <summary>
        /// 종별 고유 차체색(포트리스 참고).
        ///
        /// ⚠️ **파랑·빨강은 쓸 수 없다.** 그 둘은 팀 색이다(띠·기둥). 종 색이 팀 색과 같으면
        ///    한 화면에서 두 정보가 섞여 둘 다 안 읽힌다 — 3v3 이라 아군 세 대가 서로 달라야 한다.
        ///    아래 13색은 전부 팀색과 색상환에서 떨어뜨려 고른 것이다.
        /// </summary>
        public static Color BodyColor(TankKind k)
        {
            switch (k)
            {
                case TankKind.Catapult:     return new Color(0.64f, 0.48f, 0.32f);  // 흙갈색
                case TankKind.CrossBow:     return new Color(0.56f, 0.59f, 0.32f);  // 올리브
                case TankKind.Cannon:       return new Color(0.50f, 0.55f, 0.60f);  // 회청
                case TankKind.Carrot:       return new Color(0.97f, 0.62f, 0.25f);  // 당근 주황
                case TankKind.Duke:         return new Color(0.34f, 0.56f, 0.38f);  // 군녹
                case TankKind.MineLander:   return new Color(0.72f, 0.67f, 0.42f);  // 카키
                case TankKind.Missile:      return new Color(0.74f, 0.76f, 0.80f);  // 은회색
                case TankKind.MultiMissile: return new Color(0.28f, 0.68f, 0.66f);  // 청록
                case TankKind.SuperTank:    return new Color(0.89f, 0.76f, 0.30f);  // 금색
                case TankKind.Laser:        return new Color(0.68f, 0.62f, 0.93f);  // 라벤더
                case TankKind.IonAttacker:  return new Color(0.80f, 0.42f, 0.78f);  // 자홍
                case TankKind.Poseidon:     return new Color(0.42f, 0.79f, 0.86f);  // 아쿠아
                default:                    return new Color(0.62f, 0.86f, 0.40f);  // 세크윈드 — 연두
            }
        }
    }

    public static class ProceduralTank
    {
        /// <summary>탱크 계층을 통째로 만들어 루트를 돌려준다. FirePoint 는 out 으로.</summary>
        /// <param name="bodyMat">**종별 고유색**(포트리스 참고). 색만 보고 기체를 알게 한다.</param>
        /// <param name="accentMat">**팀 색**. 식별 기둥과 차체 띠에만 칠한다.</param>
        /// <summary>
        /// 탱크 계층을 통째로 만들어 루트를 돌려준다. FirePoint 는 out 으로.
        ///
        /// === 장난감 비례 (오너 참고 이미지 2026-09-15) ===
        ///   · 바퀴가 몸통만큼 크고 아래로 튀어나온다 — 차체가 바퀴 위에 얹힌 느낌
        ///   · 모서리가 전부 둥글다 — 챔퍼 박스(정점 24) + 부드러운 노멀. 상자를 그대로 쓰면 장갑차가 된다
        ///   · 포신은 굵고 짧고 포구에 링이 있다
        ///   · 앞에 통나무 범퍼 (계열별로 통나무/쇠막대/라이트바)
        /// === 색 채널 (참고 이미지 그대로) ===
        ///   · 차체·포탑 = **기종색**(13색) · 바퀴 허브·포신·기둥 = **팀색**
        ///   예전 "띠" 방식은 팀 색 면적이 작아 멀리서 안 읽혔다. 바퀴+포신이면 실루엣의 40% 가 팀색이다.
        /// </summary>
        /// <param name="bodyMat">기종 고유색</param>
        /// <param name="trackMat">타이어·궤도 벨트(어두운 고무색)</param>
        /// <param name="accentMat">팀색 — 바퀴 허브·포신·기둥</param>
        /// <param name="woodMat">통나무 범퍼용. null 이면 trackMat 로 대신한다</param>
        public static Transform Build(TankShape s, Material bodyMat, Material trackMat, Material accentMat,
                                      out Transform turret, out Transform barrel, out Transform firePoint,
                                      Material woodMat = null)
        {
            var root = new GameObject("Tank").transform;
            woodMat ??= trackMat;

            // --- 바퀴: 크고 통통하게. 개수는 실루엣 식별자(§72)라 유지하되, 많을수록 작아진다 ---
            int wheels = Mathf.Max(1, s.WheelCount);
            // 참고 이미지: 바퀴 지름이 차체 높이보다 크다. 개수가 많으면 겹치지 않는 선에서 최대로.
            // 바퀴 수가 반지름을 제한한다. 이웃 바퀴와 20~30% 겹치는 건 벨트 안이라 안 보인다 — 겹침을 허용해 키운다.
            float wheelR = Mathf.Max(0.42f, Mathf.Min(s.TrackHeight * 0.88f, s.BodyLength * 0.5f / wheels * 1.5f));
            float wheelW = s.TrackWidth * 1.05f;
            float wx = s.BodyWidth * 0.5f + wheelW * 0.5f + 0.02f;   // 바깥으로 — 정면에서도 바퀴가 보이게
            float bodyY0 = wheelR * 0.95f;                       // 차체 바닥 — 바퀴가 아래로 튀어나온다

            var tires = MakeMesh("Tires", root, trackMat);
            var hubs = MakeMesh("Hubs", root, accentMat);
            var tm = new MeshBuilder(); var hm = new MeshBuilder();
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * wx;
                // 궤도 벨트 — 바퀴를 감싸는 둥근 띠. 바퀴가 아래·옆으로 삐져나오게 얇게
                tm.Chamfer(new Vector3(x, wheelR * 1.1f, 0f),
                           new Vector3(wheelW * 0.55f, wheelR * 1.3f, s.BodyLength * 0.98f + wheelR * 0.5f), wheelR * 0.35f);
                for (int w = 0; w < wheels; w++)
                {
                    float t = wheels == 1 ? 0.5f : w / (float)(wheels - 1);
                    float z = Mathf.Lerp(-s.BodyLength * 0.40f, s.BodyLength * 0.40f, t);
                    var c = new Vector3(x, wheelR, z);
                    tm.CylinderX(c, wheelR, wheelW, 14);                     // 타이어(어두움)
                    hm.CylinderX(c, wheelR * 0.66f, wheelW * 1.22f, 12);     // 허브(팀색) — 옆으로 살짝 돌출
                    hm.CylinderX(c, wheelR * 0.22f, wheelW * 1.26f, 8);      // 축 캡
                }
            }
            tires.mesh = tm.ToMesh("TireMesh");
            hubs.mesh = hm.ToMesh("HubMesh");

            // --- 차체: 둥근 상자 + 앞 경사 + 어깨·꼬리(실루엣 식별자) ---
            var body = MakeMesh("Body", root, bodyMat);
            var bm = new MeshBuilder();
            float bevel = Mathf.Min(0.26f, s.BodyHeight * 0.3f);
            bm.Chamfer(new Vector3(0, bodyY0 + s.BodyHeight * 0.5f, 0), new Vector3(s.BodyWidth, s.BodyHeight, s.BodyLength), bevel);
            // 앞 경사 — 앞뒤 구분. 챔퍼 위에 얹어 코가 낮아 보이게
            bm.Wedge(new Vector3(0, bodyY0 + s.BodyHeight * 0.55f, s.BodyLength * 0.5f - bevel * 0.5f),
                     s.BodyWidth * 0.86f, s.BodyHeight * 0.5f, s.BodyLength * 0.16f);
            if (s.ShoulderBox > 0.01f)
                for (int side = -1; side <= 1; side += 2)
                    bm.Chamfer(new Vector3(side * (s.BodyWidth * 0.5f + s.ShoulderBox * 0.5f - 0.05f), bodyY0 + s.BodyHeight * 0.8f, -s.BodyLength * 0.05f),
                               new Vector3(s.ShoulderBox, s.BodyHeight * 0.5f, s.BodyLength * 0.55f), 0.12f);
            if (s.TailBox > 0.01f)
                bm.Chamfer(new Vector3(0, bodyY0 + s.BodyHeight * 0.7f, -(s.BodyLength * 0.5f + s.TailBox * 0.5f - 0.05f)),
                           new Vector3(s.BodyWidth * 0.6f, s.BodyHeight * 0.55f, s.TailBox), 0.12f);
            body.mesh = bm.ToMesh("BodyMesh");

            // --- 앞 범퍼: 계열 표식 ---
            float bz = s.BodyLength * 0.5f + 0.16f, by = bodyY0 + s.BodyHeight * 0.32f;
            if (s.BumperStyle == 1)
            {
                var log = MakeMesh("Bumper", root, woodMat);
                var lm = new MeshBuilder();
                lm.CylinderX(new Vector3(0, by, bz), s.BodyHeight * 0.22f, s.BodyWidth * 1.15f, 10);   // 통나무
                lm.CylinderX(new Vector3(-s.BodyWidth * 0.5f, by, bz), s.BodyHeight * 0.26f, 0.12f, 10); // 나이테 캡
                lm.CylinderX(new Vector3(s.BodyWidth * 0.5f, by, bz), s.BodyHeight * 0.26f, 0.12f, 10);
                log.mesh = lm.ToMesh("BumperMesh");
            }
            else if (s.BumperStyle == 2)
            {
                var bar = MakeMesh("Bumper", root, trackMat);
                var lm = new MeshBuilder();
                lm.Chamfer(new Vector3(0, by, bz), new Vector3(s.BodyWidth * 1.1f, s.BodyHeight * 0.3f, 0.3f), 0.08f);   // 쇠막대
                bar.mesh = lm.ToMesh("BumperMesh");
            }
            else if (s.BumperStyle == 3)
            {
                var bar = MakeMesh("Bumper", root, accentMat);
                var lm = new MeshBuilder();
                lm.Chamfer(new Vector3(0, by + s.BodyHeight * 0.25f, bz - 0.05f), new Vector3(s.BodyWidth * 0.8f, 0.14f, 0.16f), 0.05f); // 라이트바
                bar.mesh = lm.ToMesh("BumperMesh");
            }

            // --- 포탑(= 둥근 캐빈). 참고 이미지처럼 차체 뒤쪽에 얹힌 상자에서 포신이 나온다 ---
            turret = new GameObject("Turret").transform;
            turret.SetParent(root, false);
            turret.localPosition = new Vector3(0, bodyY0 + s.BodyHeight, -s.BodyLength * 0.08f);
            var tur = MakeMesh("TurretMesh", turret, bodyMat);
            var um = new MeshBuilder();
            float cw = s.TurretRadius * 1.55f, cl = s.TurretRadius * 1.45f;
            um.Chamfer(new Vector3(0, s.TurretHeight * 0.5f, 0), new Vector3(cw, s.TurretHeight, cl), Mathf.Min(0.24f, s.TurretHeight * 0.32f));
            um.Chamfer(new Vector3(0, s.TurretHeight * 0.98f, -cl * 0.1f), new Vector3(cw * 0.55f, s.TurretHeight * 0.28f, cl * 0.5f), 0.08f); // 해치
            tur.mesh = um.ToMesh("TurretMesh");

            // 식별 기둥 — 칸 수 = 종, 색 = 팀
            var mast = MakeMesh("TeamMast", turret, accentMat);
            var mm = new MeshBuilder();
            for (int m = 0; m < s.MastHeight; m++)
                mm.Chamfer(new Vector3(-cw * 0.3f, s.TurretHeight * 1.1f + 0.16f + m * 0.3f, -cl * 0.22f), new Vector3(0.22f, 0.26f, 0.22f), 0.06f);
            mast.mesh = mm.ToMesh("MastMesh");

            // --- 포신: 굵은 원통 + 포미 + 포구 링 + 검은 구멍. 전부 팀색 ---
            barrel = new GameObject("Barrel").transform;
            barrel.SetParent(turret, false);
            barrel.localPosition = new Vector3(0, s.TurretHeight * 0.52f, cl * 0.45f);
            var barMf = MakeMesh("BarrelMesh", barrel, accentMat);
            var am = new MeshBuilder();
            float r = s.BarrelCaliber * 0.5f;
            am.CylinderZ(new Vector3(0, 0, s.BarrelLength * 0.5f), r, s.BarrelLength, 12);
            am.CylinderZ(new Vector3(0, 0, s.BarrelLength * 0.12f), r * 1.5f, s.BarrelLength * 0.2f, 12);            // 포미(굵은 뿌리)
            float ringR = r * (s.MuzzleBrake ? 1.75f : 1.35f);
            am.CylinderZ(new Vector3(0, 0, s.BarrelLength * 0.9f), ringR, s.BarrelLength * 0.17f, 12);              // 포구 링
            barMf.mesh = am.ToMesh("BarrelMesh");
            var bore = MakeMesh("Bore", barrel, trackMat);
            var om = new MeshBuilder();
            om.CylinderZ(new Vector3(0, 0, s.BarrelLength * 0.985f), r * 0.62f, s.BarrelLength * 0.05f, 10);      // 구멍(어두운 원)
            bore.mesh = om.ToMesh("BoreMesh");

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
            b.Box(Vector3.zero, Vector3.one * (radius * 1.5f));
            b.Box(Vector3.zero, new Vector3(radius * 0.9f, radius * 0.9f, radius * 2.4f));
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

            /// <summary>
            /// 모서리 12개를 깎은 상자(정점 24). RecalculateNormals 가 깎인 면 사이를 부드럽게 이어
            /// 저폴리로도 둥글어 보인다 — 장난감 비례의 핵심.
            /// 감기 방향은 기하로 판정한다(Out): 면 24개의 순서를 손으로 맞추다 하나 틀리면 구멍이 난다.
            /// </summary>
            public void Chamfer(Vector3 c, Vector3 size, float bevel)
            {
                Vector3 h = size * 0.5f;
                float b = Mathf.Min(bevel, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.9f);
                int b0 = _v.Count;
                for (int k = 0; k < 8; k++)      // 꼭짓점 k = sx | sy<<1 | sz<<2, 각 3정점(X면·Y면·Z면)
                {
                    float sx = (k & 1) == 0 ? -1f : 1f, sy = (k & 2) == 0 ? -1f : 1f, sz = (k & 4) == 0 ? -1f : 1f;
                    _v.Add(c + new Vector3(sx * h.x, sy * (h.y - b), sz * (h.z - b)));
                    _v.Add(c + new Vector3(sx * (h.x - b), sy * h.y, sz * (h.z - b)));
                    _v.Add(c + new Vector3(sx * (h.x - b), sy * (h.y - b), sz * h.z));
                }
                int V(int sx, int sy, int sz, int f) => b0 + (sx | sy << 1 | sz << 2) * 3 + f;
                // 면 6개
                for (int sgn = 0; sgn < 2; sgn++)
                {
                    QuadOut(V(sgn,0,0,0), V(sgn,1,0,0), V(sgn,1,1,0), V(sgn,0,1,0), c);
                    QuadOut(V(0,sgn,0,1), V(1,sgn,0,1), V(1,sgn,1,1), V(0,sgn,1,1), c);
                    QuadOut(V(0,0,sgn,2), V(1,0,sgn,2), V(1,1,sgn,2), V(0,1,sgn,2), c);
                }
                // 모서리 12개 + 꼭짓점 8개
                for (int i = 0; i < 2; i++)
                    for (int j = 0; j < 2; j++)
                    {
                        QuadOut(V(i,j,0,0), V(i,j,1,0), V(i,j,1,1), V(i,j,0,1), c);   // z 방향 모서리(X면-Y면)
                        QuadOut(V(0,i,j,1), V(1,i,j,1), V(1,i,j,2), V(0,i,j,2), c);   // x 방향(Y면-Z면)
                        QuadOut(V(i,0,j,0), V(i,1,j,0), V(i,1,j,2), V(i,0,j,2), c);   // y 방향(X면-Z면)
                    }
                for (int k = 0; k < 8; k++) TriOut(b0 + k * 3, b0 + k * 3 + 1, b0 + k * 3 + 2, c);
            }

            void QuadOut(int a, int b, int c, int d, Vector3 center)
            {
                Vector3 n = Vector3.Cross(_v[b] - _v[a], _v[c] - _v[a]);
                Vector3 mid = (_v[a] + _v[b] + _v[c] + _v[d]) * 0.25f;
                if (Vector3.Dot(n, mid - center) >= 0f) Quad(a, b, c, d); else Quad(a, d, c, b);
            }
            void TriOut(int a, int b, int c, Vector3 center)
            {
                Vector3 n = Vector3.Cross(_v[b] - _v[a], _v[c] - _v[a]);
                Vector3 mid = (_v[a] + _v[b] + _v[c]) / 3f;
                if (Vector3.Dot(n, mid - center) >= 0f) Tri(a, b, c); else Tri(a, c, b);
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
