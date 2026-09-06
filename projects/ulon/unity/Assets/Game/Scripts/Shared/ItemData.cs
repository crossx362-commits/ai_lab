using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획서 12.2 데이터 기반 원칙 — 아이템 수치는 코드가 아니라 ID 기반 데이터 파일이 원장이다.
    /// 원장: StreamingAssets/Data/items.json (빌드 후에도 파일만 고치면 되고 재빌드가 필요 없다).
    /// 파일이 없거나 항목이 빠지면 ItemCatalog의 코드 기본값으로 떨어진다 — 데이터 사고로 게임이 죽지 않는다.
    /// 외형은 여기 없다(12.2 외형/능력치 분리).
    /// </summary>
    [Serializable]
    public struct ItemStat
    {
        public string id;
        public float weight;
        public int buy;
        public int uses;
        public int strReq;
        public bool container;
    }

    public static class ItemData
    {
        public const string RelativePath = "Data/items.json";

        [Serializable]
        class File_
        {
            public ItemStat[] items;
        }

        static Dictionary<string, ItemStat> map;
        static string loadedFrom = "";
        static string loadError = "";

        /// <summary>읽어들인 원장 경로. 파일이 없으면 빈 문자열(코드 기본값 사용).</summary>
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

        public static string FullPath => Path.Combine(Application.streamingAssetsPath, RelativePath);

        /// <summary>밸런스 파일을 다시 읽는다(에디터 검증·운영툴용).</summary>
        public static void Reload()
        {
            map = null;
            EnsureLoaded();
        }

        public static void EnsureLoaded()
        {
            if (map != null)
                return;
            map = new Dictionary<string, ItemStat>(StringComparer.Ordinal);
            loadedFrom = "";
            loadError = "";
            string path = FullPath;
            try
            {
                if (!System.IO.File.Exists(path))
                {
                    loadError = "no file: " + path;
                    return;
                }
                var parsed = JsonUtility.FromJson<File_>(System.IO.File.ReadAllText(path));
                if (parsed == null || parsed.items == null)
                {
                    loadError = "parse failed: " + path;
                    return;
                }
                for (int i = 0; i < parsed.items.Length; i++)
                {
                    var rec = parsed.items[i];
                    if (string.IsNullOrEmpty(rec.id))
                        continue;
                    map[rec.id] = rec;
                }
                loadedFrom = path;
            }
            catch (Exception e)
            {
                loadError = e.Message;
            }
        }

        public static bool TryGet(string id, out ItemStat stat)
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(id) && map.TryGetValue(id, out stat))
                return true;
            stat = default;
            return false;
        }
    }
}
