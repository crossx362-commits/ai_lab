using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AshesToStars
{
    /// <summary>§16-10 전투 오디오 문법 — 클립 7종 로드·위험 채널 분류·QA_NO 네거티브·소비처 배선.</summary>
    public static class SfxSelfCheck
    {
        static int _fail;
        static readonly StringBuilder _log = new StringBuilder();

        static void Check(bool cond, string what)
        {
            if (!cond) _fail++;
            _log.AppendLine((cond ? "  PASS  " : "  FAIL  ") + what);
        }

        [MenuItem("Ashes to Stars/QA/Sfx Self Check")]
        public static void Run()
        {
            _fail = 0;
            _log.Length = 0;
            string no = Environment.GetEnvironmentVariable(Sfx.EnvNo);
            Environment.SetEnvironmentVariable(Sfx.EnvNo, null);

            Check(!Sfx.Blocked, "기본은 켜짐");
            Check(Sfx.SampleRate == 22050, "합성 샘플레이트");

            // 1) 클립 7종 실로드 + mono 치수
            foreach (Sfx.Signal s in Enum.GetValues(typeof(Sfx.Signal)))
            {
                var clip = Sfx.PeekClip(s);
                Check(clip != null, $"클립 {Sfx.ClipName(s)} 로드");
                if (clip != null)
                    Check(clip.channels == 1 && clip.frequency == Sfx.SampleRate,
                        $"치수 {clip?.name} (실제 {clip?.channels}ch {clip?.frequency}Hz)");
                Check(!string.IsNullOrEmpty(Sfx.LabelOf(s)), $"라벨 {s}");
            }

            // 2) 위험 신호는 영구 손실 직결 4종+소멸 — 일반 징글과 갈린다
            Check(Sfx.IsDanger(Sfx.Signal.DangerZone)
                  && Sfx.IsDanger(Sfx.Signal.BossEnrage)
                  && Sfx.IsDanger(Sfx.Signal.LastLifeEnter)
                  && Sfx.IsDanger(Sfx.Signal.EscapeCast)
                  && Sfx.IsDanger(Sfx.Signal.LastLifeGone),
                "위험 신호 분류(장판·격노·마지막 목숨·탈출·소멸)");
            Check(!Sfx.IsDanger(Sfx.Signal.LevelUp) && !Sfx.IsDanger(Sfx.Signal.DeathLow),
                "레벨업·일반 사망은 일반 채널");

            // 3) 재생 기록 — 위험 채널 카운트와 마지막 신호
            Sfx.ResetForTest();
            Sfx.Play(Sfx.Signal.DangerZone);
            Check(Sfx.LastSignal == Sfx.Signal.DangerZone && Sfx.DangerPlays == 1,
                $"장판 발동 재생 (실제 {Sfx.LastSignal} {Sfx.DangerPlays})");
            Sfx.Play(Sfx.Signal.BossEnrage);
            Check(Sfx.LastSignal == Sfx.Signal.BossEnrage && Sfx.DangerPlays == 2,
                $"격노가 이어서 최우선 (실제 {Sfx.LastSignal} {Sfx.DangerPlays})");
            Sfx.Play(Sfx.Signal.LevelUp);
            Check(Sfx.NormalPlays == 1 && Sfx.DangerPlays == 2,
                $"징글은 별도 채널 (실제 일반 {Sfx.NormalPlays} · 위험 {Sfx.DangerPlays})");
            Check(Sfx.Line().IndexOf("위험", StringComparison.Ordinal) >= 0,
                $"줄 (실제 {Sfx.Line()})");

            // 4) 네거티브 — QA_NO_SFX면 무음
            Environment.SetEnvironmentVariable(Sfx.EnvNo, "1");
            Check(Sfx.Blocked, "QA_NO");
            Sfx.ResetForTest();
            Sfx.Play(Sfx.Signal.EscapeCast);
            Sfx.Play(Sfx.Signal.DeathLow);
            Check(Sfx.LastSignal == (Sfx.Signal)(-1) && Sfx.DangerPlays == 0 && Sfx.NormalPlays == 0,
                $"QA_NO면 재생 0 (실제 {Sfx.LastSignal})");
            Check(Sfx.Line().IndexOf("차단", StringComparison.Ordinal) >= 0,
                $"QA_NO 줄 (실제 {Sfx.Line()})");
            Environment.SetEnvironmentVariable(Sfx.EnvNo, null);

            // 5) 소비처 배선 계약 — 전투·목숨 코드가 실제로 부른다
            string rt = Path.Combine(Application.dataPath, "_Game/Scripts/Runtime");
            var wired = new (string file, string needle)[]
            {
                ("BossBattle.cs", "Sfx.Play(Sfx.Signal.DangerZone)"),
                ("BossBattle.cs", "Sfx.Play(Sfx.Signal.BossEnrage)"),
                ("EmergencyEscape.cs", "Sfx.Play(Sfx.Signal.EscapeCast)"),
                ("LifeSystem.cs", "Sfx.Play(Sfx.Signal.LastLifeGone)"),
                ("LifeSystem.cs", "Sfx.Play(Sfx.Signal.LastLifeEnter)"),
                ("LifeSystem.cs", "Sfx.Play(Sfx.Signal.DeathLow)"),
                ("LifeSystem.cs", "Sfx.Play(Sfx.Signal.LevelUp)"),
            };
            foreach (var (file, needle) in wired)
            {
                string path = Path.Combine(rt, file);
                string src = File.Exists(path) ? File.ReadAllText(path) : "";
                string sig = needle.Substring(needle.LastIndexOf('.') + 1).TrimEnd(')');
                Check(src.IndexOf(needle, StringComparison.Ordinal) >= 0,
                    $"{file} → {sig}");
            }

            Sfx.ResetForTest();
            Environment.SetEnvironmentVariable(Sfx.EnvNo, no);

            if (_fail == 0) Debug.Log("[SfxSelfCheck] PASS\n" + _log);
            else Debug.LogError($"[SfxSelfCheck] FAIL {_fail}건\n" + _log);
            if (_fail > 0) throw new InvalidOperationException($"[SfxSelfCheck] FAIL {_fail}건");
        }
    }
}
