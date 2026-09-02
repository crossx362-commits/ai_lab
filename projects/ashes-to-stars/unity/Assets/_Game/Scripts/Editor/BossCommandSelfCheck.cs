using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>보스 개체가 살아 있는 동안에만 지휘 조작을 열고, 선택 뒤 두 명령이 열리는지 검사한다.</summary>
    public static class BossCommandSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Boss Command Self Check")]
        public static void Run()
        {
            GameObject go = null;
            try
            {
                go = new GameObject("BossCommandSelfCheck");
                go.SetActive(false);
                var party = TestAttach.AttachWithAwake<global::W3Party>(go, p => p.GameMode = true);
                var boss = TestAttach.AttachWithAwake<BossBattle>(go);

                Require(!global::W3Party.CommandModeActive, "보스 등장 전에는 지휘 모드가 꺼져야 한다");
                boss.Begin(5, 1);
                Require(global::W3Party.CommandModeActive, "보스 개체 인식 시 지휘 모드가 켜져야 한다");
                Require(!global::W3Party.CommandMoveEnabled && !global::W3Party.CommandSkillEnabled,
                    "캐릭터 선택 전에는 이동·스킬 명령이 꺼져야 한다");

                Require(global::W3Party.TrySelectCommandMember(0), "살아 있는 첫 파티원을 선택할 수 있어야 한다");
                Require(global::W3Party.CommandMoveEnabled && global::W3Party.CommandSkillEnabled,
                    "캐릭터 선택 뒤에는 이동·스킬 명령이 켜져야 한다");
                Require(global::W3Party.TryQueueSelectedSkill(1),
                    "선택한 파티원의 1번 스킬 명령을 대기열에 넣을 수 있어야 한다");
                Require(global::W3Party.SelectedCommandSkill == 1,
                    "보스전 스킬 입력이 선택한 파티원 행동 대기열에 반영돼야 한다");

                var end = typeof(BossBattle).GetMethod("OnAllBossesDefeated", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(end != null, "보스전 종료 경계를 찾지 못했다");
                end.Invoke(boss, null);
                Require(!global::W3Party.CommandModeActive, "보스전 종료 시 지휘 모드가 꺼져야 한다");

                Debug.Log("[BossCommandSelfCheck] PASS boss=true select=false→true end=false");
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[BossCommand] " + message);
        }
    }
}
