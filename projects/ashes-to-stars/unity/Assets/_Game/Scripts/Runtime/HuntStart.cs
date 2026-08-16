using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 사냥 시작 두 단계(오너 2026-08-17).
    /// 필드에서 캐릭터를 고르고 스타트 → 전장. 전장에서 배치한 뒤 스타트 → 전투.
    /// 탑·던전·침략·QA 직행(GoBattle)은 Idle이라 예전처럼 바로 싸운다.
    /// </summary>
    public static class HuntStart
    {
        public const string EnvShow = "QA_HUNT_START";
        public const string EnvDeploy = "QA_HUNT_DEPLOY";
        public const string EnvNo = "QA_NO_HUNT_START";

        public enum Phase { Idle, Picking, Deploying, Fighting }

        static Phase _phase;
        static int _selected = -1;
        static readonly Vector2[] _pos = new Vector2[PartyState.MaxSlots];

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static Phase Current => Blocked ? Phase.Idle : _phase;
        public static bool Picking => Current == Phase.Picking;
        public static bool Deploying => Current == Phase.Deploying;
        public static bool Fighting => Current == Phase.Fighting;
        public static bool ShouldHold => Deploying;
        public static int Selected => _selected;

        public static string PickTitle => "출전할 캐릭터를 고른 뒤 스타트";
        public static string PickSubtitle => "스타트하면 전장으로 들어간다. 배치한 뒤 한 번 더 스타트한다";
        public static string DeployTitle => "배치한 뒤 스타트 — 전투가 시작된다";
        public static string DeployHint => "캐릭터를 고르고 땅을 누르면 자리를 옮긴다";

        public static bool BeginPick()
        {
            if (Blocked) return false;
            _phase = Phase.Picking;
            _selected = -1;
            return true;
        }

        public static void Cancel()
        {
            _phase = Phase.Idle;
            _selected = -1;
        }

        public static bool ConfirmPick()
        {
            if (Blocked) return false;
            if (_phase != Phase.Picking) return false;
            if (!PartyState.CanSortie) return false;
            _phase = Phase.Deploying;
            _selected = 0;
            InitDefaultPos();
            return true;
        }

        public static bool ConfirmStart()
        {
            if (Blocked) return false;
            if (_phase != Phase.Deploying) return false;
            _phase = Phase.Fighting;
            _selected = -1;
            return true;
        }

        public static void Select(int i)
        {
            if (_phase != Phase.Deploying) return;
            if (i < 0 || i >= PartyState.Slots.Count) return;
            _selected = i;
        }

        public static bool TryPlace(int i, Vector2 world)
        {
            if (_phase != Phase.Deploying) return false;
            if (i < 0 || i >= PartyState.Slots.Count) return false;
            _pos[i] = world;
            return true;
        }

        public static bool TryPlaceSelected(Vector2 world) =>
            _selected >= 0 && TryPlace(_selected, world);

        public static Vector2 PosOf(int i)
        {
            if (i < 0 || i >= _pos.Length) return Vector2.zero;
            return _pos[i];
        }

        public static Vector2 DefaultPos(int i, string job)
        {
            float x = i == 2 ? 1.2f : i == 4 ? -1.2f : 0f;
            float y = IsFront(job) ? 1.8f : IsMid(job) ? -0.4f : -2.6f;
            return new Vector2(x, y);
        }

        static bool IsFront(string job) =>
            job == "탱" || job == "수호기사" || job == "광전사";

        static bool IsMid(string job) =>
            job == "딜" || job == "마딜" || job == "검사" || job == "궁수"
            || job == "마법사" || job == "소환사";

        static void InitDefaultPos()
        {
            var roster = LifeSystem.GetCharacters();
            var slots = PartyState.Slots;
            for (int i = 0; i < _pos.Length; i++)
            {
                string job = "";
                if (i < slots.Count)
                {
                    int idx = slots[i];
                    if (idx >= 0 && idx < roster.Count) job = roster[idx].Job;
                }
                _pos[i] = DefaultPos(i, job);
            }
        }

        public static string StatusOf(CharacterRecord ch, int rosterIndex, bool inParty)
        {
            if (ch.IsDeleted) return "삭제됨 — 환생석으로만 복구(§4)";
            int left = LifeSystem.GetRecoveryTimeRemaining(ch);
            if (left > 0 && DefenseState.Contains(rosterIndex))
                return $"수비대 회복 {LifeSystem.FormatRecoveryPhrase(left)} — 출전 불가(§15)";
            if (DefenseState.Contains(rosterIndex))
                return "수비 배치 — 출전 불가(§13-5)";
            if (HuntSchedule.Contains(rosterIndex))
                return "일정 사냥 — 출전 불가(§6)";
            if (left > 0) return $"회복 {LifeSystem.FormatRecoveryPhrase(left)} — 출전 불가(§4·§18-8)";
            string mark = inParty ? "편성됨" : "대기";
            if (ch.DeathCount >= 2) return $"{mark} · [주의] 마지막 목숨 — 죽으면 영구 삭제(§4)";
            return mark;
        }

        public static void SeedQaIfRequested()
        {
            if (Blocked) return;
            if (EnvIs(EnvDeploy))
            {
                _ = LifeSystem.GetCharacters();
                _ = PartyState.Slots;
                _phase = Phase.Deploying;
                _selected = PartyState.Slots.Count > 0 ? 0 : -1;
                InitDefaultPos();
                return;
            }
            if (EnvIs(EnvShow))
            {
                _ = LifeSystem.GetCharacters();
                _ = PartyState.Slots;
                _phase = Phase.Picking;
                _selected = -1;
            }
        }

        static bool EnvIs(string key)
        {
            string raw = Environment.GetEnvironmentVariable(key);
            return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
        }

        public static void ResetForTest()
        {
            _phase = Phase.Idle;
            _selected = -1;
            for (int i = 0; i < _pos.Length; i++) _pos[i] = Vector2.zero;
        }
    }
}
