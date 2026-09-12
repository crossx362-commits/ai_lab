using System;
using System.IO;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 기획 §18.2. HP 자연 회복. NC: NcOff 이면 회복이 없어 빨간불.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertHpRegen()
        {
            if (HpRegen.NcOff)
                throw new InvalidOperationException("HpRegen.NcOff 가 켜져 있으면 HP가 안 찹니다.");

            string path = DataLedger.PathOf(HpRegen.FileName);
            if (!File.Exists(path))
                throw new InvalidOperationException("HP 회복 원장이 없습니다: " + path);
            HpRegen.Reload();
            if (Math.Abs(HpRegen.FileBasePerSecond - 1f) > 0.01f)
                throw new InvalidOperationException("HP 기본 회복 원장이 1/s가 아닙니다: " + HpRegen.FileBasePerSecond);
            if (Math.Abs(HpRegen.FileStrDiv - 50f) > 0.01f)
                throw new InvalidOperationException("STR 나눔이 50이 아닙니다: " + HpRegen.FileStrDiv);

            var stats = new StatSet();
            float baseRate = HpRegen.PerSecond(stats);
            float expected = 1f + StatSet.DefaultStr / 50f;
            if (Math.Abs(baseRate - expected) > 0.01f)
                throw new InvalidOperationException("기본 STR" + StatSet.DefaultStr + " 회복이 " + expected + "/s가 아닙니다: " + baseRate);

            stats.ForceSet(80, 25, 25);
            float high = HpRegen.PerSecond(stats);
            if (high <= baseRate)
                throw new InvalidOperationException("STR이 높으면 HP 회복이 빨라야 합니다: " + high + " vs " + baseRate);
            if (Math.Abs(high - (1f + 80f / 50f)) > 0.01f)
                throw new InvalidOperationException("STR80 회복이 2.6/s가 아닙니다: " + high);

            string motorPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/ClickMotor.cs");
            string motor = File.ReadAllText(motorPath);
            if (motor.IndexOf("TickHp", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("ClickMotor가 HP 틱을 안 탑니다.");

            string hud = HudSourceText();
            if (hud.IndexOf("Bar(\"HP\"", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD에 HP 바가 없습니다.");

            var go = new GameObject("selfcheck-hp-regen");
            try
            {
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                body.RecalcFromStr(30);
                body.ResetHp();
                body.SetHp(10f);
                float rate = HpRegen.PerSecond(new StatSet());
                body.TickHp(rate, 1f);
                if (Math.Abs(body.Hp - (10f + rate)) > 0.05f)
                    throw new InvalidOperationException("1초 회복이 " + (10f + rate) + " 여야 합니다: " + body.Hp);

                body.SetHp(body.MaxHp);
                body.TickHp(rate, 1f);
                if (body.Hp > body.MaxHp + 0.01f)
                    throw new InvalidOperationException("최대 HP를 넘으면 안 됩니다: " + body.Hp);

                body.SetHp(8f);
                body.Ghost = true;
                body.TickHp(rate, 1f);
                if (Math.Abs(body.Hp - 8f) > 0.01f)
                    throw new InvalidOperationException("유령은 HP가 차면 안 됩니다: " + body.Hp);

                body.Ghost = false;
                body.IsAvatar = false;
                body.SetHp(0f);
                body.TickHp(rate, 1f);
                if (body.Hp > 0.01f)
                    throw new InvalidOperationException("시체는 HP가 차면 안 됩니다: " + body.Hp);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            Debug.Log("[Ulon] HP 자연 회복 — STR · 모터 TickHp · HUD HP · 유령/시체 제외");
        }

        static void AssertHpRegenNegativeControl()
        {
            bool was = HpRegen.NcOff;
            bool red = false;
            try
            {
                HpRegen.NcOff = true;
                try { AssertHpRegen(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { HpRegen.NcOff = was; }
            if (!red)
                throw new InvalidOperationException("HP 회복 네거티브 컨트롤 실패 — NcOff 인데 통과했습니다.");
            Debug.Log("[Ulon] HP 회복 네거티브 컨트롤 통과 — NcOff 이면 FAIL");
        }
    }
}
