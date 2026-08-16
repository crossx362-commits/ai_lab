using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 합성 — 1차 이상 재료를 소멸시켜 패시브 1개를 흡수한다(§3·§18-7).
    ///
    /// 재료는 영묘에 가지 않는다(자발적 희생). 슬롯 상한 4. 이미 가진 패시브는
    /// 추첨에서 빼고 다시 뽑는다. 골드 2 G/h. 인간은 호스트 역할 계열 +20%p.
    /// 전투 소비처는 출전 계약의 배율(강골=HP, 예리함=공 등) → W3Party가 읽는다.
    /// </summary>
    public static class Fusion
    {
        public const int SlotCap = 4;
        public const string CostKey = "Fusion";
        public const float HumanFamilyBonus = 0.20f;

        public struct CombatMuls
        {
            public float Atk, Hp, Speed, Cd, Heal, Shield, Range, AtkSpd;
            public static CombatMuls Identity => new CombatMuls
            {
                Atk = 1f, Hp = 1f, Speed = 1f, Cd = 1f,
                Heal = 1f, Shield = 1f, Range = 1f, AtkSpd = 1f
            };
        }

        static readonly Dictionary<string, BoonId[]> OffersByJob = new Dictionary<string, BoonId[]>
        {
            ["수호기사"] = new[] { BoonId.강골, BoonId.방벽 },
            ["광전사"] = new[] { BoonId.예리함, BoonId.분노 },
            ["검사"] = new[] { BoonId.예리함, BoonId.숙련 },
            ["궁수"] = new[] { BoonId.집중, BoonId.예리함 },
            ["마법사"] = new[] { BoonId.집중, BoonId.분노 },
            ["소환사"] = new[] { BoonId.방벽, BoonId.숙련 },
            ["사제"] = new[] { BoonId.치유의손, BoonId.강골 },
            ["드루이드"] = new[] { BoonId.치유의손, BoonId.발놀림 },
            ["음유시인"] = new[] { BoonId.발놀림, BoonId.숙련 },
            ["주술사"] = new[] { BoonId.치유의손, BoonId.방벽 },
            ["정령사"] = new[] { BoonId.집중, BoonId.방벽 },
        };

        /// <summary>SelfCheck·QA가 추첨을 고정할 때만. 풀에 없으면 무시한다.</summary>
        public static BoonId? ForcePick;

        /// <summary>SelfCheck가 종족을 고정할 때만. 없으면 계정 종족.</summary>
        public static RaceId? ForceRace;

        static readonly Dictionary<string, BoonId[]> PreferredByRole = new Dictionary<string, BoonId[]>
        {
            ["Tank"] = new[] { BoonId.강골, BoonId.방벽 },
            ["Dps"] = new[] { BoonId.예리함, BoonId.집중, BoonId.분노 },
            ["Healer"] = new[] { BoonId.치유의손, BoonId.강골 },
            ["Buffer"] = new[] { BoonId.숙련, BoonId.발놀림 },
        };

        public static bool CanBeHost(CharacterRecord character) =>
            character != null && !character.IsDeleted && !character.IsSpecialJob;

        public static bool CanBeMaterial(CharacterRecord character) =>
            character != null && !character.IsDeleted && !character.IsSpecialJob
            && character.Advancement != AdvancementTier.Basic;

        public static bool HasMaterial(CharacterRecord except = null)
        {
            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster[i];
                if (except != null && (ReferenceEquals(ch, except) || ch.Id == except.Id)) continue;
                if (CanBeMaterial(ch)) return true;
            }
            return false;
        }

        public static IReadOnlyList<BoonId> OffersOf(CharacterRecord character)
        {
            if (character == null) return Array.Empty<BoonId>();
            return OffersByJob.TryGetValue(character.Job, out var offers)
                ? offers
                : Array.Empty<BoonId>();
        }

        public static List<BoonId> DrawPool(CharacterRecord host, CharacterRecord material)
        {
            var pool = new List<BoonId>();
            if (host == null || material == null) return pool;
            var owned = host.AbsorbedBoons;
            var offers = OffersOf(material);
            for (int i = 0; i < offers.Count; i++)
            {
                var id = offers[i];
                int raw = (int)id;
                if (owned.Contains(raw)) continue;
                if (host.PendingBoon == raw) continue;
                if (!pool.Contains(id)) pool.Add(id);
            }
            return pool;
        }

        public static long CostCopper(int? tier = null) =>
            Economy.GetActionCost(CostKey, tier ?? GameState.Tier);

        public static string RoleFamilyOf(string job) => job switch
        {
            "탱" or "수호기사" or "광전사" => "Tank",
            "힐" or "사제" or "드루이드" => "Healer",
            "버퍼" or "음유시인" or "주술사" or "정령사" => "Buffer",
            _ => "Dps",
        };

        public static List<BoonId> PreferredInPool(CharacterRecord host, IReadOnlyList<BoonId> pool)
        {
            var hit = new List<BoonId>();
            if (host == null || pool == null) return hit;
            if (!PreferredByRole.TryGetValue(RoleFamilyOf(host.Job), out var want)) return hit;
            for (int i = 0; i < pool.Count; i++)
            {
                var id = pool[i];
                for (int j = 0; j < want.Length; j++)
                    if (want[j] == id && !hit.Contains(id)) hit.Add(id);
            }
            return hit;
        }

        static RaceId CurrentRace() => ForceRace ?? RacePrefs.Get();

        /// <summary>풀에서 1개를 고른다. 인간은 호스트 역할 계열에 +20%p(§18-9).</summary>
        public static BoonId Pick(IReadOnlyList<BoonId> pool, CharacterRecord host, uint seed)
        {
            if (pool == null || pool.Count == 0) return default;
            if (ForcePick.HasValue && Contains(pool, ForcePick.Value)) return ForcePick.Value;

            var rng = Rng.Stream(seed, host != null ? host.AbsorbedBoons.Count + 1 : 1, SeedChannel.Fusion);
            var preferred = PreferredInPool(host, pool);
            float bonus = CurrentRace() == RaceId.인간 ? HumanFamilyBonus : 0f;
            if (preferred.Count > 0 && preferred.Count < pool.Count && bonus > 0f)
            {
                float p = Mathf.Min(1f, (float)preferred.Count / pool.Count + bonus);
                if (rng.Chance(p))
                    return preferred[rng.Next(preferred.Count)];
                var rest = new List<BoonId>();
                for (int i = 0; i < pool.Count; i++)
                    if (!preferred.Contains(pool[i])) rest.Add(pool[i]);
                return rest.Count > 0 ? rest[rng.Next(rest.Count)] : pool[rng.Next(pool.Count)];
            }
            return pool[rng.Next(pool.Count)];
        }

        static bool Contains(IReadOnlyList<BoonId> pool, BoonId id)
        {
            for (int i = 0; i < pool.Count; i++) if (pool[i] == id) return true;
            return false;
        }

        public static bool TryFuse(CharacterRecord host, CharacterRecord material, uint seed, out BoonId picked)
        {
            picked = default;
            if (!CanBeHost(host) || !CanBeMaterial(material)) return false;
            if (ReferenceEquals(host, material) || host.Id == material.Id) return false;
            var pool = DrawPool(host, material);
            if (pool.Count == 0) return false;

            if (ForcePick.HasValue && !pool.Contains(ForcePick.Value))
                return false;
            picked = Pick(pool, host, seed);

            long cost = CostCopper();
            if (cost > 0 && !GameState.Pay(cost))
            {
                picked = default;
                return false;
            }
            if (!LifeSystem.SacrificeForFusion(material))
            {
                if (cost > 0) GameState.Grant(cost);
                picked = default;
                return false;
            }

            if (host.AbsorbedBoons.Count < SlotCap)
                host.AbsorbedBoons.Add((int)picked);
            else
                host.PendingBoon = (int)picked;

            LifeSystem.PersistRoster();
            return true;
        }

        public static bool AcceptReplace(CharacterRecord host, int slot)
        {
            if (host == null || host.PendingBoon < 0) return false;
            if (slot < 0 || slot >= host.AbsorbedBoons.Count) return false;
            host.AbsorbedBoons[slot] = host.PendingBoon;
            host.PendingBoon = -1;
            LifeSystem.PersistRoster();
            return true;
        }

        public static bool DiscardPending(CharacterRecord host)
        {
            if (host == null || host.PendingBoon < 0) return false;
            host.PendingBoon = -1;
            LifeSystem.PersistRoster();
            return true;
        }

        /// <summary>흡수 패시브를 전투 배율로. QA_NO_FUSION=1이면 전부 1 — 네거티브 컨트롤.</summary>
        public static CombatMuls CombatOf(CharacterRecord character)
        {
            var muls = CombatMuls.Identity;
            if (character == null) return muls;
            if (Environment.GetEnvironmentVariable("QA_NO_FUSION") == "1") return muls;
            Boons.Multipliers(character.AbsorbedBoons,
                              out muls.Atk, out muls.Hp, out muls.Speed, out muls.Cd,
                              out muls.Heal, out muls.Shield, out muls.Range, out muls.AtkSpd);
            return muls;
        }

        public static float HpMulOf(CharacterRecord character) => CombatOf(character).Hp;

        public static void ClearAbsorbed(CharacterRecord character)
        {
            if (character == null) return;
            character.AbsorbedBoons.Clear();
            character.PendingBoon = -1;
        }

        public static string LabelOf(BoonId id) => Boons.Def(id).Name;

        public static string AbsorbedSummary(CharacterRecord character)
        {
            if (character == null || character.AbsorbedBoons.Count == 0) return "흡수 없음";
            var parts = new List<string>(character.AbsorbedBoons.Count);
            for (int i = 0; i < character.AbsorbedBoons.Count; i++)
                parts.Add(LabelOf((BoonId)character.AbsorbedBoons[i]));
            return string.Join(" · ", parts);
        }

        /// <summary>시각 QA. DebugAutoPilot은 대화 세션 소유라 여기서 시드한다.</summary>
        public static void SeedQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable("QA_FUSION") != "1") return;
            var roster = LifeSystem.GetCharacters();
            if (roster.Count < 2) return;
            var host = roster[0];
            if (host.IsDeleted) return;
            if (host.Advancement == AdvancementTier.Basic)
            {
                host.Advancement = AdvancementTier.First;
                host.Job = "수호기사";
                if (host.Level < 20) host.Level = 20;
            }

            long cost = CostCopper();
            if (GameState.Wallet.Copper < cost * 2)
                GameState.Grant(cost * 2 - GameState.Wallet.Copper);

            if (!host.AbsorbedBoons.Contains((int)BoonId.예리함) && host.AbsorbedBoons.Count < SlotCap)
            {
                CharacterRecord material = null;
                for (int i = 1; i < roster.Count; i++)
                {
                    if (roster[i].IsDeleted) continue;
                    material = roster[i];
                    break;
                }
                if (material != null)
                {
                    material.Advancement = AdvancementTier.First;
                    material.Job = "검사";
                    if (material.Level < 20) material.Level = 20;
                    ForcePick = BoonId.예리함;
                    TryFuse(host, material, 1u, out _);
                    ForcePick = null;
                }
            }

            for (int i = 1; i < roster.Count; i++)
            {
                if (roster[i].IsDeleted) continue;
                if (CanBeMaterial(roster[i]) && DrawPool(host, roster[i]).Count > 0) break;
                roster[i].Advancement = AdvancementTier.First;
                roster[i].Job = "사제";
                if (roster[i].Level < 20) roster[i].Level = 20;
                break;
            }
        }
    }
}
