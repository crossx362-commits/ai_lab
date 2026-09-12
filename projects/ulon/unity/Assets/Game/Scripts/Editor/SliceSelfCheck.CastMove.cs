using System;
using System.IO;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 기획 §18.6. 시전 중 이동하면 interruptible 주문이 끊긴다.
    /// NC: NcOpen 이면 이동해도 시전이 남아 빨간불.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertCastMoveInterrupt()
        {
            if (CastMove.NcOpen)
                throw new InvalidOperationException("CastMove.NcOpen 가 켜져 있으면 이동해도 시전이 안 끊깁니다.");
            if (!CastMove.BreaksOnMove)
                throw new InvalidOperationException("이동 시 시전 중단이 꺼져 있습니다.");

            string mageryPath = Path.Combine(Application.dataPath, "Game/Scripts/Server/OfflineWorld.Magery.cs");
            string magery = File.ReadAllText(mageryPath);
            if (magery.IndexOf("TryInterruptCastByMove", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("OfflineWorld에 TryInterruptCastByMove가 없습니다.");

            string motorPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/ClickMotor.cs");
            string motor = File.ReadAllText(motorPath);
            if (motor.IndexOf("TryInterruptCastByMove", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("ClickMotor가 이동 시전 중단을 안 탑니다.");

            string movePath = Path.Combine(Application.dataPath, "Game/Scripts/Client/NetAvatar.Move.cs");
            string move = File.ReadAllText(movePath);
            if (move.IndexOf("TryInterruptCastByMove", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("이동 RPC가 시전 중단을 서버에서 안 합니다.");

            string hud = HudSourceText();
            if (hud.IndexOf("시전 중", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD에 시전 중 표시가 없습니다.");

            OfflineWorld.Instance?.ResetHousePlot();

            var worldGo = new GameObject("selfcheck-cast-move-world");
            GameObject casterGo = null;
            GameObject tgtGo = null;
            try
            {
                var world = OfflineWorld.Instance ?? worldGo.AddComponent<OfflineWorld>();
                world.ResetHousePlot();

                casterGo = new GameObject("selfcheck-cast-move-caster");
                casterGo.transform.position = new Vector3(42f, 0f, 42f);
                var caster = casterGo.AddComponent<WorldBody>();
                caster.IsAvatar = true;
                caster.IsEnemy = false;
                caster.CharacterId = "cast-move-caster";
                caster.MaxHp = 80f;
                caster.ResetHp();
                world.StatsOf(caster).ForceSet(40, 20, 40);
                caster.RecalcFromInt(40);
                caster.SetMana(caster.MaxMana);
                var bag = casterGo.AddComponent<InventoryBag>();
                bag.Add(SpellCast.Reagent, 4);
                world.BookOf(caster).Learn(SpellId.Bolt);

                tgtGo = new GameObject("selfcheck-cast-move-tgt");
                tgtGo.transform.position = casterGo.transform.position + new Vector3(6f, 0f, 0f);
                var tgt = tgtGo.AddComponent<WorldBody>();
                tgt.IsEnemy = true;
                tgt.MaxHp = 100f;
                tgt.ResetHp();

                float hp0 = tgt.Hp;
                var start = world.TryCast(caster, SpellId.Bolt, tgt);
                if (!start.Applied || !caster.IsCasting(Time.time))
                    throw new InvalidOperationException("이동 중단 테스트: 벼락 풍업 시작 실패: " + start.FailReason);

                bool still = world.TryInterruptCastByMove(caster);
                if (!still)
                    throw new InvalidOperationException("시전 중 이동이 주문을 끊어야 합니다.");
                if (caster.IsCasting(Time.time))
                    throw new InvalidOperationException("이동 후 CastingUntil이 취소되어야 합니다.");

                world.TickCast(Time.time + SpellCast.BoltCastSeconds + 0.1f);
                if (tgt.Hp != hp0)
                    throw new InvalidOperationException("이동으로 끊긴 벼락은 효과를 내면 안 됩니다.");

                caster.SetMana(caster.MaxMana);
                bag.Add(SpellCast.Reagent, 2);
                var start2 = world.TryCast(caster, SpellId.Bolt, tgt);
                if (!start2.Applied || !caster.IsCasting(Time.time))
                    throw new InvalidOperationException("제자리 경로: 벼락 풍업 시작 실패: " + start2.FailReason);
                world.TickCast(Time.time + SpellCast.BoltCastSeconds + 0.1f);
                if (tgt.Hp >= hp0)
                    throw new InvalidOperationException("제자리면 벼락이 맞아야 합니다.");

                world.ResetHousePlot();
            }
            finally
            {
                if (casterGo != null)
                    UnityEngine.Object.DestroyImmediate(casterGo);
                if (tgtGo != null)
                    UnityEngine.Object.DestroyImmediate(tgtGo);
                if (worldGo != null)
                    UnityEngine.Object.DestroyImmediate(worldGo);
            }

            Debug.Log("[Ulon] 시전 이동 중단 — 벼락 이동 시 취소 · 제자리 완료 · HUD 시전 중");
        }

        static void AssertCastMoveInterruptNegativeControl()
        {
            bool was = CastMove.NcOpen;
            bool red = false;
            try
            {
                CastMove.NcOpen = true;
                try { AssertCastMoveInterrupt(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { CastMove.NcOpen = was; }
            if (!red)
                throw new InvalidOperationException("시전 이동 네거티브 컨트롤 실패 — NcOpen 인데 통과했습니다.");
            Debug.Log("[Ulon] 시전 이동 네거티브 컨트롤 통과 — NcOpen 이면 FAIL");
        }
    }
}
