using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 직업 모션이 **시간에 따라 실제로 프레임을 넘기는지** 검사한다.
    ///
    /// JobAnimSelfCheck는 프레임 파일 존재와 「walk ≠ idle」까지만 본다 — 프레임이
    /// 있어도 애니 코드가 같은 장만 돌려주면 화면은 정지 그림이다(2026-08-20 오너
    /// 지적 "탱커 말고 안 움직인다" 계열 의심을 코드 수준에서 못 박는 검사).
    /// 모션별로 t를 촘촘히 훑어 서로 다른 스프라이트 수를 세고 기대치와 대조한다.
    /// </summary>
    public static class MotionCycleSelfCheck
    {
        static readonly string[] Dirs = { "tank", "dps", "healer", "buffer", "mage" };

        [MenuItem("Ashes to Stars/QA/Motion Cycle Self Check")]
        public static void Run()
        {
            int fail = 0;
            void Check(bool cond, string what)
            {
                if (!cond)
                {
                    fail++;
                    Debug.LogError("[MotionCycleSelfCheck] FAIL  " + what);
                }
            }

            var bank = SpriteBank.Load();
            Check(bank != null, "SpriteBank를 못 읽었다");
            if (bank == null) { Debug.LogError("[MotionCycleSelfCheck] FAIL 1"); return; }

            // (모션, 기대 프레임 수). attack/special/dash는 마지막 장 고정이라
            // t 범위 안에서 기대 수만큼 등장한 뒤 멈추는 게 정상이다.
            var wants = new (SpriteBank.Motion m, int n)[]
            {
                (SpriteBank.Motion.Idle, 6),
                (SpriteBank.Motion.Walk, 6),
                (SpriteBank.Motion.Attack, 6),
                (SpriteBank.Motion.Special, 6),
                (SpriteBank.Motion.Dash, 4),
            };

            for (int j = 0; j < Dirs.Length; j++)
            {
                var job = (SpriteBank.Job)j;

                // 직업 장은 한 런타임 아틀라스라 name이 비어 있다 — 프레임의
                // 정체는 칸(rect)이다(JobAnimSelfCheck와 같은 기준).
                foreach (var w in wants)
                {
                    var seen = new HashSet<Rect>();
                    for (float t = 0f; t <= 1.6f; t += 0.01f)
                    {
                        var s = bank.CharAnim(job, w.m, t);
                        if (s == null) continue;
                        seen.Add(s.rect);
                    }
                    Check(seen.Count == w.n,
                        $"{Dirs[j]} {w.m}: 서로 다른 칸 {seen.Count}개 (기대 {w.n})");
                }

                var idle0 = bank.CharAnim(job, SpriteBank.Motion.Idle, 0f);
                var hurt = bank.CharAnim(job, SpriteBank.Motion.Hurt, 0f);
                var death = bank.CharAnim(job, SpriteBank.Motion.Death, 0f);
                Check(hurt != null && idle0 != null && hurt.rect != idle0.rect,
                    $"{Dirs[j]} Hurt가 idle과 같은 칸이다");
                Check(death != null && idle0 != null && death.rect != idle0.rect,
                    $"{Dirs[j]} Death가 idle과 같은 칸이다");
                Check(hurt != null && death != null && hurt.rect != death.rect,
                    $"{Dirs[j]} Hurt와 Death가 같은 칸이다");
            }

            if (fail == 0) Debug.Log("[MotionCycleSelfCheck] PASS");
            else Debug.LogError($"[MotionCycleSelfCheck] FAIL {fail}");
        }
    }
}
