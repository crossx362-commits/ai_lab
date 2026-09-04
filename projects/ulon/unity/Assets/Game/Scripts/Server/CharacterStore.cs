using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using UnityEngine;
using Ulon.Shared;

namespace Ulon.Server
{
    public static class CharacterStore
    {
        public const string BaseUrl = "http://127.0.0.1:8777";
        static Process persistProc;

        static string DataDir
        {
            get
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../data/accounts"));
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static bool EnsureRunning()
        {
            if (Health())
                return true;
            string serverDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../server"));
            string script = Path.Combine(serverDir, "persist.py");
            if (!File.Exists(script))
                return false;
            string python = Path.Combine(serverDir, ".venv/bin/python");
            if (!File.Exists(python))
                python = "python3";
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = python,
                    Arguments = "\"" + script + "\"",
                    WorkingDirectory = serverDir,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.EnvironmentVariables["DATABASE_URL"] = "postgresql://ulon@127.0.0.1:5432/ulon";
                persistProc = Process.Start(psi);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[Ulon] persist 시작 실패, 파일 저장으로 진행: " + e.Message);
                return false;
            }
            DateTime deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                if (Health())
                    return true;
                System.Threading.Thread.Sleep(50);
            }
            return Health();
        }

        public static bool Health()
        {
            try
            {
                using var resp = Get("/health");
                return resp != null && (int)resp.StatusCode == 200;
            }
            catch
            {
                return false;
            }
        }

        public static CharacterSnapshot Load(string accountId)
        {
            if (string.IsNullOrEmpty(accountId))
                return null;
            EnsureRunning();
            if (Health())
            {
                try
                {
                    using var resp = Get("/character/" + Uri.EscapeDataString(accountId));
                    if (resp != null && (int)resp.StatusCode == 200)
                        return UnpackSnap(JsonUtility.FromJson<CharacterSnapshot>(ReadBody(resp)));
                }
                catch (WebException ex)
                {
                    if (ex.Response is HttpWebResponse http && http.StatusCode == HttpStatusCode.NotFound)
                        return LoadFile(accountId);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning("[Ulon] persist HTTP load 실패, 파일로 시도: " + e.Message);
                }
            }
            return LoadFile(accountId);
        }

        public static CharacterSnapshot Save(CharacterSnapshot snap)
        {
            if (snap == null || string.IsNullOrEmpty(snap.AccountId))
                return snap;
            PackSnap(snap);
            SaveFile(snap);
            UnpackSnap(snap);
            EnsureRunning();
            if (!Health())
                return snap;
            try
            {
                PackSnap(snap);
                string json = JsonUtility.ToJson(snap);
                UnpackSnap(snap);
                var req = (HttpWebRequest)WebRequest.Create(BaseUrl + "/character/" + Uri.EscapeDataString(snap.AccountId));
                req.Method = "PUT";
                req.ContentType = "application/json; charset=utf-8";
                req.Timeout = 2000;
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                req.ContentLength = bytes.Length;
                using (var s = req.GetRequestStream())
                    s.Write(bytes, 0, bytes.Length);
                using var resp = (HttpWebResponse)req.GetResponse();
                return UnpackSnap(JsonUtility.FromJson<CharacterSnapshot>(ReadBody(resp))) ?? snap;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[Ulon] persist HTTP save 실패, 파일은 저장됨: " + e.Message);
                return snap;
            }
        }

        public static HouseSnapshot LoadHouse(string plotId)
        {
            if (string.IsNullOrEmpty(plotId))
                return null;
            EnsureRunning();
            if (!Health())
                return null;
            try
            {
                using var resp = Get("/house/" + Uri.EscapeDataString(plotId));
                if (resp != null && (int)resp.StatusCode == 200)
                    return UnpackHouse(JsonUtility.FromJson<HouseSnapshot>(ReadBody(resp)));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[Ulon] house load 실패: " + e.Message);
            }
            return null;
        }

        public static HouseSnapshot SaveHouse(HouseSnapshot snap)
        {
            if (snap == null || string.IsNullOrEmpty(snap.PlotId))
                return snap;
            EnsureRunning();
            if (!Health())
                return snap;
            try
            {
                PackHouse(snap);
                string json = JsonUtility.ToJson(snap);
                UnpackHouse(snap);
                var req = (HttpWebRequest)WebRequest.Create(BaseUrl + "/house/" + Uri.EscapeDataString(snap.PlotId));
                req.Method = "PUT";
                req.ContentType = "application/json; charset=utf-8";
                req.Timeout = 2000;
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                req.ContentLength = bytes.Length;
                using (var s = req.GetRequestStream())
                    s.Write(bytes, 0, bytes.Length);
                using var resp = (HttpWebResponse)req.GetResponse();
                return UnpackHouse(JsonUtility.FromJson<HouseSnapshot>(ReadBody(resp))) ?? snap;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[Ulon] house save 실패: " + e.Message);
                return snap;
            }
        }

        public static StableSnapshot LoadStable(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
                return null;
            EnsureRunning();
            if (!Health())
                return null;
            try
            {
                using var resp = Get("/stable/" + Uri.EscapeDataString(characterId));
                if (resp != null && (int)resp.StatusCode == 200)
                    return JsonUtility.FromJson<StableSnapshot>(ReadBody(resp));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[Ulon] stable load 실패: " + e.Message);
            }
            return null;
        }

        public static StableSnapshot SaveStable(StableSnapshot snap)
        {
            if (snap == null || string.IsNullOrEmpty(snap.CharacterId))
                return snap;
            EnsureRunning();
            if (!Health())
                return snap;
            try
            {
                string json = JsonUtility.ToJson(snap);
                var req = (HttpWebRequest)WebRequest.Create(BaseUrl + "/stable/" + Uri.EscapeDataString(snap.CharacterId));
                req.Method = "PUT";
                req.ContentType = "application/json; charset=utf-8";
                req.Timeout = 2000;
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                req.ContentLength = bytes.Length;
                using (var s = req.GetRequestStream())
                    s.Write(bytes, 0, bytes.Length);
                using var resp = (HttpWebResponse)req.GetResponse();
                return JsonUtility.FromJson<StableSnapshot>(ReadBody(resp)) ?? snap;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[Ulon] stable save 실패: " + e.Message);
                return snap;
            }
        }

        static CharacterSnapshot LoadFile(string accountId)
        {
            string path = Path.Combine(DataDir, accountId + ".json");
            if (!File.Exists(path))
                return null;
            try
            {
                return UnpackSnap(JsonUtility.FromJson<CharacterSnapshot>(File.ReadAllText(path, Encoding.UTF8)));
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[Ulon] persist 파일 load 실패: " + e.Message);
                return null;
            }
        }

        static void SaveFile(CharacterSnapshot snap)
        {
            string path = Path.Combine(DataDir, snap.AccountId + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(snap, true), Encoding.UTF8);
        }


        static void PackItems(ItemRecord[] items)
        {
            if (items == null)
                return;
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                it.MakerId = ExceptionalCraft.PackMaker(it.MakerId, it.Exceptional);
                items[i] = it;
            }
        }

        static void UnpackItems(ItemRecord[] items)
        {
            if (items == null)
                return;
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                ExceptionalCraft.UnpackMaker(it.MakerId, out string maker, out bool ex);
                it.MakerId = maker;
                if (ex)
                    it.Exceptional = true;
                items[i] = it;
            }
        }

        static CharacterSnapshot PackSnap(CharacterSnapshot snap)
        {
            if (snap == null)
                return null;
            PackItems(snap.Inventory);
            PackItems(snap.Bank);
            PackItems(snap.Corpse);
            return snap;
        }

        static CharacterSnapshot UnpackSnap(CharacterSnapshot snap)
        {
            if (snap == null)
                return null;
            UnpackItems(snap.Inventory);
            UnpackItems(snap.Bank);
            UnpackItems(snap.Corpse);
            return snap;
        }

        static HouseSnapshot PackHouse(HouseSnapshot snap)
        {
            if (snap == null)
                return null;
            PackItems(snap.Items);
            return snap;
        }

        static HouseSnapshot UnpackHouse(HouseSnapshot snap)
        {
            if (snap == null)
                return null;
            UnpackItems(snap.Items);
            return snap;
        }

        static HttpWebResponse Get(string path)
        {
            var req = (HttpWebRequest)WebRequest.Create(BaseUrl + path);
            req.Method = "GET";
            req.Timeout = 1500;
            req.Proxy = null;
            return (HttpWebResponse)req.GetResponse();
        }

        static string ReadBody(HttpWebResponse resp)
        {
            using var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8);
            return sr.ReadToEnd();
        }
    }

    public static class OpLog
    {
        static string Root
        {
            get
            {
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../data"));
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        static string LogPath => Path.Combine(Root, "oplog.jsonl");
        static string FrozenPath => Path.Combine(Root, "frozen.txt");

        public static void Write(string kind, string account, string obj, string msg)
        {
            string line = DateTime.UtcNow.ToString("o") + "|" + (kind ?? "") + "|" + (account ?? "") + "|"
                          + (obj ?? "") + "|" + (msg ?? "").Replace('\n', ' ').Replace('|', '/');
            File.AppendAllText(LogPath, line + "\n", Encoding.UTF8);
        }

        public static string[] Recent(int n)
        {
            if (!File.Exists(LogPath))
                return Array.Empty<string>();
            string[] lines = File.ReadAllLines(LogPath, Encoding.UTF8);
            if (n < 1)
                n = 1;
            int start = lines.Length > n ? lines.Length - n : 0;
            int count = lines.Length - start;
            var cut = new string[count];
            Array.Copy(lines, start, cut, 0, count);
            return cut;
        }

        public static bool IsFrozen(string account)
        {
            if (string.IsNullOrEmpty(account) || !File.Exists(FrozenPath))
                return false;
            string[] lines = File.ReadAllLines(FrozenPath, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Trim() == account)
                    return true;
            return false;
        }

        public static void Freeze(string account, bool frozen)
        {
            if (string.IsNullOrEmpty(account))
                return;
            var keep = new System.Collections.Generic.List<string>();
            if (File.Exists(FrozenPath))
            {
                string[] lines = File.ReadAllLines(FrozenPath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string t = lines[i].Trim();
                    if (t.Length == 0 || t == account)
                        continue;
                    keep.Add(t);
                }
            }
            if (frozen)
                keep.Add(account);
            File.WriteAllLines(FrozenPath, keep.ToArray(), Encoding.UTF8);
            Write("gm", account, "freeze", frozen ? "on" : "off");
        }

        public static string Backup()
        {
            string src = Path.Combine(Root, "accounts");
            string dest = Path.Combine(Root, "backups", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(dest);
            if (Directory.Exists(src))
            {
                string[] files = Directory.GetFiles(src, "*.json");
                for (int i = 0; i < files.Length; i++)
                    File.Copy(files[i], Path.Combine(dest, Path.GetFileName(files[i])), true);
            }
            if (File.Exists(LogPath))
                File.Copy(LogPath, Path.Combine(dest, "oplog.jsonl"), true);
            Write("gm", "", "backup", dest);
            return dest;
        }
    }
}
