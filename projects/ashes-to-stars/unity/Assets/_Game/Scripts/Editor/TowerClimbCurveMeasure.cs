using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 단계 1 관문 측정 — 탑 30층 성장 곡선(원장 §22 「지금」 칸, 2026-08-18).
    ///
    /// 재는 것은 둘이다. 둘 다 **실제 런타임 코드**에서 뽑는다. 여기서 공식을 다시 쓰지 않는다.
    ///   ①난이도 곡선 — 층별 목표 시간(<see cref="RaidScale.TargetSeconds"/>)과
    ///     그 층 보스의 실제 HP(<see cref="BossBattle.ActiveTotalHp"/>), 그리고 그 둘에서 나오는
    ///     **필요 DPS = HP ÷ 목표 시간**.
    ///   ②성장 곡선 — 레벨별 능력배수(<see cref="W3Party.LevelStatMultiplier"/>)와
    ///     그 레벨에 도달하는 데 드는 사냥 시간(<see cref="LifeSystem.ExpToNext"/> ·
    ///     <see cref="Economy.WaveHuntExp"/>).
    ///
    /// 판정은 사람이 한다. 이 검사는 **수치와 CSV만** 남기고, 아래 두 가지가 성립하지 않으면
    /// FAIL로 표시한다 — 관문 질문이 "30층까지 성장 곡선이 매끄러운가"이기 때문이다.
    ///   G1. 층이 오르면 필요 DPS도 오른다(1층과 30층이 같으면 곡선이 아니다)
    ///   G2. 5시간 사냥으로 도달하는 레벨이 30층 요구에 닿는다
    ///
    ///   Unity -batchmode -quit -nographics -projectPath &lt;unity_meas&gt; \
    ///         -executeMethod AshesToStars.TowerClimbCurveMeasure.Run
    /// </summary>
    public static class TowerClimbCurveMeasure
    {
        public const int TopFloor = 30;
        /// <summary>관문 ②의 기준 — "5시간 연속 플레이가 지루하지 않은가"(§22).</summary>
        public const float SessionHours = 5f;

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

        [MenuItem("Ashes to Stars/QA/Tower Climb Curve (30F)")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            GameObject fixtureGo = null;
            try
            {
                fixtureGo = BuildFixture(out BossBattle boss);
                var rows = new Row[TopFloor];
                for (int f = 1; f <= TopFloor; f++)
                    rows[f - 1] = Measure(boss, f);

                // ── G1. 층이 오르면 어려워지는가 ──────────────────────────────
                float dps1 = rows[0].RequiredDps;
                float dps30 = rows[TopFloor - 1].RequiredDps;
                int steps = 0;
                for (int i = 1; i < rows.Length; i++)
                    if (!Mathf.Approximately(rows[i].RequiredDps, rows[i - 1].RequiredDps)) steps++;
                Check(dps30 > dps1 * 1.5f,
                    $"G1 30층 필요 DPS가 1층의 1.5배 이상이어야 한다: 1층 {dps1:F0} → 30층 {dps30:F0}");
                Check(steps >= 10,
                    $"G1 30층 동안 난이도가 최소 10번은 변해야 한다(계단이 아니라 곡선): 변화 {steps}회");

                // ── G2. 5시간이면 30층에 닿는가 ──────────────────────────────
                int lv5h = LevelAfterHunting(SessionHours * 3600f, rows[TopFloor - 1].UnlockedTier);
                int lvNeed = rows[TopFloor - 1].LevelAtEntry;
                Check(lv5h >= lvNeed,
                    $"G2 5시간 사냥 레벨이 30층 요구 레벨 이상이어야 한다: {lv5h} vs {lvNeed}");

                string csv = BuildCsv(rows);
                string dir = Path.Combine(RepoRoot(), "output", "qa", "ashes-to-stars", "curve");
                Directory.CreateDirectory(dir);
                string csvPath = Path.Combine(dir, "tower_climb_30.csv");
                File.WriteAllText(csvPath, csv, new UTF8Encoding(false));

                string json = BuildJson(rows, dps1, dps30, steps, lv5h, lvNeed);
                File.WriteAllText(Path.Combine(dir, "tower_climb_30.json"), json, new UTF8Encoding(false));

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

        static GameObject BuildFixture(out BossBattle boss)
        {
            // BossBattleRunSelfCheck과 같은 픽스처. 비활성으로 붙여 Awake를 한 번만 돌린다.
            var go = new GameObject("TowerClimbCurveMeasure");
            go.SetActive(false);
            var party = go.AddComponent<global::W3Party>();
            party.GameMode = true;
            boss = go.AddComponent<BossBattle>();
            Invoke(party, "Awake");
            Invoke(boss, "Awake");
            return go;
        }

        static void Invoke(object target, string method, params object[] args)
        {
            var m = target.GetType().GetMethod(method,
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public);
            if (m == null) throw new MissingMethodException(target.GetType().Name + "." + method);
            m.Invoke(target, args);
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

        static string BuildJson(Row[] rows, float dps1, float dps30, int steps, int lv5h, int lvNeed)
        {
            var sb = new StringBuilder();
            sb.Append("{\"gate\":\"단계1-성장곡선\",\"ran_at\":\"")
              .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm"))
              .Append("\",\"top_floor\":").Append(TopFloor)
              .Append(",\"required_dps_f1\":").Append(dps1.ToString("F1", CultureInfo.InvariantCulture))
              .Append(",\"required_dps_f30\":").Append(dps30.ToString("F1", CultureInfo.InvariantCulture))
              .Append(",\"difficulty_changes\":").Append(steps)
              .Append(",\"level_after_5h\":").Append(lv5h)
              .Append(",\"level_needed_f30\":").Append(lvNeed)
              .Append(",\"hunt_hours_f30\":")
              .Append(rows[rows.Length - 1].HuntHoursToLevel.ToString("F2", CultureInfo.InvariantCulture))
              .Append(",\"fails\":").Append(_fail)
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
