using UnityEngine;
using Ulon.Shared;

namespace Ulon.Client
{
    /// <summary>
    /// §11.2·§18.15 VFX — 행동의 **결과가 화면에 보이게** 한다(구멍 표 ③: 마법·타격이 숫자로만 났다).
    ///
    /// 템플릿은 씬 안에 있다(`ActionVfx/Vfx*`). 런타임이 에셋을 찾아 헤매지 않게 하려는 것이고,
    /// 빌드에도 씬과 함께 따라간다. 셀프체크 빌더가 씬에 안 남기면 **플레이에서 조용히 사라진다** —
    /// 그래서 <see cref="Ensure"/>가 없으면 같은 자리의 파티클을 런타임에 다시 세운다.
    /// 종류마다 **색과 형태가 달라야** 한다 — 같은 반짝임을 세 번 쓰면 화면에서 구분이 안 된다(검수 요구).
    /// </summary>
    public static class ActionVfx
    {
        public const string RootObject = "ActionVfx";

        public enum Kind { Hit, Heal, Craft }

        public struct Spec
        {
            public Kind Kind;
            public string Texture;
            public Color Tint;
            public float Size;
            public int Count;
            public float Speed;
            public float Gravity;
            public float Spread;
            public float Alpha;
        }

        /// <summary>플레이 거리(야외 ~12m 쿼터뷰)에서 읽히는 크기. 빌더와 런타임이 같은 값을 쓴다.</summary>
        public static readonly Spec[] Specs =
        {
            new Spec { Kind = Kind.Hit,   Texture = "spark_01.png", Tint = new Color(1f, 0.42f, 0.16f), Size = 1.00f, Count = 44, Speed = 5.5f, Gravity = 0.9f, Spread = 0.25f, Alpha = 1.00f },
            new Spec { Kind = Kind.Heal,  Texture = "circle_05.png", Tint = new Color(0.36f, 1f, 0.55f), Size = 0.62f, Count = 64, Speed = 1.4f, Gravity = -0.35f, Spread = 0.95f, Alpha = 0.45f },
            new Spec { Kind = Kind.Craft, Texture = "star_01.png",  Tint = new Color(0.60f, 0.62f, 1f), Size = 1.35f, Count = 40, Speed = 2.2f, Gravity = 0.2f, Spread = 0.35f, Alpha = 0.85f },
        };

        public static string ObjectFor(Kind kind)
        {
            switch (kind)
            {
                case Kind.Heal: return "VfxHeal";
                case Kind.Craft: return "VfxCraft";
                default: return "VfxHit";
            }
        }

        public static ActionSfx.Kind SfxFor(Kind kind)
        {
            if (kind == Kind.Heal)
                return ActionSfx.Kind.Heal;
            if (kind == Kind.Craft)
                return ActionSfx.Kind.Craft;
            return ActionSfx.Kind.Hit;
        }

        public static Kind KindForSpell(SpellId spell)
        {
            switch (spell)
            {
                case SpellId.Mend:
                case SpellId.Restore:
                case SpellId.Cleanse:
                case SpellId.Bless:
                case SpellId.Ward:
                    return Kind.Heal;
                case SpellId.Blink:
                    return Kind.Craft;
                default:
                    return Kind.Hit;
            }
        }

        /// <summary>해당 효과를 그 자리에서 한 번 재생한다. 템플릿이 없으면 조용히 넘어간다(전투를 막지 않는다).</summary>
        /// <summary>
        /// **이 클라이언트에 실제로 도착해 재생된 횟수**(2클라 실측용, 검수 지시 2026-09-07).
        /// 소스 게이트는 「부르도록 적혀 있다」까지만 본다 — 옆 사람 화면에 닿았다는 증거는 실행에서 나와야 한다.
        /// </summary>
        public static int Played;
        public static Kind LastKind;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Warm()
        {
            Ensure();
        }

        /// <summary>씬에 템플릿이 없으면 같은 규격으로 세운다. 셀프체크가 씬을 안 저장해도 플레이에서 안 사라지게.</summary>
        public static void Ensure()
        {
            if (Template(Kind.Hit) != null && Template(Kind.Heal) != null && Template(Kind.Craft) != null)
                return;
            var old = GameObject.Find(RootObject);
            if (old != null)
                Object.DestroyImmediate(old);
            var root = new GameObject(RootObject);
            for (int i = 0; i < Specs.Length; i++)
                BuildChild(root.transform, Specs[i], RuntimeMaterial(Specs[i]));
        }

        public static void BuildChild(Transform parent, Spec spec, Material mat)
        {
            var go = new GameObject(ObjectFor(spec.Kind));
            go.transform.SetParent(parent, false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = 0.55f;
            main.startSpeed = spec.Speed;
            main.startSize = spec.Size;
            main.startColor = new Color(spec.Tint.r, spec.Tint.g, spec.Tint.b, spec.Alpha);
            main.gravityModifier = spec.Gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 128;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)spec.Count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = spec.Spread > 0f ? spec.Spread : 0.25f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            go.SetActive(false);
        }

        static Material RuntimeMaterial(Spec spec)
        {
#if UNITY_EDITOR
            var baked = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/VFX/Vfx" + spec.Kind + ".mat");
            if (baked != null)
                return baked;
#endif
            var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader != null ? shader : Shader.Find("Hidden/InternalErrorShader"));
            mat.color = spec.Tint;
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", spec.Tint);
            return mat;
        }

        public static void Play(Kind kind, Vector3 position)
        {
            Ensure();
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
