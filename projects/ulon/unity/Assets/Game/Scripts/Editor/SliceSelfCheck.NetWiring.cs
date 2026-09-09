using System;
using FishNet.Object;
using Ulon.Shared;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **몹을 네트워크에 잇고, 씬 오브젝트가 제 번호를 갖는지 본다**(랩 ㉪ 3/N, `Run`에서 갈라 나왔다).
        /// 담는 것: `WireMob` 명단과 `SceneId` 검사.
        /// 안 담는 것: 몹의 수치·카탈로그(그건 `Run`의 규칙 검사), 자리·외형(빌더).
        ///
        /// 몹이 하나 늘면 여기 한 줄이 는다 — 그 이유로만 이 파일을 연다.
        /// </summary>
        static void RunNetWiring(Scene scene)
        {
            if (VisualSliceBuilder.ConfigureHumanoid(
                    "Assets/_ThirdParty/KayKit/Skeletons/RAW/Characters/Skeleton_Warrior.fbx",
                    true))
                throw new InvalidOperationException("이미 설정된 Humanoid FBX를 셀프체크가 다시 임포트하면 안 됩니다.");
            NetworkSliceSetup.WireMob("Skeleton");
            NetworkSliceSetup.WireMob("Bandit");
            NetworkSliceSetup.WireMob("Raider");
            NetworkSliceSetup.WireMob("Rogue");
            NetworkSliceSetup.WireMob("Knight");
            NetworkSliceSetup.WireMob("Acolyte");
            NetworkSliceSetup.WireMob("Minion");
            NetworkSliceSetup.WireMob("SkelRogue");
            NetworkSliceSetup.WireMob(Dungeon1.MobObject);
            NetworkSliceSetup.WireMob(Dungeon1.BossObject);
            NetworkSliceSetup.WireMob(Dungeon2.MobObject);
            NetworkSliceSetup.WireMob(Dungeon2.BossObject);
            NetworkSliceSetup.WireMob(Dungeon3.MobObject);
            NetworkSliceSetup.WireMob(Dungeon3.BossObject);
            NetworkSliceSetup.WireMob(FieldBoss.Object);
            NetworkSliceSetup.EnsureSceneObjectIds(scene);
            foreach (var networkObject in UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
            {
                var serialized = new SerializedObject(networkObject);
                var sceneId = serialized.FindProperty("SceneId");
                if (sceneId == null || sceneId.ulongValue == 0)
                    throw new InvalidOperationException("씬 NetworkObject SceneId가 비어 있습니다: " + networkObject.name);
            }
        }
    }
}
