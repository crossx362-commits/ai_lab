using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 사냥 중 뱀서식 임시 3택.
    ///
    /// 왜 영구 레벨이 아닌가: 필드 경험치는 판이 끝난 뒤에 오르고, 로컬 시드는 이미
    /// 만렙에 가깝다. 레벨업에 걸면 목록이 안 뜬다. 뱀서는 **이 판의 처치**로 고른다.
    ///
    /// 왜 필드에도 켜는가: §7 ✅는 던전 런의 어휘지만, 오너가 사냥 화면에서 목록을
    /// 찾는다. 접속 중 전투만 멈춘다. 방치 스케줄·오프라인은 W3Party가 없어 안 멈춘다.
    /// 나가면 초기화 — 합성 패시브(영구)와 어휘를 섞지 않는다.
    ///
    /// 보스전은 빼다. 기믹 타이밍을 멈추면 §5·§10-5가 거짓이 된다.
    /// </summary>
    public static class HuntBoon
    {
        public const string EnvNo = "QA_NO_HUNT_BOON";
        public const int FirstKills = 8;
        public const float Growth = 1.4f;

        static List<int> _owned;
        static bool _ownsList;
        static bool _active;
        static uint _seed;
        static int _draw;

        public static int Level { get; private set; }
        public static int Xp { get; private set; }
        public static int Pending { get; private set; }
        public static List<BoonId> Offered { get; private set; }

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool Active => _active && !Blocked;
        public static bool Waiting => Active && Offered != null && Offered.Count > 0;
        public static ICollection<int> Owned => _owned ?? (ICollection<int>)Array.Empty<int>();

        public static int Need(int level) =>
            Mathf.Max(1, Mathf.RoundToInt(FirstKills * Mathf.Pow(Growth, Mathf.Max(0, level))));

        public static void BeginField(uint seed)
        {
            if (Blocked) { End(); return; }
            _owned = new List<int>();
            _ownsList = true;
            Open(seed);
        }

        public static void BindDungeon(List<int> shared, uint seed)
        {
            if (Blocked || shared == null) { End(); return; }
            bool same = _active && !_ownsList && _owned == shared && _seed == seed;
            _owned = shared;
            _ownsList = false;
            if (same)
            {
                Offered = null;
                Pending = 0;
                _active = true;
                return;
            }
            Open(seed);
        }

        static void Open(uint seed)
        {
            _seed = seed;
            _draw = 0;
            Level = 0;
            Xp = 0;
            Pending = 0;
            Offered = null;
            _active = true;
        }

        public static void LeaveBattle()
        {
            Offered = null;
            Pending = 0;
            if (_ownsList) End();
        }

        public static void End()
        {
            _owned = null;
            _ownsList = false;
            _active = false;
            Offered = null;
            Pending = 0;
            Level = 0;
            Xp = 0;
            _draw = 0;
        }

        public static void NoteKill()
        {
            if (!Active || _owned == null) return;
            if (BossBattle.IsActive) return;
            if (_owned.Count >= 8) return;
            Xp++;
            int need = Need(Level);
            while (Xp >= need && _owned.Count + Pending < 8)
            {
                Xp -= need;
                Pending++;
                Level++;
                need = Need(Level);
            }
            if (!Waiting) Offer();
        }

        public static void Offer()
        {
            if (!Active || Pending <= 0 || _owned == null)
            {
                Offered = null;
                return;
            }
            Offered = Boons.Draw(_seed, 1000 + _draw, _owned);
            if (Offered.Count == 0) Pending = 0;
        }

        public static bool Take(BoonId id)
        {
            if (!Waiting) return false;
            if (!_owned.Contains((int)id)) _owned.Add((int)id);
            Pending = Mathf.Max(0, Pending - 1);
            _draw++;
            Offered = null;
            Offer();
            return true;
        }
    }
}
