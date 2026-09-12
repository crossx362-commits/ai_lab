using System;
using System.IO;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        static void AssertInterest()
        {
            string path = DataLedger.PathOf(InterestRange.FileName);
            if (!File.Exists(path))
                throw new InvalidOperationException("관심 영역 원장이 없습니다: " + path);

            InterestRange.Reload();
            if (InterestRange.FileMeters <= 0f)
                throw new InvalidOperationException("interest.json sync_range_m가 없습니다.");
            if (Math.Abs(InterestRange.FileMeters - 18f) > 0.01f)
                throw new InvalidOperationException(
                    "관심 거리 원장이 원작 18타일이 아닙니다: " + InterestRange.FileMeters);
            if (InterestRange.Meters >= WorldTerrain.Span)
                throw new InvalidOperationException(
                    "관심 거리가 월드 한 변 이상입니다 — 전체를 보내는 것과 같습니다: " + InterestRange.Meters);
            if (!InterestRange.CanSee(Vector3.zero, new Vector3(10f, 0f, 0f)))
                throw new InvalidOperationException("10m 안 객체가 관심 밖입니다.");
            if (InterestRange.CanSee(Vector3.zero, new Vector3(19f, 0f, 0f)))
                throw new InvalidOperationException("19m 객체가 관심 안입니다 — 원작 18타일을 넘습니다.");

            string setupPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/InterestSetup.cs");
            string autoPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/AutoStartNetwork.cs");
            string hudPath = Path.Combine(Application.dataPath, "Game/Scripts/Client/NetHud.cs");
            string probePath = Path.Combine(Application.dataPath, "Game/Scripts/Client/DualClientProbe.cs");
            string setup = File.ReadAllText(setupPath);
            string auto = File.ReadAllText(autoPath);
            string hud = File.ReadAllText(hudPath);
            string probe = File.ReadAllText(probePath);
            if (setup.IndexOf("DistanceCondition", StringComparison.Ordinal) < 0 ||
                setup.IndexOf("ObserverManager", StringComparison.Ordinal) < 0 ||
                setup.IndexOf("SetMaximumDistance", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("InterestSetup이 FishNet 거리 조건을 안 붙입니다.");
            if (auto.IndexOf("InterestSetup.Apply", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("AutoStartNetwork가 관심 영역을 서버 기동 전에 안 넣습니다.");
            if (hud.IndexOf("InterestSetup.Apply", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("호스트/클라 버튼이 관심 영역을 안 넣습니다.");
            if (probe.IndexOf("HuntRoster.World", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(
                    "2클라 검사가 스폰에서 사냥터 몹을 기다립니다 — 18m 밖이라 관심 영역이 켜지면 실패합니다.");

            Debug.Log("[Ulon] 관심 영역 — " + InterestRange.FileMeters.ToString("0") +
                      "m · 출처 " + InterestRange.Source);
        }

        static void AssertInterestNegativeControl()
        {
            bool was = InterestRange.NcOpen;
            bool red = false;
            try
            {
                InterestRange.NcOpen = true;
                try { AssertInterest(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { InterestRange.NcOpen = was; }
            if (!red)
                throw new InvalidOperationException("관심 영역 네거티브 컨트롤 실패 — NcOpen 인데 통과했습니다.");
            Debug.Log("[Ulon] 관심 영역 네거티브 컨트롤 통과 — NcOpen 이면 FAIL");
        }
    }
}
