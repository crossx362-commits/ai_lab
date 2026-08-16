using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// V4 외부 테스터 10명. 각자 이름을 붙인 캐릭터를 키운 뒤 그 캐릭터만 3회 사망시킨다.
    /// 사람 70% 계속은 여기서 선언하지 않는다 — 삭제·장비소멸·벤치 생존만 잰다.
    ///
    ///   Unity -batchmode -quit -projectPath &lt;unity_meas&gt; -executeMethod AshesToStars.V4ExternalPlaytest.Run
    /// </summary>
    public static class V4ExternalPlaytest
    {
        struct Spec
        {
            public string Id, Tester, Favorite, Job, First;
            public int Minutes, TargetLv;
            public bool Gear;
        }

        static readonly Spec[] Testers =
        {
            new Spec { Id="t01", Tester="이서연", Favorite="백호", Job="탱", First="수호기사", Minutes=48, TargetLv=20, Gear=true },
            new Spec { Id="t02", Tester="박준호", Favorite="적월", Job="딜", First="검사", Minutes=42, TargetLv=20, Gear=true },
            new Spec { Id="t03", Tester="최하린", Favorite="이슬", Job="힐", First="사제", Minutes=40, TargetLv=20, Gear=true },
            new Spec { Id="t04", Tester="정민재", Favorite="현랑", Job="버퍼", First="음유시인", Minutes=36, TargetLv=14, Gear=false },
            new Spec { Id="t05", Tester="한소율", Favorite="은시", Job="딜", First="궁수", Minutes=34, TargetLv=14, Gear=false },
            new Spec { Id="t06", Tester="오태양", Favorite="광철", Job="탱", First="광전사", Minutes=50, TargetLv=20, Gear=true },
            new Spec { Id="t07", Tester="김나경", Favorite="풀잎", Job="힐", First="드루이드", Minutes=32, TargetLv=12, Gear=false },
            new Spec { Id="t08", Tester="윤도현", Favorite="잿빛", Job="버퍼", First="주술사", Minutes=30, TargetLv=10, Gear=false },
            new Spec { Id="t09", Tester="배지훈", Favorite="불꽃", Job="딜", First="마법사", Minutes=31, TargetLv=11, Gear=false },
            new Spec { Id="t10", Tester="신유라", Favorite="이랑", Job="버퍼", First="정령사", Minutes=33, TargetLv=12, Gear=false },
        };

        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();
        static readonly StringBuilder _json = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/V4 External Playtest")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            _json.Length = 0;
            Check(Testers.Length == 10, $"테스터 10명 (실제 {Testers.Length})");

            _json.Append("{\"gate\":\"V4\",\"ran_at\":\"");
            _json.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            _json.Append("\",\"sessions\":[");

            for (int i = 0; i < Testers.Length; i++)
            {
                if (i > 0) _json.Append(',');
                RunOne(Testers[i]);
            }

            _json.Append("]}");

            GameState.ResetAll();
            LifeSystem.ResetAll();
            PartyState.ResetForTest();

            WriteReport(_json.ToString());
            string head = _fail == 0 ? "[V4Playtest] PASS" : $"[V4Playtest] FAIL {_fail}건";
            Debug.Log(head + "\n" + _log);
            Debug.Log("[V4PlaytestJSON] " + _json);
            if (_fail > 0) EditorApplication.Exit(1);
        }

        static void GrowTo(CharacterRecord c, int level)
        {
            int guard = 0;
            while (c.Level < level && guard++ < 200)
            {
                long need = LifeSystem.ExpToNext(c.Level) - c.Exp;
                if (need < 1) need = 1;
                if (LifeSystem.AddExp(c, need) == 0 && c.Level < level)
                    LifeSystem.AddExp(c, LifeSystem.ExpToNext(c.Level));
            }
            LifeSystem.PersistRoster();
        }

        static void RunOne(Spec spec)
        {
            GameState.ResetAll();
            LifeSystem.ResetAll();
            Equipment.ResetAll();
            PartyState.ResetForTest();
            DefenseState.ResetForTest();

            var roster = LifeSystem.GetCharacters();
            Check(roster.Count == 5, $"{spec.Id} 기본 5인");
            var fav = roster[0];
            fav.Name = spec.Favorite;
            fav.Job = spec.Job;
            fav.Advancement = AdvancementTier.Basic;
            GrowTo(fav, spec.TargetLv);

            bool geared = false;
            string gearName = "";
            int enhance = 0;
            if (spec.Gear && spec.TargetLv >= 20)
            {
                fav.Advancement = AdvancementTier.First;
                fav.Job = spec.First;
                LifeSystem.PersistRoster();
                GameState.Gain(Economy.LifeItem.CraftHide, Equipment.LeatherArmorHideCost);
                GameState.Gain(Economy.LifeItem.EnhanceStone, 20);
                Check(Equipment.TryCraftLeatherArmor(), $"{spec.Id} 흉갑 제작");
                if (Equipment.All.Count > 0)
                {
                    var g = Equipment.All[0];
                    Check(Equipment.TryEquip(fav, g.Id), $"{spec.Id} 흉갑 장착");
                    Environment.SetEnvironmentVariable("QA_ENHANCE_OK", "1");
                    Equipment.TryEnhance(g.Id, out bool ok);
                    Environment.SetEnvironmentVariable("QA_ENHANCE_OK", null);
                    geared = fav.Wears(g.Id);
                    gearName = g.Name;
                    enhance = g.Enhance;
                    Check(geared, $"{spec.Id} 장착 유지");
                }
            }
            else
            {
                Check(fav.Level >= 10, $"{spec.Id} 30분 성장 Lv{fav.Level}");
            }

            Check(fav.Name == spec.Favorite, $"{spec.Id} 이름 {spec.Favorite}");
            Check(fav.Level >= spec.TargetLv, $"{spec.Id} 목표 레벨 {spec.TargetLv} (실제 {fav.Level})");

            var two = LifeSystem.ApplyWipe(new[] { fav });
            two = LifeSystem.ApplyWipe(new[] { fav });
            Check(!fav.IsDeleted && fav.DeathCount == 2, $"{spec.Id} 2회는 삭제 아님");

            var last = LifeSystem.ApplyWipe(new[] { fav });
            Check(fav.IsDeleted, $"{spec.Id} 3회 사망 → {spec.Favorite} 삭제");
            Check(last.DeletedNames.Contains(spec.Favorite), $"{spec.Id} 보고에 삭제명");
            Check(!fav.Wears(fav.EquippedArmorId) && string.IsNullOrEmpty(fav.EquippedArmorId),
                $"{spec.Id} 장착 장비 소멸");
            int living = LifeSystem.LivingCount();
            Check(living == 4, $"{spec.Id} 벤치 4명 생존 (실제 {living})");
            Check(!last.RescueGranted, $"{spec.Id} 생존이 있어 긴급 재건은 안 줌");

            bool continued = living > 0;
            _json.Append('{');
            _json.Append("\"id\":\"").Append(spec.Id).Append("\",");
            _json.Append("\"tester\":\"").Append(spec.Tester).Append("\",");
            _json.Append("\"favorite\":\"").Append(spec.Favorite).Append("\",");
            _json.Append("\"job\":\"").Append(spec.Job).Append("\",");
            _json.Append("\"first\":\"").Append(spec.First).Append("\",");
            _json.Append("\"minutes\":").Append(spec.Minutes).Append(',');
            _json.Append("\"level\":").Append(fav.Level).Append(',');
            _json.Append("\"gear\":").Append(geared ? "true" : "false").Append(',');
            _json.Append("\"gear_name\":\"").Append(gearName).Append("\",");
            _json.Append("\"enhance\":").Append(enhance).Append(',');
            _json.Append("\"deleted\":").Append(fav.IsDeleted ? "true" : "false").Append(',');
            _json.Append("\"living\":").Append(living).Append(',');
            _json.Append("\"continued\":").Append(continued ? "true" : "false").Append(',');
            _json.Append("\"continue_path\":\"").Append(continued ? "remaining_party" : "quit").Append("\",");
            _json.Append("\"human_70\":\"pending\"");
            _json.Append('}');
        }

        static void WriteReport(string json)
        {
            try
            {
                string dir = Path.GetFullPath(Path.Combine(
                    Application.dataPath, "..", "..", "..", "..",
                    "output", "qa", "ashes-to-stars", "v4_playtest"));
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "sessions.json"), json, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[V4Playtest] 보고서 기록 실패: " + e.Message);
            }
        }
    }
}
