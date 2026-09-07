using System;
using System.Collections.Generic;
using Ulon.Server;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **역할이 그 역할처럼 보이는가**(검수 지시 2026-09-07: 「표를 게이트로 만들어라」).
    ///
    /// 대조표(`docs/ROLE_LOOK_TABLE.md`)는 오늘의 사진이다 — 내일 새 시설이 좌판으로 들어오면
    /// 아무도 모른다. 원장 `RoleLook`이 요구를 적고, 이 게이트가 씬을 **전수로** 훑어 강제한다.
    ///
    /// 대상 수집은 이름 목록이 아니라 **역할 컴포넌트**다(`BankStation`·`VendorStation`·…).
    /// 새 시설이 생기면 저절로 대상에 들어오고, 원장에 없으면 실패한다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>한 역할 오브젝트의 화면 사실 — 게이트와 네거티브 컨트롤이 **같은 판정 함수**에 먹인다.</summary>
        public struct RoleLookFact
        {
            public string Object;
            public string Role;
            public string MeshKey;      // 주 메시(가장 큰 렌더러) 파일명
            public float Height;        // 렌더러 바운드 높이(m)
            public bool HasPart;        // 원장이 요구한 부속이 하나라도 붙어 있는가
            public bool IsPerson;       // 사람 모델(MobArt 원장)인가
        }

        /// <summary>
        /// 판정. 사유 목록을 돌려준다(빈 목록이면 통과). **한꺼번에 모아 던진다** — 하나씩 던지면
        /// 첫 사유에서 멈춰 나머지가 안 보이고, 이 목록이 곧 마을 시설 랩의 작업 목록이다.
        /// </summary>
        static List<string> RoleLookDefects(List<RoleLookFact> facts)
        {
            var reasons = new List<string>();
            var byMesh = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < facts.Count; i++)
            {
                var f = facts[i];
                if (!RoleLook.TryGet(f.Object, out RoleLook.Facility spec))
                {
                    reasons.Add(f.Object + "(" + f.Role + ")이 역할↔외형 원장에 없습니다 — " +
                        "새 역할은 「무엇처럼 보여야 하나」를 Editor/RoleLook.cs에 먼저 적어라.");
                    continue;
                }

                // (a) 시설끼리 주 메시 중복 0 — 존재가 아니라 **차이**를 잰다.
                if (!string.IsNullOrEmpty(f.MeshKey))
                {
                    if (byMesh.TryGetValue(f.MeshKey, out string had))
                        reasons.Add(spec.Role + "(" + f.Object + ")이 " + had + "과(와) 같은 메시 " + f.MeshKey +
                            "를 씁니다 — 화면에서 두 시설이 구분되지 않는다(§8.1).");
                    else
                        byMesh[f.MeshKey] = spec.Role + "(" + f.Object + ")";
                }

                // (b) 높이 하한을 플레이어 키 비율로.
                float min = VisualSliceBuilder.PlayerHeight * spec.MinHeightFrac;
                if (f.Height < min)
                    reasons.Add(spec.Role + "(" + f.Object + ") 높이가 " + f.Height.ToString("0.00") + "m입니다 — 하한 " +
                        min.ToString("0.00") + "m(플레이어 키 " + VisualSliceBuilder.PlayerHeight + "m × " +
                        spec.MinHeightFrac + "). 멀리서 그 시설로 안 읽힌다(§8.1).");

                // (c) 기능이 읽히는 부속 최소 1개.
                if (!f.HasPart)
                    reasons.Add(spec.Role + "(" + f.Object + ")에 기능이 읽히는 부속이 없습니다 — " + spec.PartWhy +
                        " (후보: " + string.Join(", ", spec.PartMeshes) + ").");

                // (d) 표시명이 사람인 역할은 사람 모델이어야 한다(§18.19).
                if (spec.MustBePerson && !f.IsPerson)
                    reasons.Add(spec.Role + "(" + f.Object + ")은 표시명이 사람인데 화면엔 사람이 없습니다 — " +
                        "보이는 메시 " + (string.IsNullOrEmpty(f.MeshKey) ? "(없음)" : f.MeshKey) + "(§18.19).");
            }
            return reasons;
        }

        static void CollectRoleLook<T>(List<RoleLookFact> facts, HashSet<GameObject> seen, string role) where T : MonoBehaviour
        {
            var all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var go = all[i].gameObject;
                if (!seen.Add(go))
                    continue;
                facts.Add(FactOf(go, role));
            }
        }

        static RoleLookFact FactOf(GameObject go, string role)
        {
            var fact = new RoleLookFact { Object = go.name, Role = role };
            var rends = go.GetComponentsInChildren<Renderer>(true);
            float biggest = 0f;
            bool any = false;
            Bounds box = new Bounds();
            var meshNames = new List<string>();
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || !rends[i].gameObject.activeInHierarchy)
                    continue;
                var mf = rends[i].GetComponent<MeshFilter>();
                Mesh m = mf != null ? mf.sharedMesh : null;
                if (m == null && rends[i] is SkinnedMeshRenderer smr)
                    m = smr.sharedMesh;
                if (m == null)
                    continue;
                if (!any) { box = rends[i].bounds; any = true; }
                else box.Encapsulate(rends[i].bounds);
                string path = UnityEditor.AssetDatabase.GetAssetPath(m);
                string file = string.IsNullOrEmpty(path) ? m.name : System.IO.Path.GetFileName(path);
                if (!meshNames.Contains(file))
                    meshNames.Add(file);
                float vol = rends[i].bounds.size.x * rends[i].bounds.size.y * rends[i].bounds.size.z;
                if (vol > biggest) { biggest = vol; fact.MeshKey = file; }
            }
            fact.Height = any ? box.size.y : 0f;
            fact.IsPerson = MobArt.ModelOf(go, out _, out _);
            if (RoleLook.TryGet(go.name, out RoleLook.Facility spec))
            {
                // 부속은 원장이 적은 후보 메시가 **붙어 있으면** 통과다. 「주 메시가 아닐 것」까지 요구했더니
                // 시설이 자기 메시 때문에 스스로 실격되는 이상한 사유가 나왔다(집터=poles, 치유소=분수) —
                // 시설끼리 겹치는 것은 이미 (a) 메시 중복이 잡는다.
                for (int i = 0; i < meshNames.Count && !fact.HasPart; i++)
                    for (int p = 0; p < spec.PartMeshes.Length; p++)
                        if (string.Equals(meshNames[i], spec.PartMeshes[p], StringComparison.Ordinal))
                        {
                            fact.HasPart = true;
                            break;
                        }
            }
            return fact;
        }

        static List<RoleLookFact> RoleLookFacts()
        {
            var facts = new List<RoleLookFact>();
            var seen = new HashSet<GameObject>();
            CollectRoleLook<BankStation>(facts, seen, "은행");
            CollectRoleLook<VendorStation>(facts, seen, "상점");
            CollectRoleLook<CraftStation>(facts, seen, "제작대");
            CollectRoleLook<TrainerStation>(facts, seen, "훈련소");
            CollectRoleLook<HealerStation>(facts, seen, "치유소");
            CollectRoleLook<HousePlotStation>(facts, seen, "집터");
            CollectRoleLook<StableMaster>(facts, seen, "마구간");
            // 낚시터만 자원 노드다 — 원장에 있는 자원 노드만 시설로 본다(광맥·나무는 자원 그대로다).
            var nodes = UnityEngine.Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < nodes.Length; i++)
            {
                var go = nodes[i].gameObject;
                if (!RoleLook.TryGet(go.name, out _) || !seen.Add(go))
                    continue;
                facts.Add(FactOf(go, "자원 시설"));
            }
            return facts;
        }

        static void AssertRoleLook()
        {
            var facts = RoleLookFacts();
            if (facts.Count == 0)
                throw new InvalidOperationException("역할 오브젝트를 한 개도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            var reasons = RoleLookDefects(facts);
            if (reasons.Count > 0)
                throw new InvalidOperationException("역할↔외형 어긋남 " + reasons.Count + "건(대상 " + facts.Count + "개):\n  " +
                    string.Join("\n  ", reasons) +
                    "\n  원장 Editor/RoleLook.cs · 표 docs/ROLE_LOOK_TABLE.md");
            Debug.Log("[Ulon] 역할↔외형 통과 — 역할 " + facts.Count + "개 전부 원장에 있고 메시 중복 0·높이 하한·부속·사람 요구 충족");
        }

        /// <summary>
        /// 네거티브 컨트롤 — **판정 함수에 합성 입력을 먹인다**.
        /// 하나는 「전부 정상」이 실제로 **초록불**인지 본다(지금 씬이 빨간불이라, 이게 없으면
        /// 「늘 빨간불인 게이트」와 구분되지 않는다). 나머지는 결함을 하나씩 넣어 빨간불을 본다.
        /// </summary>
        static void AssertRoleLookNegativeControl()
        {
            RoleLookFact Good(string obj, float h) => new RoleLookFact
            {
                Object = obj, Role = "합성", MeshKey = obj + ".fbx", Height = h, HasPart = true, IsPerson = true,
            };
            var ok = new List<RoleLookFact> { Good("Banker", 4f), Good("Forge", 2f), Good("Healer", 2f) };
            var green = RoleLookDefects(ok);
            if (green.Count != 0)
                throw new InvalidOperationException("역할↔외형 네거티브 컨트롤 실패 — 정상 입력인데 " + green.Count +
                    "건이 나왔습니다(늘 빨간불인 게이트는 증거가 아니다): " + green[0]);

            var dupe = new List<RoleLookFact> { Good("Vendor", 2f), Good("Forge", 2f) };
            var d = dupe[1]; d.MeshKey = dupe[0].MeshKey; dupe[1] = d;
            if (RoleLookDefects(dupe).Count == 0)
                throw new InvalidOperationException("역할↔외형 네거티브 컨트롤 실패 — 두 시설이 같은 메시인데 통과했습니다.");

            var low = new List<RoleLookFact> { Good("Forge", 0.4f) };
            if (RoleLookDefects(low).Count == 0)
                throw new InvalidOperationException("역할↔외형 네거티브 컨트롤 실패 — 40cm 좌판이 대장간으로 통과했습니다.");

            var noPart = new List<RoleLookFact> { Good("Forge", 2f) };
            var np = noPart[0]; np.HasPart = false; noPart[0] = np;
            if (RoleLookDefects(noPart).Count == 0)
                throw new InvalidOperationException("역할↔외형 네거티브 컨트롤 실패 — 부속 없는 시설이 통과했습니다.");

            var notPerson = new List<RoleLookFact> { Good("Healer", 2f) };
            var nps = notPerson[0]; nps.IsPerson = false; notPerson[0] = nps;
            if (RoleLookDefects(notPerson).Count == 0)
                throw new InvalidOperationException("역할↔외형 네거티브 컨트롤 실패 — 사람이어야 하는 역할이 사람 없이 통과했습니다.");

            var unknown = new List<RoleLookFact> { Good("NewShinyStation", 2f) };
            if (RoleLookDefects(unknown).Count == 0)
                throw new InvalidOperationException("역할↔외형 네거티브 컨트롤 실패 — 원장에 없는 역할이 통과했습니다.");

            Debug.Log("[Ulon] 역할↔외형 네거티브 컨트롤 통과 — 정상 입력 초록불 + 결함 5종(메시 중복·낮은 높이·부속 없음·사람 없음·원장 밖) 전부 FAIL");
        }
    }
}
