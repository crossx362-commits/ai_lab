using System;
using UnityEditor;
using UnityEngine;
using Ulon.Client;

namespace Ulon.Editor
{
    /// <summary>
    /// §11 등록 CC0 효과음을 씬에 박는다(멱등: `ActionSfx` 루트를 지우고 다시 짓는다).
    /// 출처는 Kenney RPG Audio(CC0, 오너 승인 2026-09-07) — 팩 전체가 아니라 **실제 쓰는 3개만** 저장소에 있다.
    /// 클립을 찾지 못하면 예외를 던진다 — 조용히 합성 폴백으로 내려가면 「받았다」가 거짓이 된다.
    /// </summary>
    public static class ActionSfxBuilder
    {
        const string Pack = "Assets/_ThirdParty/Kenney/RpgAudio/RAW/Audio/";

        public static readonly (ActionSfx.Kind Kind, string File)[] Clips =
        {
            (ActionSfx.Kind.Hit,   "knifeSlice.ogg"),     // 칼이 맞는 소리
            (ActionSfx.Kind.Heal,  "handleCoins.ogg"),    // 짤랑이는 반짝임 — 회복
            (ActionSfx.Kind.Craft, "metalPot1.ogg"),      // 금속을 두드리는 소리 — 제작
        };

        public static void EnsureActionSfx()
        {
            var old = GameObject.Find(ActionSfx.RootObject);
            if (old != null)
                UnityEngine.Object.DestroyImmediate(old);

            var root = new GameObject(ActionSfx.RootObject);
            for (int i = 0; i < Clips.Length; i++)
            {
                string path = Pack + Clips[i].File;
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                    throw new InvalidOperationException("등록 CC0 효과음이 없습니다: " + path +
                        " (자격 원장 docs/ASSET_REGISTER.md — 등록 밖 클립은 쓰지 않는다)");
                var go = new GameObject(ActionSfx.ObjectFor(Clips[i].Kind));
                go.transform.SetParent(root.transform, false);
                var src = go.AddComponent<AudioSource>();
                src.clip = clip;
                src.playOnAwake = false;
                src.spatialBlend = 1f;
            }
            Debug.Log("[Ulon] 행동 SFX 클립 " + Clips.Length + "종 — Kenney RPG Audio(CC0) 녹음, 합성은 폴백으로만");
        }
    }
}
