using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 단계 1 관문 측정 — 탑 50층 성장 곡선(원장 §22 · ORDERS③ 승인 2026-08-25).
    ///
    /// 재는 것은 셋이다. 전부 **실제 런타임 코드**에서 뽑는다. 여기서 공식을 다시 쓰지 않는다.
    ///   ①난이도 곡선 — 층별 목표 시간(<see cref="RaidScale.TargetSeconds"/>)과
    ///     그 층 보스의 실제 HP(<see cref="BossBattle.ActiveTotalHp"/>), 그리고 그 둘에서 나오는
    ///     **필요 DPS = HP ÷ 목표 시간**.
    ///   ②성장 곡선 — 레벨별 능력배수(<see cref="W3Party.LevelStatMultiplier"/>)와
    ///     그 레벨에 도달하는 데 드는 사냥 시간(<see cref="LifeSystem.ExpToNext"/> ·
    ///     <see cref="Economy.WaveHuntExp"/>).
    ///   ③파티 실DPS(G3, ORDERS③) — 실판 시뮬(<see cref="W3Party.Step"/>)로 기대 레벨·전직
    ///     파티가 최상층 보스를 때려 **실측 DPS = 보스 HP ÷ 처치 시간**을 뽑고 필요 DPS와 대조한다.
    ///
    /// 판정은 사람이 한다. 이 검사는 **수치와 CSV만** 남기고, 아래 세 가지가 성립하지 않으면
    /// FAIL로 표시한다 — 관문 질문이 "50층까지 성장 곡선이 매끄러운가"이기 때문이다.
    ///   G1. 층이 오르면 필요 DPS도 오른다(1층과 최상층이 같으면 곡선이 아니다)
    ///   G2. 5시간 사냥으로 도달하는 레벨이 최상층 요구에 닿는다
    ///   G3. 기대 레벨·전직 파티의 실DPS가 최상층 필요 DPS에 닿고(목표 시간 내 처치),
    ///       약한 파티(Lv1 기본직)는 같은 게이트를 통과하지 못한다(판별력 네거티브).
    ///       권장 파티 픽스처는 장비(TryGrantDrop·TryEquip)와 합성(AbsorbedBoons→Fusion.CombatOf)
    ///       실제 경로를 심는다. QA_NO_G3_GEAR=1이면 옛 베어 로스터.
    ///
    ///   Unity -batchmode -quit -nographics -projectPath &lt;unity_meas&gt; \
    ///         -executeMethod AshesToStars.TowerClimbCurveMeasure.Run
    /// </summary>
    public static class TowerClimbCurveMeasure
    {
        public const int TopFloor = 50;
        /// <summary>관문 ②의 기준 — "5시간 연속 플레이가 지루하지 않은가"(§22).</summary>
        public const float SessionHours = 5f;
        /// <summary>G3 권장 픽스처에서 장비·합성을 빼고 옛 베어 로스터로 되돌린다.</summary>
        public const string EnvNoG3Gear = "QA_NO_G3_GEAR";

        struct Row
        {
            public int Floor;
            public float TargetSec;
            public float BossHp;
            public float RequiredDps;
            public int UnlockedTier;
            public float TierMul;
            public int LevelAtEntry;
            public float LevelMul;
            public double HuntHoursToLevel;
        }

        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool ok, string what)
        {
            if (!ok) _fail++;
            _log.AppendLine((ok ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Tower Climb Curve (50F)")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            GameObject fixtureGo = null;
            try
            {
                fixtureGo = BuildFixture(out BossBattle boss, out global::W3Party party);
                var rows = new Row[TopFloor];
                for (int f = 1; f <= TopFloor; f++)
                    rows[f - 1] = Measure(boss, f);

                // ── G1. 층이 오르면 어려워지는가 ──────────────────────────────
                float dps1 = rows[0].RequiredDps;
                float dpsTop = rows[TopFloor - 1].RequiredDps;
                int steps = 0;
                for (int i = 1; i < rows.Length; i++)
                    if (!Mathf.Approximately(rows[i].RequiredDps, rows[i - 1].RequiredDps)) steps++;
                Check(dpsTop > dps1 * 1.5f,
                    $"G1 {TopFloor}층 필요 DPS가 1층의 1.5배 이상이어야 한다: 1층 {dps1:F0} → {TopFloor}층 {dpsTop:F0}");
                Check(steps >= 10,
                    $"G1 {TopFloor}층 동안 난이도가 최소 10번은 변해야 한다(계단이 아니라 곡선): 변화 {steps}회");

                // ── G2. 5시간이면 최상층에 닿는가 ──────────────────────────────
                int lv5h = LevelAfterHunting(SessionHours * 3600f, rows[TopFloor - 1].UnlockedTier);
                int lvNeed = rows[TopFloor - 1].LevelAtEntry;
                Check(lv5h >= lvNeed,
                    $"G2 5시간 사냥 레벨이 {TopFloor}층 요구 레벨 이상이어야 한다: {lv5h} vs {lvNeed}");

                // ── G3. 파티 실DPS 대조(ORDERS③) — 실판 시뮬로만 답한다 ─────────
                // 권장 파티 = 기대 레벨·전직 + 장비·합성 CombatMuls(BossHp 권장 전투력이 흡수하는 칸).
                // 약한 파티 = Lv1 기본직(베어). QA_NO_G3_GEAR면 권장도 옛 베어 로스터.
                var strong = FightProbe(party, boss, TopFloor,
                    BossHp.ExpectedLevel(TopFloor), TierForLevel(BossHp.ExpectedLevel(TopFloor)));
                var mid = FightProbe(party, boss, 25,
                    BossHp.ExpectedLevel(25), TierForLevel(BossHp.ExpectedLevel(25)));
                var weak = FightProbe(party, boss, TopFloor, 1, AdvancementTier.Basic);
                Check(strong.Killed && strong.MeasuredDps >= strong.RequiredDps,
                    $"G3 기대 레벨·전직 파티 실DPS가 {TopFloor}층 필요 DPS에 닿아야 한다" +
                    $"(목표 시간 내 처치): 실측 {strong.MeasuredDps:F0} vs 필요 {strong.RequiredDps:F0}" +
                    $"{(strong.Killed ? "" : " · 미처치")}");
                Check(!weak.PassedGate,
                    $"G3 네거티브 — 약한 파티(Lv1 기본직)는 {TopFloor}층 게이트를 통과해선 안 된다" +
                    $": 실측 {weak.MeasuredDps:F0} vs 필요 {weak.RequiredDps:F0}{(weak.Killed ? "" : " · 미처치")}");
                Check(strong.MeasuredDps > weak.MeasuredDps * 1.5f,
                    $"G3 레벨·전직 성장이 실전 DPS로 이어져야 한다: 권장 {strong.MeasuredDps:F0} vs 약한 {weak.MeasuredDps:F0}");

                string csv = BuildCsv(rows);
                string dir = Path.Combine(RepoRoot(), "output", "qa", "ashes-to-stars", "curve");
                Directory.CreateDirectory(dir);
                string csvPath = Path.Combine(dir, $"tower_climb_{TopFloor}.csv");
                File.WriteAllText(csvPath, csv, new UTF8Encoding(false));

                string json = BuildJson(rows, dps1, dpsTop, steps, lv5h, lvNeed, strong, mid, weak);
                File.WriteAllText(Path.Combine(dir, $"tower_climb_{TopFloor}.json"), json, new UTF8Encoding(false));

                Debug.Log((_fail == 0 ? "[TowerCurve] PASS" : $"[TowerCurve] FAIL {_fail}건")
                    + "\n" + _log + "\nCSV " + csvPath);
                Debug.Log("[TowerCurveJSON] " + json);
            }
            finally
            {
                if (fixtureGo != null) UnityEngine.Object.DestroyImmediate(fixtureGo);
                GameState.ResetAll();
                LifeSystem.ResetAll();
            }
            if (_fail > 0) EditorApplication.Exit(1);
        }

        // ===== 측정 =====

        static Row Measure(BossBattle boss, int floor)
        {
            // 생산 경계와 같은 순서: 층 → 목표 시간 → 보스 생성.
            GameState.SetTowerFloorForTest(floor);
            // 생산 경계(BattleScreen)와 같은 순서: 하위 레이드면 RaidScale이 덮고,
            // 아니면(=처음 오르는 층) 층 기본값 90/180/300이 그대로 목표 시간이다.
            float targetSec = RaidScale.TargetSeconds(floor);
            if (targetSec <= 0f) targetSec = RaidScale.TimeForFloor(floor);
            boss.Begin(floor, 1, targetSec);
            float hp = BossBattle.ActiveTotalHp;

            int tier = Mathf.Clamp((floor - 1) / 10, 0, Economy.TierRevenueMultiplier.Length - 1);
            int level = BossHp.ExpectedLevel(floor);
            return new Row
            {
                Floor = floor,
                TargetSec = targetSec,
                BossHp = hp,
                RequiredDps = targetSec > 0f ? hp / targetSec : 0f,
                UnlockedTier = tier,
                TierMul = Economy.TierRevenueMultiplier[tier],
                LevelAtEntry = level,
                LevelMul = global::W3Party.LevelStatMultiplier(level),
                HuntHoursToLevel = HuntHoursForLevel(level, tier),
            };
        }

        /// <summary>레벨 L까지 필요한 누적 경험치를 그 티어 사냥 속도로 나눈 시간(시간 단위).</summary>
        static double HuntHoursForLevel(int level, int tier)
        {
            double exp = 0;
            for (int lv = 1; lv < level; lv++) exp += LifeSystem.ExpToNext(lv);
            long perHour = Economy.WaveHuntExp(tier, 3600f);
            return perHour > 0 ? exp / perHour : 0;
        }

        /// <summary>그 티어에서 N초 사냥했을 때 도달하는 레벨(실제 AddExp 경로로).</summary>
        static int LevelAfterHunting(float seconds, int tier)
        {
            long exp = Economy.WaveHuntExp(tier, seconds);
            int lv = 1;
            while (lv < LifeSystem.MaxLevel && exp >= LifeSystem.ExpToNext(lv))
            {
                exp -= LifeSystem.ExpToNext(lv);
                lv++;
            }
            return lv;
        }

        // ===== 픽스처·출력 =====

        static GameObject BuildFixture(out BossBattle boss, out global::W3Party party)
        {
            // BossBattleRunSelfCheck과 같은 픽스처. 비활성으로 붙여 Awake를 한 번만 돌린다.
            var go = new GameObject("TowerClimbCurveMeasure");
            go.SetActive(false);
            party = TestAttach.AttachWithAwake<global::W3Party>(go, p => { p.GameMode = true; });
            boss = TestAttach.AttachWithAwake<BossBattle>(go);
            return go;
        }

        /// <summary>진행 단계 전직 — 캐릭터창 관문(Lv20 1차·Lv50 2차)과 같은 경계를 쓴다.</summary>
        static AdvancementTier TierForLevel(int level) =>
            level >= 50 ? AdvancementTier.Second
            : level >= 20 ? AdvancementTier.First
            : AdvancementTier.Basic;

        /// <summary>QA_NO_G3_GEAR=1이면 장비·합성을 안 심어 옛 베어 로스터가 된다.</summary>
        public static bool G3GearBlocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNoG3Gear);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// G3 권장 파티 픽스처. 레벨·전직은 항상 심고, 기본직이 아니면 실제 장비·합성 경로를 심는다.
        /// 가짜 배율을 SortieCombatant에 넣지 않는다 — Equipment.TryEquip과 AbsorbedBoons만 쓴다.
        /// </summary>
        public static void SeedRecommendedRoster(int level, AdvancementTier adv)
        {
            LifeSystem.ResetAll();
            foreach (string job in LifeSystem.BasicJobs)
            {
                var c = LifeSystem.AddBasicRecruit(job);
                if (c == null) continue;
                c.Level = level;
                c.Advancement = adv;
                if (!G3GearBlocked && adv != AdvancementTier.Basic)
                    PlantRecommendedLoadout(c);
            }
            LifeSystem.PersistRoster();
            int n = LifeSystem.GetCharacters().Count;
            int first = Mathf.Max(0, n - LifeSystem.BasicJobs.Length);
            PartyState.SetSlotsForTest(first, first + 1, first + 2, first + 3, first + 4);
        }

        /// <summary>
        /// 권장 전투력이 흡수하는 칸 — 보스 드랍 등급 장비 6부위 + 역할 계열 합성 패시브.
        /// 배율은 Equipment.HpMulOf·Fusion.CombatOf가 계산한다.
        /// </summary>
        static void PlantRecommendedLoadout(CharacterRecord character)
        {
            if (character == null) return;
            foreach (var rec in Equipment.Recipes)
            {
                if (rec.Slot == EquipSlot.Weapon)
                {
                    var probe = new GearItem { Slot = rec.Slot, RecipeId = rec.Id };
                    if (!EquipJob.CanWear(character, probe)) continue;
                }
                var gear = Equipment.TryGrantDrop(rec.Id, GearDrop.BossGrade);
                if (gear == null) continue;
                Equipment.TryEquip(character, gear.Id);
            }

            BoonId[] want = Fusion.RoleFamilyOf(character.Job) switch
            {
                "Tank" => new[] { BoonId.강골, BoonId.방벽 },
                "Dps" => new[] { BoonId.예리함, BoonId.집중, BoonId.분노 },
                "Healer" => new[] { BoonId.치유의손, BoonId.강골 },
                "Buffer" => new[] { BoonId.숙련, BoonId.발놀림 },
                _ => new[] { BoonId.예리함 },
            };
            Fusion.ClearAbsorbed(character);
            for (int i = 0; i < want.Length && character.AbsorbedBoons.Count < Fusion.SlotCap; i++)
                character.AbsorbedBoons.Add((int)want[i]);
        }

        struct DpsProbe
        {
            public int Floor;
            public int Level;
            public string PartyKind;      // "권장" / "약한"
            public bool Killed;           // 상한 안에 보스 처치
            public bool WipedOut;         // 파티 전멸
            public float TimeToKill;
            public float MeasuredDps;
            public float RequiredDps;
            public bool PassedGate => Killed && MeasuredDps >= RequiredDps && !WipedOut;

            public string ToJson()
            {
                var sb = new StringBuilder();
                sb.Append("{\"floor\":").Append(Floor)
                  .Append(",\"party\":\"").Append(PartyKind)
                  .Append("\",\"level\":").Append(Level)
                  .Append(",\"killed\":").Append(Killed ? "true" : "false")
                  .Append(",\"wiped\":").Append(WipedOut ? "true" : "false")
                  .Append(",\"time_to_kill\":").Append(TimeToKill.ToString("F1", CultureInfo.InvariantCulture))
                  .Append(",\"measured_dps\":").Append(MeasuredDps.ToString("F1", CultureInfo.InvariantCulture))
                  .Append(",\"required_dps\":").Append(RequiredDps.ToString("F1", CultureInfo.InvariantCulture))
                  .Append(",\"gate_pass\":").Append(PassedGate ? "true" : "false")
                  .Append('}');
                return sb.ToString();
            }
        }

        /// <summary>
        /// G3 실측 — 실판 시뮬(<see cref="W3Party.Step"/>)로 그 층 보스를 때려 실DPS를 뽑는다.
        /// 스탯은 전부 런타임 경로에서 온다: 로스터 레벨·장비·합성→<c>ApplyGameParty</c>,
        /// 보스 HP→<c>BossBattle.Begin</c>, 피해→<c>TickParty·DamageMob</c>. 여기서 공식을 다시 쓰지 않는다.
        /// </summary>
        static DpsProbe FightProbe(global::W3Party party, BossBattle boss, int floor, int level, AdvancementTier adv)
        {
            // 생산 경계와 같은 순서: 층 → 목표 시간(하위 레이드가 아니면 층 기본값).
            GameState.SetTowerFloorForTest(floor);
            float targetSec = RaidScale.TargetSeconds(floor);
            if (targetSec <= 0f) targetSec = RaidScale.TimeForFloor(floor);

            // 편성 로스터를 기대 레벨·전직·장비·합성으로 심는다 — NextStyle의 실제 스탯 경로.
            // EnsureLoaded가 폴백 기본 5인을 채울 수 있으므로 슬롯은 **내가 넣은 다섯**을 정확히 가린다.
            SeedRecommendedRoster(level, adv);
            party.ApplyGameParty();

            boss.Begin(floor, 1, targetSec);
            float maxHp = BossBattle.ActiveTotalHp;

            // 보스를 유일한 전투 대상으로 — BossAutoAttackSelfCheck와 같은 경계.
            var configure = typeof(global::W3Party).GetMethod("ConfigureBossTargets",
                BindingFlags.Instance | BindingFlags.NonPublic);
            configure.Invoke(party, new object[]
            {
                new[] { new global::W3Party.BossTarget(0, maxHp, new Vector2(0f, 4f)) }
            });

            var step = typeof(global::W3Party).GetMethod("Step",
                BindingFlags.Instance | BindingFlags.NonPublic);
            const float dt = 1f / 60f;                 // W3Party.FixedStep과 같은 값
            float cap = targetSec * 2.5f;              // 무한루프 방지 상한 — 게이트 판정은 목표 시간 기준
            party.최대시간 = cap + 60f;                  // Step 내장 상한(240s)이 목표 시간(300s)을 끊지 않게
            int maxSteps = Mathf.CeilToInt(cap / dt);
            float t = 0f;
            bool killed = false, wiped = false;
            for (int i = 0; i < maxSteps; i++)
            {
                step.Invoke(party, null);
                t += dt;
                if (BossBattle.ActiveTotalHp <= 0f) { killed = true; break; }
                if (global::W3Party.ActivePartyHp <= 0f) { wiped = true; break; }
            }

            return new DpsProbe
            {
                Floor = floor,
                Level = level,
                PartyKind = level >= BossHp.ExpectedLevel(floor) ? "권장" : "약한",
                Killed = killed,
                WipedOut = wiped,
                TimeToKill = killed ? t : cap,
                MeasuredDps = killed && t > 0f ? maxHp / t : 0f,
                RequiredDps = targetSec > 0f ? maxHp / targetSec : 0f,
            };
        }


        static string BuildCsv(Row[] rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("floor,target_sec,boss_hp,required_dps,unlocked_tier,tier_mul,"
                + "required_level,level_mul,hunt_hours_to_level,delta_hours");
            double prev = 0;
            foreach (var r in rows)
            {
                double delta = r.HuntHoursToLevel - prev;
                prev = r.HuntHoursToLevel;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0},{1:F0},{2:F0},{3:F1},{4},{5:F3},{6},{7:F2},{8:F3},{9:F3}",
                    r.Floor, r.TargetSec, r.BossHp, r.RequiredDps, r.UnlockedTier + 1,
                    r.TierMul, r.LevelAtEntry, r.LevelMul, r.HuntHoursToLevel, delta));
            }
            return sb.ToString();
        }

        static string BuildJson(Row[] rows, float dps1, float dpsTop, int steps, int lv5h, int lvNeed,
            DpsProbe strongTop, DpsProbe midStrong, DpsProbe weakTop)
        {
            var sb = new StringBuilder();
            sb.Append("{\"gate\":\"단계1-성장곡선\",\"ran_at\":\"")
              .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
              .Append("\",\"top_floor\":").Append(TopFloor)
              .Append(",\"required_dps_f1\":").Append(dps1.ToString("F1", CultureInfo.InvariantCulture))
              .Append(",\"required_dps_ftop\":").Append(dpsTop.ToString("F1", CultureInfo.InvariantCulture))
              .Append(",\"difficulty_changes\":").Append(steps)
              .Append(",\"level_after_5h\":").Append(lv5h)
              .Append(",\"level_needed_ftop\":").Append(lvNeed)
              .Append(",\"hunt_hours_ftop\":")
              .Append(rows[rows.Length - 1].HuntHoursToLevel.ToString("F2", CultureInfo.InvariantCulture))
              .Append(",\"party_dps_probes\":[")
              .Append(strongTop.ToJson()).Append(',')
              .Append(midStrong.ToJson()).Append(',')
              .Append(weakTop.ToJson())
              .Append("],\"fails\":").Append(_fail)
              .Append('}');
            return sb.ToString();
        }

        static string RepoRoot()
        {
            // <root>/projects/ashes-to-stars/<unity|unity_meas>/Assets
            var dir = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 4 && dir?.Parent != null; i++) dir = dir.Parent;
            return dir?.FullName ?? Application.dataPath;
        }
    }
}
