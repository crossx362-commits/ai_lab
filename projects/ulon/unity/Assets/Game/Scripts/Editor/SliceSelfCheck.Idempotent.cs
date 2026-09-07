using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **자기 출력물을 자기 입력으로 삼지 않는다**(검수 판정 2026-09-07 ③④).
    ///
    /// 화덕 부속이 돌릴 때마다 밀려 5.4m까지 퍼진 사고의 원인은 「자리 기준」으로 시설의 **전체** 바운드를
    /// 읽은 것이었다 — 붙인 돌·장작이 다시 중심을 옮겨 다음 회차의 기준을 바꿨다. 고친 자리 셋을
    /// `HostAnchor`(자기 부속을 뺀 본체 바운드)로 바꿨지만, **고친 것과 다시 안 생기는 것은 다르다.**
    /// 그래서 두 자로 상시 잰다:
    ///
    ///  ① **소스 스캔** — 빌더에서 host 바운드가 **가로 자리**(center.x/z)로 흘러드는 호출이 0건인지.
    ///     허용 경로는 `HostAnchor` 하나뿐이다(높이 `min.y`/`max.y` 사용은 허용 — 자기참조 사고가 아니다).
    ///  ② **멱등 실측** — 보수 패스를 **한 번 더** 돌려 좌표가 안 움직이는지. 소스가 규칙을 지켜도
    ///     다른 경로로 밀릴 수 있으니, 결국 화면에 남는 성질(좌표)로 확인한다.
    ///
    /// 둘 다 네거티브 컨트롤이 있다 — ①은 옛 방식 코드를 실제로 스캐너에 먹이고, ②는
    /// `VisualSliceBuilder.AnchorSelfReferenceForNc`로 자기참조를 **되살려** 좌표가 실제로 밀리는 것을 본다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>host 트랜스폼에서 뽑은 바운드 변수 이름을 잡는다.</summary>
        static readonly Regex HostBoundsRe = new Regex(
            @"BoundsOf\(\s*(?:go\.transform|host|host\.transform)\s*,[^)]*out\s+Bounds\s+(\w+)\s*\)",
            RegexOptions.Compiled);

        /// <summary>그 바운드가 **가로 자리**로 쓰였는가 — 자기참조 사고는 전부 이 형태였다.</summary>
        static readonly string[] HorizontalUses = { ".center.x", ".center.z", ".center)" };

        /// <summary>소스 한 덩이를 훑어 위반을 돌려준다(네거티브 컨트롤이 같은 함수를 쓴다 — 자가 하나여야 한다).</summary>
        static List<string> ScanHostAnchorViolations(string source, out int reads, out int heightOnly)
        {
            var bad = new List<string>();
            reads = 0;
            heightOnly = 0;
            foreach (Match m in HostBoundsRe.Matches(source))
            {
                reads++;
                string varName = m.Groups[1].Value;
                // 그 호출 이후 창(15줄)에서 이 변수가 어떻게 쓰이는지 본다.
                int from = m.Index;
                int to = from;
                for (int line = 0; line < 15 && to < source.Length; line++)
                {
                    int nl = source.IndexOf('\n', to + 1);
                    if (nl < 0) { to = source.Length; break; }
                    to = nl;
                }
                string window = source.Substring(from, to - from);
                bool horizontal = false;
                for (int i = 0; i < HorizontalUses.Length; i++)
                    if (window.Contains(varName + HorizontalUses[i], StringComparison.Ordinal))
                        horizontal = true;
                if (horizontal)
                {
                    int lineNo = 1;
                    for (int i = 0; i < from; i++)
                        if (source[i] == '\n') lineNo++;
                    bad.Add("line " + lineNo + ": " + varName + " (시설 전체 바운드가 가로 자리로 흘러든다)");
                }
                else if (window.Contains(varName + ".min.y", StringComparison.Ordinal) ||
                         window.Contains(varName + ".max.y", StringComparison.Ordinal) ||
                         window.Contains(varName + ".size", StringComparison.Ordinal))
                {
                    heightOnly++;
                }
            }
            return bad;
        }

        static string BuilderSourcePath()
        {
            string p = Path.Combine(Application.dataPath, "Game/Scripts/Editor/VisualSliceBuilder.cs");
            if (!File.Exists(p))
                throw new InvalidOperationException("빌더 소스를 못 찾았습니다: " + p + " — 못 읽은 것을 통과로 적지 않는다.");
            return p;
        }

        /// <summary>`HostAnchor` 본문만 잘라낸다 — 그 안의 옛 방식은 **네거티브 컨트롤 스위치**라 정상이다.</summary>
        static string SourceWithoutHostAnchor(string source)
        {
            int start = source.IndexOf("static Vector3 HostAnchor(", StringComparison.Ordinal);
            if (start < 0)
                throw new InvalidOperationException("HostAnchor를 소스에서 못 찾았습니다 — 스캐너가 헛돌고 있습니다(0이면 실패).");
            int end = source.IndexOf("\n        static ", start + 10, StringComparison.Ordinal);
            if (end < 0)
                end = source.Length;
            return source.Remove(start, end - start);
        }

        static void AssertNoWholeFacilityAnchor()
        {
            string source = File.ReadAllText(BuilderSourcePath());
            // **스캐너가 헛돌지 않는지 먼저** — host 바운드 호출이 0건이면 규칙이 아니라 정규식이 죽은 것이다.
            var all = ScanHostAnchorViolations(source, out int allReads, out _);
            if (allReads == 0)
                throw new InvalidOperationException("빌더에서 host 바운드 호출을 한 건도 못 찾았습니다 — 정규식이 죽었습니다(0이면 실패).");
            var bad = ScanHostAnchorViolations(SourceWithoutHostAnchor(source), out int reads, out int heightOnly);
            Debug.Log("[Ulon] 자리 기준 소스 스캔 — host 바운드 호출 " + reads + "건(높이만 쓰는 것 " + heightOnly +
                      "건), 가로 자리로 흘러드는 것 " + bad.Count + "건. 허용 경로는 HostAnchor 하나뿐이다.");
            for (int i = 0; i < bad.Count; i++)
                Debug.Log("[Ulon]   자리 기준 위반 " + bad[i]);
            if (bad.Count > 0)
                throw new InvalidOperationException("빌더 " + bad.Count + "곳이 시설 **전체** 바운드를 자리 기준으로 씁니다(" +
                    bad[0] + ") — 자기가 붙인 부속이 다음 회차의 기준을 바꿉니다(화덕 5.4m 사고). " +
                    "HostAnchor를 쓰세요(자기 부속을 뺀 본체 바운드).");
        }

        /// <summary>네거티브 컨트롤 — **옛 방식 코드 그대로**를 스캐너에 먹여 빨간불을 본다.</summary>
        static void AssertNoWholeFacilityAnchorNegativeControl()
        {
            const string old =
                "            var go = GameObject.Find(host);\n" +
                "            if (BoundsOf(go.transform, true, out Bounds b) && b.size.sqrMagnitude > 0.0001f)\n" +
                "                at = new Vector3(b.center.x, b.min.y + 0.25f, b.center.z);\n";
            var bad = ScanHostAnchorViolations(old, out int reads, out _);
            if (reads == 0)
                throw new InvalidOperationException("자리 기준 네거티브 컨트롤 실패 — 옛 방식 코드에서 호출조차 못 찾았습니다.");
            if (bad.Count == 0)
                throw new InvalidOperationException("자리 기준 네거티브 컨트롤 실패 — 옛 방식(b.center.x/z로 배치)이 통과했습니다.");
            Debug.Log("[Ulon] 자리 기준 네거티브 컨트롤 통과 — 옛 방식 화덕 코드를 먹이자 " + bad.Count + "건 적발");
        }

        /// <summary>좌표가 흔들리면 안 되는 대상 — 씬의 보이는 것 전수(경로→위치).</summary>
        static Dictionary<string, Vector3> PositionSnapshot()
        {
            var map = new Dictionary<string, Vector3>();
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (!all[i].gameObject.activeInHierarchy)
                    continue;
                string path = GroundFit.NodePath(all[i]);
                map[path] = all[i].position;              // 같은 경로가 겹치면 마지막 것 — 비교는 경로 기준이라 무방
            }
            return map;
        }

        /// <summary>보수 패스 중 **자리를 잡는 것들**만 다시 돌린다(빌드 전체를 다시 짓지 않는다).</summary>
        static void RunPlacementPasses()
        {
            VisualSliceBuilder.EnsureFishingSpotAtWater();
            VisualSliceBuilder.EnsureVillageFacilities();
            VisualSliceBuilder.EnsureServiceNpcs();
            VisualSliceBuilder.EnsureVillagerLooks();
            VisualSliceBuilder.EnsureCampfireFire();
            VisualSliceBuilder.EnsureStableYardFence();
            VisualSliceBuilder.EnsureCapeIsBossOnly();
            VisualSliceBuilder.EnsureActorsOnSurface();
            Physics.SyncTransforms();
        }

        /// <summary>허용 흔들림 — 재착지 광선 오차 수준(2cm). 이보다 크면 「기준이 밀린다」는 뜻이다.</summary>
        const float IdempotentDriftMax = 0.02f;

        static List<string> MeasureDrift()
        {
            var before = PositionSnapshot();
            RunPlacementPasses();
            var after = PositionSnapshot();
            var moved = new List<string>();
            foreach (var kv in before)
            {
                if (!after.TryGetValue(kv.Key, out Vector3 now))
                    continue;                             // 헐고 다시 붙이는 부속은 경로가 같으니 대부분 남는다
                float d = Vector3.Distance(kv.Value, now);
                if (d > IdempotentDriftMax)
                    moved.Add(kv.Key + " " + d.ToString("0.00") + "m");
            }
            return moved;
        }

        static void AssertBuilderIdempotent()
        {
            var moved = MeasureDrift();
            Debug.Log("[Ulon] 멱등 실측 — 보수 패스를 한 번 더 돌린 뒤 " + IdempotentDriftMax + "m 넘게 움직인 것 " + moved.Count + "개");
            for (int i = 0; i < moved.Count && i < 10; i++)
                Debug.Log("[Ulon]   좌표 밀림 " + moved[i]);
            if (moved.Count > 0)
                throw new InvalidOperationException("보수 패스를 다시 돌리자 " + moved.Count + "개가 움직였습니다(" + moved[0] +
                    ") — 멱등이 아닙니다. 패스가 자기 출력물을 입력으로 읽고 있습니다.");
        }

        /// <summary>네거티브 컨트롤 — 자기참조를 **실제로 되살려** 좌표가 밀리는 것을 본다.</summary>
        static void AssertBuilderIdempotentNegativeControl()
        {
            var snapshot = PositionSnapshot();
            List<string> moved;
            VisualSliceBuilder.AnchorSelfReferenceForNc = true;
            try
            {
                RunPlacementPasses();                     // 자기참조 상태로 한 번(부속이 붙어 중심이 밀린다)
                moved = MeasureDrift();                   // 그 다음 회차에서 좌표가 또 밀리는지
            }
            finally
            {
                VisualSliceBuilder.AnchorSelfReferenceForNc = false;
                RunPlacementPasses();                     // 정상 기준으로 되돌린다
                RunPlacementPasses();                     // 되돌린 뒤 수렴시킨다(다음 게이트가 깨끗한 씬을 본다)
            }
            if (moved.Count == 0)
            {
                var names = new List<string>(snapshot.Keys);
                throw new InvalidOperationException("멱등 네거티브 컨트롤 실패 — 자기참조를 되살렸는데 좌표가 안 밀렸습니다(대상 " +
                    names.Count + "개). 계측이 결함보다 얕은지 의심하라.");
            }
            Debug.Log("[Ulon] 멱등 네거티브 컨트롤 통과 — 자기참조를 되살리자 " + moved.Count + "개가 밀림(" + moved[0] + ")");
        }
    }
}
