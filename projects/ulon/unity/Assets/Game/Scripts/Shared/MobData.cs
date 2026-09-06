using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획서 12.2 — 몬스터 수치 원장. 원장: StreamingAssets/Data/mobs.json.
    /// 외형(FBX·프리팹 경로)은 여기 없다(12.2 외형/능력치 분리) — 코드가 계속 들고 있다.
    /// 파일이 없거나 항목이 빠지면 MobCatalog의 코드 기본값으로 폴백한다.
    /// </summary>
    [Serializable]
    public struct MobStat
    {
        public string id;
        public string name;
        public float hp;
        public float height;
        public int str;
        public int resist;
        public int dmgMin;
        public int dmgMax;
        public bool boss;
        public bool tamable;
        public string drop;
    }

    public static class MobData
    {
        public const string FileName = "mobs.json";

        [Serializable]
        class File_
        {
            public MobStat[] mobs;
        }

        static Dictionary<string, MobStat> map;
        static string loadedFrom = "";
        static string loadError = "";

        public static string FullPath => DataLedger.PathOf(FileName);

        public static string LoadedFrom
        {
            get { EnsureLoaded(); return loadedFrom; }
        }

        public static string LoadError
        {
            get { EnsureLoaded(); return loadError; }
        }

        public static int Count
        {
            get { EnsureLoaded(); return map.Count; }
        }

        public static void Reload()
        {
            map = null;
            EnsureLoaded();
        }

        public static void EnsureLoaded()
        {
            if (map != null)
                return;
            map = new Dictionary<string, MobStat>(StringComparer.Ordinal);
            loadedFrom = "";
            loadError = "";
            if (!DataLedger.TryRead(FileName, out File_ parsed, out loadError))
                return;
            if (parsed.mobs == null)
            {
                loadError = "no mobs array: " + FullPath;
                return;
            }
            for (int i = 0; i < parsed.mobs.Length; i++)
            {
                var rec = parsed.mobs[i];
                if (string.IsNullOrEmpty(rec.id))
                    continue;
                map[rec.id] = rec;
            }
            loadedFrom = FullPath;
        }

        public static bool TryGet(string id, out MobStat stat)
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(id) && map.TryGetValue(id, out stat))
                return true;
            stat = default;
            return false;
        }
    }
}
