using System;
using System.IO;
using Ulon.Server;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        static void AssertPersistBackup()
        {
            if (PersistBackup.NcSkipRestore)
                throw new InvalidOperationException("PersistBackup.NcSkipRestore 가 켜져 있으면 복구가 빈 경로를 반환합니다.");

            string persistPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../server/persist.py"));
            if (!File.Exists(persistPath))
                throw new InvalidOperationException("persist.py 가 없습니다.");
            string persist = File.ReadAllText(persistPath);
            if (persist.IndexOf("SNAPSHOT_TABLES", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("persist 스냅샷 테이블 목록이 없습니다.");
            foreach (string table in new[] { "\"houses\"", "\"stables\"", "\"characters\"" })
            {
                if (persist.IndexOf(table, StringComparison.Ordinal) < 0)
                    throw new InvalidOperationException("persist 스냅샷에 " + table + " 이 없습니다.");
            }
            if (persist.IndexOf("def export_snapshot", StringComparison.Ordinal) < 0 ||
                persist.IndexOf("def restore_snapshot", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("persist export/restore 함수가 없습니다.");
            if (persist.IndexOf("NC_SKIP_RESTORE", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("persist 복구 네거티브 컨트롤 스위치가 없습니다.");

            string store = File.ReadAllText(Path.Combine(Application.dataPath, "Game/Scripts/Server/CharacterStore.cs"));
            if (store.IndexOf("\"/backup\"", StringComparison.Ordinal) < 0 ||
                store.IndexOf("\"/restore\"", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("CharacterStore 가 persist /backup·/restore 를 부르지 않습니다.");
            if (store.IndexOf("persist.json", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("JSON 백업 폴더에 persist 스냅샷을 안 넣습니다.");

            string hud = HudSourceText();
            if (hud.IndexOf("RpcGmRestore", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("GM 패널 복구가 서버 RPC로 안 갑니다.");

            string travel = File.ReadAllText(Path.Combine(Application.dataPath, "Game/Scripts/Server/OfflineWorld.Travel.cs"));
            if (travel.IndexOf("GmRestore", StringComparison.Ordinal) < 0 ||
                travel.IndexOf("PersistBackup.NcSkipRestore", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("GmRestore 가 NC 스위치를 안 봅니다.");

            Debug.Log("[Ulon] persist 백업/복구 — 스냅샷에 캐릭터·집·마구간, HUD RpcGmRestore, NC 스위치");
        }

        static void AssertPersistBackupNegativeControl()
        {
            bool was = PersistBackup.NcSkipRestore;
            bool red = false;
            try
            {
                PersistBackup.NcSkipRestore = true;
                try { AssertPersistBackup(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { PersistBackup.NcSkipRestore = was; }
            if (!red)
                throw new InvalidOperationException("persist 백업 네거티브 컨트롤 실패 — NcSkipRestore 인데 통과했습니다.");
            Debug.Log("[Ulon] persist 백업 네거티브 컨트롤 통과 — NcSkipRestore 이면 FAIL");
        }
    }
}
