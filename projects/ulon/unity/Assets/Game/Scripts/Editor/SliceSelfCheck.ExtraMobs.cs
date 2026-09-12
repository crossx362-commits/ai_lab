using System;
using System.Collections.Generic;
using FishNet.Object;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        static void AssertExtraMobCatalog()
        {
            MobData.Reload();
            if (MobData.Count != 20)
                throw new InvalidOperationException("몬스터 수치 원장은 20종이어야 합니다(사냥 8+추가 6+보스 4+조련 2): " + MobData.Count);
            if (MobCatalog.HostileKindCount != 8)
                throw new InvalidOperationException("사냥터 잡몹 종수는 8로 유지해야 합니다: " + MobCatalog.HostileKindCount);
            if (ExtraMobRoster.Spots.Length != 6)
                throw new InvalidOperationException("추가 잡몹 자리 원장은 6종이어야 합니다: " + ExtraMobRoster.Spots.Length);

            var missing = new List<string>();
            var spots = ExtraMobRoster.Spots;
            for (int i = 0; i < spots.Length; i++)
            {
                if (!MobCatalog.TryGet(spots[i].MobId, out MobDefinition def))
                    throw new InvalidOperationException("추가 몹 카탈로그에 " + spots[i].MobId + "가 없습니다.");
                if (MobCatalog.IsBoss(spots[i].MobId) || MobCatalog.TamableOf(spots[i].MobId))
                    throw new InvalidOperationException(spots[i].MobId + "는 잡몹이어야 합니다(보스/조련 아님).");
                var go = GameObject.Find(spots[i].Name);
                var body = go != null ? go.GetComponent<WorldBody>() : null;
                if (body == null || body.MobId != spots[i].MobId || !body.IsEnemy)
                    missing.Add(spots[i].Name + "(씬/MobId)");
                else if (Math.Abs(body.MaxHp - def.MaxHp) > 0.0001f || body.DisplayName != def.DisplayName)
                    missing.Add(spots[i].Name + "(HP/이름 " + body.DisplayName + " " + body.MaxHp + ")");
                else if (go.GetComponent<NetworkObject>() == null || go.GetComponent<NetMob>() == null)
                    missing.Add(spots[i].Name + "(NetMob)");
                else if (GuardZone.Contains(go.transform.position.x, go.transform.position.z))
                    missing.Add(spots[i].Name + "(가드존 안)");
                else
                {
                    Vector2 want = ExtraMobRoster.World(i);
                    if (Math.Abs(go.transform.position.x - want.x) > 0.8f ||
                        Math.Abs(go.transform.position.z - want.y) > 0.8f)
                        missing.Add(spots[i].Name + "(자리)");
                }
            }
            if (missing.Count > 0)
                throw new InvalidOperationException("추가 잡몹 불량 " + missing.Count + "건: " + string.Join(", ", missing));
            Debug.Log("[Ulon] 추가 잡몹 — 원장 20종·씬 6체 가드존 밖(사냥터 8종 유지)");
        }

        static void AssertExtraMobCatalogNegativeControl()
        {
            var go = GameObject.Find(ExtraMobRoster.Spots[0].Name);
            if (go == null)
                throw new InvalidOperationException("추가 잡몹 NC 대상이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            bool was = go.activeSelf;
            bool red = false;
            try
            {
                go.SetActive(false);
                try { AssertExtraMobCatalog(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { go.SetActive(was); }
            if (!red)
                throw new InvalidOperationException("추가 잡몹 네거티브 컨트롤 실패 — 한 체를 지웠는데 통과했습니다.");
            Debug.Log("[Ulon] 추가 잡몹 네거티브 컨트롤 통과 — " + ExtraMobRoster.Spots[0].Name + "를 끄면 FAIL");
        }
    }
}
