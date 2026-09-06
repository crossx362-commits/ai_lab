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

        const string Dungeon = "Assets/_ThirdParty/KayKit/Dungeon/RAW/Models/";
        const string Town = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/Models/";

        // 던전 소품은 KayKit Dungeon Remastered(CC0, 오너 승인 2026-09-06 도입)에서 온다.
        // 그 전에는 마을 가구(걸상·벤치·짐수레)로 대신했고 검수가 「가구 창고」라고 반려했다 —
        // 궤짝·통·부서진 상자 계열이 저장소에 아예 없었던 것이 원인이었다.
        public static readonly Model[] Registered =
        {
            new Model { Fbx = Dungeon + "barrel_large.obj",        License = "CC0", Source = "KayKit Dungeon Remastered 1.0", Note = "큰 통" },
            new Model { Fbx = Dungeon + "barrel_small.obj",        License = "CC0", Source = "KayKit Dungeon Remastered 1.0", Note = "작은 통" },
            new Model { Fbx = Dungeon + "barrel_small_stack.obj",  License = "CC0", Source = "KayKit Dungeon Remastered 1.0", Note = "통 더미" },
            new Model { Fbx = Dungeon + "box_large.obj",           License = "CC0", Source = "KayKit Dungeon Remastered 1.0", Note = "큰 궤짝" },
            new Model { Fbx = Dungeon + "box_small.obj",           License = "CC0", Source = "KayKit Dungeon Remastered 1.0", Note = "작은 궤짝" },
            new Model { Fbx = Dungeon + "box_stacked.obj",         License = "CC0", Source = "KayKit Dungeon Remastered 1.0", Note = "쌓인 궤짝" },
            new Model { Fbx = Dungeon + "crates_stacked.obj",      License = "CC0", Source = "KayKit Dungeon Remastered 1.0", Note = "쌓인 나무상자" },
            new Model { Fbx = Dungeon + "chest.obj",               License = "CC0", Source = "KayKit Dungeon Remastered 1.0", Note = "궤" },
            new Model { Fbx = Dungeon + "pillar.obj",              License = "CC0", Source = "KayKit Dungeon Remastered 1.0", Note = "돌기둥" },
            new Model { Fbx = Dungeon + "pillar_decorated.obj",    License = "CC0", Source = "KayKit Dungeon Remastered 1.0", Note = "장식 돌기둥" },
            new Model { Fbx = Dungeon + "rubble_large.obj",        License = "CC0", Source = "KayKit Dungeon Remastered 1.0", Note = "잔해 더미" },
            new Model { Fbx = Dungeon + "rubble_half.obj",         License = "CC0", Source = "KayKit Dungeon Remastered 1.0", Note = "잔해" },
            new Model { Fbx = Dungeon + "table_medium_broken.obj", License = "CC0", Source = "KayKit Dungeon Remastered 1.0", Note = "부서진 탁자" },
            new Model { Fbx = Dungeon + "torch_mounted.obj",       License = "CC0", Source = "KayKit Dungeon Remastered 1.0", Note = "벽 횃불(광원의 출처)" },
            new Model { Fbx = Town + "planks.fbx",                 License = "CC0", Source = "Kenney Fantasy Town Kit 2.0", Note = "널빤지 — 마을 가구 중 유일하게 남긴 것" },
        };

        /// <summary>
        /// 마을·지역 소품은 개수가 많아 파일 하나씩 적을 수 없다 — **팩 단위**로 등록한다(검수 랩 C).
        /// 팩도 라이선스·출처를 여기 적는다. 등록 안 된 곳에서 온 메시는 여전히 자격이 없다.
        /// </summary>
        public struct Pack
        {
            public string PathPrefix;
            public string License;
            public string Source;
            public string Note;
        }

        public static readonly Pack[] RegisteredPacks =
        {
            new Pack { PathPrefix = "Assets/_ThirdParty/Kenney/FantasyTown/RAW/",
                License = "CC0", Source = "Kenney Fantasy Town Kit 2.0", Note = "마을 건물·울타리·좌판·장식" },
            new Pack { PathPrefix = "Assets/_ThirdParty/Kenney/Nature/RAW/",
                License = "CC0", Source = "Kenney Nature Kit 1.0", Note = "나무·풀·바위 — 숲·평지 산포" },
            new Pack { PathPrefix = "Assets/_ThirdParty/KayKit/Dungeon/RAW/",
                License = "CC0", Source = "KayKit Dungeon Remastered 1.0", Note = "던전 소품 전반" },
        };

        public static bool IsRegistered(string assetPath)
        {
            for (int i = 0; i < Registered.Length; i++)
                if (string.Equals(Registered[i].Fbx, assetPath, StringComparison.Ordinal))
                    return true;
            for (int i = 0; i < RegisteredPacks.Length; i++)
                if (assetPath.StartsWith(RegisteredPacks[i].PathPrefix, StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>던전 방 안에서 쓸 수 있는 것은 **파일 단위 등록분만**이다(가구 창고 반려 이후 규칙).</summary>
        public static bool IsRegisteredForRoom(string assetPath)
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
