using UnityEngine;
using Tankfall.Sim;

namespace Tankfall.View
{
    /// <summary>
    /// 🚨 **모델 전환의 안전 조건을 «코드로» 지킨다** (오너 결정 2026-09-20: 탱크를 외부 모델로).
    ///
    /// **규칙 하나**: 모델은 **«보이는 것»만 바꾸고 «재는 것»은 못 바꾼다.**
    ///
    /// 🔑 **왜 발사점이 «재는 것»인가**: `BattleDemo` 가 `u.Fire.position` 을 그대로 탄도의 시작점
    ///    `p0` 으로 넘긴다. 발사점이 몇 cm 만 달라져도 **모든 사격의 출발점이 달라지고**,
    ///    명중률·승률·M4 판정·맵 표·± 가 **조용히 전부 바뀐다.**
    ///    (히트박스는 안전하다 — `Radius = TankRadius` 상수 · `Center = 유닛 위치`라 메시와 무관하다.
    ///     ⚠️ **그 둘을 모델 지오메트리에서 유도하도록 바꾸지 마라.** 그 순간 이 안전이 사라진다.)
    ///
    /// 🔑 **왜 팀색이 «재는 것»만큼 중요한가**: 팀색은 `accentMat` 으로 주입된다.
    ///    모델이 그 머티리얼을 안 쓰면 **아군과 적군이 화면에서 안 갈린다** — 게임이 성립하지 않는다.
    ///    전환에서 **제일 먼저 깨질 자리**라 발사점과 같은 급으로 잰다.
    ///
    /// ⚠️ **모델이 들어오기 «전»에 만들어 뒀다** — 그래야 **첫 모델이 들어오는 순간** 걸린다.
    /// </summary>
    public static class TankShapeContract
    {
        /// <summary>발사점이 같다고 볼 오차(m). 모델 파이프라인은 프로시저럴 발사점을 **그대로 써야** 한다.</summary>
        const float FireTol = 0.02f;

        public static void Run(System.Action<bool, string> check)
        {
            var mat = new Material(Shader.Find("Unlit/Color"));
            int withModel = 0, procedural = 0, bypassBroken = 0;

            for (int i = 0; i < TankStats.Count; i++)
            {
                var kind = (TankKind)i;
                var shape = TankShape.Of(kind);
                string name = TankStats.Get(kind).Name;

                // ── 1) 지금 «실제로» 지어지는 경로 ─────────────────────────────
                int c0 = ProceduralTank.ProceduralBuilds;
                var liveRoot = ProceduralTank.Build(shape, mat, mat, mat, out _, out _, out var liveFire);
                bool liveUsedModel = ProceduralTank.ProceduralBuilds == c0;   // 본체가 안 돌았다 = 모델이 이겼다

                // ── 2) 우회해서 프로시저럴로 한 번 더 ─────────────────────────
                int c1 = ProceduralTank.ProceduralBuilds;
                ProceduralTank.ForceProcedural = true;
                var procRoot = ProceduralTank.Build(shape, mat, mat, mat, out _, out _, out var procFire);
                ProceduralTank.ForceProcedural = false;
                bool bypassWorked = ProceduralTank.ProceduralBuilds > c1;

                // 🚨 **이 검사가 게이트의 게이트다.** 모델 로더가 `ForceProcedural` «밖»에 붙으면
                //    두 경로가 둘 다 모델이 되어 발사점 Δ 가 언제나 0 → 게이트가 «영원히 초록»이 된다.
                //    주석으로는 못 막아서 **프로시저럴 본체가 실제로 돌았는지 센다.**
                if (!bypassWorked) bypassBroken++;
                check(bypassWorked, bypassWorked
                    ? $"{name} 우회 작동 — 프로시저럴 대조군을 지었다"
                    : $"{name} **우회가 안 먹혔다** — `ForceProcedural=true` 인데 프로시저럴 본체가 안 돌았다. " +
                      "모델 로더 호출이 `if (!ProceduralTank.ForceProcedural) { … }` **밖**에 붙어 있다. " +
                      "감싸지 않으면 두 경로가 둘 다 모델이 되어 **이 게이트가 영원히 초록**이 된다.");

                // ── 3) 발사점 ─────────────────────────────────────────────────
                if (liveFire == null || procFire == null || liveRoot == null || procRoot == null)
                    check(false, $"{name} 발사점/루트가 null — 지어지지 않았다");
                else
                {
                    var a = liveRoot.InverseTransformPoint(liveFire.position);
                    var b = procRoot.InverseTransformPoint(procFire.position);
                    float d = Vector3.Distance(a, b);
                    check(d <= FireTol, d <= FireTol
                        ? $"{name} 발사점 일치 (Δ {d * 100f:F1}cm)"
                        : $"{name} **발사점이 다르다** Δ {d * 100f:F1}cm — 모델 {a} vs 프로시저럴 {b}. " +
                          "모델은 «보이는 것»만 바꿔야 한다: 모델이 자기 지오메트리에서 발사점을 유도하지 말고 " +
                          "**프로시저럴 발사점을 그대로 쓰게** 해라. 안 그러면 탄도 시작점이 바뀌어 " +
                          "명중률·승률·M4 판정이 전부 조용히 달라진다.");
                }

                // ── 4) 팀색 주입 ──────────────────────────────────────────────
                var teamMat = new Material(Shader.Find("Unlit/Color")) { color = new Color(0.15f, 0.48f, 0.98f) };
                var tinted = ProceduralTank.Build(shape, mat, mat, teamMat, out _, out _, out _);
                bool tookTeamMat = false;
                if (tinted != null)
                    foreach (var r in tinted.GetComponentsInChildren<Renderer>())
                        foreach (var m in r.sharedMaterials)
                            if (m == teamMat) { tookTeamMat = true; break; }
                check(tookTeamMat, tookTeamMat
                    ? $"{name} 팀색 주입됨 (accentMat 이 렌더러에 실제로 쓰였다)"
                    : $"{name} **팀색이 안 먹었다** — `accentMat` 으로 넘긴 머티리얼이 어느 렌더러에도 안 쓰였다. " +
                      "모델이 자기 머티리얼만 쓰면 **아군과 적군이 화면에서 안 갈린다.** " +
                      "모델 파이프라인이 팀색 슬롯에 `accentMat` 을 **그대로 물리게** 해라.");

                if (liveUsedModel) withModel++; else procedural++;

                if (liveRoot != null) Object.DestroyImmediate(liveRoot.gameObject);
                if (procRoot != null) Object.DestroyImmediate(procRoot.gameObject);
                if (tinted != null) Object.DestroyImmediate(tinted.gameObject);
            }

            // 🚨 **조용한 폴백 금지** — «지금 무엇이 그려졌나»를 코드가 말한다.
            Debug.Log($"[Tankfall] 탱크 {TankStats.Count}종: 모델 {withModel} · 프로시저럴 {procedural}");

            // 🚨 **«자동 통과하는 초록»을 화면이 말하게 한다.**
            //    모델이 0 이면 두 경로가 같은 코드라 Δ 가 당연히 0 이다 — 그건 **대조군이 없다는 뜻**이지
            //    「모델이 안전하다」는 뜻이 아니다. 초록을 초록으로만 읽히게 두면 안 된다.
            string warn = withModel == 0
                ? " — ⚠️ **대조군 없음(모델 0종)**. 첫 모델이 들어와야 이 게이트가 의미를 갖는다."
                : (bypassBroken > 0 ? $" — 🚨 우회 깨짐 {bypassBroken}종" : "");
            check(bypassBroken == 0, $"경로 구성 — 모델 {withModel} · 프로시저럴 {procedural}{warn}");
        }

        /// <summary>
        /// **성능 기준선 — «프로시저럴 8대». 지금만 잴 수 있다.**
        /// 🔑 첫 모델이 들어오면 **«before» 는 영영 못 만든다**(밸런스 기준선에서 배운 그것).
        /// 잰 것: 삼각형 수 · 렌더러 수(드로콜 상한) · 메시 메모리.
        /// </summary>
        public static void PerfBaseline()
        {
            var mat = new Material(Shader.Find("Unlit/Color"));
            int tris = 0, renderers = 0; long mem = 0;
            var roots = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < 8; i++)
            {
                var kind = (TankKind)(i % TankStats.Count);
                var root = ProceduralTank.Build(TankShape.Of(kind), mat, mat, mat, out _, out _, out _);
                roots.Add(root);
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh != null)
                    {
                        tris += mf.sharedMesh.triangles.Length / 3;
                        mem += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mf.sharedMesh);
                    }
                renderers += root.GetComponentsInChildren<Renderer>().Length;
            }
            // 🚨 **0 을 «값»으로 찍지 마라.** 플레이어 빌드에서 `GetRuntimeMemorySizeLong` 은 0 을 준다 —
            //    그건 「메모리를 안 쓴다」가 아니라 **「여기서는 못 잰다」**다(오늘의 「없음은 값이 아니다」).
            string memTxt = mem > 0 ? $"{mem / 1024f:N0}KB" : "⏭ 측정 불가(플레이어 빌드에서 Profiler 가 0 을 준다 — 0 이 아니라 «못 잼»이다)";
            Debug.Log($"[Tankfall] 📐 성능 기준선 — **프로시저럴 8대** · 삼각형 {tris:N0} · 렌더러 {renderers}(드로콜 상한) · 메시 메모리 {memTxt}");
            Debug.Log("[Tankfall]    ⚠️ 이 줄은 «모델 이전»의 값이다. 모델이 들어오면 같은 형식으로 다시 재서 나란히 놓아라.");
            foreach (var r in roots) if (r != null) Object.DestroyImmediate(r.gameObject);
        }
    }
}
