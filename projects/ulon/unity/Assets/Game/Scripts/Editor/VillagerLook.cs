using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Editor
{
    /// <summary>
    /// **마을 역할 사람**을 모으고 그 외형을 재는 공용 자(검수 랩 ③사람, 2026-09-07).
    ///
    /// 대상은 **이름 목록이 아니라 규칙**으로 모은다 — `RoleLook.Facilities` 원장의 각 시설에 서 있는
    /// 사람(시설 자신이 사람이거나, 시설 밑의 `*Npc`)이다. 새 역할이 원장에 들어오면 아무도 목록을
    /// 고치지 않아도 판정 대상이 된다(목록에 기대면 조련 대상이 빠졌던 그 구멍이 또 난다).
    ///
    /// 재는 자를 여기 한 곳에 두는 이유: 감사 도구·빌더·게이트가 **같은 값**을 봐야 한다.
    /// 같은 로직이 세 곳에 복사되면 세 곳이 서로 다른 사실을 말하기 시작한다(원장 교훈).
    /// </summary>
    public static class VillagerLook
    {
        /// <summary>
        /// 마을 시설에 서 있는 사람 전수(원장 순서).
        ///
        /// **시설이 아니라 사람을 집는다** — 첫 판(감사 실측)에서는 시설 오브젝트를 그대로 담았더니
        /// 마구간의 「몸 색」이 마당 짐승의 가죽 색으로 읽혔다. 시설 밑의 `*Npc`가 있으면 그것이 사람이고,
        /// 없을 때만 시설 자신이 사람이다(훈련사).
        /// </summary>
        public static List<GameObject> Villagers()
        {
            var found = new List<GameObject>();
            var facilities = RoleLook.Facilities;
            for (int i = 0; i < facilities.Length; i++)
            {
                var host = GameObject.Find(facilities[i].Object);
                if (host == null)
                    continue;
                GameObject npc = null;
                foreach (var t in host.GetComponentsInChildren<Transform>(true))
                {
                    if (t == host.transform || !t.name.EndsWith("Npc", System.StringComparison.Ordinal))
                        continue;
                    if (MobArt.ModelOf(t.gameObject, out _, out _))
                        npc = t.gameObject;
                }
                if (npc != null)
                    found.Add(npc);
                else if (MobArt.ModelOf(host, out _, out _))
                    found.Add(host);                       // 시설 자리 자체가 사람이다(훈련사)
            }
            return found;
        }

        /// <summary>이 사람이 서 있는 시설 이름 — `HealerNpc`→`Healer`, 훈련사는 자기 이름.</summary>
        public static string HostOf(GameObject person)
        {
            string n = person.name;
            if (n.EndsWith("Npc", System.StringComparison.Ordinal))
                return n.Substring(0, n.Length - 3);
            return n;
        }

        /// <summary>
        /// **몸으로 읽히는 색** — 장비를 뺀 렌더러 중 화면에서 가장 큰 것의 재질 색.
        /// 장비를 넣으면 칼날 색이 「그 사람의 색」이 된다(발 높이가 칼끝이 됐던 것과 같은 종류의 오염).
        /// </summary>
        public static Color BodyColor(GameObject go, out string via)
        {
            via = "(없음)";
            var rends = go.GetComponentsInChildren<Renderer>(true);
            float biggest = -1f;
            Color color = Color.white;
            for (int i = 0; i < rends.Length; i++)
            {
                if (!rends[i].enabled || !rends[i].gameObject.activeInHierarchy)
                    continue;
                if (VisualSliceBuilder.IsGearName(rends[i].gameObject.name))
                    continue;
                var b = rends[i].bounds;
                float vol = b.size.x * b.size.y * b.size.z;
                if (vol <= biggest)
                    continue;
                biggest = vol;
                var mat = rends[i].sharedMaterial;
                if (mat != null && mat.HasProperty("_Color"))
                {
                    color = mat.color;
                    via = rends[i].gameObject.name + "/" + mat.name;
                }
            }
            return color;
        }

        /// <summary>화면에 **보이는** 장비 노드 이름(정렬) — 「무엇을 들었나」 축.</summary>
        public static List<string> VisibleGear(GameObject go)
        {
            var gear = new List<string>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (!VisualSliceBuilder.IsGearName(t.name))
                    continue;
                var r = t.GetComponent<Renderer>();
                if (r != null && r.enabled && t.gameObject.activeInHierarchy)
                    gear.Add(t.name);
            }
            gear.Sort(System.StringComparer.Ordinal);
            return gear;
        }
    }
}
