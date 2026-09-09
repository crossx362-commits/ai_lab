using System.Collections.Generic;
using UnityEngine;

namespace Ulon.Client
{
    /// <summary>
    /// 기획서 §4.2 「고정 3/4 쿼터뷰」를 지키면서 실내가 보이게 한다(검수 2026-09-06 P0).
    /// 카메라 피치·요는 건드리지 않고, **카메라와 플레이어 사이에 낀 던전 벽·천장만 렌더를 끈다.**
    /// 콜라이더는 그대로라 하늘 차단·이동 제한은 유지된다.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class DungeonSightFade : MonoBehaviour
    {
        public const string BlockerLayer = "DungeonBlocker";

        [SerializeField] Transform target;
        /// <summary>시야 창을 얼마나 넓게 열지. 좁으면 뚜껑 한 장만 걷혀 방이 조금밖에 안 보인다(실측).</summary>
        public const float DefaultRadius = 2.2f;

        [SerializeField] float radius = DefaultRadius;

        readonly List<Renderer> hidden = new List<Renderer>();

        public void SetTarget(Transform t) => target = t;

        void LateUpdate()
        {
            Restore(hidden);
            if (target == null)
                return;
            Hide(transform.position, target.position + Vector3.up * 1.0f, radius, hidden);
        }

        /// <summary>
        /// 카메라와 대상 사이의 DungeonBlocker 렌더러를 끈다. 런타임(LateUpdate)과 QA 스크린샷 도구가
        /// **같은 함수**를 쓴다 — 검증 화면이 플레이 화면과 다르면 증거가 아니다(검수 2026-09-06 P0).
        /// </summary>
        /// <param name="except">
        /// **바라보는 대상 자신**은 비치게 하지 않는다. 게임에서는 look 지점이 플레이어(블로커 레이어가
        /// 아니다)라 해당 없고 null이지만, QA 근접 샷은 look 지점에 **피사체**를 놓는다 — 그러면 반경
        /// 2.2m 구체가 피사체를 물어 **판정할 대상이 반투명으로** 찍힌다(33_campfire의 돌·장작이 그랬다).
        /// 이건 페이드 사본이 남은 게 아니라 **자기 자신을 가림으로 센 것**이다.
        /// </param>
        public static void Hide(Vector3 eye, Vector3 look, float radius, List<Renderer> hidden, Transform except = null)
        {
            int layer = LayerMask.NameToLayer(BlockerLayer);
            if (layer < 0)
                return;
            Vector3 dir = look - eye;
            float dist = dir.magnitude;
            if (dist < 0.01f)
                return;
            dir /= dist;
            var found = Physics.SphereCastAll(eye, radius, dir, dist, 1 << layer, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < found.Length; i++)
            {
                var rend = found[i].collider != null ? found[i].collider.GetComponent<Renderer>() : null;
                if (rend == null || !rend.enabled)
                    continue;
                // 발밑(바닥 판)은 내려다보는 카메라를 가리지 않는다. 창 반경이 커지면 스피어캐스트가
                // 바닥까지 물어 방 바닥이 사라지고 하늘이 비쳤다(2026-09-06 플레이캠 실측).
                if (rend.bounds.max.y < look.y - 0.2f)
                    continue;
                if (except != null && rend.transform.IsChildOf(except))
                    continue;
                // **대상 뒤에 있는 것은 대상을 가릴 수 없다**(검수 판정 2026-09-09).
                // 구를 쓸어 보내므로 대상 **옆·뒤**를 스친 것까지 「사이에 낀 것」으로 잡혔다:
                // 은행 근접 샷에서 은행원 **뒤 3.4m**의 은행이 통째로 비쳐 화면 절반이 유령이 됐다.
                // 가릴 수 없는 것을 가린 셈 치고 지우는 것이라 상식 모순이다 — 거리 하나로 가른다.
                if (!IgnoreBehindRuleForNc && BehindTarget(rend, eye, look))
                    continue;
                Ghost(rend);
                hidden.Add(rend);
            }
        }

        /// <summary>
        /// 눈에서 이 물건의 **가장 가까운 점**까지가 대상보다 멀면 그것은 대상 뒤다 — 가릴 수 없다.
        /// 재는 쪽(QA 근접 프레이밍)과 찍는 쪽이 **이 한 함수**를 같이 부른다: 같은 판정이 두 곳에
        /// 따로 살면 한쪽만 고쳐져 화면과 숫자가 갈린다(이 저장소가 여러 번 밟은 함정).
        /// </summary>
        public static bool BehindTarget(Renderer rend, Vector3 eye, Vector3 look)
        {
            float toLook = Vector3.Distance(eye, look);
            return Vector3.Distance(eye, rend.bounds.ClosestPoint(eye)) > toLook + 0.1f;
        }

        /// <summary>
        /// **네거티브 컨트롤 전용 스위치** — 켜면 「대상 뒤는 안 비친다」 규칙이 없던 때로 돌아간다.
        /// 규칙을 넣고 나면 방위 고르는 쪽이 그런 자리를 아예 안 골라서, 씬을 흔들어도 결함이
        /// 안 만들어진다. 그래서 **규칙 자체를 끄고** 「그때는 은행이 다시 비치는가」를 본다.
        /// `ULON_FADE_NC=1`로 QA 샷을 돌리면 켜진다.
        /// </summary>
        public static bool IgnoreBehindRuleForNc;

        /// <summary>
        /// **가리지 말고 비치게 한다**(검수 판정 2026-09-07). 렌더러를 끄면 건물 한 채가 통째로 증발해
        /// 마을이 빈 흙바닥으로 보였다. 알파만 낮춰 **실루엣과 그림자는 남긴다** — 0으로 내리면
        /// 끈 것과 같으므로 하한을 두고 게이트가 강제한다.
        /// </summary>
        public const float GhostAlpha = 0.35f;
        /// <summary>이 아래로 내려가면 「비침」이 아니라 「사라짐」이다 — 게이트가 이 선을 지킨다.</summary>
        public const float GhostAlphaMin = 0.20f;

        static readonly Dictionary<Material, Material> ghostCache = new Dictionary<Material, Material>();
        static readonly Dictionary<Renderer, Material[]> ghosted = new Dictionary<Renderer, Material[]>();

        /// <summary>원본 머티리얼을 **파괴하지 않는다** — 반투명 사본을 만들어 갈아 끼우고 원본은 보관한다.</summary>
        public static Material MakeGhost(Material src, float alpha)
        {
            var m = new Material(src);
            m.name = src.name + " (Ghost)";
            // Built-in Standard의 Fade 모드 — 알파 블렌딩, 그림자·실루엣 유지.
            m.SetFloat("_Mode", 2f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
            Color c = m.HasProperty("_Color") ? m.color : Color.white;
            c.a = alpha;
            m.color = c;
            return m;
        }

        static void Ghost(Renderer rend)
        {
            if (ghosted.ContainsKey(rend))
                return;
            var originals = rend.sharedMaterials;
            var faded = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                if (originals[i] == null)
                    continue;
                if (!ghostCache.TryGetValue(originals[i], out Material g) || g == null)
                {
                    g = MakeGhost(originals[i], GhostAlpha);
                    ghostCache[originals[i]] = g;
                }
                faded[i] = g;
            }
            ghosted[rend] = originals;
            rend.sharedMaterials = faded;
        }

        /// <summary>이 렌더러가 지금 「비치는 중」인가 — 판정 게이트가 「걷혔다」를 이 뜻으로 읽는다.</summary>
        public static bool IsGhosted(Renderer rend)
        {
            return rend != null && ghosted.ContainsKey(rend);
        }

        public static void Restore(List<Renderer> hidden)
        {
            for (int i = 0; i < hidden.Count; i++)
            {
                var rend = hidden[i];
                if (rend == null)
                    continue;
                if (ghosted.TryGetValue(rend, out Material[] originals))
                {
                    rend.sharedMaterials = originals;      // 원상복구 — 게이트가 확인한다
                    ghosted.Remove(rend);
                }
                rend.enabled = true;                        // 옛 방식(렌더 끄기)으로 남은 것도 되살린다
            }
            hidden.Clear();
        }
    }
}
