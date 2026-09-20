using System;
using System.Collections.Generic;

namespace Tankfall.Sim
{
    /// <summary>한 발이 터져서 «한 유닛에게» 일어난 일. **사실이지 판단이 아니다.**</summary>
    public struct BlastHit
    {
        public int UnitId;
        public int Team;
        /// <summary>방어 적용 **후** 피해. **0 은 이 목록에 안 들어온다**(계약: 대상 목록 = 피해 > 0).</summary>
        public int Damage;
        /// <summary>탄이 이 유닛에 **직격**했는가(직격 보너스가 붙었다).</summary>
        public bool Direct;
        /// <summary>
        /// **실드가 막았는가.** 🔑 **막혔어도 여기 «남는다»** — 「막혔지만 맞긴 맞았다」를
        /// **명중으로 볼지는 호출부마다 다르다**(전적은 「안 셈」 · 연출은 「실드 이펙트」 ·
        /// 서버는 「막혔다는 사실을 보내야 함」). **그릇은 사실만 내고 판정은 호출부가 한다.**
        /// </summary>
        public bool ShieldBlocked;
    }

    /// <summary>한 발이 터진 결과 — **사실의 목록**. §10 `TurnResult` 의 몸통이 된다.</summary>
    public struct BlastFacts
    {
        /// <summary>
        /// **가해자가 있는가.** 지뢰·서든데스·턴시작 지속피해처럼 **«쏜 사람»이 없는 피해**가 있다.
        /// 🔑 지금 `BattleDemo.CountDamage` 의 `credited: bool` 이 이것의 **압축판**이다 —
        ///    「누구 «것»인가」를 「누구 것이긴 «한가»」로 줄인 것. 여기서는 **값으로 되돌린다.**
        /// </summary>
        public bool HasAttacker;
        public int AttackerId, AttackerTeam;
        public List<BlastHit> Hits;
    }

    /// <summary>
    /// 🧨 **한 발의 폭발을 «푼다»** — §8 이 이름과 책임을 이미 적어 둔 그릇이다
    /// (`ExplosionResolver.cs — 피해 배분(§6) + CraterRequest 생성`).
    /// 계약: `docs/EXPLOSION_RESOLVER_CONTRACT.md`
    ///
    /// 🔑 **설계 원칙 셋**
    ///  1. **사실만 낸다. 판단은 호출부.** 집계(팝업·소리·전적)를 여기서 하면 **Sim 이 UI 상태를 갖게 되어**
    ///     §4-1(SIM 은 순수·결정론)을 깬다. 「명중인가」·「전적에 셀까」도 호출부가 정한다.
    ///  2. **전역·필드를 안 읽는다. 전부 인자.** 그래야 게임(`BattleDemo`)에서 떼어낼 때
    ///     지금 흩어져 있는 «현재 사수» 필드들이 **자연히 인자가 된다**(📌 M5 선행 목록).
    ///  3. **`UnityEngine` 무참조**(§4-1). 벡터는 <see cref="Vec3"/>.
    ///
    /// ⚠️ **이 조각의 범위는 «피해 배분»까지다.** 장판·설치물 파괴는 **다음 덩어리**다 —
    ///    한 번에 여럿 옮기면 **표가 움직였을 때 어느 것 때문인지 못 가른다.**
    /// </summary>
    public static class ExplosionResolver
    {
        /// <summary>폭발 반경 안에 있을 수 있는 후보. **상태가 아니라 «그 순간의 값»만** 넘긴다.</summary>
        public struct Candidate
        {
            public int Id, Team;
            public Vec3 Center;
            public float Defense;
        }

        /// <summary>
        /// 🚨 **배율이 곱해지는 «순서»가 계약이다**(경계 #8).
        /// `Damage.Compute` 에 **이미 곱한 값**을 넘긴다 — 감쇠 뒤에 곱하면 **정수 내림에서 갈린다.**
        /// (게임도 같은 순서다: `Damage.Compute(dist, r, BaseDamage * scale, DirectDamage * scale, ...)`)
        /// </summary>
        /// <param name="directHitId">탄이 직접 맞힌 유닛 Id. 없으면 -1.</param>
        /// <param name="consumeShield">
        /// 실드를 «소모»하고 막았는지 답하는 것. 아이템 상태는 그릇이 안 갖는다 — 호출부가 준다.
        /// null 이면 실드가 없는 판으로 본다.
        /// </param>
        public static BlastFacts Resolve(
            Vec3 impact, float radius, float baseDamage, float directDamage, float damageScale,
            int directHitId,
            IReadOnlyList<Candidate> candidates,
            bool hasAttacker, int attackerId, int attackerTeam,
            Func<int, bool> consumeShield = null)
        {
            var facts = new BlastFacts
            {
                HasAttacker = hasAttacker,
                AttackerId = attackerId,
                AttackerTeam = attackerTeam,
                Hits = new List<BlastHit>(),
            };
            if (candidates == null) return facts;

            float b = baseDamage * damageScale;
            float d = directDamage * damageScale;

            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                float dist = (c.Center - impact).Length;

                // ⚠️ `Damage.Falloff` 가 `dist >= radius` 를 0 으로 잘라 준다 — 여기서 반경 밖을 따로 안 거른다.
                //    **「반경 안」과 「피해가 있다」는 다른 집합**이고(감쇠가 ^1.3 이라 마지막 0.5~0.7% 는 0),
                //    계약이 정한 집합은 **뒤쪽**이다.
                bool direct = directHitId >= 0 && c.Id == directHitId;
                int raw = Damage.Compute(dist, radius, b, d, direct);
                int dmg = Damage.AfterDefense(raw, c.Defense);

                // 🔑 **계약 고정: 대상 목록 = 「피해 > 0 인 유닛」.** 반경 «안»이어도 0 이면 뺀다.
                //    게임(`CountDamage` 조기 반환)과 하네스(`dmg<=0 → continue`) 가 **둘 다 그렇게 센다**
                //    (2026-09-20 확인). 여기서 반대로 짜면 **명중%가 움직인다.**
                if (dmg <= 0) continue;

                bool blocked = consumeShield != null && consumeShield(c.Id);

                facts.Hits.Add(new BlastHit
                {
                    UnitId = c.Id,
                    Team = c.Team,
                    Damage = dmg,
                    Direct = direct,
                    ShieldBlocked = blocked,   // 🔑 막혀도 «남긴다» — 판정은 호출부
                });
            }
            return facts;
        }
    }
}
