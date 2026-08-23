using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>영지 건물 드래그 미리보기(§2-2). QA_NO면 이동 거부.</summary>
    public static class EstateDragSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Estate Drag Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EstateYard.EnvShowDrag);
            string no = Environment.GetEnvironmentVariable(EstateYard.EnvNoDrag);
            string noFill = Environment.GetEnvironmentVariable(EstateYard.EnvNo);
            string noStore = Environment.GetEnvironmentVariable(EstateStore.EnvNo);
            Environment.SetEnvironmentVariable(EstateYard.EnvShowDrag, null);
            Environment.SetEnvironmentVariable(EstateYard.EnvNoDrag, null);
            Environment.SetEnvironmentVariable(EstateYard.EnvNo, null);
            Environment.SetEnvironmentVariable(EstateStore.EnvNo, null);

            GameState.ResetAll();
            EstateGrid.ResetForTest();
            EstateStore.ResetForTest();
            EstateYard.ResetForTest();
            EstateBuild.ResetForTest();

            Check(!EstateYard.DragBlocked, "기본은 드래그 켜짐");
            Check(EstateYard.DragEnabled, "마을 채움이면 드래그 가능");
            Check(EstateYard.DragSlop == 8f, "DragSlop 8");

            int wx = EstateGrid.StoreX, wy = EstateGrid.StoreY;
            Check(EstateGrid.At(wx, wy) == EstateGrid.Cell.Warehouse, "기본 창고 (3,3)");
            Check(EstateScreen.WhyCannotDragMove(wx, wy, wx, wy) == null, "제자리는 허용");
            Check(EstateScreen.TryDragMove(wx, wy, EstateStore.QaX, EstateStore.QaY),
                "창고를 (3,6)으로 드래그 이동");
            Check(EstateGrid.At(EstateStore.QaX, EstateStore.QaY) == EstateGrid.Cell.Warehouse,
                "창고가 (3,6)");
            Check(EstateGrid.At(wx, wy) == EstateGrid.Cell.Empty, "옛 창고 칸 비움");
            Check(EstateGrid.InvaderSide() == EstateGrid.Side.남, "옮기면 최단 남");

            Check(!EstateScreen.TryDragMove(EstateStore.QaX, EstateStore.QaY, 1, 2),
                "본성 자리로 창고 거부");
            Check(EstateScreen.WhyCannotDragMove(EstateStore.QaX, EstateStore.QaY, 1, 2)
                    == "자리 크기가 겹친다",
                $"겹침 사유 (실제 {EstateScreen.WhyCannotDragMove(EstateStore.QaX, EstateStore.QaY, 1, 2)})");
            Check(!EstateScreen.TryDragMove(EstateStore.QaX, EstateStore.QaY, -1, 0),
                "격자 밖 거부");
            Check(EstateScreen.WhyCannotDragMove(EstateStore.QaX, EstateStore.QaY, -1, 0)
                    == "격자 밖이다",
                $"격자 밖 사유 (실제 {EstateScreen.WhyCannotDragMove(EstateStore.QaX, EstateStore.QaY, -1, 0)})");

            // 광산 2×1 — 일반 IsCore 이동
            GameState.ResetAll();
            EstateGrid.ResetForTest();
            EstateStore.ResetForTest();
            int mx = EstateGrid.MineX, my = EstateGrid.MineY;
            Check(EstateGrid.At(mx, my) == EstateGrid.Cell.Mine, "기본 광산");
            int nx = 4, ny = 5;
            Check(EstateScreen.WhyCannotDragMove(mx, my, nx, ny) == null,
                $"광산 ({nx},{ny}) 가능 (실제 {EstateScreen.WhyCannotDragMove(mx, my, nx, ny)})");
            Check(EstateScreen.TryDragMove(mx, my, nx, ny), "광산 이동");
            Check(EstateGrid.At(nx, ny) == EstateGrid.Cell.Mine, "광산 새 칸");
            Check(EstateGrid.At(mx, my) == EstateGrid.Cell.Empty, "광산 옛 칸 비움");

            // 대장간 1×1
            int sx = EstateGrid.SmithX, sy = EstateGrid.SmithY;
            Check(EstateGrid.At(sx, sy) == EstateGrid.Cell.Smith, "대장간 있음");
            Check(EstateScreen.TryDragMove(sx, sy, 2, 0), "대장간 (2,0)");
            Check(EstateGrid.At(2, 0) == EstateGrid.Cell.Smith, "대장간 새 칸");

            // 공사 중이면 거부(본성 — 광산은 본성 상한에 막힘)
            GameState.ResetAll();
            EstateGrid.ResetForTest();
            EstateBuild.ResetForTest();
            GameState.Grant(EstateBuild.UpgradeCost(1));
            long now = 1_700_000_000;
            EstateBuild.NowUnix = () => now;
            Check(EstateBuild.TryStartKeep(), "본성 공사 시작");
            Check(EstateBuild.Busy(EstateGrid.Cell.Keep), "본성 공사 중");
            Check(EstateScreen.WhyCannotDragMove(EstateGrid.KeepX, EstateGrid.KeepY, 4, 0)
                    == "건설 중이다",
                $"공사 중 사유 (실제 {EstateScreen.WhyCannotDragMove(EstateGrid.KeepX, EstateGrid.KeepY, 4, 0)})");
            Check(!EstateScreen.TryDragMove(EstateGrid.KeepX, EstateGrid.KeepY, 4, 0),
                "공사 중 이동 거부");
            EstateBuild.ResetForTest();

            // QA_NO_ESTATE_DRAG
            GameState.ResetAll();
            EstateGrid.ResetForTest();
            EstateStore.ResetForTest();
            EstateBuild.ResetForTest();
            Environment.SetEnvironmentVariable(EstateYard.EnvNoDrag, "1");
            Check(EstateYard.DragBlocked, "QA_NO_ESTATE_DRAG");
            Check(!EstateYard.DragEnabled, "QA_NO면 DragEnabled false");
            Check(EstateScreen.WhyCannotDragMove(EstateGrid.StoreX, EstateGrid.StoreY, 3, 6)
                    == "건물 이동이 꺼져 있다",
                $"QA_NO 사유 (실제 {EstateScreen.WhyCannotDragMove(EstateGrid.StoreX, EstateGrid.StoreY, 3, 6)})");
            Check(!EstateScreen.TryDragMove(EstateGrid.StoreX, EstateGrid.StoreY, 3, 6),
                "QA_NO면 창고도 이동 거부");
            Check(EstateGrid.At(EstateGrid.StoreX, EstateGrid.StoreY) == EstateGrid.Cell.Warehouse,
                "QA_NO면 창고 고정");
            Environment.SetEnvironmentVariable(EstateYard.EnvNoDrag, null);

            // ShowQa 시드 줄
            Environment.SetEnvironmentVariable(EstateYard.EnvShowDrag, "1");
            Check(EstateYard.ShowDragQa, "ShowDragQa");
            Check(EstateYard.Line().IndexOf("끌면", StringComparison.Ordinal) >= 0
                  || EstateYard.Line().IndexOf("옮긴다", StringComparison.Ordinal) >= 0,
                $"드래그 줄 (실제 {EstateYard.Line()})");
            Environment.SetEnvironmentVariable(EstateYard.EnvShowDrag, null);

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string screen = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            string yard = File.ReadAllText(Path.Combine(runtime, "EstateYard.cs"));
            Check(screen.IndexOf("HandleBuildingDrag", StringComparison.Ordinal) >= 0
                  && screen.IndexOf("TryDragMove", StringComparison.Ordinal) >= 0
                  && screen.IndexOf("DrawMovePreview", StringComparison.Ordinal) >= 0,
                "EstateScreen이 드래그·미리보기를 읽는다");
            Check(yard.IndexOf("TryHitCoreOrigin", StringComparison.Ordinal) >= 0
                  && yard.IndexOf("EnvNoDrag", StringComparison.Ordinal) >= 0
                  && yard.IndexOf("CoreGesture", StringComparison.Ordinal) >= 0,
                "EstateYard가 건물 제스처·플래그를 연다");
            Check(yard.IndexOf("DragSlop", StringComparison.Ordinal) >= 0, "DragSlop 재사용");

            _ = nameof(EstateScreen.TryDragMove);
            _ = nameof(EstateScreen.WhyCannotDragMove);
            _ = nameof(EstateYard.DrawMovePreview);
            _ = nameof(EstateYard.TryHitCoreOrigin);
            _ = nameof(EstateStore.TryMove);

            Environment.SetEnvironmentVariable(EstateYard.EnvShowDrag, show);
            Environment.SetEnvironmentVariable(EstateYard.EnvNoDrag, no);
            Environment.SetEnvironmentVariable(EstateYard.EnvNo, noFill);
            Environment.SetEnvironmentVariable(EstateStore.EnvNo, noStore);
            EstateYard.ResetForTest();
            EstateStore.ResetForTest();
            EstateBuild.ResetForTest();
            EstateGrid.ResetForTest();
            GameState.ResetAll();

            if (_fail == 0) Debug.Log("[EstateDragSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EstateDragSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0)
                throw new InvalidOperationException($"[EstateDragSelfCheck] FAIL {_fail}건");
        }
    }
}
