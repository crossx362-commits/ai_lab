using UnityEngine;
using Ulon.Shared;
using Ulon.Server;

namespace Ulon.Client
{
    public sealed partial class SliceHud : MonoBehaviour
    {
        // **캐릭터 생성 화면**(랩 ㉭) — 새 계정이 처음 보는 한 판(능력치·스킬 셋 고르기·제출).
        // 담는 것: 그 화면과 제출. 안 담는 것: 만들어진 뒤의 상시 화면·패널·행동 전송.
        void DrawCreate()
        {
            GUI.Box(new Rect(16, 16, 460, 520), "");
            GUI.Label(new Rect(28, 24, 430, 22), "캐릭터 생성  (직업 선택 없음)");
            GUI.Label(new Rect(28, 50, 80, 22), "이름");
            createName = GUI.TextField(new Rect(110, 48, 200, 24), createName ?? "");
            GUI.Label(new Rect(28, 80, 80, 22), "외형");
            if (GUI.Button(new Rect(110, 78, 80, 24), createAppear == 0 ? "[기사]" : "기사"))
                createAppear = 0;
            if (GUI.Button(new Rect(196, 78, 90, 24), createAppear == 1 ? "[민머리]" : "민머리"))
                createAppear = 1;
            if (GUI.Button(new Rect(292, 78, 80, 24), createAppear == 2 ? "[야만]" : "야만"))
                createAppear = 2;

            int leftStat = CharacterCreate.StatTotal - createStr - createDex - createInt;
            GUI.Label(new Rect(28, 112, 400, 22), "스탯 총합 " + CharacterCreate.StatTotal + "  남은 " + leftStat);
            DrawStat(28, 136, "STR", ref createStr, leftStat);
            DrawStat(28, 164, "DEX", ref createDex, leftStat);
            DrawStat(28, 192, "INT", ref createInt, leftStat);

            float leftSkill = CharacterCreate.SkillTotal - createAv - createBv - createCv;
            GUI.Label(new Rect(28, 228, 400, 22), "시작 스킬 3개 총합 " + CharacterCreate.SkillTotal + "  남은 " + leftSkill.ToString("0"));
            DrawSkillPick(28, 256, ref createA, ref createAv, createB, createC, leftSkill);
            DrawSkillPick(28, 284, ref createB, ref createBv, createA, createC, leftSkill);
            DrawSkillPick(28, 312, ref createC, ref createCv, createA, createB, leftSkill);

            if (!string.IsNullOrEmpty(createError))
                GUI.Label(new Rect(28, 350, 420, 40), createError);
            if (GUI.Button(new Rect(28, 400, 140, 32), "시작"))
                SubmitCreate();
        }

        void DrawStat(float x, float y, string label, ref int value, int remaining)
        {
            GUI.Label(new Rect(x, y, 50, 22), label);
            if (GUI.Button(new Rect(x + 54, y, 28, 22), "-") && value > CharacterCreate.StatMin)
                value--;
            GUI.Label(new Rect(x + 88, y, 40, 22), value.ToString());
            if (GUI.Button(new Rect(x + 128, y, 28, 22), "+") && remaining > 0 && value < CharacterCreate.StatEachMax)
                value++;
        }

        void DrawSkillPick(float x, float y, ref SkillId id, ref float value, SkillId otherA, SkillId otherB, float remaining)
        {
            if (GUI.Button(new Rect(x, y, 24, 22), "<"))
                id = NextSkill(id, otherA, otherB, -1);
            GUI.Label(new Rect(x + 28, y, 88, 22), SkillNames.KoreanOf(id));
            if (GUI.Button(new Rect(x + 118, y, 24, 22), ">"))
                id = NextSkill(id, otherA, otherB, 1);
            if (GUI.Button(new Rect(x + 150, y, 28, 22), "-") && value > 1f)
                value -= 5f;
            if (value < 1f)
                value = 1f;
            GUI.Label(new Rect(x + 184, y, 40, 22), value.ToString("0"));
            if (GUI.Button(new Rect(x + 224, y, 28, 22), "+") && remaining >= 5f && value + 5f <= CharacterCreate.SkillEachMax)
                value += 5f;
        }

        static SkillId NextSkill(SkillId current, SkillId skipA, SkillId skipB, int dir)
        {
            int n = (int)SkillId.Count;
            int i = (int)current;
            for (int step = 0; step < n; step++)
            {
                i = (i + dir + n) % n;
                var id = (SkillId)i;
                if (id != skipA && id != skipB)
                    return id;
            }
            return current;
        }

        void SubmitCreate()
        {
            var picks = new[] { createA, createB, createC };
            var values = new[] { createAv, createBv, createCv };
            createError = CharacterCreate.Validate(createName, createStr, createDex, createInt, picks, values);
            if (createError != null)
                return;
            var snap = CharacterCreate.Build(PersistDriver.AccountKey(), createName, createAppear, createStr, createDex, createInt, picks, values);
            PersistDriver.Commit(snap);
            var world = OfflineWorld.Instance;
            if (world != null && world.Player != null)
                OutfitSwap.ApplyLook(world.Player.transform, createAppear);
            lookApplied = true;
            createError = "";
        }
    }
}
