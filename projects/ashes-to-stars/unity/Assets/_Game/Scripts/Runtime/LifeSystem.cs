using System;
using System.Collections.Generic;
using UnityEngine;

namespace AshesToStars
{
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
        public string Job { get; set; }  // 기본 직업명 (예: "수호기사", "마법사")
        public int Level { get; set; }

        /// <summary>누적 사망 횟수 (0~3, 3이면 삭제된 상태).</summary>
        public int DeathCount { get; set; }

        /// <summary>회복 종료 시각 (Unix 타임스탬프). 0이면 회복 중이 아님.</summary>
        public long RecoveryEndTime { get; set; }

        /// <summary>삭제됨 여부.</summary>
        public bool IsDeleted { get; set; }

        public CharacterRecord(string name, string job, int level = 1)
        {
            Name = name;
            Job = job;
            Level = level;
            DeathCount = 0;
            RecoveryEndTime = 0;
            IsDeleted = false;
        }
    }

    /// <summary>
    /// 목숨 시스템의 중앙 관리자.
    /// 파티 보유 캐릭터 목록, 사망 처리, 회복 확인, 부활초 관리를 담당한다.
    /// 프로토타입이므로 PlayerPrefs와 메모리로만 저장한다.
    /// </summary>
    public static class LifeSystem
    {
        private static List<CharacterRecord> _characters = new();
        private static int _revivePotions = 3;  // 초기 부활초 3개
        private const int MaxRevivePotions = 3;
        private const int RecoveryDurationSeconds = 86400;  // 1일 = 24시간 = 86,400초

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
            _revivePotions = 3;

            // 프로토타입: 초기 캐릭터 5명
            // (기획서 §3는 시작 2캐릭이지만, 5인 파티 검증용으로 5명 생성)
            // 이유: 탑/레이드는 5인 파티가 기본 전제(§9)이고, 프로토타입에서
            // 1~5인 조합의 사망 시나리오를 테스트하려면 충분한 캐릭터가 필요.
            // (결정: 프로토타입에는 5명이 합리적)

            _characters.Add(new CharacterRecord("탱크", "수호기사", level: 10));
            _characters.Add(new CharacterRecord("딜러1", "마법사", level: 10));
            _characters.Add(new CharacterRecord("딜러2", "검사", level: 10));
            _characters.Add(new CharacterRecord("힐러", "사제", level: 10));
            _characters.Add(new CharacterRecord("버퍼", "음유시인", level: 10));
        }

        /// <summary>
        /// 캐릭터 목록을 반환한다.
        /// </summary>
        public static List<CharacterRecord> GetCharacters() => _characters;

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

            if (_revivePotions <= 0)
                return false;  // 부활초 없음

            // 부활초 사용
            _revivePotions--;
            character.DeathCount--;
            character.RecoveryEndTime = 0;  // 회복 기간 취소

            Debug.Log($"[부활초] {character.Name}의 사망 카운트 차감: {character.DeathCount + 1} → {character.DeathCount} (남은 부활초: {_revivePotions})");
            return true;
        }

        /// <summary>
        /// 부활초 개수를 얻는다.
        /// </summary>
        public static int GetRevivePotions() => _revivePotions;

        /// <summary>
        /// 부활초를 획득한다. 상한 3개 초과는 거부한다(사재기 방지, §4).
        /// 초과분은 소실되지 않고 획득이 거부되어 "억울함"을 방지한다.
        /// </summary>
        public static bool AddRevivePotion(int count = 1)
        {
            int newCount = _revivePotions + count;
            if (newCount > MaxRevivePotions)
            {
                Debug.LogWarning($"[부활초] 상한(3개) 도달. 추가 획득 거부.");
                return false;
            }

            _revivePotions = newCount;
            Debug.Log($"[부활초] 획득: {count}개 (현재: {_revivePotions}/{MaxRevivePotions})");
            return true;
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
