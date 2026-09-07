using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **화면에서 완전히 같은 사람형이 있는가**(검수 지시 2026-09-07 2, 랩 ③).
    ///
    /// 모델이 겹치는 것 자체는 결함이 아니다 — 색이나 든 것이 다르면 화면에서는 갈린다.
    /// 결함은 **모델·몸 색·든 것이 셋 다 같은 쌍**이다. 그런데 마을판↔던전판처럼 **일부러 같게 둔 쌍**이
    /// 있다(같은 종류의 몹이니 같은 모습인 게 맞다).
    ///
    /// 그 예외를 **침묵으로 두지 않는다**(검수): 아무 데도 안 적혀 있으면 다음 사람이 결함으로 보고 또 판다.
    /// 그래서 「같은 종류로 선언된 쌍」을 아래 원장에 적고, **선언된 쌍만** 통과시킨다.
    /// 선언 없는 새 쌍이 생기면 빨간불이다(`CreatureArtKnownDefect`와 같은 원칙 — 예외는 적힌 선언이다).
    /// </summary>
    public static partial class SliceSelfCheck
    {
        /// <summary>
        /// **같은 종류로 선언된 쌍** — 마을 사냥터판과 던전판은 같은 몹이라 같은 모습이 맞다.
        /// 새 줄을 넣는 것은 검수 승인 사항이다(결함이 목록 뒤에 숨지 않게).
        /// </summary>
        static readonly (string A, string B, string Why)[] SameKindPairs =
        {
            ("Skeleton", "DungeonSkeleton", "같은 스켈레톤 — 마을 사냥터판과 던전판"),
            ("Bandit", "DungeonBandit", "같은 도적 — 마을 사냥터판과 던전판"),
            ("Raider", "DungeonRaider", "같은 약탈자 — 마을 사냥터판과 던전판"),
        };

        static bool DeclaredSameKind(string a, string b)
        {
            for (int i = 0; i < SameKindPairs.Length; i++)
                if ((SameKindPairs[i].A == a && SameKindPairs[i].B == b) ||
                    (SameKindPairs[i].A == b && SameKindPairs[i].B == a))
                    return true;
            return false;
        }

        /// <summary>화면에서 읽히는 서명 — 모델 · 든 것 · 몸 색(0.05 단위로 양자화).</summary>
        static string LookSignature(GameObject go)
        {
            string model = MobArt.ModelOf(go, out MobArt.Model m, out _) ? m.Prefix : "(원장 밖)";
            var gear = VillagerLook.VisibleGear(go);
            var c = VillagerLook.BodyColor(go, out _);
            return model + "|" + string.Join("+", gear) + "|" +
                   Mathf.RoundToInt(c.r * 20f) + "," + Mathf.RoundToInt(c.g * 20f) + "," + Mathf.RoundToInt(c.b * 20f);
        }

        static void AssertNoUndeclaredLookTwins()
        {
            var actors = UnityEngine.Object.FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (actors.Length == 0)
                throw new InvalidOperationException("사람형이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var bySig = new Dictionary<string, List<string>>();
            for (int i = 0; i < actors.Length; i++)
            {
                string sig = LookSignature(actors[i].gameObject);
                if (!bySig.TryGetValue(sig, out var list))
                    bySig[sig] = list = new List<string>();
                list.Add(actors[i].name);
            }
            var undeclared = new List<string>();
            int declared = 0;
            foreach (var kv in bySig)
            {
                var who = kv.Value;
                for (int i = 0; i < who.Count; i++)
                    for (int j = i + 1; j < who.Count; j++)
                    {
                        if (DeclaredSameKind(who[i], who[j])) { declared++; continue; }
                        undeclared.Add(who[i] + "↔" + who[j] + "(" + kv.Key + ")");
                    }
            }
            if (undeclared.Count > 0)
                throw new InvalidOperationException("선언 없이 화면에서 완전히 같은 쌍 " + undeclared.Count + "건: " +
                    string.Join(", ", undeclared) + " — 같은 종류라 일부러 같게 둔 것이면 " +
                    "`SliceSelfCheck.SameKindPairs` 원장에 사유와 함께 적어라(예외는 침묵이 아니라 선언이다). " +
                    "아니라면 색이나 든 것으로 갈라라(§8.1).");
            Debug.Log("[Ulon] 화면 겹침 — 사람형 " + actors.Length + "체 전수, 선언된 같은 종류 " + declared +
                      "쌍만 같고 그 밖의 완전 동일 쌍 0건");
        }

        /// <summary>NC — **선언 없는 쌍을 실제로 만들면** 빨간불이어야 한다.</summary>
        static void AssertNoUndeclaredLookTwinsNegativeControl()
        {
            var donor = GameObject.Find("Raider");
            var victim = GameObject.Find("Knight");          // 둘 다 Knight 모델이지만 선언된 쌍이 아니다
            if (donor == null || victim == null)
                throw new InvalidOperationException("겹침 NC 대상(Raider·Knight)이 없습니다 — 잰 것이 없습니다(0이면 실패).");
            var keepMat = new Dictionary<Renderer, Material>();
            var gearWas = new List<(GameObject Go, bool On)>();
            var want = new HashSet<string>(VillagerLook.VisibleGear(donor));
            var donorMat = FirstBodyMaterial(donor);
            bool red = false;
            string message = "";
            try
            {
                foreach (var r in victim.GetComponentsInChildren<Renderer>(true))
                {
                    if (VisualSliceBuilder.IsGearName(r.gameObject.name))
                    {
                        gearWas.Add((r.gameObject, r.gameObject.activeSelf));
                        r.gameObject.SetActive(want.Contains(r.gameObject.name));
                        continue;
                    }
                    keepMat[r] = r.sharedMaterial;
                    r.sharedMaterial = donorMat;
                }
                if (LookSignature(victim) != LookSignature(donor))
                    throw new InvalidOperationException("겹침 NC가 결함을 못 만들었습니다 — 서명이 " +
                        LookSignature(victim) + " vs " + LookSignature(donor));
                try { AssertNoUndeclaredLookTwins(); }
                catch (InvalidOperationException e) { red = true; message = e.Message; }
            }
            finally
            {
                foreach (var kv in keepMat)
                    kv.Key.sharedMaterial = kv.Value;
                for (int i = 0; i < gearWas.Count; i++)
                    gearWas[i].Go.SetActive(gearWas[i].On);
            }
            if (!red)
                throw new InvalidOperationException("겹침 네거티브 컨트롤 실패 — Knight를 Raider와 같은 모습으로 만들었는데 통과했습니다.");
            Debug.Log("[Ulon] 화면 겹침 네거티브 컨트롤 통과 — 선언 없는 쌍을 만들면 FAIL: " + message);
        }
    }
}
