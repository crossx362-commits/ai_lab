// `AiGunner.PickShell` 의 지속피해 평가가 **적용 규칙과 같은 걸 보는지** 재는 하네스.
//
// 배경(2026-09-17): `EffectValue` 가 독·화상을 **총량**으로 쳐 줬다. 그런데 `StatusEffects.Poison` 은
// `_poison[id] = ...` **덮어쓰기**라 안 쌓인다 — 이미 중독된 적에게 다시 쏴도 남은 턴이 갱신될 뿐이다.
// AI 는 가장 가까운 적을 반복해서 쏘므로 실제로 자주 걸리던 오차다. 포세이돈 독 저항 50% 도 안 깎았다.
//
// ⚠️ 검사마다 네거티브 컨트롤을 붙였다. 특히 **"인자를 안 넘기면 옛 값이 나온다"** 를 직접 확인한다 —
//    새 인자가 실제로 읽히는지를 안 보면 "넣었는데 안 도는" 상태를 못 잡는다
//    (궁극기가 한 번도 발동 안 했는데 ON/OFF 승률이 같게 나와서야 들통난 사고와 같은 종류).
//
// 도는 법:  ./tools/verify.sh shellpick
//   (verify.sh 에 한 줄:  shellpick)  run_console ShellPickVerify $SIM/*.cs tools/ShellPickVerify.cs ;;

using System;
using System.Collections.Generic;
using Tankfall.Sim;

static class ShellPickVerify
{
    static int _fail;
    static void Test(string name, bool ok, string detail = "")
    {
        Console.WriteLine($"  {(ok ? "✅" : "❌")} {name}{(detail.Length > 0 ? "   " + detail : "")}");
        if (!ok) _fail++;
    }

    static List<AiGunner.SubImpact> One(Vec3 at, int directId, float scale = 1f)
        => new List<AiGunner.SubImpact> { new AiGunner.SubImpact { Impact = at, DirectId = directId, Scale = scale } };

    static List<AiGunner.Target> Foe(int id, Vec3 at, TankKind kind, float def = 100f)
        => new List<AiGunner.Target> { new AiGunner.Target { Id = id, Center = at, Defense = def, Kind = kind } };

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("=== 탄종 선택의 지속피해 평가 검증 ===\n");

        // ═══════════════════════════════════════════════════════════
        Console.WriteLine("[1] PoisonGain — 덮어쓰기 규칙을 그대로 따르는가");
        {
            var st = new StatusEffects();
            // 크로스보우 독 25/턴 × 3턴 = 75
            Test("멀쩡한 적 → 총량 그대로", st.PoisonGain(1, 25, 3, TankKind.Cannon) == 75,
                 $"{st.PoisonGain(1, 25, 3, TankKind.Cannon)}");

            st.Poison(1, 25, 3, TankKind.Cannon);
            Test("방금 중독된 적에게 같은 독 → 이득 0 (덮어쓰기라 안 쌓인다)",
                 st.PoisonGain(1, 25, 3, TankKind.Cannon) == 0, $"{st.PoisonGain(1, 25, 3, TankKind.Cannon)}");

            st.TickStartOfTurn(1);           // 1턴 지남 → 남은 25×2 = 50
            Test("한 턴 지난 뒤 → 남은 만큼만 이득 (75-50=25)",
                 st.PoisonGain(1, 25, 3, TankKind.Cannon) == 25, $"{st.PoisonGain(1, 25, 3, TankKind.Cannon)}");

            Test("더 약한 독으로 덮어쓰기 → 이득 0 (손해를 이득으로 세지 않는다)",
                 st.PoisonGain(1, 10, 2, TankKind.Cannon) == 0, $"{st.PoisonGain(1, 10, 2, TankKind.Cannon)}");

            // 포세이돈 저항 — 적용과 평가가 같은 식(PoisonAfterResist)을 봐야 한다
            var st2 = new StatusEffects();
            int pos = st2.PoisonGain(2, 25, 3, TankKind.Poseidon);
            Test("포세이돈은 독 저항 50% 가 평가에도 걸린다", pos == 39, $"{pos} (25→13/턴 × 3턴)");
            Test("  네거티브 컨트롤: 다른 기종은 안 깎인다",
                 st2.PoisonGain(3, 25, 3, TankKind.Cannon) == 75, $"{st2.PoisonGain(3, 25, 3, TankKind.Cannon)}");

            // 적용 후 실제로 들어가는 피해와 평가가 일치하는가 (가장 중요한 검사)
            var st3 = new StatusEffects();
            int predicted = st3.PoisonGain(4, 25, 3, TankKind.Poseidon);
            st3.Poison(4, 25, 3, TankKind.Poseidon);
            int actual = 0;
            for (int t = 0; t < 5; t++) actual += st3.TickStartOfTurn(4);
            Test("예측 = 실제로 들어간 피해 (평가와 적용이 안 갈린다)", predicted == actual,
                 $"예측 {predicted} · 실제 {actual}");
        }

        // ═══════════════════════════════════════════════════════════
        Console.WriteLine("\n[2] BurnGain — 화상도 덮어쓰기다 (저항은 없다)");
        {
            var st = new StatusEffects();
            Test("멀쩡한 적 → 총량(40×3=120)", st.BurnGain(1, 40, 3) == 120, $"{st.BurnGain(1, 40, 3)}");
            st.Burn(1, 40, 3);
            Test("이미 화상 → 이득 0", st.BurnGain(1, 40, 3) == 0, $"{st.BurnGain(1, 40, 3)}");
            int predicted = 120; int actual = 0;
            for (int t = 0; t < 5; t++) actual += st.TickStartOfTurn(1);
            Test("예측 = 실제", predicted == actual, $"예측 {predicted} · 실제 {actual}");
        }

        // ═══════════════════════════════════════════════════════════
        // ⚠️ **알려진 어긋남을 눈에 보이게 찍는다 (2026-09-19).**
        //    판정(rc)에는 영향을 주지 않는다 — 어긋남을 «보이게» 하는 것과 «판정»하는 것은 다른 일이고,
        //    여기서 rc 를 내면 고치기 전까지 `verify.sh` 가 계속 죽는다.
        //    그렇다고 아무 데도 안 적으면 다음 사람이 "AI 가 왜 캐터펄트 2번탄을 과대평가하지"를
        //    처음부터 다시 판다. **못 고칠 것은 적어도 보이게 둔다.**
        //    ✏️ 2026-09-19 갱신: 여기 「오너 지시 전까지 **금지**」라고 적혀 있었는데 **그 동결은 09-19 에
        //       풀렸다.** 밸런스는 열려 있다 — 다만 순서가 ①판 구성 → ②수치 → ③표 → ④네트워크라
        //       이 어긋남은 ② 차례다(`docs/BALANCE_PREP_TANK_ARTILLERY.md`).
        //       ⚠️ 가드·주석의 «사유»는 유통기한이 있다. 금지를 풀 때 **그걸 찍는 자리도 같이 고쳐라.**
        {
            // 2026-09-19: 여기는 「알려진 어긋남」 경고였다 — `AiGunner` 가 `BurnGain` 으로 점수를 매기는데
            // 게임이 `StatusEffects.Burn` 을 **한 번도 안 불렀다.** 오너 허가로 배선했으므로 **게이트로 승격**한다.
            // 재는 것: **AI 가 계산한 이득이 실제로 들어가는 피해와 같은가.**
            // ⚠️ 평가(BurnGain)와 적용(Burn+TickStartOfTurn)이 같은 규칙(덮어쓰기)을 봐야 한다.
            //    식을 AiGunner 쪽에 베껴 두면 한쪽만 낡는다 — 독이 그래서 한 번 틀렸다(d61e7527).
            var fx = ShellEffects.Of(TankKind.Catapult, ShellKind.Special);
            Console.WriteLine("\n[2-1] 화상: AI 평가 == 실제 적용 피해 (배선 게이트)");
            {
                var probe = new StatusEffects();
                int aiScore = probe.BurnGain(9, fx.Param1, fx.Param2);
                probe.Burn(9, fx.Param1, fx.Param2);
                int real = 0;
                for (int t = 0; t < fx.Param2 + 2; t++) real += probe.TickStartOfTurn(9);
                Test($"멀쩡한 적: 평가 {aiScore} == 실제 {real}", aiScore == real, $"{aiScore} vs {real}");

                // 이미 타고 있는 적에게 다시 걸면 **덮어쓰기**라 추가 이득이 0 이어야 한다.
                var probe2 = new StatusEffects();
                probe2.Burn(9, fx.Param1, fx.Param2);
                int gain2 = probe2.BurnGain(9, fx.Param1, fx.Param2);
                probe2.Burn(9, fx.Param1, fx.Param2);
                int real2 = 0;
                for (int t = 0; t < fx.Param2 + 2; t++) real2 += probe2.TickStartOfTurn(9);
                Test($"이미 화상: 추가 이득 {gain2} == 0, 총량은 그대로 {real2}", gain2 == 0 && real2 == fx.Param1 * fx.Param2,
                     $"gain {gain2} · total {real2}");
            }
        }

        // ═══════════════════════════════════════════════════════════
        Console.WriteLine("\n[3] PickShell 이 그 값을 실제로 읽는가 — 거리를 훑어 선택이 바뀌는 지점을 찾는다");
        Console.WriteLine("    캐터펄트(화상 40×3). 멀쩡한 적 vs 이미 화상 걸린 적.\n");
        {
            var baseSt = TankStats.For(TankKind.Catapult, ShellKind.Normal, 1f, Weather.Clear);
            var impact = new Vec3(100f, 10f, 100f);
            int flipped = 0;
            Console.WriteLine($"    {"거리",6} {"멀쩡한 적",10} {"화상 걸린 적",12}");
            foreach (float d in new[] { 0f, 1f, 2f, 3f, 4f, 5f, 6f, 6.5f })
            {
                var foes = Foe(7, new Vec3(100f + d, 10f, 100f), TankKind.Cannon);
                int direct = d < 1e-3f ? 7 : -1;
                var n = One(impact, direct);
                var s = One(impact, direct);

                var fresh = new StatusEffects();
                var burnt = new StatusEffects(); burnt.Burn(7, 40, 3);

                var pf = AiGunner.PickShell(baseSt, n, s, foes, fresh);
                var pb = AiGunner.PickShell(baseSt, n, s, foes, burnt);
                if (pf != pb) flipped++;
                Console.WriteLine($"    {(direct >= 0 ? "정타" : $"{d:F1}m"),6} {(pf == ShellKind.Special ? "2번탄" : "1번탄"),10} {(pb == ShellKind.Special ? "2번탄" : "1번탄"),12}{(pf != pb ? "   ← 바뀐다" : "")}");
            }
            Test("이미 화상 걸린 적에게는 2번탄을 덜 고른다 (판별력 검사)", flipped > 0,
                 flipped > 0 ? $"{flipped}개 거리에서 선택이 바뀜"
                             : "어느 거리에서도 안 바뀜 — 이 검사는 아무것도 안 재고 있다");
        }

        // ═══════════════════════════════════════════════════════════
        Console.WriteLine("\n[4] 네거티브 컨트롤 — status 를 안 넘기면(null) 옛 동작으로 떨어지는가");
        Console.WriteLine("    (새 인자가 정말 읽히는지 확인. 여기서 차이가 없으면 인자가 죽은 것이다)");
        {
            var baseSt = TankStats.For(TankKind.Catapult, ShellKind.Normal, 1f, Weather.Clear);
            var impact = new Vec3(100f, 10f, 100f);
            var foes = Foe(7, new Vec3(104f, 10f, 100f), TankKind.Cannon);
            var n = One(impact, -1); var s = One(impact, -1);

            var burnt = new StatusEffects(); burnt.Burn(7, 40, 3);
            var withNull = AiGunner.PickShell(baseSt, n, s, foes, null);
            var withState = AiGunner.PickShell(baseSt, n, s, foes, burnt);
            Console.WriteLine($"    null → {(withNull == ShellKind.Special ? "2번탄" : "1번탄")} · 실제 상태 → {(withState == ShellKind.Special ? "2번탄" : "1번탄")}");
            Test("null 과 실제 상태의 결과가 다르다 (= 인자가 살아 있다)", withNull != withState,
                 withNull != withState ? "" : "같다 — status 가 안 읽히고 있다");
        }

        Console.WriteLine($"\n=== {(_fail == 0 ? "전부 통과" : $"실패 {_fail}건")} ===");
        Environment.Exit(_fail == 0 ? 0 : 1);
    }
}
