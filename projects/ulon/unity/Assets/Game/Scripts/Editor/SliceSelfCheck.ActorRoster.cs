using System;
using System.IO;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **배우 명단이 없는가** — 애니메이터를 붙일 액터를 **이름 목록**으로 고르지 않는가(랩 ③).
    ///
    /// `FixCharacterAnimation`은 `StripAndAssign(GameObject.Find("Player"|"Companion"|...))` 14줄로
    /// 대상을 골랐다. 명단은 **모델이 하나 늘 때마다 새는 자**다 — 새 액터는 아무 오류 없이
    /// 명단 밖에 남아 게임에서 T포즈로 선다(오류가 안 나므로 아무도 못 센다). 선정을 씬 전수 스윕
    /// (`VisualSliceBuilder.ActorsToDress`)으로 바꿨고, 이 게이트가 그 성질을 지킨다.
    ///
    /// 재는 방식은 **소스 한 번, 행동 한 번**이다.
    /// ① 소스: `FixCharacterAnimation` 본문이 `ActorsToDress()`로 고르고 `GameObject.Find(`로 고른 줄이 0인가.
    /// ② 행동(양방향 NC): **명단에 없는 새 사람형**을 하나 세우면 스윕이 그것도 고르는가(NC①),
    ///    그 사람형에서 몸(`CharacterController`)을 떼면 **안 고르는가**(NC② — 자가 빨간불을 낼 수 있는가).
    ///
    /// 소스만 재면 「스윕이라고 써 놓고 안 도는」 경우를 못 보고, 행동만 재면 이름 명단으로 골라도
    /// 이미 명단에 있는 액터들 덕에 초록불이 유지된다 — 둘 다 있어야 명단 회귀가 막힌다.
    ///
    /// **재는 동안 세계를 바꾸지 않는다.** 첫 판은 NC가 빌더 본체(`FixCharacterAnimation`)를 다시 불렀는데,
    /// 그것이 모든 보정 패스 **뒤에** 액터 계층·프리팹·FBX 임포트를 다시 손대 QA 샷
    /// 「마구간 사람이 24방위 전부 가림」 빨간불을 만들었다(판별 테스트: 이 게이트만 끄면 통과,
    /// 씬 파일을 떠서 되돌려도 여전히 실패 — 바뀐 것이 씬만이 아니었다). 그래서 **부작용 없는
    /// 선정 함수만** 부른다. 계측이 세계를 바꾸면 그 판의 초록·빨강은 둘 다 못 믿는다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>어느 명단에도 없는 이름 — 명단 방식이면 **반드시** 빠지는 자리다.</summary>
        const string ActorRosterProbeName = "SelfcheckRosterProbe";
        /// <summary>
        /// 소스를 읽는 자는 **파일 이름이 아니라 함수로** 찾는다(2026-09-09).
        /// 옛 자는 `Game/Scripts/Editor/VisualSliceBuilder.cs`를 경로로 열었다 — 그 파일이 6,737줄에서
        /// partial 11개로 갈라지던 날, 함수가 옆 파일로 옮겨졌으면 자는 **「소스를 못 찾았습니다」로 죽거나
        /// 더 나쁘게는 빈 통과**가 됐다. 자가 지켜야 하는 성질은 「그 함수가 명단으로 안 고른다」이지
        /// 「그 함수가 그 파일에 있다」가 아니다. 그래서 에디터 폴더를 훑어 **정의가 있는 파일 하나**를 찾는다.
        /// 둘 이상이면 판정이 갈리므로 그것도 빨간불이다.
        /// </summary>
        const string ActorRosterEditorDir = "Game/Scripts/Editor";
        const string ActorRosterGateFile = "SliceSelfCheck.ActorRoster.cs";
        const string ActorRosterBuilderMark = "public static void FixCharacterAnimation()";
        // 스윕 표시는 **선정 함수의 정의**로 잡는다 — `FindObjectsByType<CharacterController>`는
        // 12곳에서 쓰이는 흔한 호출이라 그것으로 파일을 특정하면 「12곳에 있다」로 늘 빨간불이다.
        // 자가 지키는 성질은 「선정 함수가 전수로 훑는다」이므로, 함수를 찾고 **그 안에** 스윕이 있는지 본다.
        const string ActorRosterSweepMark = "public static GameObject[] ActorsToDress()";
        const string ActorRosterSweepCall = "FindObjectsByType<CharacterController>";

        static void AssertActorRosterFree()
        {
            AssertActorRosterFreeSource();
            AssertActorRosterFreeBehaviour();
        }

        /// <summary>
        /// **옛 이름 명단** — `FixCharacterAnimation`이 2026-09-08까지 손으로 적어 두던 14줄.
        /// 지우지 않고 남긴다: 스윕이 **누구를 새로 덮었는지**를 이름으로 말하려면 옛 경계가 필요하다.
        /// 「고르는 대상이 늘었다」는 「그들이 이제 움직인다」가 아니다(검수 조건 2026-09-08) —
        /// 움직이는지는 `AssertActorsAnimated`가 전수로 따로 잰다.
        /// </summary>
        static readonly string[] ActorRosterOldNames =
        {
            "Player", "Companion", "Skeleton", "Bandit", "Raider", "Rogue", "Knight",
            "Acolyte", "Minion", "SkelRogue",
            Dungeon1.BossObject, Dungeon2.BossObject, Dungeon3.BossObject, FieldBoss.Object,
        };

        /// <summary>
        /// 소스 — 빌더가 이름 명단으로 액터를 고르지 않는가.
        ///
        /// **이 검사는 보조다.** 문자열 모양(`StripAndAssign(GameObject.Find(`)으로 재므로
        /// 명단이 다른 모양으로 부활하면(`var go = GameObject.Find("Healer"); StripAndAssign(go);`)
        /// 그대로 지나간다 — **대상을 모양으로 재는 자**의 한계다(검수 지적 2026-09-08).
        /// **판정은 행동 NC①이 한다**(명단 밖 새 사람형을 실제로 고르는가). 이 검사는 「스윕이라고
        /// 써 놓고 안 도는」 반대편 구멍을 막는 겹띠일 뿐이니, 문자열이 초록이라고 안심하지 마라.
        /// </summary>
        static void AssertActorRosterFreeSource()
        {
            string builderPath = SoleSourceContaining(ActorRosterBuilderMark);
            string sweepPath = SoleSourceContaining(ActorRosterSweepMark);
            string src = File.ReadAllText(builderPath);
            int begin = src.IndexOf(ActorRosterBuilderMark, StringComparison.Ordinal);
            if (begin < 0)
                throw new InvalidOperationException("FixCharacterAnimation을 소스에서 못 찾았습니다 — 게이트가 빈 통과입니다.");
            int end = src.IndexOf("[MenuItem(", begin, StringComparison.Ordinal);
            string body = end > begin ? src.Substring(begin, end - begin) : src.Substring(begin);
            int roster = 0;
            for (int i = body.IndexOf("StripAndAssign(GameObject.Find(", StringComparison.Ordinal); i >= 0;
                 i = body.IndexOf("StripAndAssign(GameObject.Find(", i + 1, StringComparison.Ordinal))
                roster++;
            bool picks = body.Contains("ActorsToDress()");
            // 스윕은 선정 함수 **한 곳**에만 있어야 한다 — 자가 둘이면 또 갈린다.
            string sweepSrc = File.ReadAllText(sweepPath);
            int sBegin = sweepSrc.IndexOf(ActorRosterSweepMark, StringComparison.Ordinal);
            int sEnd = sweepSrc.IndexOf("\n        public ", sBegin + 1, StringComparison.Ordinal);
            string sweepBody = sEnd > sBegin ? sweepSrc.Substring(sBegin, sEnd - sBegin) : sweepSrc.Substring(sBegin);
            bool sweep = sweepBody.Contains(ActorRosterSweepCall);
            Debug.Log("[Ulon] 배우 명단 없음(소스) — " + Path.GetFileName(builderPath) + "·" +
                      Path.GetFileName(sweepPath) + " · 선정 함수 " + (picks ? "사용" : "미사용") +
                      ", 전수 스윕 " + (sweep ? "있음" : "없음") + ", 이름 명단 " + roster + "줄");
            if (!picks || !sweep || roster > 0)
                throw new InvalidOperationException("FixCharacterAnimation이 이름 명단으로 액터를 고릅니다(명단 " + roster +
                    "줄, 선정 함수 " + (picks ? "사용" : "미사용") + ", 스윕 " + (sweep ? "있음" : "없음") +
                    ") — 명단은 모델이 늘 때마다 새는 자다. `ActorsToDress()` 전수 스윕으로 고를 것.");
        }

        /// <summary>
        /// 행동 — 명단에 없는 새 액터를 세워 **선정 함수**에 물어본다(NC①), 몸을 떼면 안 골라야 한다(NC②).
        /// 씬을 저장하지 않고 탐침도 그 자리에서 지운다 — 재는 자가 세계에 흔적을 남기면 안 된다.
        /// </summary>
        static void AssertActorRosterFreeBehaviour()
        {
            int before = VisualSliceBuilder.ActorsToDress().Length;
            if (before == 0)
                throw new InvalidOperationException("액터를 한 명도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");

            var probe = new GameObject(ActorRosterProbeName);
            try
            {
                var cc = probe.AddComponent<CharacterController>();
                cc.height = 1.75f;
                cc.radius = 0.3f;
                cc.center = new Vector3(0f, 0.875f, 0f);

                if (!ActorRosterPicked(probe))
                    throw new InvalidOperationException("배우 명단 NC① 실패 — 명단에 없는 새 액터 " + ActorRosterProbeName +
                        "를 안 골랐습니다. 대상 선정이 아직 이름 명단입니다.");
                Debug.Log("[Ulon] 배우 명단 NC① 통과 — 명단에 없는 새 액터 " + ActorRosterProbeName +
                          "도 스윕이 골랐다(" + before + "명 → " + VisualSliceBuilder.ActorsToDress().Length + "명)");

                UnityEngine.Object.DestroyImmediate(cc);       // 몸을 뗀다 = 배우가 아닌 물건과 같은 상태
                if (ActorRosterPicked(probe))
                    throw new InvalidOperationException("배우 명단 NC② 실패 — 몸(CharacterController)을 뗀 " +
                        ActorRosterProbeName + "를 여전히 골랐습니다. 자가 아무나 고르면 빈 통과입니다.");
                Debug.Log("[Ulon] 배우 명단 NC② 통과 — 몸을 떼자 " + ActorRosterProbeName + "를 안 골랐다");
                LogActorsOutsideOldRoster();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
            if (GameObject.Find(ActorRosterProbeName) != null)
                throw new InvalidOperationException("탐침이 씬에 남았습니다 — 계측이 세계를 바꿨습니다.");
            if (VisualSliceBuilder.ActorsToDress().Length != before)
                throw new InvalidOperationException("잰 뒤 배우 수가 달라졌습니다 — 계측이 세계를 바꿨습니다.");
        }

        /// <summary>옛 명단 밖이던 배우를 **이름으로** 찍는다 — 개수는 남의 것으로도 채워진다.</summary>
        static void LogActorsOutsideOldRoster()
        {
            var picked = VisualSliceBuilder.ActorsToDress();
            var outside = new System.Collections.Generic.List<string>();
            for (int i = 0; i < picked.Length; i++)
            {
                if (picked[i] == null || picked[i].name == ActorRosterProbeName)
                    continue;
                bool known = false;
                for (int k = 0; k < ActorRosterOldNames.Length; k++)
                    if (string.Equals(picked[i].name, ActorRosterOldNames[k], StringComparison.Ordinal))
                        known = true;
                if (known)
                    continue;
                var anim = picked[i].GetComponentInChildren<Animator>(true);
                outside.Add(picked[i].name + (anim != null && anim.runtimeAnimatorController != null
                    ? "(컨트롤러 " + anim.runtimeAnimatorController.name + ")"
                    : "(컨트롤러 없음 — T포즈)"));
            }
            Debug.Log("[Ulon] 옛 이름 명단 밖이던 배우 " + outside.Count + "명 — " + string.Join(", ", outside) +
                      " · 「고른다」와 「움직인다」는 다른 질문이라 움직임은 AssertActorsAnimated가 전수로 잰다");
        }

        static bool ActorRosterPicked(GameObject probe)
        {
            var picked = VisualSliceBuilder.ActorsToDress();
            for (int i = 0; i < picked.Length; i++)
                if (picked[i] == probe)
                    return true;
            return false;
        }

        /// <summary>
        /// 에디터 소스 폴더에서 그 표시가 **딱 한 파일**에 있는지 찾아 경로를 준다.
        /// 0개면 자가 빈 통과가 되고, 2개 이상이면 어느 쪽을 재느냐로 판정이 갈린다 — 둘 다 빨간불이다.
        /// (파일이 갈라져도 자가 따라가게 하는 값싼 방법이자, **못 읽은 것을 통과로 적지 않는** 장치다.)
        /// </summary>
        static string SoleSourceContaining(string mark)
        {
            string dir = Path.Combine(Application.dataPath, ActorRosterEditorDir);
            if (!Directory.Exists(dir))
                throw new InvalidOperationException("에디터 소스 폴더를 못 찾았습니다: " + dir + " — 못 읽은 것을 통과로 적지 않는다.");
            var hits = new System.Collections.Generic.List<string>();
            foreach (string f in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                // **자기 자신은 빼고 센다** — 표시 문자열이 이 파일에 리터럴로 적혀 있으므로,
                // 안 빼면 「두 곳에 있다」로 늘 빨간불이 난다(자가 자기 그림자를 밟는 자리다).
                if (Path.GetFileName(f) == ActorRosterGateFile)
                    continue;
                if (File.ReadAllText(f).Contains(mark))
                    hits.Add(f);
            }
            if (hits.Count == 0)
                throw new InvalidOperationException("소스에서 `" + mark + "`를 못 찾았습니다 — 게이트가 빈 통과입니다.");
            if (hits.Count > 1)
            {
                for (int i = 0; i < hits.Count; i++)
                    hits[i] = Path.GetFileName(hits[i]);
                hits.Sort(StringComparer.Ordinal);
                throw new InvalidOperationException("`" + mark + "`가 " + hits.Count + "곳에 있습니다(" +
                    string.Join(", ", hits) + ") — 자가 어느 쪽을 재느냐로 판정이 갈립니다. 한 곳에 두십시오.");
            }
            return hits[0];
        }

    }
}
