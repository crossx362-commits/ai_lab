using System;
using System.Collections.Generic;
using System.IO;

namespace ChinaGuard
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
            try
            {
                if (File.Exists(PathFile))
                {
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
            }
            if (result.Count == 0) result.Add("CN");   // 기본값
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
