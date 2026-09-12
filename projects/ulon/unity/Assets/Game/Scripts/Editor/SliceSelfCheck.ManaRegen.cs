using System;
using System.IO;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 기획 §18.6. 마나 자연 회복. NC: NcOff 이면 회복이 없어 빨간불.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertManaRegen()
        {
            if (ManaRegen.NcOff)
                throw new InvalidOperationException("ManaRegen.NcOff 가 켜져 있으면 마나가 안 찹니다.");

            string path = DataLedger.PathOf(ManaRegen.FileName);
            if (!File.Exists(path))
                throw new InvalidOperationException("마나 회복 원장이 없습니다: " + path);
            ManaRegen.Reload();
            if (Math.Abs(ManaRegen.FileBasePerSecond - 1f) > 0.01f)
                throw new InvalidOperationException("마나 기본 회복 원장이 1/s가 아닙니다: " + ManaRegen.FileBasePerSecond);
            if (Math.Abs(ManaRegen.FileMeditationDiv - 100f) > 0.01f)
                throw new InvalidOperationException("명상 나눔이 100이 아닙니다: " + ManaRegen.FileMeditationDiv);
            if (Math.Abs(ManaRegen.FileIntDiv - 50f) > 0.01f)
                throw new InvalidOperationException("INT 나눔이 50이 아닙니다: " + ManaRegen.FileIntDiv);

            var skills = new SkillSet();
            var stats = new StatSet();
            float light = ManaRegen.PerSecond(skills, stats, false);
            float heavy = ManaRegen.PerSecond(skills, stats, true);
            float expectedLight = 1f + 25f / 50f;
            if (Math.Abs(light - expectedLight) > 0.01f)
                throw new InvalidOperationException("기본 INT25 회복이 1.5/s가 아닙니다: " + light);
            if (Math.Abs(heavy - expectedLight * MeditationResolve.HeavyMul) > 0.01f)
                throw new InvalidOperationException("중갑 회복이 절반이 아닙니다: " + heavy);
            if (heavy >= light)
                throw new InvalidOperationException("중갑 마나 회복은 무갑보다 낮아야 합니다.");

            skills.ForceSet(SkillId.Meditation, 100f, SkillLock.Up);
            float high = ManaRegen.PerSecond(skills, stats, false);
            if (Math.Abs(high - (expectedLight + 1f)) > 0.01f)
                throw new InvalidOperationException("명상 100이면 기본보다 1/s 빨라야 합니다: " + high);

            string motorPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/ClickMotor.cs");
            string motor = File.ReadAllText(motorPath);
            if (motor.IndexOf("TickMana", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("ClickMotor가 마나 틱을 안 탑니다.");

            string hud = HudSourceText();
            if (hud.IndexOf("Bar(\"MP\"", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD에 MP 바가 없습니다.");
            if (hud.IndexOf("중갑", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD에 중갑 마나 표시가 없습니다.");

            var go = new GameObject("selfcheck-mana-regen");
            try
            {
                var body = go.AddComponent<WorldBody>();
                body.RecalcFromInt(25);
                body.SetMana(0f);
                float rate = ManaRegen.PerSecond(new SkillSet(), new StatSet(), false);
                body.TickMana(rate, 1f);
                if (Math.Abs(body.Mana - rate) > 0.05f)
                    throw new InvalidOperationException("1초 무갑 회복이 " + rate + " 여야 합니다: " + body.Mana);
                body.Ghost = true;
                body.SetMana(0f);
                body.TickMana(rate, 1f);
                if (body.Mana > 0.01f)
                    throw new InvalidOperationException("유령은 마나가 차면 안 됩니다: " + body.Mana);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            Debug.Log("[Ulon] 마나 자연 회복 — 명상/INT · 중갑 절반 · 모터 TickMana · HUD MP");
        }

        static void AssertManaRegenNegativeControl()
        {
            bool was = ManaRegen.NcOff;
            bool red = false;
            try
            {
                ManaRegen.NcOff = true;
                try { AssertManaRegen(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { ManaRegen.NcOff = was; }
            if (!red)
                throw new InvalidOperationException("마나 회복 네거티브 컨트롤 실패 — NcOff 인데 통과했습니다.");
            Debug.Log("[Ulon] 마나 회복 네거티브 컨트롤 통과 — NcOff 이면 FAIL");
        }
    }
}
