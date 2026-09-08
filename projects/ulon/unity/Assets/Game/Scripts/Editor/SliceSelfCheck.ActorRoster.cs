using System;
using System.IO;
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
        const string ActorRosterBuilderSource = "Game/Scripts/Editor/VisualSliceBuilder.cs";
        const string ActorRosterSweepSource = "Game/Scripts/Editor/VisualSliceBuilder.World.cs";

        static void AssertActorRosterFree()
        {
            AssertActorRosterFreeSource();
            AssertActorRosterFreeBehaviour();
        }

        /// <summary>소스 — 빌더가 이름 명단으로 액터를 고르지 않는가.</summary>
        static void AssertActorRosterFreeSource()
        {
            string builderPath = Path.Combine(Application.dataPath, ActorRosterBuilderSource);
            string sweepPath = Path.Combine(Application.dataPath, ActorRosterSweepSource);
            if (!File.Exists(builderPath) || !File.Exists(sweepPath))
                throw new InvalidOperationException("빌더 소스를 못 찾았습니다: " + builderPath + " / " + sweepPath +
                    " — 못 읽은 것을 통과로 적지 않는다.");
            string src = File.ReadAllText(builderPath);
            int begin = src.IndexOf("public static void FixCharacterAnimation()", StringComparison.Ordinal);
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
            bool sweep = File.ReadAllText(sweepPath).Contains("FindObjectsByType<CharacterController>");
            Debug.Log("[Ulon] 배우 명단 없음(소스) — 선정 함수 " + (picks ? "사용" : "미사용") +
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

        static bool ActorRosterPicked(GameObject probe)
        {
            var picked = VisualSliceBuilder.ActorsToDress();
            for (int i = 0; i < picked.Length; i++)
                if (picked[i] == probe)
                    return true;
            return false;
        }
    }
}
