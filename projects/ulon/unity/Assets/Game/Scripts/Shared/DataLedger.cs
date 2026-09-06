using System;
using System.IO;
using UnityEngine;

namespace Ulon.Shared
{
    /// <summary>
    /// 기획서 12.2 수치 원장 파일 하나를 읽는 공용 경로. items.json·mobs.json 등이 이걸 쓴다.
    /// 읽기 실패는 예외가 아니라 error 문자열이다 — 호출부는 코드 기본값으로 폴백한다.
    /// </summary>
    public static class DataLedger
    {
        public static string Root => Path.Combine(Application.streamingAssetsPath, "Data");

        public static string PathOf(string fileName) => Path.Combine(Root, fileName);

        public static bool TryRead<T>(string fileName, out T parsed, out string error) where T : class
        {
            parsed = null;
            error = "";
            string path = PathOf(fileName);
            try
            {
                if (!File.Exists(path))
                {
                    error = "no file: " + path;
                    return false;
                }
                parsed = JsonUtility.FromJson<T>(File.ReadAllText(path));
                if (parsed == null)
                {
                    error = "parse failed: " + path;
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }
    }
}
