using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **씬에 있어야 할 것이 있는가**(검수 지시 2026-09-07 3(b), 랩 ③).
    ///
    /// 동료(`Companion`)가 씬에서 **통째로 사라져 있었다**. 외형이 아니라 **기능이 없어진 것**이다 —
    /// HUD의 「동료 초대」와 결투 상대 폴백이 오프라인에서 아무 일도 안 했다. 원인은 구조적이다:
    /// **자격 패스(`EnsureMobArtQualified`)가 지웠고, 다시 세우는 패스가 없었다.**
    ///
    /// 「없는 것은 아무도 못 센다」 — 그래서 **있어야 할 것의 명단**을 코드에 적고 전수로 확인한다.
    /// 지우는 패스를 새로 쓸 때는 **세우는 패스도 같이** 있어야 하고, 그 사실을 이 게이트가 강제한다.
    /// 명단은 다른 원장(`RoleLook.Facilities`·`HuntSpots`·던전 상수)에서 끌어온다 —
    /// 손으로 두 번 적으면 두 벌이 어긋난다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>명단 — (오브젝트 이름, 없으면 무엇이 죽는가).</summary>
        static List<(string Name, string Why)> SceneRoster()
        {
            var roster = new List<(string, string)>
            {
                ("Player", "플레이어가 없으면 아무것도 못 한다"),
                (VisualSliceBuilder.CompanionObject, "HUD 「동료 초대」·결투 상대 폴백이 가리킬 상대가 없다"),
                (TameCritter.Object, "조련 대상(야생하트)이 없으면 조련·펫 기능이 화면에서 죽는다"),
                (TameBoar.Object, "두 번째 조련 대상"),
                (VisualSliceBuilder.StableBeastObject, "마구간 마당이 비면 마구간으로 안 읽힌다"),
                (Dungeon1.MobObject, "던전 1 잡몹"), (Dungeon1.BossObject, "던전 1 보스"),
                (Dungeon2.MobObject, "던전 2 잡몹"), (Dungeon2.BossObject, "던전 2 보스"),
                (Dungeon3.MobObject, "던전 3 잡몹"), (Dungeon3.BossObject, "던전 3 보스"),
                (FieldBoss.Object, "필드 보스"),
            };
            for (int i = 0; i < VisualSliceBuilder.HuntSpots.Length; i++)
                roster.Add((VisualSliceBuilder.HuntSpots[i].Name, "사냥터 몹"));
            var fac = RoleLook.Facilities;
            for (int i = 0; i < fac.Length; i++)
                roster.Add((fac[i].Object, "마을 " + fac[i].Role));
            return roster;
        }

        static void AssertSceneRosterPresent()
        {
            var roster = SceneRoster();
            if (roster.Count == 0)
                throw new InvalidOperationException("명단이 비었습니다 — 잰 것이 없습니다(0이면 실패).");
            var missing = new List<string>();
            for (int i = 0; i < roster.Count; i++)
                if (GameObject.Find(roster[i].Name) == null)
                    missing.Add(roster[i].Name + "(" + roster[i].Why + ")");
            if (missing.Count > 0)
                throw new InvalidOperationException("씬에서 사라진 것 " + missing.Count + "개: " +
                    string.Join(", ", missing) + " — **지우는 패스는 있는데 세우는 패스가 없다**는 뜻입니다. " +
                    "화면에 없는 것은 아무도 못 봅니다(동료가 그렇게 사라져 있었다).");
            Debug.Log("[Ulon] 씬 명단 — " + roster.Count + "개 전수 존재");
        }

        /// <summary>NC — 명단의 하나를 **실제로 지우면** 빨간불이어야 한다.</summary>
        static void AssertSceneRosterNegativeControl()
        {
            var pal = GameObject.Find(VisualSliceBuilder.CompanionObject);
            if (pal == null)
                throw new InvalidOperationException("씬 명단 NC 대상(동료)이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            bool red = false;
            string message = "";
            bool was = pal.activeSelf;
            try
            {
                pal.SetActive(false);            // `GameObject.Find`는 꺼진 것을 못 찾는다 = 사라진 것과 같다
                try { AssertSceneRosterPresent(); }
                catch (InvalidOperationException e) { red = true; message = e.Message; }
            }
            finally { pal.SetActive(was); }
            if (!red)
                throw new InvalidOperationException("씬 명단 네거티브 컨트롤 실패 — 동료를 지웠는데 통과했습니다.");
            Debug.Log("[Ulon] 씬 명단 네거티브 컨트롤 통과 — 동료를 지우면 FAIL: " + message);
        }
    }
}
