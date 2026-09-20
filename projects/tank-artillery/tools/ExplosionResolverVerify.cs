using System;
using System.Collections.Generic;
using Tankfall.Sim;

/// <summary>
/// 🧪 `ExplosionResolver` 계약 검사 — `docs/EXPLOSION_RESOLVER_CONTRACT.md`
///
/// 🔑 **예측은 «짜기 전에» 계약과 공용 공식만 보고 적은 것이고, 여기에 «하드코딩»돼 있다.**
///    결과를 보고 예측을 고칠 수 없게 하려는 것이다(사전 예측의 값은 «못 고치는 데» 있다).
/// ⚠️ **방어력은 100 으로 고정**한다 — `AfterDefense(raw, 100) == raw` 라 **예측값이 그대로 보인다.**
///    (기종 실제 방어력을 쓰면 예측 표를 다시 계산해야 하고, 그러면 «사전»이 아니게 된다.)
/// </summary>
static class ExplosionResolverVerify
{
    static int _fail;
    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  ✅ " : "  ❌ ") + what);
        if (!ok) _fail++;
    }

    struct Kind { public string Name; public float R, Base, Direct; }

    static readonly Kind Carrot  = new Kind { Name = "캐롯탱크", R = 7.0f, Base = 250f, Direct = 100f };
    static readonly Kind SecWind = new Kind { Name = "세크윈드", R = 9.5f, Base = 220f, Direct = 110f };

    /// <summary>후보 하나짜리 판을 만들어 «그 유닛이 받은 피해»를 돌려준다. 0 이면 «대상에서 빠졌다»는 뜻.</summary>
    static int One(in Kind k, float dist, bool direct, float scale = 1f)
    {
        var cands = new List<ExplosionResolver.Candidate>
        {
            new ExplosionResolver.Candidate { Id = 7, Team = 1, Center = new Vec3(dist, 0f, 0f), Defense = 100f },
        };
        var f = ExplosionResolver.Resolve(new Vec3(0f, 0f, 0f), k.R, k.Base, k.Direct, scale,
                                          direct ? 7 : -1, cands, true, 1, 0);
        return f.Hits.Count == 0 ? 0 : f.Hits[0].Damage;
    }

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== ExplosionResolver 계약 검사 (예측 ↔ 그릇) ===\n");
        Console.WriteLine("⚠️ 이것은 **①(그릇 ↔ 하네스)의 «절반»**이다 — 지금 대조하는 것은 «예측»이다.");
        Console.WriteLine("   하네스 쪽은 폭발 절차가 `RunMatch` 안에 인라인이라 «부를 수 없다» — 그걸 떼어내는 것이 다음 덩어리다.");
        Console.WriteLine("⚠️ 작성자가 양쪽 일부를 이미 읽었다(계약 §4-2) ⇒ **통과는 «약한» 증거, 불일치는 «강한» 증거.**\n");

        foreach (var k in new[] { Carrot, SecWind })
        {
            float zero = k.Name == "캐롯탱크" ? 6.95f : 9.43f;   // 예측: 피해가 0 이 되기 «시작»하는 거리
            int at0    = k.Name == "캐롯탱크" ? 250 : 220;
            int at0Dir = k.Name == "캐롯탱크" ? 350 : 330;

            Console.WriteLine($"── {k.Name} (r {k.R:F1} · base {k.Base:F0} · direct {k.Direct:F0})");
            Check(One(k, 0f, false)        == at0,    $"d=0        → {One(k, 0f, false)} (예측 {at0})");
            Check(One(k, 0f, true)         == at0Dir, $"d=0 직격    → {One(k, 0f, true)} (예측 {at0Dir})");
            Check(One(k, k.R, false)       == 0,      $"d=r        → {One(k, k.R, false)} (예측 0 — 대상에서 빠진다)");
            Check(One(k, k.R + 0.01f, false) == 0,    $"d=r+1cm    → {One(k, k.R + 0.01f, false)} (예측 0)");
            Check(One(k, k.R - 0.01f, false) == 0,    $"d=r−1cm    → {One(k, k.R - 0.01f, false)} (예측 0 — **반경 «안»이지만 0**)");
            Check(One(k, zero + 0.01f, false) == 0,   $"d={zero + 0.01f:F2}     → {One(k, zero + 0.01f, false)} (예측 0 — 0 이 시작된 뒤)");
            Check(One(k, zero - 0.02f, false) > 0,    $"d={zero - 0.02f:F2}     → {One(k, zero - 0.02f, false)} (예측 >0 — 0 이 되기 직전)");
            Console.WriteLine();
        }

        // ── 배율: 곱하는 «순서»가 계약이다(경계 #8) ──────────────────────────
        Console.WriteLine("── 배율(2번탄) — 곱한 «뒤» 감쇠한다");
        var carrot2 = new Kind { Name = "캐롯 2번탄", R = 7.0f * 1.15f, Base = 250f * 0.45f, Direct = 100f * 0.80f };
        var sec2    = new Kind { Name = "세크 2번탄", R = 9.5f * 0.65f, Base = 220f * 0.77f, Direct = 110f * 1.00f };
        // ✏️ **예측이 틀렸다 — 그리고 «왜»가 값지다**(2026-09-20).
        //    캐롯 2번탄은 `250 × 0.45 = 112.5` 로 **정확히 .5 경계**에 떨어진다.
        //    내 예측 스크립트는 `floor(x+0.5)`(올림)를 썼는데 **C# `MathF.Round` 는 «짝수로» 반올림**한다
        //    ⇒ **112.5 → 112**(113 아님). **그릇이 맞고 예측이 틀렸다** — 그릇은 공용 `Damage.Compute` 를 부른다.
        //    🛑 **예측 숫자는 계약에서 «안 고쳤다»** — 틀린 채로 두고 사유를 남기는 게 사전 예측의 값이다.
        //    🔑 그리고 **이 케이스는 «값»이 아니라 «반올림 «규칙»»을 시험하고 있었다** — 경계의 다른 얼굴이다.
        Check(One(carrot2, 0f, false) == 112, $"캐롯 2번탄 d=0     → {One(carrot2, 0f, false)} (예측 113 ✏️ 틀림 — 112.5 는 .5 경계, C# 은 짝수로 반올림)");
        Check(One(carrot2, 0f, true)  == 192, $"캐롯 2번탄 d=0 직격 → {One(carrot2, 0f, true)} (예측 193 ✏️ 같은 이유)");
        Check(One(sec2, 0f, false)    == 169, $"세크 2번탄 d=0     → {One(sec2, 0f, false)} (예측 169)");
        Check(One(sec2, 0f, true)     == 279, $"세크 2번탄 d=0 직격 → {One(sec2, 0f, true)} (예측 279)");
        Console.WriteLine();

        // ── 대상 0명 · 여럿 ──────────────────────────────────────────────────
        Console.WriteLine("── 대상 수");
        var empty = ExplosionResolver.Resolve(new Vec3(0, 0, 0), 7f, 250f, 100f, 1f, -1,
                                              new List<ExplosionResolver.Candidate>(), true, 1, 0);
        Check(empty.Hits.Count == 0, $"후보 0명 → 대상 {empty.Hits.Count} (예측 0)");

        var many = new List<ExplosionResolver.Candidate>
        {
            new ExplosionResolver.Candidate { Id = 1, Team = 1, Center = new Vec3(0f, 0, 0), Defense = 100f },
            new ExplosionResolver.Candidate { Id = 2, Team = 1, Center = new Vec3(3f, 0, 0), Defense = 100f },
            new ExplosionResolver.Candidate { Id = 3, Team = 1, Center = new Vec3(99f, 0, 0), Defense = 100f },  // 반경 밖
        };
        var mf = ExplosionResolver.Resolve(new Vec3(0, 0, 0), 7f, 250f, 100f, 1f, -1, many, true, 1, 0);
        Check(mf.Hits.Count == 2, $"후보 3명(하나는 반경 밖) → 대상 {mf.Hits.Count} (예측 2)");
        Check(mf.Hits.Count == 2 && mf.Hits[0].Damage > mf.Hits[1].Damage,
              "가까운 쪽이 더 아프다");

        // ── 사실이지 판단이 아니다 ───────────────────────────────────────────
        Console.WriteLine();
        Console.WriteLine("── 사실 vs 판단");
        var noAtk = ExplosionResolver.Resolve(new Vec3(0, 0, 0), 7f, 250f, 100f, 1f, -1, many, false, -1, -1);
        Check(!noAtk.HasAttacker, "가해자 «없음»이 표현된다 (지뢰·서든데스처럼 쏜 사람이 없는 피해)");
        var shielded = ExplosionResolver.Resolve(new Vec3(0, 0, 0), 7f, 250f, 100f, 1f, -1, many, true, 1, 0,
                                                 id => id == 1);
        Check(shielded.Hits.Count == 2 && shielded.Hits[0].ShieldBlocked && !shielded.Hits[1].ShieldBlocked,
              "실드로 막힌 유닛도 목록에 «남는다» — 「명중인가」는 호출부가 정한다");

        // ── 못 재는 것을 «못 잰다»고 찍는다 ──────────────────────────────────
        Console.WriteLine();
        Console.WriteLine("⏭ **귀속(누가 준 피해인가) — ①에서 확인 불가.** 하네스엔 대응 개념이 없다");
        Console.WriteLine("   (하네스는 «피해자 팀»으로 근사하고 게임은 «사수»로 센다 — 자해에서 갈린다).");
        Console.WriteLine("   ⇒ 이 줄은 «통과»가 아니라 «대조군 없음»이다. ②(그릇 ↔ 게임)에서만 닫힌다.\n");

        Console.WriteLine(_fail == 0
            ? "=== 전부 통과 (⚠️ 위 두 줄의 한계와 함께 읽어라) ==="
            : $"=== 실패 {_fail}건 ===");
        Environment.Exit(_fail == 0 ? 0 : 1);
    }
}
