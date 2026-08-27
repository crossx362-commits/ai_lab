using System;
using System.IO;
using System.Text;

namespace AshesToStars
{
    /// <summary>
    /// 소스 계약 SelfCheck용 주석 스트립. <c>//</c> 줄 주석·<c>/* */</c> 블록·
    /// <c>///</c> xml docs를 뺀 뒤 Contains/IndexOf 한다. 문자열 리터럴은 그대로 두어
    /// 코드가 토큰을 문자열로 언급하면 여전히 잡힌다.
    /// QA_NO_SRC_NO_COMMENTS=1이면 원문 그대로(옛 FALSE-FAIL 경로 — 주석만의 옛 문자열이 검사에 남음).
    /// </summary>
    public static class SrcNoComments
    {
        public const string EnvNo = "QA_NO_SRC_NO_COMMENTS";

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>파일을 읽고 주석을 스트립한다. QA_NO면 원문.</summary>
        public static string Read(string path)
        {
            string raw = File.ReadAllText(path);
            return Blocked ? raw : Strip(raw);
        }

        /// <summary>문자열에서 C# 주석만 뺀다. 문자열 리터럴은 유지.</summary>
        public static string Strip(string src)
        {
            if (string.IsNullOrEmpty(src)) return src ?? "";
            var sb = new StringBuilder(src.Length);
            int n = src.Length;
            int i = 0;
            // 0 code · 1 line comment · 2 block comment · 3 string · 4 verbatim · 5 char
            int state = 0;
            while (i < n)
            {
                char c = src[i];
                char next = i + 1 < n ? src[i + 1] : '\0';
                if (state == 0)
                {
                    if (c == '/' && next == '/')
                    {
                        state = 1;
                        i += 2;
                        continue;
                    }
                    if (c == '/' && next == '*')
                    {
                        state = 2;
                        i += 2;
                        continue;
                    }
                    // $@"..." · @$"..." · @"..." → verbatim (보간 구멍의 주석은 드묾 — 문자열로 취급)
                    if (c == '$' && next == '@' && i + 2 < n && src[i + 2] == '"')
                    {
                        sb.Append(c);
                        sb.Append(next);
                        sb.Append('"');
                        state = 4;
                        i += 3;
                        continue;
                    }
                    if (c == '@' && next == '$' && i + 2 < n && src[i + 2] == '"')
                    {
                        sb.Append(c);
                        sb.Append(next);
                        sb.Append('"');
                        state = 4;
                        i += 3;
                        continue;
                    }
                    if (c == '@' && next == '"')
                    {
                        sb.Append(c);
                        sb.Append(next);
                        state = 4;
                        i += 2;
                        continue;
                    }
                    if (c == '$' && next == '"')
                    {
                        sb.Append(c);
                        sb.Append(next);
                        state = 3;
                        i += 2;
                        continue;
                    }
                    if (c == '"')
                    {
                        sb.Append(c);
                        state = 3;
                        i++;
                        continue;
                    }
                    if (c == '\'')
                    {
                        sb.Append(c);
                        state = 5;
                        i++;
                        continue;
                    }
                    sb.Append(c);
                    i++;
                    continue;
                }
                if (state == 1)
                {
                    if (c == '\n')
                    {
                        sb.Append(c);
                        state = 0;
                    }
                    i++;
                    continue;
                }
                if (state == 2)
                {
                    if (c == '*' && next == '/')
                    {
                        sb.Append(' ');
                        state = 0;
                        i += 2;
                        continue;
                    }
                    i++;
                    continue;
                }
                if (state == 3)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < n)
                    {
                        sb.Append(src[i + 1]);
                        i += 2;
                        continue;
                    }
                    if (c == '"') state = 0;
                    i++;
                    continue;
                }
                if (state == 4)
                {
                    sb.Append(c);
                    if (c == '"' && next == '"')
                    {
                        sb.Append(next);
                        i += 2;
                        continue;
                    }
                    if (c == '"') state = 0;
                    i++;
                    continue;
                }
                // char
                sb.Append(c);
                if (c == '\\' && i + 1 < n)
                {
                    sb.Append(src[i + 1]);
                    i += 2;
                    continue;
                }
                if (c == '\'') state = 0;
                i++;
            }
            return sb.ToString();
        }
    }
}
