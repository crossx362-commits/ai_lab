using System;

namespace AshesToStars
{
    /// <summary>
    /// 정예 주 드랍은 강화석·일반 장비(§10-8).
    /// 던전 정예 노드가 이긴 뒤에만 읽는다. 필드 정예는 킬 카운트가 W3Party라 이 칸 아님.
    /// QA_NO면 옛 0.
    /// </summary>
    public static class EliteDrop
    {
        public const string EnvShow = "QA_ELITE_DROP";
        public const string EnvNo = "QA_NO_ELITE_DROP";
        public const GearGrade Grade = GearGrade.Common;
        public const int Stones = 1;

        static bool _qaSeeded;
        static string _lastLine = "";
        static int _fieldKills;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShowQa
        {
            get
            {
                if (Blocked) return false;
                string raw = Environment.GetEnvironmentVariable(EnvShow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string LastLine => _lastLine ?? "";

        public static bool Applies(NodeKind kind) => kind == NodeKind.정예;

        public static string Line() =>
            Blocked ? "정예 드랍 없음" : "정예 일반 장비(§10-8)";

        public static string Format(GearItem gear)
        {
            if (gear == null) return "";
            return "일반 " + Equipment.DisplayName(gear) + "(§10-8)";
        }

        /// <summary>정예 1노드 = 강화석 1 + 일반 장비 1. 가득이면 있는 것만.</summary>
        public static GearItem Apply(NodeKind kind, ref Rng rng)
        {
            _lastLine = "";
            if (Blocked) return null;
            if (!Applies(kind)) return null;
            GameState.Gain(Economy.LifeItem.EnhanceStone, Stones);
            GearItem gear = null;
            if (BagSlots.CanAddGear())
                gear = Equipment.TryGrantDrop(GearDrop.PickRecipe(ref rng), Grade);
            _lastLine = Format(gear);
            if (string.IsNullOrEmpty(_lastLine) &&
                GameState.Bag.GetCount(Economy.LifeItem.EnhanceStone) > 0)
                _lastLine = Line();
            return gear;
        }

        /// <summary>필드 정예 누적 처치 수(§10-2 보정 소비용). QA_NO면 오르지 않는다.
        /// 소비처: EliteWaveDrop.BeginWave → Mul → Economy.FieldHuntHideCount.</summary>
        public static int FieldKills => _fieldKills;

        /// <summary>
        /// 필드 정예 처치 훅 — W3Party.KillMob이 정예(종류 3·4) 1회씩 건다(큐#1, 오너 위임 승인).
        /// 던전 정예는 노드 Complete가 이미 Apply로 지급하므로 W3Party 쪽에서 던전 중 호출 금지.
        /// §10-1 「처치 시 추가 드랍」을 노드와 같은 보상(§10-8)으로 지급하고 카운트를 남긴다.
        /// QA_NO_ELITE_DROP이면 옛 동작(무지급·카운트 고정) 유지.
        /// </summary>
        public static void NoteFieldKill()
        {
            if (Blocked) return;
            _fieldKills++;
            uint seed = 20260825u ^ (uint)_fieldKills * 2654435761u;
            var rng = new Rng(seed);
            Apply(NodeKind.정예, ref rng);
        }

        /// <summary>시각 QA. QA_ELITE_DROP=1이면 가방에 일반 흉갑+강화석.</summary>
        public static void SeedQaIfRequested()
        {
            if (!ShowQa) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            GameState.Gain(Economy.LifeItem.EnhanceStone, Stones);
            var bag = Equipment.Unequipped();
            for (int i = 0; i < bag.Count; i++)
            {
                if (bag[i].Grade == Grade && bag[i].RecipeId == Equipment.LeatherArmorRecipe)
                {
                    _lastLine = Format(bag[i]);
                    return;
                }
            }
            var gear = Equipment.TryGrantDrop(Equipment.LeatherArmorRecipe, Grade);
            _lastLine = Format(gear);
        }

        public static void ResetForTest()
        {
            _qaSeeded = false;
            _lastLine = "";
            _fieldKills = 0;
        }
    }
}
