using System.Collections.Generic;

namespace AshesToStars
{
    // ─────────────────────────────────────────────────────────────
    // 던전 임시 강화 (명세 S8 · 기획서 §7)
    //
    // ✅ §7 "진행 방식은 뱀서류 — 웨이브 생존 + 처치, 진행 중 임시 강화 선택"
    // 💡 §7 "던전 내 3택 임시 스킬/패시브 → **던전을 나가면 초기화**(런 안에서만 유효)"
    //
    // 중복 규칙은 §18-7(합성)의 확정 규칙을 그대로 가져온다:
    //   **이미 보유한 것은 추첨 대상에서 제외하고 재추첨.**
    //   중복으로 낭비되지 않되 합산 강화도 없다(무한 성장 차단).
    //   💡 합성에 대한 확정을 강화 3택에 적용하는 것은 이 문서의 제안이다 —
    //      규칙을 두 벌 만들면 "왜 여긴 다르지"가 생기고, 유저는 한 게임 안에서 같은 어휘를 기대한다.
    //
    // 효과는 **전투 수치 배율**로만 만든다. 새 메커니즘(신규 스킬·상태이상)을 여기서 만들면
    // 검증할 것이 갑자기 늘어나고, 그건 수직 슬라이스의 목적이 아니다.
    // ─────────────────────────────────────────────────────────────

    public enum BoonId
    {
        예리함,      // 공격력
        강골,        // 최대 HP
        발놀림,      // 이동 속도
        숙련,        // 쿨다운 감소
        치유의손,    // 회복량
        방벽,        // 보호막
        집중,        // 사거리
        분노,        // 공격 속도
    }

    public struct BoonDef
    {
        public BoonId Id;
        public string Name;
        public string Desc;
    }

    public static class Boons
    {
        /// <summary>한 번에 보여주는 선택지 수(✅ §7 "3택").</summary>
        public const int Choices = 3;

        static readonly BoonDef[] All =
        {
            new BoonDef { Id = BoonId.예리함,   Name = "예리함",     Desc = "공격력 +20%" },
            new BoonDef { Id = BoonId.강골,     Name = "강골",       Desc = "최대 HP +25% (즉시 그만큼 회복)" },
            new BoonDef { Id = BoonId.발놀림,   Name = "발놀림",     Desc = "이동 속도 +15%" },
            new BoonDef { Id = BoonId.숙련,     Name = "숙련",       Desc = "스킬 쿨다운 -20%" },
            new BoonDef { Id = BoonId.치유의손, Name = "치유의 손",  Desc = "회복량 +30%" },
            new BoonDef { Id = BoonId.방벽,     Name = "방벽",       Desc = "보호막 +50%" },
            new BoonDef { Id = BoonId.집중,     Name = "집중",       Desc = "사거리 +20%" },
            new BoonDef { Id = BoonId.분노,     Name = "분노",       Desc = "공격 속도 +20%" },
        };

        public static BoonDef Def(BoonId id)
        {
            foreach (var b in All) if (b.Id == id) return b;
            return All[0];
        }

        /// <summary>
        /// 3택 후보를 뽑는다. 시드가 같으면 같은 후보가 나온다 —
        /// 강화 선택은 런의 결과를 크게 바꾸므로 시드 감사에 포함돼야 한다(§3-2 Boon 채널).
        /// </summary>
        public static List<BoonId> Draw(uint runSeed, int node, ICollection<int> owned)
        {
            var rng = Rng.Stream(runSeed, node, SeedChannel.Boon);
            var pool = new List<BoonId>();
            foreach (var b in All)
                if (!owned.Contains((int)b.Id)) pool.Add(b.Id);   // 보유한 것은 후보에서 제외(§18-7)

            rng.Shuffle(pool);
            var picks = new List<BoonId>();
            for (int i = 0; i < Choices && i < pool.Count; i++) picks.Add(pool[i]);
            return picks;   // 남은 후보가 3개 미만이면 그만큼만 — 없는 걸 지어내지 않는다
        }

        /// <summary>보유 강화를 전투 배율로 환산한다. 곱이 아니라 합으로 쌓는다(예측 가능하게).</summary>
        public static void Multipliers(ICollection<int> owned,
                                       out float atk, out float hp, out float speed,
                                       out float cd, out float heal, out float shield,
                                       out float range, out float atkSpeed)
        {
            atk = hp = speed = cd = heal = shield = range = atkSpeed = 1f;
            foreach (int i in owned)
            {
                switch ((BoonId)i)
                {
                    case BoonId.예리함: atk += 0.20f; break;
                    case BoonId.강골: hp += 0.25f; break;
                    case BoonId.발놀림: speed += 0.15f; break;
                    case BoonId.숙련: cd -= 0.20f; break;
                    case BoonId.치유의손: heal += 0.30f; break;
                    case BoonId.방벽: shield += 0.50f; break;
                    case BoonId.집중: range += 0.20f; break;
                    case BoonId.분노: atkSpeed += 0.20f; break;
                }
            }
            if (cd < 0.4f) cd = 0.4f;   // 쿨다운이 0에 수렴하면 스킬 판정이 무의미해진다
        }
    }
}
