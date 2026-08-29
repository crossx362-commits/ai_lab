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
        public static void Run()
        {
            Debug.Assert(StatusIconAtlas.IsReady, "[StatusVfx] 상태 아이콘 아틀라스 로드 실패");
            Debug.Assert(StatusVfxSheets.SourceCount == 7, "[StatusVfx] 상태·보스 기믹 시트 7종이 등록돼야 한다");
            AssertRepackerKeysBelongToRuntime();
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

            var poison = Resources.Load<Texture2D>("fx/poison_status_sheet");
            Debug.Assert(poison != null && poison.width == 1024 && poison.height == 512,
                "[StatusVfx] 독 상태 시트는 정수 256px 4x2 격자여야 한다");
            var freeze = Resources.Load<Texture2D>("fx/freeze_status_sheet");
            Debug.Assert(freeze != null && freeze.width == 1024 && freeze.height == 512,
                "[StatusVfx] 빙결 상태 시트는 정수 256px 4x2 격자여야 한다");

            Debug.Log("[StatusVfxSelfCheck] PASS");
        }

        static void AssertRepackerKeysBelongToRuntime()
        {
            string projectRoot = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
            string repackerPath = Path.Combine(projectRoot, "art/repack_job_vfx_sheet.py");
            Debug.Assert(File.Exists(repackerPath), "[StatusVfx] VFX 재패커를 찾지 못했다: " + repackerPath);
            if (!File.Exists(repackerPath)) return;

            string source = File.ReadAllText(repackerPath);
            Match block = Regex.Match(source, @"STATUS_SHEETS\s*=\s*\{(?<body>[\s\S]*?)\}");
            Debug.Assert(block.Success, "[StatusVfx] 재패커 STATUS_SHEETS 계약을 읽지 못했다");
            if (!block.Success) return;

            var repackerFiles = new HashSet<string>();
            foreach (Match entry in Regex.Matches(block.Groups["body"].Value,
                         "\"[^\"]+\"\\s*:\\s*\"(?<file>[^\"]+\\.png)\""))
                repackerFiles.Add(Path.GetFileNameWithoutExtension(entry.Groups["file"].Value));

            var runtimeKeys = new HashSet<string>(StatusVfxSheets.RequiredKeys);
            Debug.Assert(KeysBelongToRuntime(repackerFiles, runtimeKeys),
                "[StatusVfx] 재패커/런타임 키 드리프트: repacker="
                + string.Join(",", repackerFiles) + " runtime=" + string.Join(",", runtimeKeys));
            Debug.Assert(!KeysBelongToRuntime(new HashSet<string> { "missing_status_sheet" }, runtimeKeys),
                "[StatusVfx] 네거티브 컨트롤이 등록되지 않은 재패커 키를 탐지하지 못했다");
        }

        static bool KeysBelongToRuntime(HashSet<string> repackerFiles, HashSet<string> runtimeKeys)
            => repackerFiles.Count > 0 && repackerFiles.IsSubsetOf(runtimeKeys);
    }
}
