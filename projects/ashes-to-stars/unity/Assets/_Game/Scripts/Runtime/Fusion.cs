using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 합성 — 1차 이상 재료를 소멸시켜 패시브 1개를 흡수한다(§3·§18-7).
    ///
    /// 재료는 영묘에 가지 않는다(자발적 희생). 슬롯 상한 4. 이미 가진 패시브는
    /// 추첨에서 빼고 다시 뽑는다. 전투 소비처는 강골 → 기존 출전 HpMul.
    /// W3Party는 이 슬라이스에서 건드리지 않는다(장비와 같은 출전 계약).
    /// </summary>
    public static class Fusion
    {
        public const int SlotCap = 4;

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

        public static bool TryFuse(CharacterRecord host, CharacterRecord material, uint seed, out BoonId picked)
        {
            picked = default;
            if (!CanBeHost(host) || !CanBeMaterial(material)) return false;
            if (ReferenceEquals(host, material) || host.Id == material.Id) return false;
            var pool = DrawPool(host, material);
            if (pool.Count == 0) return false;

            if (ForcePick.HasValue)
            {
                // 강제 픽이 풀에 없으면 실패다. 랜덤으로 넘어가면
                // "이미 가진 것은 빠진다"를 검증할 수 없다.
                if (!pool.Contains(ForcePick.Value)) return false;
                picked = ForcePick.Value;
            }
            else
            {
                var rng = Rng.Stream(seed, host.AbsorbedBoons.Count + 1, SeedChannel.Fusion);
                picked = pool[rng.Next(pool.Count)];
            }

            if (!LifeSystem.SacrificeForFusion(material)) return false;

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

        /// <summary>강골만 출전 HpMul에 곱한다. QA_NO_FUSION=1이면 1 — 네거티브 컨트롤.</summary>
        public static float HpMulOf(CharacterRecord character)
        {
            if (character == null) return 1f;
            if (Environment.GetEnvironmentVariable("QA_NO_FUSION") == "1") return 1f;
            Boons.Multipliers(character.AbsorbedBoons, out _, out float hp,
                              out _, out _, out _, out _, out _, out _);
            return hp > 0f ? hp : 1f;
        }

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
            if (host.AbsorbedBoons.Count > 0 || host.PendingBoon >= 0) return;

            CharacterRecord material = null;
            for (int i = 1; i < roster.Count; i++)
            {
                if (roster[i].IsDeleted) continue;
                material = roster[i];
                break;
            }
            if (material == null) return;
            material.Advancement = AdvancementTier.First;
            material.Job = "수호기사";
            if (material.Level < 20) material.Level = 20;

            ForcePick = BoonId.강골;
            TryFuse(host, material, 1u, out _);
            ForcePick = null;
        }
    }
}
