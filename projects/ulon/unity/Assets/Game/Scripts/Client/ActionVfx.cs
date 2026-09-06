using UnityEngine;

namespace Ulon.Client
{
    /// <summary>
    /// §11.2·§18.15 VFX — 행동의 **결과가 화면에 보이게** 한다(구멍 표 ③: 마법·타격이 숫자로만 났다).
    ///
    /// 템플릿은 씬 안에 있다(`ActionVfx/Vfx*`). 런타임이 에셋을 찾아 헤매지 않게 하려는 것이고,
    /// 빌드에도 씬과 함께 따라간다. 템플릿을 만드는 쪽은 `VisualSliceBuilder.EnsureActionVfx()`다.
    /// 종류마다 **색과 형태가 달라야** 한다 — 같은 반짝임을 세 번 쓰면 화면에서 구분이 안 된다(검수 요구).
    /// </summary>
    public static class ActionVfx
    {
        public const string RootObject = "ActionVfx";

        public enum Kind { Hit, Heal, Craft }

        public static string ObjectFor(Kind kind)
        {
            switch (kind)
            {
                case Kind.Heal: return "VfxHeal";
                case Kind.Craft: return "VfxCraft";
                default: return "VfxHit";
            }
        }

        /// <summary>해당 효과를 그 자리에서 한 번 재생한다. 템플릿이 없으면 조용히 넘어간다(전투를 막지 않는다).</summary>
        /// <summary>
        /// **이 클라이언트에 실제로 도착해 재생된 횟수**(2클라 실측용, 검수 지시 2026-09-07).
        /// 소스 게이트는 「부르도록 적혀 있다」까지만 본다 — 옆 사람 화면에 닿았다는 증거는 실행에서 나와야 한다.
        /// </summary>
        public static int Played;
        public static Kind LastKind;

        public static void Play(Kind kind, Vector3 position)
        {
            var template = Template(kind);
            if (template == null)
                return;
            var go = Object.Instantiate(template.gameObject, position, template.transform.rotation);
            go.name = template.name + "Burst";
            go.SetActive(true);
            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                Object.Destroy(go);
                return;
            }
            ps.Play(true);
            Played++;
            LastKind = kind;
            Object.Destroy(go, ps.main.duration + ps.main.startLifetime.constantMax + 0.2f);
        }

        /// <summary>씬의 템플릿(꺼진 상태). 편집기 게이트도 같은 경로로 찾는다.</summary>
        public static ParticleSystem Template(Kind kind)
        {
            var root = GameObject.Find(RootObject);
            if (root == null)
                return null;
            var child = root.transform.Find(ObjectFor(kind));
            return child != null ? child.GetComponent<ParticleSystem>() : null;
        }
    }
}
