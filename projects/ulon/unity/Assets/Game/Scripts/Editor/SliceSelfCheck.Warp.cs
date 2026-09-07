using System;
using System.Collections.Generic;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **워프가 내려놓는 자리**를 전수로 잰다(검수 랩 B, 2026-09-07 사각지대 표 6번).
    /// 스폰 게이트는 좌표 두 개(마을·던전 방)만 봤는데, 과거 P0(`WarpBody` y=0.1 하드코딩)는
    /// 정확히 **워프 경로**에서 터졌다 — 은행·문게이트·Recall·던전 출입마다 지하 10m로 처박혔다.
    ///
    /// 판정은 좌표가 아니라 **땅과의 관계**다: 게임이 실제로 쓰는 `OfflineWorld.WarpTarget`이 준 자리에서
    /// 아래로 광선을 쏴 딛을 것과의 차이를 잰다. 계산을 계산과 비교하면(지형 공식 vs 지형 공식) 늘 통과한다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>발이 이만큼 넘게 뜨거나 묻히면 떨어지거나 박힌 것이다(스폰 게이트와 같은 값).</summary>
        const float WarpGroundErrorMax = 0.35f;

        struct WarpSpot
        {
            public string Name;
            public float X, Z;
            public bool Indoor;
            /// <summary>은행·상인처럼 **대상 옆**으로 보내는 워프면 그 대상. 게임과 같은 함수로 자리를 구한다.</summary>
            public Transform Beside;
            public float Range;
        }

        /// <summary>워프 목적지 원장 — 새 워프를 추가하면 여기에도 적어야 한다.</summary>
        static List<WarpSpot> WarpSpots()
        {
            var list = new List<WarpSpot>
            {
                new WarpSpot { Name = "던전 1 나가기", X = Dungeon1.LeaveX, Z = Dungeon1.LeaveZ },
                new WarpSpot { Name = "던전 1 들어가기", X = Dungeon1.InteriorX, Z = Dungeon1.InteriorZ, Indoor = true },
                new WarpSpot { Name = "던전 2 나가기", X = Dungeon2.LeaveX, Z = Dungeon2.LeaveZ },
                new WarpSpot { Name = "던전 2 들어가기", X = Dungeon2.InteriorX, Z = Dungeon2.InteriorZ, Indoor = true },
                new WarpSpot { Name = "던전 3 나가기", X = Dungeon3.LeaveX, Z = Dungeon3.LeaveZ },
                new WarpSpot { Name = "던전 3 들어가기", X = Dungeon3.InteriorX, Z = Dungeon3.InteriorZ, Indoor = true },
                new WarpSpot { Name = "문게이트 광장", X = TravelGate.PlazaX, Z = TravelGate.PlazaZ },
                new WarpSpot { Name = "GM 광장 워프", X = 0f, Z = 0f },
                new WarpSpot { Name = "GM 테스트 공간", X = WorldRegions.TestChamber.X, Z = WorldRegions.TestChamber.Z },
                // Recall은 플레이어가 찍은 자리로 간다 — 필드 한 점을 대표로 잰다.
                new WarpSpot { Name = "Recall(동쪽 필드)", X = 24f, Z = 6f },
            };
            var bank = UnityEngine.Object.FindFirstObjectByType<BankStation>(FindObjectsInactive.Include);
            if (bank != null)
                list.Add(new WarpSpot { Name = "은행 옆(키워드 워프)", Beside = bank.transform, Range = bank.InteractRange });
            var vendor = UnityEngine.Object.FindFirstObjectByType<VendorStation>(FindObjectsInactive.Include);
            if (vendor != null)
                list.Add(new WarpSpot { Name = "상인 옆(키워드 워프)", Beside = vendor.transform, Range = vendor.InteractRange });
            return list;
        }

        static void AssertWarpLandings()
        {
            var spots = WarpSpots();
            CheckWarpSpots(spots, "워프", LandingOf);
            Debug.Log("[Ulon] 워프 착지 통과 — 목적지 " + spots.Count + "곳 전부 딛을 것 위 " +
                      WarpGroundErrorMax + "m 안(계산이 아니라 광선 실측)");
        }

        /// <summary>게임이 그 워프에서 실제로 쓰는 자리 계산 — 대상 옆 워프는 같은 함수를 그대로 부른다.</summary>
        static Vector3 LandingOf(WarpSpot sp)
        {
            return sp.Beside != null
                ? OfflineWorld.WarpBesideTarget(sp.Beside, sp.Range)
                : OfflineWorld.WarpTarget(sp.X, sp.Z, sp.Indoor);
        }

        static void CheckWarpSpots(List<WarpSpot> spots, string label, Func<WarpSpot, Vector3> landingOf)
        {
            if (spots.Count == 0)
                throw new InvalidOperationException(label + " 목적지가 0곳입니다 — 잰 것이 없습니다(0이면 실패).");
            var bad = new List<string>();
            for (int i = 0; i < spots.Count; i++)
            {
                var s = spots[i];
                var landing = landingOf(s);
                if (!FloorUnder(landing, out float floor, out string what))
                {
                    bad.Add(s.Name + " — 발밑에 딛을 것이 없다(허공)");
                    continue;
                }
                float dy = landing.y - floor;
                bool inside = Inside(landing, out string blocker);
                Debug.Log("[Ulon] " + label + " " + s.Name + " — 착지 y " + landing.y.ToString("0.00") +
                          ", 딛는 면 " + floor.ToString("0.00") + " (" + what + "), 차 " + dy.ToString("0.00") + "m" +
                          (inside ? " · **구조물 안**(" + blocker + ")" : ""));
                // 값의 해석을 게이트가 스스로 말한다 — 「-1.85m」는 「땅에서 떨어졌다」가 아니라 「몸이 구조물 속」이었다.
                if (inside)
                    bad.Add(s.Name + " 구조물 안(" + blocker + ")");
                else if (Mathf.Abs(dy) > WarpGroundErrorMax)
                    bad.Add(s.Name + " " + dy.ToString("0.00") + "m " + (dy > 0f ? "떠 있음" : "땅에 묻힘"));
            }
            if (bad.Count > 0)
                throw new InvalidOperationException(label + " 착지가 어긋난 곳 " + bad.Count + "곳: " + string.Join(", ", bad) +
                    " — 허용 " + WarpGroundErrorMax + "m. 워프는 좌표가 아니라 그 자리 지표에서 유도해야 합니다(2026-09-06 P0).");
        }

        /// <summary>사람 몸 크기로 서 봤을 때 구조물과 겹치는가 — 겹치면 「묻힘」이 아니라 「구조물 안」이다.</summary>
        static bool Inside(Vector3 spot, out string blocker)
        {
            blocker = "";
            var hits = Physics.OverlapCapsule(spot + Vector3.up * 0.5f, spot + Vector3.up * 1.6f, 0.35f);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].GetComponentInParent<WorldBody>() != null)
                    continue;
                if (hits[i].GetComponent<TerrainCollider>() != null)
                    continue;
                blocker = hits[i].name;
                return true;
            }
            return false;
        }

        /// <summary>착지점 발밑에서 가장 가까운 딛을 면 — 위 3m부터 아래 6m까지 본다.</summary>
        static bool FloorUnder(Vector3 landing, out float floorY, out string what)
        {
            floorY = 0f;
            what = "";
            var hits = Physics.RaycastAll(landing + Vector3.up * 3f, Vector3.down, 9f);
            if (hits.Length == 0)
                return false;
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                // 플레이어·몹 몸통은 딛는 면이 아니다.
                if (hits[i].collider.GetComponentInParent<WorldBody>() != null)
                    continue;
                floorY = hits[i].point.y;
                what = hits[i].collider.name;
                return true;
            }
            return false;
        }

        /// <summary>
        /// **도약(Blink) 착지**도 구조물 안이면 안 된다(검수 랩 C). 전방 3.5m를 그냥 계산해 보내던 옛 코드는
        /// 벽을 향해 쓰면 플레이어를 벽 속에 넣었다 — 은행 워프와 원인은 달라도 화면 결과는 같다.
        /// 판정은 마을에서 **여덟 방향**으로 뛰어 보고, 그중 벽을 향한 방향이 실제로 막히는지까지 본다.
        /// </summary>
        static void AssertBlinkLanding()
        {
            var bank = UnityEngine.Object.FindFirstObjectByType<BankStation>(FindObjectsInactive.Include);
            if (bank == null)
                throw new InvalidOperationException("은행이 없어 도약 착지를 잴 기준점을 못 잡습니다.");
            // 건물에서 3m 떨어져 건물을 바라보는 자리 — 옛 코드라면 3.5m 도약이 건물 속으로 들어간다.
            var toward = new Vector3(1f, 0f, 0f);
            var stand = OfflineWorld.WarpTarget(bank.transform.position.x - 3f, bank.transform.position.z);
            int checkedCount = 0;
            var bad = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var landing = OfflineWorld.BlinkLanding(stand, dir);
                checkedCount++;
                if (Inside(landing, out string blocker))
                    bad.Add("방향 " + i + " 구조물 안(" + blocker + ")");
            }
            if (checkedCount == 0)
                throw new InvalidOperationException("도약 착지를 한 번도 재지 못했습니다(0이면 실패).");
            if (bad.Count > 0)
                throw new InvalidOperationException("도약 착지가 구조물 안인 방향 " + bad.Count + "개: " +
                    string.Join(", ", bad) + " — 막혔으면 막히기 직전까지만 가야 합니다.");

            // 건물을 정면으로 향한 도약은 **실제로 짧아져야** 한다 — 안 짧아지면 잰 것이 없다.
            var full = OfflineWorld.BlinkLanding(stand, toward);
            float moved = Vector2.Distance(new Vector2(stand.x, stand.z), new Vector2(full.x, full.z));
            Debug.Log("[Ulon] 도약 착지 — 여덟 방향 전부 구조물 밖, 건물 정면 도약은 " + moved.ToString("0.00") +
                      "m에서 멈춤(요구 3.5m)");
            if (moved > SpellCast.BlinkDistance - 0.2f)
                throw new InvalidOperationException("건물을 향한 도약이 " + moved.ToString("0.00") +
                    "m나 갔습니다 — 막힘을 재지 못하고 있습니다(게이트가 헛돌고 있다).");
        }

        /// <summary>네거티브 컨트롤 — 옛 계산(막힘 무시하고 전방 3.5m)이면 빨간불이어야 한다.</summary>
        static void AssertBlinkLandingNegativeControl()
        {
            var bank = UnityEngine.Object.FindFirstObjectByType<BankStation>(FindObjectsInactive.Include);
            if (bank == null)
                throw new InvalidOperationException("은행이 없어 도약 네거티브 컨트롤을 할 수 없습니다.");
            var stand = OfflineWorld.WarpTarget(bank.transform.position.x - 3f, bank.transform.position.z);
            var old = OfflineWorld.WarpTarget(stand.x + SpellCast.BlinkDistance, stand.z);
            bool inside = Inside(old, out string blocker);
            // 은행이 **속이 빈 건물**이 되면서(2026-09-07) 「구조물 안」만으로는 결함을 못 잡는다 —
            // 옛 계산의 착지점은 벽 **너머**의 빈 방이라 겹침이 없다. 진짜 결함은 「벽을 통과해서 간다」다.
            bool through = Physics.Linecast(stand + Vector3.up * 1.0f, old + Vector3.up * 1.0f,
                out RaycastHit wall, ~0, QueryTriggerInteraction.Ignore)
                && wall.collider.GetComponentInParent<WorldBody>() == null
                && wall.collider.GetComponent<TerrainCollider>() == null;
            if (through && string.IsNullOrEmpty(blocker))
                blocker = wall.collider.transform.root.name + " 벽을 통과";
            bool red = inside || through;
            Debug.Log("[Ulon] 도약 네거티브 컨트롤 — 옛 계산(전방 3.5m 무조건)은 " +
                      (red ? "구조물 안이거나 벽 너머(" + blocker + ")" : "**바깥**") + "에 떨어진다");
            if (!red)
                throw new InvalidOperationException("도약 네거티브 컨트롤 실패 — 옛 계산이 건물 안에 떨어지지 않습니다. " +
                    "기준점이 건물을 향하고 있지 않다는 뜻이라, 이 게이트는 막힘을 재고 있지 않습니다.");
        }

        /// <summary>
        /// 네거티브 컨트롤 둘.
        /// ① **옛 결함을 그대로 재현한다** — y=0.1 하드코딩(2026-09-06 P0)을 착지 계산으로 넣어 빨간불 확인.
        /// ② **방 바닥을 실제로 내린다** — 실내 착지가 허공에 뜨는지 오브젝트를 움직여 확인.
        /// </summary>
        static void AssertWarpLandingsNegativeControl()
        {
            var spots = WarpSpots();
            bool redOld = false;
            try { CheckWarpSpots(spots, "워프(옛 결함)", sp => new Vector3(sp.X, 0.1f, sp.Z)); }
            catch (InvalidOperationException) { redOld = true; }
            if (!redOld)
                throw new InvalidOperationException("워프 네거티브 컨트롤 실패 — y=0.1 하드코딩(옛 P0)이 통과했습니다.");

            var room = GameObject.Find(Dungeon1.InteriorObject);
            if (room == null)
                throw new InvalidOperationException("던전 1 실내가 없어 워프 네거티브 컨트롤 2를 할 수 없습니다.");
            var was = room.transform.position;
            bool redFloor = false;
            try
            {
                room.transform.position = was + Vector3.down * 5f;   // 방을 통째로 5m 내린다
                Physics.SyncTransforms();
                try { AssertWarpLandings(); }
                catch (InvalidOperationException) { redFloor = true; }
            }
            finally
            {
                room.transform.position = was;
                Physics.SyncTransforms();
            }
            if (!redFloor)
                throw new InvalidOperationException("워프 네거티브 컨트롤 실패 — 방 바닥을 5m 내렸는데 실내 착지가 통과했습니다.");

            Debug.Log("[Ulon] 워프 착지 네거티브 컨트롤 — 옛 y=0.1 계산·방 바닥 5m 하강 둘 다 빨간불, 되돌리면 통과");
        }
    }
}
