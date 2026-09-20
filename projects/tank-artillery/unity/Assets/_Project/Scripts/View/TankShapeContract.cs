using UnityEngine;
using Tankfall.Sim;

namespace Tankfall.View
{
    /// <summary>
    /// 🚨 **모델 전환의 안전 조건을 «코드로» 지킨다** (오너 결정 2026-09-20: 탱크를 외부 모델로).
    ///
    /// **규칙 하나**: 모델은 **«보이는 것»만 바꾸고 «재는 것»은 못 바꾼다.**
    /// 재는 것 = **발사점(`firePoint`)** · 히트박스 · 반지름.
    ///
    /// 🔑 **왜 발사점이 «재는 것»인가**: `BattleDemo` 가 `u.Fire.position` 을 그대로 탄도의 시작점
    ///    `p0` 으로 넘긴다(`ProjectileSimulator.Simulate(..., p0, ...)`). 즉 발사점이 몇 cm 만 달라져도
    ///    **모든 사격의 출발점이 달라지고**, 명중률·승률·M4 판정·맵 표·± 가 **조용히 전부 바뀐다.**
    ///    (히트박스는 안전하다 — `Radius = TankRadius` 상수에 `Center = 유닛 위치`라 메시와 무관하다.
    ///     ⚠️ 그 둘을 **모델 지오메트리에서 유도하도록 바꾸지 마라.** 그 순간 이 안전이 사라진다.)
    ///
    /// ⇒ 이 제약을 지키면 **모델 교체가 «설계상 밸런스 중립»** 이 된다. 그게 이 전환을 안전하게 만드는
    ///    유일한 조건이고, 그래서 **문서가 아니라 게이트**로 둔다.
    ///
    /// ⚠️ **모델이 들어오기 «전»에 만들어 뒀다** — 그래야 **첫 모델이 들어오는 순간** 걸린다.
    ///    (`ProceduralTank.Build` 은 이미 `BlenderModels.TryBuild` 로 먼저 넘어간다. 문은 이미 열려 있다.)
    /// </summary>
    public static class TankShapeContract
    {
        /// <summary>발사점이 같다고 볼 오차(m). 모델 파이프라인은 프로시저럴 발사점을 **그대로 써야** 한다.</summary>
        const float FireTol = 0.02f;

        /// <summary>
        /// 13종 전부에 대해 **모델 경로**와 **프로시저럴 경로**를 각각 지어 **발사점이 같은지** 본다.
        /// 모델이 없는 기종은 두 경로가 같은 것이므로 자동으로 통과한다(그 사실도 로그에 남는다).
        /// </summary>
        public static void Run(System.Action<bool, string> check)
        {
            var mat = new Material(Shader.Find("Unlit/Color"));
            int withModel = 0, procedural = 0;

            for (int i = 0; i < TankStats.Count; i++)
            {
                var kind = (TankKind)i;
                var shape = TankShape.Of(kind);
                string name = TankStats.Get(kind).Name;

                var liveRoot = ProceduralTank.Build(shape, mat, mat, mat, out _, out _, out var liveFire);
                ProceduralTank.ForceProcedural = true;
                var procRoot = ProceduralTank.Build(shape, mat, mat, mat, out _, out _, out var procFire);
                ProceduralTank.ForceProcedural = false;

                if (liveFire == null || procFire == null)
                {
                    check(false, $"{name} 발사점이 null — 지어지지 않았다");
                }
                else
                {
                    // 탱크 루트 기준 좌표로 본다 — 월드 위치는 배치에 따라 달라지므로 의미가 없다.
                    var a = liveRoot.InverseTransformPoint(liveFire.position);
                    var b = procRoot.InverseTransformPoint(procFire.position);
                    float d = Vector3.Distance(a, b);
                    bool same = d <= FireTol;
                    // 🔑 거부 메시지에 **이유와 고치는 법**을 같이 둔다(설명 없는 거부를 안 만든다).
                    check(same, same
                        ? $"{name} 발사점 일치 (Δ {d * 100f:F1}cm)"
                        : $"{name} **발사점이 다르다** Δ {d * 100f:F1}cm — 모델 {a} vs 프로시저럴 {b}. " +
                          "모델은 «보이는 것»만 바꿔야 한다: 모델 파이프라인이 자기 지오메트리에서 발사점을 " +
                          "유도하지 말고 **프로시저럴 발사점을 그대로 쓰게** 해라. 안 그러면 탄도 시작점이 바뀌어 " +
                          "명중률·승률·M4 판정이 전부 조용히 달라진다.");
                }

                bool usedModel = liveRoot != procRoot && liveRoot.name != procRoot.name;
                if (usedModel) withModel++; else procedural++;

                if (liveRoot != null) Object.DestroyImmediate(liveRoot.gameObject);
                if (procRoot != null) Object.DestroyImmediate(procRoot.gameObject);
            }

            // 🚨 **조용한 폴백 금지** — «지금 무엇이 그려졌나»를 코드가 말한다.
            //    둘 다 살려두면 먼저 그리는 쪽이 이기고, 아무도 어느 쪽인지 모른다.
            Debug.Log($"[Tankfall] 탱크 {TankStats.Count}종: 모델 {withModel} · 프로시저럴 {procedural}");
            check(true, $"경로 구성 — 모델 {withModel} · 프로시저럴 {procedural} (전환 중이면 섞여 있는 게 정상)");
        }
    }
}
