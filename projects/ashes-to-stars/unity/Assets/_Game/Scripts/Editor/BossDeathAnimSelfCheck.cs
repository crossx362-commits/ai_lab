using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 클로드가 그리는 보스 death 4장을 전투가 실제로 읽는지 검사한다.
    /// PNG는 이 검사가 고치지 않는다 — 빈 자리만 말한다.
    /// </summary>
    public static class BossDeathAnimSelfCheck
    {
        [MenuItem("Ashes to Stars/QA/Boss Death Anim Self Check")]
        public static void Run()
        {
            int fail = 0;
            void Check(bool cond, string what)
            {
                if (!cond)
                {
                    fail++;
                    Debug.LogError("[BossDeathAnimSelfCheck] FAIL  " + what);
                }
            }

            var bank = SpriteBank.Load();
            Check(bank != null, "SpriteBank를 못 읽었다");
            string[] dirs = { "boss_brute", "boss_serpent", "boss_wraith", "boss_construct" };
            string[] frames = { "death_00", "death_01", "death_02", "death_03" };
            for (int k = 0; k < dirs.Length; k++)
            {
                for (int f = 0; f < frames.Length; f++)
                {
                    string path = $"sprites/{dirs[k]}/{dirs[k]}_{frames[f]}";
                    var tex = Resources.Load<Texture2D>(path);
                    Check(tex != null, $"{path} 없음");
                    if (tex == null) continue;
                    Check(tex.width >= 8 && tex.height >= 8, $"{path} 너무 작다 {tex.width}x{tex.height}");
                }
                var a = bank.BossAnim(k, SpriteBank.Motion.Death, 0f);
                var b = bank.BossAnim(k, SpriteBank.Motion.Death, 0.40f);
                Check(a != null, $"{dirs[k]} death 첫 장 없음");
                Check(b != null, $"{dirs[k]} death 끝 장 없음");
                Check(a != null && b != null && a.texture != b.texture,
                    $"{dirs[k]} death가 한 장만 반복된다");
            }

            if (fail == 0) Debug.Log("[BossDeathAnimSelfCheck] PASS");
            else Debug.LogError($"[BossDeathAnimSelfCheck] FAIL {fail}");
        }
    }
}
