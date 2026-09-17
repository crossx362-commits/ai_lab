// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §2-9-10 (아이템)
//
// === 출처 ===
// 원작 포트리스2 의 아이템 규칙은 **기억이 아니라 조사**로 가져왔다(오너 규칙: 외부 게임 규칙은
// 조사하고 출처를 코드 머리말에 남긴다 — 건바운드 탱크를 포트리스로 착각했던 사고의 재발 방지).
//   https://namu.wiki/w/포트리스2/아이템   (2026-09-16 조회)
// 원작 목록은 30종이 넘는다. 여기 담은 건 **지금 코드에 이미 있는 시스템으로 성립하는 것들**이다
// (체력·상태이상·바람·날씨·이동 게이지). 화면 방해(상하반전·울렁임·안개)나 채팅 도청처럼
// 렌더링·네트워크가 있어야 말이 되는 것은 담지 않았다 — 없는 시스템 위에 선언만 하면 죽은 값이 된다.
//
// === 이 파일이 정하지 않는 것 ===
// **획득 방법**. 원작은 상점에서 골드로 사서 슬롯에 장착한다. 기획서에 규칙이 없어
// "판 시작에 슬롯 N개를 무작위로 채운다"로 뒀다 [추정] — 규칙이 정해지면 `Roll` 만 갈아끼우면 된다.

using System;
using System.Collections.Generic;

namespace Tankfall.Sim
{
    public enum ItemKind
    {
        None = 0,
        AddEnergy1,     // 에너지1 — 자기 체력 20% 회복, 턴 소모 없음
        AddEnergy2,     // 에너지2 — 자기 체력 40% 회복, 턴 소모
        TeamEnergy,     // 팀에너지 — 팀 전원 20% 회복, 턴 소모 없음
        Shield,         // 실드 — 다음 피격 1회 무효, 턴 소모 없음
        PowerUp,        // 파워업 — 이번 사격 파워 +50%, 턴 소모 없음
        DoubleFire,     // 더블파이어 — 같은 각도·파워로 한 발 더, 턴 소모
        TeleportBullet, // 텔레포트탄 — 자기 탱크를 착탄 자리로 이동
        SnowFall,       // 눈내리기 — 날씨를 눈으로, 턴 소모 없음
        WindReverse,    // 바람반대 — 바람 방향 반전, 턴 소모 없음
        MoveUp,         // 이동증가 — 5턴간 이동 게이지 2배, 턴 소모 없음
        // ── 방해탄(§2-9-14) ── 쏴서 맞춘 상대에게 건다. 전부 턴 소모(원작). 규칙·지속턴은 Sim/Impair.cs 머리말 참조.
        FlipShell,      // 반전탄 — 상대 화면 상하 반전 3턴
        WobbleShell,    // 멀미탄 — 상대 화면 울렁임 4턴
        FogShell,       // 안개탄 — 상대 안개 고립 3턴
        ConfuseShell,   // 바보탄 — 상대 화면 혼란 4턴
        LockAngleShell, // 각도고정탄 — 상대 각도 조절 불가 3턴
        LockPowerShell, // 파워고정탄 — 상대 파워 50% 미만 발사 불가 3턴
    }

    public struct ItemInfo
    {
        public ItemKind Kind;
        public string Name;
        /// <summary>쓰면 그 턴이 끝나는가. 원작 표기 "턴 소모 함/없음".</summary>
        public bool ConsumesTurn;
        /// <summary>사격과 함께 발동하는가(사격 전에 미리 걸어두는 종류).</summary>
        public bool AppliesToShot;
        public string Desc;
    }

    public static class Items
    {
        public const float HealSmall = 0.20f;     // 에너지1 — 원작 20%
        public const float HealBig = 0.40f;       // 에너지2 — 원작 40%
        public const float TeamHeal = 0.20f;      // 팀에너지 — 원작 20%
        public const float PowerUpScale = 1.50f;  // 파워업 — 원작 "파워 50% 증폭"
        public const int MoveUpTurns = 5;         // 이동증가 — 원작 5턴
        public const float MoveUpScale = 2.0f;    // 원작 "최대 이동 거리의 2배"



        static readonly ItemInfo[] Table =
        {
            new ItemInfo { Kind = ItemKind.AddEnergy1,     Name = "에너지1",   ConsumesTurn = false, AppliesToShot = false, Desc = "체력 20% 회복" },
            new ItemInfo { Kind = ItemKind.AddEnergy2,     Name = "에너지2",   ConsumesTurn = true,  AppliesToShot = false, Desc = "체력 40% 회복(턴 소모)" },
            new ItemInfo { Kind = ItemKind.TeamEnergy,     Name = "팀에너지", ConsumesTurn = false, AppliesToShot = false, Desc = "팀 전원 20% 회복" },
            new ItemInfo { Kind = ItemKind.Shield,         Name = "실드",     ConsumesTurn = false, AppliesToShot = false, Desc = "다음 피격 1회 무효" },
            new ItemInfo { Kind = ItemKind.PowerUp,        Name = "파워업",   ConsumesTurn = false, AppliesToShot = true,  Desc = "이번 사격 파워 +50%" },
            new ItemInfo { Kind = ItemKind.DoubleFire,     Name = "더블파이어", ConsumesTurn = true,  AppliesToShot = true,  Desc = "같은 각도·파워로 한 발 더" },
            new ItemInfo { Kind = ItemKind.TeleportBullet, Name = "텔레포트탄", ConsumesTurn = true,  AppliesToShot = true,  Desc = "착탄 자리로 내가 이동" },
            new ItemInfo { Kind = ItemKind.SnowFall,       Name = "눈내리기", ConsumesTurn = false, AppliesToShot = false, Desc = "날씨를 눈으로" },
            new ItemInfo { Kind = ItemKind.WindReverse,    Name = "바람반대", ConsumesTurn = false, AppliesToShot = false, Desc = "바람 방향 반전" },
            new ItemInfo { Kind = ItemKind.MoveUp,         Name = "이동증가", ConsumesTurn = false, AppliesToShot = false, Desc = "5턴간 이동 2배" },
            // 방해탄 6종(§2-9-14) — 전부 사격과 함께 나가고 턴을 먹는다(원작 표기 그대로).
            new ItemInfo { Kind = ItemKind.FlipShell,      Name = "반전탄",   ConsumesTurn = true, AppliesToShot = true, Desc = "맞은 적 화면 상하반전 3턴" },
            new ItemInfo { Kind = ItemKind.WobbleShell,    Name = "멀미탄",   ConsumesTurn = true, AppliesToShot = true, Desc = "맞은 적 화면 울렁임 4턴" },
            new ItemInfo { Kind = ItemKind.FogShell,       Name = "안개탄",   ConsumesTurn = true, AppliesToShot = true, Desc = "맞은 적 안개 고립 3턴" },
            new ItemInfo { Kind = ItemKind.ConfuseShell,   Name = "바보탄",   ConsumesTurn = true, AppliesToShot = true, Desc = "맞은 적 화면 혼란 4턴" },
            new ItemInfo { Kind = ItemKind.LockAngleShell, Name = "각도고정탄", ConsumesTurn = true, AppliesToShot = true, Desc = "맞은 적 각도 고정 3턴" },
            new ItemInfo { Kind = ItemKind.LockPowerShell, Name = "파워고정탄", ConsumesTurn = true, AppliesToShot = true, Desc = "맞은 적 파워 하한 50% 3턴" },
        };

        /// <summary>방해탄이면 어떤 효과인지. 아니면 None. 규칙·지속턴의 단일 소스는 Sim/Impair.cs 다.</summary>
        public static ImpairKind ImpairOf(ItemKind k)
        {
            switch (k)
            {
                case ItemKind.FlipShell: return ImpairKind.FlipScreen;
                case ItemKind.WobbleShell: return ImpairKind.Wobble;
                case ItemKind.FogShell: return ImpairKind.Fog;
                case ItemKind.ConfuseShell: return ImpairKind.Confuse;
                case ItemKind.LockAngleShell: return ImpairKind.LockAngle;
                case ItemKind.LockPowerShell: return ImpairKind.LockPower;
                default: return ImpairKind.None;
            }
        }

        public static ItemInfo Get(ItemKind k)
        {
            for (int i = 0; i < Table.Length; i++) if (Table[i].Kind == k) return Table[i];
            return new ItemInfo { Kind = ItemKind.None, Name = "없음", Desc = "" };
        }

        public static ItemKind[] All()
        {
            var a = new ItemKind[Table.Length];
            for (int i = 0; i < Table.Length; i++) a[i] = Table[i].Kind;
            return a;
        }

        /// <summary>판 시작 슬롯 채우기 [추정 — 기획서에 획득 규칙 없음]. 같은 아이템이 겹쳐도 된다(원작도 여러 개 산다).</summary>
        /// <summary>
        /// 판 시작 아이템 뽑기.
        /// </summary>
        public static void Roll(ref Rng rng, int slots, List<ItemKind> into)
        {
            into.Clear();
            for (int i = 0; i < slots; i++) into.Add(Table[Math.Min((int)(rng.Float01() * Table.Length), Table.Length - 1)].Kind);
        }

        /// <summary>보급 상자용 뽑기.</summary>
        public static ItemKind RollSupply(ref Rng rng)
            => Table[Math.Min((int)(rng.Float01() * Table.Length), Table.Length - 1)].Kind;
    }

    /// <summary>
    /// 유닛별 아이템 보유·지속 효과. **게임과 하네스가 같은 이 클래스를 쓴다** —
    /// 한쪽만 아이템을 쓰면 승률이 게임의 승률이 아니게 된다(§2-9-1 교대 순서 버그의 교훈).
    /// </summary>
    public class ItemState
    {
        readonly Dictionary<int, List<ItemKind>> _bag = new();
        readonly HashSet<int> _shield = new();          // 실드가 걸린 유닛
        readonly Dictionary<int, int> _moveUp = new();  // 이동증가 남은 턴
        readonly HashSet<int> _powerUp = new();         // 이번 사격에 파워업이 걸린 유닛
        readonly HashSet<int> _doubleFire = new();      // 이번 사격이 두 발인 유닛
        readonly HashSet<int> _teleport = new();        // 이번 사격이 텔레포트탄인 유닛
        readonly Dictionary<int, ImpairKind> _impairShot = new();   // 이번 사격에 실린 방해탄(§2-9-14)

        public void Clear()
        {
            _bag.Clear(); _shield.Clear(); _moveUp.Clear();
            _powerUp.Clear(); _doubleFire.Clear(); _teleport.Clear(); _impairShot.Clear();
        }

        public List<ItemKind> Bag(int id)
        {
            if (!_bag.TryGetValue(id, out var l)) { l = new List<ItemKind>(); _bag[id] = l; }
            return l;
        }

        public bool Has(int id, ItemKind k) => Bag(id).Contains(k);
        public int Count(int id) => Bag(id).Count;

        public bool HasShield(int id) => _shield.Contains(id);
        public bool HasPowerUp(int id) => _powerUp.Contains(id);
        public bool HasDoubleFire(int id) => _doubleFire.Contains(id);
        public bool HasTeleport(int id) => _teleport.Contains(id);
        /// <summary>이번 사격에 실린 방해탄. 없으면 None.</summary>
        public ImpairKind ImpairShot(int id) => _impairShot.TryGetValue(id, out var k) ? k : ImpairKind.None;
        public float MoveScale(int id) => _moveUp.ContainsKey(id) ? Items.MoveUpScale : 1f;

        /// <summary>실드 1회 소모. 막았으면 true — 피해 계산 앞에서 부른다.</summary>
        public bool ConsumeShield(int id) => _shield.Remove(id);

        /// <summary>턴 시작 — 이동증가 수명을 깎는다.</summary>
        public void TickStartOfTurn(int id)
        {
            if (!_moveUp.TryGetValue(id, out int t)) return;
            t--;
            if (t <= 0) _moveUp.Remove(id); else _moveUp[id] = t;
        }

        /// <summary>사격이 끝나면 **이번 사격용 효과는 반드시 지운다**. 안 지우면 파워업이 영구 버프가 된다.</summary>
        public void ClearShotFlags(int id)
        {
            _powerUp.Remove(id); _doubleFire.Remove(id); _teleport.Remove(id); _impairShot.Remove(id);
        }

        public enum UseResult { NotHeld, Applied, AppliedEndsTurn }

        /// <summary>
        /// 아이템 사용. 체력 회복·팀 회복은 호출부가 유닛을 알아야 하므로 델리게이트로 받는다
        /// (Sim 은 유닛 표현을 모른다 — asmdef 규칙상 UnityEngine 도 못 본다).
        /// </summary>
        public UseResult Use(int id, int team, ItemKind k,
                             Action<int, float> healUnit,          // (유닛id, 최대체력 대비 비율)
                             Action<int, float> healTeam,          // (팀, 비율)
                             Action snowFall, Action windReverse)
        {
            var bag = Bag(id);
            if (!bag.Remove(k)) return UseResult.NotHeld;
            switch (k)
            {
                case ItemKind.AddEnergy1: healUnit?.Invoke(id, Items.HealSmall); break;
                case ItemKind.AddEnergy2: healUnit?.Invoke(id, Items.HealBig); break;
                case ItemKind.TeamEnergy: healTeam?.Invoke(team, Items.TeamHeal); break;
                case ItemKind.Shield: _shield.Add(id); break;
                case ItemKind.PowerUp: _powerUp.Add(id); break;
                case ItemKind.DoubleFire: _doubleFire.Add(id); break;
                case ItemKind.TeleportBullet: _teleport.Add(id); break;
                case ItemKind.SnowFall: snowFall?.Invoke(); break;
                case ItemKind.WindReverse: windReverse?.Invoke(); break;
                case ItemKind.MoveUp: _moveUp[id] = Items.MoveUpTurns; break;
                default:
                    // 방해탄(§2-9-14)은 즉발이 아니라 **이번 사격에 실린다** — 맞은 적에게 걸린다.
                    var im = Items.ImpairOf(k);
                    if (im != ImpairKind.None) _impairShot[id] = im;
                    break;
            }
            return Items.Get(k).ConsumesTurn ? UseResult.AppliedEndsTurn : UseResult.Applied;
        }
    }
}
