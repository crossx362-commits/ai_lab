using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 로컬에서 전 메뉴를 열어보기 위한 시드. 에디터 Play 또는 QA_PLAY=1.
    /// 스모크(--auto / GAME_START)와 QA_NO_PLAY=1은 안 넣는다 — 샷·검사가 오염된다.
    /// </summary>
    public static class LocalPlayKit
    {
        public const string EnvShow = "QA_PLAY";
        public const string EnvNo = "QA_NO_PLAY";
        public const int PlayFloor = 30;
        public const int PlayLevel = 30;
        public const long PlayGold = 50000;
        public static readonly long WantCopper = PlayGold * Economy.COPPER_PER_GOLD;

        static bool _applied;

        public static bool Applied => _applied;

        public static string Line =>
            _applied
                ? $"로컬 테스트 · {EstateStatusHud.ShortCopper(WantCopper)} · {PlayFloor}층 · Lv{PlayLevel}"
                : "";

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool Forced
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvShow);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static bool ShouldApply()
        {
            if (Blocked || SmokeRunning()) return false;
            if (Forced) return true;
            return Application.isEditor;
        }

        static bool SmokeRunning()
        {
            if (DebugAutoPilot.Requested) return true;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (args[i] == "--auto") return true;
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GAME_START"));
        }

        public static void ApplyIfNeeded()
        {
            if (_applied || !ShouldApply()) return;
            Apply();
        }

        public static void Apply()
        {
            if (Blocked) return;
            _applied = true;
            if (GameState.Wallet.Copper < WantCopper)
                GameState.Grant(WantCopper - GameState.Wallet.Copper);
            if (GameState.TowerFloor < PlayFloor)
                GameState.SetTowerFloorForTest(PlayFloor);
            if (EstateBuild.KeepLevel < 3)
                EstateBuild.SetLevelForTest(3);
            FillBag(Economy.LifeItem.RevivalTea, 3);
            FillBag(Economy.LifeItem.ScrollOfReturn, 5);
            FillBag(Economy.LifeItem.RebornStone, 10);
            FillBag(Economy.LifeItem.AdvancementMaterial, 40);
            FillBag(Economy.LifeItem.SpecialJobToken, 5);
            FillBag(Economy.LifeItem.CraftHide, 30);
            FillBag(Economy.LifeItem.CraftFang, 30);
            FillBag(Economy.LifeItem.CraftBone, 30);
            FillBag(Economy.LifeItem.CraftPart, 30);
            FillBag(Economy.LifeItem.CraftCrystal, 30);
            FillBag(Economy.LifeItem.CraftDemonite, 30);
            FillBag(Economy.LifeItem.EnhanceStone, 30);
            var roster = LifeSystem.GetCharacters();
            for (int i = 0; i < roster.Count; i++)
            {
                var ch = roster[i];
                if (ch == null || ch.IsDeleted) continue;
                if (ch.Level < PlayLevel) ch.Level = PlayLevel;
                if (ch.Advancement == AdvancementTier.Basic)
                {
                    string next = FirstJobOf(ch.Job);
                    if (!string.IsNullOrEmpty(next))
                    {
                        ch.Job = next;
                        ch.Advancement = AdvancementTier.First;
                    }
                }
            }
            LifeSystem.PersistRoster();
            PartyState.Refresh();
        }

        static void FillBag(Economy.LifeItem item, int want)
        {
            int have = GameState.Bag.GetCount(item);
            if (have >= want) return;
            GameState.Gain(item, want - have);
        }

        static string FirstJobOf(string job) => job switch
        {
            "탱" => "수호기사",
            "딜" => "검사",
            "마딜" => "마법사",
            "힐" => "사제",
            "버퍼" => "음유시인",
            _ => null,
        };

        public static void ResetForTest() => _applied = false;
    }
}
