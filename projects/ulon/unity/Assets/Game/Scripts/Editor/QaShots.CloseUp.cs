using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **근접 샷의 프레이밍** — 「어디에 서서 무엇을 볼 것인가」를 고르는 쪽(랩 ㉨에서 갈라 나왔다).
    ///
    /// `QaShots.cs`가 1,500줄을 넘었고, 그 절반이 이 한 가지 일이었다: 방위 24조합을 돌며
    /// 보임·유령·렌즈·볕·거리를 재고 하나를 고른다. 찍는 쪽(`QaShots.Run`)과 고르는 쪽은
    /// 고쳐야 하는 이유가 서로 다르다 — 화면이 나쁘면 이 파일, 목록이 바뀌면 저 파일이다.
    ///
    /// **재는 자는 `QaShots.CloseUp.Sight.cs`로 나갔다**(랩 ㉾) — 여기 남은 것은 고르는 루프뿐이다.
    ///
    /// **옮기기만 했다 — 동작 변경 0**(분할 전후 EXIT=0·QA 샷 픽셀 동일).
    /// </summary>
    public static partial class QaShots
    {
        /// <summary>근접 샷 거리 여유 — 시설 반대각의 몇 배 거리에서 보나(1.0 = 딱 화면 높이에 꽉 참).</summary>
        const float CloseUpFramingSlack = 2.0f;

        /// <summary>사람 근접은 온몸이 들어오게 맞춰 둔 옛 값 그대로다(시설만 넓힌다).</summary>
        const float PersonFramingSlack = 1.35f;

        /// <summary>내려보기 각 1°당 깎는 점수 — 65°가 35°를 이기려면 표본이 6% 더 보여야 한다.</summary>
        const float SteepPitchPenalty = 0.002f;

        static Shot FacilityCloseUp(string name, string objectName) => FacilityCloseUp(name, objectName, null);

        /// <param name="beyond">
        /// 이 지점이 **피사체 너머(배경)**에 오도록 카메라를 세운다. 낚시터처럼 「무엇 옆에 있는가」가
        /// 판정의 핵심인 시설에 쓴다 — 가림만 보고 방위를 고르면 물을 등지고 찍어 물이 화면에서 사라진다(실측).
        /// </param>
        static Shot FacilityCloseUp(string name, string objectName, Vector3? beyond) =>
            FacilityCloseUp(name, objectName, beyond, false);

        /// <param name="lowAngle">
        /// **발이 바닥에 닿았는지**를 보는 샷은 내려보는 각을 낮춘다. 시설용 각(35~65°)으로 사람을 찍으면
        /// 정수리와 어깨만 나와 **발과 바닥의 접점이 화면에 없다** — 판정 대상이 안 찍히는 샷은 판정이 아니다
        /// (첫 촬영본 39_boss1이 그랬다: 왕관만 보였다).
        /// </param>
        static Shot FacilityCloseUp(string name, string objectName, Vector3? beyond, bool lowAngle) =>
            FacilityCloseUp(name, objectName, beyond, lowAngle, float.NaN);

        /// <param name="avoidYaw">
        /// **이 방위 근처는 고르지 않는다** — 같은 대상을 찍는 다른 샷이 이미 쓰고 있는 방위다
        /// (검수 지시 2026-09-09: `17`과 `41`이 2° 차이로 같은 그림이 됐다).
        /// 피하는 폭은 상수가 아니라 **후보 격자**에서 온다: 방위 후보가 45° 간격이므로 한 칸이 하한이다.
        /// </param>
        static Shot FacilityCloseUp(string name, string objectName, Vector3? beyond, bool lowAngle, float avoidYaw)
        {
            // **캐시는 한 샷보다 오래 살면 안 된다** — NC가 세계에 판을 세웠다 치웠다 하는데
            // 캐시가 남아 있으면 자가 옛 세계를 잰다(실측: 은행원 NC가 「둘러쌌는데도 통과」로 울었다).
            ClearBlockerCache();
            var go = FindSubject(objectName);
            if (go == null)
                return new Shot { Name = name, Eye = new Vector3(0f, 5f, -5f), Target = Vector3.zero };
            var rends = go.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            Bounds box = new Bounds();
            // **피사체가 사람이면 사람의 몸을 잰다.** 시설 프레이밍은 「시설에 서 있는 사람」을 빼는데,
            // 그 규칙을 사람 피사체에 그대로 적용했더니 바운드가 통째로 비어 `new Bounds()`의 중심,
            // 즉 **월드 원점**을 향해 방위를 골랐다(첫 촬영본 46_person_Vendor에 상인이 아예 없었다).
            // 무엇을 빼는가가 곧 정의다 — 장비는 빼고(GroundFit.BodyBounds) 몸만 잰다.
            // 사람 몸에서 **장비와 시설 부속을 뺀다** — 훈련사 밑에 걸린 시설 깃발(FacPart*)이 섞여
            // 몸이 2.9m로 읽혔고 카메라가 5.5m 뒤로 물러나 사람이 콩알이 됐다(첫 촬영본 47).
            if (go.GetComponent<CharacterController>() != null &&
                GroundFit.WorldBounds(go.transform, out Bounds body,
                    t => GroundFit.IsGear(go.transform, t) || GroundFit.IsFacilityPart(go.transform, t)))
            {
                box = body;
                any = true;
                Debug.Log("[Ulon] 근접 바운드 " + name + " ← 사람 몸 " + body.size.ToString("0.0"));
            }
            // **머리 장비 상단까지 프레임에 넣는다**(검수 판정 2026-09-09).
            // 몸 바운드는 장비를 뺀다 — 그 규칙이 맞는데, **왕관은 「보스임을 읽게 하는 표식」**이라
            // 프레임 밖으로 나가면 그 샷은 보스의 샷이 아니게 된다(실측 `41_boss3`: 투구를 걷었더니
            // 맨머리만 남고 왕관이 화면 위로 잘려 그냥 사람 얼굴이 됐다). 그래서 **머리 장비만** 다시 넣는다.
            // 자리는 `BossFit.HeadMetrics`가 정한 그 왕관이고, 여기서는 그 결과물의 바운드를 그대로 쓴다.
            if (any)
            {
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != VisualSliceBuilder.BossCrownObject)
                        continue;
                    bool gotCrown = false;
                    Bounds crownBox = new Bounds();
                    foreach (var cr in t.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!cr.enabled || cr is ParticleSystemRenderer)
                            continue;
                        if (!gotCrown) { crownBox = cr.bounds; gotCrown = true; }
                        else crownBox.Encapsulate(cr.bounds);
                    }
                    if (gotCrown)
                    {
                        box.Encapsulate(crownBox);
                        Debug.Log("[Ulon] 근접 바운드 " + name + " ← 머리 장비 " + t.name + " 상단 " +
                                  crownBox.max.y.ToString("0.00") + "m를 프레임에 넣는다");
                    }
                    break;
                }
            }
            bool personBox = any;                       // 사람 몸을 이미 쟀으면 아래 시설 루프는 돌지 않는다
            // **당김 하한은 피사체를 재서 정한다**(2026-09-09). 상수 1.9m는 키 1.8m 사람 기준이라,
            // 왕관까지 2.9m가 되는 보스를 당기면 **표식이 화면 위로 잘린다**(`41_boss3`가 그랬다).
            // 55° 화각에서 높이 h가 화면에 꽉 차는 거리는 h/(2·tan27.5°) ≈ h·0.96이다 — 그 아래로는 안 당긴다.
            // **여유를 더 주면 오히려 나빠진다**: 1.06배로 늘렸더니 하한이 올라가 「껍데기 안 당기기」가
            // 다른 방위·각을 고르게 됐고 왕관이 다시 프레임 밖(1.02)으로 나갔다(실측). 거리는 하나의
            // 손잡이가 아니다 — 방위를 고르는 루프와 물려 있다.
            float pullFloor = InsidePullFloor;
            for (int i = 0; i < rends.Length && !personBox; i++)
            {
                if (!rends[i].enabled || !rends[i].gameObject.activeInHierarchy)
                    continue;
                // **파티클은 프레이밍에서 뺀다** — 월드 시뮬레이션 파티클의 바운드는 수십 m로 잡혀
                // 「화덕 근접」이 마을 전경이 됐다(화덕에 불을 붙인 직후 실측).
                if (rends[i] is ParticleSystemRenderer)
                    continue;
                // **시설에 세운 사람은 시설의 크기가 아니다**(검수 승인 2026-09-07) — 스킨드 바운드가
                // 부풀어 근접 거리를 키운다. 파티클을 뺀 것과 같은 처리다.
                if (rends[i].GetComponentInParent<CharacterController>() != null)
                    continue;
                if (!any) { box = rends[i].bounds; any = true; }
                else box.Encapsulate(rends[i].bounds);
                Debug.Log("[Ulon] 근접 바운드 " + name + " ← " + rends[i].gameObject.name + " " + rends[i].bounds.size.ToString("0.0"));
            }
            if (any)
                pullFloor = Mathf.Max(InsidePullFloor, box.size.y * 0.96f);
            var target = any ? box.center : go.transform.position + Vector3.up;
            float radius = any ? Mathf.Max(box.extents.magnitude, 0.6f) : 1.5f;
            // 거리 = 바운드 **반대각** / tan(화각/2) × 여유. 1.35였을 때 대장간이 프레임에 안 들어오고
            // 굴뚝·통만 찍혔다(검수 반려 2026-09-09) — 화면 세로는 맞아도 **가로 16:9로 퍼지는 폭**과
            // 시설이 기울어 선 방향의 대각이 프레임을 넘었다. 2.0이면 시설이 통째로 들어오고
            // 이웃이 먹는 비율도 같이 준다(아래 실측). 자를 안 만들고 **굽는 쪽 상수 하나**로 듣는다.
            // **사람은 그대로 1.35다** — 2.0을 사람에게도 먹였더니 훈련사가 화면 높이의 3분의 1로
            // 줄어 「역할이 서로 다른가」를 읽을 수 없었다(실측 3.3m→4.9m). 사람 근접의 거리는
            // 이미 온몸이 들어오게 맞춰 둔 값이다 — 시설이 안 들어온다고 사람까지 물리면 개악이다.
            float slack = go.GetComponent<CharacterController>() != null ? PersonFramingSlack : CloseUpFramingSlack;
            float dist = radius / Mathf.Tan(55f * 0.5f * Mathf.Deg2Rad) * slack;
            var qv = Object.FindFirstObjectByType<Ulon.Client.QuarterViewCamera>(FindObjectsInactive.Include);
            float pitch = qv != null ? qv.Pitch : 35f;
            float baseYaw = qv != null ? qv.Yaw : 45f;
            // **어느 쪽에서 봐야 시설이 보이는가를 잰다.** 마을 한복판이라 게임 방위 그대로 잡으면
            // 앞집이 가려 판정이 불가능한 샷이 나온다(첫 촬영본 30_forge가 그랬다). 네 방위를 쏴 보고
            // **가리는 것이 가장 적은 쪽**을 고른다 — 취향이 아니라 광선으로 고르고, 고른 쪽을 로그에 남긴다.
            float bestYaw = baseYaw;
            float bestPitch = pitch;
            float bestSeen = -1f;
            float bestScore = -99f;
            float bestDist = -1f;
            float bestLit = -2f;
            bool bestEyeClear = false;                       // 지금 고른 방위가 렌즈 앞이 비었는가
            float bestGhost = 1f;                            // 지금 고른 방위에서 유령이 화면을 덮는 비율
            var blockers = new System.Collections.Generic.List<string>();
            var perBearing = new System.Collections.Generic.List<string>();
            var acceptedBearings = new System.Collections.Generic.List<string>();   // 통과한 후보와 채택 이유
            int frontRejected = 0;
            // 마을은 시설이 2~3m 간격으로 붙어 있어 게임 각도에서는 앞집 지붕이 시설을 통째로 덮는다
            // (첫 촬영본 35_fishing이 그랬다). 방위 8 × 내려보는 각 3을 다 재고 제일 잘 보이는 조합을 쓴다.
            float[] pitches = lowAngle ? new[] { 10f, 18f, 26f } : new[] { pitch, 50f, 65f };
            // **두 바퀴 돈다 — 엄격하게 한 바퀴, 안 되면 풀어서 한 바퀴**(검수 반려 2026-09-09).
            // 「사람 샷은 렌즈 규칙에서 뺀다」로 두었더니 결함이 그대로 남았다: 은행원·상인은
            // **자기 집 벽 안에** 카메라가 박힌 채 찍혀 반투명 판이 얼굴을 덮었다(로그: 렌즈 앞
            // `Banker/Visual` 0.00m). 규칙을 빼는 것과 세계를 좁히지 않는 것은 **양자택일이 아니다** —
            // 먼저 엄격한 자로 방위를 찾고, 그런 방위가 하나도 없을 때만 풀어서 다시 찾는다.
            for (int k = 0; k < 16 * pitches.Length; k++)
            {
                int kk = k % (8 * pitches.Length);
                bool strictEye = k < 8 * pitches.Length;
                if (!strictEye && bestSeen >= 0f)
                    break;                                   // 엄격한 바퀴에서 찾았으면 풀지 않는다
                float y = baseYaw + (kk % 8) * 45f;
                float pit = pitches[kk / 8];
                if (!float.IsNaN(avoidYaw) && Mathf.Abs(Mathf.DeltaAngle(y, avoidYaw)) < BearingGridStep)
                    continue;                                   // 다른 샷이 쓰는 방위 — 같은 그림을 두 장 찍지 않는다
                var eyeK = target - Quaternion.Euler(pit, y, 0f) * Vector3.forward * dist;
                // 중심선 하나만 쏘면 「앞집 옆을 스쳐 지나가」 0개로 읽힌다(첫 시도가 그랬다) —
                // 시설 표면 표본에 쏴서 **몇 %가 실제로 이 시설로 먼저 닿는지**를 잰다(차폐 게이트와 같은 방식).
                int seen = 0, total = 0;
                bool byRenderer = go.GetComponent<CharacterController>() != null;
                for (int sx = -1; sx <= 1; sx++)
                    for (int sy = -1; sy <= 1; sy++)
                        for (int sz = -1; sz <= 1; sz++)
                        {
                            var p = box.center + new Vector3(sx * box.extents.x * 0.6f, sy * box.extents.y * 0.6f, sz * box.extents.z * 0.6f);
                            total++;
                            // **사람은 콜라이더로 가려짐을 못 잰다** — 좌판·집 같은 시설은 콜라이더가
                            // 없거나 성기어서 광선이 그냥 통과했고, 「100% 보인다」로 고른 방위에서
                            // 화면엔 벽만 찍혔다(첫 촬영본 46: 상인이 아예 없었다).
                            // 사람 피사체는 **보이는 것**(렌더러 바운드)으로 가려짐을 잰다.
                            // **시설도 사람과 같은 자로 잰다**(검수 판정 2026-09-09).
                            // 예전엔 시설만 **콜라이더 광선**으로 쟀는데 이웃 좌판·집 지붕엔 콜라이더가
                            // 없어 광선이 그냥 통과했다 — `30_forge`는 「표본 100% 보임」으로 기본 방위를
                            // 고르고 화면엔 이웃 지붕만 찍혔다. **자가 있는데 고르는 쪽이 안 부른 것**이다.
                            // 사람 쪽은 이미 렌더러(=보이는 것)로 옳게 재고 있었으므로 그 자를 부른다.
                            if (BlockedByRenderer(eyeK, p, go.transform))
                                continue;
                            seen++;
                        }
                float share = total > 0 ? seen / (float)total : 0f;
                // **시설은 한복판이 뚫려야 한다**(검수 반려 2026-09-09 `34_mortar`).
                // 표본 비율만 보면 얇은 기둥은 표본 두어 개만 먹어서 「95% 보임」으로 통과하는데,
                // 화면에서는 그 기둥이 **한가운데를 세로로 가른다**. 뒤로 물러나도 그대로다 —
                // 물러나기는 프레임을 넓힐 뿐 사이에 선 것을 치우지 못한다. **옆으로 도는 것**이 답이다.
                if (!byRenderer && strictEye && BlockedByRenderer(eyeK, box.center, go.transform))
                    continue;
                // 그리고 **피사체보다 크게 보이는 앞물건**이 있는 방위도 엄격한 바퀴에서 뺀다.
                if (strictEye && ForegroundHog(eyeK, target, go.transform, radius))
                    continue;
                // **사람은 앞에서 찍는다 — 선호가 아니라 규칙이다**(검수 반려 2026-09-07).
                // 처음엔 점수에 가산점으로 얹었더니 가림 점수에 묻혀 치유사가 뒷모습으로 찍혔다.
                // 등을 보이는 각은 아예 **후보에서 뺀다** — 얼굴이 없으면 「누구인지」가 화면에 없다.
                if (byRenderer && !IgnoreFrontRuleForNc && FrontDot(go.transform, target, pit, y, dist) < PersonFrontMin)
                {
                    frontRejected++;                 // 「가려서 못 찍는다」와 「앞이 아니라 못 쓴다」를 갈라 센다
                    continue;
                }
                // **「대상이 실제로 보이는가」로 후보를 거른다**(검수 반려 2026-09-08).
                // 처음엔 「반투명이 끼는 방위를 뺀다」로 걸었는데 그건 **대리 지표**였다 —
                // 반투명이 없다 ≠ 대상이 보인다. 불투명한 지붕에 통째로 가려진 방위가 그 규칙을
                // 통과해서, 게이트는 EXIT=0인데 `50_villagers` 한 타일은 붉은 지붕만, 마법사 타일은
                // 지붕 위로 모자만 나왔다. 그래서 **머리와 몸통 두 점 모두**가 카메라에서 안 막힌
                // 방위만 남긴다 — 반투명·불투명을 가리지 않는다(막힘은 막힘이다).
                if (byRenderer)
                {
                    // **껍데기 안으로 들어가서 찍는다**(검수 판정 2026-09-08, (ㄴ)).
                    // 은행원이 은행 안에, 상인이 차양 밑에, 훈련사가 지붕 밑에 서 있는 것은 §18.19가
                    // 맞게 구현된 모습이지 결함이 아니다. **샷이 안 찍힌다고 사람을 문 밖으로 옮기는
                    // (ㄱ)안은 금지다** — 그건 계측기가 세계를 바꾸는 짓이고, 게임은 나빠지고 숫자만 좋아진다.
                    // 그래서 밖에서 막히면 카메라를 그 껍데기 **안쪽까지** 당겨 본다. 하한 1.9m는
                    // 온몸이 화면에 들어오는 거리다(55° 화각·키 1.8m 기준 1.73m가 꽉 차는 거리) —
                    // 예전에 1.4m까지 열었다가 훈련사가 얼굴만 찍힌 개악을 되풀이하지 않기 위한 바닥이다.
                    float tryDist = -1f;
                    string lastBlocker = "";            // 이 방위에서 마지막으로 막은 것 — 방위별 로그의 실체
                    // **하한은 「더 당기지 마라」이지 「그보다 가까우면 안 본다」가 아니다.**
                    // 처음엔 `d >= 하한`으로만 돌렸더니, 원래 거리가 이미 1.9m 아래인 작은 피사체는
                    // 루프가 **한 번도 안 돌아** 「전 방위가 막혔다」로 보고됐다(실측: 마구간지기 —
                    // 실제로는 아무것도 안 막고 있었다). 원래 거리는 언제나 한 번 잰다.
                    float stop = Mathf.Min(dist, pullFloor);
                    for (float d = dist; d >= stop - 0.01f; d -= 0.3f)
                    {
                        // 0.3m 격자가 하한을 건너뛰면 「1.9m에서 보이는데 못 찾는」 일이 생긴다(실측 은행원).
                        if (d - 0.3f < stop && d > stop)
                            d = stop;
                        var eyeD = target - Quaternion.Euler(pit, y, 0f) * Vector3.forward * d;
                        // **눈 앞이 비어 있어야 한다** — 피사체는 안 막혔는데 카메라가 가로등에
                        // 코를 박고 있으면 화면 절반이 기둥이다(검수 관찰 `34_mortar`, 2026-09-09).
                        // 광선으로는 안 잡힌다: 막은 것이 피사체와 눈 **사이**가 아니라 눈 **위**에 있다.
                        // **사람 샷에는 이 조항을 안 건다** — 사람은 좁은 마당·좌판 사이에 서 있어서
                        // 눈앞을 비우라고 하면 찍을 방위가 사라진다(실측: 은행원·상인 두 명이 통째로
                        // 못 찍혔다). 「자를 조이면 세계가 좁아진다」 — 조항은 시설 근접에만.
                        if ((!personBox || strictEye) && EyeCrowded(eyeD, target, go.transform))
                        {
                            lastBlocker = "(눈앞이 막힘)";
                            continue;
                        }
                        if (PersonBlocked(eyeD, box, go.transform, out string bd))
                        {
                            lastBlocker = bd;
                            if (bd != "" && !blockers.Contains(bd))
                                blockers.Add(bd);
                            continue;
                        }
                        tryDist = d;
                        break;
                    }
                    if (tryDist < 0f)
                    {
                        // **방위별로 무엇이 막았는지 남긴다**(검수 지시 2026-09-08, 마구간지기).
                        // 이 줄이 없어서 「막힘 기록 0」이 찍혔고, 나는 그것을 세계의 사실로 읽을 뻔했다 —
                        // 리스트를 만들어 놓고 채우지 않은 것은 **계측기가 결과를 만든 것**이다.
                        // 막은 이름이 비어 있으면 그것도 그대로 적는다(빈칸을 숨기면 다시 같은 오독이 난다).
                        if (perBearing.Count < 40)
                            perBearing.Add("요 " + y.ToString("0") + "°/내려 " + pit.ToString("0") + "° ← " +
                                           (lastBlocker == "" ? "(막은 이름 없음)" : lastBlocker));
                        continue;
                    }
                    // 가림이 같으면 **해를 등지지 않는 쪽**을 고른다(검수 지시 2026-09-09: 은행원 칸이
                    // 그늘로만 찍혔다). 방위를 옮기는 것은 세계를 안 바꾼다 — 사람을 문 밖으로 끌어내는
                    // (ㄱ)안과 다른 점이 그것이다. 그다음에야 **덜 당긴 방위**를 고른다.
                    float lit = SunFacing(pit, y);
                    bool tie = Mathf.Abs(share - bestSeen) <= 0.02f;
                    // **푸는 바퀴에서도 눈앞은 버리지 않는다 — 탈락 조건에서 우선순위로 내린다.**
                    // 처음엔 완화 바퀴에서 이 조항을 통째로 껐더니, 은행원처럼 사방이 막힌 사람은
                    // **우연히 벽 속을 고른** 방위로 찍혔다(반투명 판이 얼굴을 덮음). 같은 만큼 보이면
                    // 벽에 코를 박지 않은 쪽을 고른다 — 규칙을 끄는 것과 순위를 낮추는 것은 다르다.
                    bool eyeClear = !EyeCrowded(target - Quaternion.Euler(pit, y, 0f) * Vector3.forward * tryDist,
                                                target, go.transform);
                    // **유령이 화면을 얼마나 덮나**를 고르는 데 쓴다(랩 ㉨ 반려 2026-09-09).
                    // 은행원 판이 그래서 나빴다: 유령 0%인 방위(요360)가 있는데도 고르는 쪽은
                    // **그 질문을 아예 안 물어서** 유령 35%짜리를 볕 0.1 차이로 골랐다.
                    // 순서는 보임 → **유령** → 렌즈 → 볕 → 거리다. 화면 절반이 반투명한 것은
                    // 볕 반 발짝보다 나쁘다 — 그림이 「렌더링 오류」로 읽히기 때문이다.
                    float ghostShare = GhostShare(target - Quaternion.Euler(pit, y, 0f) * Vector3.forward * tryDist,
                                                  Quaternion.Euler(pit, y, 0f), target, go.transform);
                    bool ghostTie = Mathf.Abs(ghostShare - bestGhost) <= 0.10f;
                    bool win = share > bestSeen + 0.02f
                        || (tie && ghostShare < bestGhost - 0.10f)
                        || (tie && ghostTie && eyeClear && !bestEyeClear)
                        || (tie && ghostTie && eyeClear == bestEyeClear && lit > bestLit + 0.05f)
                        || (tie && ghostTie && eyeClear == bestEyeClear && Mathf.Abs(lit - bestLit) <= 0.05f && tryDist > bestDist);
                    // **통과한 후보도 남긴다** — 탈락만 적어 뒀더니 「왜 어두운 쪽이 이겼나」를 로그로
                    // 못 갈랐다(은행원: 볕 −0.97 방위가 −0.26을 이겼는데 이유가 안 보였다).
                    // 자가 고른 이유를 자기 입으로 말하게 한다.
                    if (byRenderer && acceptedBearings.Count < 40)
                        acceptedBearings.Add("요" + y.ToString("0") + "/내려" + pit.ToString("0") + " 보임" +
                                             (share * 100f).ToString("0") + "% " + tryDist.ToString("0.0") + "m 볕" +
                                             lit.ToString("0.00") + " 유령" + (ghostShare * 100f).ToString("0") + "%" +
                                             (eyeClear ? "·렌즈빔" : "") + (win ? " ←채택" : ""));
                    if (win)
                    {
                        bestSeen = share; bestYaw = y; bestPitch = pit; bestDist = tryDist; bestLit = lit;
                        bestEyeClear = eyeClear; bestGhost = ghostShare;
                    }
                    continue;
                }
                // **시설은 위에서 내려다보면 지붕만 보인다** — 가림 표본은 「막혔나」만 재고
                // 「무엇이 화면을 채우나」는 안 잰다(제 차양은 자식이라 가림으로 안 세어진다).
                // 그래서 급한 내려보기 각에 값을 매긴다: 65°가 35°를 이기려면 6% 더 보여야 한다
                // (실측: 잡화점이 65°로 넘어가 화면 절반이 제 차양의 분홍 지붕이 됐다).
                float score = share - pit * SteepPitchPenalty;
                if (score > bestScore + 0.02f) { bestScore = score; bestSeen = share; bestYaw = y; bestPitch = pit; }
            }
            // **차선으로 찍지 않는다**(검수 지시 2026-09-08). 뚫린 방위가 하나도 없으면 그 사실이
            // 곧 배치 보고다 — 기본 방위로 찍어 두되 **이름과 함께 실패로 올린다**(`PersonShotClear`).
            // 예전엔 「반투명이 끼는 차선」으로 몰래 찍었고, 그래서 못 쓰는 샷이 통과했다.
            if (go.GetComponent<CharacterController>() != null)
            {
                PersonShotClear[name] = bestSeen >= 0f;
                Debug.Log("[Ulon] 사람 샷 결론 " + name + "(" + go.name + ") — bestSeen " + bestSeen.ToString("0.00") +
                          " · clear " + (bestSeen >= 0f) + "\n  통과 후보: " + string.Join(" / ", acceptedBearings));
                if (bestSeen < 0f)
                {
                    // 진단 — **얼마나 더 들어가면 보이는가**를 같이 잰다(하한 1.9m는 판정용이고,
                    // 이 탐색은 「불가능인가, 하한이 문제인가」를 가른다).
                    float clears = -1f; float clearYaw = 0f, clearPitch = 0f;
                    for (int k = 0; k < 8 * pitches.Length && clears < 0f; k++)
                    {
                        float y2 = baseYaw + (k % 8) * 45f, pit2 = pitches[k / 8];
                        for (float d = pullFloor; d >= 0.9f; d -= 0.15f)
                        {
                            var e2 = target - Quaternion.Euler(pit2, y2, 0f) * Vector3.forward * d;
                            if (PersonBlocked(e2, box, go.transform, out _))
                                continue;
                            clears = d; clearYaw = y2; clearPitch = pit2;
                            break;
                        }
                    }
                    Debug.Log("[Ulon] 사람 샷 진단 " + name + " — 원래 거리 " + dist.ToString("0.00") +
                              "m · 정면 탈락 " + frontRejected + " · 막힘 기록 " + perBearing.Count +
                              " · 바운드 " + box.size.ToString("0.00") + " · 반지름 " + radius.ToString("0.00"));
                    Debug.Log("[Ulon] 사람 샷 " + name + " — 머리·몸통이 다 보이는 방위가 없다(24조합 전부 막힘). " +
                              "기본 방위로 찍고 실패로 보고한다. 막은 것: " + string.Join(", ", blockers) +
                              "\n  방위별(정면 규칙에 걸린 것 " + frontRejected + "개 제외): " + string.Join(" / ", perBearing) +
                              " | 하한을 낮추면 " + (clears < 0f ? "0.9m까지 내려도 안 보인다" :
                              clears.ToString("0.0") + "m·방위 " + clearYaw.ToString("0") + "°/" + clearPitch.ToString("0") + "°에서 보인다"));
                }
            }
            if (beyond.HasValue)
            {
                // 배경에 둬야 할 것의 **반대편**에 선다 — 그래야 그것이 피사체 뒤로 들어온다.
                var away = target - beyond.Value; away.y = 0f;
                if (away.sqrMagnitude > 0.0001f)
                {
                    bestYaw = Quaternion.LookRotation(-away.normalized, Vector3.up).eulerAngles.y;
                    bestPitch = 20f;                     // 낮게 봐야 수면이 화면에 들어온다
                }
            }
            // 사람은 껍데기 안까지 당겨 고른 그 거리로 찍는다(위 (ㄴ) 판정).
            if (bestDist > 0f && !beyond.HasValue)
            {
                if (bestDist < dist - 0.05f)
                    Debug.Log("[Ulon] 사람 샷 " + name + " — 밖에서는 막혀 껍데기 안까지 " +
                              dist.ToString("0.0") + "m → " + bestDist.ToString("0.0") + "m로 들어가 찍는다");
                dist = bestDist;
            }
            var rot = Quaternion.Euler(bestPitch, bestYaw, 0f);
            // **방 안 피사체는 카메라도 방 안에 세운다**(검수 판정 2026-09-07 3(a)).
            // 밖에 서면 벽·뚜껑이 규칙대로 페이드돼 화면 위쪽에 바깥 지형·하늘이 들어온다(40·41이 그랬다) —
            // 방이 뚫린 게 아니라(뚜껑은 방 span+16m를 덮는다) **샷이 방 밖에서 찍힌 것**이다.
            dist = ClampInsideRoom(target, rot, dist);
            // **사람은 가리는 것 앞으로 당겨 선다**(검수 사소 지적 2026-09-07: 반투명 벽이 인물을 덮었다).
            // 페이드는 벽을 지워 주는 것이 아니라 **반투명하게** 만든다 — 그 유령 너머로 사람을 보면
            // 색이 섞여 「무슨 색 옷인가」가 흐려진다. 막는 것이 있으면 그 앞까지 카메라를 당긴다.
            if (go.GetComponent<CharacterController>() != null)
            {
                // 당김은 **반투명해질 것**(페이드 레이어)만 피한다. 처음엔 아무 렌더러나 피하게 했더니
                // 울타리·바닥 바운드까지 걸려 1.2m까지 붙었고 얼굴만 찍혔다(개악) — 그래서
                // ① 대상을 페이드 레이어로 좁히고 ② 원래 거리의 70%까지만 당긴다.
                // **하한 70%는 지킨다.** 「뚫린 방위가 없으면 더 깊이 당기자」고 1.4m까지 열어 봤더니
                // 훈련사가 **얼굴만** 찍혔다(원장에 이미 적힌 개악을 그대로 다시 밟았다 — 2026-09-07 재확인).
                // 유령이 남는 것보다 대상이 안 찍히는 것이 나쁘다.
                // 하한은 껍데기 안으로 들어갈 때 쓰는 하한과 **같은 값**을 쓴다 — 안 그러면
                // 애써 1.9m로 정한 자리를 이 루프가 1.5m까지 다시 당겨 얼굴만 남긴다(실측).
                float floor = Mathf.Max(pullFloor, dist * 0.7f);
                int pulled = 0;
                while (dist > floor && FadeBlocked(target - rot * Vector3.forward * dist, target, go.transform))
                {
                    dist -= 0.2f;
                    pulled++;
                }
                if (pulled > 0)
                    Debug.Log("[Ulon] 사람 샷 가림 회피 " + name + " — " + (pulled * 0.2f).ToString("0.0") +
                              "m 당겨 " + dist.ToString("0.0") + "m에서 찍는다");
            }
            // **뚫린 방위가 없다고 이미 실패한 샷은 정면성으로 또 세지 않는다** — 한 결함에 게이트 하나다.
            // (기본 방위로 찍은 그림은 뒤통수일 수밖에 없어, 안 그러면 같은 원인으로 빨간불이 두 번 뜨고
            //  정작 「가려서 못 찍는다」는 진짜 사유가 정면 실패에 가려진다.)
            if (go.GetComponent<CharacterController>() != null && PersonShotClear.TryGetValue(name, out bool wasClear) && !wasClear)
                Debug.Log("[Ulon] 사람 샷 정면성 " + name + " — 가려서 못 찍은 샷이라 정면 판정에서 뺀다(가림 게이트가 잡는다)");
            else if (go.GetComponent<CharacterController>() != null)
            {
                float front = FrontDot(go.transform, target, bestPitch, bestYaw, dist);
                PersonShotFront[name] = front;
                Debug.Log("[Ulon] 사람 샷 정면성 " + name + " — " + front.ToString("0.00") +
                          "(하한 " + PersonFrontMin + ", 1=정면 -1=뒤통수)");
            }
            // 고른 자리에서 **렌즈에 가장 가까운 남의 물건**을 적는다 — 화면을 덮는 것의 실체를 숫자로.
            {
                var rl = BlockerCache();
                float nd = float.MaxValue; string nn = "(없음)";
                for (int i = 0; i < rl.Length; i++)
                {
                    var r = rl[i];
                    if (r == null || r is ParticleSystemRenderer)
                        continue;
                    var tt = r.transform;
                    if (tt == go.transform || tt.IsChildOf(go.transform) || tt.root.name == "Terrain" || tt.name == "Ground")
                        continue;
                    var eyeNow = target - rot * Vector3.forward * dist;
                    float dd = Vector3.Distance(r.bounds.ClosestPoint(eyeNow), eyeNow);
                    if (dd < nd) { nd = dd; nn = tt.root.name + "/" + r.gameObject.name; }
                }
                // **최종 자리에서 한 번 더 묻는다.** 후보 루프에서 통과한 자리와 최종 자리가 다르다
                // (뒤에서 거리를 다시 당기는 단계가 있다) — 그래서 「후보는 깨끗한데 찍힌 그림은 가로등」이
                // 나왔다(실측 2026-09-09 `34_mortar`: 후보 통과, 최종 자리에서는 자가 「막힘」).
                // 여기서는 **뒤로 물러난다** — 앞으로 당기면 더 코를 박는다.
                // **한 걸음까지만.** 2.5m를 열어 뒀더니 `33_campfire`가 2.7m 물러나 화덕이 프레임에서
                // 작아졌다(검수가 통과로 봤던 그림이 나빠졌다) — 물러나기는 렌즈를 비우는 임시방편이지
                // 그림을 만드는 수단이 아니다. 사이에 선 것은 **방위로** 피한다(위 `ForegroundHog`).
                float grew = 0f;
                while (!personBox && grew < 1.2f &&
                       EyeCrowded(target - rot * Vector3.forward * dist, target, go.transform))
                {
                    dist += 0.3f;
                    grew += 0.3f;
                }
                if (grew > 0f)
                    Debug.Log("[Ulon] 렌즈 앞 비우기 " + name + " — " + grew.ToString("0.0") + "m 물러났다");
                var eyeFinal = target - rot * Vector3.forward * dist;
                // **못 찍는 자리는 못 찍는다고 적는다**(검수 판정 2026-09-09). 은행원은 몸이 은행
                // 껍데기에 박혀 있지 않다 — 그냥 벽 앞에 서 있고, 근접 하한(1.9m)이 렌즈를 벽 속에
                // 넣는 것뿐이다. **세계는 옳고 이 각도로 못 찍는 것**이므로, 세계를 비트는 대신 사실을 남긴다.
                if (personBox && EyeCrowded(eyeFinal, target, go.transform))
                    Debug.Log("[Ulon] 못 찍는 자리 " + name + " — 카메라가 " + nn + "(" + nd.ToString("0.00") +
                              "m) 안에 선다. 근접 하한 " + InsidePullFloor.ToString("0.0") +
                              "m가 렌즈를 벽 속에 넣는다 — 세계는 옳고 이 각도로는 못 찍는다.");
                Debug.Log("[Ulon] 렌즈 앞 " + name + " — 가장 가까운 남의 물건 " + nd.ToString("0.00") + "m " + nn +
                          " · 자 판정 " + (EyeCrowded(eyeFinal, target, go.transform) ? "막힘" : "안 막힘") +
                          " · 사람샷 " + personBox);
            }
            Debug.Log("[Ulon] 시설 근접 " + name + "(" + objectName + ") — 바운드 " + (any ? box.size.ToString("0.0") : "(없음)") +
                      ", 거리 " + dist.ToString("0.0") + "m, 방위 " + bestYaw.ToString("0") + "°/내려보기 " +
                      bestPitch.ToString("0") + "°(시설이 먼저 보이는 표본 " + (bestSeen * 100f).ToString("0") + "%)");
            return new Shot { Name = name, Eye = target - rot * Vector3.forward * dist, Target = target, PlayCamera = true, Subject = go.transform };
        }

        /// <summary>
        /// **네거티브 컨트롤 전용 스위치** — 켜면 정면 규칙이 없던 때로 돌아간다(가산점도 없다).
        /// 규칙을 넣고 나면 프레이밍이 **구조적으로** 앞을 고르기 때문에, 씬을 돌려세워도 결함이 안 만들어진다.
        /// 그래서 규칙 자체를 끄고 「그때는 뒷모습이 나오는가」를 확인한다(앵커 NC와 같은 처방).
        /// </summary>
        public static bool IgnoreFrontRuleForNc;

        /// <summary>사람 샷의 프레이밍만 다시 계산해 정면성 표를 갱신한다(렌더는 하지 않는다).</summary>
        public static void RecomputePersonFront()
        {
            PersonShotFront.Clear();
            PersonShotClear.Clear();
            var people = VillagerLook.Villagers();
            for (int i = 0; i < people.Count; i++)
                FacilityCloseUp((45 + i).ToString("00") + "_person_" + VillagerLook.HostOf(people[i]),
                                people[i].name, null, true);
        }

        /// <summary>
        /// 껍데기 안으로 들어갈 때의 **최소 거리** — 온몸이 화면에 들어오는 거리다
        /// (55° 화각·키 1.8m면 1.73m에서 화면 높이를 꽉 채운다). 이 아래로는 얼굴만 찍힌다.
        /// </summary>
        const float InsidePullFloor = 1.9f;

        /// <summary>사람 샷이 앞에서 찍혔다고 인정하는 최소 정면성(코사인) — 0.2는 정면 ±78°다.</summary>
        public const float PersonFrontMin = 0.2f;

        /// <summary>
        /// 이번 실행의 사람 샷이 **뚫린 방위에서 찍혔는가** — 샷 이름 → 참/거짓.
        /// 거짓이면 그 사람은 24개 방위·내려보기 조합 어디에서도 머리·몸통이 다 보이지 않는다는 뜻이고,
        /// 그건 샷의 문제가 아니라 **배치 보고**다(검수 지시 2026-09-08).
        /// </summary>
        public static readonly System.Collections.Generic.Dictionary<string, bool> PersonShotClear =
            new System.Collections.Generic.Dictionary<string, bool>();

        /// <summary>이번 실행의 사람 샷 정면성 — 샷 이름 → 코사인. 게이트가 이 값을 판정한다.</summary>
        public static readonly System.Collections.Generic.Dictionary<string, float> PersonShotFront =
            new System.Collections.Generic.Dictionary<string, float>();

        /// <summary>
        /// 이름으로 피사체를 찾되 **액터(CharacterController)를 먼저** 고른다.
        /// KayKit 프리팹 안에 모델 이름과 같은 노드가 있어(`Rogue/Rogue`) `GameObject.Find`가
        /// **속 노드**를 집었고, 그 노드엔 CC가 없어 사람 판정이 빗나가 바운드가 통째로 비었다
        /// (첫 촬영본 53_rogue: 프레이밍이 대상 없이 잡혔다). **이름은 유일하지 않다.**
        /// </summary>
        static GameObject FindSubject(string objectName)
        {
            var actors = Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < actors.Length; i++)
                if (actors[i].gameObject.name == objectName)
                    return actors[i].gameObject;
            return GameObject.Find(objectName);
        }

        /// <summary>
        /// **둘을 한 화면에** — 동료가 플레이어와 갈리는지는 나란히 놓고 봐야 판정된다(검수 완료 기준).
        /// 두 몸 바운드를 합쳐 가운데를 보고, 플레이어 앞쪽에서 낮게 찍는다(얼굴·앞섶이 보이게).
        /// </summary>
        static Shot PairCloseUp(string name, string aName, string bName)
        {
            // **이름은 유일하지 않다** — `GameObject.Find("Rogue")`는 실체가 아니라 프리팹 속 같은 이름의
            // 노드를 집는다(근접 샷에서 이미 당했다). 여기도 같은 함정이었다: 두 거리는 「같다」고 찍히는데
            // 화면에서는 한쪽이 확연히 작았다 — **화면이 계측과 어긋나면 계측이 다른 것을 재고 있는 것**이다.
            var a = FindSubject(aName);
            var b = FindSubject(bName);
            if (a == null || b == null ||
                !GroundFit.BodyBounds(a.transform, out Bounds ba) || !GroundFit.BodyBounds(b.transform, out Bounds bb))
                return new Shot { Name = name, Eye = new Vector3(0f, 5f, -5f), Target = Vector3.zero };
            var box = ba; box.Encapsulate(bb);
            // **나란히 비교하는 샷은 두 대상을 같은 거리에 둔다**(검수 규칙 2026-09-07) — 거리가 다르면
            // 원근이 크기를 바꿔 「갈리는가」 판정이 오염된다. 합친 상자의 중심은 두 몸 중앙이 아니다
            // (덩치 큰 쪽으로 끌린다) — **두 몸 중심의 중점**을 보고, 그 둘을 잇는 선의 **수직**에서 본다.
            // 그러면 두 거리는 대칭으로 같아진다. 아래 로그가 실제 두 거리를 찍어 규칙을 증명한다.
            var target = (ba.center + bb.center) * 0.5f;
            float radius = Mathf.Max(box.extents.magnitude, 0.8f);
            float dist = radius / Mathf.Tan(55f * 0.5f * Mathf.Deg2Rad) * 1.25f;
            // 둘이 나란히 서므로 **둘을 잇는 선의 옆**에서 봐야 서로 겹치지 않는다. 그 두 방향 중
            // 플레이어의 앞쪽을 고른다 — 뒤통수 둘을 찍으면 누가 누구인지가 화면에 없다.
            var along = bb.center - ba.center; along.y = 0f;
            var side = Vector3.Cross(along.normalized, Vector3.up);
            if (Vector3.Dot(side, a.transform.forward) < 0f)
                side = -side;
            var eye = target + side * dist + Vector3.up * dist * 0.30f;
            float da = Vector3.Distance(eye, ba.center), db = Vector3.Distance(eye, bb.center);
            Debug.Log("[Ulon] 둘 근접 " + name + " — 합친 바운드 " + box.size.ToString("0.0") + ", 거리 " + dist.ToString("0.0") +
                      "m, 두 대상까지 " + aName + " " + da.ToString("0.00") + "m ↔ " + bName + " " + db.ToString("0.00") +
                      "m (차이 " + Mathf.Abs(da - db).ToString("0.00") + "m — 같아야 원근이 크기를 안 바꾼다)" +
                      " | 화면 높이 비 " + ((ba.size.y / da) / Mathf.Max(0.0001f, bb.size.y / db)).ToString("0.00") +
                      " (몸 " + ba.size.y.ToString("0.00") + "m·" + bb.size.y.ToString("0.00") + "m, 자리 " +
                      ba.center.ToString("F1") + "·" + bb.center.ToString("F1") + ")");
            return new Shot { Name = name, Eye = eye, Target = target, PlayCamera = true, Subject = a.transform };
        }

        /// <summary>
        /// 피사체가 던전 방 안이면 카메라가 방 벽을 넘지 않도록 **거리를 줄인다**.
        /// 각도는 그대로 둔다 — 판정 각(발-바닥 접점)이 바뀌면 안 되기 때문이다.
        /// </summary>
        static float ClampInsideRoom(Vector3 target, Quaternion rot, float dist)
        {
            var rooms = new[]
            {
                (new Vector2(Dungeon1.InteriorX, Dungeon1.InteriorZ), Dungeon1.RoomHalf),
                (new Vector2(Dungeon2.InteriorX, Dungeon2.InteriorZ), Dungeon2.RoomHalf),
                (new Vector2(Dungeon3.InteriorX, Dungeon3.InteriorZ), Dungeon3.RoomHalf),
            };
            var flat = new Vector2(target.x, target.z);
            for (int i = 0; i < rooms.Length; i++)
            {
                if (Vector2.Distance(flat, rooms[i].Item1) > rooms[i].Item2)
                    continue;                                   // 이 방 안의 피사체가 아니다
                float half = rooms[i].Item2 - 0.8f;             // 벽 두께·여유
                for (int k = 0; k < 40; k++)                    // 0.2m씩 당기며 방 안에 들어올 때까지
                {
                    var eye = target - rot * Vector3.forward * dist;
                    if (Vector2.Distance(new Vector2(eye.x, eye.z), rooms[i].Item1) <= half)
                        break;
                    dist -= 0.2f;
                    if (dist < 1.2f) { dist = 1.2f; break; }
                }
                Debug.Log("[Ulon] 근접 샷 방 안 제한 — 거리 " + dist.ToString("0.0") + "m로 당김(방 반경 " +
                          rooms[i].Item2.ToString("0.0") + "m)");
                break;
            }
            return dist;
        }

        /// <summary>보스 근접 — 왕관·큰 무기를 확인하는 검수용 샷(검수 요청 2026-09-06).</summary>
        static Shot BossCloseUp(string name, float bx, float bz)
        {
            float y = GroundY(bx, bz) - VisualSliceBuilder.DungeonDepth;
            var target = new Vector3(bx, y + 1.5f, bz);
            // 방 중앙 쪽에서 본다 — 보스는 벽 가까이 서 있어 바깥쪽에서 잡으면 벽 속이다.
            var toCenter = new Vector3(Dungeon3.InteriorX - bx, 0f, Dungeon3.InteriorZ - bz).normalized;
            return new Shot { Name = name, Eye = target + toCenter * 3.4f + new Vector3(0f, 1.6f, 0f), Target = target };
        }
    }
}
