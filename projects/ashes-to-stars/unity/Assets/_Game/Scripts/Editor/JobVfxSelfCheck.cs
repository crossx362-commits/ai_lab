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
            Debug.Log("[JobVfxSelfCheck] PASS");
        }
    }
}
