using System;

namespace Ulon.Editor
{
    /// <summary>
    /// **잡몹·보스로 쓸 수 있는 캐릭터 모델의 자격 원장**(검수 2026-09-06).
    /// 화면이 「살색 덩어리」로 읽힌 원인은 모델이 맨몸이었기 때문이다. 재발 방지는 픽셀 색이 아니라
    /// **자격**으로 박는다 — 여기에 없거나 `BodyReadsClothed = false`인 모델을 몹에 쓰면 게이트가 빨간불을 낸다.
    /// §11에 따라 라이선스·출처를 같은 줄에 적는다(등록 없이는 자격도 없다).
    /// </summary>
    public static class MobArt
    {
        public struct Model
        {
            public string Prefix;             // 메시 노드 이름 접두사(씬에서 모델을 알아보는 열쇠)
            public string Fbx;
            public string License;
            public string Source;
            public bool BodyReadsClothed;     // 몸통이 옷·갑옷·뼈로 읽히는가(맨살이면 false)
            public string Note;
        }

        const string Adventurers = "https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0";
        const string Skeletons = "https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0";

        public static readonly Model[] All =
        {
            new Model { Prefix = "Knight", Fbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Characters/Knight.fbx",
                License = "CC0", Source = Adventurers, BodyReadsClothed = true, Note = "판금 갑옷 몸통 + 망토·투구" },
            new Model { Prefix = "Mage", Fbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Characters/Mage.fbx",
                License = "CC0", Source = Adventurers, BodyReadsClothed = true, Note = "로브 + 모자" },
            new Model { Prefix = "Rogue", Fbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Characters/Rogue.fbx",
                License = "CC0", Source = Adventurers, BodyReadsClothed = true, Note = "가죽 상의 + 후드" },
            new Model { Prefix = "RogueHooded", Fbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Characters/RogueHooded.fbx",
                License = "CC0", Source = Adventurers, BodyReadsClothed = true,
                Note = "후드를 쓴 로브 몸통(오너 승인 2026-09-08 도입) — 치유사가 판금 갑옷이라 경비로 읽히던 것을 푼다" },
            new Model { Prefix = "Barbarian", Fbx = "Assets/_ThirdParty/KayKit/Adventurers/RAW/Characters/Barbarian.fbx",
                License = "CC0", Source = Adventurers, BodyReadsClothed = false,
                Note = "상반신이 맨살이다 — 12_playcam에서 살색 덩어리로 읽혔다(검수 반려). 몹·동료로 쓰지 마라." },
            new Model { Prefix = "Skeleton_Warrior", Fbx = "Assets/_ThirdParty/KayKit/Skeletons/RAW/Characters/Skeleton_Warrior.fbx",
                License = "CC0", Source = Skeletons, BodyReadsClothed = true, Note = "언데드 — 몸통이 뼈라 맨살로 안 읽힌다" },
            new Model { Prefix = "Skeleton_Mage", Fbx = "Assets/_ThirdParty/KayKit/Skeletons/RAW/Characters/Skeleton_Mage.fbx",
                License = "CC0", Source = Skeletons, BodyReadsClothed = true, Note = "언데드" },
            new Model { Prefix = "Skeleton_Minion", Fbx = "Assets/_ThirdParty/KayKit/Skeletons/RAW/Characters/Skeleton_Minion.fbx",
                License = "CC0", Source = Skeletons, BodyReadsClothed = true, Note = "언데드" },
            new Model { Prefix = "Skeleton_Rogue", Fbx = "Assets/_ThirdParty/KayKit/Skeletons/RAW/Characters/Skeleton_Rogue.fbx",
                License = "CC0", Source = Skeletons, BodyReadsClothed = true, Note = "언데드" },
        };

        /// <summary>씬의 몹이 어떤 모델인지 — 메시 노드 이름으로 원장을 짚는다. 게이트와 보수 패스가 같이 쓴다.</summary>
        public static bool ModelOf(UnityEngine.GameObject go, out Model model, out string via)
        {
            model = default;
            via = "";
            // **화면에 보이는 것만** 센다 — 꺼 둔 옛 모델이 남아 있으면 판정이 그것을 따라간다(네거티브 컨트롤에서 실측).
            var all = go.GetComponentsInChildren<UnityEngine.Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var rend = all[i].GetComponent<UnityEngine.Renderer>();
                if (rend == null || !rend.enabled || !all[i].gameObject.activeInHierarchy)
                    continue;
                if (!Find(all[i].name, out Model m))
                    continue;
                if (via.Length == 0 || m.Prefix.Length > model.Prefix.Length)
                {
                    model = m;
                    via = all[i].name;
                }
            }
            return via.Length > 0;
        }

        /// <summary>메시 노드 이름(예: "Knight_Body")으로 원장을 찾는다. 없으면 Prefix가 빈 모델.</summary>
        public static bool Find(string nodeName, out Model model)
        {
            model = default;
            bool found = false;
            for (int i = 0; i < All.Length; i++)
            {
                if (!nodeName.StartsWith(All[i].Prefix + "_", StringComparison.Ordinal))
                    continue;
                // 더 긴 접두사가 이긴다("Skeleton_Rogue"가 "Rogue"보다 구체적이다).
                if (found && All[i].Prefix.Length <= model.Prefix.Length)
                    continue;
                model = All[i];
                found = true;
            }
            return found;
        }
    }
}
