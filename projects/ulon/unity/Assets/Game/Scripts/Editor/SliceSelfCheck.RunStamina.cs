using System;
using System.IO;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 기획 §18.2. 스태미나 0이면 달리기 불가. NC: NcOpen 이면 0이어도 달린다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertRunStamina()
        {
            if (RunStamina.NcOpen)
                throw new InvalidOperationException("RunStamina.NcOpen 가 켜져 있으면 스태미나 0이어도 달립니다.");
            if (!RunStamina.CanRun(1f))
                throw new InvalidOperationException("스태미나가 있으면 달릴 수 있어야 합니다.");
            if (RunStamina.CanRun(0f))
                throw new InvalidOperationException("스태미나 0이면 달리면 안 됩니다.");
            if (StatSet.MaxStaminaOf(25) != 35 || StatSet.MaxStaminaOf(40) != 50)
                throw new InvalidOperationException("MaxStamina=10+DEX 이어야 합니다.");

            string path = DataLedger.PathOf(RunStamina.FileName);
            if (!File.Exists(path))
                throw new InvalidOperationException("스태미나 원장이 없습니다: " + path);
            RunStamina.Reload();
            if (Math.Abs(RunStamina.FileDrainPerSecond - 4f) > 0.01f)
                throw new InvalidOperationException("달리기 소모 원장이 4/s가 아닙니다: " + RunStamina.FileDrainPerSecond);
            if (Math.Abs(RunStamina.FileRegenPerSecond - 2f) > 0.01f)
                throw new InvalidOperationException("회복 원장이 2/s가 아닙니다: " + RunStamina.FileRegenPerSecond);

            string motorPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/ClickMotor.cs");
            string motor = File.ReadAllText(motorPath);
            if (motor.IndexOf("RunStamina.CanRun", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("ClickMotor가 스태미나 달리기 원장을 안 탑니다.");
            if (motor.IndexOf("TickStamina", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("ClickMotor가 스태미나 틱을 안 탑니다.");

            string movePath = Path.Combine(Application.dataPath, "Game/Scripts/Client/NetAvatar.Move.cs");
            string move = File.ReadAllText(movePath);
            if (move.IndexOf("RunStamina.CanRun", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("이동 RPC가 스태미나 달리기를 서버에서 안 막습니다.");

            string hud = HudSourceText();
            if (hud.IndexOf("Bar(\"ST\"", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD에 ST 바가 없습니다.");
            if (hud.IndexOf("기진", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD에 기진 표시가 없습니다.");

            var go = new GameObject("selfcheck-run-stamina");
            try
            {
                var body = go.AddComponent<WorldBody>();
                body.RecalcFromDex(25);
                if (Math.Abs(body.MaxStamina - 35f) > 0.01f)
                    throw new InvalidOperationException("RecalcFromDex MaxStamina가 35가 아닙니다: " + body.MaxStamina);
                body.SetStamina(4f);
                body.TickStamina(true, 1f);
                if (body.Stamina > 0.05f)
                    throw new InvalidOperationException("1초 달리면 스태미나 4가 바닥나야 합니다: " + body.Stamina);
                if (RunStamina.CanRun(body.Stamina))
                    throw new InvalidOperationException("바닥난 뒤 CanRun 이 거짓이어야 합니다.");
                body.SetStamina(0f);
                body.TickStamina(false, 1f);
                if (Math.Abs(body.Stamina - 2f) > 0.05f)
                    throw new InvalidOperationException("1초 정지면 2가 회복되어야 합니다: " + body.Stamina);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            Debug.Log("[Ulon] 달리기 스태미나 — 0이면 불가 · 모터/RPC RunStamina · HUD ST");
        }

        static void AssertRunStaminaNegativeControl()
        {
            bool was = RunStamina.NcOpen;
            bool red = false;
            try
            {
                RunStamina.NcOpen = true;
                try { AssertRunStamina(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { RunStamina.NcOpen = was; }
            if (!red)
                throw new InvalidOperationException("스태미나 달리기 네거티브 컨트롤 실패 — NcOpen 인데 통과했습니다.");
            Debug.Log("[Ulon] 스태미나 달리기 네거티브 컨트롤 통과 — NcOpen 이면 FAIL");
        }
    }
}
