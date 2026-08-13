using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AshesToStars
{
    /// <summary>
    /// 보스전 독립 컴포넌트. W3Party와 분리되어 독립적으로 동작 가능.
    /// §21-2 프로토타입: 기믹 3종(동시장판/쫄소환/힐체크) + 격노 타이머
    /// </summary>
    public class BossBattle : MonoBehaviour
    {
        // ===== PUBLIC API =====

        public event Action<int> OnBossDefeated;        // 보스 클리어 (남은 목숨 수)
        public event Action OnPartyWiped;               // 파티 전멸
        public event Action<float> OnBossPhaseChange;   // 페이즈 진입 (0~1, 1이 마지막)

        // ===== 보스 수치 =====

        [SerializeField] private float basePartyDps = 100f; // T1 기준 5인 DPS (문서에 없음, 임시)

        private int currentFloor;
        private int bossCount;
        private float targetClearTime;           // §18-11: 5층 90초 / 10층 180초 / 50층+ 300초
        private float rageTimerDuration;         // §18-11: 목표 시간 × 2

        // ===== 보스 상태 =====

        private List<BossInstance> bosses;
        private int remainingSkillCount;         // 현재 페이즈의 활성 스킬 개수

        // ===== 기믹 관리 =====

        private const int MAX_SIMULTANEOUS_DANGER_MECHANICS = 2;  // §10-7: 위험 기믹 동시 발동 2개 상한
        private int activeDangerMechanicsCount;

        // 장판 (동시 장판 기믹)
        private List<FloorAOE> activeFloorAOEs;

        // 쫄 소환 (분리 소환 기믹)
        private List<GameObject> summonedMobs;

        // 힐 체크 (지속 광역딜 기믹)
        private float healCheckDuration;
        private float healCheckElapsed;
        private float requiredPartyHealing;      // 문서에 없음, 임시
        private float actualPartyHealing;

        // 격노 타이머 (§18-11: 목표 시간 × 2)
        private float rageTimer;
        private bool isEnraged;

        // ===== 내부 상태 =====

        private bool isActive;
        private float elapsedTime;

        // ===== MONOBEHAVIOUR =====

        private void Awake()
        {
            bosses = new List<BossInstance>();
            activeFloorAOEs = new List<FloorAOE>();
            summonedMobs = new List<GameObject>();
        }

        private void Update()
        {
            if (!isActive) return;

            elapsedTime += Time.deltaTime;
            UpdateRageTimer();
            UpdateHealCheckDuration();
            UpdateBosses();
        }

        // ===== PUBLIC METHODS =====

        /// <summary>
        /// 보스전 시작
        /// </summary>
        public void Begin(int floor, int bossCount)
        {
            this.currentFloor = floor;
            this.bossCount = Mathf.Clamp(bossCount, 1, 3);  // §10-7: 최대 3마리
            this.isActive = true;
            this.elapsedTime = 0f;
            this.activeDangerMechanicsCount = 0;
            this.actualPartyHealing = 0f;

            // §18-11: 목표 시간 계산
            // 5층 90초, 10층 180초, 50층+ 300초
            // 간단히: 5층 단위로 90초 + (층 / 10 - 1) × 90초
            if (floor <= 5)
                targetClearTime = 90f;
            else if (floor <= 10)
                targetClearTime = 180f;
            else
                targetClearTime = 300f;

            // §18-11: 격노 타이머 = 목표 시간 × 2
            rageTimerDuration = targetClearTime * 2f;
            rageTimer = rageTimerDuration;
            isEnraged = false;

            // 보스 생성 (§10-7)
            CreateBosses();

            // 힐 체크 (기믹 3)
            healCheckDuration = 15f;  // 문서에 없음, 임시 15초
            healCheckElapsed = 0f;
            requiredPartyHealing = basePartyDps * 50f;  // 임시, 실제는 W3 테스트로 조정 필요

            Debug.Log($"[BossBattle] Begin - Floor {floor}, Boss Count {this.bossCount}, " +
                $"Target Clear Time {targetClearTime}s, Rage Timer {rageTimerDuration}s");
        }

        /// <summary>
        /// 파티의 현재 회복량 누적 (힐 체크용)
        /// </summary>
        public void ReportPartyHealing(float amount)
        {
            actualPartyHealing += amount;
        }

        // ===== PRIVATE: 보스 생성 =====

        private void CreateBosses()
        {
            bosses.Clear();

            // §18-11: HP 계산
            // 보스 HP = 5인 파티 총 DPS × 목표 시간
            float totalPartyDps = basePartyDps;
            float singleBossHp = totalPartyDps * targetClearTime;

            // §10-7: 다중 등장 HP 보정
            // 1체 100% / 2체 65% / 3체 45%
            float hpPerBoss = singleBossHp;
            if (bossCount == 2)
                hpPerBoss = singleBossHp * 0.65f;
            else if (bossCount == 3)
                hpPerBoss = singleBossHp * 0.45f;

            // §10-5: 스킬 수 변화
            // 5층: 2→3 (총 3)
            // 10층: 2→3→4 (총 4)  + 격노
            // 50층+: 2→3→4→5 (총 5) + 격노
            int phaseCount;
            if (currentFloor <= 5)
                phaseCount = 2;  // 페이즈 1개 추가 (2→3)
            else if (currentFloor <= 10)
                phaseCount = 3;  // 페이즈 2개 추가 (2→3→4)
            else
                phaseCount = 4;  // 페이즈 3개 추가 (2→3→4→5)

            for (int i = 0; i < bossCount; i++)
            {
                var boss = new BossInstance
                {
                    index = i,
                    maxHp = hpPerBoss,
                    currentHp = hpPerBoss,
                    phaseCount = phaseCount,
                    currentPhase = 0,
                    isActive = true
                };
                bosses.Add(boss);
            }

            remainingSkillCount = 2;  // 기본 스킬 2개 (§10-5)

            Debug.Log($"[BossBattle] Created {bossCount} bosses, HP {hpPerBoss:F0} each, " +
                $"Phase count {phaseCount}, Total skills (phase 0): {remainingSkillCount}");
        }

        // ===== PRIVATE: 업데이트 루프 =====

        private void UpdateRageTimer()
        {
            if (isEnraged) return;

            rageTimer -= Time.deltaTime;
            if (rageTimer <= 0f)
            {
                isEnraged = true;
                OnRageActivated();
            }
        }

        private void UpdateHealCheckDuration()
        {
            if (healCheckElapsed > healCheckDuration)
                return;  // 이미 종료

            healCheckElapsed += Time.deltaTime;

            // 시간 만료
            if (healCheckElapsed >= healCheckDuration)
            {
                OnHealCheckFailed();
            }
        }

        private void UpdateBosses()
        {
            int activeCount = 0;
            foreach (var boss in bosses)
            {
                if (boss.isActive)
                    activeCount++;
            }

            if (activeCount == 0)
            {
                OnAllBossesDefeated();
            }
        }

        // ===== PRIVATE: 기믹 구현 =====

        /// <summary>
        /// 기믹 1: 동시 장판 (동시 다발 장판)
        /// </summary>
        private void TriggerFloorAOE(int count = 2)
        {
            if (activeDangerMechanicsCount >= MAX_SIMULTANEOUS_DANGER_MECHANICS)
            {
                Debug.Log("[BossBattle] Floor AoE blocked - danger mechanics at limit");
                return;
            }

            activeDangerMechanicsCount++;

            for (int i = 0; i < count; i++)
            {
                var aoe = new FloorAOE
                {
                    // 임시: 화면상 임의의 위치
                    position = new Vector3(
                        UnityEngine.Random.Range(-5f, 5f),
                        0f,
                        UnityEngine.Random.Range(-5f, 5f)
                    ),
                    warningDuration = 1f,    // 예고 시간 (§10-5: 예고 표식 필수)
                    damageRadius = 2f,
                    damage = 30f              // 임시
                };
                aoe.elapsedTime = 0f;
                activeFloorAOEs.Add(aoe);
            }

            Debug.Log($"[BossBattle] Triggered Floor AoE x{count}, active danger mechanics: {activeDangerMechanicsCount}");
        }

        /// <summary>
        /// 기믹 2: 쫄 소환 (분리 소환)
        /// </summary>
        private void TriggerSummonMobs(int mobCount = 3)
        {
            if (activeDangerMechanicsCount >= MAX_SIMULTANEOUS_DANGER_MECHANICS)
            {
                Debug.Log("[BossBattle] Mob summon blocked - danger mechanics at limit");
                return;
            }

            activeDangerMechanicsCount++;

            for (int i = 0; i < mobCount; i++)
            {
                // 실제 구현은 W3Party의 몹 소환 시스템을 써야 함
                // 여기서는 추적만 함
                var mobGo = new GameObject($"SummonedMob_{i}");
                mobGo.transform.parent = transform;
                summonedMobs.Add(mobGo);
            }

            Debug.Log($"[BossBattle] Triggered mob summon x{mobCount}, active danger mechanics: {activeDangerMechanicsCount}");
        }

        /// <summary>
        /// 기믹 3: 힐 체크 (지속 광역딜)
        /// </summary>
        private void TriggerHealCheck()
        {
            if (activeDangerMechanicsCount >= MAX_SIMULTANEOUS_DANGER_MECHANICS)
            {
                Debug.Log("[BossBattle] Heal check blocked - danger mechanics at limit");
                return;
            }

            activeDangerMechanicsCount++;

            healCheckElapsed = 0f;
            actualPartyHealing = 0f;

            Debug.Log($"[BossBattle] Triggered Heal Check - Required: {requiredPartyHealing:F0}, Duration: {healCheckDuration}s");
        }

        // ===== PRIVATE: 이벤트 핸들러 =====

        private void OnRageActivated()
        {
            Debug.Log($"[BossBattle] ENRAGED! Remaining time: {elapsedTime:F1}s / {rageTimerDuration:F1}s");
            // 실제 구현: 보스 공격 강화, 이펙트 등
        }

        private void OnHealCheckFailed()
        {
            if (actualPartyHealing < requiredPartyHealing)
            {
                Debug.Log($"[BossBattle] Heal Check FAILED! " +
                    $"Actual: {actualPartyHealing:F0} / Required: {requiredPartyHealing:F0}");
                // 실제: 파티에 큰 피해
                OnPartyWiped?.Invoke();
            }
            else
            {
                Debug.Log($"[BossBattle] Heal Check PASSED! " +
                    $"Actual: {actualPartyHealing:F0} / Required: {requiredPartyHealing:F0}");
            }

            activeDangerMechanicsCount = Mathf.Max(0, activeDangerMechanicsCount - 1);
        }

        private void OnAllBossesDefeated()
        {
            isActive = false;
            Debug.Log($"[BossBattle] All bosses defeated! Elapsed: {elapsedTime:F1}s / Target: {targetClearTime}s");
            OnBossDefeated?.Invoke(bossCount);
        }

        // ===== INTERNAL TYPES =====

        private class BossInstance
        {
            public int index;
            public float maxHp;
            public float currentHp;
            public int phaseCount;           // 총 페이즈 개수
            public int currentPhase;         // 0부터 시작
            public bool isActive;

            /// <summary>
            /// 페이즈별 활성 스킬 개수
            /// 기본 2개 + 페이즈당 1개 추가 (§10-5)
            /// </summary>
            public int GetSkillCountForPhase()
            {
                return 2 + currentPhase;
            }
        }

        private class FloorAOE
        {
            public Vector3 position;
            public float warningDuration;    // 예고 시간
            public float damageRadius;
            public float damage;
            public float elapsedTime;
        }
    }
}
