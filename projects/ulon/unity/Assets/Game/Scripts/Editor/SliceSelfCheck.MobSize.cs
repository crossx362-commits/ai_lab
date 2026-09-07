using System;
using System.Collections.Generic;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **몹이 원장 키로 서 있는가**(검수 지시 2026-09-07, 랩 ⑥ — 아마밭보다 앞).
    ///
    /// 발단: 「나란히 샷은 같은 거리」 규칙을 넣자 54에서 거리는 같은데 화면 크기가 달랐다.
    /// 화면 높이 비 0.64가 이유를 말했다 — 자객 2.37m / 도적 1.53m인데 원장은 1.70 / 1.75다.
    /// 크기는 「이놈이 얼마나 센가」를 읽는 축이라(§8.1), 자가 틀리면 그 축이 통째로 거짓이 된다.
    /// §12.2대로 **원장이 진실**이고 씬을 원장에 맞춘다(원장 수치는 이 랩에서 건드리지 않았다).
    ///
    /// **자의 정의**(검수 조건 2 — 자가 틀리면 랩 전체가 헛돈다):
    ///   몸 높이 = `GroundFit.BodyBounds(actor).size.y`
    ///   · **스킨드 메시를 구워서**(`BakeMesh`) 잰다 — 스킨드 렌더러의 `bounds`는 부푼다
    ///     (이 저장소가 세 번 밟은 함정: 훈련사 모자 2.09m·보스 실루엣·사람 크기표).
    ///   · **장비는 뺀다**(`GroundFit.IsGear`) — 안 빼면 「키」가 사실은 **치켜든 칼끝**이다
    ///     (발 높이 게이트에서 이미 겪은 것과 같은 결함).
    ///   · 컨트롤러 높이·본 거리는 쓰지 않았다. 둘 다 **화면에 안 보이는 값**이다 —
    ///     플레이어가 보는 것은 그려진 몸이고, 그것을 그대로 재는 게 구운 바운드다.
    /// 같은 자를 **맞추는 쪽(`FitHeight`)도 쓴다** — 자가 둘이면 재는 값과 맞추는 값이 갈린다
    /// (이번 어긋남의 원인이 정확히 그것이었다: 맞출 땐 안 굽고 잴 땐 구웠다).
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>원장 대비 허용 편차 — 애니메이션 포즈·모델 비례 차이를 받아 주는 폭.</summary>
        const float MobHeightTolerance = 0.10f;

        /// <summary>씬의 몹 전수 — 이름 목록이 아니라 **원장에 묶인 것**(`WorldBody.MobId`)으로 모은다.</summary>
        static List<Ulon.Server.WorldBody> CatalogMobs()
        {
            var found = new List<Ulon.Server.WorldBody>();
            var all = UnityEngine.Object.FindObjectsByType<Ulon.Server.WorldBody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (!string.IsNullOrEmpty(all[i].MobId) && MobCatalog.HeightOf(all[i].MobId) > 0.01f)
                    found.Add(all[i]);
            return found;
        }

        static void AssertMobHeightsMatchCatalog()
        {
            var mobs = CatalogMobs();
            if (mobs.Count == 0)
                throw new InvalidOperationException("원장에 묶인 몹이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var bad = new List<string>();
            var ok = new List<string>();
            for (int i = 0; i < mobs.Count; i++)
            {
                var go = mobs[i].gameObject;
                float want = MobCatalog.HeightOf(mobs[i].MobId);
                if (!GroundFit.BodyBounds(go.transform, out Bounds b))
                {
                    bad.Add(go.name + "(잴 몸이 없음)");
                    continue;
                }
                // **자가 이상하면 다른 자로도 재서 같이 말한다** — 「0.03m」만 적힌 실패는
                // 무엇을 고쳐야 하는지 안 알려준다(멧돼지가 그랬다).
                var rends = go.GetComponentsInChildren<Renderer>(true);
                var rb = new Bounds();
                bool anyR = false;
                for (int r = 0; r < rends.Length; r++)
                {
                    if (!anyR) { rb = rends[r].bounds; anyR = true; }
                    else rb.Encapsulate(rends[r].bounds);
                }
                if (b.size.y < 0.3f)
                {
                    bad.Add(go.name + "(구운 몸 " + b.size.y.ToString("0.00") + "m인데 렌더러 바운드는 " +
                            (anyR ? rb.size.y.ToString("0.00") : "-") + "m·렌더러 " + rends.Length + "개 — " +
                            "메시가 스킨드도 MeshFilter도 아닌 경로일 수 있다. 자를 먼저 고쳐라)");
                    continue;
                }
                // 자가 무너진 경우를 **판정과 구분**한다 — 「몸」이 전체의 절반도 안 되면 장비 제외
                // 규칙이 몸통까지 뺐다는 뜻이고, 그 값으로 크기를 논하면 안 된다(멧돼지 0.03m).
                if (GroundFit.WorldBounds(go.transform, out Bounds full) && b.size.y < full.size.y * 0.5f)
                {
                    var excluded = new List<string>();
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                        if (GroundFit.IsGear(go.transform, r.transform))
                            excluded.Add(r.transform.name);
                    bad.Add(go.name + "(몸 자가 무너짐: 몸 " + b.size.y.ToString("0.00") + "m vs 전체 " +
                            full.size.y.ToString("0.00") + "m, 장비로 제외된 것 " +
                            (excluded.Count == 0 ? "없음" : string.Join("+", excluded)) + ")");
                    continue;
                }
                float got = b.size.y;
                float off = (got - want) / want;
                string line = go.name + " 원장 " + want.ToString("0.00") + "m·실측 " + got.ToString("0.00") +
                              "m(" + (off >= 0f ? "+" : "") + (off * 100f).ToString("0") + "%)";
                if (Mathf.Abs(off) > MobHeightTolerance)
                    bad.Add(line);
                else
                    ok.Add(line);
            }
            // **대상 수와 통과/실패를 이름과 함께** 찍는다(검수 조건 4 — 분모 없는 로그가 7체를 숨겼다).
            Debug.Log("[Ulon] 몹 키 — 원장에 묶인 " + mobs.Count + "체 중 통과 " + ok.Count + "·벗어남 " + bad.Count +
                      " (허용 ±" + (MobHeightTolerance * 100f).ToString("0") + "%): " + string.Join(", ", ok));
            if (bad.Count > 0)
                throw new InvalidOperationException("원장 키에서 벗어난 몹 " + bad.Count + "체: " + string.Join(", ", bad) +
                    " — 크기는 「이놈이 얼마나 센가」를 읽는 축입니다(§8.1). §12.2대로 **원장이 진실**이니 " +
                    "씬을 원장에 맞추십시오(`VisualSliceBuilder.EnsureMobSizes`). 원장 수치가 틀렸다고 보이면 " +
                    "고치지 말고 근거와 함께 검수에 보고하십시오.");
        }

        /// <summary>NC — 한 마리를 **실제로 1.4배로 키우면** 빨간불이어야 한다.</summary>
        static void AssertMobHeightsNegativeControl()
        {
            var mobs = CatalogMobs();
            if (mobs.Count == 0)
                throw new InvalidOperationException("몹 키 NC 대상이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            // 「Visual」이라는 이름에 기대지 마라 — 만드는 경로에 따라 자식 이름이 모델 이름이다
            // (`SkelRogue/SkeletonRogue/Visual`). NC가 대상을 못 찾아 죽으면 게이트를 못 증명한다.
            Transform victim = mobs[0].transform.Find("Visual");
            if (victim == null)
                for (int c = 0; c < mobs[0].transform.childCount; c++)
                    if (mobs[0].transform.GetChild(c).GetComponentInChildren<Renderer>(true) != null)
                    {
                        victim = mobs[0].transform.GetChild(c);
                        break;
                    }
            if (victim == null)
                throw new InvalidOperationException("몹 키 NC 대상(" + mobs[0].name + "의 렌더러 자식)이 없습니다.");
            var keep = victim.localScale;
            bool red = false;
            string message = "";
            try
            {
                victim.localScale = keep * 1.4f;
                if (!GroundFit.BodyBounds(mobs[0].transform, out Bounds grown) ||
                    grown.size.y < MobCatalog.HeightOf(mobs[0].MobId) * 1.2f)
                    throw new InvalidOperationException("몹 키 NC가 결함을 못 만들었습니다 — 키운 뒤에도 " +
                        (grown.size.y).ToString("0.00") + "m입니다.");
                try { AssertMobHeightsMatchCatalog(); }
                catch (InvalidOperationException e) { red = true; message = e.Message; }
            }
            finally { victim.localScale = keep; }
            if (!red)
                throw new InvalidOperationException("몹 키 네거티브 컨트롤 실패 — " + mobs[0].name +
                    "을 1.4배로 키웠는데 통과했습니다.");
            Debug.Log("[Ulon] 몹 키 네거티브 컨트롤 통과 — 한 마리를 1.4배로 키우면 FAIL: " + message);
        }
    }
}
