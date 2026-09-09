using System;
using System.IO;
using UnityEngine;

namespace Ulon.Editor
{
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **HUD 소스는 한 파일이 아니다**(랩 ㉭, 2026-09-09).
        ///
        /// 다섯 자리가 `Client/SliceHud.cs`를 **파일 이름으로** 열어 「이 배선이 있나」를 봤다.
        /// 화면 하나가 파일 넷으로 갈리자 그 자들이 한꺼번에 빨간불이 됐다 — 배선은 그대로인데.
        /// **자가 보는 것은 파일이 아니라 화면이어야 한다.** 그래서 `SliceHud*.cs`를 전부 이어 붙여
        /// 돌려준다. 검사 내용은 하나도 안 느슨해졌다(같은 문자열을 같은 방식으로 찾는다).
        ///
        /// **이 자가 못 보는 것**: 이름이 `SliceHud`로 시작하지 않는 파일로 화면을 또 쪼개면
        /// 이 그물 밖이다. HUD를 새 파일로 뗄 땐 이름을 `SliceHud.*`로 지어라.
        /// </summary>
        internal static string HudSourceText()
        {
            var sb = new System.Text.StringBuilder();
            foreach (string p in HudSourceFiles())
                sb.Append(File.ReadAllText(p)).Append('\n');
            return sb.ToString();
        }

        /// <summary>위와 같은 것을 줄 단위로 — 줄을 훑는 자(`HudReachable`)가 쓴다.</summary>
        internal static string[] HudSourceLines()
        {
            var all = new System.Collections.Generic.List<string>();
            foreach (string p in HudSourceFiles())
                all.AddRange(File.ReadAllLines(p));
            return all.ToArray();
        }

        static string[] HudSourceFiles()
        {
            string dir = Path.Combine(Application.dataPath, "Game/Scripts/Client");
            string[] files = Directory.GetFiles(dir, "SliceHud*.cs");
            if (files.Length == 0)
                throw new InvalidOperationException("SliceHud 소스를 찾을 수 없습니다 — 화면 배선을 검사할 수 없습니다.");
            Array.Sort(files, StringComparer.Ordinal);   // 기계마다 순서가 달라지지 않게
            return files;
        }
    }
}
