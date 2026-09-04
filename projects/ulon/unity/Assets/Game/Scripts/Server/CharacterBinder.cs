using System;
using System.Collections.Generic;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public static class CharacterBinder
    {
        public static void Apply(WorldBody body, CharacterSnapshot snap, SkillSet skills, StatSet stats)
        {
            if (body == null || snap == null)
                return;
            if (!string.IsNullOrEmpty(snap.CharacterId))
                body.CharacterId = snap.CharacterId;
            else if (!string.IsNullOrEmpty(snap.AccountId))
                body.CharacterId = snap.AccountId;
            if (!string.IsNullOrEmpty(snap.AccountId))
                body.AccountId = snap.AccountId;
            if (!string.IsNullOrEmpty(snap.Name))
                body.DisplayName = snap.Name;
            body.Appearance = snap.Appearance;
            body.transform.SetPositionAndRotation(
                new Vector3(snap.X, snap.Y, snap.Z),
                body.transform.rotation);
            if (stats != null)
            {
                int s = snap.Str > 0 ? snap.Str : StatSet.DefaultStr;
                int d = snap.Dex > 0 ? snap.Dex : StatSet.DefaultDex;
                int i = snap.Int > 0 ? snap.Int : StatSet.DefaultInt;
                stats.ForceSet(s, d, i, (SkillLock)snap.StrLock, (SkillLock)snap.DexLock, (SkillLock)snap.IntLock);
                body.RecalcFromStr(stats.Str);
                body.RecalcFromInt(stats.Int);
            }
            body.Ghost = snap.Ghost;
            if (snap.Ghost)
                body.SetHp(0f);
            else
            {
                float hp = snap.Hp > 0f ? snap.Hp : body.MaxHp;
                body.SetHp(hp);
            }
            if (snap.Mana > 0f)
                body.SetMana(snap.Mana);
            skills.ReadFrom(snap.Skills);
            if (OfflineWorld.Instance != null)
                OfflineWorld.Instance.BookOf(body).ReadFrom(snap.Spells);
            var bag = body.GetComponent<InventoryBag>() ?? body.gameObject.AddComponent<InventoryBag>();
            bag.Replace(snap.Inventory);
            var vault = body.GetComponent<BankVault>() ?? body.gameObject.AddComponent<BankVault>();
            vault.Replace(snap.Bank);
            body.Gold = snap.Gold;
            body.Fame = snap.Fame;
            body.Karma = snap.Karma;
            body.Notoriety = snap.Notoriety;
            body.MurderCount = snap.MurderCount;
            if (body.Notoriety == NotorietyId.Criminal)
                body.CriminalUntil = Time.time + 120f;
        }

        public static void Apply(WorldBody body, CharacterSnapshot snap, SkillSet skills)
        {
            Apply(body, snap, skills, null);
        }

        public static CharacterSnapshot Capture(string accountId, WorldBody body, SkillSet skills, StatSet stats)
        {
            var list = new List<SkillRecord>();
            skills.WriteTo(list);
            var bag = body != null ? body.GetComponent<InventoryBag>() : null;
            var vault = body != null ? body.GetComponent<BankVault>() : null;
            Vector3 pos = body != null ? body.transform.position : Vector3.zero;
            var book = OfflineWorld.Instance != null && body != null ? OfflineWorld.Instance.BookOf(body) : null;
            var snap = new CharacterSnapshot
            {
                AccountId = accountId,
                CharacterId = body != null && !string.IsNullOrEmpty(body.CharacterId) ? body.CharacterId : accountId,
                Name = body != null ? body.DisplayName : "나",
                X = pos.x,
                Y = pos.y,
                Z = pos.z,
                Hp = body != null ? body.Hp : 50f,
                Mana = body != null ? body.Mana : 0f,
                Ghost = body != null && body.Ghost,
                Str = stats != null ? stats.Str : StatSet.DefaultStr,
                Dex = stats != null ? stats.Dex : StatSet.DefaultDex,
                Int = stats != null ? stats.Int : StatSet.DefaultInt,
                StrLock = stats != null ? (int)stats.GetLock(StatId.Str) : 0,
                DexLock = stats != null ? (int)stats.GetLock(StatId.Dex) : 0,
                IntLock = stats != null ? (int)stats.GetLock(StatId.Int) : 0,
                Skills = list.ToArray(),
                Inventory = bag != null ? bag.ToArray() : Array.Empty<ItemRecord>(),
                Bank = vault != null ? vault.ToArray() : Array.Empty<ItemRecord>(),
                Appearance = body != null ? body.Appearance : 0,
                Spells = book != null ? book.ToArray() : Array.Empty<int>(),
                Gold = body != null ? body.Gold : 0,
                Fame = body != null ? body.Fame : 0,
                Karma = body != null ? body.Karma : 0,
                Notoriety = body != null ? body.Notoriety : 0,
                MurderCount = body != null ? body.MurderCount : 0
            };
            OfflineWorld.WriteCorpse(snap, accountId);
            return snap;
        }

        public static CharacterSnapshot Capture(string accountId, WorldBody body, SkillSet skills)
        {
            return Capture(accountId, body, skills, null);
        }
    }
}
