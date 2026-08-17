using System;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>집은 그림만큼 막고, 몬스터는 지붕이 아니라 옆으로 돈다.</summary>
    public static class ArenaLayoutSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Arena Layout Self Check")]
        public static void Run()
        {
            float house = FieldDecor.ObstacleRadius("village_house_1");
            float bush = FieldDecor.ObstacleRadius("field_bush_0");
            float tree = FieldDecor.ObstacleRadius("field_tree_1");
            Debug.Assert(house > bush + 1f,
                "[ArenaLayoutSelfCheck] 집 반경이 덤불과 비슷하면 지붕을 가로지른다");
            Debug.Assert(house >= 2.6f,
                "[ArenaLayoutSelfCheck] 집 반경이 그림 절반보다 작다");
            Debug.Assert(tree > bush,
                "[ArenaLayoutSelfCheck] 나무 반경이 덤불과 같으면 수관을 가로지른다");

            ArenaLayout.Clear();
            ArenaLayout.AddObstacle(Vector2.zero, 2.6f);
            var inside = ArenaLayout.Resolve(new Vector2(0.2f, 0f), 0.35f);
            Debug.Assert(inside.magnitude >= 2.9f,
                "[ArenaLayoutSelfCheck] 집 안 좌표를 밖으로 안 민다");

            var step = ArenaLayout.Around(new Vector2(-4f, 0f), new Vector2(1.2f, 0f), 0.35f);
            Debug.Assert(Mathf.Abs(step.y) > 0.15f,
                "[ArenaLayoutSelfCheck] 집을 향해 걸으면 옆으로 비껴야 한다");
            var next = new Vector2(-4f, 0f) + step;
            Debug.Assert(next.magnitude >= 2.9f - 0.05f,
                "[ArenaLayoutSelfCheck] 비껴 간 자리가 집 안이면 안 된다");

            ArenaLayout.Clear();
            Debug.Log("[ArenaLayoutSelfCheck] PASS");
        }
    }
}
