using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
    public enum AdvancementTier { Basic = 0, First = 1, Second = 2 }
    public enum FirstTrialAction { Guard, Taunt, Brace, Mark, Strike, Execute, Heal, Cleanse, Stabilize, Inspire, Weaken, Sustain }

    public sealed class FirstAdvancementTrial
    {
        public CharacterRecord Character { get; }
        public string TargetJob { get; }
        public int Pattern { get; }
        public int Progress { get; internal set; }
        public int Required { get; }
        public string Objective { get; }
        public bool ObjectiveMet => Progress >= Required;
        readonly FirstTrialAction[] _actions;
        public IReadOnlyList<FirstTrialAction> Actions => _actions;
        public FirstTrialAction RequiredAction => _actions[(Progress + Pattern) % _actions.Length];

        internal FirstAdvancementTrial(CharacterRecord character, string targetJob, int pattern,
                                       int required, string objective, FirstTrialAction[] actions)
        {
            Character = character;
            TargetJob = targetJob;
            Pattern = pattern;
            Required = required;
            Objective = objective;
            _actions = actions;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 목숨 시스템 (§4 사망·환생·목숨 경제)
    //
    // 기획서 핵심:
    // - 사망은 PvE 전체(필드·던전·탑·레이드)에서 카운트, PvP는 카운트 안 함
    // - 사망 시 1일 회복 기간 → 그 동안 출전 불가
    // - 누적 3회 사망 = 캐릭터 삭제 (장비·소지품 함께 소멸)
    // - 부활초(소지 상한 3개) = 사망 카운트 1 차감
    // - 환생석 = 10층 보스 드랍으로 삭제된 캐릭터 부활
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 캐릭터의 생명 관련 데이터.
    /// 이름, 직업, 레벨, 사망 횟수, 회복 상태를 기록한다.
    /// </summary>
    [System.Serializable]
    public class CharacterRecord
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Job { get; set; }  // 현재 직업명(기본: 탱·딜·힐·버퍼, 1차: 수호기사 등)
        public AdvancementTier Advancement { get; set; }
        public int Level { get; set; }

        /// <summary>
        /// 현재 레벨에서 다음 레벨까지 쌓은 경험치(§3·§18-6). 레벨업 때 0으로 리셋된다.
        /// 예전엔 Level이 생성값 그대로 굳어 성장이 "정의만 있고 소비처 0곳"이었다 —
        /// 이제 전투 보상(ResultScreen 경유 BattleScreen)이 여기에 쌓인다.
        /// </summary>
        public long Exp { get; set; }

        /// <summary>누적 사망 횟수 (0~3, 3이면 삭제된 상태).</summary>
        public int DeathCount { get; set; }

        /// <summary>회복 종료 시각 (Unix 타임스탬프). 0이면 회복 중이 아님.</summary>
        public long RecoveryEndTime { get; set; }

        /// <summary>삭제됨 여부.</summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// 생존 0명일 때만 나오는 긴급 재건 캐릭터(§3).
        /// 동시에 살아있는 재건은 1명뿐이라, 일반 영입 비용을 우회하지 못한다.
        /// </summary>
        public bool IsRescue { get; set; }

        public CharacterRecord(string name, string job, int level = 1,
                               AdvancementTier advancement = AdvancementTier.Basic)
        {
            Name = name;
            Id = System.Guid.NewGuid().ToString("N");
            Job = job;
            Advancement = advancement;
            Level = level;
            Exp = 0;
            DeathCount = 0;
            RecoveryEndTime = 0;
            IsDeleted = false;
            IsRescue = false;
        }
    }

    /// <summary>PvE 패배 한 번의 목숨 결과. BattleScreen·결과 화면·V4 SelfCheck가 같은 값을 본다.</summary>
    public sealed class PveDefeatReport
    {
        public readonly List<string> FallenNames = new();
        public readonly List<string> DeletedNames = new();
        public string RescueName;
        public int LivingCount;
        public bool RescueGranted;
    }

    /// <summary>
    /// 목숨 시스템의 중앙 관리자.
    /// 파티 보유 캐릭터 목록, 사망 처리, 회복 확인, 부활초 관리를 담당한다.
    /// 프로토타입이므로 PlayerPrefs와 메모리로만 저장한다.
    /// </summary>
    public static class LifeSystem
    {
        private static List<CharacterRecord> _characters = new();
        private static bool _loaded;
        private const int RecoveryDurationSeconds = 86400;  // 1일 = 24시간 = 86,400초
        private const string K_ROSTER = "ats.roster";
        private const int InitialRevivePotions = 3;
        public const int FirstAdvancementMaterialCost = 5;
        public const int SecondAdvancementMaterialCost = 20;
        public static FirstAdvancementTrial ActiveFirstTrial { get; private set; }
        public static FirstAdvancementTrial ActiveSecondTrial { get; private set; }
        private static readonly Dictionary<string, string[]> FirstAdvancementByBasicJob = new()
        {
            { "탱", new[] { "수호기사", "광전사" } },
            { "딜", new[] { "검사", "궁수", "마법사", "소환사" } },
            { "힐", new[] { "사제", "드루이드" } },
            { "버퍼", new[] { "음유시인", "주술사", "정령사" } },
        };

        // 테스트용 배속 상수 (기본 1.0 = 실제 시간)
        // Inspector에서 조정 가능하려면 더 큰 구조가 필요하므로, 여기선 고정값 유지
        private const float TimeScale = 1.0f;  // 배속 1.0배 = 실제 시간

        /// <summary>
        /// 시스템 초기화. 프로토타입에서는 테스트용 캐릭터 5명을 만든다.
        /// 본 게임에서는 시작 화면에서 호출될 때 초기 2캐릭을 생성할 것(§3).
        /// </summary>
        public static void Initialize()
        {
            _characters.Clear();
            _loaded = true;
            // 부활초는 GameState.Bag이 단일 소스다(아래 EnsureLoaded 주석 참고)
            int have = GameState.Bag.GetCount(Economy.LifeItem.RevivalTea);
            if (have < InitialRevivePotions)
                GameState.Gain(Economy.LifeItem.RevivalTea, InitialRevivePotions - have);

            // 프로토타입: 초기 캐릭터 5명
            // (기획서 §3는 시작 2캐릭이지만, 5인 파티 검증용으로 5명 생성)
            // 이유: 탑/레이드는 5인 파티가 기본 전제(§9)이고, 프로토타입에서
            // 1~5인 조합의 사망 시나리오를 테스트하려면 충분한 캐릭터가 필요.
            // (결정: 프로토타입에는 5명이 합리적)

            _characters.Add(new CharacterRecord("탱크", "탱", level: 10));
            _characters.Add(new CharacterRecord("딜러1", "딜", level: 10));
            _characters.Add(new CharacterRecord("딜러2", "딜", level: 10));
            _characters.Add(new CharacterRecord("힐러", "힐", level: 10));
            _characters.Add(new CharacterRecord("버퍼", "버퍼", level: 10));
            Save();
        }

        /// <summary>
        /// 로스터를 보장한다.
        ///
        /// 왜 게으른 로드인가: `Initialize()`는 **어디에서도 호출되지 않고 있었다**(grep 0건).
        /// 그래서 캐릭터 목록이 항상 비어 있었고, 그 결과
        ///   · 캐릭터 화면이 공백 · `HasLastLifeCharacter()`가 언제나 false(마지막 목숨 경고 부재)
        ///   · 전멸해도 사망이 **한 건도 기록되지 않음** — §4가 이 게임의 정체성인데 통째로 죽어 있었다.
        /// "클래스가 있는 것과 그 인스턴스를 가진 주체가 있는 것은 다르다"(인수인계 §5)와 같은 사고다.
        /// 진입점을 새로 만들면 그 진입점을 부르는 걸 또 잊는다 — GameState와 같은 게으른 로드로 맞춘다.
        ///
        /// 영속화도 여기서 한다: **다시 켜면 사망이 사라지는 영구사망은 영구사망이 아니다.**
        /// </summary>
        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            string raw = PlayerPrefs.GetString(K_ROSTER, "");
            if (string.IsNullOrEmpty(raw))
            {
                _loaded = false;      // Initialize가 스스로 세운다
                Initialize();
                return;
            }

            _characters.Clear();
            int legacyIndex = 0;
            foreach (string line in raw.Split('\n'))
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] p = line.Split('\t');
                if (p.Length < 6) continue;
                var c = new CharacterRecord(p[0], p[1], SafeInt(p[2], 1))
                {
                    DeathCount = SafeInt(p[3], 0),
                    RecoveryEndTime = SafeLong(p[4], 0),
                    IsDeleted = p[5] == "1",
                    // 7번째 필드(경험치)는 나중에 추가됐다 — 옛 6필드 저장은 0으로 읽어 하위호환.
                    Exp = p.Length > 6 ? SafeLong(p[6], 0) : 0,
                    // 8번째 필드는 전직 단계. 이 필드가 없던 기존 저장의
                    // 11종 직업명은 이미 1차 완료한 캐릭터로 보존한다.
                    Advancement = p.Length > 7
                        ? SafeAdvancement(p[7])
                        : (IsFirstAdvancementJob(p[1]) ? AdvancementTier.First : AdvancementTier.Basic),
                    Id = p.Length > 8 && !string.IsNullOrEmpty(p[8])
                        ? p[8] : LegacyCharacterId(p[0], p[1], legacyIndex),
                    // 10번째 필드는 긴급 재건 표시. 없던 저장은 일반 캐릭터다.
                    IsRescue = p.Length > 9 && p[9] == "1",
                };
                _characters.Add(c);
                legacyIndex++;
            }
        }

        static string LegacyCharacterId(string name, string job, int index)
        {
            uint hash = 2166136261u;
            string key = name + "|" + job + "|" + index;
            for (int i = 0; i < key.Length; i++) { hash ^= key[i]; hash *= 16777619u; }
            return "legacy" + hash.ToString("x8");
        }

        static int SafeInt(string s, int fallback) => int.TryParse(s, out int v) ? v : fallback;
        static long SafeLong(string s, long fallback) => long.TryParse(s, out long v) ? v : fallback;
        static AdvancementTier SafeAdvancement(string s)
        {
            int value = SafeInt(s, 0);
            return value >= 0 && value <= 2 ? (AdvancementTier)value : AdvancementTier.Basic;
        }

        static bool IsFirstAdvancementJob(string job) => job == "수호기사" || job == "광전사"
            || job == "검사" || job == "궁수" || job == "마법사" || job == "소환사"
            || job == "사제" || job == "드루이드" || job == "음유시인" || job == "주술사"
            || job == "정령사";

        /// <summary>로스터를 저장한다. 사망·삭제는 즉시 남아야 한다(§4).</summary>
        static void StageRosterForSave()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in _characters)
                sb.Append(c.Name).Append('\t').Append(c.Job).Append('\t').Append(c.Level)
                  .Append('\t').Append(c.DeathCount).Append('\t').Append(c.RecoveryEndTime)
                  .Append('\t').Append(c.IsDeleted ? '1' : '0')
                  .Append('\t').Append(c.Exp).Append('\t').Append((int)c.Advancement)
                  .Append('\t').Append(c.Id).Append('\t').Append(c.IsRescue ? '1' : '0').Append('\n');
            PlayerPrefs.SetString(K_ROSTER, sb.ToString());
        }

        private static void Save(bool includeStagedBag = false)
        {
            StageRosterForSave();
            if (includeStagedBag) GameState.StageBagForAtomicSave();
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 저장은 그대로 두고 **메모리만** 잊는다 — "게임을 껐다 켠 상태"를 만든다.
        /// 자가검사가 영속화를 확인할 때 쓴다(저장을 지우면 확인이 성립하지 않는다).
        /// </summary>
        public static void ForgetInMemoryForTest()
        {
            _characters.Clear();
            _loaded = false;
        }

        /// <summary>디버그·테스트용 초기화. 저장된 로스터까지 지운다.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(K_ROSTER);
            PlayerPrefs.Save();
            _characters.Clear();
            _loaded = false;
            ActiveFirstTrial = null;
            ActiveSecondTrial = null;
        }

        /// <summary>
        /// 캐릭터 목록을 반환한다.
        /// </summary>
        public static List<CharacterRecord> GetCharacters()
        {
            EnsureLoaded();
            return _characters;
        }

        // ========== 전직 (§3 기본직업 → Lv20 1차) ==========

        /// <summary>현재 기본직업에서 선택 가능한 1차 직업명. 전직 완료·잘못된 입력은 빈 목록.</summary>
        public static IReadOnlyList<string> FirstAdvancementOptions(CharacterRecord character)
        {
            if (character == null || character.IsDeleted || character.Advancement != AdvancementTier.Basic
                || !FirstAdvancementByBasicJob.TryGetValue(character.Job, out string[] options))
                return Array.Empty<string>();
            return options;
        }

        static bool CanFirstAdvance(CharacterRecord character, string targetJob)
        {
            EnsureLoaded();
            if (character == null || character.IsDeleted || character.Level < 20
                || character.Advancement != AdvancementTier.Basic || !_characters.Contains(character))
                return false;

            var options = FirstAdvancementOptions(character);
            bool allowed = false;
            for (int i = 0; i < options.Count; i++)
                if (options[i] == targetJob) { allowed = true; break; }
            return allowed;
        }

        /// <summary>Lv20·재료5개 조건을 확인하고 비살상 역할 시험을 시작한다.</summary>
        public static bool TryBeginFirstAdvancementTrial(CharacterRecord character, string targetJob)
        {
            if (!CanFirstAdvance(character, targetJob)
                || GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial) < FirstAdvancementMaterialCost)
                return false;

            int pattern = StableTrialPattern(character.Id, AdvancementTier.First);
            int required;
            string objective;
            FirstTrialAction[] actions;
            switch (character.Job)
            {
                case "탱": required = 3; objective = "훈련 인형 보호·후열 공격 차단";
                    actions = new[] { FirstTrialAction.Guard, FirstTrialAction.Taunt, FirstTrialAction.Brace }; break;
                case "딜": required = 3; objective = "우선 표적 순서대로 처치";
                    actions = new[] { FirstTrialAction.Mark, FirstTrialAction.Strike, FirstTrialAction.Execute }; break;
                case "힐": required = 3; objective = "아군 생존·해로운 효과 2회 정화";
                    actions = new[] { FirstTrialAction.Heal, FirstTrialAction.Cleanse, FirstTrialAction.Stabilize }; break;
                default: required = 3; objective = "강화·약화 유지로 목표 DPS 달성";
                    actions = new[] { FirstTrialAction.Inspire, FirstTrialAction.Weaken, FirstTrialAction.Sustain }; break;
            }
            ActiveFirstTrial = new FirstAdvancementTrial(character, targetJob, pattern, required, objective, actions);
            return true;
        }

        static int StableTrialPattern(string characterId, AdvancementTier tier)
        {
            uint hash = 2166136261u;
            string key = characterId + "|" + (int)tier;
            for (int i = 0; i < key.Length; i++) { hash ^= key[i]; hash *= 16777619u; }
            return (int)(hash % 3u);
        }

        /// <summary>역할 목표 1회를 기록한다. 추후 전투 이벤트가 이 진입점을 호출한다.</summary>
        public static bool ReportFirstTrialProgress(FirstTrialAction action)
        {
            if (ActiveFirstTrial == null || ActiveFirstTrial.ObjectiveMet
                || action != ActiveFirstTrial.RequiredAction) return false;
            ActiveFirstTrial.Progress++;
            return true;
        }

        /// <summary>시험 중단/실패. 비살상이라 재료·목숨·직업을 전혀 건드리지 않는다.</summary>
        public static void CancelFirstAdvancementTrial() => ActiveFirstTrial = null;

        /// <summary>역할 목표 성공을 확인하는 단일 커밋 지점. 여기서만 재료 5개를 소비한다.</summary>
        public static bool ConfirmFirstAdvancementTrial()
        {
            var trial = ActiveFirstTrial;
            if (trial == null || !trial.ObjectiveMet || !CanFirstAdvance(trial.Character, trial.TargetJob)
                || GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial) < FirstAdvancementMaterialCost)
                return false;
            if (!GameState.TryConsumeDeferred(Economy.LifeItem.AdvancementMaterial, FirstAdvancementMaterialCost))
                return false;

            string oldJob = trial.Character.Job;
            AdvancementTier oldTier = trial.Character.Advancement;
            trial.Character.Job = trial.TargetJob;
            trial.Character.Advancement = AdvancementTier.First;
            try { Save(includeStagedBag: true); }
            catch
            {
                trial.Character.Job = oldJob;
                trial.Character.Advancement = oldTier;
                // Save가 던지기 전에 전직 roster가 PlayerPrefs 캐시에 스테이징됐을 수 있다.
                // 메모리만 되돌리고 Gain이 저장하면 그 낡은 스테이징이 살아나므로 먼저 덮는다.
                StageRosterForSave();
                GameState.Gain(Economy.LifeItem.AdvancementMaterial, FirstAdvancementMaterialCost);
                throw;
            }
            Debug.Log($"[전직] {trial.Character.Name}: {trial.TargetJob} 1차 전직 완료");
            ActiveFirstTrial = null;
            return true;
        }

        // ========== 2차 각성 (§3·§18-6 Lv50, 같은 직업 심화) ==========

        static bool CanSecondAdvance(CharacterRecord character)
        {
            EnsureLoaded();
            return character != null && !character.IsDeleted && character.Level >= 50
                && character.Advancement == AdvancementTier.First && _characters.Contains(character)
                && IsFirstAdvancementJob(character.Job);
        }

        /// <summary>Lv50·재료20 조건을 확인하고 현재 1차 직업의 비살상 각성 시험을 시작한다.</summary>
        public static bool TryBeginSecondAdvancementTrial(CharacterRecord character)
        {
            if (!CanSecondAdvance(character)
                || GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial) < SecondAdvancementMaterialCost)
                return false;

            FirstTrialAction[] actions;
            string objective;
            if (character.Job == "수호기사" || character.Job == "광전사")
            {
                objective = "극한 피해를 막고 후열을 보호";
                actions = new[] { FirstTrialAction.Guard, FirstTrialAction.Taunt, FirstTrialAction.Brace };
            }
            else if (character.Job == "검사" || character.Job == "궁수" || character.Job == "마법사" || character.Job == "소환사")
            {
                objective = "각성 표적을 순서대로 제압";
                actions = new[] { FirstTrialAction.Mark, FirstTrialAction.Strike, FirstTrialAction.Execute };
            }
            else if (character.Job == "사제" || character.Job == "드루이드")
            {
                objective = "치명 상황의 아군을 안정화";
                actions = new[] { FirstTrialAction.Heal, FirstTrialAction.Cleanse, FirstTrialAction.Stabilize };
            }
            else
            {
                objective = "각성 강화·약화를 끊김 없이 유지";
                actions = new[] { FirstTrialAction.Inspire, FirstTrialAction.Weaken, FirstTrialAction.Sustain };
            }

            ActiveSecondTrial = new FirstAdvancementTrial(character, character.Job,
                StableTrialPattern(character.Id, AdvancementTier.Second), 3, objective, actions);
            return true;
        }

        public static bool ReportSecondTrialProgress(FirstTrialAction action)
        {
            if (ActiveSecondTrial == null || ActiveSecondTrial.ObjectiveMet
                || action != ActiveSecondTrial.RequiredAction) return false;
            ActiveSecondTrial.Progress++;
            return true;
        }

        public static void CancelSecondAdvancementTrial() => ActiveSecondTrial = null;

        /// <summary>시험 성공 확인 때만 재료20을 소비하고 직업명은 유지한 채 2차 단계로 저장한다.</summary>
        public static bool ConfirmSecondAdvancementTrial()
        {
            var trial = ActiveSecondTrial;
            if (trial == null || !trial.ObjectiveMet || !CanSecondAdvance(trial.Character)
                || trial.TargetJob != trial.Character.Job
                || GameState.Bag.GetCount(Economy.LifeItem.AdvancementMaterial) < SecondAdvancementMaterialCost)
                return false;
            if (!GameState.TryConsumeDeferred(Economy.LifeItem.AdvancementMaterial, SecondAdvancementMaterialCost))
                return false;

            trial.Character.Advancement = AdvancementTier.Second;
            try { Save(includeStagedBag: true); }
            catch
            {
                trial.Character.Advancement = AdvancementTier.First;
                StageRosterForSave();
                GameState.Gain(Economy.LifeItem.AdvancementMaterial, SecondAdvancementMaterialCost);
                throw;
            }
            Debug.Log($"[전직] {trial.Character.Name}: {trial.TargetJob} 2차 각성 완료");
            ActiveSecondTrial = null;
            return true;
        }

        // ========== 성장 (§3 경험치 분배 · §18-6 레벨 곡선) ==========

        /// <summary>레벨 상한(§18-6). 여기 도달하면 경험치를 더 쌓지 않는다.</summary>
        public const int MaxLevel = 100;

        /// <summary>
        /// 레벨 L에서 L+1로 오르는 데 필요한 경험치 (§18-6: `100 × Lv^2.2`).
        /// 단조 증가라 뒤로 갈수록 비싸진다. 만렙이면 무한대(더 안 오른다).
        /// 예: Lv1→2 = 100 · Lv2→3 = 459 · Lv3→4 = 1121 · Lv10→11 = 15848.
        /// </summary>
        public static long ExpToNext(int level)
        {
            if (level >= MaxLevel) return long.MaxValue;
            if (level < 1) level = 1;
            return (long)(100.0 * System.Math.Pow(level, 2.2));
        }

        /// <summary>
        /// 경험치를 더하고 레벨업을 처리한다(§18-6). 이번에 오른 레벨 수를 반환(0이면 그대로).
        /// 삭제된 캐릭터·0 이하 입력은 무시한다. 저장은 호출부(AwardBattleExp)가 한다.
        /// </summary>
        public static int AddExp(CharacterRecord c, long amount)
        {
            if (c == null || c.IsDeleted || amount <= 0) return 0;
            int gained = 0;
            c.Exp += amount;
            while (c.Level < MaxLevel && c.Exp >= ExpToNext(c.Level))
            {
                c.Exp -= ExpToNext(c.Level);
                c.Level++;
                gained++;
            }
            if (c.Level >= MaxLevel) c.Exp = 0;   // 만렙은 경험치를 쌓지 않는다
            return gained;
        }

        /// <summary>
        /// 전투 경험치를 **출전 파티**(PartyState.Slots)에 §3 레벨 비례로 분배한다.
        ///   캐릭터 i 획득 = 총 × (Lv_i ÷ 출전 레벨 합) — 1명이면 100%, 다인이면 레벨 비례.
        /// 삭제된 유령은 제외한다. 저장까지 하고, 화면에 보일 요약 줄들을 반환한다.
        /// </summary>
        public static List<string> AwardBattleExp(long totalExp)
        {
            var lines = new List<string>();
            if (totalExp <= 0) return lines;
            EnsureLoaded();

            var deployed = new List<CharacterRecord>();
            long levelSum = 0;
            foreach (int idx in PartyState.Slots)
            {
                if (idx < 0 || idx >= _characters.Count) continue;
                var c = _characters[idx];
                if (c.IsDeleted) continue;
                deployed.Add(c);
                levelSum += c.Level;
            }
            if (deployed.Count == 0 || levelSum <= 0) return lines;

            long distributed = 0;
            for (int i = 0; i < deployed.Count; i++)
            {
                var c = deployed[i];
                // 마지막 캐릭이 나머지를 흡수해 총합을 정확히 보존한다(정수 나눗셈 손실 방지).
                long share = (i == deployed.Count - 1)
                    ? totalExp - distributed
                    : totalExp * c.Level / levelSum;
                distributed += share;
                int up = AddExp(c, share);
                lines.Add(up > 0
                    ? $"{c.Name} +{share} EXP → Lv.{c.Level}"
                    : $"{c.Name} +{share} EXP");
            }
            Save();
            return lines;
        }

        /// <summary>
        /// 캐릭터가 현재 출전 가능한 상태인지 확인한다.
        /// 삭제되었거나 회복 중이면 false.
        /// </summary>
        public static bool IsAvailable(CharacterRecord character)
        {
            if (character == null || character.IsDeleted)
                return false;

            // 회복 중인가?
            if (character.RecoveryEndTime > 0)
            {
                long currentTime = GetCurrentUnixTime();
                if (currentTime < character.RecoveryEndTime)
                    return false;  // 회복 중

                // 회복이 끝났으므로 상태 초기화
                character.RecoveryEndTime = 0;
            }

            return true;
        }

        /// <summary>
        /// 사망을 기록한다.
        /// isPvp가 true면 사망 카운트를 올리지 않는다(§4).
        /// 3회 사망 도달 시 캐릭터를 삭제 처리한다.
        /// </summary>
        public static void RegisterDeath(CharacterRecord character, bool isPvp = false)
        {
            if (character == null || character.IsDeleted)
                return;

            // PvP 사망은 카운트하지 않음 (§4)
            if (isPvp)
                return;

            // 회복 기간 설정 (1일 = 86,400초)
            long currentTime = GetCurrentUnixTime();
            character.RecoveryEndTime = currentTime + (long)(RecoveryDurationSeconds / TimeScale);

            // 사망 카운트 증가
            character.DeathCount++;

            // 3회 사망 = 삭제 (§4)
            if (character.DeathCount >= 3)
            {
                character.IsDeleted = true;
                character.DeathCount = 3;  // 상한 유지
                // 장비·소지품은 캐릭터와 함께 소멸 (별도 인벤토리 시스템에서 처리)
                Debug.Log($"[목숨] {character.Name}이(가) 삭제되었습니다. (3회 사망)");
            }
            else
            {
                Debug.Log($"[목숨] {character.Name} 사망: {character.DeathCount}/3 회복 기간 시작");
            }

            Save();
        }

        /// <summary>
        /// 삭제되지 않은 캐릭터 수. 회복·수비·스케줄 중도 생존으로 센다(§3 긴급 재건 조건).
        /// </summary>
        public static int LivingCount()
        {
            EnsureLoaded();
            int n = 0;
            for (int i = 0; i < _characters.Count; i++)
                if (!_characters[i].IsDeleted) n++;
            return n;
        }

        /// <summary>지금 살아있는 긴급 재건 캐릭터. 없으면 null.</summary>
        public static CharacterRecord ActiveRescue()
        {
            EnsureLoaded();
            for (int i = 0; i < _characters.Count; i++)
            {
                var c = _characters[i];
                if (c.IsRescue && !c.IsDeleted) return c;
            }
            return null;
        }

        /// <summary>
        /// 생존 0명일 때만 Lv1 기본직업 1명을 무료로 준다.
        /// 살아있는 재건이 이미 있으면 그걸 돌려주고 새로 만들지 않는다.
        /// </summary>
        public static CharacterRecord EnsureEmergencyRecruit()
        {
            EnsureLoaded();
            var active = ActiveRescue();
            if (active != null) return active;
            if (LivingCount() > 0) return null;

            int n = 0;
            for (int i = 0; i < _characters.Count; i++)
                if (_characters[i].IsRescue) n++;
            var rescue = new CharacterRecord($"재건{n + 1}", "딜", 1) { IsRescue = true };
            _characters.Add(rescue);
            Save();
            Debug.Log($"[목숨] 긴급 재건 {rescue.Name} (딜 Lv1) — 생존 0명");
            return rescue;
        }

        /// <summary>
        /// PvE 패배의 생산 경계. 인자로 받은 출전 멤버만 사망하고, 그 뒤 생존이 0이면
        /// 긴급 재건 1명을 붙인다. PvP는 목숨·재건 모두 그대로다.
        /// </summary>
        public static PveDefeatReport ApplyWipe(IReadOnlyList<CharacterRecord> members, bool isPvp = false)
        {
            var report = new PveDefeatReport();
            EnsureLoaded();
            if (!isPvp && members != null)
            {
                var seen = new HashSet<string>();
                for (int i = 0; i < members.Count; i++)
                {
                    var ch = members[i];
                    if (ch == null || ch.IsDeleted) continue;
                    if (!string.IsNullOrEmpty(ch.Id) && !seen.Add(ch.Id)) continue;
                    RegisterDeath(ch, isPvp: false);
                    report.FallenNames.Add(ch.Name);
                    if (ch.IsDeleted) report.DeletedNames.Add(ch.Name);
                }
            }

            if (LivingCount() == 0)
            {
                var granted = EnsureEmergencyRecruit();
                if (granted != null)
                {
                    report.RescueGranted = true;
                    report.RescueName = granted.Name;
                }
            }
            report.LivingCount = LivingCount();
            return report;
        }

        /// <summary>
        /// 부활초를 사용하여 사망 카운트를 1 차감한다.
        /// 부활초 보유량이 없으면 false를 반환한다.
        /// 이미 삭제된 캐릭터는 부활초로 복구 불가(환생석 필요, §4).
        /// </summary>
        public static bool UseRevivePotion(CharacterRecord character)
        {
            if (character == null || character.IsDeleted)
                return false;

            if (character.DeathCount == 0)
                return false;  // 사망하지 않았으면 부활초 불필요

            // 소비는 GameState를 거친다 — 없으면 여기서 false
            if (!GameState.Consume(Economy.LifeItem.RevivalTea))
                return false;

            character.DeathCount--;
            character.RecoveryEndTime = 0;  // 회복 기간 취소
            Save();

            Debug.Log($"[부활초] {character.Name}의 사망 카운트 차감: {character.DeathCount + 1} → {character.DeathCount} (남은 부활초: {GetRevivePotions()})");
            return true;
        }

        /// <summary>
        /// 환생석으로 **삭제된** 캐릭터를 되돌린다(§4, 영묘).
        ///
        /// 부활초와 다른 자리다: 부활초는 사망 카운트를 1 깎아 **삭제를 미루는** 것이고,
        /// 환생석은 이미 삭제된 것을 **되돌리는** 것이다. 그래서 조건도 정반대다
        /// (`IsDeleted == false`를 요구하는 부활초 vs `true`를 요구하는 여기).
        ///
        /// 돌아온 캐릭터는 사망 카운트 0에서 다시 시작한다 — 3회 사망의 무게가
        /// 환생 한 번으로 사라지면 목숨 시스템(§4)이 성립하지 않으므로, **환생석 자체가
        /// 희소한 것**(10층 보스 드랍)으로 균형을 잡는다.
        ///
        /// ⚠️ 기획서 §4는 "장비는 함께 돌아오지 않는다"고 정했다. 지금은 장비 시스템이
        ///    없어 그 조항이 구현할 것이 없다 — 장비가 생기면 **여기서 장비를 비워야 한다.**
        /// </summary>
        public static bool UseRebornStone(CharacterRecord character)
        {
            if (character == null || !character.IsDeleted)
                return false;                      // 삭제되지 않은 캐릭터엔 쓸 이유가 없다

            if (!GameState.Consume(Economy.LifeItem.RebornStone))
                return false;

            character.IsDeleted = false;
            character.DeathCount = 0;
            character.RecoveryEndTime = 0;
            Save();

            Debug.Log($"[환생석] {character.Name} 복구 — 사망 카운트 0으로 재시작 " +
                      $"(남은 환생석: {GetRebornStones()})");
            return true;
        }

        /// <summary>환생석 보유량. 단일 소스는 `GameState.Bag`이다(부활초와 같은 이유).</summary>
        public static int GetRebornStones() => GameState.Bag.GetCount(Economy.LifeItem.RebornStone);

        /// <summary>삭제되어 환생 대상이 되는 캐릭터들.</summary>
        public static List<CharacterRecord> GetDeletedCharacters()
        {
            EnsureLoaded();
            return _characters.FindAll(c => c.IsDeleted);
        }

        /// <summary>
        /// 부활초 개수를 얻는다.
        ///
        /// 단일 소스는 `GameState.Bag`이다. 예전에는 여기 별도 카운터(`_revivePotions`)가 있어
        /// **전투 드랍(GameState.Gain)과 캐릭터 화면이 서로 다른 숫자를 보고 있었다** —
        /// 드랍으로 받은 부활초가 화면에 안 나타나고, 여기서 쓴 것이 소지품에서 안 줄었다.
        /// 상한(§18-4)도 Economy가 이미 강제하므로 상한 상수를 두 곳에 둘 이유가 없다.
        /// </summary>
        public static int GetRevivePotions() => GameState.Bag.GetCount(Economy.LifeItem.RevivalTea);

        /// <summary>
        /// 부활초를 획득한다. 상한 3개 초과는 거부한다(사재기 방지, §4).
        /// 초과분은 소실되지 않고 획득이 거부되어 "억울함"을 방지한다.
        /// </summary>
        public static bool AddRevivePotion(int count = 1)
        {
            bool ok = GameState.Gain(Economy.LifeItem.RevivalTea, count);
            if (!ok)
                Debug.LogWarning("[부활초] 상한(3개) 도달. 추가 획득 거부.");
            else
                Debug.Log($"[부활초] 획득: {count}개 (현재: {GetRevivePotions()})");
            return ok;
        }

        /// <summary>
        /// 캐릭터의 남은 회복 시간을 초 단위로 반환한다.
        /// 회복 중이 아니면 0을 반환한다.
        /// </summary>
        public static int GetRecoveryTimeRemaining(CharacterRecord character)
        {
            if (character == null || character.RecoveryEndTime == 0)
                return 0;

            long currentTime = GetCurrentUnixTime();
            long remaining = character.RecoveryEndTime - currentTime;

            return remaining > 0 ? (int)remaining : 0;
        }

        /// <summary>
        /// 현재 Unix 타임스탬프를 반환한다.
        /// 배속 상수로 스케일할 수 있게 한다(테스트용).
        /// </summary>
        private static long GetCurrentUnixTime()
        {
            return (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds * TimeScale);
        }

        /// <summary>
        /// 회복 시간을 "HH:MM:SS" 형식으로 포맷한다.
        /// </summary>
        public static string FormatRecoveryTime(int seconds)
        {
            int hours = seconds / 3600;
            int minutes = (seconds % 3600) / 60;
            int secs = seconds % 60;
            return $"{hours:D2}:{minutes:D2}:{secs:D2}";
        }
    }
}
