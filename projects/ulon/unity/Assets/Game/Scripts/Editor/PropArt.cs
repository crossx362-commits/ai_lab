using System;
using UnityEditor;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// §8.2 비주얼 하한 — **실내 소품은 등록된 CC0 모델에서만 온다**(검수 2026-09-06 반려:
    /// 채운 소품이 전부 무텍스처 색칠 큐브였다. 맨바닥 비율 게이트는 「덮기만 하면」 통과해 이걸 놓쳤다).
    ///
    /// `MobArt`와 같은 **자격 원장** 방식이다: 프리미티브 메시(Unity 내장 Cube 등)는 등록 자체가 안 되고,
    /// 원장에 없는 모델도 자격이 없다. 판정은 「보이는 메시가 어느 에셋에서 왔는가」로 한다 —
    /// 재질에 텍스처가 있는지만 보면 노이즈 텍스처를 입힌 큐브가 그대로 통과한다(대리 지표).
    /// </summary>
    public static class PropArt
    {
        public struct Model
        {
            public string Fbx;
            public string License;
            public string Source;
            public string Note;
        }

        const string Town = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/";
        const string Nature = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/";

        public static readonly Model[] Registered =
        {
            new Model { Fbx = Town + "poles.fbx",       License = "CC0", Source = "Kenney Fantasy Town Kit 2.0", Note = "실내 지지 기둥" },
            new Model { Fbx = Town + "planks.fbx",      License = "CC0", Source = "Kenney Fantasy Town Kit 2.0", Note = "널빤지 더미" },
            new Model { Fbx = Town + "cart.fbx",        License = "CC0", Source = "Kenney Fantasy Town Kit 2.0", Note = "짐수레" },
            new Model { Fbx = Town + "stall-bench.fbx", License = "CC0", Source = "Kenney Fantasy Town Kit 2.0", Note = "작업대" },
            new Model { Fbx = Town + "stall-stool.fbx", License = "CC0", Source = "Kenney Fantasy Town Kit 2.0", Note = "궤짝 대용 걸상" },
            new Model { Fbx = Town + "lantern.fbx",     License = "CC0", Source = "Kenney Fantasy Town Kit 2.0", Note = "벽 등불(광원의 출처)" },
            new Model { Fbx = Town + "rock-small.fbx",  License = "CC0", Source = "Kenney Fantasy Town Kit 2.0", Note = "잔해" },
            new Model { Fbx = Town + "rock-wide.fbx",   License = "CC0", Source = "Kenney Fantasy Town Kit 2.0", Note = "잔해" },
            new Model { Fbx = Nature + "rock_smallA.fbx",   License = "CC0", Source = "Kenney Nature Kit", Note = "잔해" },
        };

        public static bool IsRegistered(string assetPath)
        {
            for (int i = 0; i < Registered.Length; i++)
                if (string.Equals(Registered[i].Fbx, assetPath, StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>
        /// 자격 판정. 보이는 메시가 하나도 없거나, 내장 프리미티브거나, 원장에 없는 모델이면 사유를 돌려준다.
        /// 통과면 빈 문자열.
        /// </summary>
        /// <summary>이 소품이 어느 등록 모델인가(자격 통과 오브젝트 기준). 못 찾으면 빈 문자열.</summary>
        public static string ModelKeyOf(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || !rends[i].gameObject.activeInHierarchy)
                    continue;
                var mf = rends[i].GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                    continue;
                string path = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (IsRegistered(path))
                    return System.IO.Path.GetFileNameWithoutExtension(path);
            }
            return "";
        }

        public static string ReasonUnqualified(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            int visible = 0;
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || !rends[i].gameObject.activeInHierarchy)
                    continue;                                   // 꺼 둔 것은 화면에 없다
                visible++;
                var mf = rends[i].GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null)
                    return rends[i].name + ": 메시 없음";
                string path = AssetDatabase.GetAssetPath(mesh);
                if (string.IsNullOrEmpty(path) || path.IndexOf("unity default resources", StringComparison.OrdinalIgnoreCase) >= 0
                    || path.StartsWith("Library/", StringComparison.Ordinal) || path.StartsWith("Resources/", StringComparison.Ordinal))
                    return rends[i].name + ": 내장 프리미티브 메시(" + mesh.name + ") — 색칠 큐브는 소품이 아니다(§8.2)";
                if (!IsRegistered(path))
                    return rends[i].name + ": 미등록 모델 " + path + " — PropArt 원장에 없으면 쓸 수 없다(§11 라이선스·§8.2)";
            }
            if (visible == 0)
                return "보이는 메시가 없다";
            return "";
        }
    }
}
