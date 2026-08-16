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
        private bool summonActive;            // 소환 몹이 아직 정리 안 됐으면 위험 슬롯을 잡고 있다
        private float summonGraceUntil;       // 스폰 직후 한 프레임은 「살아 있음 0」으로 오판 방지
        // 네거티브 컨트롤: BOSS_NO_SUMMON=1이면 소환 기믹을 무력화한다 —
        // 소환을 끄면 「소환 몹이 준 파티 피해」가 0으로 떨어지는지로 배선을 검증한다.
        private bool summonDisabled;

        // 네거티브 컨트롤: BOSS_NO_DPS=1이면 파티 피해가 보스 HP에 반영되지 않는다 —
        // 이걸 끄면 보스 HP가 안 줄어 OnBossDefeated가 안 떠 층이 안 돌파되는지로 배선을 검증한다.
        private bool bossDpsDisabled;

        // 네거티브 컨트롤: 장판의 실제 파티 피해와 힐체크 피해 보고를 함께 끈다.
        private bool floorAoeDisabled;

        // 힐 체크 (지속 광역딜 기믹)
        private float healCheckDuration;
        private float healCheckElapsed;
        private float requiredPartyHealing;      // 문서에 없음, 임시
        private float actualPartyHealing;
        private bool healCheckActive;

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
            UpdateFloorAOEs(Time.deltaTime);
            UpdateBosses();
        }

        // ===== PUBLIC METHODS =====

        /// <summary>
        /// 보스전 시작
        /// </summary>
        /// <param name="targetTimeOverride">
        /// 목표 처치 시간(초)을 직접 지정한다. 던전 종점 보스가 이걸 쓴다 —
        /// 몬스터문서 §7이 "**던전 보스 HP = 5인 파티 총 DPS × 75초**"로 못박았는데,
        /// 탑 층수에서 유도하면 T4 던전이 35층 보스(300초)가 되어 파티가 18초 만에 전멸한다(실측).
        /// 던전 난이도는 **던전 티어**에서 나와야지 탑 진행도에서 나오면 안 된다.
        /// </param>
        public void Begin(int floor, int bossCount, float targetTimeOverride = 0f)
        {
            _active = this;
            this.currentFloor = floor;
            this.bossCount = Mathf.Clamp(bossCount, 1, 3);  // §10-7: 최대 3마리
            this.isActive = true;
            this.elapsedTime = 0f;
            this.activeDangerMechanicsCount = 0;
            this.activeFloorAOEs.Clear();
            this.actualPartyHealing = 0f;
            // 첫 기믹은 곧바로 터뜨리지 않는다 — 진입 직후 즉사는 학습 기회가 없다.
            // 파티가 자리를 잡을 시간을 주고 시작한다.
            this._nextGimmickAt = 6f;
            this._gimmickCursor = 0;
            this.summonActive = false;
            this.summonDisabled = System.Environment.GetEnvironmentVariable("BOSS_NO_SUMMON") == "1";
            this.bossDpsDisabled = System.Environment.GetEnvironmentVariable("BOSS_NO_DPS") == "1";
            this.floorAoeDisabled = System.Environment.GetEnvironmentVariable("BOSS_NO_AOE") == "1";
            if (summonDisabled)
                Debug.Log("[BossBattle] BOSS_NO_SUMMON=1 — 소환 기믹 비활성(네거티브 컨트롤)");
            if (bossDpsDisabled)
                Debug.Log("[BossBattle] BOSS_NO_DPS=1 — 파티의 보스 피해 비활성(네거티브 컨트롤)");
            if (floorAoeDisabled)
                Debug.Log("[BossBattle] BOSS_NO_AOE=1 — 장판 피해 비활성(네거티브 컨트롤)");

            // §18-11: 목표 시간 계산
            // 5층 90초, 10층 180초, 50층+ 300초
            // 간단히: 5층 단위로 90초 + (층 / 10 - 1) × 90초
            if (targetTimeOverride > 0f)
                targetClearTime = targetTimeOverride;
            else if (floor <= 5)
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
            healCheckActive = false;
            // 💡 힐 체크 요구량 — 문서에 수치가 없어 **실측 처리량**에서 유도한다.
            // 사제 치유의 파동은 5인에게 14씩(쿨 1.4초) → 약 50/초. 15초면 750이 상한이다.
            // 그 70%인 525를 요구선으로 둔다 — 힐러가 제 일을 하면 통과하고, 놀거나 죽으면 실패한다.
            // 예전 값(basePartyDps × 50 = 5,000)은 상한의 6.7배라 **어떤 파티도 통과할 수 없었다**.
            requiredPartyHealing = 0f;      // 창이 열릴 때 들어온 피해에서 정해진다(아래 ReportDamageToActive)
            windowDamage = 0f;

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

        /// <summary>
        /// 지금 도는 보스전에 회복량을 보고한다.
        /// 전투 코드(W3Party)가 BossBattle 인스턴스를 몰라도 되게 정적 통로를 둔다 —
        /// 참조를 넘기게 하면 그 배선을 잊는 순간 기믹이 조용히 죽는다(실제로 죽어 있었다).
        /// </summary>
        static BossBattle _active;
        public static bool IsActive => _active != null && _active.isActive;
        public static bool IsEnraged => _active != null && _active.isActive && _active.isEnraged;

        /// <summary>실루엣이 때리는 쪽을 보게 한다. 슬롯 스프라이트는 꺼져 있다.</summary>
        public static void FaceActive(int index, bool flipX)
        {
            if (_active == null || _active.bosses == null) return;
            if (index < 0 || index >= _active.bosses.Count) return;
            var view = _active.bosses[index].view;
            if (view != null) view.flipX = flipX;
        }
        public static void ReportHealingToActive(float amount)
        {
            if (_active != null && _active.isActive) _active.ReportPartyHealing(amount);
        }

        /// <summary>
        /// 파티가 받은 피해를 보고한다. 힐 체크의 요구량이 여기서 나온다.
        ///
        /// 💡 요구량을 **절대 수치로 두면 안 된다**: 파티가 만피면 회복이 0으로 잡혀
        /// 아무리 잘해도 통과할 수 없다(실측: 15초 창에서 회복 21 / 요구 525).
        /// 「들어온 피해의 일정 비율을 회복으로 되받아라」가 §10-5 힐 체크의 실제 의미다 —
        /// 보스가 안 때리면 요구도 0이 되어 억지 실패가 생기지 않는다.
        /// </summary>
        public static void ReportDamageToActive(float amount)
        {
            if (_active != null && _active.isActive && _active.healCheckActive && amount > 0f)
                _active.windowDamage += amount;
        }
        float windowDamage;

        /// <summary>활성 힐체크 창에 실제 보고된 피해. HUD와 실행 QA가 같은 값을 읽는다.</summary>
        public static float ActiveHealCheckWindowDamage =>
            _active != null && _active.isActive && _active.healCheckActive ? _active.windowDamage : 0f;

        /// <summary>현재 보스들의 남은 HP 합. 실행 QA와 HUD가 같은 전투 상태를 읽는다.</summary>
        public static float ActiveTotalHp
        {
            get
            {
                if (_active == null || !_active.isActive || _active.bosses == null) return 0f;
                return _active.bosses.Where(b => b.isActive).Sum(b => Mathf.Max(0f, b.currentHp));
            }
        }

        /// <summary>
        /// W3Party에서 실제로 낸 피해를 현재 보스에 전달한다. 다중 보스는 앞의 생존 보스부터
        /// 순서대로 받으며 초과 피해는 다음 보스로 넘어간다.
        /// </summary>
        public static void ReportPartyDamageToActive(float amount)
        {
            if (_active == null || !_active.isActive || _active.bossDpsDisabled || amount <= 0f) return;

            float remaining = amount;
            foreach (var boss in _active.bosses)
            {
                if (!boss.isActive || remaining <= 0f) continue;
                float dealt = Mathf.Min(boss.currentHp, remaining);
                boss.currentHp -= dealt;
                remaining -= dealt;
                if (boss.currentHp <= 0f)
                {
                    _active.UpdatePhase(boss);
                    boss.currentHp = 0f;
                    boss.isActive = false;
                    if (boss.view != null) boss.view.gameObject.SetActive(false);
                }
                else
                {
                    _active.UpdatePhase(boss);
                }
            }

            if (_active.bosses.All(b => !b.isActive)) _active.OnAllBossesDefeated();
        }

        /// <summary>W3의 보스 타깃 슬롯 하나가 받은 피해만 해당 보스에 반영한다.</summary>
        public static bool TryReportPartyDamageToActive(int bossIndex, float amount)
        {
            if (_active == null || !_active.isActive || _active.bossDpsDisabled || amount <= 0f
                || bossIndex < 0 || bossIndex >= _active.bosses.Count) return false;
            var boss = _active.bosses[bossIndex];
            if (!boss.isActive) return false;
            boss.currentHp = Mathf.Max(0f, boss.currentHp - amount);
            _active.UpdatePhase(boss);
            if (boss.currentHp <= 0f)
            {
                boss.isActive = false;
                if (boss.view != null) boss.view.gameObject.SetActive(false);
                if (_active.bosses.All(b => !b.isActive)) _active.OnAllBossesDefeated();
            }
            return true;
        }

        /// <summary>BattleScreen이 W3 게임 모드를 초기화한 뒤 실제 공격 타깃을 연결한다.</summary>
        public void AttachCombatTargets()
        {
            global::W3Party.ConfigureBossTargetsToActive(
                bosses.Select(b => new global::W3Party.BossTarget(b.index, b.maxHp,
                    new Vector2(b.position.x, b.position.y))).ToArray());
        }

        private void OnDestroy()
        {
            if (_active == this) _active = null;
        }

        /// <summary>회복이 피해의 이만큼은 되어야 통과. 💡 문서 미정 — 실측으로 조정할 값.</summary>
        public const float HealCheckRatio = 0.6f;

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
                SpawnBossView(boss, i, bossCount);
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
            if (!healCheckActive) return;

            healCheckElapsed += Time.deltaTime;

            // 시간 만료
            if (healCheckElapsed >= healCheckDuration)
            {
                OnHealCheckFailed();
            }
        }

        // 기믹 발동 주기(초). 페이즈가 오를수록 짧아진다 — §10-5 "페이즈마다 패턴이 바뀌어
        // 한 가지 대응으로 못 버틴다". 값은 문서에 없어 여기서 제안한다(§18에 앵커 없음).
        private const float GIMMICK_INTERVAL_BASE = 12f;
        private const float GIMMICK_INTERVAL_PER_PHASE = 2.5f;
        private float _nextGimmickAt;
        private int _gimmickCursor;

        private void UpdateBosses()
        {
            int activeCount = 0;
            foreach (var boss in bosses)
            {
                if (boss.isActive)
                {
                    activeCount++;
                    UpdatePhase(boss);
                }
            }

            if (activeCount == 0)
            {
                OnAllBossesDefeated();
                return;
            }

            // 소환 슬롯 해제: 소환한 쫄이 전부 정리되면 위험 슬롯을 돌려줘 기믹이 다시 돌 수 있게 한다.
            // 이 해제가 없으면 소환은 판당 한 번 터지고 슬롯을 영영 물고 있다(발동만 하고 안 도는 상태).
            if (summonActive && elapsedTime > summonGraceUntil
                && global::W3Party.SummonedAliveOnActive() == 0)
            {
                summonActive = false;
                activeDangerMechanicsCount = Mathf.Max(0, activeDangerMechanicsCount - 1);
                Debug.Log("[BossBattle] 소환 쫄 정리 완료 — 위험 슬롯 해제");
            }

            // ── 기믹 발동. **이 배선이 통째로 없었다**(2026-08-15 발견: 기믹 3종이 정의만
            //    되고 호출부가 0곳). §10-5는 "1인 클리어 불가를 수치가 아니라 기믹으로
            //    구현한다"고 정했는데, 기믹이 안 돌면 그 확정이 코드에서 성립하지 않는다.
            if (elapsedTime >= _nextGimmickAt)
            {
                FireNextGimmick();
                int phase = bosses.Count > 0 ? bosses[0].currentPhase : 0;
                float interval = Mathf.Max(4f, GIMMICK_INTERVAL_BASE - phase * GIMMICK_INTERVAL_PER_PHASE);
                _nextGimmickAt = elapsedTime + interval;
            }
        }

        /// <summary>
        /// HP 구간으로 페이즈를 올린다(§10-5 기믹 #6 "페이즈 전환").
        /// 페이즈가 오르면 활성 스킬이 하나 늘고(`GetSkillCountForPhase`) 기믹 주기도 짧아진다.
        /// </summary>
        /// <summary>
        /// 보스를 화면에 세운다.
        ///
        /// **여기가 없어서 보스전에 보스가 없었다.** `BossInstance`는 HP·페이즈·좌표까지
        /// 가진 실체인데 그리는 코드가 0곳이라, 기믹(장판·소환·회복)만 도는 유령 전투였다.
        /// 아트도 `boss_0`(옛 3D 플레이스홀더) 한 장뿐이라 실루엣 구분 자체가 불가능했다 —
        /// 그래서 §0-B의 「실루엣 4종 변주」 아트를 뽑아 여기에 물린다.
        ///
        /// 층으로 실루엣을 고른다(§0-B: 전용 모델을 만들지 않고 크기·색으로 변주).
        /// </summary>
        private void SpawnBossView(BossInstance boss, int i, int total)
        {
            var bank = SpriteBank.Load();
            if (bank?.Boss == null || bank.Boss.Length == 0)
            {
                Debug.LogWarning("[BossBattle] 보스 스프라이트 없음 — 보스가 안 보인다");
                return;
            }

            var go = new GameObject($"Boss_{i}");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = bank.Boss[Mathf.Abs(currentFloor / 5 + i) % bank.Boss.Length];
            sr.sharedMaterial = bank.Mat;
            // 층이 높을수록 크게 — §0-B가 말한 "크기 변주"가 이것이다.
            float scale = 1f + Mathf.Min(0.6f, currentFloor * 0.01f);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            // 여러 마리면 좌우로 벌려 세운다(§10-5 다수 보스).
            float span = total > 1 ? (i - (total - 1) * 0.5f) * 4.5f : 0f;
            boss.position = new Vector3(span, 5.5f, 0f);
            go.transform.position = new Vector3(boss.position.x,
                                                boss.position.y * StressTest.ISO_Y, 0f);
            sr.sortingOrder = 1000 - Mathf.RoundToInt(boss.position.y * 10f);   // 유닛과 같은 척도
            boss.view = sr;
        }

        private void UpdatePhase(BossInstance boss)
        {
            if (boss.phaseCount <= 1) return;
            // 페이즈를 HP로 균등 분할한다 — 3페이즈면 66%·33%에서 전환
            float ratio = boss.maxHp > 0f ? boss.currentHp / boss.maxHp : 1f;
            int want = Mathf.Clamp(
                Mathf.FloorToInt((1f - ratio) * boss.phaseCount), 0, boss.phaseCount - 1);
            if (want <= boss.currentPhase) return;   // 되돌아가지 않는다(회복해도 페이즈는 유지)

            boss.currentPhase = want;
            remainingSkillCount = boss.GetSkillCountForPhase();
            OnBossPhaseChange?.Invoke((want + 1) / (float)boss.phaseCount);
            Debug.Log($"[보스] 페이즈 {want + 1}/{boss.phaseCount} 진입 — 활성 스킬 {remainingSkillCount}개");
        }

        /// <summary>
        /// 기믹을 하나씩 돌아가며 발동한다.
        /// ⚠️ 동시 상한(`MAX_SIMULTANEOUS_DANGER_MECHANICS` = 2, §10-7)을 지킨다 —
        /// 상한이 없으면 "실수 1회가 즉사"가 아니라 "아무것도 못 하고 죽음"이 된다.
        /// </summary>
        private void FireNextGimmick()
        {
            if (activeDangerMechanicsCount >= MAX_SIMULTANEOUS_DANGER_MECHANICS) return;

            switch (_gimmickCursor % 3)
            {
                case 0: TriggerFloorAOE(); break;      // #1 동시 다발 장판
                case 1: TriggerSummonMobs(); break;    // #2 분리 소환
                default: TriggerHealCheck(); break;    // #4 지속 광역딜(힐 체크)
            }
            _gimmickCursor++;
        }

        // ===== PRIVATE: 기믹 구현 =====

        /// <summary>
        /// 기믹 1: 동시 장판 (동시 다발 장판)
        /// </summary>
        private void TriggerFloorAOE(int count = 2)
        {
            if (floorAoeDisabled) return;

            if (activeDangerMechanicsCount >= MAX_SIMULTANEOUS_DANGER_MECHANICS)
            {
                Debug.Log("[BossBattle] Floor AoE blocked - danger mechanics at limit");
                return;
            }

            activeDangerMechanicsCount++;

            var partyTargets = global::W3Party.FloorAoeTargetsToActive(count);
            for (int i = 0; i < count; i++)
            {
                Vector2 target = i < partyTargets.Length
                    ? partyTargets[i]
                    : new Vector2(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-5f, 5f));
                var aoe = new FloorAOE
                {
                    position = new Vector3(target.x, 0f, target.y),
                    warningDuration = 1f,    // 예고 시간 (§10-5: 예고 표식 필수)
                    damageRadius = 2f,
                    damage = 30f              // 임시
                };
                aoe.elapsedTime = 0f;
                activeFloorAOEs.Add(aoe);
                FxPool.PlayStatus(0, new Vector2(aoe.position.x, aoe.position.z), 1.8f);
            }

            Debug.Log($"[BossBattle] Triggered Floor AoE x{count}, active danger mechanics: {activeDangerMechanicsCount}");
        }

        private void UpdateFloorAOEs(float dt)
        {
            bool resolvedAny = false;
            for (int i = activeFloorAOEs.Count - 1; i >= 0; i--)
            {
                var aoe = activeFloorAOEs[i];
                aoe.elapsedTime += dt;
                if (aoe.elapsedTime < aoe.warningDuration) continue;

                float applied = global::W3Party.ApplyPartyAreaDamageToActive(
                    new Vector2(aoe.position.x, aoe.position.z), aoe.damageRadius, aoe.damage);
                activeFloorAOEs.RemoveAt(i);
                resolvedAny = true;
                Debug.Log($"[BossBattle] Floor AoE resolved - party HP damage {applied:F1}");
            }
            if (resolvedAny && activeFloorAOEs.Count == 0)
                activeDangerMechanicsCount = Mathf.Max(0, activeDangerMechanicsCount - 1);
        }

        /// <summary>
        /// 기믹 2: 쫄 소환 (분리 소환)
        /// </summary>
        private void TriggerSummonMobs(int mobCount = 5)
        {
            // 네거티브 컨트롤: 소환을 끄면 위험 슬롯도 잡지 않고 조용히 지나간다.
            if (summonDisabled) return;

            if (activeDangerMechanicsCount >= MAX_SIMULTANEOUS_DANGER_MECHANICS)
            {
                Debug.Log("[BossBattle] Mob summon blocked - danger mechanics at limit");
                return;
            }

            activeDangerMechanicsCount++;
            summonActive = true;
            summonGraceUntil = elapsedTime + 0.5f;

            // 예전엔 빈 GameObject만 만들고 끝나서 소환 기믹이 파티에 아무 위협도 아니었다
            // (2026-08-15 발견). 실제 W3Party 전투에 쫄을 밀어넣어 「§10-5 분리 소환」이
            // 파티 피해로 성립하게 한다. W3Party가 도는 판이 아니면(예: 검증 하네스) 조용히 무시된다.
            global::W3Party.SummonMobsToActive(mobCount);
            FxPool.PlayStatus(4, Vector2.zero, 2.0f);

            Debug.Log($"[BossBattle] Triggered mob summon x{mobCount} → W3Party, active danger mechanics: {activeDangerMechanicsCount}");
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
            windowDamage = 0f;          // 이 창에서 들어온 피해만 센다
            healCheckActive = true;
            FxPool.PlayStatus(6, Vector2.zero, 2.0f);

            Debug.Log($"[BossBattle] Triggered Heal Check - 회복이 이 창의 피해 × {HealCheckRatio:P0} 이상이어야 한다, {healCheckDuration}s");
        }

        // ===== PRIVATE: 이벤트 핸들러 =====

        private void OnRageActivated()
        {
            Debug.Log($"[BossBattle] ENRAGED! Remaining time: {elapsedTime:F1}s / {rageTimerDuration:F1}s");
            // 실제 구현: 보스 공격 강화, 이펙트 등
        }

        private void OnHealCheckFailed()
        {
            // 요구량은 **이 창에서 실제로 들어온 피해**에서 나온다.
            // 절대 수치로 두면 파티가 만피일 때 회복이 0으로 잡혀 통과가 불가능해진다(실측).
            requiredPartyHealing = windowDamage * HealCheckRatio;

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
            healCheckActive = false;
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
            /// <summary>화면에 선 보스. 없으면 **기믹만 돌고 보스는 안 보인다**(2026-08-15까지 그랬다).</summary>
            public SpriteRenderer view;
            /// <summary>월드 좌표. 장판·소환이 보스 자리를 기준으로 터져야 위치가 뜻을 갖는다.</summary>
            public Vector3 position;

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
