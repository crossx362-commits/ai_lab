using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Ulon.Server;
using UnityEngine;

namespace Ulon.Client
{
    /// <summary>
    /// **실행 중인 게임에서 사람형이 실제로 움직이는가**(검수 지시 2026-09-07 (a)).
    ///
    /// 편집기 샷은 애니메이션이 안 돌아 `QaShots`가 Idle 클립을 손으로 입혀 찍는다 — 그건
    /// **검사 환경의 보정**이라 「게임에서도 돈다」의 증거가 못 된다. 그래서 스탠드얼론을 띄워
    /// **본 위치를 두 시점에 재서 차이**를 본다. 「Animator가 있다·컨트롤러가 있다」는 대리 지표다
    /// (컨트롤러가 있어도 상태가 멈춰 있으면 T포즈다) — 움직임 자체를 재라.
    ///
    /// 게이트가 아니다. 사실을 찍어 남기고 판정은 검수가 한다.
    /// 실행: `bash tools/idle_check.sh` → `builds/qa/idle_check.md` + `idle_01_hunt.png`
    /// </summary>
    public sealed class IdleCheck : MonoBehaviour
    {
        static string dir;
        static string outPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = System.Environment.GetCommandLineArgs();
            bool on = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-idlecheck") on = true;
                if (args[i] == "-shotdir" && i + 1 < args.Length) dir = args[i + 1];
                if (args[i] == "-idleout" && i + 1 < args.Length) outPath = args[i + 1];
            }
            if (!on)
                return;
            var go = new GameObject("IdleCheck");
            DontDestroyOnLoad(go);
            go.AddComponent<IdleCheck>();
        }

        /// <summary>움직였다고 볼 최소 이동(m) — 부동소수 잡음보다 크고, 숨쉬기 모션보다 작다.</summary>
        const float MoveEpsilon = 0.001f;

        IEnumerator Start()
        {
            if (string.IsNullOrEmpty(dir))
                dir = Application.persistentDataPath;
            if (string.IsNullOrEmpty(outPath))
                outPath = Path.Combine(dir, "idle_check.md");
            Directory.CreateDirectory(dir);
            Screen.SetResolution(1280, 720, false);
            for (int i = 0; i < 30; i++) yield return null;

            if (PersistDriver.Creating)
            {
                var picks = new[] { Ulon.Shared.SkillId.Swordsmanship, Ulon.Shared.SkillId.Mining, Ulon.Shared.SkillId.Blacksmithing };
                var snap = Ulon.Shared.CharacterCreate.Build(PersistDriver.AccountKey(), "검수용", 0, 30, 25, 25, picks, new[] { 50f, 30f, 20f });
                PersistDriver.Commit(snap);
                for (int i = 0; i < 20; i++) yield return null;
            }

            var world = OfflineWorld.Instance;
            if (world == null || world.Player == null)
            {
                Debug.Log("[Ulon] idle 실측 실패 — 월드/플레이어가 없습니다.");
                Application.Quit(1);
                yield break;
            }

            var actors = FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var before = new Dictionary<CharacterController, Vector3[]>();
            for (int i = 0; i < actors.Length; i++)
                before[actors[i]] = Sample(actors[i]);

            // 한 사이클을 충분히 돌 만큼 기다린다(0.5초로는 프레임 하나 차이로 「안 움직였다」가 나올 수 있다).
            float t0 = Time.time;
            while (Time.time - t0 < 1.5f) yield return null;

            var sb = new StringBuilder();
            sb.AppendLine("# 실행 중 idle 실측 (스탠드얼론)");
            sb.AppendLine();
            sb.AppendLine("본 위치를 1.5초 간격으로 두 번 재서 **움직였는지**를 본다. 「Animator/컨트롤러가 있다」는");
            sb.AppendLine("대리 지표라 안 쓴다(있어도 멈춰 있으면 T포즈다). 단위 m.");
            sb.AppendLine();
            sb.AppendLine("| 액터 | 최대 본 이동 | 애니메이터 | 컨트롤러 | 상태 | 판정 |");
            sb.AppendLine("| --- | --- | --- | --- | --- | --- |");
            int moving = 0, still = 0;
            for (int i = 0; i < actors.Length; i++)
            {
                var a = actors[i];
                var now = Sample(a);
                var was = before[a];
                float max = 0f;
                if (was != null && now != null && was.Length == now.Length)
                    for (int b = 0; b < now.Length; b++)
                        max = Mathf.Max(max, Vector3.Distance(was[b], now[b]));
                var anim = a.GetComponentInChildren<Animator>(true);
                bool hasCtrl = anim != null && anim.runtimeAnimatorController != null;
                string state = "-";
                if (hasCtrl && anim.isActiveAndEnabled && anim.layerCount > 0)
                {
                    var info = anim.GetCurrentAnimatorStateInfo(0);
                    state = info.shortNameHash != 0 ? info.normalizedTime.ToString("0.00") + "회전" : "(상태 없음)";
                }
                bool ok = max > MoveEpsilon;
                if (ok) moving++; else still++;
                sb.AppendLine("| " + a.name + " | " + max.ToString("0.0000") + " | " + (anim != null ? "있음" : "**없음**") +
                              " | " + (hasCtrl ? "있음" : "**없음**") + " | " + state + " | " + (ok ? "돈다" : "**멈춤(T포즈)**") + " |");
            }
            sb.AppendLine();
            sb.AppendLine("합계: 돈다 " + moving + "체 / 멈춤 " + still + "체 (이 실행에 **있는** 사람형 " + actors.Length + "체)");
            sb.AppendLine();
            // **덮은 것과 안 덮은 것을 이름으로 적는다**(검수 지시 2026-09-07) — 스탠드얼론 오프라인
            // 월드에는 편집기 씬의 사람형 일부만 존재한다. 숫자만 있으면 다음 사람이 「전수 통과」로 읽는다.
            string rosterPath = Path.Combine(dir, "scene_roster.txt");
            if (File.Exists(rosterPath))
            {
                var have = new HashSet<string>();
                for (int i = 0; i < actors.Length; i++) have.Add(actors[i].name);
                var missing = new List<string>();
                foreach (var line in File.ReadAllLines(rosterPath))
                {
                    string nm = line.Trim();
                    if (nm.Length > 0 && !have.Contains(nm))
                        missing.Add(nm);
                }
                sb.AppendLine("## 이 실측이 **안 덮은** 것 (" + missing.Count + "개)");
                sb.AppendLine();
                sb.AppendLine("편집기 씬 명단에는 있으나 스탠드얼론 오프라인 월드에 없어 재지 못했다 — " +
                              "이들은 편집기 게이트(`AssertActorsAnimated`)가 덮는다. " +
                              "명단에는 사람형뿐 아니라 **시설·짐승도 들어 있다**(대장간·화덕 등은 애초에 애니메이션 대상이 아니다) — " +
                              "명단을 여기서 따로 추리지 않는 것은 두 벌이 어긋나지 않게 하려는 것이다:");
                sb.AppendLine();
                sb.AppendLine(missing.Count == 0 ? "(없음 — 명단 전수를 실행에서 쟀다)" : string.Join(", ", missing));
            }
            else
            {
                sb.AppendLine("**주의**: 씬 명단 파일이 없어 「안 덮은 것」을 못 적었다(" + rosterPath +
                              "). `tools/idle_check.sh`가 덤프를 먼저 돌린다 — 이 줄이 보이면 그 단계가 빠진 것이다.");
            }
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log("[Ulon] idle 실측 — 돈다 " + moving + " / 멈춤 " + still + " → " + outPath);

            // 사냥터가 보이는 자리에서 한 장. 검수가 눈으로도 확인할 수 있게.
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(2.0f, 3.4f, 6.4f);
                cam.transform.LookAt(new Vector3(1.0f, 1.0f, 13.4f));
                for (int i = 0; i < 3; i++) yield return null;
                string shot = Path.Combine(dir, "idle_01_hunt.png");
                ScreenCapture.CaptureScreenshot(shot);
                for (int i = 0; i < 10; i++) yield return null;
                Debug.Log("[Ulon] idle 샷 — " + shot);
            }
            Application.Quit(0);
        }

        /// <summary>본 몇 개의 월드 위치 — 스킨드 메시의 본을 쓴다(없으면 자식 트랜스폼).</summary>
        static Vector3[] Sample(CharacterController a)
        {
            var smr = a.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (smr != null && smr.bones != null && smr.bones.Length > 0)
            {
                var list = new List<Vector3>();
                for (int i = 0; i < smr.bones.Length; i++)
                    if (smr.bones[i] != null)
                        list.Add(smr.bones[i].position);
                if (list.Count > 0)
                    return list.ToArray();
            }
            var ts = a.GetComponentsInChildren<Transform>(true);
            var all = new Vector3[ts.Length];
            for (int i = 0; i < ts.Length; i++)
                all[i] = ts[i].position;
            return all;
        }
    }
}
