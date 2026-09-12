using System;
using System.IO;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 기획 §18.2. 휘두름 간격이 스태미나에 따라 변한다. NC: NcFixed 이면 옛 1.1초 고정.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertAttackSpeed()
        {
            if (AttackSpeed.NcFixed)
                throw new InvalidOperationException("AttackSpeed.NcFixed 가 켜져 있으면 스태미나가 간격을 안 바꿉니다.");

            string path = DataLedger.PathOf(AttackSpeed.FileName);
            if (!File.Exists(path))
                throw new InvalidOperationException("휘두름 원장이 없습니다: " + path);
            AttackSpeed.Reload();
            if (Math.Abs(AttackSpeed.FileBaseSeconds - 1.8f) > 0.01f)
                throw new InvalidOperationException("기본 간격 원장이 1.8s가 아닙니다: " + AttackSpeed.FileBaseSeconds);
            if (Math.Abs(AttackSpeed.FileStamDiv - 50f) > 0.01f)
                throw new InvalidOperationException("스태미나 나눔이 50이 아닙니다: " + AttackSpeed.FileStamDiv);
            if (Math.Abs(AttackSpeed.FileMinSeconds - 0.5f) > 0.01f)
                throw new InvalidOperationException("하한이 0.5s가 아닙니다: " + AttackSpeed.FileMinSeconds);

            float full = AttackSpeed.Seconds(35f);
            if (Math.Abs(full - 1.1f) > 0.01f)
                throw new InvalidOperationException("기본 만땅 35 간격이 1.1s가 아닙니다: " + full);
            float empty = AttackSpeed.Seconds(0f);
            if (Math.Abs(empty - 1.8f) > 0.01f)
                throw new InvalidOperationException("기진 간격이 1.8s가 아닙니다: " + empty);
            if (empty <= full)
                throw new InvalidOperationException("기진이면 휘두름이 느려야 합니다: " + empty + " vs " + full);
            float high = AttackSpeed.Seconds(90f);
            if (Math.Abs(high - 0.5f) > 0.01f)
                throw new InvalidOperationException("스태미나 90은 하한 0.5s여야 합니다: " + high);
            if (high >= full)
                throw new InvalidOperationException("스태미나가 높으면 휘두름이 빨라야 합니다: " + high + " vs " + full);

            var stats = new StatSet();
            float fromDex = AttackSpeed.Seconds(stats);
            if (Math.Abs(fromDex - 1.1f) > 0.01f)
                throw new InvalidOperationException("기본 DEX 만땅 간격이 1.1s가 아닙니다: " + fromDex);

            string combatPath = Path.Combine(Application.dataPath, "Game/Scripts/Server/OfflineWorld.Combat.cs");
            string combat = File.ReadAllText(combatPath);
            if (combat.IndexOf("AttackSpeed.Seconds", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("TryAttack이 AttackSpeed를 안 탑니다.");

            string stealthPath = Path.Combine(Application.dataPath, "Game/Scripts/Server/OfflineWorld.Stealth.cs");
            string stealth = File.ReadAllText(stealthPath);
            if (stealth.IndexOf("AttackSpeed.Seconds", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("반격 휘두름이 AttackSpeed를 안 탑니다.");

            string hud = HudSourceText();
            if (hud.IndexOf("AttackSpeed.Seconds", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD에 휘두름 간격이 없습니다.");

            var worldGo = new GameObject("selfcheck-attack-speed-world");
            var atkGo = new GameObject("selfcheck-attack-speed-atk");
            var defGo = new GameObject("selfcheck-attack-speed-def");
            try
            {
                var world = worldGo.AddComponent<OfflineWorld>();
                var atk = atkGo.AddComponent<WorldBody>();
                var def = defGo.AddComponent<WorldBody>();
                atk.IsAvatar = true;
                def.IsEnemy = true;
                def.MaxHp = 400f;
                def.SetHp(400f);
                atk.RecalcFromDex(StatSet.DefaultDex);
                atk.SetStamina(35f);
                atkGo.transform.position = Vector3.zero;
                defGo.transform.position = new Vector3(1f, 0f, 0f);

                AttackResult first = world.TryAttack(atk, def);
                if (!first.Applied)
                    throw new InvalidOperationException("첫 휘두름이 적용돼야 합니다: " + first.FailReason);
                AttackResult second = world.TryAttack(atk, def);
                if (second.FailReason != "cooldown")
                    throw new InvalidOperationException("바로 다음 휘두름은 cooldown 이어야 합니다: " + second.FailReason);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(worldGo);
                UnityEngine.Object.DestroyImmediate(atkGo);
                UnityEngine.Object.DestroyImmediate(defGo);
            }

            Debug.Log("[Ulon] 휘두름 간격 — 스태미나 · TryAttack AttackSpeed · HUD · 연속 거부");
        }

        static void AssertAttackSpeedNegativeControl()
        {
            bool was = AttackSpeed.NcFixed;
            bool red = false;
            try
            {
                AttackSpeed.NcFixed = true;
                try { AssertAttackSpeed(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { AttackSpeed.NcFixed = was; }
            if (!red)
                throw new InvalidOperationException("휘두름 간격 네거티브 컨트롤 실패 — NcFixed 인데 통과했습니다.");
            Debug.Log("[Ulon] 휘두름 간격 네거티브 컨트롤 통과 — NcFixed 이면 FAIL");
        }
    }
}
