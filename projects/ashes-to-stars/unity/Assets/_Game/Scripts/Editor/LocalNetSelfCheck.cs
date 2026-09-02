using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>성계·랭킹·동맹 로컬 목업. QA_NO면 옛 잠김 카드.</summary>
    public static class LocalNetSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Local Net Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(LocalNet.EnvNo);
            Environment.SetEnvironmentVariable(LocalNet.EnvNo, null);
            GameState.ResetAll();
            Honor.ResetForTest();
            LocalNet.ResetForTest();

            Check(!LocalNet.Blocked, "기본은 켠다");
            Check(LocalNet.AllyCount == 0, "시작 동맹 0");
            Check(LocalNet.TryAlly("가까운 별"), "가까운 별 동맹");
            Check(LocalNet.IsAlly("가까운 별") && LocalNet.AllyCount == 1, "동맹 1");
            Check(!LocalNet.CanInvade("가까운 별"), "동맹은 침략 불가");
            Check(LocalNet.CanInvade("경계 별"), "중립은 침략 가능");
            Check(LocalNet.WhyCannotAlly("가까운 별") != null, "중복 동맹 거부");
            Check(LocalNet.TryUnally("가까운 별") && LocalNet.AllyCount == 0, "해제");
            LocalNet.MarkVisit("안개 별");
            Check(LocalNet.LastVisit == "안개 별", "지나감 기록");

            Honor.ApplyGuard(true);
            Honor.ApplyGuard(true);
            Check(Honor.GuardWins == 2, $"수비 성공 횟수 (실제 {Honor.GuardWins})");
            Check(LocalNet.MyScore(LocalNet.Board.Guard) == 2, "랭킹 수비 점수는 GuardWins");
            Check(LocalNet.BoardRows(LocalNet.Board.Floor).Length >= 4, "층 보드에 나+라이벌");
            bool hasMe = false;
            var floorRows = LocalNet.BoardRows(LocalNet.Board.Floor);
            for (int i = 0; i < floorRows.Length; i++)
                if (floorRows[i].Mine) hasMe = true;
            Check(hasMe, "층 보드에 내가 있다");
            Check(LocalNet.MyPlace(LocalNet.Board.Floor) >= 1, "내 순위 ≥ 1");

            Environment.SetEnvironmentVariable(LocalNet.EnvNo, "1");
            Check(LocalNet.Blocked, "QA_NO면 차단");
            Environment.SetEnvironmentVariable(LocalNet.EnvNo, null);

            string map = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/WorldMapScreen.cs"));
            Check(map.Contains("LocalNet.Blocked") && map.Contains("Hub.성계") && map.Contains("Hub.랭킹"),
                "월드맵이 성계·랭킹 로컬 허브를 연다");
            Check(map.Contains("LocalNet.TryAlly") && map.Contains("TryGoInvasion"),
                "성계에서 동맹·침략을 고른다");
            string exp = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Game/Scripts/Runtime/WorldExplore.cs"));
            Check(exp.Contains("HitStar"), "밝힌 별 클릭이 있다");

            Honor.ResetForTest();
            LocalNet.ResetForTest();
            Environment.SetEnvironmentVariable(LocalNet.EnvNo, no);
            if (_fail == 0) Debug.Log("[LocalNetSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[LocalNetSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[LocalNetSelfCheck] FAIL {_fail}건");
        }
    }
}
