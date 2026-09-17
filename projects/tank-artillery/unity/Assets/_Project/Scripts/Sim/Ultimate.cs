// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §2-9-12 (궁극기 — 기획서 §49 "궁극 충전")
//
// === 출처 — 여기서만큼은 "원작 그대로"라고 말할 수 없다 ===
// 오너 규칙대로 **조사부터** 했다. 찾아본 곳(2026-09-17 조회):
//   https://namu.wiki/w/포트리스2             — SS·나이스샷·스킬포인트·필살기 항목 없음
//   https://namu.wiki/w/포트리스2/게임 용어   — 용어집에 해당 항목 없음
//   https://namu.wiki/w/포트리스 V2           — 리메이크작. 턴제·2번무기 쿨타임만 있고 궁극 게이지 없음
// 웹검색("포트리스2 나이스샷 SS 스킬포인트", "포트리스2 SS샷 조건")도 전부 팀 포트리스2(다른 게임)로 샜다.
// **결론: 원작의 궁극기 규칙은 인용할 수 있는 근거가 없다.** 그래서 이 파일의 규칙은 원작이 아니라
// **오너 기획서 §49("궁극 충전")에서 유도**했고, 수치·효과는 전부 [추정]이다. 원작이라고 말하지 않는다.
//
// === 설계 원칙 (아이템 §2-9-10 과 같다) ===
// 효과는 **이미 동작하는 시스템만 조합**해서 만든다 — 없는 시스템 위에 선언만 하면 죽은 값이 된다.
// 여기 5종은 각각 (피해배율) · (다탄두) · (실드+회복) · (SDF 굴착+낙하 §28) · (상태해제+팀회복) 의
// 재사용이다. 전부 이미 게임과 하네스 양쪽에서 도는 것들이다.
//
// === SS(NiceShot) 와 무엇이 다른가 ===
// 겹치면 안 되므로 역할을 갈랐다:
//   · SS      = 나이스샷 2점 → 2번탄이 1.6배. **자주, 작게.** 조작 보상이다.
//   · 궁극기  = 게이지 100 → 기종 고유 효과. **드물게, 크게.** 판을 뒤집는 한 방이다.
// 궁극 게이지는 나이스샷으로만 차지 않는다 — **주고받은 피해로도 찬다**(기획서가 궁극 충전을
// TankHealth 에 달아둔 이유로 읽었다). 맞고 있는 쪽도 반격 기회를 얻는다 [추정].

using System;
using System.Collections.Generic;

namespace Tankfall.Sim
{
    public enum UltimateKind
    {
        None = 0,
        Overcharge,   // 과충전 — 이번 사격 피해 2배·폭발 반경 1.4배
        Volley,       // 연사 — 같은 각도·파워로 3발
        Bunker,       // 벙커 — 이번 라운드 받는 피해 절반 + 체력 25% 회복
        Quake,        // 지진 — 착탄점 굴착 반경 2.2배(발밑을 무너뜨려 낙하 피해 §28 를 노린다)
        Purge,        // 정화 — 자기 상태이상 해제 + 팀 전원 20% 회복
    }

    public struct UltimateInfo
    {
        public UltimateKind Kind;
        public string Name;
        public string Desc;
        /// <summary>사격과 함께 터지는가(쏘기 전에 걸어두는 종류).</summary>
        public bool AppliesToShot;
    }

    public static class Ultimate
    {
        /// <summary>게이지 만충 값.</summary>
        public const float Full = 100f;
        /// <summary>나이스샷 한 번이 채우는 양 [추정] — 3번 성공하면 거의 찬다.</summary>
        public const float ChargeNiceShot = 30f;
        /// <summary>내가 준 피해 1당 충전 [추정].</summary>
        public const float ChargePerDamageDealt = 0.05f;
        /// <summary>내가 받은 피해 1당 충전 [추정] — 지는 쪽이 더 빨리 찬다(역전 여지).</summary>
        public const float ChargePerDamageTaken = 0.09f;

        public const float OverchargeDamage = 2.0f, OverchargeBlast = 1.4f;
        public const int VolleyShots = 3;
        public const float VolleySpreadDeg = 2.5f;      // 3발이 완전히 겹치면 한 발과 같다
        public const float BunkerDamageTaken = 0.5f;
        public const float BunkerHeal = 0.25f;
        public const float QuakeCrater = 2.2f;
        public const float PurgeTeamHeal = 0.20f;

        /// <summary>
        /// 기종별 궁극기 [추정 — 원작 근거 없음]. 그 기종이 이미 잘하는 것을 **더 크게** 하는 쪽으로 골랐다:
        /// 한 방 기종은 과충전, 다발 기종은 연사, 튼튼한 기종은 벙커, 굴착 기종은 지진, 보조 기종은 정화.
        /// </summary>
        public static UltimateKind Of(TankKind k)
        {
            switch (k)
            {
                case TankKind.Catapult: return UltimateKind.Quake;       // 곡사 굴착
                case TankKind.CrossBow: return UltimateKind.Volley;      // 다발
                case TankKind.Cannon: return UltimateKind.Overcharge;    // 정직한 한 방
                case TankKind.Carrot: return UltimateKind.Volley;
                case TankKind.Duke: return UltimateKind.Overcharge;
                case TankKind.MineLander: return UltimateKind.Quake;     // 지면을 다루는 기종
                case TankKind.Missile: return UltimateKind.Overcharge;
                case TankKind.MultiMissile: return UltimateKind.Volley;  // 원래 다탄두
                case TankKind.SuperTank: return UltimateKind.Bunker;     // 원래 튼튼함
                case TankKind.Laser: return UltimateKind.Overcharge;
                case TankKind.IonAttacker: return UltimateKind.Quake;    // 원래 굴착이 크다
                case TankKind.Poseidon: return UltimateKind.Purge;       // 조건부·보조 기종
                case TankKind.SecWind: return UltimateKind.Bunker;       // 저체력에서 강해지는 기종
                default: return UltimateKind.Overcharge;
            }
        }

        public static UltimateInfo Get(UltimateKind k)
        {
            switch (k)
            {
                case UltimateKind.Overcharge: return new UltimateInfo { Kind = k, Name = "과충전", Desc = "이번 사격 피해 2배·폭발 1.4배", AppliesToShot = true };
                case UltimateKind.Volley: return new UltimateInfo { Kind = k, Name = "연사", Desc = "같은 각도·파워로 3발", AppliesToShot = true };
                case UltimateKind.Bunker: return new UltimateInfo { Kind = k, Name = "벙커", Desc = "이번 라운드 피해 절반 + 체력 25% 회복", AppliesToShot = false };
                case UltimateKind.Quake: return new UltimateInfo { Kind = k, Name = "지진", Desc = "착탄 굴착 2.2배 — 발밑을 무너뜨린다", AppliesToShot = true };
                case UltimateKind.Purge: return new UltimateInfo { Kind = k, Name = "정화", Desc = "상태이상 해제 + 팀 전원 20% 회복", AppliesToShot = false };
                default: return new UltimateInfo { Kind = UltimateKind.None, Name = "없음", Desc = "" };
            }
        }

        /// <summary>과충전·지진처럼 사격 수치를 바꾸는 궁극기를 적용한다.</summary>
        public static TankStats ApplyToShot(TankStats st, UltimateKind k)
        {
            switch (k)
            {
                case UltimateKind.Overcharge:
                    st.BaseDamage *= OverchargeDamage;
                    st.DirectDamage *= OverchargeDamage;
                    st.BlastRadius *= OverchargeBlast;
                    st.Name = "[궁]" + st.Name;
                    break;
                case UltimateKind.Quake:
                    st.CraterRadius *= QuakeCrater;
                    st.Name = "[궁]" + st.Name;
                    break;
                case UltimateKind.Volley:
                    st.Name = "[궁]" + st.Name;
                    break;
            }
            return st;
        }
    }

    /// <summary>
    /// 유닛별 궁극 게이지와 지속 효과. **게임과 하네스가 같은 이 클래스를 쓴다**
    /// (한쪽만 궁극기를 쓰면 승률이 게임의 승률이 아니다 — §2-9-1 의 교훈).
    /// </summary>
    public class UltimateState
    {
        readonly Dictionary<int, float> _gauge = new();
        readonly HashSet<int> _armed = new();       // 이번 사격에 궁극기가 걸린 유닛
        readonly Dictionary<int, int> _bunker = new();  // 벙커 남은 턴

        public void Clear() { _gauge.Clear(); _armed.Clear(); _bunker.Clear(); }

        public float Gauge(int id) => _gauge.TryGetValue(id, out float g) ? g : 0f;
        public bool Ready(int id) => Gauge(id) >= Ultimate.Full;
        public bool Armed(int id) => _armed.Contains(id);
        public bool HasBunker(int id) => _bunker.ContainsKey(id);

        void Add(int id, float v)
        {
            float g = Gauge(id) + v;
            _gauge[id] = g > Ultimate.Full ? Ultimate.Full : g;
        }

        public void OnNiceShot(int id) => Add(id, Ultimate.ChargeNiceShot);
        public void OnDamageDealt(int id, int dmg) { if (dmg > 0) Add(id, dmg * Ultimate.ChargePerDamageDealt); }
        public void OnDamageTaken(int id, int dmg) { if (dmg > 0) Add(id, dmg * Ultimate.ChargePerDamageTaken); }

        /// <summary>벙커가 걸려 있으면 들어오는 피해를 줄인다. 피해 적용 **직전**에 통과시킨다.</summary>
        public int Mitigate(int id, int dmg)
            => HasBunker(id) ? (int)MathF.Round(dmg * Ultimate.BunkerDamageTaken) : dmg;

        /// <summary>턴 시작 — 벙커 수명을 깎는다.</summary>
        public void TickStartOfTurn(int id)
        {
            if (!_bunker.TryGetValue(id, out int t)) return;
            t--;
            if (t <= 0) _bunker.Remove(id); else _bunker[id] = t;
        }

        /// <summary>
        /// 궁극기 발동. 게이지를 전부 쓴다. 즉발형(벙커·정화)은 델리게이트로 호출부에 맡긴다
        /// (Sim 은 유닛 표현도 UnityEngine 도 모른다 — asmdef 규칙).
        /// </summary>
        public bool Fire(int id, int team, TankKind kind,
                         Action<int, float> healUnit, Action<int, float> healTeam, Action<int> cleanse)
        {
            if (!Ready(id)) return false;
            _gauge[id] = 0f;
            var k = Ultimate.Of(kind);
            switch (k)
            {
                case UltimateKind.Bunker:
                    _bunker[id] = 2;                       // 이번 턴 + 다음 턴 [추정]
                    healUnit?.Invoke(id, Ultimate.BunkerHeal);
                    break;
                case UltimateKind.Purge:
                    cleanse?.Invoke(id);
                    healTeam?.Invoke(team, Ultimate.PurgeTeamHeal);
                    break;
                default:
                    _armed.Add(id);                        // 사격과 함께 터진다
                    break;
            }
            return true;
        }

        /// <summary>사격이 끝나면 **반드시** 지운다. 안 지우면 궁극기가 영구 버프가 된다(아이템에서 겪은 함정).</summary>
        public void ClearShotFlags(int id) => _armed.Remove(id);
    }
}
