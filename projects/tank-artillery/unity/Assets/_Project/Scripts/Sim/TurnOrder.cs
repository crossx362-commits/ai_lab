// SIM 레이어 턴 순서 라이브러리 — 포트리스2 딜레이 기반 턴 시스템
//
// === 원작 포트리스2 규칙 ===
// 턴이 교대가 아니라 **누적 딜레이가 가장 작은 유닛이 다음에 행동**한다.
// 각 탱크는 기본 딜레이(530~580)를 가지며, 행동할 때마다 추가 딜레이가 누적된다.
// 따라서 기본 딜레이가 낮은 탱크는 판 전체에서 더 많은 턴을 얻는다.
//
// === 구현 ===
// 추정 추가 딜레이(원작 문서 미상, 기간 게임 스타일 추정):
//   · 일반탄: +0
//   · 특수탄: +기본 × 0.2  [추정] 특수탄은 장전·발사 과정이 더 길다고 가정
//   · 이동: +기본 × 0.1    [추정] 이동도 시간을 써 턴을 지연시킨다고 가정

using System;
using System.Collections.Generic;

namespace Tankfall.Sim
{
    /// <summary>
    /// 딜레이 기반 턴 순서 관리자. 포트리스2의 턴 규칙을 구현한다.
    /// UnityEngine 참조 금지(SIM 레이어).
    /// </summary>
    public sealed class TurnOrder
    {
        struct UnitDelay
        {
            public int Id;
            public int BaseDelay;
            public int Accumulated;
        }

        // [추정] 추가 딜레이 배율. 원작 문서 미상으로 추정값.
        const float SpecialDelayRatio = 0.2f;    // [추정] 특수탄: 기본 × 20%
        const float MovedDelayRatio = 0.1f;      // [추정] 이동: 기본 × 10%

        List<UnitDelay> _units = new List<UnitDelay>();
        int _actionsTaken;

        /// <summary>유닛을 등록한다. 게임 시작 시 모든 유닛을 여기에 추가해야 한다.</summary>
        public void Add(int id, int baseDelay)
        {
            if (baseDelay <= 0) throw new ArgumentException($"baseDelay must be positive, got {baseDelay}");
            _units.Add(new UnitDelay { Id = id, BaseDelay = baseDelay, Accumulated = 0 });
        }

        /// <summary>
        /// 다음 행동할 유닛을 반환한다.
        /// 누적 딜레이가 가장 작은 살아있는 유닛을 선택하고, **동률이면 먼저 등록된 쪽**이 우선한다.
        /// alive(id)가 false인 유닛은 절대 선택되지 않는다.
        ///
        /// ⚠️ 동률을 Id 로 깨면 안 된다. Id 는 A=0,1,2 / B=3,4,5 라서 판 시작(전원 0)에
        ///    A팀이 세 번 연속 행동한다 — 하네스에서 미러 승률 80~90% 를 만든 바로 그 버그다.
        ///    호출자가 팀 교대 순서로 Add 하면 그 순서가 그대로 동률 우선순위가 된다.
        /// </summary>
        public int Next(Func<int, bool> alive)
        {
            if (alive == null) throw new ArgumentNullException(nameof(alive));

            int bestIdx = -1;
            int bestAccum = int.MaxValue;

            for (int i = 0; i < _units.Count; i++)
            {
                var u = _units[i];
                if (!alive(u.Id)) continue;
                // 엄격히 작을 때만 교체 → 동률은 앞선 등록이 이긴다
                if (u.Accumulated < bestAccum)
                {
                    bestIdx = i;
                    bestAccum = u.Accumulated;
                }
            }

            if (bestIdx < 0) throw new InvalidOperationException("No alive unit found");
            return _units[bestIdx].Id;
        }

        /// <summary>
        /// 유닛이 행동을 완료했을 때 호출한다. 누적 딜레이를 갱신한다.
        ///   · 기본: BaseDelay
        ///   · 특수탄 사용: +BaseDelay × 20% [추정]
        ///   · 이동 했을 때: +BaseDelay × 10% [추정]
        /// </summary>
        public void Consume(int id, ShellKind shell, bool moved)
        {
            int idx = -1;
            for (int i = 0; i < _units.Count; i++)
                if (_units[i].Id == id) { idx = i; break; }
            if (idx < 0) throw new ArgumentException($"Unit {id} not registered");

            var u = _units[idx];
            int added = u.BaseDelay;

            // 특수탄 추가
            if (shell == ShellKind.Special)
                added += (int)MathF.Round(u.BaseDelay * SpecialDelayRatio);

            // 이동 추가
            if (moved)
                added += (int)MathF.Round(u.BaseDelay * MovedDelayRatio);

            u.Accumulated += added;
            _units[idx] = u;
            _actionsTaken++;
        }

        /// <summary>
        /// 이미 Consume 한 행동에 **추가 딜레이만** 얹는다(ActionsTaken 은 안 늘린다).
        /// 하네스처럼 "선택 직후 일단 Consume(Normal) → 나중에 탄종이 정해지는" 흐름용.
        /// 그렇게 하는 이유: 사격 실패로 continue 하는 경로가 여럿이라 끝에서 Consume 하면
        /// 한 경로라도 빠뜨리는 순간 그 유닛이 무한히 다시 뽑힌다.
        /// </summary>
        public void AddExtra(int id, ShellKind shell, bool moved)
        {
            int idx = -1;
            for (int i = 0; i < _units.Count; i++)
                if (_units[i].Id == id) { idx = i; break; }
            if (idx < 0) throw new ArgumentException($"Unit {id} not registered");
            var u = _units[idx];
            if (shell == ShellKind.Special) u.Accumulated += (int)MathF.Round(u.BaseDelay * SpecialDelayRatio);
            if (moved) u.Accumulated += (int)MathF.Round(u.BaseDelay * MovedDelayRatio);
            _units[idx] = u;
        }

        /// <summary>누적 누적된 턴 수. 라운드 유도용: 라운드 = ActionsTaken / 유닛수 + 1</summary>
        public int ActionsTaken => _actionsTaken;

        /// <summary>특정 유닛의 현재 누적 딜레이를 반환한다. HUD 표시용.</summary>
        public int Accumulated(int id)
        {
            for (int i = 0; i < _units.Count; i++)
                if (_units[i].Id == id) return _units[i].Accumulated;
            throw new ArgumentException($"Unit {id} not registered");
        }
    }
}
