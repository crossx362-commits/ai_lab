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
            public float Thickness;     // 수평 최소 두께(m) — 얇은 판 감지
            public int Renderers;       // 보이는 렌더러 수 — 조립물인가 조각 하나인가
            public bool HasInsideRoom;  // **안에 사람이 설 자리**가 물리로 비어 있는가
            public string InsideWhy;    // 그 판정의 근거(로그용)
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

                // (e) 사람이 **들어가는** 역할은 높이로 못 잰다 — 풍차 날개가 3.1m로 통과했다(검수).
                if (spec.Enterable)
                {
                    if (f.Thickness < RoleLook.EnterableThickMin)
                        reasons.Add(spec.Role + "(" + f.Object + ") 수평 최소 두께가 " + f.Thickness.ToString("0.00") +
                            "m입니다 — 하한 " + RoleLook.EnterableThickMin + "m. 얇은 판은 건물이 아니다(§8.2).");
                    if (f.Renderers < RoleLook.EnterableRendererMin)
                        reasons.Add(spec.Role + "(" + f.Object + ")이 렌더러 " + f.Renderers +
                            "개짜리 조각 하나입니다 — 건물은 벽·지붕·문이 조립된 것이다(§8.2).");
                    if (!f.HasInsideRoom)
                        reasons.Add(spec.Role + "(" + f.Object + ") 안에 사람이 설 자리가 없습니다 — " + f.InsideWhy +
                            ". 「들어갈 수 있다」의 정의는 높이가 아니라 이것이다(§8.2).");
                }

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
            int rendererCount = 0;
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
                rendererCount++;
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
            fact.Thickness = any ? Mathf.Min(box.size.x, box.size.z) : 0f;
            fact.Renderers = rendererCount;
            fact.IsPerson = MobArt.ModelOf(go, out _, out _);
            if (RoleLook.TryGet(go.name, out RoleLook.Facility enter) && enter.Enterable)
                fact.HasInsideRoom = HasStandingRoomInside(go, box, out fact.InsideWhy);
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

        /// <summary>
        /// **안에 사람이 설 자리가 있는가**를 물리로 잰다(검수 2026-09-07: 「들어갈 수 있다」의 정의).
        /// 두 조건을 **같이** 본다 —
        ///   ① 건물 한가운데에 플레이어 캡슐이 **겹치지 않고** 선다(속이 찬 덩어리면 실패),
        ///   ② 그 자리에서 수평 네 방향 광선이 **자기 건물 콜라이더에 맞는다**(둘러싸였다).
        /// ②가 없으면 허공도 통과한다 — 풍차 날개 옆의 빈 공기가 「설 자리」로 읽혔을 것이다.
        /// </summary>
        static bool HasStandingRoomInside(GameObject go, Bounds box, out string why)
        {
            const float Radius = 0.3f;                       // 플레이어 캡슐 반지름
            float h = VisualSliceBuilder.PlayerHeight;
            var feet = new Vector3(box.center.x, box.min.y + 0.05f, box.center.z);
            var p0 = feet + Vector3.up * Radius;
            var p1 = feet + Vector3.up * (h - Radius);
            if (Physics.CheckCapsule(p0, p1, Radius, ~0, QueryTriggerInteraction.Ignore))
            {
                why = "한가운데가 막혀 있다(사람 캡슐이 겹친다)";
                return false;
            }
            var dirs = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            int walls = 0;
            var mid = feet + Vector3.up * (h * 0.5f);
            float reach = Mathf.Max(box.size.x, box.size.z) * 0.5f + 0.5f;
            for (int i = 0; i < dirs.Length; i++)
            {
                var hits = Physics.RaycastAll(mid, dirs[i], reach, ~0, QueryTriggerInteraction.Ignore);
                for (int k = 0; k < hits.Length; k++)
                    if (hits[k].collider != null && hits[k].collider.transform.IsChildOf(go.transform))
                    {
                        walls++;
                        break;
                    }
            }
            if (walls < 3)
            {
                why = "둘러싸이지 않았다(수평 네 방향 중 자기 벽에 막히는 방향이 " + walls + "개, 3개 이상 필요)";
                return false;
            }
            why = "사람 캡슐이 서고 벽 " + walls + "면에 둘러싸임";
            return true;
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
            // **통과와 미검사가 로그에서 같아 보이면 그게 빈 통과다**(검수 2026-09-07).
            // 역할마다 「무엇을 요구했고 무엇은 요구하지 않았는지」를 한 줄씩 남긴다.
            for (int i = 0; i < facts.Count; i++)
            {
                var f = facts[i];
                if (!RoleLook.TryGet(f.Object, out RoleLook.Facility spec))
                    continue;
                Debug.Log("[Ulon] 역할 요구 " + spec.Role + "(" + f.Object + ") — 높이 " + f.Height.ToString("0.00") +
                          "m/하한 " + (VisualSliceBuilder.PlayerHeight * spec.MinHeightFrac).ToString("0.00") +
                          "m · 부속 " + (f.HasPart ? "있음" : "없음") +
                          " · 사람 " + (spec.MustBePerson ? (f.IsPerson ? "있음" : "없음") : "요구 조건 없음") +
                          " · 들어가기 " + (spec.Enterable
                              ? ("두께 " + f.Thickness.ToString("0.00") + "m/렌더러 " + f.Renderers + "개/" +
                                 (f.HasInsideRoom ? "설 자리 있음" : "설 자리 없음(" + f.InsideWhy + ")"))
                              : "요구 조건 없음"));
            }
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
                Thickness = 4f, Renderers = 6, HasInsideRoom = true, InsideWhy = "합성",
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

            // 사람이 들어가는 역할의 세 축 — 축마다 하나씩 결함을 만든다.
            var thin = new List<RoleLookFact> { Good("Banker", 4f) };
            var t = thin[0]; t.Thickness = 0.5f; thin[0] = t;
            if (RoleLookDefects(thin).Count == 0)
                throw new InvalidOperationException("역할↔외형 네거티브 컨트롤 실패 — 0.5m 판때기가 은행으로 통과했습니다.");

            var onePiece = new List<RoleLookFact> { Good("Banker", 4f) };
            var o = onePiece[0]; o.Renderers = 1; onePiece[0] = o;
            if (RoleLookDefects(onePiece).Count == 0)
                throw new InvalidOperationException("역할↔외형 네거티브 컨트롤 실패 — 렌더러 1개짜리 조각이 은행으로 통과했습니다.");

            var solid = new List<RoleLookFact> { Good("Banker", 4f) };
            var so = solid[0]; so.HasInsideRoom = false; so.InsideWhy = "합성"; solid[0] = so;
            if (RoleLookDefects(solid).Count == 0)
                throw new InvalidOperationException("역할↔외형 네거티브 컨트롤 실패 — 속이 찬 덩어리가 은행으로 통과했습니다.");

            var unknown = new List<RoleLookFact> { Good("NewShinyStation", 2f) };
            if (RoleLookDefects(unknown).Count == 0)
                throw new InvalidOperationException("역할↔외형 네거티브 컨트롤 실패 — 원장에 없는 역할이 통과했습니다.");

            Debug.Log("[Ulon] 역할↔외형 네거티브 컨트롤 통과 — 정상 입력 초록불 + 결함 8종(메시 중복·낮은 높이·부속 없음·사람 없음·원장 밖·얇은 판·조각 하나·속이 참) 전부 FAIL");
        }
    }
}
