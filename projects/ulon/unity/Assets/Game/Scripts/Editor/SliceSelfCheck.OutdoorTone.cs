using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **야외 소품이 대낮에 검은 덩어리로 보이지 않는가**(검수 관찰 2026-09-08, 25번 샷).
    ///
    /// 발단: 「마을 한복판에 검은 직육면체가 있다」는 지적을 세 번 헛짚었다.
    /// ① 재질 **색**(`material.color`)만 재서 0건 — 이 소품들은 색이 흰색이고 **텍스처가 어둡다**.
    /// ② 던전 텍스처를 쓰는 야외 소품 8개를 찾아 고쳤는데 **화면은 그대로 검었다** — 범인이 아니었다.
    /// ③ 샷 카메라를 그대로 만들어 그 픽셀에 무엇이 투영되는지 물어서야 이름이 나왔다:
    ///    `rock-large`·`rock-wide`·`IronVein`, 재질 `MountainRockProp`(0.15~0.34).
    ///    **우리가 직접 어둡게 칠한 것**이었다(「흰 스티로폼으로 보인다」를 고치다 반대편으로 넘어갔다).
    ///
    /// 그래서 재는 것은 색이 아니라 **눈에 닿는 밝기**다: 재질 색 × 텍스처 평균색.
    /// 대상은 야외(던전 뿌리 밖)에 선 소품 중 **우리가 만든 재질**(`Assets/Game/Art/Env`의 노이즈 재질)
    /// 을 쓰는 것 전수 — 남의 킷 텍스처는 읽기 설정을 건드려야 해서 계측이 세계를 바꾼다(그래서 제외를 선언한다).
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>대낮 야외에서 「검은 덩어리」로 안 읽히는 하한 — 돌은 어두워도 이보다는 밝다.</summary>
        public const float OutdoorToneMin = 0.30f;

        static float Luma(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;

        /// <summary>
        /// 이 게이트가 보는 것 = **야외에 놓인 소품 덩어리**. 제외는 셋이고 전부 선언한다:
        /// ① 던전 뿌리 안(실내는 어두운 것이 맞다) ② **사람·짐승**(옷·가죽이 칙칙한 것은 §8.1 실루엣 축이지
        /// 「검은 덩어리」가 아니다 — 실측에서 몹 옷 0.21·멧돼지 가죽 0.24가 걸렸다)
        /// ③ **바닥에 깔린 것**(높이 0.4m 미만 — 흙길 0.28은 길이라서 어두운 것이고 덩어리가 아니다).
        /// 예외를 늘리지 말 것: 이 셋은 「다른 축」이라서 빼는 것이지 봐주는 것이 아니다.
        /// </summary>
        static bool OutdoorPropRenderer(Renderer r)
        {
            if (r == null || !OurEnvMaterial(r.sharedMaterial))
                return false;
            var t = r.transform;
            if (t.root.name.IndexOf("Dungeon", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (IsBodyPart(t) || r.GetComponentInParent<CharacterController>() != null)
                return false;
            if (r.GetComponentInParent<Terrain>() != null)
                return false;
            return r.bounds.size.y >= 0.4f;
        }

        static bool OurEnvMaterial(Material m)
        {
            if (m == null || m.mainTexture == null)
                return false;
            string path = UnityEditor.AssetDatabase.GetAssetPath(m);
            return !string.IsNullOrEmpty(path) && path.StartsWith("Assets/Game/Art/Env/", StringComparison.Ordinal);
        }

        static float EffectiveTone(Material m)
        {
            var tex = m.mainTexture as Texture2D;
            if (tex == null)
                return Luma(m.color);
            // **계측이 세계를 바꾸면 안 된다** — 임포트된 텍스처는 읽기 설정이 꺼져 있는데(실측:
            // `KenneyDirt`에서 GetPixels32가 던졌다), 그 설정을 켜면 그건 재는 자가 대상을 고친 것이다
            // (조사용 프로브가 실제로 킷 텍스처 두 개의 `.meta`를 바꿔 놓아 되돌렸다).
            // 그래서 **디스크의 PNG를 따로 읽어** 임시 텍스처로 디코드해 잰다.
            string texPath = UnityEditor.AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(texPath) || !System.IO.File.Exists(texPath))
                return Luma(m.color);
            var probe = new Texture2D(2, 2);
            if (!UnityEngine.ImageConversion.LoadImage(probe, System.IO.File.ReadAllBytes(texPath)))
            {
                UnityEngine.Object.DestroyImmediate(probe);
                return Luma(m.color);
            }
            var px = probe.GetPixels32();
            long r = 0, g = 0, b = 0;
            int step = Mathf.Max(1, px.Length / 4096), n = 0;
            for (int i = 0; i < px.Length; i += step) { r += px[i].r; g += px[i].g; b += px[i].b; n++; }
            var avg = new Color(r / (255f * n), g / (255f * n), b / (255f * n));
            UnityEngine.Object.DestroyImmediate(probe);
            var col = m.HasProperty("_Color") ? m.color : Color.white;
            return Luma(new Color(avg.r * col.r, avg.g * col.g, avg.b * col.b));
        }

        static void AssertOutdoorPropsNotBlack()
        {
            var rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var seen = new Dictionary<string, float>();
            var bad = new List<string>();
            int looked = 0;
            for (int i = 0; i < rends.Length; i++)
            {
                if (!OutdoorPropRenderer(rends[i]))
                    continue;
                var m = rends[i].sharedMaterial;
                looked++;
                if (!seen.TryGetValue(m.name, out float tone))
                {
                    tone = EffectiveTone(m);
                    seen[m.name] = tone;
                    if (tone < OutdoorToneMin)
                        bad.Add(m.name + " " + tone.ToString("0.00") + " (" + GroundFit.NodePath(rends[i].transform) + ")");
                }
            }
            if (looked == 0)
                throw new InvalidOperationException("야외 소품 재질을 하나도 못 찾았습니다 — 잰 것이 없습니다(0이면 실패).");
            var tones = new List<string>();
            foreach (var kv in seen)
                tones.Add(kv.Key + " " + kv.Value.ToString("0.00"));
            Debug.Log("[Ulon] 야외 소품 밝기 — 렌더러 " + looked + "개 · 재질 " + seen.Count + "종: " +
                      string.Join(", ", tones) + " (하한 " + OutdoorToneMin.ToString("0.00") + ")");
            if (bad.Count > 0)
                throw new InvalidOperationException("야외 소품이 대낮에 검은 덩어리로 보입니다: " + string.Join(", ", bad) +
                    " — 재질 색이 아니라 **텍스처까지 곱한 밝기**가 하한 아래입니다. `EnsureWorldPropMaterials`의 색 쌍을 올리십시오.");
        }

        /// <summary>NC — 소품 재질 하나를 **실제로 새까맣게** 칠하면 빨간불이어야 한다.</summary>
        static void AssertOutdoorPropsNotBlackNegativeControl()
        {
            var rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Material victim = null;
            for (int i = 0; i < rends.Length && victim == null; i++)
                if (OutdoorPropRenderer(rends[i]))
                    victim = rends[i].sharedMaterial;
            if (victim == null)
                throw new InvalidOperationException("야외 소품 밝기 NC 대상이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var saved = victim.color;
            bool red = false;
            try
            {
                victim.color = new Color(0.05f, 0.05f, 0.05f);
                try { AssertOutdoorPropsNotBlack(); }
                catch (InvalidOperationException) { red = true; }
            }
            finally { victim.color = saved; }
            if (!red)
                throw new InvalidOperationException("야외 소품 밝기 네거티브 컨트롤 실패 — " + victim.name +
                    "을 새까맣게 칠했는데 통과했습니다.");
            Debug.Log("[Ulon] 야외 소품 밝기 네거티브 컨트롤 통과 — 재질 하나를 새까맣게 칠하면 FAIL");
        }
    }
}
