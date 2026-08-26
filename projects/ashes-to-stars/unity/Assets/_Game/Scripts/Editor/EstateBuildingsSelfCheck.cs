using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 영지 기능 건물 전용 프랍. 경매장은 estate_auction_0. QA_NO면 옛 수레(§16).
    /// </summary>
    public static class EstateBuildingsSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Estate Buildings Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string show = Environment.GetEnvironmentVariable(EstateBuildings.EnvShow);
            string no = Environment.GetEnvironmentVariable(EstateBuildings.EnvNo);
            Environment.SetEnvironmentVariable(EstateBuildings.EnvShow, null);
            Environment.SetEnvironmentVariable(EstateBuildings.EnvNo, null);
            EstateBuildings.ResetForTest();

            // 결정론 전제: 영지 건물 레벨 prefs 잔상을 치운다. 실측(2026-08-26 unity_meas 배치)에서
            // ats.estate.b.* 레벨 5+ 잔상이 PropOf가 티어 _1을 반환해 「마을이 전용을 읽는다」 기대(_0)와
            // 어긋났다 — MemberStyleSelfCheck의 ats.style.* 오염과 같은 계열. 검증만 결정론으로 만들고
            // 런타임 로직은 변경하지 않는다. Load()는 이 뒤 처음 불리므로 지금 치우면 레벨 기본 1(티어0)로 읽힌다.
            var cores = new[]
            {
                EstateGrid.Cell.Keep, EstateGrid.Cell.Mine, EstateGrid.Cell.Warehouse,
                EstateGrid.Cell.Smith, EstateGrid.Cell.Auction, EstateGrid.Cell.Mausoleum,
                EstateGrid.Cell.Barracks,
            };
            foreach (var c in cores)
            foreach (var s in new[] { "lv", "to", "done", "orig", "job" })
                PlayerPrefs.DeleteKey("ats.estate.b." + c + "." + s);
            PlayerPrefs.DeleteKey("ats.estate.keep");
            PlayerPrefs.DeleteKey("ats.estate.keep_to");
            PlayerPrefs.DeleteKey("ats.estate.keep_done");
            PlayerPrefs.DeleteKey("ats.estate.keep_orig");
            PlayerPrefs.DeleteKey("ats.estate.keep_job");
            PlayerPrefs.Save();

            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Keep) == EstateBuildings.Keep,
                "본성 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Mine) == EstateBuildings.Mine,
                "광산 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Warehouse) == EstateBuildings.Warehouse,
                "창고 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Barracks) == EstateBuildings.Barracks,
                "수비대 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Smith) == EstateBuildings.Smith,
                "대장간 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Mausoleum) == EstateBuildings.Mausoleum,
                "영묘 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Arrow) == EstateBuildings.Tower,
                "화살탑 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Magic) == EstateBuildings.Tower,
                "마법탑 전용 이름");
            Check(EstateBuildings.DedicatedOf(EstateGrid.Cell.Auction) == EstateBuildings.Auction,
                "경매장 전용 이름");
            Check(EstateBuildings.OldOf(EstateGrid.Cell.Keep) == "village_house_1", "옛 본성=큰 집");
            Check(EstateBuildings.OldOf(EstateGrid.Cell.Mine) == "village_barn_0", "옛 광산=헛간");
            Check(EstateBuildings.OldOf(EstateGrid.Cell.Warehouse) == "village_house_0", "옛 창고=집");
            Check(EstateBuildings.OldOf(EstateGrid.Cell.Barracks) == "village_barn_0", "옛 수비대=헛간");

            Check(EstateBuildings.HasDedicated(EstateBuildings.Keep), "본성 PNG");
            Check(EstateBuildings.HasDedicated(EstateBuildings.Mine), "광산 PNG");
            Check(EstateBuildings.HasDedicated(EstateBuildings.Warehouse), "창고 PNG");
            Check(EstateBuildings.HasDedicated(EstateBuildings.Barracks), "수비대 PNG");
            Check(EstateBuildings.HasDedicated(EstateBuildings.Auction), "경매장 PNG");

            // oxalpha 코드합성 세트 반입 판정(2026-08-26, 오너 INBOX 「영지가 밋밋함」).
            // keep_0·mine_0가 지금까지 대상(한 바퀴 한 건물). 게임이 읽는 Resources 경로와
            // 실제 파일 내용(크기·알파)을 둘 다 본다 — 낡은 나노바나나(keep 1487×1516 ·
            // mine 1783×1626)가 남아 있으면 크기 판정에서 걸린다.
            var keepRes = Resources.Load<Texture2D>("props/" + EstateBuildings.Keep);
            Check(keepRes != null, "본성 PNG를 Resources에서 읽는다");
            string keepPath = Path.Combine(Application.dataPath,
                "Resources/props/" + EstateBuildings.Keep + ".png");
            Check(File.Exists(keepPath), "본성 PNG 파일이 Assets/Resources에 있다");
            if (File.Exists(keepPath))
            {
                var ox = new Texture2D(2, 2);
                ox.LoadImage(File.ReadAllBytes(keepPath));
                Check(ox.width == 256 && ox.height == 256,
                    $"본성 PNG가 oxalpha 256 세트 (실제 {ox.width}×{ox.height})");
                var px = ox.GetPixels();
                int clear = 0, solid = 0;
                foreach (var p in px)
                {
                    if (p.a < 0.05f) clear++;
                    else if (p.a > 0.95f) solid++;
                }
                Check(clear > 0 && solid > px.Length / 4,
                    $"본성 알파 유효 — 배경 잘림+실체 함께 (투명 {clear} · 불투명 {solid}/{px.Length})");
                UnityEngine.Object.DestroyImmediate(ox);
            }
            var mineRes = Resources.Load<Texture2D>("props/" + EstateBuildings.Mine);
            Check(mineRes != null, "광산 PNG를 Resources에서 읽는다");
            string minePath = Path.Combine(Application.dataPath,
                "Resources/props/" + EstateBuildings.Mine + ".png");
            Check(File.Exists(minePath), "광산 PNG 파일이 Assets/Resources에 있다");
            if (File.Exists(minePath))
            {
                var mx = new Texture2D(2, 2);
                mx.LoadImage(File.ReadAllBytes(minePath));
                Check(mx.width == 256 && mx.height == 256,
                    $"광산 PNG가 oxalpha 256 세트 (실제 {mx.width}×{mx.height})");
                var mpx = mx.GetPixels();
                int mClear = 0, mSolid = 0;
                foreach (var p in mpx)
                {
                    if (p.a < 0.05f) mClear++;
                    else if (p.a > 0.95f) mSolid++;
                }
                // 광산은 언덕+갱도라 실체 면적이 본성(27%)보다 낮다(실측 22%) — 임계 1/8.
                Check(mClear > 0 && mSolid > mpx.Length / 8,
                    $"광산 알파 유효 — 배경 잘림+실체 함께 (투명 {mClear} · 불투명 {mSolid}/{mpx.Length})");
                UnityEngine.Object.DestroyImmediate(mx);
            }
            var whRes = Resources.Load<Texture2D>("props/" + EstateBuildings.Warehouse);
            Check(whRes != null, "창고 PNG를 Resources에서 읽는다");
            string whPath = Path.Combine(Application.dataPath,
                "Resources/props/" + EstateBuildings.Warehouse + ".png");
            Check(File.Exists(whPath), "창고 PNG 파일이 Assets/Resources에 있다");
            if (File.Exists(whPath))
            {
                var wx = new Texture2D(2, 2);
                wx.LoadImage(File.ReadAllBytes(whPath));
                Check(wx.width == 256 && wx.height == 256,
                    $"창고 PNG가 oxalpha 256 세트 (실제 {wx.width}×{wx.height})");
                var wpx = wx.GetPixels();
                int wClear = 0, wSolid = 0;
                foreach (var p in wpx)
                {
                    if (p.a < 0.05f) wClear++;
                    else if (p.a > 0.95f) wSolid++;
                }
                // 창고는 직사각형 건물이라 실체가 광산보다 넓다(실측 30%) — 임계 1/6.
                Check(wClear > 0 && wSolid > wpx.Length / 6,
                    $"창고 알파 유효 — 배경 잘림+실체 함께 (투명 {wClear} · 불투명 {wSolid}/{wpx.Length})");
                UnityEngine.Object.DestroyImmediate(wx);
            }
            var baRes = Resources.Load<Texture2D>("props/" + EstateBuildings.Barracks);
            Check(baRes != null, "수비대 PNG를 Resources에서 읽는다");
            string baPath = Path.Combine(Application.dataPath,
                "Resources/props/" + EstateBuildings.Barracks + ".png");
            Check(File.Exists(baPath), "수비대 PNG 파일이 Assets/Resources에 있다");
            if (File.Exists(baPath))
            {
                var bx = new Texture2D(2, 2);
                bx.LoadImage(File.ReadAllBytes(baPath));
                Check(bx.width == 256 && bx.height == 256,
                    $"수비대 PNG가 oxalpha 256 세트 (실제 {bx.width}×{bx.height})");
                var bpx = bx.GetPixels();
                int bClear = 0, bSolid = 0;
                foreach (var p in bpx)
                {
                    if (p.a < 0.05f) bClear++;
                    else if (p.a > 0.95f) bSolid++;
                }
                // 수비대는 직사각형 건물이라 실체가 넓다(실측 24%) — 임계 1/6.
                Check(bClear > 0 && bSolid > bpx.Length / 6,
                    $"수비대 알파 유효 — 배경 잘림+실체 함께 (투명 {bClear} · 불투명 {bSolid}/{bpx.Length})");
                UnityEngine.Object.DestroyImmediate(bx);
            }
            var smRes = Resources.Load<Texture2D>("props/" + EstateBuildings.Smith);
            Check(smRes != null, "대장간 PNG를 Resources에서 읽는다");
            string smPath = Path.Combine(Application.dataPath,
                "Resources/props/" + EstateBuildings.Smith + ".png");
            Check(File.Exists(smPath), "대장간 PNG 파일이 Assets/Resources에 있다");
            if (File.Exists(smPath))
            {
                var sx = new Texture2D(2, 2);
                sx.LoadImage(File.ReadAllBytes(smPath));
                Check(sx.width == 256 && sx.height == 256,
                    $"대장간 PNG가 oxalpha 256 세트 (실제 {sx.width}×{sx.height})");
                var spx = sx.GetPixels();
                int sClear = 0, sSolid = 0;
                foreach (var p in spx)
                {
                    if (p.a < 0.05f) sClear++;
                    else if (p.a > 0.95f) sSolid++;
                }
                // 대장간은 실체가 넓다(실측 18%) — 임계 1/6.
                Check(sClear > 0 && sSolid > spx.Length / 6,
                    $"대장간 알파 유효 — 배경 잘림+실체 함께 (투명 {sClear} · 불투명 {sSolid}/{spx.Length})");
                UnityEngine.Object.DestroyImmediate(sx);
            }
            var mzRes = Resources.Load<Texture2D>("props/" + EstateBuildings.Mausoleum);
            Check(mzRes != null, "영묘 PNG를 Resources에서 읽는다");
            string mzPath = Path.Combine(Application.dataPath,
                "Resources/props/" + EstateBuildings.Mausoleum + ".png");
            Check(File.Exists(mzPath), "영묘 PNG 파일이 Assets/Resources에 있다");
            if (File.Exists(mzPath))
            {
                var mz = new Texture2D(2, 2);
                mz.LoadImage(File.ReadAllBytes(mzPath));
                Check(mz.width == 256 && mz.height == 256,
                    $"영묘 PNG가 oxalpha 256 세트 (실제 {mz.width}×{mz.height})");
                var mzpx = mz.GetPixels();
                int mzClear = 0, mzSolid = 0;
                foreach (var p in mzpx)
                {
                    if (p.a < 0.05f) mzClear++;
                    else if (p.a > 0.95f) mzSolid++;
                }
                // 영묘는 첨탑형 건물이라 실체가 다소 좁다(실측 18%) — 임계 1/6.
                Check(mzClear > 0 && mzSolid > mzpx.Length / 6,
                    $"영묘 알파 유효 — 배경 잘림+실체 함께 (투명 {mzClear} · 불투명 {mzSolid}/{mzpx.Length})");
                UnityEngine.Object.DestroyImmediate(mz);
            }
            Check(EstateYard.PropOf(EstateGrid.Cell.Keep) == EstateBuildings.Keep,
                "마을이 본성 전용을 읽는다");
            Check(EstateYard.PropOf(EstateGrid.Cell.Mine) == EstateBuildings.Mine,
                "마을이 광산 전용을 읽는다");
            Check(EstateYard.PropOf(EstateGrid.Cell.Warehouse) == EstateBuildings.Warehouse,
                "마을이 창고 전용을 읽는다");
            Check(EstateYard.PropOf(EstateGrid.Cell.Barracks) == EstateBuildings.Barracks,
                "마을이 수비대 전용을 읽는다");
            Check(EstateYard.PropOf(EstateGrid.Cell.Smith) == EstateBuildings.Smith,
                "마을이 대장간 전용을 읽는다");
            Check(EstateYard.PropOf(EstateGrid.Cell.Auction) == EstateBuildings.Auction,
                "마을이 경매장 전용을 읽는다");
            Check(string.IsNullOrEmpty(EstateBuildings.LastFallback),
                "전용 그림이 있으면 폴백하지 않는다");
            Check(EstateBuildings.Line().Contains("경매장") && EstateBuildings.Line().Contains("전용 그림"),
                $"줄 (실제 {EstateBuildings.Line()})");

            Environment.SetEnvironmentVariable(EstateBuildings.EnvNo, "1");
            Check(EstateBuildings.Blocked, "QA_NO면 차단");
            Check(EstateYard.PropOf(EstateGrid.Cell.Keep) == "village_house_1",
                "차단 본성=큰 집");
            Check(EstateYard.PropOf(EstateGrid.Cell.Mine) == "village_barn_0",
                "차단 광산=헛간");
            Check(EstateYard.PropOf(EstateGrid.Cell.Warehouse) == "village_house_0",
                "차단 창고=집");
            Check(EstateYard.PropOf(EstateGrid.Cell.Barracks) == "village_barn_0",
                "차단 수비대=헛간");
            Check(EstateYard.PropOf(EstateGrid.Cell.Auction) == "village_cart_0",
                "차단 경매장=수레");
            Check(EstateBuildings.Line().Contains("수레"),
                $"차단 줄 (실제 {EstateBuildings.Line()})");
            Environment.SetEnvironmentVariable(EstateBuildings.EnvNo, null);

            Environment.SetEnvironmentVariable(EstateBuildings.EnvShow, "1");
            EstateBuildings.SeedQaIfRequested();
            Check(EstateBuildings.ShowQa, "시드 켜짐");
            Check(EstateGrid.Count(EstateGrid.Cell.Arrow) + EstateGrid.Count(EstateGrid.Cell.Magic) >= 1,
                "시드가 탑 한 칸을 세운다");
            Check(EstateBuildings.Line().Contains("전용 그림"), "시드 줄");
            Environment.SetEnvironmentVariable(EstateBuildings.EnvShow, null);
            EstateBuildings.ResetForTest();

            string runtime = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            string estate = File.ReadAllText(Path.Combine(runtime, "EstateScreen.cs"));
            string yard = File.ReadAllText(Path.Combine(runtime, "EstateYard.cs"));
            Check(yard.Contains("EstateBuildings.PropOf"), "마을이 PropOf를 읽는다");
            Check(estate.Contains("EstateBuildings.Line"), "자막이 Line을 읽는다");
            Check(estate.Contains("EstateBuildings.SeedQaIfRequested"), "시드를 읽는다");
            string buildings = File.ReadAllText(Path.Combine(runtime, "EstateBuildings.cs"));
            Check(buildings.Contains("Cell.Auction => Auction"),
                "DedicatedOf가 경매장을 읽는다");
            Check(buildings.Contains("LogWarning") && buildings.Contains("전용 그림 없음"),
                "폴백이면 경고를 찍는다");

            Environment.SetEnvironmentVariable(EstateBuildings.EnvShow, show);
            Environment.SetEnvironmentVariable(EstateBuildings.EnvNo, no);
            if (_fail == 0) Debug.Log("[EstateBuildingsSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[EstateBuildingsSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[EstateBuildingsSelfCheck] FAIL {_fail}건");
        }
    }
}
