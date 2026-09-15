// 나이스샷 규칙 검증 — 네거티브 컨트롤 필수
//
// 검증 항목:
//   1) Judge: 경계 바로 안(±Tolerance−ε) true, 바로 밖(±Tolerance+ε) false
//   2) SkillGauge: 포인트 누적 및 SS 발동 조건
//   3) AiJudge: 난이도별 나이스샷 확률 단조성 (초보 < 전문가 < 에이스)
//   4) ApplySs: SS 적용 후 피해·폭발 증가 검증

using System;
using System.Collections.Generic;
using Tankfall.Sim;

static class NiceShotVerify
{
    static int _pass, _fail;

    static void Test(string name, bool condition)
    {
        if (condition) { Console.WriteLine($"  ✅ {name}"); _pass++; }
        else { Console.WriteLine($"  ❌ {name}"); _fail++; }
    }

    static void Main()
    {
        Console.WriteLine("=== 나이스샷 규칙 검증 ===\n");

        TestJudge();
        Console.WriteLine();
        TestSkillGauge();
        Console.WriteLine();
        TestAiJudge();
        Console.WriteLine();
        TestApplySs();
        Console.WriteLine();

        if (_fail == 0)
            Console.WriteLine($"✅ 전체 통과 ({_pass}개 검사)");
        else
        {
            Console.WriteLine($"❌ 실패 있음 (통과 {_pass}, 실패 {_fail})");
            Environment.Exit(1);
        }
    }

    static void TestJudge()
    {
        Console.WriteLine("### 1. Judge — 경계 조건");

        const float eps = 0.0001f;
        float marked = 0.5f;

        // 범위 안: 정확히 같은 값
        Test("정확히 같은 값", NiceShot.Judge(marked, marked));

        // 범위 안: Tolerance−ε
        Test("Tolerance−ε (안)", NiceShot.Judge(marked, marked + NiceShot.Tolerance - eps));
        Test("Tolerance−ε (반대)", NiceShot.Judge(marked, marked - NiceShot.Tolerance + eps));

        // 경계: Tolerance 정확히
        Test("Tolerance 경계 (포함)", NiceShot.Judge(marked, marked + NiceShot.Tolerance));
        Test("−Tolerance 경계 (포함)", NiceShot.Judge(marked, marked - NiceShot.Tolerance));

        // 범위 밖: Tolerance+ε
        Test("Tolerance+ε (밖)", !NiceShot.Judge(marked, marked + NiceShot.Tolerance + eps));
        Test("−Tolerance−ε (밖)", !NiceShot.Judge(marked, marked - NiceShot.Tolerance - eps));

        // 극단값
        Test("0과 1의 나이스샷", NiceShot.Judge(0f, 0f) && NiceShot.Judge(1f, 1f));
        Test("0과 1은 거리 1 > Tolerance", !NiceShot.Judge(0f, 1f));
    }

    static void TestSkillGauge()
    {
        Console.WriteLine("### 2. SkillGauge — 상태 전환");

        var gauge = new SkillGauge();

        // 초기: 0포인트, SS 불가
        Test("초기 포인트 = 0", gauge.Points == 0);
        Test("초기 CanSs = false", !gauge.CanSs());

        // 1회 나이스샷
        gauge.OnNiceShot();
        Test("1회 OnNiceShot → Points = 1", gauge.Points == 1);
        Test("1포인트에선 CanSs = false", !gauge.CanSs());

        // 2회 나이스샷
        gauge.OnNiceShot();
        Test("2회 OnNiceShot → Points = 2", gauge.Points == 2);
        Test("2포인트에선 CanSs = true", gauge.CanSs());

        // SS 사용
        gauge.SpendSs();
        Test("SpendSs → Points = 0", gauge.Points == 0);
        Test("사용 후 CanSs = false", !gauge.CanSs());

        // 3회 나이스샷 후 2회 사용
        gauge.OnNiceShot();
        gauge.OnNiceShot();
        gauge.OnNiceShot();
        Test("3회 누적 → Points = 3", gauge.Points == 3);
        gauge.SpendSs();
        Test("한 번 사용 → Points = 1", gauge.Points == 1);
        // 1포인트인데 CanSs() 확인 (비용이 2이므로 false)
        Test("1포인트: CanSs = false", !gauge.CanSs());
        // SpendSs() 호출하면 Max(0, 1-2) = 0
        gauge.SpendSs();
        Test("SpendSs 호출 후 → Points = 0", gauge.Points == 0);
    }

    static void TestAiJudge()
    {
        Console.WriteLine("### 3. AiJudge — 난이도별 나이스샷 확률");

        const int trials = 10000;
        var errors = new (float ratio, string name)[]
        {
            (AiGunner.ErrorNovice, "초보(Novice)"),
            (AiGunner.ErrorNormal, "보통(Normal)"),
            (AiGunner.ErrorExpert, "전문가(Expert)"),
            (AiGunner.ErrorAce, "에이스(Ace)"),
        };

        var results = new float[errors.Length];
        for (int i = 0; i < errors.Length; i++)
        {
            int success = 0;
            var rng = new Rng(0x12345678u);   // 고정 시드 — 재현성
            for (int t = 0; t < trials; t++)
            {
                if (NiceShot.AiJudge(errors[i].ratio, ref rng))
                    success++;
            }
            results[i] = success / (float)trials;
            Console.WriteLine($"  {errors[i].name,-18} 성공률 {results[i] * 100:F1}%");
        }

        // 네거티브 컨트롤: 난이도별 단조 증가 (초보 < 보통 < 전문가 < 에이스)
        Test("초보 > 0%", results[0] > 0f);
        Test("초보 < 70%", results[0] < 0.7f);  // 약 50%
        Test("에이스 > 80%", results[3] > 0.8f);  // 약 99%
        Test("에이스 > 초보", results[3] > results[0]);
        Test("에이스 > 보통", results[3] > results[1]);
        Test("에이스 > 전문가", results[3] > results[2]);
        Test("단조성: 초보 < 보통", results[0] < results[1]);
        Test("단조성: 보통 < 전문가", results[1] < results[2]);
        Test("단조성: 전문가 < 에이스", results[2] < results[3]);

        // 결정론: 같은 시드 두 번 실행 → 동일 결과
        var testRng1 = new Rng(0xABCDEFu);
        var testRng2 = new Rng(0xABCDEFu);
        bool result1 = NiceShot.AiJudge(AiGunner.ErrorNormal, ref testRng1);
        bool result2 = NiceShot.AiJudge(AiGunner.ErrorNormal, ref testRng2);
        Test("결정론: 같은 시드 = 같은 결과", result1 == result2);
    }

    static void TestApplySs()
    {
        Console.WriteLine("### 4. ApplySs — SS 강화 적용");

        // 기준: 캐롯탱크 특수탄
        var normal = TankStats.Get(TankKind.Carrot);
        var special = normal.WithShell(ShellKind.Special);
        var ss = NiceShot.ApplySs(special);

        // SS 이후 피해가 증가했는가
        Test("피해 증가: BaseDamage", ss.BaseDamage > special.BaseDamage);
        Test("피해 증가: DirectDamage", ss.DirectDamage > special.DirectDamage);
        Test("폭발 증가: BlastRadius", ss.BlastRadius > special.BlastRadius);

        // 비율 확인 (약 1.6배 피해, 1.3배 폭발)
        float dmgRatio = ss.BaseDamage / special.BaseDamage;
        float blastRatio = ss.BlastRadius / special.BlastRadius;
        Test($"피해 배율 약 1.6배 (실제 {dmgRatio:F2})", dmgRatio > 1.5f && dmgRatio < 1.7f);
        Test($"폭발 배율 약 1.3배 (실제 {blastRatio:F2})", blastRatio > 1.2f && blastRatio < 1.4f);

        // Name 변경
        Test("Name에 [SS] 추가", ss.Name.Contains("[SS]"));

        // 다른 탱크로도 확인 (예: 캐논)
        var cannon = TankStats.Get(TankKind.Cannon);
        var cannonSp = cannon.WithShell(ShellKind.Special);
        var cannonSs = NiceShot.ApplySs(cannonSp);
        Test("캐논도 SS 적용 가능", cannonSs.BaseDamage > cannonSp.BaseDamage);
    }
}
