using System;
using System.Collections.Generic;
using System.IO;

namespace GeoGuard
{
    // 차단 대상 국가 설정 (한 줄에 국가 코드 하나)
    static class Config
    {
        static string PathFile
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "blocked-countries.txt"); }
        }

        public static List<string> LoadBlockedCountries()
        {
            var result = new List<string>();
            // 설정 파일이 없을 때만 기본값(중국)을 쓴다.
            // 파일이 비어 있는 것은 "차단 안 함"을 선택한 상태이므로 존중해야 한다.
            bool haveFile = false;
            try
            {
                if (File.Exists(PathFile))
                {
                    haveFile = true;
                    foreach (var line in File.ReadAllLines(PathFile))
                    {
                        string cc = line.Trim().ToUpperInvariant();
                        if (cc.Length == 2 && !result.Contains(cc)) result.Add(cc);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("설정 로드 실패: " + ex.Message);
                haveFile = false;
            }
            if (!haveFile && result.Count == 0) result.Add("CN");
            return result;
        }

        public static void SaveBlockedCountries(List<string> ccs)
        {
            try
            {
                File.WriteAllLines(PathFile, ccs.ToArray());
            }
            catch (Exception ex)
            {
                Logger.Log("설정 저장 실패: " + ex.Message);
            }
        }
    }
}
