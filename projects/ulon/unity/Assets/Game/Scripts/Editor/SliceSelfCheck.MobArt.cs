using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// §8.1·§8.2 — 몹·보스에 **맨몸 모델을 쓰지 못하게** 자격으로 막는다(검수 2026-09-06).
        /// 「살색인지」를 픽셀로 재지 않는다. 그건 증상이고, 통과시키려면 어차피 옷 있는 모델이 필요하다.
        /// 원장은 `Editor/MobArt.cs` — 라이선스·출처·몸통 판정을 한 줄에 적게 해서, 등록 없는 모델은 아예 못 들어온다.
        /// </summary>
        static void AssertCharacterArtQualified()
        {
            var names = CharacterObjectNames();
            int checkedCount = 0;
            for (int i = 0; i < names.Count; i++)
            {
                var go = GameObject.Find(names[i]);
                if (go == null)
                    continue;
                if (!MobArt.ModelOf(go, out MobArt.Model model, out string via))
                    throw new InvalidOperationException(names[i] + "의 모델을 자격 원장(MobArt)에서 못 찾았습니다 — " +
                        "등록되지 않은 캐릭터 모델은 몹으로 쓸 수 없습니다(§11 라이선스·§8.2 화면 하한).");
                checkedCount++;
                if (!model.BodyReadsClothed)
                    throw new InvalidOperationException(names[i] + "이(가) 맨몸 모델 " + model.Prefix + "을(를) 씁니다(" + via +
                        ") — " + model.Note + " 옷·갑옷으로 읽히는 모델로 교체하세요(§8.1·§8.2).");
            }
            Debug.Log("[Ulon] 사람 모델 자격 통과(몹·보스·마을 사람) — " + checkedCount + "체 전부 원장 등록·몸통이 옷/갑옷/뼈로 읽힘(맨몸 모델 금지)");
        }

        /// <summary>네거티브 컨트롤 — 맨몸 모델을 **실제로 몹 자리에 놓아** 게이트가 빨간불이 되는지 본다.</summary>
        static void AssertCharacterArtNegativeControl()
        {
            var names = CharacterObjectNames();
            GameObject host = null;
            for (int i = 0; i < names.Count && host == null; i++)
                host = GameObject.Find(names[i]);
            if (host == null)
                throw new InvalidOperationException("몹이 하나도 없습니다 — 모델 자격 네거티브 컨트롤을 할 수 없습니다.");

            const string Bare = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Characters/Barbarian.fbx";
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(Bare);
            if (prefab == null)
                throw new InvalidOperationException("맨몸 모델(" + Bare + ")이 없습니다 — 네거티브 컨트롤을 할 수 없습니다.");

            var art = new List<Transform>();
            for (int c = 0; c < host.transform.childCount; c++)
                art.Add(host.transform.GetChild(c));
            var bare = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
            bool red = false;
            try
            {
                for (int i = 0; i < art.Count; i++)
                    art[i].gameObject.SetActive(false);          // 원래 모델을 치운다
                bare.transform.SetParent(host.transform, false); // 그 자리에 맨몸 모델을 넣는다
                try { AssertCharacterArtQualified(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bare);
                for (int i = 0; i < art.Count; i++)
                    art[i].gameObject.SetActive(true);
            }
            if (!red)
                throw new InvalidOperationException("몹 모델 자격 네거티브 컨트롤 실패 — " + host.name +
                    "에 맨몸 모델을 넣었는데도 통과했습니다.");
            Debug.Log("[Ulon] 몹 모델 자격 네거티브 컨트롤 통과 — " + host.name + "에 맨몸 모델 투입 시 FAIL");
        }

        internal static List<string> CharacterObjectNames()
        {
            var names = new List<string>();
            var spots = VisualSliceBuilder.HuntSpots;
            for (int i = 0; i < spots.Length; i++)
                names.Add(spots[i].Name);
            names.Add(Dungeon1.MobObject); names.Add(Dungeon1.BossObject);
            names.Add(Dungeon2.MobObject); names.Add(Dungeon2.BossObject);
            names.Add(Dungeon3.MobObject); names.Add(Dungeon3.BossObject);
            names.Add(FieldBoss.Object);
            // **마을 사람도 사람 모델이다**(검수 랩 C, 사각지대 표 2번) — 몹만 보면 맨몸 마네킹이
            // 마을에 서 있어도 통과한다. 플레이어·동료·훈련사도 같은 자격 원장으로 잰다.
            names.Add("Player");
            names.Add("Companion");
            names.Add("Trainer");
            return names;
        }

    }
}
