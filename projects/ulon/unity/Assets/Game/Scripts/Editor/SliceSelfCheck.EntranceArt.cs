using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// §8.2 — **입구 구조물도 등록 CC0 모델에서 온다**(검수 2026-09-07 반려).
    ///
    /// 소품 자격 게이트(`AssertPropsQualified`)는 대상을 `PropScope` 구역(던전 방·마을·지역)으로 모은다.
    /// 던전 입구는 그 어느 구역에도 속하지 않아 **자격 검사 밖**이었고, 그래서 소품 랩에서 이미 반려된
    /// 「무텍스처 색칠 큐브」가 입구에만 살아남았다(대낮 야외의 검은 비석 셋).
    ///
    /// 대상은 이름 목록이 아니라 **규칙**으로 모은다 — 씬의 `DungeonEntranceFrame` 전부.
    /// 목록으로 모으면 넷째 입구가 생겼을 때 조용히 빠진다(두더지잡기 교훈).
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>문구멍이 「어둠」으로 통하는 상한 — 이보다 밝으면 그냥 색칠 판때기다.</summary>
        const float EntrancePortalDarkMax = 0.15f;

        /// <summary>
        /// 화면에 실제로 나오는 밝기 = **틴트 × 텍스처 평균**. 첫 실행에서 이걸 틴트만으로 쟀다가
        /// 멀쩡한 문구멍이 1.00으로 읽혀 빨간불이 났다 — `MakeNoiseMat`은 어둠을 텍스처에 굽고 틴트는
        /// 흰색으로 둔다(밝기 대비를 명도로 쟀다가 틀렸던 VFX 랩과 **같은 「축을 잘못 골랐다」**).
        /// 텍스처는 PNG 파일을 직접 읽어 평균한다 — 임포트된 텍스처는 배치모드에서 GetPixels가 막힌다.
        /// </summary>
        static float AlbedoBrightness(Material mat)
        {
            var c = mat.color;
            float tint = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            var tex = mat.mainTexture;
            if (tex == null)
                return tint;
            string path = UnityEditor.AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
                return tint;
            var probe = new Texture2D(2, 2);
            float mean = tint;
            if (probe.LoadImage(System.IO.File.ReadAllBytes(path)))
            {
                var px = probe.GetPixels();
                float sum = 0f;
                for (int i = 0; i < px.Length; i++)
                    sum += Mathf.Max(px[i].r, Mathf.Max(px[i].g, px[i].b));
                mean = tint * (px.Length > 0 ? sum / px.Length : 1f);
            }
            UnityEngine.Object.DestroyImmediate(probe);
            return mean;
        }

        static List<Transform> EntranceFrames()
        {
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var frames = new List<Transform>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name == VisualSliceBuilder.EntranceFrameObject && all[i].gameObject.activeInHierarchy)
                    frames.Add(all[i]);
            return frames;
        }

        /// <summary>
        /// 문틀 조각 하나의 자격 사유. 통과면 빈 문자열.
        /// **게이트와 네거티브 컨트롤이 같은 함수를 쓴다.**
        ///
        /// 예외는 문구멍 하나뿐이다 — 안쪽의 어둠이라 등록 메시로 만들 수 없다(아래가 실제 방이 아니라
        /// 지표라서 뚫어 두면 잔디가 비친다). 예외가 샛길이 되지 않게 **이름이 그것이고 실제로 어두울 때만**
        /// 봐준다. 이름만 `EntrancePortal`로 바꿔 붙인 밝은 판때기는 통과하지 못한다.
        /// </summary>
        static string EntrancePieceReason(Transform piece)
        {
            if (piece.name.StartsWith(VisualSliceBuilder.EntrancePortalObject, StringComparison.Ordinal))
            {
                var r = piece.GetComponentInChildren<Renderer>(false);
                if (r == null || r.sharedMaterial == null)
                    return piece.name + ": 문구멍에 재질이 없다";
                float bright = AlbedoBrightness(r.sharedMaterial);
                if (bright > EntrancePortalDarkMax)
                    return piece.name + ": 문구멍이 밝다(" + bright.ToString("0.00") + " > " + EntrancePortalDarkMax +
                        ") — 어두운 구멍만 예외다. 밝은 판때기는 §8.2 프리미티브 그대로다";
                return "";
            }
            return PropArt.ReasonUnqualified(piece.gameObject);
        }

        static void AssertEntranceArtQualified()
        {
            var frames = EntranceFrames();
            if (frames.Count == 0)
                throw new InvalidOperationException("입구 문틀을 한 개도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            int pieces = 0;
            int portals = 0;
            var reasons = new List<string>();
            for (int f = 0; f < frames.Count; f++)
            {
                int inFrame = 0;
                for (int c = 0; c < frames[f].childCount; c++)
                {
                    var child = frames[f].GetChild(c);
                    if (!child.gameObject.activeInHierarchy || child.GetComponentInChildren<Renderer>(false) == null)
                        continue;
                    inFrame++;
                    pieces++;
                    if (child.name.StartsWith(VisualSliceBuilder.EntrancePortalObject, StringComparison.Ordinal))
                        portals++;
                    string why = EntrancePieceReason(child);
                    if (why != "")
                        reasons.Add(frames[f].parent != null ? frames[f].parent.name + " 입구 — " + why : "입구 — " + why);
                }
                if (inFrame == 0)
                    reasons.Add((frames[f].parent != null ? frames[f].parent.name : "?") + " 입구 문틀이 비었습니다(0이면 실패).");
            }
            // 문구멍이 하나도 없으면 「예외가 없으니 전부 통과」가 된다 — 그건 잰 게 아니다.
            if (portals == 0)
                reasons.Add("문구멍을 한 개도 못 찾았습니다 — 예외 경로가 실제로 밟히는지 확인할 수 없습니다(0이면 실패).");
            if (reasons.Count > 0)
                throw new InvalidOperationException("입구 구조물 자격 미달 " + reasons.Count + "건:\n  " + string.Join("\n  ", reasons) +
                    "\n  등록된 CC0 모델만 쓴다(자격 원장 Editor/PropArt.cs).");
            Debug.Log("[Ulon] 입구 구조물 자격 통과 — 문틀 " + frames.Count + "곳 조각 " + pieces +
                      "개(문구멍 " + portals + "개 예외) 전부 PropArt 등록 CC0 모델, 프리미티브 0개");
        }

        /// <summary>
        /// 네거티브 컨트롤 — **결함을 실제로 씬에 세워** 두 길을 다 막는지 본다.
        /// ① 색칠 큐브를 문틀에 붙이면 빨간불(옛 입구가 정확히 이 모양이었다).
        /// ② 문구멍 이름을 단 **밝은** 판을 붙여도 빨간불(예외를 샛길로 못 쓴다).
        /// </summary>
        static void AssertEntranceArtNegativeControl()
        {
            var frames = EntranceFrames();
            if (frames.Count == 0)
                throw new InvalidOperationException("입구 문틀이 없어 자격 네거티브 컨트롤을 할 수 없습니다.");
            var frame = frames[0];

            var fake = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fake.name = "EntranceFakePillar";
            fake.transform.SetParent(frame, true);
            fake.transform.position = frame.position;
            bool cubeRed = false;
            try
            {
                try { AssertEntranceArtQualified(); }
                catch (InvalidOperationException) { cubeRed = true; }
            }
            finally { UnityEngine.Object.DestroyImmediate(fake); }
            if (!cubeRed)
                throw new InvalidOperationException("입구 자격 네거티브 컨트롤 실패 — 색칠 큐브를 문틀에 세웠는데도 통과했습니다.");

            var bright = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bright.name = VisualSliceBuilder.EntrancePortalObject + "Fake";
            bright.transform.SetParent(frame, true);
            bright.transform.position = frame.position;
            var mat = new Material(Shader.Find("Standard")) { color = new Color(0.9f, 0.9f, 0.9f) };
            bright.GetComponent<Renderer>().sharedMaterial = mat;
            bool brightRed = false;
            try
            {
                try { AssertEntranceArtQualified(); }
                catch (InvalidOperationException) { brightRed = true; }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bright);
                UnityEngine.Object.DestroyImmediate(mat);
            }
            if (!brightRed)
                throw new InvalidOperationException("입구 자격 네거티브 컨트롤 실패 — 문구멍 이름을 단 밝은 판이 통과했습니다(예외가 샛길이 됐습니다).");

            Debug.Log("[Ulon] 입구 구조물 자격 네거티브 컨트롤 통과 — 색칠 큐브·이름만 문구멍인 밝은 판 둘 다 FAIL");
        }
    }
}
