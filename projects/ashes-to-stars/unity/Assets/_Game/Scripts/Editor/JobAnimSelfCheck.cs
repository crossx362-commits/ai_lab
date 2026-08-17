using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 클로드가 넣는 직업 13장을 전투가 실제로 읽는지 검사한다.
    /// PNG는 이 검사가 고치지 않는다 — 빈 자리만 말한다.
    /// </summary>
    public static class JobAnimSelfCheck
    {
        static readonly string[] Dirs = { "tank", "dps", "healer", "buffer", "mage" };
        static readonly string[] Frames =
        {
            "idle_00", "walk_00", "walk_01", "attack_00", "attack_01",
            "special_00", "hurt_00", "death_00",
            "dash_00", "dash_01", "dash_02", "dash_03", "invuln_00",
        };

        [MenuItem("Ashes to Stars/QA/Job Anim Self Check")]
        public static void Run()
        {
            int fail = 0;
            void Check(bool cond, string what)
            {
                if (!cond)
                {
                    fail++;
                    Debug.LogError("[JobAnimSelfCheck] FAIL  " + what);
                }
            }

            var bank = SpriteBank.Load();
            Check(bank != null, "SpriteBank를 못 읽었다");
            for (int j = 0; j < Dirs.Length; j++)
            {
                for (int f = 0; f < Frames.Length; f++)
                {
                    string path = $"sprites/{Dirs[j]}/{Dirs[j]}_{Frames[f]}";
                    var tex = Resources.Load<Texture2D>(path);
                    Check(tex != null, $"{path} 없음");
                    if (tex == null) continue;
                    Check(tex.width >= 8 && tex.height >= 8,
                        $"{path} 너무 작다 {tex.width}x{tex.height}");
                }
                var job = (SpriteBank.Job)j;
                var idle = bank.CharAnim(job, SpriteBank.Motion.Idle, 0f);
                var walk = bank.CharAnim(job, SpriteBank.Motion.Walk, 0.20f);
                var atk = bank.CharAnim(job, SpriteBank.Motion.Attack, 0.18f);
                var dash = bank.CharAnim(job, SpriteBank.Motion.Dash, 0.12f);
                Check(idle != null, $"{Dirs[j]} idle 없음");
                // 직업 장은 한 아틀라스라 texture는 같다. 칸(rect)이 달라야 다른 프레임이다.
                Check(walk != null && idle != null && walk.rect != idle.rect,
                    $"{Dirs[j]} 걸음이 idle 칸과 같다");
                Check(atk != null && idle != null && atk.rect != idle.rect,
                    $"{Dirs[j]} 공격이 idle 칸과 같다");
                Check(dash != null && idle != null && dash.rect != idle.rect,
                    $"{Dirs[j]} 대시가 idle 칸과 같다");
            }

            if (fail == 0) Debug.Log("[JobAnimSelfCheck] PASS");
            else Debug.LogError($"[JobAnimSelfCheck] FAIL {fail}");
        }
    }
}
