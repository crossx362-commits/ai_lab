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
        public TankKind Kind;           // 기종 — 고유 부품(투석 팔·활대·접시·삼지창…)은 이걸 보고 붙는다

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
            sh.Kind = k;
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

            // ══════════════════════════════════════════════════════════════
            //  기종 고유 형태 (오너 지적 2026-09-17 "탱크들이 다 똑같이 생긴 게 제일 큰 문제")
            //
            //  예전엔 13종이 전부 **같은 틀**(둥근 상자 + 둥근 캐빈 + 원통 포신)에 수치만 달랐다.
            //  바퀴 수·어깨·꼬리·기둥 칸수로 갈랐지만 그건 "자세히 보면 다르다"지 "한눈에 다르다"가 아니었다 —
            //  갤러리 정면 샷 13장을 나란히 놓으면 색 빼고 전부 같은 탱크였다.
            //
            //  원작(포트리스2)은 기종마다 **무기 자체가 다르게 생겼다**: 투석기는 팔, 쇠뇌는 활대, 다연장은 상자,
            //  이온은 접시, 포세이돈은 삼지창. 그래서 여기서는 **포신 자리를 기종 무기로 통째로 바꾸고**,
            //  차체 위에 기종 소품(가스통·지뢰·팬·폰툰…)을 얹고, 캐빈은 시대별로 갈랐다(고전=나무 받침 ·
            //  근대=둥근 캐빈 · 현대=각진 캐빈 · 미래=돔).
            //
            //  ⚠️ FirePoint 는 무기 끝에 맞춘다 — 탄이 접시 뒤나 활대 안에서 태어나면 안 된다(§5 P₀).
            //  ⚠️ 메시 파츠는 전부 아래 Part() 로 만들고 끝에서 한 번에 굽는다 — 파츠마다 ToMesh 를 부르면 잊기 쉽다.
            // ══════════════════════════════════════════════════════════════
            var kind = s.Kind;
            var era = TankStats.EraOf(kind);
            var parts = new List<(MeshFilter mf, MeshBuilder mb, string n)>();
            MeshBuilder Part(string n, Transform parent, Material m)
            {
                var mf = MakeMesh(n, parent, m); var mb = new MeshBuilder(); parts.Add((mf, mb, n)); return mb;
            }
            var leafMat = new Material(bodyMat) { color = new Color(0.36f, 0.66f, 0.30f) };   // 당근 잎

            // --- 캐빈(포탑). 시대별로 다른 형태 ---
            turret = new GameObject("Turret").transform;
            turret.SetParent(root, false);
            turret.localPosition = new Vector3(0, bodyY0 + s.BodyHeight, -s.BodyLength * 0.08f);
            float cw = s.TurretRadius * 1.55f, cl = s.TurretRadius * 1.45f;
            {
                var um = Part("TurretMesh", turret, bodyMat);
                switch (era)
                {
                    case TankEra.Classic:
                    {
                        // 나무 받침 위에 작은 캐빈 — 투석기·쇠뇌·고전 대포는 "장치"가 얹힌 수레다
                        var wm = Part("TurretDeck", turret, woodMat);
                        wm.Chamfer(new Vector3(0, s.TurretHeight * 0.16f, 0), new Vector3(cw * 1.15f, s.TurretHeight * 0.32f, cl * 1.15f), 0.08f);
                        for (int side = -1; side <= 1; side += 2)                              // 널빤지 결
                            wm.Box(new Vector3(side * cw * 0.3f, s.TurretHeight * 0.34f, 0), new Vector3(0.06f, 0.04f, cl * 1.05f));
                        um.Chamfer(new Vector3(0, s.TurretHeight * 0.62f, -cl * 0.12f), new Vector3(cw * 0.7f, s.TurretHeight * 0.6f, cl * 0.6f), 0.12f);
                        break;
                    }
                    case TankEra.Modern:
                    {
                        // 각진 캐빈 + 앞 경사 — 현대 전차의 인상
                        um.Box(new Vector3(0, s.TurretHeight * 0.5f, -cl * 0.05f), new Vector3(cw * 1.05f, s.TurretHeight, cl * 0.8f));
                        um.Wedge(new Vector3(0, s.TurretHeight * 0.5f, cl * 0.35f), cw * 1.05f, s.TurretHeight, cl * 0.35f);
                        um.Chamfer(new Vector3(cw * 0.25f, s.TurretHeight * 1.02f, -cl * 0.15f), new Vector3(cw * 0.4f, s.TurretHeight * 0.22f, cl * 0.4f), 0.06f); // 해치
                        break;
                    }
                    case TankEra.Future:
                    {
                        // 돔 + 팀색 띠 — 매끈한 미래 기체
                        um.Dome(new Vector3(0, s.TurretHeight * 0.25f, 0), cw * 0.55f, s.TurretHeight * 0.95f, 14, 5);
                        um.CylinderY(new Vector3(0, s.TurretHeight * 0.12f, 0), cw * 0.56f, s.TurretHeight * 0.25f, 14);
                        var band = Part("TurretBand", turret, accentMat);
                        band.CylinderY(new Vector3(0, s.TurretHeight * 0.30f, 0), cw * 0.58f, 0.08f, 14);
                        break;
                    }
                    default:
                    {
                        // 근대 — 둥근 캐빈(장난감 비례의 원형)
                        um.Chamfer(new Vector3(0, s.TurretHeight * 0.5f, 0), new Vector3(cw, s.TurretHeight, cl), Mathf.Min(0.24f, s.TurretHeight * 0.32f));
                        um.Chamfer(new Vector3(0, s.TurretHeight * 0.98f, -cl * 0.1f), new Vector3(cw * 0.55f, s.TurretHeight * 0.28f, cl * 0.5f), 0.08f); // 해치
                        break;
                    }
                }
            }

            // 식별 기둥 — 칸 수 = 종, 색 = 팀
            {
                var mm = Part("TeamMast", turret, accentMat);
                for (int m = 0; m < s.MastHeight; m++)
                    mm.Chamfer(new Vector3(-cw * 0.3f, s.TurretHeight * 1.1f + 0.16f + m * 0.3f, -cl * 0.22f), new Vector3(0.22f, 0.26f, 0.22f), 0.06f);
            }

            // --- 무기(포신 자리). 기종마다 통째로 다르다 ---
            barrel = new GameObject("Barrel").transform;
            barrel.SetParent(turret, false);
            barrel.localPosition = new Vector3(0, s.TurretHeight * 0.52f, cl * 0.45f);
            float L = s.BarrelLength;
            float r = s.BarrelCaliber * 0.5f;
            float fireZ = L;
            var am = Part("BarrelMesh", barrel, accentMat);
            var dm = Part("BarrelDark", barrel, trackMat);
            switch (kind)
            {
                case TankKind.Catapult:
                {
                    // 던지는 팔(나무) + 바가지 + 돌. 팔 길이가 곧 실루엣이다. 포신 피치가 팔 각도가 된다.
                    L = s.BarrelLength * 2.2f; fireZ = L;
                    var wm = Part("Arm", barrel, woodMat);
                    wm.Box(new Vector3(0, 0, L * 0.5f - 0.45f), new Vector3(0.22f, 0.22f, L + 0.9f));
                    wm.Chamfer(new Vector3(0, 0, -0.75f), new Vector3(0.62f, 0.62f, 0.55f), 0.1f);            // 평형추
                    am.CylinderY(new Vector3(0, 0.18f, L - 0.1f), 0.42f, 0.36f, 12);                          // 바가지
                    dm.CylinderY(new Vector3(0, 0.37f, L - 0.1f), 0.34f, 0.04f, 12);                          // 바가지 속(어둡게)
                    dm.Sphere(new Vector3(0, 0.5f, L - 0.1f), 0.24f, 8);                                        // 돌
                    // A 자 지지대(캐빈 위, 나무)
                    var fm = Part("Frame", turret, woodMat);
                    for (int side = -1; side <= 1; side += 2)
                        fm.BoxRot(new Vector3(side * cw * 0.42f, s.TurretHeight * 0.55f + 0.55f, cl * 0.3f), new Vector3(0.14f, 1.3f, 0.14f), Quaternion.Euler(0, 0, side * -14f));
                    fm.Box(new Vector3(0, s.TurretHeight * 0.55f + 1.15f, cl * 0.3f), new Vector3(cw * 0.95f, 0.14f, 0.14f));
                    break;
                }
                case TankKind.CrossBow:
                {
                    // 활대(나무) 좌우로 벌어짐 + 시위 + 가느다란 볼트(팀색)
                    am.Box(new Vector3(0, 0, L * 0.5f), new Vector3(0.14f, 0.10f, L));                          // 레일
                    am.ConeZ(new Vector3(0, 0.06f, L * 0.5f), 0.09f, 0.0f, L * 0.9f, 8);                       // 볼트(끝이 뾰족)
                    for (int side = -1; side <= 1; side += 2)
                        am.BoxRot(new Vector3(0, 0.06f, 0.35f), new Vector3(0.03f, 0.22f, 0.3f), Quaternion.Euler(0, 0, side * 40f)); // 깃
                    var wm = Part("Bow", barrel, woodMat);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        wm.BoxRot(new Vector3(side * 0.85f, 0, 0.25f), new Vector3(1.7f, 0.10f, 0.16f), Quaternion.Euler(0, side * -28f, 0));
                        wm.BoxRot(new Vector3(side * 0.8f, 0, -0.32f), new Vector3(1.62f, 0.03f, 0.03f), Quaternion.Euler(0, side * 7f, 0));  // 시위
                    }
                    break;
                }
                case TankKind.Cannon:
                {
                    // 굵고 짧은 고전 대포 — 보강 링 둘 + 나팔 포구. 뒤 갑판에 포탄 더미
                    am.CylinderZ(new Vector3(0, 0, L * 0.45f), r, L * 0.9f, 12);
                    am.CylinderZ(new Vector3(0, 0, L * 0.1f), r * 1.55f, L * 0.2f, 12);
                    am.CylinderZ(new Vector3(0, 0, L * 0.38f), r * 1.25f, 0.14f, 12);
                    am.CylinderZ(new Vector3(0, 0, L * 0.62f), r * 1.25f, 0.14f, 12);
                    am.ConeZ(new Vector3(0, 0, L * 0.9f), r * 1.05f, r * 1.7f, L * 0.2f, 12);
                    dm.CylinderZ(new Vector3(0, 0, L * 0.995f), r * 1.15f, 0.04f, 12);
                    var pm = Part("Ammo", root, trackMat);
                    float py = bodyY0 + s.BodyHeight, pz = -s.BodyLength * 0.42f;
                    pm.Sphere(new Vector3(-0.32f, py + 0.22f, pz), 0.22f, 8);
                    pm.Sphere(new Vector3(0.32f, py + 0.22f, pz), 0.22f, 8);
                    pm.Sphere(new Vector3(0f, py + 0.56f, pz), 0.22f, 8);
                    break;
                }
                case TankKind.Carrot:
                {
                    // 당근 그 자체 — 주황 원뿔 + 초록 잎. 포신이 기종색이다(팀색은 바퀴·기둥이 맡는다)
                    L = s.BarrelLength * 1.25f; fireZ = L;
                    var cm = Part("CarrotCone", barrel, bodyMat);
                    cm.ConeZ(new Vector3(0, 0, L * 0.5f), r * 1.35f, 0.04f, L, 12);
                    cm.CylinderZ(new Vector3(0, 0, L * 0.18f), r * 1.42f, 0.08f, 12);                          // 마디
                    cm.CylinderZ(new Vector3(0, 0, L * 0.42f), r * 1.12f, 0.08f, 12);
                    var lm = Part("Leaves", barrel, leafMat);
                    for (int i = 0; i < 4; i++)
                    {
                        float ang = 40f + i * 90f;
                        var q = Quaternion.Euler(0, 0, ang) * Quaternion.Euler(-35f, 0, 0);
                        lm.BoxRot(new Vector3(0, 0, -0.12f) + Quaternion.Euler(0, 0, ang) * new Vector3(0, r * 1.2f, 0) + q * new Vector3(0, 0.28f, 0),
                                  new Vector3(0.16f, 0.62f, 0.05f), q);
                    }
                    break;
                }
                case TankKind.Duke:
                {
                    // 독가스 살포기 — 포신 끝 둥근 노즐 + 등 뒤 가스통 둘 + 굴뚝
                    am.CylinderZ(new Vector3(0, 0, L * 0.5f), r, L, 12);
                    am.CylinderZ(new Vector3(0, 0, L * 0.12f), r * 1.5f, L * 0.2f, 12);
                    am.Sphere(new Vector3(0, 0, L), r * 1.7f, 10);
                    dm.CylinderZ(new Vector3(0, 0, L + r * 1.7f), r * 0.7f, 0.05f, 10);
                    var gm = Part("GasTanks", root, trackMat);
                    var gb = Part("GasBands", root, accentMat);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var c = new Vector3(side * s.BodyWidth * 0.26f, bodyY0 + s.BodyHeight + 0.34f, -s.BodyLength * 0.40f);
                        gm.CylinderZ(c, 0.30f, 1.15f, 12);
                        gm.Sphere(c + new Vector3(0, 0, -0.55f), 0.30f, 8);
                        gb.CylinderZ(c + new Vector3(0, 0, 0.42f), 0.32f, 0.12f, 12);
                        gb.CylinderZ(c + new Vector3(0, 0, -0.2f), 0.32f, 0.12f, 12);
                    }
                    var sm = Part("Stack", turret, trackMat);
                    sm.CylinderY(new Vector3(cw * 0.36f, s.TurretHeight + 0.45f, -cl * 0.35f), 0.10f, 0.95f, 8);
                    sm.CylinderY(new Vector3(cw * 0.36f, s.TurretHeight + 0.95f, -cl * 0.35f), 0.15f, 0.12f, 8);
                    break;
                }
                case TankKind.MineLander:
                {
                    // 공병차 — 앞 도저 삽날 + 짧은 박격포 + 뒤 지뢰 투하구와 지뢰 더미
                    am.CylinderZ(new Vector3(0, 0, L * 0.5f), r, L, 12);
                    am.CylinderZ(new Vector3(0, 0, L * 0.15f), r * 1.4f, L * 0.3f, 12);
                    dm.CylinderZ(new Vector3(0, 0, L * 0.99f), r * 0.7f, 0.04f, 10);
                    var bm2 = Part("Dozer", root, trackMat);
                    bm2.BoxRot(new Vector3(0, bodyY0 + s.BodyHeight * 0.35f, s.BodyLength * 0.5f + 0.42f),
                               new Vector3(s.BodyWidth * 1.25f, s.BodyHeight * 0.8f, 0.14f), Quaternion.Euler(-14f, 0, 0));
                    for (int side = -1; side <= 1; side += 2)
                        bm2.Box(new Vector3(side * s.BodyWidth * 0.3f, bodyY0 + s.BodyHeight * 0.4f, s.BodyLength * 0.5f + 0.2f), new Vector3(0.1f, 0.1f, 0.45f)); // 팔
                    var ch = Part("Chute", root, bodyMat);
                    ch.BoxRot(new Vector3(0, bodyY0 + s.BodyHeight * 0.55f, -s.BodyLength * 0.5f - 0.35f),
                              new Vector3(s.BodyWidth * 0.5f, 0.12f, 0.8f), Quaternion.Euler(32f, 0, 0));
                    var mn = Part("Mines", root, trackMat);
                    var mc = Part("MineCaps", root, accentMat);
                    for (int i = 0; i < 3; i++)
                    {
                        var c = new Vector3((i - 1) * 0.55f, bodyY0 + s.BodyHeight + 0.08f, -s.BodyLength * 0.36f);
                        mn.CylinderY(c, 0.26f, 0.14f, 10);
                        mc.CylinderY(c + new Vector3(0, 0.08f, 0), 0.09f, 0.06f, 8);
                    }
                    break;
                }
                case TankKind.Missile:
                {
                    // 발사 레일 위에 큰 미사일 한 발 — 탄두·몸통·꼬리날개
                    float mr = r * 0.9f, my = mr + 0.12f;
                    dm.Box(new Vector3(0, 0, L * 0.5f), new Vector3(mr * 1.8f, 0.10f, L));                        // 레일
                    dm.Box(new Vector3(0, my * 0.5f, L * 0.3f), new Vector3(0.12f, my, 0.5f));                     // 받침
                    am.CylinderZ(new Vector3(0, my, L * 0.42f), mr, L * 0.84f, 12);
                    am.ConeZ(new Vector3(0, my, L * 0.84f + 0.3f), mr, 0.0f, 0.6f, 12);
                    for (int i = 0; i < 4; i++)
                    {
                        var q = Quaternion.Euler(0, 0, 45f + i * 90f);
                        am.BoxRot(new Vector3(0, my, L * 0.08f) + q * new Vector3(0, mr + 0.18f, 0), new Vector3(0.04f, 0.4f, 0.45f), q);
                    }
                    fireZ = L * 0.84f + 0.6f;
                    break;
                }
                case TankKind.MultiMissile:
                {
                    // 다연장 상자 — 3×3 발사관(원작 2번탄 9연). 포신이 아니라 **네모**다
                    float W = 1.35f;
                    am.Chamfer(new Vector3(0, 0, L * 0.5f), new Vector3(W, W, L), 0.08f);
                    for (int i = -1; i <= 1; i++)
                        for (int j = -1; j <= 1; j++)
                            dm.CylinderZ(new Vector3(i * W * 0.3f, j * W * 0.3f, L * 0.99f), W * 0.12f, 0.06f, 10);
                    dm.Box(new Vector3(0, -W * 0.5f - 0.06f, L * 0.35f), new Vector3(0.5f, 0.12f, 0.5f));         // 받침
                    break;
                }
                case TankKind.SuperTank:
                {
                    // 쌍포신 + 옆치마 장갑 + 뒤 배기관 둘 — "전 항목 최대"가 몸집으로 보이게
                    float rr = r * 0.8f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float x = side * rr * 1.35f;
                        am.CylinderZ(new Vector3(x, 0, L * 0.5f), rr, L, 12);
                        am.CylinderZ(new Vector3(x, 0, L * 0.9f), rr * 1.5f, L * 0.14f, 12);
                        dm.CylinderZ(new Vector3(x, 0, L * 0.985f), rr * 0.6f, 0.05f, 10);
                    }
                    am.Chamfer(new Vector3(0, 0, L * 0.12f), new Vector3(rr * 5.2f, rr * 2.6f, L * 0.24f), 0.08f);   // 포미 블록
                    var sk = Part("Skirts", root, bodyMat);
                    for (int side = -1; side <= 1; side += 2)
                        sk.Chamfer(new Vector3(side * (s.BodyWidth * 0.5f + wheelW + 0.06f), bodyY0 + s.BodyHeight * 0.55f, 0), new Vector3(0.14f, s.BodyHeight * 0.55f, s.BodyLength * 0.98f), 0.05f);
                    var ex = Part("Exhaust", root, trackMat);
                    for (int side = -1; side <= 1; side += 2)
                        ex.CylinderY(new Vector3(side * 0.4f, bodyY0 + s.BodyHeight + 0.3f, -s.BodyLength * 0.5f - s.TailBox * 0.5f), 0.11f, 0.7f, 8);
                    break;
                }
                case TankKind.Laser:
                {
                    // 가늘고 긴 빔 방출기 — 냉각 링 셋 + 끝의 결정. 캐빈 위 코일
                    am.CylinderZ(new Vector3(0, 0, L * 0.5f), r, L, 10);
                    for (int i = 0; i < 3; i++)
                        am.CylinderZ(new Vector3(0, 0, L * (0.25f + i * 0.25f)), r * 3.2f, 0.10f, 12);
                    am.ConeZ(new Vector3(0, 0, L + 0.2f), r * 3.4f, r * 1.4f, 0.4f, 12);
                    var cr = Part("Crystal", barrel, bodyMat);
                    cr.ConeZ(new Vector3(0, 0, L + 0.55f), 0.0f, r * 2.0f, 0.3f, 6);
                    cr.ConeZ(new Vector3(0, 0, L + 0.85f), r * 2.0f, 0.0f, 0.3f, 6);
                    fireZ = L + 1.0f;
                    var coil = Part("Coil", turret, accentMat);
                    for (int i = 0; i < 3; i++)
                        coil.CylinderY(new Vector3(cw * 0.26f, s.TurretHeight * 0.95f + 0.1f + i * 0.16f, -cl * 0.1f), 0.42f - i * 0.1f, 0.08f, 12);
                    break;
                }
                case TankKind.IonAttacker:
                {
                    // 위성 접시 — 넓은 원뿔(속이 보이게 안쪽 면 별도) + 중앙 방출봉
                    var dish = Part("Dish", barrel, bodyMat);
                    dish.ConeZ(new Vector3(0, 0, 0.62f), 0.35f, 1.35f, 0.65f, 16, capA: true, capB: false);
                    dish.ConeZ(new Vector3(0, 0, 0.60f), 0.32f, 1.30f, 0.62f, 16, capA: true, capB: false, flip: true);
                    am.CylinderZ(new Vector3(0, 0, L * 0.5f), 0.09f, L, 8);
                    am.Sphere(new Vector3(0, 0, L), 0.2f, 8);
                    for (int i = 0; i < 3; i++)                                                           // 지지 살
                    {
                        var q = Quaternion.Euler(0, 0, 90f + i * 120f);
                        am.BoxRot(new Vector3(0, 0, 0.9f + (L - 0.9f) * 0.5f) + q * new Vector3(0, 0.65f, 0), new Vector3(0.04f, 0.04f, L - 0.9f), q * Quaternion.Euler(Mathf.Atan2(1.3f, L - 0.9f) * Mathf.Rad2Deg * 0.5f, 0, 0));
                    }
                    break;
                }
                case TankKind.Poseidon:
                {
                    // 삼지창 + 옆 폰툰(배 느낌) + 등지느러미
                    am.CylinderZ(new Vector3(0, 0, L * 0.5f), 0.09f, L, 8);
                    am.Box(new Vector3(0, 0, L - 0.45f), new Vector3(0.95f, 0.12f, 0.12f));
                    am.ConeZ(new Vector3(0, 0, L - 0.05f), 0.09f, 0.0f, 0.8f, 8);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        am.CylinderZ(new Vector3(side * 0.42f, 0, L - 0.2f), 0.07f, 0.5f, 8);
                        am.ConeZ(new Vector3(side * 0.42f, 0, L + 0.2f), 0.07f, 0.0f, 0.4f, 8);
                    }
                    fireZ = L + 0.35f;
                    var pn = Part("Pontoons", root, bodyMat);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var c = new Vector3(side * (s.BodyWidth * 0.5f + wheelW + 0.3f), bodyY0 + s.BodyHeight * 0.4f, -s.BodyLength * 0.05f);
                        pn.CylinderZ(c, 0.36f, s.BodyLength * 0.8f, 12);
                        pn.ConeZ(c + new Vector3(0, 0, s.BodyLength * 0.4f + 0.3f), 0.36f, 0.05f, 0.6f, 12);
                        pn.Sphere(c + new Vector3(0, 0, -s.BodyLength * 0.4f), 0.36f, 8);
                    }
                    var fin = Part("Fin", turret, bodyMat);
                    fin.BoxRot(new Vector3(0, s.TurretHeight * 1.05f + 0.3f, -cl * 0.35f), new Vector3(0.08f, 0.75f, 0.7f), Quaternion.Euler(-32f, 0, 0));
                    break;
                }
                default:   // SecWind
                {
                    // 바람칼 — 납작하고 넓은 날 + 캐빈 뒤 팬 + 꼬리 날개
                    am.Box(new Vector3(0, 0, L * 0.4f), new Vector3(0.8f, 0.07f, L * 0.8f));
                    am.Wedge(new Vector3(0, 0, L * 0.8f), 0.8f, 0.07f, L * 0.3f);
                    am.CylinderZ(new Vector3(0, 0, L * 0.12f), r * 1.4f, L * 0.24f, 10);
                    fireZ = L * 1.05f;
                    var fan = Part("Fan", turret, accentMat);
                    var fanC = new Vector3(0, s.TurretHeight * 0.6f, -cl * 0.85f);
                    fan.CylinderZ(fanC, 0.16f, 0.22f, 10);
                    for (int i = 0; i < 4; i++)
                    {
                        var q = Quaternion.Euler(0, 0, 45f + i * 90f);
                        fan.BoxRot(fanC + q * new Vector3(0, 0.5f, 0), new Vector3(0.14f, 0.85f, 0.04f), q * Quaternion.Euler(0, 28f, 0));
                    }
                    var ring = Part("FanRing", turret, trackMat);
                    ring.CylinderZ(fanC, 0.98f, 0.06f, 16);
                    ring.CylinderZ(fanC, 0.90f, 0.08f, 16, flip: true);
                    var tail = Part("Tail", root, bodyMat);
                    tail.BoxRot(new Vector3(0, bodyY0 + s.BodyHeight + 0.3f, -s.BodyLength * 0.5f - s.TailBox * 0.6f), new Vector3(0.06f, 0.6f, 0.55f), Quaternion.Euler(-25f, 0, 0));
                    break;
                }
            }

            firePoint = new GameObject("FirePoint").transform;
            firePoint.SetParent(barrel, false);
            firePoint.localPosition = new Vector3(0, 0, fireZ);

            foreach (var (mf, mb, n) in parts) mf.mesh = mb.ToMesh(n);

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

        /// <summary>
        /// 탄 모양 — 탱크·탄종마다 다르다.
        ///
        /// 근거는 원작 무기 조사(§2-9)다. 전부 같은 검은 공으로 날아가면 **무엇이 오고 있는지**를
        /// 화면에서 못 읽는다 — 원작은 탄마다 생김새가 달라서 날아오는 것만 보고 대비할 수 있었다.
        ///   캐논 1번탄 "검콩" / 2번탄 "빨콩" → 둥근 콩
        ///   미사일·멀티미사일 → 탄두 + 꼬리날개
        ///   레이저탱크 → 가늘고 긴 빔 덩어리
        ///   이온어태커 위성탄 → 수직으로 꽂히는 각진 탄
        ///   마인랜더 지뢰 → 납작한 원반
        ///   캐터펄트 → 투석기 돌덩이
        ///   그 밖 → 기본 포탄(§9-2 의 박스·실린더만 쓴다)
        /// ⚠️ Z+ 가 진행 방향이다. 비행 중 `LookAt` 으로 방향을 맞추므로 길쭉한 축은 Z 로 만들어라.
        /// </summary>
        public static Mesh Shell(TankKind kind, ShellKind shell)
        {
            // 재조형(오너 지시 2026-09-17 "미사일 모양 더 디테일하게"): 예전엔 상자·쐐기 두세 개였다.
            // 원뿔대(ConeZ)·구(Sphere)·회전 상자(BoxRot)로 탄두·노즐·날개·띠를 실제 탄 구조대로 그린다.
            // 정점은 탄당 200~500 — 한 화면에 최대 9발(멀티미사일)이라 부담 없다.
            var b = new MeshBuilder();
            switch (kind)
            {
                case TankKind.Cannon:            // 검콩/빨콩 — 둥근 콩. 가운데 띠 + 뒤 뇌관
                    b.Sphere(Vector3.zero, 0.46f, 12);
                    b.CylinderZ(Vector3.zero, 0.48f, 0.14f, 12);
                    b.CylinderZ(new Vector3(0f, 0f, -0.44f), 0.16f, 0.14f, 8);
                    break;

                case TankKind.Missile:           // 미사일 — 몸통·탄두·4 날개·조종 날개·노즐
                    b.CylinderZ(new Vector3(0f, 0f, -0.05f), 0.22f, 1.30f, 12);
                    b.ConeZ(new Vector3(0f, 0f, 0.92f), 0.22f, 0.0f, 0.64f, 12);                 // 탄두
                    b.CylinderZ(new Vector3(0f, 0f, 0.35f), 0.235f, 0.08f, 12);                   // 띠
                    b.ConeZ(new Vector3(0f, 0f, -0.78f), 0.18f, 0.13f, 0.16f, 10);                // 노즐
                    for (int i = 0; i < 4; i++)                                                    // 꼬리 날개(뒤로 젖힘)
                    {
                        var q = Quaternion.Euler(0f, 0f, 45f + i * 90f);
                        b.BoxRot(new Vector3(0f, 0f, -0.55f) + q * new Vector3(0f, 0.38f, 0f), new Vector3(0.04f, 0.34f, 0.42f), q * Quaternion.Euler(-28f, 0f, 0f));
                        b.BoxRot(new Vector3(0f, 0f, 0.45f) + q * new Vector3(0f, 0.30f, 0f), new Vector3(0.03f, 0.16f, 0.22f), q);   // 앞 조종 날개
                    }
                    break;

                case TankKind.MultiMissile:      // 묶음 탄두 — 원작 2번탄이 9연이다(§2-9). 작은 미사일 세 개가 띠로 묶여 난다.
                    for (int mi = 0; mi < 3; mi++)
                    {
                        float ox = (mi - 1) * 0.30f, oy = mi == 1 ? 0.22f : -0.08f;
                        var c = new Vector3(ox, oy, 0f);
                        b.CylinderZ(c + new Vector3(0f, 0f, -0.06f), 0.12f, 0.86f, 8);
                        b.ConeZ(c + new Vector3(0f, 0f, 0.55f), 0.12f, 0.0f, 0.36f, 8);
                        b.ConeZ(c + new Vector3(0f, 0f, -0.52f), 0.10f, 0.07f, 0.08f, 8);
                        for (int i = 0; i < 3; i++)
                        {
                            var q = Quaternion.Euler(0f, 0f, 90f + i * 120f);
                            b.BoxRot(c + new Vector3(0f, 0f, -0.36f) + q * new Vector3(0f, 0.18f, 0f), new Vector3(0.025f, 0.14f, 0.22f), q);
                        }
                    }
                    b.CylinderZ(new Vector3(0f, 0.04f, -0.30f), 0.46f, 0.10f, 10);   // 묶는 띠
                    b.CylinderZ(new Vector3(0f, 0.04f, 0.20f), 0.46f, 0.10f, 10);
                    break;

                case TankKind.Laser:             // 빔 덩어리 — 가늘고 긴 코어 + 양끝 원뿔 + 에너지 링
                    b.CylinderZ(Vector3.zero, 0.11f, 1.90f, 8);
                    b.ConeZ(new Vector3(0f, 0f, 1.25f), 0.11f, 0.0f, 0.6f, 8);
                    b.ConeZ(new Vector3(0f, 0f, -1.25f), 0.0f, 0.11f, 0.6f, 8);
                    for (int i = -1; i <= 1; i++) b.CylinderZ(new Vector3(0f, 0f, i * 0.55f), 0.26f, 0.06f, 10);
                    break;

                case TankKind.IonAttacker:       // 위성탄 — 결정(팔면체) + 적도 링 + 안테나
                    b.ConeZ(new Vector3(0f, 0f, 0.42f), 0.36f, 0.0f, 0.84f, 6);
                    b.ConeZ(new Vector3(0f, 0f, -0.42f), 0.0f, 0.36f, 0.84f, 6);
                    b.CylinderZ(Vector3.zero, 0.52f, 0.05f, 12);
                    b.Box(Vector3.zero, new Vector3(1.10f, 0.05f, 0.05f));
                    b.Box(Vector3.zero, new Vector3(0.05f, 1.10f, 0.05f));
                    break;

                case TankKind.MineLander:        // 지뢰 — 원반 + 테두리 + 압력판 + 가시
                    b.CylinderY(Vector3.zero, 0.50f, 0.22f, 12);
                    b.CylinderY(new Vector3(0f, 0.14f, 0f), 0.22f, 0.08f, 8);
                    for (int i = 0; i < 6; i++)
                    {
                        var q = Quaternion.Euler(0f, i * 60f, 0f);
                        b.BoxRot(q * new Vector3(0f, 0f, 0.54f), new Vector3(0.08f, 0.08f, 0.18f), q);
                    }
                    break;

                case TankKind.Catapult:          // 투석기 돌덩이 — 구 세 개를 어긋나게 겹친 울퉁불퉁한 덩어리
                    b.Sphere(Vector3.zero, 0.40f, 7);
                    b.Sphere(new Vector3(0.18f, 0.14f, -0.12f), 0.30f, 6);
                    b.Sphere(new Vector3(-0.16f, -0.10f, 0.16f), 0.28f, 6);
                    b.BoxRot(new Vector3(0.05f, -0.18f, 0.05f), new Vector3(0.5f, 0.4f, 0.5f), Quaternion.Euler(20f, 35f, 15f));
                    break;

                case TankKind.Poseidon:          // 물방울 — 둥근 머리 + 뒤로 가늘어지는 꼬리 + 작은 방울
                    b.Sphere(new Vector3(0f, 0f, 0.12f), 0.36f, 12);
                    b.ConeZ(new Vector3(0f, 0f, -0.36f), 0.0f, 0.34f, 0.72f, 12);
                    b.Sphere(new Vector3(0.22f, 0.20f, -0.30f), 0.11f, 6);
                    b.Sphere(new Vector3(-0.20f, -0.16f, -0.44f), 0.09f, 6);
                    break;

                case TankKind.Duke:              // 독구름 탄 — 통 + 양옆 가스통(구 마개) + 밸브
                    b.CylinderZ(Vector3.zero, 0.30f, 0.92f, 10);
                    b.Sphere(new Vector3(0f, 0f, 0.46f), 0.30f, 8);
                    b.Sphere(new Vector3(0f, 0f, -0.46f), 0.30f, 8);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        b.CylinderZ(new Vector3(side * 0.34f, -0.06f, -0.1f), 0.12f, 0.56f, 8);
                        b.Sphere(new Vector3(side * 0.34f, -0.06f, -0.38f), 0.12f, 6);
                    }
                    b.CylinderY(new Vector3(0f, 0.34f, 0f), 0.06f, 0.16f, 6);
                    b.CylinderY(new Vector3(0f, 0.42f, 0f), 0.14f, 0.04f, 8);
                    break;

                case TankKind.CrossBow:          // 화살 — 촉 + 대 + 깃 셋(비스듬히) + 마디
                    b.ConeZ(new Vector3(0f, 0f, 0.82f), 0.14f, 0.0f, 0.5f, 6);
                    b.CylinderZ(new Vector3(0f, 0f, 0.62f), 0.15f, 0.06f, 6);
                    b.CylinderZ(new Vector3(0f, 0f, -0.06f), 0.06f, 1.30f, 6);
                    for (int i = 0; i < 3; i++)
                    {
                        var q = Quaternion.Euler(0f, 0f, i * 120f);
                        b.BoxRot(new Vector3(0f, 0f, -0.62f) + q * new Vector3(0f, 0.17f, 0f), new Vector3(0.02f, 0.26f, 0.36f), q * Quaternion.Euler(-18f, 0f, 0f));
                    }
                    break;

                case TankKind.Carrot:            // 당근 — 앞이 뾰족하고 뒤가 굵다. 마디 + 잎 셋
                    b.ConeZ(new Vector3(0f, 0f, 0.10f), 0.30f, 0.0f, 1.24f, 10);
                    b.CylinderZ(new Vector3(0f, 0f, -0.20f), 0.31f, 0.05f, 10);
                    b.CylinderZ(new Vector3(0f, 0f, 0.15f), 0.23f, 0.05f, 10);
                    b.Sphere(new Vector3(0f, 0f, -0.55f), 0.26f, 8);
                    for (int i = 0; i < 3; i++)
                    {
                        var q = Quaternion.Euler(0f, 0f, 30f + i * 120f) * Quaternion.Euler(-40f, 0f, 0f);
                        b.BoxRot(new Vector3(0f, 0f, -0.66f) + q * new Vector3(0f, 0.22f, 0f), new Vector3(0.10f, 0.40f, 0.04f), q);
                    }
                    break;

                case TankKind.SuperTank:         // 유도탄 — 매끈한 몸통 + 긴 탄두 + 젖힌 날개 넷 + 앞 카나드 + 노즐
                    b.CylinderZ(new Vector3(0f, 0f, 0.05f), 0.20f, 1.40f, 12);
                    b.ConeZ(new Vector3(0f, 0f, 1.15f), 0.20f, 0.0f, 0.80f, 12);
                    b.ConeZ(new Vector3(0f, 0f, -0.72f), 0.20f, 0.13f, 0.14f, 10);
                    b.CylinderZ(new Vector3(0f, 0f, 0.55f), 0.215f, 0.06f, 12);
                    for (int i = 0; i < 4; i++)
                    {
                        var q = Quaternion.Euler(0f, 0f, i * 90f);
                        b.BoxRot(new Vector3(0f, 0f, -0.45f) + q * new Vector3(0f, 0.42f, 0f), new Vector3(0.03f, 0.44f, 0.5f), q * Quaternion.Euler(-35f, 0f, 0f));
                        b.BoxRot(new Vector3(0f, 0f, 0.62f) + q * new Vector3(0f, 0.28f, 0f), new Vector3(0.025f, 0.14f, 0.26f), q * Quaternion.Euler(-20f, 0f, 0f));
                    }
                    break;

                case TankKind.SecWind:           // 바람칼 — 초승달 날 + 중심 허브 + 회전 방향 홈
                    b.BoxRot(new Vector3(0.40f, 0f, 0.05f), new Vector3(0.70f, 0.08f, 0.34f), Quaternion.Euler(0f, 28f, 0f));
                    b.BoxRot(new Vector3(-0.40f, 0f, 0.05f), new Vector3(0.70f, 0.08f, 0.34f), Quaternion.Euler(0f, -28f, 0f));
                    b.ConeZ(new Vector3(0f, 0f, 0.55f), 0.30f, 0.0f, 0.5f, 8);
                    b.CylinderY(Vector3.zero, 0.22f, 0.26f, 10);
                    b.CylinderY(Vector3.zero, 0.10f, 0.34f, 8);
                    break;

                default:                         // 남은 기종 — 기본 포탄
                    b.CylinderZ(new Vector3(0f, 0f, -0.1f), 0.28f, 0.8f, 10);
                    b.ConeZ(new Vector3(0f, 0f, 0.55f), 0.28f, 0.0f, 0.5f, 10);
                    break;
            }

            // 2번탄(특수탄)은 같은 실루엣에 **표식**을 붙인다.
            // 모양을 통째로 바꾸지 않는 이유: 날아오는 게 어느 기종 것인지가 먼저 읽혀야 하고(기종 = 실루엣),
            // 무엇이 실렸는지는 그 다음이다(탄종 = 표식 + 색, ShellColor 가 밝기로 한 번 더 가른다).
            if (shell == ShellKind.Special)
            {
                b.Box(new Vector3(0f, 0f, -0.18f), new Vector3(0.94f, 0.12f, 0.12f));
                b.Box(new Vector3(0f, 0f, -0.18f), new Vector3(0.12f, 0.94f, 0.12f));
            }
            return b.ToMesh($"Shell_{kind}_{shell}");
        }

        /// <summary>
        /// 탄 색 — 종별 차체색(BodyColor)에서 끌어온다. 같은 색 계열이라야 "저 탱크가 쏜 것"이 읽힌다.
        /// 2번탄은 더 밝게 해서 특수탄이 오는 걸 구분시킨다(원작도 1·2번탄 색이 달랐다).
        /// </summary>
        public static Color ShellColor(TankKind kind, ShellKind shell)
        {
            var body = TankShape.BodyColor(kind);
            // 어두운 탄이 기본 — 하늘 배경에서 실루엣이 읽혀야 한다.
            var baseCol = Color.Lerp(body, new Color(0.10f, 0.10f, 0.13f), 0.55f);
            return shell == ShellKind.Special ? Color.Lerp(baseCol, body, 0.75f) : baseCol;
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

            public void CylinderY(Vector3 c, float r, float h, int seg) => Cylinder(c, r, r, h, seg, 1);
            public void CylinderX(Vector3 c, float r, float len, int seg) => Cylinder(c, r, r, len, seg, 0);
            public void CylinderZ(Vector3 c, float r, float len, int seg, bool flip = false) => Cylinder(c, r, r, len, seg, 2, true, true, flip);
            /// <summary>+z 로 갈수록 r0→r1 로 변하는 원뿔대. 원뿔·접시·탄두·당근이 전부 이것이다.</summary>
            public void ConeZ(Vector3 c, float r0, float r1, float len, int seg, bool capA = true, bool capB = true, bool flip = false)
                => Cylinder(c, r0, r1, len, seg, 2, capA, capB, flip);

            /// <summary>회전한 상자 — 활대·팬 날개·잎처럼 기울어진 판을 만든다.</summary>
            public void BoxRot(Vector3 c, Vector3 size, Quaternion q)
            {
                Vector3 h = size * 0.5f;
                int b = _v.Count;
                for (int i = 0; i < 8; i++)
                    _v.Add(c + q * new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z));
                int[] qd = { 0,2,3,1, 4,5,7,6, 0,1,5,4, 2,6,7,3, 0,4,6,2, 1,3,7,5 };
                for (int i = 0; i < qd.Length; i += 4) Quad(b + qd[i], b + qd[i+1], b + qd[i+2], b + qd[i+3]);
            }

            /// <summary>반구(위로 볼록). 미래 캐빈.</summary>
            public void Dome(Vector3 c, float r, float h, int seg, int rings)
            {
                int b = _v.Count;
                for (int j = 0; j <= rings; j++)
                {
                    float phi = j / (float)rings * Mathf.PI * 0.5f;       // 0=적도, π/2=꼭대기
                    float rr = Mathf.Cos(phi) * r, y = Mathf.Sin(phi) * h;
                    for (int i = 0; i < seg; i++)
                    {
                        float a = i * Mathf.PI * 2f / seg;
                        _v.Add(c + new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr));
                    }
                }
                for (int j = 0; j < rings; j++)
                    for (int i = 0; i < seg; i++)
                    {
                        int i0 = b + j * seg + i, i1 = b + j * seg + (i + 1) % seg;
                        Quad(i0, i0 + seg, i1 + seg, i1);
                    }
                int cap = _v.Count; _v.Add(c + new Vector3(0, -0.001f, 0));
                for (int i = 0; i < seg; i++) Tri(cap, b + (i + 1) % seg, b + i);
            }

            /// <summary>구 — 포탄 더미·노즐·가스통 끝. 세그먼트 8이면 정점 ~50.</summary>
            public void Sphere(Vector3 c, float r, int seg)
            {
                int b = _v.Count; int rings = Mathf.Max(3, seg / 2);
                for (int j = 0; j <= rings; j++)
                {
                    float phi = -Mathf.PI * 0.5f + j / (float)rings * Mathf.PI;
                    float rr = Mathf.Cos(phi) * r, y = Mathf.Sin(phi) * r;
                    for (int i = 0; i < seg; i++)
                    {
                        float a = i * Mathf.PI * 2f / seg;
                        _v.Add(c + new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr));
                    }
                }
                for (int j = 0; j < rings; j++)
                    for (int i = 0; i < seg; i++)
                    {
                        int i0 = b + j * seg + i, i1 = b + j * seg + (i + 1) % seg;
                        Quad(i0, i0 + seg, i1 + seg, i1);
                    }
            }

            void Cylinder(Vector3 c, float r0, float r1, float len, int seg, int axis, bool capA = true, bool capB = true, bool flip = false)
            {
                int b = _v.Count;
                float hl = len * 0.5f;
                for (int i = 0; i < seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                    _v.Add(c + Axis(axis, -hl, cs * r0, sn * r0));
                    _v.Add(c + Axis(axis, hl, cs * r1, sn * r1));
                }
                for (int i = 0; i < seg; i++)
                {
                    int i0 = b + i * 2, i1 = b + ((i + 1) % seg) * 2;
                    if (flip) Quad(i1, i1 + 1, i0 + 1, i0); else Quad(i0, i0 + 1, i1 + 1, i1);
                }
                // 뚜껑
                int cA = _v.Count; _v.Add(c + Axis(axis, -hl, 0, 0));
                int cB = _v.Count; _v.Add(c + Axis(axis, hl, 0, 0));
                for (int i = 0; i < seg; i++)
                {
                    int i0 = b + i * 2, i1 = b + ((i + 1) % seg) * 2;
                    if (capA) { if (flip) Tri(cA, i0, i1); else Tri(cA, i1, i0); }
                    if (capB) { if (flip) Tri(cB, i1 + 1, i0 + 1); else Tri(cB, i0 + 1, i1 + 1); }
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
