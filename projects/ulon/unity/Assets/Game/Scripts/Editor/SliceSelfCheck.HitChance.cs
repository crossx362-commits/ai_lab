using System;
using System.IO;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 기획 §18.3. 명중은 무기 스킬. NC: NcAlwaysHit 이면 빗나감이 없다.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertHitChance()
        {
            if (HitChance.NcAlwaysHit)
                throw new InvalidOperationException("HitChance.NcAlwaysHit 가 켜져 있으면 빗나감이 없습니다.");

            string path = DataLedger.PathOf(HitChance.FileName);
            if (!File.Exists(path))
                throw new InvalidOperationException("명중 원장이 없습니다: " + path);
            HitChance.Reload();
            if (Math.Abs(HitChance.FileOffset - 20f) > 0.01f)
                throw new InvalidOperationException("명중 offset이 20이 아닙니다: " + HitChance.FileOffset);
            if (Math.Abs(HitChance.FileMin) > 0.01f)
                throw new InvalidOperationException("명중 하한이 0이 아닙니다: " + HitChance.FileMin);
            if (Math.Abs(HitChance.FileMax - 1f) > 0.01f)
                throw new InvalidOperationException("명중 상한이 1이 아닙니다: " + HitChance.FileMax);

            float even = HitChance.Percent(0f, 0f);
            if (Math.Abs(even - 0.5f) > 0.01f)
                throw new InvalidOperationException("동일 스킬 명중이 50%가 아닙니다: " + even);
            float low = HitChance.Percent(0f, 100f);
            float high = HitChance.Percent(100f, 0f);
            if (low >= even)
                throw new InvalidOperationException("방어 스킬이 높으면 명중이 낮아야 합니다: " + low + " vs " + even);
            if (high <= even)
                throw new InvalidOperationException("공격 스킬이 높으면 명중이 높아야 합니다: " + high + " vs " + even);
            if (Math.Abs(low - (20f / 240f)) > 0.01f)
                throw new InvalidOperationException("0 vs 100 명중이 20/240이 아닙니다: " + low);

            var tacticsOnly = new SkillSet();
            var tacticVals = new float[(int)SkillId.Count];
            tacticVals[(int)SkillId.Tactics] = 100f;
            tacticsOnly.ApplyNetworkValues(tacticVals);
            if (HitChance.DefendSkill(tacticsOnly) > 0.01f)
                throw new InvalidOperationException("전술은 명중 방어 스킬이 아닙니다: " + HitChance.DefendSkill(tacticsOnly));
            var swordOnly = new SkillSet();
            var swordVals = new float[(int)SkillId.Count];
            swordVals[(int)SkillId.Swordsmanship] = 80f;
            swordOnly.ApplyNetworkValues(swordVals);
            if (Math.Abs(HitChance.DefendSkill(swordOnly) - 80f) > 0.01f)
                throw new InvalidOperationException("검술 80이 방어 스킬이어야 합니다: " + HitChance.DefendSkill(swordOnly));
            if (!HitChance.Hits(0f, 0f, 0f))
                throw new InvalidOperationException("굴림 0은 명중이어야 합니다.");
            if (HitChance.Hits(0f, 0f, 0.99f))
                throw new InvalidOperationException("굴림 0.99는 50%에서 빗나가야 합니다.");

            string resolvePath = Path.Combine(Application.dataPath, "Game/Scripts/Shared/AttackResolve.cs");
            string resolve = File.ReadAllText(resolvePath);
            if (resolve.IndexOf("HitChance.Hits", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("AttackResolve가 HitChance를 안 탑니다.");

            string combatPath = Path.Combine(Application.dataPath, "Game/Scripts/Server/OfflineWorld.Combat.cs");
            string combat = File.ReadAllText(combatPath);
            if (combat.IndexOf("HitChance.NextRoll", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("TryAttack이 HitChance.NextRoll을 안 탑니다.");

            string hud = HudSourceText();
            if (hud.IndexOf("HitChance.Percent", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD에 명중 확률이 없습니다.");
            if (hud.IndexOf("LastCombatMessage", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("HUD가 빗나감 안내를 안 읽습니다.");

            float saved = HitChance.ForcedRoll;
            var worldGo = new GameObject("selfcheck-hit-chance-world");
            var atkGo = new GameObject("selfcheck-hit-chance-atk");
            var missGo = new GameObject("selfcheck-hit-chance-misser");
            var defGo = new GameObject("selfcheck-hit-chance-def");
            try
            {
                var world = worldGo.AddComponent<OfflineWorld>();
                var atk = atkGo.AddComponent<WorldBody>();
                var misser = missGo.AddComponent<WorldBody>();
                var def = defGo.AddComponent<WorldBody>();
                atk.IsAvatar = true;
                misser.IsAvatar = true;
                def.IsEnemy = true;
                def.MaxHp = 400f;
                def.SetHp(400f);
                atkGo.transform.position = Vector3.zero;
                missGo.transform.position = Vector3.zero;
                defGo.transform.position = new Vector3(1f, 0f, 0f);

                HitChance.ForcedRoll = 0f;
                AttackResult hit = world.TryAttack(atk, def);
                if (!hit.Applied || !hit.Hit || hit.Damage <= 0)
                    throw new InvalidOperationException("굴림 0은 맞아야 합니다: hit=" + hit.Hit + " dmg=" + hit.Damage + " " + hit.FailReason);
                if (Math.Abs(def.Hp - (400f - hit.Damage)) > 0.01f)
                    throw new InvalidOperationException("명중 후 HP가 안 깎입니다: " + def.Hp);

                def.SetHp(400f);
                HitChance.ForcedRoll = 0.99f;
                AttackResult miss = world.TryAttack(misser, def);
                if (!miss.Applied || miss.Hit || miss.Damage != 0 || miss.FailReason != "miss")
                    throw new InvalidOperationException("굴림 0.99는 빗나가야 합니다: hit=" + miss.Hit + " dmg=" + miss.Damage + " " + miss.FailReason);
                if (Math.Abs(def.Hp - 400f) > 0.01f)
                    throw new InvalidOperationException("빗나감은 HP를 안 깎아야 합니다: " + def.Hp);
                if (world.LastCombatMessage.IndexOf("빗나감", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("빗나감 안내가 없습니다: " + world.LastCombatMessage);
            }
            finally
            {
                HitChance.ForcedRoll = saved;
                UnityEngine.Object.DestroyImmediate(worldGo);
                UnityEngine.Object.DestroyImmediate(atkGo);
                UnityEngine.Object.DestroyImmediate(missGo);
                UnityEngine.Object.DestroyImmediate(defGo);
            }

            Debug.Log("[Ulon] 명중 — 무기 스킬 · TryAttack HitChance · HUD · 빗나감 HP 유지");
        }

        static void AssertHitChanceNegativeControl()
        {
            bool was = HitChance.NcAlwaysHit;
            bool red = false;
            try
            {
                HitChance.NcAlwaysHit = true;
                try { AssertHitChance(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { HitChance.NcAlwaysHit = was; }
            if (!red)
                throw new InvalidOperationException("명중 네거티브 컨트롤 실패 — NcAlwaysHit 인데 통과했습니다.");
            Debug.Log("[Ulon] 명중 네거티브 컨트롤 통과 — NcAlwaysHit 이면 FAIL");
        }
    }
}
