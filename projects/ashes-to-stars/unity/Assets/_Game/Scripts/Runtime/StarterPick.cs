using System;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// 타이틀에서 기본직업 5종을 전신으로 고른다(오너 21:38).
    /// 그림은 이미 Resources/sprites/{tank,dps,mage,healer,buffer}에 있다 — 여기서는 소비처만 연다.
    /// </summary>
    public static class StarterPick
    {
        public const string EnvShow = "QA_START_PICK";
        public const string EnvJob = "QA_START_JOB";
        public const string EnvNo = "QA_NO_START_PICK";

        static bool _open;

        public static bool Open => _open;
        public static string[] Jobs => LifeSystem.BasicJobs;

        public static string Blurb(string job) => job switch
        {
            "탱" => "앞에서 막고 도발한다",
            "딜" => "가장 가까운 적을 벤다",
            "마딜" => "멀리서 마법으로 지른다",
            "힐" => "쓰러지기 전에 고친다",
            "버퍼" => "파티를 강화한다",
            _ => "",
        };

        public static bool Blocked =>
            Environment.GetEnvironmentVariable(EnvNo) == "1";

        public static bool ShouldOffer()
        {
            if (Blocked) return false;
            if (Environment.GetEnvironmentVariable(EnvShow) == "1") return true;
            return !LifeSystem.HasSavedRoster();
        }

        public static void Request()
        {
            if (Blocked) return;
            _open = true;
        }

        public static bool TryChoose(string job)
        {
            if (Blocked) return false;
            var created = LifeSystem.BeginNewGame(job);
            if (created == null) return false;
            _open = false;
            return true;
        }

        public static void SeedQaIfRequested()
        {
            if (Environment.GetEnvironmentVariable(EnvShow) == "1")
            {
                _open = true;
                return;
            }
            string job = Environment.GetEnvironmentVariable(EnvJob);
            if (LifeSystem.IsBasicJob(job))
                TryChoose(job);
        }

        public static void ResetForTest() => _open = false;
    }
}
