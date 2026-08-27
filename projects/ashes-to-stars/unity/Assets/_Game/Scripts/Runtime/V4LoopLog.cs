using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>
    /// V4 루프 자동 경계 로그. 삭제→재건 경계만 남긴다(docs/plans/V4_LOOP_BOUNDARY.md).
    /// 삭제 판정은 LifeSystem/Memorial 이벤트(IsDeleted·Stamp)다. 파티 카드 Cap 문구는 안 읽는다.
    /// 벽시계 30분 미만은 표본으로 안 남긴다. QA_NO면 파일을 안 만들고 줄도 안 붙인다.
    /// </summary>
    public static class V4LoopLog
    {
        public const string EnvNo = "QA_NO_V4_LOOP_LOG";
        public const string EnvSession = "QA_V4_SESSION_ID";
        /// <summary>원장 §21-1 · 계획서 §3: 이름·장비·성장 벽시계 30분.</summary>
        public const long GrowthGuardSeconds = 30 * 60;

        public static readonly string[] Events =
        {
            "permadeath", "rebuild_offer", "rebuild_accept", "rebuild_decline", "session_end"
        };

        public static readonly string[] Reasons =
        {
            "조작 불명확", "난이도", "손실 분노", "다시 키우기 지루함", "기술 문제"
        };

        static readonly HashSet<string> _permadeathOnce = new HashSet<string>();
        static string _sessionId;
        static string _forcedSession;
        static string _root;

        public static bool Blocked
        {
            get
            {
                string raw = Environment.GetEnvironmentVariable(EnvNo);
                return raw == "1" || string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string SessionId
        {
            get
            {
                EnsureSession();
                return _sessionId ?? "";
            }
        }

        public static string LogDir
        {
            get
            {
                string root = FindRoot();
                if (string.IsNullOrEmpty(root)) return "";
                return Path.Combine(root, "output", "qa", "ashes-to-stars", "v4");
            }
        }

        public static string CurrentPath
        {
            get
            {
                string dir = LogDir;
                if (string.IsNullOrEmpty(dir)) return "";
                EnsureSession();
                return Path.Combine(dir, "session_" + _sessionId + ".log");
            }
        }

        public static string BuildId()
        {
            try { return Application.version ?? "1.0"; }
            catch { return "1.0"; }
        }

        public static bool IsEvent(string ev)
        {
            if (string.IsNullOrEmpty(ev)) return false;
            for (int i = 0; i < Events.Length; i++)
                if (Events[i] == ev) return true;
            return false;
        }

        /// <summary>빈 칸은 사람 사유가 아직 없을 때(permadeath·offer). 값이 있으면 다섯만.</summary>
        public static bool IsReason(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return true;
            for (int i = 0; i < Reasons.Length; i++)
                if (Reasons[i] == reason) return true;
            return false;
        }

        public static bool MeetsGrowthGuard(CharacterRecord ch)
        {
            if (ch == null || ch.GrowthStartUnix <= 0) return false;
            return LifeSystem.NowUnix() - ch.GrowthStartUnix >= GrowthGuardSeconds;
        }

        public static void ForceSessionIdForTest(string id)
        {
            _forcedSession = string.IsNullOrEmpty(id) ? null : Sanitize(id);
            _sessionId = _forcedSession;
        }

        public static void NotePermadeath(CharacterRecord ch)
        {
            if (ch == null || !ch.IsDeleted) return;
            if (!string.IsNullOrEmpty(ch.Id) && _permadeathOnce.Contains(ch.Id)) return;
            if (!MeetsGrowthGuard(ch)) return;
            if (!Write("permadeath", ch, "")) return;
            if (!string.IsNullOrEmpty(ch.Id)) _permadeathOnce.Add(ch.Id);
        }

        public static void NoteRebuildOffer(CharacterRecord ch)
        {
            Write("rebuild_offer", ch, "");
        }

        public static void NoteRebuildAccept(CharacterRecord ch, string reason = "")
        {
            Write("rebuild_accept", ch, reason);
        }

        public static void NoteRebuildDecline(CharacterRecord ch, string reason)
        {
            Write("rebuild_decline", ch, reason);
        }

        public static void NoteSessionEnd(CharacterRecord ch, string reason)
        {
            Write("session_end", ch, reason);
        }

        public static void ResetForTest()
        {
            _permadeathOnce.Clear();
            _sessionId = null;
            _forcedSession = null;
            _root = null;
        }

        static bool Write(string ev, CharacterRecord ch, string reason)
        {
            if (Blocked) return false;
            if (!IsEvent(ev)) return false;
            if (!IsReason(reason)) return false;
            string path = CurrentPath;
            if (string.IsNullOrEmpty(path)) return false;

            long now = LifeSystem.NowUnix();
            string tUtc = DateTimeOffset.FromUnixTimeSeconds(now).UtcDateTime
                .ToString("yyyy-MM-ddTHH:mm:ss") + "Z";
            string charId = ch != null && !string.IsNullOrEmpty(ch.Id) ? ch.Id : "";
            var sb = new StringBuilder(192);
            sb.Append("{\"session_id\":\"").Append(Esc(_sessionId))
              .Append("\",\"build\":\"").Append(Esc(BuildId()))
              .Append("\",\"t_utc\":\"").Append(Esc(tUtc))
              .Append("\",\"char_id\":\"").Append(Esc(charId))
              .Append("\",\"event\":\"").Append(Esc(ev))
              .Append("\",\"reason_enum\":\"").Append(Esc(reason ?? ""))
              .Append("\"}\n");
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[V4LoopLog] 기록 실패: " + e.Message);
                return false;
            }
        }

        static void EnsureSession()
        {
            if (!string.IsNullOrEmpty(_sessionId)) return;
            if (!string.IsNullOrEmpty(_forcedSession))
            {
                _sessionId = _forcedSession;
                return;
            }
            string env = Environment.GetEnvironmentVariable(EnvSession);
            if (!string.IsNullOrEmpty(env))
            {
                _sessionId = Sanitize(env);
                return;
            }
            _sessionId = Guid.NewGuid().ToString("N");
        }

        static string Sanitize(string id)
        {
            if (string.IsNullOrEmpty(id)) return "unknown";
            var sb = new StringBuilder(id.Length);
            for (int i = 0; i < id.Length; i++)
            {
                char c = id[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9') || c == '_' || c == '-')
                    sb.Append(c);
            }
            return sb.Length == 0 ? "unknown" : sb.ToString();
        }

        static string FindRoot()
        {
            if (!string.IsNullOrEmpty(_root)) return _root;
            try
            {
                var d = new DirectoryInfo(Application.dataPath);
                while (d != null)
                {
                    if (File.Exists(Path.Combine(d.FullName, "loop", "board.py")))
                    {
                        _root = d.FullName;
                        return _root;
                    }
                    d = d.Parent;
                }
            }
            catch
            {
                // 배치 밖이면 경로를 못 찾는다 — 그때는 쓰지 않는다.
            }
            return null;
        }

        static string Esc(string s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
    }
}
