using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 첫 5층 레이드 전 비살상 훈련(§온보딩 ✅).
    /// 동의 전 PvE 패배는 사망 대신 HP 1 귀환. 5층 입장 직전 영구 사망 규칙을
    /// 보여주고 동의한 뒤부터 목숨을 깎는다. 10층까지 장기 면제는 없다.
    /// QA_NO면 처음부터 살상. ApplyWipe는 그대로 살상(V4 경로).
    /// </summary>
    public static class DeathTraining
    {
        public const int RaidFloor = 5;
        public const string EnvShow = "QA_DEATH_TRAINING";
        public const string EnvNo = "QA_NO_DEATH_TRAINING";
        const string K_CONSENT = "ats.death_consent";

        static bool _loaded;
        static bool _consented;
        static bool _qaSeeded;
        static bool _showConsent;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool Consented
        {
            get { Load(); return _consented; }
        }

        /// <summary>동의 전이고 아직 5층을 지나지 않았을 때만 훈련. 6층부터는 자동으로 끝난다.</summary>
        public static bool IsTraining
        {
            get
            {
                if (Blocked) return false;
                Load();
                return !_consented && GameState.TowerFloor <= RaidFloor;
            }
        }

        /// <summary>5층 이상 입장 직전, 아직 동의하지 않았고 훈련 구간일 때.</summary>
        public static bool NeedsConsent(int floor)
        {
            if (Blocked) return false;
            Load();
            return !_consented && floor >= RaidFloor && GameState.TowerFloor <= RaidFloor;
        }

        public static bool CanEnterFloor(int floor) => !NeedsConsent(floor);

        public static bool QaPromptConsent => _showConsent;

        public static void AckQaPrompt() => _showConsent = false;

        public static string Line()
        {
            if (!IsTraining) return "";
            return "비살상 훈련 — 5층 레이드 전 HP 1 귀환(§4)";
        }

        public static string ReturnLine() =>
            "비살상 훈련 — HP 1 귀환. 목숨은 그대로(§4)";

        public static string ConsentTitle() =>
            "5층부터는 영구 사망이 적용된다";

        public static string ConsentBody() =>
            "3번 죽으면 캐릭터와 장착 장비가 사라집니다(§4). 동의 뒤부터 모든 PvE 사망이 목숨을 깎는다. 10층까지 면제는 없다.";

        /// <summary>영구 사망 규칙에 동의한다. 이미 동의면 false.</summary>
        public static bool Consent()
        {
            Load();
            if (_consented) return false;
            _consented = true;
            Save();
            return true;
        }

        /// <summary>훈련 패배. 목숨·회복·삭제를 안 건드린다. ApplyWipe와 갈린다.</summary>
        public static PveDefeatReport ApplyReturn(IReadOnlyList<CharacterRecord> members)
        {
            var report = new PveDefeatReport { TrainingReturn = true };
            if (members != null)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    var ch = members[i];
                    if (ch == null || ch.IsDeleted) continue;
                    report.ReturnedNames.Add(ch.Name);
                }
            }
            report.LivingCount = LifeSystem.LivingCount();
            return report;
        }

        /// <summary>시각 QA. QA_DEATH_TRAINING=1이면 5층·미동의·동의 화면.</summary>
        public static void SeedQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShow) != "1") return;
            if (Blocked) return;
            if (_qaSeeded) return;
            _qaSeeded = true;
            PlayerPrefs.DeleteKey(K_CONSENT);
            _consented = false;
            _loaded = true;
            _showConsent = true;
            Save();
            GameState.SetTowerFloorForTest(RaidFloor);
            var report = ApplyReturn(PartyState.SortieRecords());
            GameFlow.LastDefeatReport = report;
            GameFlow.LastBattleSummary = GameFlow.FormatDefeatSummary("전멸 — 훈련", report);
        }

        public static void ResetForTest()
        {
            PlayerPrefs.DeleteKey(K_CONSENT);
            PlayerPrefs.Save();
            _consented = false;
            _loaded = false;
            _qaSeeded = false;
            _showConsent = false;
        }

        public static void ForgetInMemoryForTest()
        {
            _consented = false;
            _loaded = false;
            _showConsent = false;
        }

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _consented = PlayerPrefs.GetInt(K_CONSENT, 0) == 1;
        }

        static void Save()
        {
            PlayerPrefs.SetInt(K_CONSENT, _consented ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
