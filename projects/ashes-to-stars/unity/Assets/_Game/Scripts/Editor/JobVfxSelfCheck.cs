using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    public static class JobVfxSelfCheck
    {
        public static void Run()
        {
            Debug.Assert(JobVfxSheets.IsReady, "[JobVfxSelfCheck] 직업 이펙트 시트를 읽지 못했다");
            Debug.Assert(JobVfxSheets.FrameCount == 8, "[JobVfxSelfCheck] 8프레임 시트가 아니다");
            Debug.Assert(JobVfxSheets.SourceCount == 6, "[JobVfxSelfCheck] 음유시인 오라 시트가 등록되지 않았다");
            AssertRepackerKeysMatchRuntime();
            var tank = Resources.Load<Texture2D>("fx/tank_slash_sheet");
            Debug.Assert(tank != null && tank.width == 1024 && tank.height == 512,
                "[JobVfxSelfCheck] 탱커 베기 시트는 정수 256px 4x2 격자여야 한다");
            var bard = Resources.Load<Texture2D>("fx/bard_aura_sheet");
            Debug.Assert(bard != null && bard.width == 1024 && bard.height == 512,
                "[JobVfxSelfCheck] 음유시인 오라 시트는 정수 256px 4x2 격자여야 한다");
            var dps = Resources.Load<Texture2D>("fx/dps_slash_sheet");
            Debug.Assert(dps != null && dps.width == 1024 && dps.height == 512,
                "[JobVfxSelfCheck] 물리 딜러 베기 시트는 정수 256px 4x2 격자여야 한다");
            var mage = Resources.Load<Texture2D>("fx/mage_fire_sheet");
            Debug.Assert(mage != null && mage.width == 1024 && mage.height == 512,
                "[JobVfxSelfCheck] 마법사 화염 시트는 정수 256px 4x2 격자여야 한다");
            var priest = Resources.Load<Texture2D>("fx/priest_heal_sheet");
            Debug.Assert(priest != null && priest.width == 1024 && priest.height == 512,
                "[JobVfxSelfCheck] 사제 치유 시트는 정수 256px 4x2 격자여야 한다");
            var barrier = Resources.Load<Texture2D>("fx/tank_barrier_sheet");
            Debug.Assert(barrier != null && barrier.width == 1024 && barrier.height == 512,
                "[JobVfxSelfCheck] 탱커 방어막 시트는 정수 256px 4x2 격자여야 한다");
            Debug.Log("[JobVfxSelfCheck] PASS");
        }

        static void AssertRepackerKeysMatchRuntime()
        {
            string projectRoot = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
            string repackerPath = Path.Combine(projectRoot, "art/repack_job_vfx_sheet.py");
            Debug.Assert(File.Exists(repackerPath), "[JobVfxSelfCheck] 직업 VFX 재패커를 찾지 못했다: " + repackerPath);
            if (!File.Exists(repackerPath)) return;

            string source = File.ReadAllText(repackerPath);
            Match block = Regex.Match(source, @"SHEETS\s*=\s*\{(?<body>[\s\S]*?)\}");
            Debug.Assert(block.Success, "[JobVfxSelfCheck] 재패커 SHEETS 계약을 읽지 못했다");
            if (!block.Success) return;

            var repackerFiles = new HashSet<string>();
            foreach (Match entry in Regex.Matches(block.Groups["body"].Value,
                         "\\\"[^\\\"]+\\\"\\s*:\\s*\\\"(?<file>[^\\\"]+\\.png)\\\""))
                repackerFiles.Add(Path.GetFileNameWithoutExtension(entry.Groups["file"].Value));

            var runtimeKeys = new HashSet<string>(JobVfxSheets.RequiredKeys);
            Debug.Assert(KeysMatch(repackerFiles, runtimeKeys),
                "[JobVfxSelfCheck] 재패커/런타임 키 드리프트: repacker="
                + string.Join(",", repackerFiles) + " runtime=" + string.Join(",", runtimeKeys));
            Debug.Assert(!KeysMatch(new HashSet<string> { "tank_slash_sheet" }, runtimeKeys),
                "[JobVfxSelfCheck] 네거티브 컨트롤이 키 누락을 탐지하지 못했다");
        }

        static bool KeysMatch(HashSet<string> repackerFiles, HashSet<string> runtimeKeys)
            => repackerFiles.SetEquals(runtimeKeys);
    }
}
