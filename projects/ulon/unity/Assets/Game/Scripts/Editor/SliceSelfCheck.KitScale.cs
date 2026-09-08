using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **문이 사람보다 큰가**(랩 B, 2026-09-09).
    ///
    /// 킷(Kenney FantasyTown)은 1m 모듈로 만들어졌고 사람은 1.8m다 — 그대로 세우면 사람이 집보다 크다.
    /// 「킷을 몇 배로 키울 것인가」는 눈대중이 아니라 **문 개구부와 사람 키의 비**에서 유도한다.
    ///
    /// 재는 것은 **이름이 아니라 성질**이다: 벽 조각에 임시 메시 콜라이더를 붙여 **가장 얇은 축**을
    /// 법선으로 삼고 0.02m 간격으로 수직 훑어, 막히지 않고 이어진 가장 긴 구간을 개구부로 본다.
    /// 문짝 조각의 이름(`wall-door`)을 몰라도 문만 0.7m 이상으로 갈라진다(창은 0.14~0.18m, 벽은 0).
    /// 한 채의 개구부는 그 채의 벽 조각 중 **가장 크게 뚫린 것**이다.
    ///
    /// 사람 키도 상수가 아니라 **씬의 플레이어를 실제로 재서** 쓴다 —
    /// 재는 자와 맞추는 자는 하나여야 한다.
    ///
    /// 양방향 NC: 아무 집 하나를 **원래 킷 크기(1/KitScale)** 로 되돌리면 빨간불이어야 하고,
    /// 되돌려 놓으면 다시 초록이어야 한다. 잰 뒤 그 집의 크기가 그대로인지도 본다 —
    /// 계측이 세계를 바꾸면 그 판의 초록도 빨강도 못 믿는다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>문 개구부는 사람 키의 이 비율 이상이어야 한다(머리를 안 부딪는 최소).</summary>
        const float DoorOpeningFrac = 0.85f;

        static void AssertDoorFitsPerson()
        {
            string reason = DoorFitReason(true);
            if (!string.IsNullOrEmpty(reason))
                throw new InvalidOperationException(reason);
        }

        /// <summary>빨간불 사유(없으면 빈 문자열) — 게이트와 NC가 **같은 자**를 쓴다.</summary>
        static string DoorFitReason(bool log)
        {
            var who = GameObject.Find("Player");
            if (who == null || !GroundFit.PersonBounds(who.transform, out Bounds body) || body.size.y < 0.01f)
                return "사람을 못 쟀습니다 — 잰 것이 없습니다(0이면 실패).";
            float need = body.size.y * DoorOpeningFrac;

            var houses = VillageHouses();
            if (houses.Count == 0)
                return "민가를 한 채도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).";

            float worst = float.MaxValue;
            string worstAt = "";
            int measured = 0;
            foreach (var house in houses)
            {
                float best = 0f;
                foreach (var piece in WallPieces(house))
                {
                    float open = OpeningHeight(piece);
                    if (open > best)
                        best = open;
                    measured++;
                }
                if (best <= 0f)
                    continue;                              // 문 없는 헛간은 사람이 안 들어간다
                if (best < worst)
                {
                    worst = best;
                    worstAt = house.position.ToString("F1");
                }
            }
            if (measured < 4 || worst == float.MaxValue)
                return "벽 조각을 " + measured + "개밖에 못 쟀습니다 — 잰 것이 없습니다(0이면 실패).";

            if (log)
                Debug.Log("[Ulon] 문/사람 — 민가 " + houses.Count + "채 · 벽 조각 " + measured +
                          "개 · 가장 낮은 개구부 " + worst.ToString("0.00") + "m · 사람 " +
                          body.size.y.ToString("0.00") + "m · 필요 " + need.ToString("0.00") + "m(" +
                          (DoorOpeningFrac * 100f).ToString("0") + "%) · 비 " +
                          (worst / body.size.y).ToString("0.00"));

            if (worst < need)
                return "문 개구부 " + worst.ToString("0.00") + "m가 사람 " + body.size.y.ToString("0.00") +
                       "m에 대해 너무 낮습니다(" + worstAt + " 부근, 필요 " + need.ToString("0.00") + "m). " +
                       "킷 배율(`VisualSliceBuilder.KitScale`)이 사람 키에서 유도되지 않았습니다.";
            return "";
        }

        /// <summary>양방향 NC — 집 하나를 원래 킷 크기로 되돌리면 빨간불, 되돌려 놓으면 다시 초록.</summary>
        static void AssertDoorFitsPersonNegativeControl()
        {
            Transform house = null;
            foreach (var h in VillageHouses())
            {
                foreach (var piece in WallPieces(h))
                    if (OpeningHeight(piece) > 0.01f) { house = h; break; }
                if (house != null)
                    break;
            }
            if (house == null)
                throw new InvalidOperationException("문 뚫린 민가가 없습니다 — 잰 것이 없습니다(0이면 실패).");

            string before = DoorFitReason(true);
            if (!string.IsNullOrEmpty(before))
                throw new InvalidOperationException("문/사람 NC 실패 — 손대기 전부터 빨간불입니다: " + before);

            Vector3 kept = house.localScale;
            bool red;
            try
            {
                house.localScale = kept / VisualSliceBuilder.KitScale;   // 킷 원래 크기 = 배율을 안 준 세계
                red = !string.IsNullOrEmpty(DoorFitReason(false));
            }
            finally { house.localScale = kept; }

            if (!red)
                throw new InvalidOperationException("문/사람 NC 실패 — 집을 원래 킷 크기로 줄였는데 통과했습니다. 빈 통과입니다.");
            if (!string.IsNullOrEmpty(DoorFitReason(false)))
                throw new InvalidOperationException("문/사람 NC 실패 — 크기를 되돌렸는데 빨간불이 남았습니다(계측이 세계를 바꿨습니다).");
            if ((house.localScale - kept).sqrMagnitude > 1e-6f)
                throw new InvalidOperationException("잰 뒤 집 크기가 달라졌습니다 — 계측이 세계를 바꿨습니다.");
            Debug.Log("[Ulon] 문/사람 양방향 NC 통과 — 배율을 뺀 집은 FAIL · 되돌리면 다시 통과(배율 " +
                      VisualSliceBuilder.KitScale.ToString("0.00") + ")");
        }

        static List<Transform> VillageHouses()
        {
            var list = new List<Transform>();
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == "House")
                    list.Add(t);
            return list;
        }

        static List<Transform> WallPieces(Transform house)
        {
            var list = new List<Transform>();
            foreach (Transform c in house)
                if (c.name.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0)
                    list.Add(c);
            return list;
        }

        /// <summary>벽 조각의 **뚫린 높이** — 임시 메시 콜라이더를 붙여 수직으로 훑고 곧 뗀다.</summary>
        static float OpeningHeight(Transform piece)
        {
            var mf = piece.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                return 0f;
            var r = mf.GetComponent<Renderer>();
            if (r == null)
                return 0f;
            if (mf.GetComponent<Collider>() != null)
                return 0f;                                  // 이미 콜라이더가 있으면 손대지 않는다
            Bounds b = r.bounds;
            const float Step = 0.02f;
            var added = mf.gameObject.AddComponent<MeshCollider>();
            try
            {
                Physics.SyncTransforms();
                bool thinX = b.size.x < b.size.z;
                Vector3 n = thinX ? Vector3.right : Vector3.forward;     // 가장 얇은 축이 벽 법선이다
                float half = (thinX ? b.size.x : b.size.z) * 0.5f + 0.3f;
                float best = 0f, run = 0f;
                for (float y = b.min.y + Step; y <= b.max.y - Step; y += Step)
                {
                    var origin = new Vector3(b.center.x, y, b.center.z) - n * half;
                    bool blocked = Physics.Raycast(origin, n, half * 2f, ~0, QueryTriggerInteraction.Ignore);
                    run = blocked ? 0f : run + Step;
                    if (run > best)
                        best = run;
                }
                return best;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(added);
                Physics.SyncTransforms();
            }
        }
    }
}
