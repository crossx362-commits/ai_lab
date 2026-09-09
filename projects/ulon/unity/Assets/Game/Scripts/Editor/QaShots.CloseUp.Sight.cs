using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **근접 샷의 자** — 「가리는가·보이는가·프레임 안인가」를 재는 쪽(랩 ㉾에서 갈라 나왔다).
    ///
    /// 고르는 쪽(`QaShots.CloseUp.cs`)은 방위 24조합을 돌며 하나를 고르고, 이 파일은 그 루프가
    /// 묻는 질문에 답한다 — 가림·유령·렌즈 앞·앞물건·정면성·볕, 그리고 게이트가 직접 부르는
    /// 두 자(왕관이 프레임 안인가 · 두 보스 샷이 같은 그림인가). 고쳐야 하는 이유가 서로 다르다:
    /// **그림이 나쁘면 고르는 쪽, 자가 화면과 어긋나면 이 파일**이다.
    ///
    /// **옮기기만 했다 — 동작 변경 0**(분할 전후 EXIT=0 두 판·QA 샷 보이는 차이 0장).
    /// </summary>
    public static partial class QaShots
    {
        /// <summary>눈과 피사체 사이에 **페이드될 것**(DungeonBlocker 레이어)이 있는가.</summary>
        static bool FadeBlocked(Vector3 eye, Vector3 point, Transform subject)
        {
            int layer = LayerMask.NameToLayer(Ulon.Client.DungeonSightFade.BlockerLayer);
            if (layer < 0)
                return false;
            var seg = point - eye;
            float len = seg.magnitude;
            if (len < 0.001f)
                return false;
            var ray = new Ray(eye, seg / len);
            var rends = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || rends[i].gameObject.layer != layer)
                    continue;
                var t = rends[i].transform;
                if (t == subject || t.IsChildOf(subject))
                    continue;
                if (rends[i].bounds.IntersectRay(ray, out float d) && d < len - 0.05f)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// **유령이 화면을 얼마나 덮나**(랩 ㉨ 반려, 2026-09-09) — 눈에서 화면 격자로 광선을 쏘아
        /// **페이드될 것**(반투명이 될 벽·지붕)에 맞는 칸의 비율을 돌려준다.
        ///
        /// `FadeBlocked`는 **눈과 사람을 잇는 선 하나**만 본다. 은행원 판이 그래서 통과했다:
        /// 사람 앞은 비었는데 **화면 오른쪽 절반이 통째로 유령 건물**이었다 — 렌더링 오류처럼 읽힌다.
        /// 「막았나」와 「화면을 덮나」는 다른 질문이다.
        ///
        /// **이 자가 못 보는 것**: 바운드로 재므로 속이 빈 건물도 통째로 덮은 것으로 센다(과대 계상),
        /// 거리를 안 보므로 40m 밖 유령도 한 칸으로 센다, 그리고 **얼마나 진하게 비치는지**는 모른다.
        /// </summary>
        static float GhostShare(Vector3 eye, Quaternion rot, Vector3 look, Transform subject)
        {
            // **화면을 칠하는 그 규칙을 그대로 부른다** — 첫 판은 「블로커 레이어에 맞는 광선」을 셌더니
            // 다섯 자리가 다 82~99%로 나왔다(멀쩡한 46까지). 유령이 되는 것은 레이어가 아니라
            // **`DungeonSightFade.Hide`가 고른 것**이다. 자가 화면과 다른 규칙을 읽으면 그 숫자는 세계가 아니다.
            var ghosts = new System.Collections.Generic.List<Renderer>();
            int layer = LayerMask.NameToLayer(Ulon.Client.DungeonSightFade.BlockerLayer);
            if (layer < 0)
                return 0f;
            var dir = look - eye;
            float len = dir.magnitude;
            if (len < 0.01f)
                return 0f;
            var found = Physics.SphereCastAll(eye, Ulon.Client.DungeonSightFade.DefaultRadius, dir / len, len,
                                              1 << layer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < found.Length; i++)
            {
                var rend = found[i].collider != null ? found[i].collider.GetComponent<Renderer>() : null;
                if (rend == null || !rend.enabled)
                    continue;
                if (rend.bounds.max.y < look.y - 0.2f)
                    continue;                                  // 발밑 바닥은 화면을 안 가린다(Hide와 같은 조항)
                if (subject != null && rend.transform.IsChildOf(subject))
                    continue;
                if (!Ulon.Client.DungeonSightFade.IgnoreBehindRuleForNc &&
                    Ulon.Client.DungeonSightFade.BehindTarget(rend, eye, look))
                    continue;                                  // 대상 뒤는 가림이 아니다(찍는 쪽과 **같은 함수**)
                ghosts.Add(rend);
            }
            if (ghosts.Count == 0)
                return 0f;
            const int cols = 16, rows = 9;
            float tanY = Mathf.Tan(55f * 0.5f * Mathf.Deg2Rad), tanX = tanY * (W / (float)H);
            var fwd = rot * Vector3.forward;
            var right = rot * Vector3.right;
            var up = rot * Vector3.up;
            int hit = 0;
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    float sx = (c + 0.5f) / cols * 2f - 1f, sy = (r + 0.5f) / rows * 2f - 1f;
                    var ray = new Ray(eye, (fwd + right * (sx * tanX) + up * (sy * tanY)).normalized);
                    for (int i = 0; i < ghosts.Count; i++)
                        if (ghosts[i].bounds.IntersectRay(ray))
                        {
                            hit++;
                            break;
                        }
                }
            return hit / (float)(cols * rows);
        }

        /// <summary>카메라가 사람의 앞쪽에 있는가 — 1이면 정면, -1이면 뒤통수.</summary>
        static float FrontDot(Transform person, Vector3 target, float pit, float yaw, float dist)
        {
            var toEye = -(Quaternion.Euler(pit, yaw, 0f) * Vector3.forward * dist);
            toEye.y = 0f;
            if (toEye.sqrMagnitude < 0.0001f)
                return 0f;
            return Vector3.Dot(toEye.normalized, person.forward);
        }

        /// <summary>
        /// 눈에서 표본점까지 **보이는 것**이 가로막는가 — 콜라이더가 아니라 렌더러 바운드로 잰다.
        /// 피사체 자신과 지형은 막는 것으로 세지 않는다(지형은 발밑이라 늘 걸린다).
        /// </summary>

        /// <summary>
        /// 가림 판정이 훑는 렌더러 목록 — **한 판에 한 번만** 모은다.
        /// 시설 근접까지 렌더러로 재게 되면서 호출이 표본 27 × 방위 24로 늘었다.
        /// 매번 `FindObjectsByType`를 돌면 잰 값은 같은데 시간만 든다.
        /// 수명은 **한 샷**이다 — `FacilityCloseUp` 첫 줄에서 버린다(NC가 판을 세웠다 치웠다 하므로).
        /// </summary>
        static Renderer[] blockerCache;

        static Renderer[] BlockerCache()
        {
            if (blockerCache == null)
                blockerCache = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            return blockerCache;
        }

        static void ClearBlockerCache() => blockerCache = null;

        /// <summary>
        /// 카메라가 놓일 자리에 **다른 물건이 걸쳐 있는가**. 렌즈 앞 한 뼘(0.35m)까지 본다 —
        /// 그보다 가까운 물건은 초점 밖 덩어리로 화면을 덮는다.
        /// </summary>
        const float EyeClearance = 0.35f;

        /// <summary>렌즈 앞 이 거리 안에 있는 물건은 초점 밖 덩어리로 화면을 덮는다.</summary>
        const float NearClutter = 2.2f;

        static bool EyeCrowded(Vector3 eye, Transform subject) => EyeCrowded(eye, eye, subject);

        /// <summary>
        /// 카메라 자리와 **렌즈 앞 한 걸음**이 비어 있는가. 눈 위에 걸친 것만 보면 부족했다 —
        /// `34_mortar`를 덮은 가로등은 눈에서 1m 앞·옆에 있었다(실측 2026-09-09).
        /// 그래서 「눈에서 NearClutter 안 + 보는 방향 쪽」에 있는 남의 물건을 본다.
        /// </summary>
        static bool EyeCrowded(Vector3 eye, Vector3 lookAt, Transform subject)
        {
            Vector3 dir = lookAt - eye;
            bool haveDir = dir.sqrMagnitude > 0.0001f;
            if (haveDir)
                dir = dir.normalized;
            var rends = BlockerCache();
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null || r is ParticleSystemRenderer)
                    continue;
                var t = r.transform;
                if (subject != null && (t == subject || t.IsChildOf(subject)))
                    continue;
                if (t.root.name == "Terrain" || t.name == "Ground")
                    continue;
                var b = r.bounds;
                b.Expand(EyeClearance * 2f);
                if (b.Contains(eye))
                    return true;
                if (!haveDir)
                    continue;
                Vector3 near = b.ClosestPoint(eye);
                Vector3 to = near - eye;
                float d = to.magnitude;
                if (d > NearClutter || d < 0.001f)
                    continue;
                if (Vector3.Dot(to / d, dir) > -0.15f && Vector3.Distance(eye, lookAt) > d + 0.5f)
                    return true;                    // 피사체보다 훨씬 앞에 있는 것이 렌즈를 덮는다
            }
            return false;
        }

        /// <summary>
        /// **화면을 잡아먹는 앞물건이 있는가**(검수 반려 2026-09-09 — `34_mortar` 가로등).
        /// 「렌즈 앞 2.2m」로는 못 잡았다: 그 가로등은 3~4m 앞에 있었고, 자는 「안 막힘」이라 했는데
        /// 화면에서는 한복판을 세로로 갈랐다. 거리로 재던 것을 **각크기**로 바꾼다 — 화면을 얼마나
        /// 먹느냐는 거리가 아니라 「가까운 것이 커 보인다」의 문제다. 피사체보다 앞에 있고, 시선에서
        /// 25°(화각 절반) 안에 들고, **피사체보다 크게 보이면** 그 방위는 못 쓴다.
        /// </summary>
        static bool ForegroundHog(Vector3 eye, Vector3 lookAt, Transform subject, float subjectRadius)
        {
            Vector3 dir = lookAt - eye;
            float dist = dir.magnitude;
            if (dist < 0.01f)
                return false;
            dir /= dist;
            float subjAng = Mathf.Atan2(Mathf.Max(subjectRadius, 0.05f), dist);
            var rends = BlockerCache();
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null || r is ParticleSystemRenderer)
                    continue;
                var t = r.transform;
                if (subject != null && (t == subject || t.IsChildOf(subject) || subject.IsChildOf(t)))
                    continue;
                if (t.root.name == "Terrain" || t.name == "Ground")
                    continue;
                Vector3 to = r.bounds.center - eye;
                float d = to.magnitude;
                if (d < 0.05f || d >= dist)
                    continue;                                   // 피사체보다 뒤에 있는 것은 배경이다
                // 문턱은 **피사체 실루엣에 겹치는 만큼**이다(각반경 + 한 뼘 10°). 화면을 넓게(25°·35°)
                // 잡았더니 `33_campfire`에서 **잘 찍히던 방위까지 죽였다** — 화덕 옆에 비껴 선 가로등은
                // 그림을 해치지 않는데 「앞물건」으로 걸렸고, 대신 차양이 절반을 덮는 방위가 뽑혔다.
                // 자를 넓히는 것이 아니라 **묻는 것을 정확히** 한다: 가리는 것만 가린 것이다.
                if (Vector3.Angle(to, dir) > subjAng * Mathf.Rad2Deg + 10f)
                    continue;
                if (Mathf.Atan2(r.bounds.extents.magnitude, d) > subjAng * 0.8f)
                    return true;
            }
            return false;
        }

        static bool BlockedByRenderer(Vector3 eye, Vector3 point, Transform subject)
            => BlockedByRenderer(eye, point, subject, out _);

        /// <summary>
        /// **사람이 판정할 만큼 보이는가** — 머리와 몸통 두 점을 본다.
        ///
        /// **다섯 점(실루엣 좌우 끝·손 높이)까지 요구해 봤다가 되돌렸다**(2026-09-09):
        /// 검수가 지적한 `50_villagers` 첫 칸(은행원)은 **가림이 아니라 그늘 + 벽 페이드**였고
        /// — 원본 샷에서는 모자 비례도 손도 읽힌다 — 다섯 점으로 조인 결과 고친 것은 없이
        /// **훈련사가 아예 못 찍히는** 대가만 남았다(전 방위 탈락). 자를 조이면 세계가 좁아진다.
        /// 남은 밝기 문제는 「후보 방위 중 밝은 쪽 고르기」로 따로 잡는다(검수: 랩 B 뒤로).
        /// </summary>
        static bool PersonBlocked(Vector3 eye, Bounds box, Transform subject, out string blocker)
        {
            var points = new[]
            {
                box.center + Vector3.up * box.extents.y * 0.8f,   // 머리
                box.center,                                        // 몸통
            };
            for (int i = 0; i < points.Length; i++)
                if (BlockedByRenderer(eye, points[i], subject, out blocker))
                    return true;
            blocker = "";
            return false;
        }

        /// <summary>같은 판정에 **무엇이 막았는지**를 같이 돌려준다 — 「막혔다」만으로는
        /// 진짜 지붕인지 남의 바운드가 부푼 것인지 구분할 수 없다(이 저장소가 여러 번 밟은 함정).</summary>
        static bool BlockedByRenderer(Vector3 eye, Vector3 point, Transform subject, out string blocker)
        {
            blocker = "";
            var seg = point - eye;
            float len = seg.magnitude;
            if (len < 0.001f)
                return false;
            var ray = new Ray(eye, seg / len);
            var rends = BlockerCache();
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || rends[i] is ParticleSystemRenderer)
                    continue;
                var t = rends[i].transform;
                if (t == subject || t.IsChildOf(subject))
                    continue;
                if (rends[i].GetComponent<TerrainCollider>() != null || t.GetComponent<Terrain>() != null)
                    continue;
                // **선언된 예외 하나: 플레이어 아바타.** QA 씬의 플레이어는 스폰 자리에 세워 둔
                // 소품이라, 그 몸이 훈련사 앞을 막아 24방위가 전부 막힌 것으로 읽혔다(실측
                // `Player>Knight_Helmet`). 실제 플레이에서 플레이어는 비켜서면 그만이므로
                // 「구조적으로 안 보이는 자리」가 아니다 — 다른 사람·짐승은 그대로 가리는 것으로 센다.
                if (t.root.name == "Player")
                    continue;
                if (Vector3.Distance(rends[i].bounds.center, point) > 30f)
                    continue;                                   // 멀리 있는 것은 이 표본을 못 가린다
                // **카메라가 그 껍데기 안에 있으면 그것은 가리는 것이 아니다**(검수 (ㄴ) 판정 2026-09-08).
                // 바운드 안에서 쏜 광선은 `IntersectRay`가 거리 0으로 참을 돌려주기 때문에,
                // 안으로 들어가 찍는 순간 지붕이 스스로를 「가림」으로 세었다(당겨도 계속 빨간불이던 이유).
                if (rends[i].bounds.Contains(eye))
                    continue;
                // **표본 점이 남의 바운드 안에 있다고 봐주지 않는다**(2026-09-08 되돌림).
                // 한때 「바운드가 머리를 품으면 판정 불가」로 건너뛰었더니, 훈련사 타일이
                // **청록 지붕 뒤로 모자만 나온 채 통과**했다 — 자의 한계를 봐주는 규칙이
                // 곧 못 쓰는 샷을 통과시키는 구멍이 된다. 판정은 자를 느슨하게 해서가 아니라
                // 사람이 지붕 밑에 있다는 **사실을 보고**해서 닫는다.
                if (rends[i].bounds.IntersectRay(ray, out float dist) && dist < len - 0.05f)
                {
                    blocker = t.root.name + ">" + (t.parent != null ? t.parent.name + "/" : "") + rends[i].gameObject.name +
                              "(바운드 " + rends[i].bounds.size.ToString("0.0") + ")";
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// **머리 장비 상단이 프레임 안인가 — 보스 샷의 자**(검수 지시 2026-09-09, 조건 2).
        ///
        /// 「보스임을 읽게 하는 표식이 화면 밖이면 그 샷은 보스의 샷이 아니다」를 자로 만든다.
        /// 이 자가 없어서 `41_boss3`는 왕관이 잘린 채 **초록불로 지나갔다** — 화면이 유일한 자였다.
        /// 네 보스 샷(17·39·40·41)의 카메라를 **찍을 때와 같은 함수로** 만들고, 왕관 상단 점을
        /// 그 카메라 화면에 투영해 위아래·좌우 경계 안에 있는지 본다.
        ///
        /// **이 자가 못 보는 것**: 가림은 안 본다(프레임 안이어도 벽에 가릴 수 있다 — 그건 다른 자가 본다).
        /// </summary>
        public static bool HeadgearFramed(out string report)
        {
            var checks = new (string Shot, string Object)[]
            {
                ("17_boss_closeup", Dungeon3.BossObject),
                ("39_boss1", Dungeon1.BossObject),
                ("40_boss2", Dungeon2.BossObject),
                ("41_boss3", Dungeon3.BossObject),
            };
            bool ok = true;
            report = "";
            for (int i = 0; i < checks.Length; i++)
            {
                var go = FindSubject(checks[i].Object);
                if (go == null)
                {
                    report += " · " + checks[i].Shot + " 대상 없음";
                    ok = false;
                    continue;
                }
                Bounds crown = new Bounds();
                bool hasCrown = false;
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != VisualSliceBuilder.BossCrownObject)
                        continue;
                    foreach (var cr in t.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!cr.enabled || cr is ParticleSystemRenderer)
                            continue;
                        if (!hasCrown) { crown = cr.bounds; hasCrown = true; }
                        else crown.Encapsulate(cr.bounds);
                    }
                    break;
                }
                if (!hasCrown)
                {
                    // **왕관이 없는 보스는 이 자의 대상이 아니다** — 「없다」와 「밖으로 나갔다」는 다르다.
                    report += " · " + checks[i].Shot + " 왕관 없음(대상 아님)";
                    continue;
                }
                var shot = checks[i].Shot == "17_boss_closeup"
                    ? BossCloseUp(checks[i].Shot, Dungeon3.BossX, Dungeon3.BossZ)
                    : checks[i].Shot == "41_boss3"
                        ? BossShot41()
                        : FacilityCloseUp(checks[i].Shot, checks[i].Object, null, true);
                var rot = Quaternion.LookRotation((shot.Target - shot.Eye).normalized, Vector3.up);
                var local = Quaternion.Inverse(rot) * (new Vector3(crown.center.x, crown.max.y, crown.center.z) - shot.Eye);
                float tanY = Mathf.Tan(55f * 0.5f * Mathf.Deg2Rad), tanX = tanY * (W / (float)H);
                float sy = local.z > 0.01f ? local.y / (local.z * tanY) : 9f;
                float sx = local.z > 0.01f ? local.x / (local.z * tanX) : 9f;
                bool inFrame = Mathf.Abs(sy) <= 0.97f && Mathf.Abs(sx) <= 0.97f;
                report += " · " + checks[i].Shot + " 왕관 상단 화면 " + sx.ToString("0.00") + "," + sy.ToString("0.00") +
                          (inFrame ? " 안" : " **밖**");
                if (!inFrame)
                    ok = false;
            }
            return ok;
        }

        /// <summary>
        /// **같은 대상을 찍는 두 샷이 같은 그림인가 — 방위 차를 잰다**(검수 지시 2026-09-09).
        /// `17_boss_closeup`과 `41_boss3`은 같은 보스를 찍는다. 프레임을 넓히는 수리를 하고 나니
        /// 둘의 방위가 거의 같아져 **21샷 중 두 장이 같은 화면**이 됐다 — 「들어왔나」만 묻는 자는
        /// 이걸 못 본다. 그래서 「다른가」를 묻는 자를 따로 세운다.
        /// </summary>
        /// <summary>방위 후보 격자 한 칸 — 「다른 그림인가」의 하한도 여기서 온다(상수로 따로 정하지 않는다).</summary>
        public const float BearingGridStep = 45f;

        /// <summary>
        /// `41_boss3`을 만드는 **한 자리** — 17이 쓰는 방위를 피한다.
        /// 찍는 쪽·프레임 자·방위 자가 모두 이 함수를 부른다(같은 판정이 세 곳에 살면 어긋난다).
        /// </summary>
        static Shot BossShot41() =>
            FacilityCloseUp("41_boss3", Dungeon3.BossObject, null, true,
                            Yaw(BossCloseUp("17_boss_closeup", Dungeon3.BossX, Dungeon3.BossZ) is var s
                                ? s.Target - s.Eye : Vector3.forward));

        /// <summary>NC용 — **피하기를 끄고** 만든 41의 방위 차. 옛 상태(2°)가 재현돼야 자가 산 것이다.</summary>
        public static float BossShotBearingGapWithoutAvoid(out string report)
        {
            var a = BossCloseUp("17_boss_closeup", Dungeon3.BossX, Dungeon3.BossZ);
            var b = FacilityCloseUp("41_boss3", Dungeon3.BossObject, null, true, float.NaN);
            float ya = Yaw(a.Target - a.Eye), yb = Yaw(b.Target - b.Eye);
            report = "17 요 " + ya.ToString("0") + "° · 41(피하기 끔) 요 " + yb.ToString("0") + "°";
            return Mathf.Abs(Mathf.DeltaAngle(ya, yb));
        }

        public static float BossShotBearingGap(out string report)
        {
            var a = BossCloseUp("17_boss_closeup", Dungeon3.BossX, Dungeon3.BossZ);
            var b = BossShot41();
            float ya = Yaw(a.Target - a.Eye), yb = Yaw(b.Target - b.Eye);
            float gap = Mathf.Abs(Mathf.DeltaAngle(ya, yb));
            report = "17 요 " + ya.ToString("0") + "° · 41 요 " + yb.ToString("0") + "° → 벌어짐 " + gap.ToString("0") + "°";
            return gap;
        }

        static float Yaw(Vector3 v) => Mathf.Atan2(v.x, v.z) * Mathf.Rad2Deg;

        /// <summary>임의 시점 — 조망 샷용.</summary>
        /// <summary>
        /// 이 방위에서 **피사체의 카메라 쪽 면이 얼마나 해를 받는가**(-1~1). 카메라가 있는 쪽 방향과
        /// 햇빛이 오는 방향이 같을수록 1이다 — 해를 등지고 찍으면 얼굴이 통째로 그늘이다.
        /// </summary>
        static float SunFacing(float pitch, float yaw)
        {
            var sun = Object.FindFirstObjectByType<Light>(FindObjectsInactive.Include);
            Light dir = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { dir = l; break; }
            if (dir == null && sun == null)
                return 0f;
            Vector3 from = -(dir != null ? dir.transform.forward : sun.transform.forward);  // 햇빛이 오는 쪽
            Vector2 sunSide = new Vector2(from.x, from.z);
            if (sunSide.sqrMagnitude < 0.0001f)
                return 0f;
            Vector3 eyeDir = -(Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward);          // 카메라가 있는 쪽
            Vector2 camSide = new Vector2(eyeDir.x, eyeDir.z);
            if (camSide.sqrMagnitude < 0.0001f)
                return 0f;
            return Vector2.Dot(sunSide.normalized, camSide.normalized);
        }
    }
}
