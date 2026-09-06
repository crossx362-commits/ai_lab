using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 기획서 §7.2 「서버 권한형」 — 클라이언트가 서버를 거치지 않고 자기 오프라인 월드를 바꾸면
        /// 온라인에서 그 기능은 **자기 화면에만** 존재한다. 조련·펫 명령·키워드 대화가 실제로 그랬다
        /// (검수 2026-09-06 P0). 기존 Assert는 OfflineWorld를 직접 불러 검증해서 이 누락을 통과시켰다 —
        /// **오프라인 경로만 보는 게이트는 온라인 미동작을 잡을 수 없다.**
        ///
        /// 그래서 런타임이 아니라 **소스를 정적으로 스캔**한다: 클라 코드의 `OfflineWorld.Instance.Try*`
        /// 호출은 같은 메서드 안에 NetAvatar 분기(net != null / IsClientInitialized / net.Rpc)를 가져야 한다.
        /// 이 판정을 통과하면서 화면이 틀릴 수 있나? — Rpc를 부르되 서버 구현이 비어 있는 경우다.
        /// 그래서 Rpc 이름이 NetAvatar에 실제로 존재하는지도 함께 본다.
        /// </summary>

        static void AssertServerAuthorityWiring()
        {
            string clientDir = Path.Combine(Application.dataPath, "Game/Scripts/Client");
            if (!Directory.Exists(clientDir))
                throw new InvalidOperationException("클라 스크립트 폴더가 없습니다: " + clientDir);

            string netPath = Path.Combine(clientDir, "NetAvatar.cs");
            if (!File.Exists(netPath))
                throw new InvalidOperationException("NetAvatar.cs가 없습니다 — 서버 권한 배선을 검사할 수 없습니다.");
            string netSource = File.ReadAllText(netPath);

            var callRe = new Regex(@"OfflineWorld\.Instance\??\.(Try\w+)\s*\(");
            var rpcCallRe = new Regex(@"net\.(Rpc\w+)\s*\(");
            int checkedCalls = 0;
            int rpcUses = 0;

            foreach (string file in Directory.GetFiles(clientDir, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name == "NetAvatar.cs")
                    continue;   // 서버에서 도는 Rpc 본체다 — 여기서 OfflineWorld를 부르는 것이 정상이다.

                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var m = callRe.Match(lines[i]);
                    if (!m.Success)
                        continue;
                    checkedCalls++;
                    // 위로 훑되 **메서드 경계에서 멈춘다**. 그냥 25줄을 보면 앞 메서드의 net 분기를
                    // 자기 것으로 오인해 결함을 통과시킨다(네거티브 컨트롤에서 실제로 통과했다).
                    bool branched = false;
                    for (int k = i - 1; k >= 0 && k >= i - 60; k--)
                    {
                        string c = lines[k];
                        if (c.TrimEnd() == "        }")
                            break;   // 앞 멤버의 끝 — 여기부터는 다른 메서드다
                        if (c.Contains("net != null") || c.Contains("IsClientInitialized") || c.Contains("net.Rpc"))
                        {
                            branched = true;
                            break;
                        }
                    }
                    if (!branched)
                        throw new InvalidOperationException(name + ":" + (i + 1) + " 의 " + m.Groups[1].Value +
                            " 호출이 NetAvatar 분기 없이 OfflineWorld를 직접 바꿉니다 — 기획서 §7.2 서버 권한형 위반입니다. " +
                            "온라인에서는 이 기능이 자기 클라에만 반영되고 다른 플레이어에게 안 보입니다. " +
                            "NetAvatar에 ServerRpc를 만들고 `net != null && net.IsClientInitialized` 분기로 호출하세요.");
                }

                // Rpc를 부르는데 NetAvatar에 그 이름이 없으면 배선이 끊긴 것이다.
                foreach (Match rm in rpcCallRe.Matches(File.ReadAllText(file)))
                {
                    rpcUses++;
                    string rpc = rm.Groups[1].Value;
                    if (netSource.IndexOf("public void " + rpc + "(", StringComparison.Ordinal) < 0)
                        throw new InvalidOperationException(name + "이 부르는 " + rpc + "가 NetAvatar에 없습니다 — 서버 배선이 끊겨 있습니다(§7.2).");
                }
            }

            // 이번에 빠져 있던 것들이 실제로 생겼는지 이름으로 못 박는다.
            string[] required = { "RpcTame", "RpcPetCommand", "RpcPetAttack", "RpcPetCome", "RpcSpeech" };
            for (int i = 0; i < required.Length; i++)
            {
                if (netSource.IndexOf("public void " + required[i] + "(", StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("NetAvatar에 " + required[i] + "가 없습니다 — 조련·펫·키워드가 서버를 거치지 않습니다(§7.2).");
            }

            Debug.Log("[Ulon] 서버 권한 배선 통과 — 클라 Try* 호출 " + checkedCalls + "건 전부 NetAvatar 분기 안, Rpc 사용 " + rpcUses + "건 전부 실재 (§7.2)");
        }
    }
}
