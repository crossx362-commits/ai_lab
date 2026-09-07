using System;
using System.Collections.Generic;
using Ulon.Server;
using Ulon.Shared;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **살아 있는 것은 전부** 모델 자격을 받는다(검수 랩 C 후속, 2026-09-07).
    /// 랩 C에서 사람 자격을 17체로 넓혔는데도 **조련 대상이 목록 밖**이었다 — 사각지대 표에도 없던 구멍이다.
    /// 그래서 이 게이트는 이름 목록이 아니라 **씬의 `WorldBody` 전수**를 돈다. 새 생물이 생기면
    /// 아무도 목록에 적지 않아도 자동으로 판정 대상이 된다(목록에 의존하면 같은 구멍이 또 생긴다).
    ///
    /// 판정: 보이는 메시가 ① 사람 모델 원장(`MobArt`)이거나 ② 짐승 모델 원장(`CreatureArt`)이어야 한다.
    /// **소품 메시(덤불·바위 등)는 생물이 될 수 없다** — 화면에서 짐승으로 안 읽힌다(§8.1·§8.2).
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// 짐승 모델 원장 — 오너 승인(2026-09-07)으로 받은 CC0 모델만 적는다.
        /// 출처·라이선스·받은 날은 각 폴더의 SOURCE_URL.txt/LICENSE.txt와 `docs/ASSET_REGISTER.md` §11에 있다.
        /// </summary>
        static readonly string[] CreatureArtRegistered =
        {
            "Assets/_ThirdParty/OpenGameArt/Deer/RAW/Deer.obj",     // 야생하트
            "Assets/_ThirdParty/OpenGameArt/Boar/RAW/Boar.fbx",     // 멧돼지
        };

        /// <summary>
        /// **알려진 결함**과 그 사유. 여기 적힌 것은 실패 대신 경고로 남긴다 —
        /// 「게이트를 통과시키려 게이트를 고친다」와 다른 점은, 결함을 **숨기지 않고 매 실행 경고로 드러내며**
        /// 사유와 해제 조건을 코드에 박아 둔다는 것이다. 모델이 들어오면 이 줄을 지우는 것이 수리 완료다.
        /// </summary>
        static readonly Dictionary<string, string> CreatureArtKnownDefect = new Dictionary<string, string>
        {
            // 비어 있는 것이 정상이다. 2026-09-07 사슴·멧돼지 모델 도입으로 두 줄을 **지웠다**.
            // 새 줄을 넣는 것은 검수 승인 사항이다(결함이 목록 뒤에 숨지 않게).
        };

        static bool creatureRosterLogged;

        static void AssertCreatureArtQualified()
        {
            var bodies = UnityEngine.Object.FindObjectsByType<WorldBody>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (bodies.Length == 0)
                throw new InvalidOperationException("씬에 생물이 하나도 없습니다 — 잰 것이 없습니다(0이면 실패).");

            int ok = 0;
            var bad = new List<string>();
            var known = new List<string>();
            var roster = new List<string>();   // 무엇이 무슨 메시인지 — 판정 근거를 로그에 남긴다
            for (int i = 0; i < bodies.Length; i++)
            {
                var go = bodies[i].gameObject;
                if (MobArt.ModelOf(go, out MobArt.Model model, out string _))
                {
                    roster.Add(go.name + " = 사람 원장 " + model.Prefix);
                    if (!model.BodyReadsClothed)
                        bad.Add(go.name + ": 맨몸 모델 " + model.Prefix);
                    else
                        ok++;
                    continue;
                }
                string mesh = VisibleMeshAsset(go);
                roster.Add(go.name + " = " + (mesh == "" ? "(메시 없음)" : mesh));
                if (IsRegisteredCreatureMesh(mesh))
                {
                    ok++;
                    continue;
                }
                if (CreatureArtKnownDefect.TryGetValue(go.name, out string why))
                {
                    known.Add(go.name + " — " + why);
                    continue;
                }
                bad.Add(go.name + ": 사람·짐승 원장 어디에도 없는 메시 " + (mesh == "" ? "(메시 없음)" : mesh));
            }

            if (!creatureRosterLogged)
            {
                creatureRosterLogged = true;   // 네거티브 컨트롤 재실행 때 두 번 찍지 않는다
                for (int i = 0; i < roster.Count; i++)
                    Debug.Log("[Ulon] 생물 메시 — " + roster[i]);
            }
            for (int i = 0; i < known.Count; i++)
                Debug.LogWarning("[Ulon] 생물 모델 **알려진 결함** — " + known[i]);
            if (bad.Count > 0)
                throw new InvalidOperationException("생물 모델 자격 미달 " + bad.Count + "체: " + string.Join(", ", bad) +
                    " — 살아 있는 것은 사람 원장(MobArt)이나 짐승 원장(CreatureArt)에 등록된 모델만 쓸 수 있습니다. " +
                    "소품 메시(덤불·바위)는 화면에서 생물로 안 읽힙니다(§8.1·§8.2).");

            Debug.Log("[Ulon] 생물 모델 자격 — 씬 WorldBody " + bodies.Length + "체 중 " + ok +
                      "체 통과, 알려진 결함 " + known.Count + "체(사유는 위 경고, 모델 도입 시 해제)");
        }

        static bool IsRegisteredCreatureMesh(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return false;
            for (int i = 0; i < CreatureArtRegistered.Length; i++)
                if (string.Equals(CreatureArtRegistered[i], assetPath, StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>화면에 **보이는** 메시의 에셋 경로(꺼 둔 것은 세지 않는다).</summary>
        static string VisibleMeshAsset(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || !rends[i].gameObject.activeInHierarchy)
                    continue;
                var mf = rends[i].GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    return UnityEditor.AssetDatabase.GetAssetPath(mf.sharedMesh);
                var skinned = rends[i] as SkinnedMeshRenderer;
                if (skinned != null && skinned.sharedMesh != null)
                    return UnityEditor.AssetDatabase.GetAssetPath(skinned.sharedMesh);
            }
            return "";
        }

        /// <summary>
        /// 네거티브 컨트롤 — **생물에 소품 메시를 물리면 빨간불**이어야 한다.
        ///
        /// 2026-09-07 교체: 예전 NC는 「알려진 결함 목록을 비우면 빨간불」이었다. 그 목록이 실제 결함
        /// (덤불로 세운 야생하트·멧돼지)을 가리고 있음을 증명하는 장치였는데, 모델이 들어와 목록이
        /// 비었으니 그 NC는 **자기 전제를 잃었다**(그리고 스스로 「지우라」고 말했다). 이제는
        /// 결함을 직접 만든다 — 짐승 하나에 덤불 메시를 끼워 넣고 게이트가 무는지 본다.
        /// </summary>
        static void AssertCreatureArtNegativeControl()
        {
            var victim = GameObject.Find(TameCritter.Object) ?? GameObject.Find(TameBoar.Object);
            if (victim == null)
                throw new InvalidOperationException("생물 자격 NC 대상(조련 생물)이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var mf = victim.GetComponentInChildren<MeshFilter>(true);
            if (mf == null)
                throw new InvalidOperationException("생물 자격 NC 대상에 메시가 없습니다.");
            const string bush = "Assets/_ThirdParty/Kenney/Nature/RAW/Models/plant_bush.fbx";
            var bushGo = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(bush);
            var bushMf = bushGo != null ? bushGo.GetComponentInChildren<MeshFilter>(true) : null;
            if (bushMf == null || bushMf.sharedMesh == null)
                throw new InvalidOperationException("생물 자격 NC용 덤불 메시를 못 찾았습니다: " + bush);
            var keep = mf.sharedMesh;
            bool red = false;
            string message = "";
            try
            {
                mf.sharedMesh = bushMf.sharedMesh;      // **결함을 실제로 만든다**
                try { AssertCreatureArtQualified(); }
                catch (InvalidOperationException e) { red = true; message = e.Message; }
            }
            finally { mf.sharedMesh = keep; }
            if (!red)
                throw new InvalidOperationException("생물 자격 네거티브 컨트롤 실패 — " + victim.name +
                    "에 덤불 메시를 물렸는데 통과했습니다.");
            Debug.Log("[Ulon] 생물 자격 네거티브 컨트롤 통과 — 짐승에 덤불 메시를 물리면 FAIL: " + message);
        }
    }
}
