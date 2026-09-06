using System.Collections;
using System.IO;
using System.Text;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Client
{
    /// <summary>
    /// **진짜 프레임 시간**을 재는 유일한 정직한 경로(검수 2026-09-07). 개수·렌더 제출 ms는 대리 지표다.
    ///
    /// 스탠드얼론 Development 빌드를 `-frameprobe -frameout <파일>`로 띄웠을 때만 깨어난다 —
    /// 평소 플레이에는 아무 영향이 없다. 세 지점(방·마을·월드)에서 카메라를 세우고 프레임 시간을 모아
    /// 중앙값·p95를 파일로 남기고 종료한다. **매 랩 돌리지 않는다**(빌드 비용) — 기준선 갱신용이다.
    /// </summary>
    public sealed class FrameTimeProbe : MonoBehaviour
    {
        const int Warmup = 60;
        const int Samples = 180;

        static string outPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = System.Environment.GetCommandLineArgs();
            bool on = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-frameprobe") on = true;
                if (args[i] == "-frameout" && i + 1 < args.Length) outPath = args[i + 1];
            }
            if (!on)
                return;
            var go = new GameObject("FrameTimeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<FrameTimeProbe>();
        }

        IEnumerator Start()
        {
            // 상한을 풀어야 「낼 수 있는 프레임」을 잰다 — VSync가 켜져 있으면 60fps로 평평해진다.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            var cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                Write("카메라를 찾지 못해 측정하지 못했습니다.");
                Application.Quit(1);
                yield break;
            }
            // 카메라를 우리가 몰기 위해 따라다니는 스크립트를 끈다.
            var follow = cam.GetComponent<QuarterViewCamera>();
            if (follow != null) follow.enabled = false;

            float ground = 0f;
            if (Physics.Raycast(new Vector3(Dungeon1.InteriorX, 500f, Dungeon1.InteriorZ), Vector3.down, out RaycastHit hit, 1000f))
                ground = hit.point.y;
            var room = new Vector3(Dungeon1.InteriorX, ground - 5.6f + 1f, Dungeon1.InteriorZ);

            var sb = new StringBuilder();
            sb.AppendLine("| 지점 | 중앙값(ms) | p95(ms) | 중앙값 기준 fps |");
            sb.AppendLine("|---|---:|---:|---:|");

            yield return Measure(sb, cam, "던전 1 방", room + new Vector3(4f, 3.5f, 4f), room);
            yield return Measure(sb, cam, "마을 광장", new Vector3(14f, WorldTerrain.LandBase + 12f, 14f), new Vector3(0f, WorldTerrain.LandBase, 0f));
            yield return Measure(sb, cam, "월드 조망", new Vector3(-165f, 95f, -165f), new Vector3(0f, WorldTerrain.LandBase, 0f));

            Write(sb.ToString());
            Application.Quit(0);
        }

        IEnumerator Measure(StringBuilder sb, Camera cam, string name, Vector3 eye, Vector3 look)
        {
            cam.transform.position = eye;
            cam.transform.LookAt(look);
            for (int i = 0; i < Warmup; i++) yield return null;

            var ms = new float[Samples];
            for (int i = 0; i < Samples; i++)
            {
                yield return null;
                ms[i] = Time.unscaledDeltaTime * 1000f;
            }
            System.Array.Sort(ms);
            float median = ms[Samples / 2];
            float p95 = ms[Mathf.Clamp(Mathf.RoundToInt(Samples * 0.95f), 0, Samples - 1)];
            sb.AppendLine("| " + name + " | " + median.ToString("0.0") + " | " + p95.ToString("0.0") +
                          " | " + (median > 0.01f ? (1000f / median).ToString("0") : "?") + " |");
            Debug.Log("[Ulon] 프레임 " + name + " — 중앙값 " + median.ToString("0.0") + "ms, p95 " + p95.ToString("0.0") + "ms");
        }

        static void Write(string text)
        {
            string path = string.IsNullOrEmpty(outPath)
                ? Path.Combine(Application.persistentDataPath, "frame_times.md")
                : outPath;
            File.WriteAllText(path, text);
            Debug.Log("[Ulon] 프레임 시간 기록 — " + path);
        }
    }
}
