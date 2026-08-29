using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>상태 아이콘과 독·빙결·보스 경고 스프라이트 시트의 로드·분할을 검사한다.</summary>
    public static class StatusVfxSelfCheck
    {
        const int RuntimePixels = 64;
        const int ContactCell = 88;

        public static void Run()
        {
            Debug.Assert(StatusIconAtlas.IsReady, "[StatusVfx] 상태 아이콘 아틀라스 로드 실패");
            Debug.Assert(StatusVfxSheets.SourceCount == 7, "[StatusVfx] 상태·보스 기믹 시트 7종이 등록돼야 한다");
            var repackerFiles = ReadRepackerStatusFiles();
            AssertRepackerContract(repackerFiles);
            foreach (var key in StatusIconAtlas.RequiredKeys)
                Debug.Assert(StatusIconAtlas.RectFor(key).width > 0, $"[StatusVfx] 상태 아이콘 누락: {key}");
            var live = StatusIconAtlas.LiveKeys(true, true, true, true);
            Debug.Assert(live.Count == 3 && live[0] == "shield" && live[1] == "taunt" && live[2] == "attack_up",
                "[StatusVfx] 켜진 상태만 아이콘으로 접혀야 한다");
            Debug.Assert(StatusIconAtlas.LiveKeys(false, false, false, false).Count == 0,
                "[StatusVfx] 꺼진 상태를 그리면 안 된다");
            Debug.Assert(StatusIconAtlas.LiveKeys(false, false, false, true).Count == 1
                         && StatusIconAtlas.LiveKeys(false, false, false, true)[0] == "shield",
                "[StatusVfx] 최후의 보루는 방패 아이콘");

            for (int sheet = 0; sheet < StatusVfxSheets.SourceCount; sheet++)
                for (int frame = 0; frame < 8; frame++)
                    Debug.Assert(StatusVfxSheets.Frame(sheet, frame) != null,
                        $"[StatusVfx] 시트 {sheet} 프레임 {frame} 누락");

            AssertPackedStatusSheets(repackerFiles);
            WriteRuntimeContactSheet();

            Debug.Log("[StatusVfxSelfCheck] PASS");
        }

        static HashSet<string> ReadRepackerStatusFiles()
        {
            string projectRoot = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
            string repackerPath = Path.Combine(projectRoot, "art/repack_job_vfx_sheet.py");
            Debug.Assert(File.Exists(repackerPath), "[StatusVfx] VFX 재패커를 찾지 못했다: " + repackerPath);
            if (!File.Exists(repackerPath)) return new HashSet<string>();

            string source = File.ReadAllText(repackerPath);
            Match block = Regex.Match(source, @"STATUS_SHEETS\s*=\s*\{(?<body>[\s\S]*?)\}");
            Debug.Assert(block.Success, "[StatusVfx] 재패커 STATUS_SHEETS 계약을 읽지 못했다");
            if (!block.Success) return new HashSet<string>();

            var repackerFiles = new HashSet<string>();
            foreach (Match entry in Regex.Matches(block.Groups["body"].Value,
                         "\"[^\"]+\"\\s*:\\s*\"(?<file>[^\"]+\\.png)\""))
                repackerFiles.Add(Path.GetFileNameWithoutExtension(entry.Groups["file"].Value));
            return repackerFiles;
        }

        static void AssertRepackerContract(HashSet<string> repackerFiles)
        {
            var runtimeKeys = new HashSet<string>(StatusVfxSheets.RequiredKeys);
            Debug.Assert(RepackerContractPasses(repackerFiles, runtimeKeys),
                "[StatusVfx] 재패커/런타임 키 드리프트: repacker="
                + string.Join(",", repackerFiles) + " runtime=" + string.Join(",", runtimeKeys));
            Debug.Assert(!RepackerContractPasses(new HashSet<string> { "missing_status_sheet" }, runtimeKeys),
                "[StatusVfx] 네거티브 컨트롤이 등록되지 않은 재패커 키를 탐지하지 못했다");
            var missingOne = new HashSet<string>(repackerFiles);
            foreach (string file in repackerFiles)
            {
                missingOne.Remove(file);
                break;
            }
            Debug.Assert(!RepackerContractPasses(missingOne, runtimeKeys),
                "[StatusVfx] 네거티브 컨트롤이 런타임 시트 누락을 탐지하지 못했다");
            var extraOne = new HashSet<string>(repackerFiles) { "extra_status_sheet" };
            Debug.Assert(!RepackerContractPasses(extraOne, runtimeKeys),
                "[StatusVfx] 네거티브 컨트롤이 재패커 여분 시트를 탐지하지 못했다");
        }

        static void AssertPackedStatusSheets(HashSet<string> repackerFiles)
        {
            foreach (string file in repackerFiles)
            {
                var texture = Resources.Load<Texture2D>("fx/" + file);
                Debug.Assert(texture != null && texture.width == 1024 && texture.height == 512,
                    $"[StatusVfx] {file} 시트는 정수 256px 4x2 격자여야 한다");
            }
        }

        static void WriteRuntimeContactSheet()
        {
            const int columns = 8, rows = 7;
            var sheet = new Texture2D(columns * ContactCell, rows * ContactCell, TextureFormat.RGBA32, false);
            var background = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < background.Length; i++) background[i] = new Color32(8, 9, 13, 255);
            sheet.SetPixels32(background);

            for (int style = 0; style < rows; style++)
            for (int frame = 0; frame < columns; frame++)
            {
                Sprite sprite = StatusVfxSheets.Frame(style, frame);
                if (sprite == null || !sprite.texture.isReadable) continue;
                float u = (frame + .5f) / columns;
                float eased = 1f - (1f - u) * (1f - u);
                int size = Mathf.RoundToInt(RuntimePixels * Mathf.Lerp(.65f, 1.15f, eased));
                float opacity = u < .5f ? 1f : 1f - (u - .5f) * 2f;
                int cellX = frame * ContactCell + (ContactCell - size) / 2;
                int cellY = (rows - 1 - style) * ContactCell + (ContactCell - size) / 2;
                Rect rect = sprite.rect;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float tx = (rect.x + (x + .5f) / size * rect.width) / sprite.texture.width;
                    float ty = (rect.y + (y + .5f) / size * rect.height) / sprite.texture.height;
                    Color fg = sprite.texture.GetPixelBilinear(tx, ty);
                    fg.a *= opacity;
                    Color bg = sheet.GetPixel(cellX + x, cellY + y);
                    sheet.SetPixel(cellX + x, cellY + y, Color.Lerp(bg, fg, fg.a));
                }
            }

            sheet.Apply();
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../..", "results"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "status_vfx_runtime_frames.png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Debug.Log("[StatusVfx] 실소비 64px·시간 스케일 견본(열=프레임 0→7, 행="
                + string.Join("/", StatusVfxSheets.RequiredKeys) + "): " + path);
        }

        static bool RepackerContractPasses(HashSet<string> repackerFiles, HashSet<string> runtimeKeys)
            => repackerFiles.SetEquals(runtimeKeys);
    }
}
