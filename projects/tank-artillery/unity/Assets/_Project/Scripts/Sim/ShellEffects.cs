// 2번탄(특수탄) 고유 메커니즘 라이브러리.
//
// 포트리스2 원작의 2번탄은 전부 고유 효과인데, 지금 게임은 "폭발·굴착 배율"로만
// 흉내냈다. 이 모듈은 각 탱크 2번탄의 정체성을 구현한다:
//   - 독(크로스보우, 듀크탱크): 매 턴 지속피해
//   - 화상(캐터펄트): 착지점 지속불
//   - 이동금지(포세이돈): 2턴 속박
//   - 지뢰(마인랜더): 반경 내 1회 폭발
//   - 위성탄(이온어태커): 오버행 무시 수직 낙하
//   - 다탄두(멀티미사일, 레이저, 슈퍼탱크): 부채꼴 다발 발사
//
// ⚠️ UnityEngine 참조 금지. 모든 계산은 정수/float와 VEC3만 쓴다.

using System;
using System.Collections.Generic;

namespace Tankfall.Sim
{
    /// <summary>유닛별 지속 상태 효과(독, 화상, 이동금지).</summary>
    public class StatusEffects
    {
        struct ActiveEffect
        {
            public int DmgPerTurn;
            public int TurnsLeft;
        }

        readonly Dictionary<int, ActiveEffect> _poison = new();
        readonly Dictionary<int, ActiveEffect> _burn = new();
        readonly Dictionary<int, int> _rooted = new();       // 이동금지 남은 턴 수

        /// <summary>턴 시작 시 호출 — 독/화상 피해를 반환하고 남은 턴을 감소.</summary>
        public int TickStartOfTurn(int id)
        {
            int totalDmg = 0;

            // 독 피해 적용
            if (_poison.TryGetValue(id, out var p))
            {
                totalDmg += p.DmgPerTurn;
                p.TurnsLeft--;
                if (p.TurnsLeft <= 0) _poison.Remove(id);
                else _poison[id] = p;
            }

            // 화상 피해 적용
            if (_burn.TryGetValue(id, out var b))
            {
                totalDmg += b.DmgPerTurn;
                b.TurnsLeft--;
                if (b.TurnsLeft <= 0) _burn.Remove(id);
                else _burn[id] = b;
            }

            // 이동금지 턴 감소 (피해 없음)
            if (_rooted.TryGetValue(id, out int rt))
            {
                rt--;
                if (rt <= 0) _rooted.Remove(id);
                else _rooted[id] = rt;
            }

            return totalDmg;
        }

        /// <summary>포세이돈은 독 저항 50%. **적용(<see cref="Poison"/>)과 평가(<see cref="PoisonGain"/>)가
        /// 같은 식을 봐야 하므로 여기 하나만 둔다** — 두 곳에 적으면 반드시 어긋난다(§2-9-1).</summary>
        public static int PoisonAfterResist(int dmgPerTurn, TankKind targetKind)
            => targetKind == TankKind.Poseidon ? (dmgPerTurn + 1) / 2 : dmgPerTurn;

        /// <summary>유닛에 독 감염. 듀크는 자신의 독 면역, 포세이돈은 반감.</summary>
        public void Poison(int id, int dmgPerTurn, int turns, TankKind targetKind)
        {
            dmgPerTurn = PoisonAfterResist(dmgPerTurn, targetKind);
            if (dmgPerTurn <= 0) return;

            // ⚠️ **덮어쓰기다 — 안 쌓인다.** 이미 중독된 적에게 다시 걸면 남은 턴이 갱신될 뿐이다.
            //    AI 의 탄종 평가가 이걸 모르면 재중독을 "총량만큼 이득"으로 오판한다 → <see cref="PoisonGain"/>.
            _poison[id] = new ActiveEffect { DmgPerTurn = dmgPerTurn, TurnsLeft = turns };
        }

        /// <summary>유닛에 화상 감염.</summary>
        public void Burn(int id, int dmgPerTurn, int turns)
        {
            if (dmgPerTurn <= 0) return;
            _burn[id] = new ActiveEffect { DmgPerTurn = dmgPerTurn, TurnsLeft = turns };   // 독과 같이 덮어쓰기
        }

        /// <summary>
        /// 지금 이 유닛에 독을 새로 걸면 **추가로** 들어갈 총 피해.
        ///
        /// 덮어쓰기라 새로 거는 순간 남은 총량은 정확히 `dmg×turns` 가 된다. 따라서 이득은
        /// **새 총량 − 지금 남은 총량**이고, 이미 더 센 독이 걸려 있으면 0 이다(더 약하게 덮어쓸 뿐이라 이득이 없다).
        ///
        /// ⚠️ 이 함수가 <see cref="Poison"/> 옆에 있는 이유: 평가와 적용이 **같은 규칙**을 봐야 한다.
        ///    AiGunner 쪽에 식을 베껴 두면 저항·지속 규칙이 바뀔 때 한쪽만 고쳐져 조용히 갈린다.
        /// </summary>
        public int PoisonGain(int id, int dmgPerTurn, int turns, TankKind targetKind)
        {
            dmgPerTurn = PoisonAfterResist(dmgPerTurn, targetKind);
            if (dmgPerTurn <= 0 || turns <= 0) return 0;
            int after = dmgPerTurn * turns;
            int before = _poison.TryGetValue(id, out var p) ? p.DmgPerTurn * p.TurnsLeft : 0;
            return Math.Max(0, after - before);
        }

        /// <summary>화상판 <see cref="PoisonGain"/>. 화상은 저항이 없다.</summary>
        public int BurnGain(int id, int dmgPerTurn, int turns)
        {
            if (dmgPerTurn <= 0 || turns <= 0) return 0;
            int after = dmgPerTurn * turns;
            int before = _burn.TryGetValue(id, out var b) ? b.DmgPerTurn * b.TurnsLeft : 0;
            return Math.Max(0, after - before);
        }

        /// <summary>유닛을 이동금지 상태로. 포세이돈의 2번탄 효과.</summary>
        public void Root(int id, int turns)
        {
            _rooted[id] = turns;
        }

        /// <summary>이 유닛의 상태이상(독·화상·속박)을 전부 지운다. 궁극기 "정화"(§2-9-12)가 쓴다.</summary>
        public void Cleanse(int id)
        {
            _poison.Remove(id); _burn.Remove(id); _rooted.Remove(id);
        }

        /// <summary>유닛이 이동할 수 있는지 확인(이동금지 상태 아닌지).</summary>
        public bool CanMove(int id) => !_rooted.ContainsKey(id);

        /// <summary>독 또는 화상이 걸려 있는가 — 연출(상태 파티클)용 읽기 전용 조회. 판정에는 안 쓴다.</summary>
        public bool HasDot(int id) => _poison.ContainsKey(id) || _burn.ContainsKey(id);

        /// <summary>상태 리셋(시뮬레이션 테스트용).</summary>
        public void Clear()
        {
            _poison.Clear();
            _burn.Clear();
            _rooted.Clear();
        }
    }

    /// <summary>위치 기반 설치물(지뢰, 지속불).</summary>
    public class HazardField
    {
        struct Mine
        {
            public float X, Y, Z;
            public float Radius;
            public int Damage;
            public int OwnerId;
            public bool Triggered;
        }

        struct FireField
        {
            public float X, Y, Z;
            public float Radius;
            public int DmgPerTurn;
            public int TurnsLeft;
            public int Tag;          // 0 지속불, 1 독구름 — 판정은 같고 연출만 다르다
        }

        /// <summary>연출용 읽기 전용 뷰. Kind: 0 지뢰(미폭발), 1 지속불, 2 독구름.</summary>
        public struct HazardView
        {
            public int Kind;
            public float X, Y, Z, Radius;
            public int TurnsLeft;    // 지뢰는 0
        }

        /// <summary>살아 있는 설치물을 전부 into 에 담는다(기존 내용은 지운다). 게임이 턴마다 불러 화면에 그린다.</summary>
        public void Snapshot(List<HazardView> into)
        {
            into.Clear();
            for (int i = 0; i < _mines.Count; i++)
                if (!_mines[i].Triggered)
                    into.Add(new HazardView { Kind = 0, X = _mines[i].X, Y = _mines[i].Y, Z = _mines[i].Z, Radius = _mines[i].Radius });
            for (int i = 0; i < _fires.Count; i++)
                if (_fires[i].TurnsLeft > 0)
                    into.Add(new HazardView { Kind = _fires[i].Tag == 1 ? 2 : 1, X = _fires[i].X, Y = _fires[i].Y, Z = _fires[i].Z, Radius = _fires[i].Radius, TurnsLeft = _fires[i].TurnsLeft });
        }

        readonly List<Mine> _mines = new();
        readonly List<FireField> _fires = new();

        /// <summary>지뢰를 착지점에 설치. 반경 내로 접근하면 1회 폭발.</summary>
        public void PlaceMine(float x, float y, float z, float radius, int dmg, int ownerId)
        {
            _mines.Add(new Mine { X = x, Y = y, Z = z, Radius = radius, Damage = dmg, OwnerId = ownerId, Triggered = false });
        }

        /// <summary>지속불을 착지점에 설치. 반경 내 유닛이 매 턴 피해를 입는다.</summary>
        /// <summary>
        /// 눈이 오면 장판이 사라진다 — **원작 규칙**이다.
        ///   "눈이 내리면 포세이돈의 파워가 증폭되며, 카터펄트의 불과 듀크탱크의 독가스는 사라진다"
        ///   (https://namu.wiki/w/포트리스2 · 2026-09-17 조회)
        /// 지뢰는 장판이 아니라 설치물이라 남긴다(원작 서술도 불·독가스만 말한다).
        /// </summary>
        /// <returns>지운 장판 수</returns>
        public int ClearFires()
        {
            int n = 0;
            for (int i = 0; i < _fires.Count; i++) if (_fires[i].TurnsLeft > 0) n++;
            _fires.Clear();
            return n;
        }

        public void PlaceFire(float x, float y, float z, float radius, int dmgPerTurn, int turns, int tag = 0)
        {
            _fires.Add(new FireField { X = x, Y = y, Z = z, Radius = radius, DmgPerTurn = dmgPerTurn, TurnsLeft = turns, Tag = tag });
        }

        /// <summary>유닛이 그 위치에 있을 때 받을 피해(지뢰 폭발 + 지속불).</summary>
        public int OnUnitAt(int id, TankKind kind, Vec3 pos)
        {
            int dmg = 0;

            // 지뢰: 레이저·포세이돈은 무시, 나머지는 반경 내 1회 폭발
            if (kind != TankKind.Laser && kind != TankKind.Poseidon)
            {
                for (int i = 0; i < _mines.Count; i++)
                {
                    if (_mines[i].Triggered) continue;
                    // ⚠️ 수평 거리만 본다. 설치물은 착탄점(파이기 전 지면)에 놓이는데 바로 그 자리에
                    //    크레이터가 파여 지면이 몇 m 내려간다 — 3D 거리로 재면 지뢰가 허공에 떠서 영원히 안 터진다.
                    //    실제로 120판 동안 지뢰 피해 0 이었다. 지면은 계속 변하니 높이는 판정에서 뺀다.
                    float dx = _mines[i].X - pos.X;
                    float dz = _mines[i].Z - pos.Z;
                    float dist = MathF.Sqrt(dx * dx + dz * dz);

                    if (dist <= _mines[i].Radius)
                    {
                        dmg += _mines[i].Damage;
                        var m = _mines[i];
                        m.Triggered = true;
                        _mines[i] = m;
                    }
                }
            }

            // 지속불: 반경 내에서 매 턴 피해
            for (int i = 0; i < _fires.Count; i++)
            {
                float dx = _fires[i].X - pos.X;
                float dz = _fires[i].Z - pos.Z;
                float dist = MathF.Sqrt(dx * dx + dz * dz);   // 지뢰와 같은 이유로 수평 거리

                if (dist <= _fires[i].Radius)
                {
                    dmg += _fires[i].DmgPerTurn;
                }
            }

            return dmg;
        }

        /// <summary>턴 시작 시 호출 — 지속불 남은 턴 감소.</summary>
        public void TickStartOfTurn()
        {
            for (int i = 0; i < _fires.Count; i++)
            {
                var f = _fires[i];
                f.TurnsLeft--;
                if (f.TurnsLeft <= 0) _fires.RemoveAt(i--);
                else _fires[i] = f;
            }
        }

        /// <summary>상태 리셋(시뮬레이션 테스트용).</summary>
        public void Clear()
        {
            _mines.Clear();
            _fires.Clear();
        }
    }

    /// <summary>위성탄(이온어태커 2번탄): 착탄점의 (X,Z)에서 수직 낙하, 오버행 무시.</summary>
    public static class SatelliteStrike
    {
        const float DropStep = 0.5f;  // [추정] 수직 낙하 검사 간격

        /// <summary>
        /// 착탄점의 X,Z 좌표에서 수직으로 내려가 마지막 지면(오버행 무시)에 닿는 점을 반환.
        /// 오버행 구조에서는 처마 아래 공간을 통과해 더 아래 지형을 찾는다.
        /// </summary>
        /// <param name="vol">지형 SDF 볼륨</param>
        /// <param name="x">착탄 X 좌표</param>
        /// <param name="z">착탄 Z 좌표</param>
        /// <param name="topY">낙하 시작 높이(보통 포탄 착탄점 Y)</param>
        /// <param name="boxes">충돌 대상 탱크 목록(null 가능)</param>
        /// <param name="directHitId">직격한 탱크 id(-1: 없음)</param>
        /// <returns>최종 낙하 착지점</returns>
        public static Vec3 Resolve(SdfVolume vol, float x, float z, float topY,
                                   IReadOnlyList<TankHitbox> boxes, out int directHitId)
        {
            directHitId = -1;

            // 마지막 지면 표면을 찾기 위해 모든 높이를 스캔
            float lastGroundY = vol.OriginY;
            bool wasInGround = false;

            for (float y = topY; y >= vol.OriginY - DropStep; y -= DropStep)
            {
                // 탱크 직격 검사
                if (boxes != null)
                {
                    for (int i = 0; i < boxes.Count; i++)
                    {
                        var box = boxes[i];
                        float dx = box.Center.X - x;
                        float dz = box.Center.Z - z;
                        float dist = MathF.Sqrt(dx * dx + dz * dz);

                        if (dist <= box.Radius)
                        {
                            directHitId = box.Id;
                            return box.Center;
                        }
                    }
                }

                // 지형 충돌 검사
                float sdf = vol.SampleWorld(x, y, z);

                if (sdf <= 0f && !wasInGround)
                {
                    // 첫 번째 지형 접촉
                    lastGroundY = y;
                    wasInGround = true;
                }
                else if (wasInGround && sdf > 0f)
                {
                    // 첫 지형 이후 다시 공기 — 오버행 감지
                    // 계속 내려가서 아래 지면을 찾는다
                    wasInGround = false;
                    // 재지면 감지 안 하고 계속 내려감
                }
            }

            // 첫 지면 높이에서 착지 (단순 지형) 또는 오버행 아래로 내려간 후의 마지막 높이
            return new Vec3(x, lastGroundY, z);
        }
    }

    /// <summary>다탄두(멀티미사일, 레이저, 슈퍼탱크): 한 번 쏘면 N발이 각도 편차를 두고 날아감.</summary>
    public static class Spread
    {
        public struct ShotPattern
        {
            public float YawOffsetDeg;       // 좌우 각도 편차
            public float PitchOffsetDeg;     // 상하 각도 편차
            public float DamageScale;         // 피해 배율(풀히트 보정용)
        }

        /// <summary>탱크와 탄종에 따른 다탄두 패턴. 단발이면 배열 길이 1.</summary>
        /// <summary>
        /// 다탄두 편차 패턴. **발사면 안(상하)에서만** 벌어진다.
        ///
        /// ⚠️ 처음엔 좌우(yaw) ±12~20°, 레이저는 120°/240° 로 뒀는데 그건 2D 원작을 잘못 옮긴 것이다.
        ///    150m 사거리에서 yaw 12° 는 옆으로 31m 라 바깥 탄이 전부 빗나가고, 레이저는 옆을 쏜다.
        ///    원작 부채꼴은 화면(발사면) 안에서 벌어져 **앞뒤로** 떨어진다 — 여기서는 pitch 편차다.
        ///    각도는 전부 [추정]. "집탄 시 고피해"가 되려면 좁아야 한다.
        /// </summary>
        public static IReadOnlyList<ShotPattern> Pattern(TankKind kind, ShellKind shell)
            => Pattern(kind, shell, false);

        /// <summary>
        /// 궁극기(§49)는 **연사형 기종만** 탄두를 늘린다(오너 지시 2026-09-17: 기종별 차별점).
        ///
        /// 전 기종의 탄두를 늘리면 궁극기가 다시 하나가 된다 — 화력형(캐논·크로스보우)은 단발로 남겨야
        /// "한 방"이라는 축이 살고, 굴착형(캐롯·이온)은 반경으로, 지속형은 장판으로 간다(NiceShot.ApplyUltimate).
        /// ⚠️ 수는 전부 [추정]. 늘릴수록 집탄이 흩어져 오히려 약해질 수 있으니 하네스로 재고 조정하라.
        /// </summary>
        public static IReadOnlyList<ShotPattern> Pattern(TankKind kind, ShellKind shell, bool ultimate)
        {
            if (ultimate && shell == ShellKind.Special)
                switch (kind)
                {
                    case TankKind.MultiMissile: return Fan(12, 1.8f);   // 9연 → 탄막
                    case TankKind.SuperTank:    return Fan(11, 1.8f);   // 9연 유도탄 → 11연
                    case TankKind.Laser:        return Fan(5, 0.9f);    // 3연 회전 → 5연, 더 촘촘히
                    case TankKind.Catapult:     return Fan(4, 3.2f);    // 투석기 → 넓게 흩뿌린다
                    case TankKind.MineLander:   return Fan(3, 2.6f);    // 지뢰밭
                    case TankKind.Missile:      return Fan(4, 2.0f);    // 4단 폭발
                    case TankKind.Carrot:       return Fan(5, 2.0f);    // 삼연 → 오연
                    case TankKind.IonAttacker:  return Fan(3, 2.2f);    // 궤도 폭격 3발
                    case TankKind.Poseidon:     return Fan(3, 1.5f);    // 190×2 → 3발
                    case TankKind.SecWind:      return Fan(3, 1.5f);
                    // 캐논·크로스보우·듀크는 단발 유지 — 각각 한 방·직격·장판이 축이다.
                }

            if (shell == ShellKind.Normal)
            {
                // 멀티미사일 1번탄 — 원작 175×3 부채꼴.
                // ⚠️ 간격 2.5° 는 150m 에서 발 사이 6.5m 인데 폭발 반경이 5.8m 라 **세 발이 전부 겹쳐 맞았다**
                //    (175×3=525 가 통째로 들어가 승률 75% ⬆). 부채꼴은 퍼져야 부채꼴이다 —
                //    4.5° 면 발 사이 11.8m 로 폭발이 안 겹쳐, 전부 맞히려면 조준이 맞아야 한다.
                if (kind == TankKind.MultiMissile) return Fan(3, 4.5f);
                // 미사일 1번탄 — 원작 **4단 폭발(120-90-60-30)**.
                // ⚠️ 예전엔 합계 300 을 **단발**로 근사했다. 그러면 나눠 들어가야 할 피해가 직격 한 방에 전부 들어가
                //    원작보다 훨씬 강해진다 — 실측 승률 76%(⬆ 2위)의 원인이었다.
                //    네 발로 쪼개고 원작 비율(0.40/0.30/0.20/0.10)을 DamageScale 로 준다. 합은 그대로 300 이지만
                //    흩어져 떨어지므로 **전부 맞히려면 잘 쏴야 한다** — 그게 원작의 4단 폭발이다.
                if (kind == TankKind.Missile) return MultiStage;
                // ⚠️ 세크윈드 1번탄은 원작대로 **단발**(220). 처음에 2발로 뒀더니 2번탄(170×2)이 항상 열세라
                //    120판 동안 한 번도 안 쓰였다 — 원작을 벗어난 변형이 선택지를 죽였다.
                return Fan(1, 0f);
            }
            switch (kind)
            {
                case TankKind.MultiMissile: return Fan(9, 2.6f);   // 60×9 부채꼴 — 1번탄과 같은 이유로 벌린다
                case TankKind.SuperTank:    return Fan(9, 2.0f);   // 9연 유도탄 [추정]
                case TankKind.Laser:        return Fan(3, 1.0f);   // 3연 회전 — 아주 촘촘 [추정 ±1°]
                case TankKind.Carrot:       return Fan(3, 2.0f);   // 삼연포탄 [추정]
                case TankKind.SecWind:      return Fan(2, 1.5f);   // 170×2 [추정]
                case TankKind.Poseidon:     return Fan(2, 1.5f);   // 원작 190×2 — 빠져 있어서 1발로 계산됐고 한 번도 안 쓰였다
                default:                    return Fan(1, 0f);
            }
        }

        /// <summary>n 발을 pitch 로 step 씩 벌린다(가운데 0). 피해 배율은 전부 1 — 발당 피해는 TankStats 가 정한다.</summary>
        /// <summary>미사일 1번탄 4단 폭발. 원작 피해 배분(120-90-60-30)을 비율로 옮겼다.</summary>
        static readonly ShotPattern[] MultiStage =
        {
            new ShotPattern { YawOffsetDeg = 0f, PitchOffsetDeg = -1.8f, DamageScale = 0.40f },
            new ShotPattern { YawOffsetDeg = 0f, PitchOffsetDeg = -0.6f, DamageScale = 0.30f },
            new ShotPattern { YawOffsetDeg = 0f, PitchOffsetDeg =  0.6f, DamageScale = 0.20f },
            new ShotPattern { YawOffsetDeg = 0f, PitchOffsetDeg =  1.8f, DamageScale = 0.10f },
        };

        static ShotPattern[] Fan(int n, float stepDeg)
        {
            var arr = new ShotPattern[n];
            for (int i = 0; i < n; i++)
                arr[i] = new ShotPattern { YawOffsetDeg = 0f, PitchOffsetDeg = (i - (n - 1) * 0.5f) * stepDeg, DamageScale = 1f };
            return arr;
        }
    }

    /// <summary>각 탱크의 2번탄이 어떤 효과를 갖는지 선언.</summary>
    public static class ShellEffects
    {
        public enum EffectType
        {
            None,           // 효과 없음(배율만)
            Poison,         // 독(크로스보우, 듀크)
            Burn,           // 화상(캐터펄트)
            PoisonCloud,    // 독구름(듀크) — 착탄점에 남아 안에 선 유닛이 매 턴 피해(장판은 HazardField.PlaceFire 와 같은 구조)
            Root,           // 이동금지(포세이돈)
            Mine,           // 지뢰(마인랜더)
            SatelliteStrike, // 위성탄(이온어태커)
            Homing,          // 유도탄(미사일 2번, 슈퍼탱크 2번) — 착탄점이 근처 적에게 끌린다
        }

        /// <summary>
        /// 유도탄 보정 [추정]. 착탄점에서 <see cref="HomingRange"/> 안에 적이 있으면 그 중심으로 끌린다.
        /// 원작 "녹색 유도탄"의 유도를 탄도 변경 없이 착탄점 보정으로 옮긴 것 — 하네스·게임이 같은 함수를 쓴다.
        /// </summary>
        public const float HomingRange = 12f;
        public static Vec3 HomingCorrect(Vec3 impact, IReadOnlyList<TankHitbox> targets, int shooterId, int shooterTeamOfIds, System.Func<int, bool> isEnemy, out int directId)
        {
            directId = -1;
            float best = HomingRange; Vec3 bestC = impact;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i].Id == shooterId || !isEnemy(targets[i].Id)) continue;
                float d = (targets[i].Center - impact).Length;
                if (d < best) { best = d; bestC = targets[i].Center; directId = targets[i].Id; }
            }
            return bestC;
        }

        /// <summary>탱크-탄종 조합의 효과 타입과 수치.</summary>
        public struct EffectInfo
        {
            public EffectType Type;
            public int Param1;  // 피해/회복량 등
            public int Param2;  // 지속 턴수 등
        }

        /// <summary>탱크와 탄종에 따른 효과 정보.</summary>
        public static EffectInfo Of(TankKind kind, ShellKind shell)
        {
            if (shell == ShellKind.Normal) return new EffectInfo { Type = EffectType.None };

            // 특수탄(2번탄)만 효과 있음
            return kind switch
            {
                TankKind.CrossBow => new EffectInfo { Type = EffectType.Poison, Param1 = 25, Param2 = 3 },   // [추정] 독 25/턴, 3턴
                // ⚠️ 처음엔 "맞은 유닛에 독"(Poison 35×3)으로 옮겼는데 원작 독구름은 **자리에 남는 구름**이다.
                //    맞아야만 붙는 독으로는 2번탄/판 0.9·승률 29%(꼴찌) — 구름이 남아야 지형 위 구역 봉쇄가 된다. 바람에 흐르는 것은 미구현.
                TankKind.Duke => new EffectInfo { Type = EffectType.PoisonCloud, Param1 = 40, Param2 = 3 }, // [추정] 독구름 40/턴, 3턴, 반경 = 폭발 반경
                TankKind.Catapult => new EffectInfo { Type = EffectType.Burn, Param1 = 40, Param2 = 3 },     // [추정] 화상 40/턴, 3턴
                TankKind.Poseidon => new EffectInfo { Type = EffectType.Root, Param1 = 2, Param2 = 0 },      // 이동금지 2턴
                TankKind.MineLander => new EffectInfo { Type = EffectType.Mine, Param1 = 200, Param2 = 0 },  // [추정] 지뢰 피해 200
                TankKind.IonAttacker => new EffectInfo { Type = EffectType.SatelliteStrike, Param1 = 0, Param2 = 0 }, // 위성탄(별도 위치 계산)
                TankKind.Missile => new EffectInfo { Type = EffectType.Homing, Param1 = 0, Param2 = 0 },      // 원작 "녹색 유도탄"
                TankKind.SuperTank => new EffectInfo { Type = EffectType.Homing, Param1 = 0, Param2 = 0 },    // 원작 "9연 유도탄"
                _ => new EffectInfo { Type = EffectType.None },
            };
        }
    }
}
