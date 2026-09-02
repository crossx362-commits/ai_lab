using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>6초 캐스트·피격 취소·완료 시만 두루마리 소모.</summary>
    public static class EmergencyEscapeSelfCheck
    {
        public static void Run()
        {
            EmergencyEscape.ResetForTest();
            GameState.ResetAll();
            var oldKind = GameFlow.Kind;
            GameFlow.Kind = GameFlow.BattleKind.보스;

            Debug.Assert(!EmergencyEscape.TryBegin(),
                "[EmergencyEscapeSelfCheck] 두루마리 0개인데 캐스트가 시작됐다");

            GameState.Gain(Economy.LifeItem.ScrollOfReturn, 1);
            Debug.Assert(EmergencyEscape.TryBegin(),
                "[EmergencyEscapeSelfCheck] 두루마리 1개인데 캐스트가 안 시작된다");
            Debug.Assert(EmergencyEscape.Casting, "[EmergencyEscapeSelfCheck] 시작 후 Casting이 아니다");

            var mid = EmergencyEscape.Tick(1f, false);
            Debug.Assert(mid == EmergencyEscape.Phase.Casting
                         && GameState.Bag.GetCount(Economy.LifeItem.ScrollOfReturn) == 1,
                "[EmergencyEscapeSelfCheck] 캐스트 중에 두루마리를 미리 썼다");

            var cancel = EmergencyEscape.Tick(0.1f, true);
            Debug.Assert(cancel == EmergencyEscape.Phase.Cancelled
                         && !EmergencyEscape.Casting
                         && GameState.Bag.GetCount(Economy.LifeItem.ScrollOfReturn) == 1,
                "[EmergencyEscapeSelfCheck] 피격 취소 뒤 두루마리가 사라지거나 캐스트가 남았다");
            Debug.Assert(EmergencyEscape.IsFatalFailure(cancel),
                "[EmergencyEscapeSelfCheck] 피격 취소는 사망 처리할 탈출 실패여야 한다");

            Debug.Assert(EmergencyEscape.TryBegin(),
                "[EmergencyEscapeSelfCheck] 취소 후 재시작 실패");
            var done = EmergencyEscape.Tick(EmergencyEscape.CastSeconds, false);
            Debug.Assert(done == EmergencyEscape.Phase.Escaped
                         && GameState.Bag.GetCount(Economy.LifeItem.ScrollOfReturn) == 0,
                "[EmergencyEscapeSelfCheck] 6초 완료 후 소모·탈출이 아니다");
            Debug.Assert(!EmergencyEscape.IsFatalFailure(done),
                "[EmergencyEscapeSelfCheck] 6초 안 탈출 성공을 실패로 판정했다");

            Debug.Assert(!EmergencyEscape.TryBegin(),
                "[EmergencyEscapeSelfCheck] 다 쓴 뒤에도 캐스트가 시작된다");

            // 수동으로 ESC를 누르는 잡몹 웨이브도 같은 즉시 반응·6초 잠금 경로를 쓴다.
            EmergencyEscape.ResetForTest();
            GameState.ResetAll();
            GameFlow.Kind = GameFlow.BattleKind.잡몹웨이브;
            GameState.Gain(Economy.LifeItem.ScrollOfReturn, 1);
            Debug.Assert(EmergencyEscape.TryBegin(),
                "[EmergencyEscapeSelfCheck] 수동 잡몹 웨이브에서 긴급 탈출이 시작되지 않는다");
            Debug.Assert(!EmergencyEscape.TryBegin(),
                "[EmergencyEscapeSelfCheck] 6초 캐스트 중 긴급 탈출을 다시 시작할 수 있다");

            EmergencyEscape.ResetForTest();
            GameFlow.Kind = oldKind;
            Debug.Log("[EmergencyEscapeSelfCheck] PASS");
        }
    }
}
