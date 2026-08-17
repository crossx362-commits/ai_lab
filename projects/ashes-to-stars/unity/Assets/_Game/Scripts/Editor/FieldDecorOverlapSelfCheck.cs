using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>집·나무·바위가 같은 자리를 나눠 쓰지 않고, 몬스터는 고체를 돈다.</summary>
    public static class FieldDecorOverlapSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Field Decor Overlap Self Check")]
        public static void Run()
        {
            int fail = 0;
            void Check(bool cond, string what)
            {
                if (!cond) fail++;
                if (!cond) Debug.LogError("[FieldDecorOverlapSelfCheck] FAIL  " + what);
            }

            Check(FieldDecor.IsSolidObstacle("village_house_1"), "집은 고체");
            Check(FieldDecor.IsSolidObstacle("village_barn_0"), "헛간은 고체");
            Check(FieldDecor.IsSolidObstacle("field_tree_1"), "나무는 고체");
            Check(FieldDecor.IsSolidObstacle("field_rock_0"), "바위는 고체");
            Check(!FieldDecor.IsSolidObstacle("field_bush_0"), "덤불은 통과");
            Check(!FieldDecor.IsSolidObstacle("village_fence_0"), "울타리는 통과");
            Check(!FieldDecor.IsSolidObstacle("village_lamp_0"), "가로등은 통과");

            Check(FieldDecor.Footprint("village_house_1") > FieldDecor.Footprint("field_bush_0") + 1f,
                "집 그림이 덤불과 비슷하면 지붕이 겹친다");
            Check(FieldDecor.ObstacleRadius("field_tree_1") > FieldDecor.ObstacleRadius("field_bush_0"),
                "나무 반경이 덤불과 같으면 수관을 가로지른다");

            var o = Vector2.zero;
            Check(FieldDecor.WouldOverlap("village_house_0", o, "village_house_1", new Vector2(1f, 0f)),
                "집 두 채가 1유닛 간격이면 겹쳐야 한다");
            Check(!FieldDecor.WouldOverlap("village_house_0", o, "village_house_1", new Vector2(12f, 0f)),
                "집 두 채가 12유닛이면 떨어져야 한다");
            Check(FieldDecor.WouldOverlap("village_house_1", o, "field_tree_1", new Vector2(1.5f, 0f)),
                "나무와 집이 붙어 있으면 겹쳐야 한다");
            Check(!FieldDecor.WouldOverlap("village_house_1", o, "field_tree_1", new Vector2(14f, 0f)),
                "멀리 떨어진 나무는 집을 덮으면 안 된다");
            Check(FieldDecor.WouldOverlap("field_tree_0", o, "field_rock_0", new Vector2(0.4f, 0f)),
                "나무와 바위가 같은 점이면 겹친다");

            ArenaLayout.Clear();
            ArenaLayout.AddObstacle(Vector2.zero, FieldDecor.ObstacleRadius("village_house_1"));
            var mob = ArenaLayout.Resolve(new Vector2(0.3f, 0f), 0.35f);
            Check(mob.magnitude >= FieldDecor.ObstacleRadius("village_house_1") + 0.3f,
                "집 안의 몹을 밖으로 안 민다");
            ArenaLayout.Clear();

            if (fail == 0) Debug.Log("[FieldDecorOverlapSelfCheck] PASS");
            else Debug.LogError($"[FieldDecorOverlapSelfCheck] FAIL {fail}");
        }
    }
}
