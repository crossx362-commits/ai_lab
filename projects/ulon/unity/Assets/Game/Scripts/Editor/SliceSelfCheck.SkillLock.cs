using System;
using System.IO;
using Ulon.Client;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// 기획 §3.1·§18.13. 잠금은 서버 Try/Rpc. 검프에 합계 700/225. NC: NcOpen 이면 빨간불.
    /// </summary>
    public static partial class SliceSelfCheck
    {
        static void AssertSkillLockAuth()
        {
            if (SkillLockAuth.NcOpen)
                throw new InvalidOperationException("SkillLockAuth.NcOpen 가 켜져 있으면 잠금이 클라에서 돕니다.");

            string hud = HudSourceText();
            if (hud.IndexOf("RpcCycleSkillLock", StringComparison.Ordinal) < 0 ||
                hud.IndexOf("RpcCycleStatLock", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("스킬 검프가 잠금 RPC를 안 탑니다.");
            if (hud.IndexOf("TryCycleSkillLock", StringComparison.Ordinal) < 0 ||
                hud.IndexOf("TryCycleStatLock", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("오프라인 폴백 TryCycle*Lock 이 없습니다.");
            if (hud.IndexOf("SkillSet.TotalCap", StringComparison.Ordinal) < 0 ||
                hud.IndexOf("StatSet.TotalCap", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("스킬 검프에 합계 700/225 표시가 없습니다.");
            if (hud.IndexOf("sk.CycleLock", StringComparison.Ordinal) >= 0 ||
                hud.IndexOf("st.CycleLock", StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("HUD가 잠금을 클라 SkillSet에서 직접 돌립니다.");

            string worldPath = Path.Combine(Application.dataPath, "Game/Scripts/Server/OfflineWorld.Skills.cs");
            if (!File.Exists(worldPath))
                throw new InvalidOperationException("OfflineWorld.Skills.cs가 없습니다.");

            var sk = new SkillSet();
            sk.ForceSet(SkillId.Swordsmanship, 10f, SkillLock.Up);
            sk.CycleLock(SkillId.Swordsmanship);
            if (sk.GetLock(SkillId.Swordsmanship) != SkillLock.Down)
                throw new InvalidOperationException("오프라인 CycleLock ↑→↓ 실패");
            sk.CycleLock(SkillId.Swordsmanship);
            if (sk.GetLock(SkillId.Swordsmanship) != SkillLock.Locked)
                throw new InvalidOperationException("오프라인 CycleLock ↓→고정 실패");
            sk.CycleLock(SkillId.Swordsmanship);
            if (sk.GetLock(SkillId.Swordsmanship) != SkillLock.Up)
                throw new InvalidOperationException("오프라인 CycleLock 고정→↑ 실패");

            var world = OfflineWorld.Instance;
            if (world == null)
                throw new InvalidOperationException("OfflineWorld 없음");
            var go = new GameObject("selfcheck-skill-lock");
            try
            {
                var body = go.AddComponent<WorldBody>();
                body.IsAvatar = true;
                var up = world.TryCycleSkillLock(body, SkillId.Mining);
                if (!up.Applied)
                    throw new InvalidOperationException("서버 TryCycleSkillLock 실패: " + up.FailReason);
                if (world.SkillsOf(body).GetLock(SkillId.Mining) != SkillLock.Down)
                    throw new InvalidOperationException("서버 잠금이 ↓가 아닙니다.");
                var st = world.TryCycleStatLock(body, StatId.Str);
                if (!st.Applied)
                    throw new InvalidOperationException("서버 TryCycleStatLock 실패: " + st.FailReason);
                if (world.StatsOf(body).GetLock(StatId.Str) != SkillLock.Down)
                    throw new InvalidOperationException("STR 잠금이 ↓가 아닙니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }

            string sig = NetAvatar.SkillSignature(sk);
            if (sig.IndexOf(':') < 0)
                throw new InvalidOperationException("스킬 서명이 잠금을 안 싣습니다.");

            Debug.Log("[Ulon] 스킬/스탯 잠금 — 서버 Try/Rpc · 합계 700/225 · 서명 잠금");
        }

        static void AssertSkillLockAuthNegativeControl()
        {
            bool was = SkillLockAuth.NcOpen;
            bool red = false;
            try
            {
                SkillLockAuth.NcOpen = true;
                try { AssertSkillLockAuth(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { SkillLockAuth.NcOpen = was; }
            if (!red)
                throw new InvalidOperationException("스킬 잠금 네거티브 컨트롤 실패 — NcOpen 인데 통과했습니다.");
            Debug.Log("[Ulon] 스킬 잠금 네거티브 컨트롤 통과 — NcOpen 이면 FAIL");
        }
    }
}
