// 설정·전적 저장 — `PlayerPrefs` 한 곳. 게임으로서 빠져 있던 것(2026-09-18):
//   · 맵·난이도·아이템·날씨·Boom·고른 탱크·소리 설정이 켤 때마다 초기화됐다.
//   · 결과 화면은 있는데 승·패 누계가 프로세스와 함께 사라졌다.
//
// ⚠️ **헤드리스·자동 모드에서는 읽지도 쓰지도 않는다.** 읽으면 하네스가 사람의 마지막 설정(맵·난이도)을
//    물려받아 실행마다 결과가 달라지고, 쓰면 하네스 판이 사람 전적에 섞인다. 호출부(BattleDemo.Start ·
//    GameOver)가 사람 판인지 먼저 가른다 — 이 파일은 그 판정을 하지 않는다(판정을 두 곳에 두지 않기 위해).
//
// ⚠️ 명령줄 인자가 준 값은 저장값보다 우선한다. 그래서 `Load` 는 **기본값 그대로인 항목만** 채운다.
//    (`-map Crater` 로 켰는데 저장된 TwinHills 가 덮어쓰면 인자가 거짓말이 된다.)

using UnityEngine;

namespace Tankfall.View
{
    public static class Prefs
    {
        /// <summary>키 접두사. 자체검사는 이걸 바꿔 사람의 저장값과 격리된 곳에 쓰고 지운다.</summary>
        public static string Namespace = "tankfall.";
        static string P => Namespace;

        static readonly string[] SettingKeys = { "map", "difficulty", "items", "weather", "boom", "sfxoff", "musicoff", "roster", "volume" };

        /// <summary>현재 네임스페이스의 키를 전부 지운다(자체검사 정리용).</summary>
        public static void DeleteAll(int difficultyCount)
        {
            foreach (var k in SettingKeys) PlayerPrefs.DeleteKey(P + k);
            for (int i = 0; i < difficultyCount; i++)
                for (int o = 0; o < 3; o++) PlayerPrefs.DeleteKey(P + $"rec.{i}.{o}");
            PlayerPrefs.Save();
        }

        static bool Has(string k) => PlayerPrefs.HasKey(P + k);
        static int GetInt(string k, int d) => PlayerPrefs.GetInt(P + k, d);
        static void SetInt(string k, int v) => PlayerPrefs.SetInt(P + k, v);
        static string GetStr(string k, string d) => PlayerPrefs.GetString(P + k, d);
        static void SetStr(string k, string v) => PlayerPrefs.SetString(P + k, v);

        // ── 전체 음량 ─────────────────────────────────────────
        /// <summary>
        /// 설정 화면의 전체 음량(0~1). 설정 묶음(`Settings`)과 따로 둔다 — 그건 "판마다 바꾸는 값"이라
        /// 한 번에 저장되고, 이건 화살표를 누를 때마다 즉시 저장되는 환경값이다.
        ///
        /// ⚠️ **`PlayerPrefs` 를 직접 쓰지 마라.** 음량만 네임스페이스 밖에 있던 동안
        ///    `-settingsselftest` 가 사람의 저장 파일에 `tankfall_volume` 을 실제로 썼다(2026-09-18 실측:
        ///    다른 키는 0개인데 이것만 남았다). 저장은 전부 이 파일을 거쳐야 자체검사 격리가 걸린다.
        /// </summary>
        public static float Volume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(P + "volume", 1f));
            set { PlayerPrefs.SetFloat(P + "volume", Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        // ── 설정 ──────────────────────────────────────────────
        /// <summary>저장할 설정 묶음. 값 이름은 BattleDemo 의 필드와 같다.</summary>
        public struct Settings
        {
            public int Map, Difficulty, ItemSlots, Weather;   // Weather: 0 자동 · 1 맑음 · 2 눈
            public bool Boom, SfxOff, MusicOff;
            public string Roster;                            // "Cannon,Carrot,Laser"
        }

        public static void Save(in Settings s)
        {
            SetInt("map", s.Map); SetInt("difficulty", s.Difficulty); SetInt("items", s.ItemSlots);
            SetInt("weather", s.Weather); SetInt("boom", s.Boom ? 1 : 0);
            SetInt("sfxoff", s.SfxOff ? 1 : 0); SetInt("musicoff", s.MusicOff ? 1 : 0);
            SetStr("roster", s.Roster ?? "");
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 저장값을 읽되 <paramref name="cur"/> 가 <paramref name="def"/> 와 같은 항목만 바꾼다(머리말: 인자 우선).
        /// 저장된 적 없는 항목은 그대로 둔다. 바뀐 게 하나라도 있으면 true.
        /// </summary>
        public static bool Load(ref Settings cur, in Settings def)
        {
            bool any = false;
            if (Has("map") && cur.Map == def.Map) { cur.Map = GetInt("map", def.Map); any = true; }
            if (Has("difficulty") && cur.Difficulty == def.Difficulty) { cur.Difficulty = GetInt("difficulty", def.Difficulty); any = true; }
            if (Has("items") && cur.ItemSlots == def.ItemSlots) { cur.ItemSlots = GetInt("items", def.ItemSlots); any = true; }
            if (Has("weather") && cur.Weather == def.Weather) { cur.Weather = GetInt("weather", def.Weather); any = true; }
            if (Has("boom") && cur.Boom == def.Boom) { cur.Boom = GetInt("boom", 0) == 1; any = true; }
            if (Has("roster") && cur.Roster == def.Roster) { cur.Roster = GetStr("roster", def.Roster); any = true; }
            // 소리는 인자로 정하는 값이 아니다 — 저장돼 있으면 항상 따른다.
            if (Has("sfxoff")) { cur.SfxOff = GetInt("sfxoff", 0) == 1; any = true; }
            if (Has("musicoff")) { cur.MusicOff = GetInt("musicoff", 0) == 1; any = true; }
            return any;
        }

        // ── 전적 ──────────────────────────────────────────────
        public enum Outcome { Win, Lose, Draw }

        /// <summary>한 판의 결과를 난이도별로 누적한다. 연습장·하네스 판은 호출부가 걸러야 한다.</summary>
        public static void Record(int difficulty, Outcome o)
        {
            string k = $"rec.{difficulty}.{(int)o}";
            SetInt(k, GetInt(k, 0) + 1);
            PlayerPrefs.Save();
        }

        public static (int Win, int Lose, int Draw) Total(int difficultyCount)
        {
            int w = 0, l = 0, d = 0;
            for (int i = 0; i < difficultyCount; i++)
            {
                w += GetInt($"rec.{i}.{(int)Outcome.Win}", 0);
                l += GetInt($"rec.{i}.{(int)Outcome.Lose}", 0);
                d += GetInt($"rec.{i}.{(int)Outcome.Draw}", 0);
            }
            return (w, l, d);
        }

        /// <summary>"통산 12승 8패 1무" — 한 판도 없으면 빈 문자열(타이틀에 0승 0패를 띄우면 초라하다).</summary>
        public static string TotalText(int difficultyCount)
        {
            var (w, l, d) = Total(difficultyCount);
            if (w + l + d == 0) return "";
            return d > 0 ? $"통산 {w}승 {l}패 {d}무" : $"통산 {w}승 {l}패";
        }
    }
}
