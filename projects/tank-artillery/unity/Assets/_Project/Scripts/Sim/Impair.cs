// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §2-9-14 (화면 방해·조작 봉인 탄)
//
// === 출처 ===
// 원작 포트리스2 규칙은 **기억이 아니라 조사**로 가져왔다(오너 규칙).
//   https://namu.wiki/w/포트리스2/아이템   (2026-09-17 조회)
// 조사로 확인된 원문 효과와 지속 턴 — 여기 수치는 [추정]이 아니라 **원작 값**이다:
//   · 반전탄     "화면이 상하로 뒤집힌다"                                   3턴, 턴 소모
//   · 멀미탄     "화면이 물결치듯이 울렁거린다"                             4턴, 턴 소모
//   · 안개탄     "안개가 껴서 고립된다"                                     3턴, 턴 소모
//   · 바보탄     "모든 탱이 자기 자신의 탱+아이디로 보이며 자신의 턴이 와도
//                 화면이 자신의 위치로 오지 않는다"                          4턴, 턴 소모
//   · 각도고정탄 "각도를 조절할 수 없다"                                    3턴, 턴 소모
//   · 파워고정탄 "50% 이하의 힘으로 발사할 수 없게 된다"                    3턴, 턴 소모
//
// === 정직하게 적어두는 한계 ===
// 앞의 네 개는 **사람의 화면을 방해하는 것**이다. AI 에게는 화면이 없으므로 AI 대 AI 에서는
// 효과가 0 이다. 그런데 원작에서 이 탄들은 **턴을 소모**하므로, AI 가 AI 에게 쓰면 순손해다.
// 그래서 AI 정책은 "사람이 표적일 때만 쓴다" 로 뒀다 [추정] — 매치업 하네스(AI 대 AI)에서
// 이 열이 0 으로 찍히는 건 **죽은 시스템이 아니라 의도된 정책**이며, 이 탄들의 값어치는
// 하네스가 아니라 실제 게임에서만 판정할 수 있다. 숨기지 말고 표에 그대로 드러낸다.
// 반대로 각도고정·파워고정은 **조준 자체를 묶으므로 AI 에게도 실제 효과가 있다** — 하네스가 잰다.

using System;
using System.Collections.Generic;

namespace Tankfall.Sim
{
    public enum ImpairKind
    {
        None = 0,
        FlipScreen,   // 반전탄 — 화면 상하 반전
        Wobble,       // 멀미탄 — 화면 울렁임
        Fog,          // 안개탄 — 안개로 고립
        Confuse,      // 바보탄 — 모든 탱크가 자기로 보이고 카메라가 자기 턴에도 안 옴
        LockAngle,    // 각도고정탄 — 각도 조절 불가
        LockPower,    // 파워고정탄 — 50% 미만 파워로 발사 불가
    }

    public static class Impair
    {
        /// <summary>원작 표기 그대로의 지속 턴.</summary>
        public static int Turns(ImpairKind k)
        {
            switch (k)
            {
                case ImpairKind.FlipScreen: return 3;
                case ImpairKind.Wobble: return 4;
                case ImpairKind.Fog: return 3;
                case ImpairKind.Confuse: return 4;
                case ImpairKind.LockAngle: return 3;
                case ImpairKind.LockPower: return 3;
                default: return 0;
            }
        }

        /// <summary>파워고정탄의 하한 — 원작 "50% 이하의 힘으로 발사할 수 없게 된다".</summary>
        public const float PowerFloor = 0.5f;

        /// <summary>화면만 방해하는가(= AI 에게는 효과가 0 인가). 정직한 구분을 코드가 강제한다.</summary>
        public static bool IsScreenOnly(ImpairKind k)
            => k == ImpairKind.FlipScreen || k == ImpairKind.Wobble
            || k == ImpairKind.Fog || k == ImpairKind.Confuse;

        public static string Name(ImpairKind k)
        {
            switch (k)
            {
                case ImpairKind.FlipScreen: return "반전탄";
                case ImpairKind.Wobble: return "멀미탄";
                case ImpairKind.Fog: return "안개탄";
                case ImpairKind.Confuse: return "바보탄";
                case ImpairKind.LockAngle: return "각도고정탄";
                case ImpairKind.LockPower: return "파워고정탄";
                default: return "없음";
            }
        }
    }

    /// <summary>
    /// 유닛별로 걸린 방해 효과의 남은 턴. **게임과 하네스가 같은 이 클래스를 쓴다**(§2-9-1 의 교훈).
    /// 화면 방해는 게임만 그릴 수 있지만, **상태는 양쪽이 같이 들고 있어야** 턴 수명·해제가 어긋나지 않는다.
    /// </summary>
    public class ImpairState
    {
        readonly Dictionary<int, Dictionary<ImpairKind, int>> _on = new();

        public void Clear() => _on.Clear();

        Dictionary<ImpairKind, int> Bag(int id)
        {
            if (!_on.TryGetValue(id, out var d)) { d = new Dictionary<ImpairKind, int>(); _on[id] = d; }
            return d;
        }

        /// <summary>이 유닛에 효과를 건다. 이미 걸려 있으면 긴 쪽으로 갱신한다 [추정].</summary>
        public void Apply(int id, ImpairKind k)
        {
            if (k == ImpairKind.None) return;
            var d = Bag(id);
            int t = Impair.Turns(k);
            d[k] = d.TryGetValue(k, out int cur) && cur > t ? cur : t;
        }

        public bool Has(int id, ImpairKind k) => Bag(id).TryGetValue(k, out int t) && t > 0;

        public int TurnsLeft(int id, ImpairKind k) => Bag(id).TryGetValue(k, out int t) ? t : 0;

        /// <summary>이 유닛에 걸린 것이 하나라도 있는가(HUD 표시용).</summary>
        public bool Any(int id)
        {
            foreach (var kv in Bag(id)) if (kv.Value > 0) return true;
            return false;
        }

        /// <summary>턴 시작 — 이 유닛에 걸린 것들의 수명을 깎는다.</summary>
        public void TickStartOfTurn(int id)
        {
            var d = Bag(id);
            if (d.Count == 0) return;
            List<ImpairKind> gone = null;
            var keys = new List<ImpairKind>(d.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                int t = d[keys[i]] - 1;
                if (t <= 0) { (gone ??= new List<ImpairKind>()).Add(keys[i]); }
                else d[keys[i]] = t;
            }
            if (gone != null) for (int i = 0; i < gone.Count; i++) d.Remove(gone[i]);
        }

        /// <summary>파워고정탄이 걸려 있으면 하한을 올린다. 발사 직전에 통과시킨다.</summary>
        public float ClampPower(int id, float power)
            => Has(id, ImpairKind.LockPower) && power < Impair.PowerFloor ? Impair.PowerFloor : power;

        /// <summary>각도고정탄: 각도를 못 바꾼다 — 조준을 이 각도로 묶는다.</summary>
        public bool AngleLocked(int id) => Has(id, ImpairKind.LockAngle);
    }
}
