using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>캐릭터는 서로·몹·집과 겹치지 않는다.</summary>
    public static class UnitSeparationSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Unit Separation Self Check")]
        public static void Run()
        {
            int fail = 0;
            void Check(bool cond, string what)
            {
                if (!cond)
                {
                    fail++;
                    Debug.LogError("[UnitSeparationSelfCheck] FAIL  " + what);
                }
            }

            Check(UnitSeparation.AllyRadius > UnitSeparation.Radius,
                "파티 반경이 몹과 같으면 몸이 겹친다");

            var pos = new Vector2[4];
            var on = new[] { true, true, true, false };
            pos[0] = Vector2.zero;
            pos[1] = Vector2.zero;
            UnitSeparation.Resolve(pos, on, 2, 1f, UnitSeparation.AllyRadius);
            Check((pos[0] - pos[1]).magnitude >= UnitSeparation.AllyRadius * 1.6f,
                "같은 점의 캐릭터를 안 뗀다");

            pos[0] = Vector2.zero;
            pos[1] = Vector2.zero;
            UnitSeparation.Unstick(pos, on, 2, UnitSeparation.AllyRadius);
            Check((pos[0] - pos[1]).magnitude >= UnitSeparation.AllyRadius * 2f - 0.02f,
                "Unstick이 파티를 몸 너비만큼 안 뗀다");

            pos[0] = new Vector2(0.1f, 0f);
            var mob = new[] { Vector2.zero };
            var mobOn = new[] { true };
            var one = new[] { true };
            var ally = new[] { pos[0] };
            UnitSeparation.UnstickFrom(ally, one, 1, mob, mobOn, 1,
                                       UnitSeparation.AllyRadius + UnitSeparation.Radius);
            Check((ally[0] - mob[0]).magnitude >= UnitSeparation.AllyRadius + UnitSeparation.Radius - 0.02f,
                "캐릭터가 몹 위에 남는다");

            ally[0] = new Vector2(8f, 0f);
            var before = ally[0];
            UnitSeparation.UnstickFrom(ally, one, 1, mob, mobOn, 1,
                                       UnitSeparation.AllyRadius + UnitSeparation.Radius);
            Check((ally[0] - before).sqrMagnitude < 1e-6f,
                "멀리 있는 캐릭터를 끌어당긴다");

            ArenaLayout.Clear();
            ArenaLayout.AddObstacle(Vector2.zero, 2.6f);
            var step = ArenaLayout.Around(new Vector2(-4f, 0f), new Vector2(1.2f, 0f),
                                          UnitSeparation.AllyRadius);
            var next = new Vector2(-4f, 0f) + step;
            Check(Mathf.Abs(step.y) > 0.15f, "캐릭터가 집을 향해 곧장 들어간다");
            Check(next.magnitude >= 2.6f + UnitSeparation.AllyRadius - 0.05f,
                "비껴 간 자리가 집 안이다");
            ArenaLayout.Clear();

            if (fail == 0) Debug.Log("[UnitSeparationSelfCheck] PASS");
            else Debug.LogError($"[UnitSeparationSelfCheck] FAIL {fail}");
        }
    }
}
