using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>수비 배치가 출전에서 빠지고, 침략 본게임을 열지 않는지 검사한다.</summary>
    public static class DefenseStateSelfCheck
    {
        public static void Run()
        {
            DefenseState.ResetForTest();
            PartyState.ResetForTest();
            GameState.SetTowerFloorForTest(DefenseState.UnlockFloor);

            var roster = LifeSystem.GetCharacters();
            Debug.Assert(roster.Count >= 2,
                "[DefenseStateSelfCheck] 로스터 2명 미만 — 배치 소비를 검증할 수 없다");

            int a = -1, b = -1;
            for (int i = 0; i < roster.Count; i++)
            {
                if (!LifeSystem.IsAvailable(roster[i])) continue;
                if (a < 0) a = i;
                else { b = i; break; }
            }
            Debug.Assert(a >= 0 && b >= 0,
                "[DefenseStateSelfCheck] 출전 가능 캐릭터가 2명 미만이다");

            if (!PartyState.Contains(a))
                Debug.Assert(PartyState.Toggle(a), "[DefenseStateSelfCheck] 기준 출전 편성 실패");
            Debug.Assert(PartyState.Contains(a), "[DefenseStateSelfCheck] 출전 편성이 안 남았다");

            Debug.Assert(DefenseState.Toggle(a), "[DefenseStateSelfCheck] 수비 배치 실패");
            Debug.Assert(DefenseState.Contains(a) && DefenseState.Count == 1,
                "[DefenseStateSelfCheck] 배치 후 수비 목록이 비어 있다");
            Debug.Assert(!PartyState.Contains(a),
                "[DefenseStateSelfCheck] 수비에 둔 캐릭터가 출전에 남아 있다");
            Debug.Assert(!PartyState.Toggle(a),
                "[DefenseStateSelfCheck] 수비 중인 캐릭터를 다시 출전시켰다");

            DefenseState.ForgetInMemoryForTest();
            Debug.Assert(DefenseState.Contains(a),
                "[DefenseStateSelfCheck] 재기동 후 수비 배치가 사라졌다");

            Debug.Assert(DefenseState.Toggle(a), "[DefenseStateSelfCheck] 해임 실패");
            Debug.Assert(!DefenseState.Contains(a),
                "[DefenseStateSelfCheck] 해임 후에도 수비 목록에 남아 있다");
            PartyState.ResetForTest();
            if (!PartyState.Contains(a))
                Debug.Assert(PartyState.Toggle(a),
                    "[DefenseStateSelfCheck] 해임 후 출전 편성 실패");
            Debug.Assert(PartyState.Contains(a),
                "[DefenseStateSelfCheck] 해임 후 출전이 다시 안 된다");

            Debug.Assert(!string.IsNullOrEmpty(WorldMapScreen.InvasionUnlockFloor.ToString()),
                "[DefenseStateSelfCheck] 침략 해금 층 상수가 없다");
            // 침략 버튼은 수비 명단을 적으로 넣으면 안 된다 — GoBattle(WorldMap)만 유지.
            _ = nameof(WorldMapScreen);
            _ = nameof(EstateScreen);

            DefenseState.ResetForTest();
            PartyState.ResetForTest();
            Debug.Log("[DefenseStateSelfCheck] PASS");
        }
    }
}
