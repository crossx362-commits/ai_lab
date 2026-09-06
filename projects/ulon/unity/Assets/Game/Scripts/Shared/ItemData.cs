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
        /// <summary>플레이어에게 보이는 한국어 표시명 — 이름 원장도 여기 하나뿐이다(코드는 폴백).</summary>
        public string name;
        public float weight;
        public int buy;
        public int uses;
        public int strReq;
        public bool container;
    }

    public static class ItemData
    {
        public const string FileName = "items.json";

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

        public static string FullPath => DataLedger.PathOf(FileName);

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
            if (!DataLedger.TryRead(FileName, out File_ parsed, out loadError))
                return;
            if (parsed.items == null)
            {
                loadError = "no items array: " + FullPath;
                return;
            }
            int bad = 0;
            for (int i = 0; i < parsed.items.Length; i++)
            {
                var rec = parsed.items[i];
                if (string.IsNullOrEmpty(rec.id))
                    continue;
                // 레코드 단위 검증 — 불량 수치 하나가 원장 전체를 못 쓰게 만들면 안 되고,
                // 조용히 통과해서도 안 된다(무게 0인 아이템은 무한 적재, 음수 가격은 무한 골드다).
                string why = ReasonInvalid(rec);
                if (why != "")
                {
                    bad++;
                    loadError = (loadError == "" ? "" : loadError + "; ") + rec.id + ": " + why;
                    Debug.LogError("[Ulon] items.json 레코드 무시 — " + rec.id + ": " + why + " (코드 기본값으로 떨어집니다)");
                    continue;   // 코드 폴백
                }
                map[rec.id] = rec;
            }
            loadedFrom = FullPath;
            if (bad > 0)
                loadError = "불량 레코드 " + bad + "건 — " + loadError;
        }

        /// <summary>불량이면 사유, 정상이면 빈 문자열. 판정을 한 곳에 둬야 로더와 Assert가 갈라지지 않는다.</summary>
        public static string ReasonInvalid(ItemStat rec)
        {
            if (string.IsNullOrEmpty(rec.name))
                return "name 비어 있음 (화면에 영문 ID가 그대로 나온다)";
            if (rec.weight <= 0f)
                return "weight " + rec.weight + " (0 이하면 무한 적재)";
            if (rec.buy < 0)
                return "buy " + rec.buy + " (음수 가격은 무한 골드)";
            if (rec.uses < 0)
                return "uses " + rec.uses;
            if (rec.strReq < 0)
                return "strReq " + rec.strReq;
            return "";
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
