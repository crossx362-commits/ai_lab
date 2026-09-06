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
        public static void Hide(Vector3 eye, Vector3 look, float radius, List<Renderer> hidden)
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
                rend.enabled = false;
                hidden.Add(rend);
            }
        }

        public static void Restore(List<Renderer> hidden)
        {
            for (int i = 0; i < hidden.Count; i++)
            {
                if (hidden[i] != null)
                    hidden[i].enabled = true;
            }
            hidden.Clear();
        }
    }
}
