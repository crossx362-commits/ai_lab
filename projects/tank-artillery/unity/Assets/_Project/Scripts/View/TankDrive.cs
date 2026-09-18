// 주행 연출 — 탱크가 "미끄러지지" 않게 하는 것들.
//
// 2026-09-18 까지 이동은 `root.position` 대입이 전부였다. 바퀴는 차체 메시에 통째로 구워져 있어
// **구조적으로 돌 수가 없었고**(ProceduralTank 가 MeshBuilder 로 한 덩어리를 만든다), 흙먼지도
// 바퀴자국도 없었다. 엔진 소리만 나는 채로 탱크가 지면 위를 미끄러졌다.
//
// 명세 §9-4 는 궤도 UV 스크롤(`TrackUVScroller`)을 적었지만 **메시에 UV 가 아예 없다** —
// 모든 프리미티브에 UV 를 넣는 건 메시 빌더 전체를 손대는 일이라, 대신 바퀴의 **회전부(살·허브)만**
// 자식 트랜스폼으로 빼서 실제로 돌린다. 테·벨트 같은 축대칭 부분은 돌려도 안 보이므로 구운 채로 둔다.
//
// ⚠️ 이동 거리를 **호출부에서 받지 않는다.** 위치 변화를 스스로 본다 — 이동 경로가 여럿이고
//    (조작 이동 · AI 이동 · 지진 · 낙하 · 스폰) 한 곳만 배선하면 나머지가 조용히 빠지기 때문이다.
//    대신 순간이동은 걸러야 한다(지진·스폰이 한 프레임에 수십 m 를 옮긴다) — `TeleportStep` 참조.

using System.Collections.Generic;
using UnityEngine;

namespace Tankfall.View
{
    public sealed class TankDrive : MonoBehaviour
    {
        /// <summary>바퀴의 회전부. 각자 제 바퀴 중심에 놓여 있고 로컬 X 축으로 돈다.</summary>
        public readonly List<Transform> Wheels = new List<Transform>();

        /// <summary>바퀴 반지름(m). 굴러간 거리를 각도로 바꾸는 데 쓴다 — 크면 천천히 돈다.</summary>
        public float WheelRadius = 0.5f;

        /// <summary>호버는 바퀴가 없다. 흙먼지도 안 낸다(떠 있으니까) — 대신 추진 링이 있다.</summary>
        public bool Hover;

        /// <summary>한 프레임에 이만큼 넘게 움직였으면 주행이 아니라 순간이동이다(지진·스폰·낙하).</summary>
        const float TeleportStep = 3.0f;

        /// <summary>이 속도 미만은 멈춘 것으로 본다 — 지면에 눌러앉는 미세 보정에 바퀴가 떨지 않게.</summary>
        const float MoveEpsilon = 0.012f;

        Vector3 _last;
        bool _has;
        float _spin;

        /// <summary>이번 프레임에 실제로 굴러간 거리(m). 흙먼지를 낼지는 바깥이 정한다.</summary>
        public float LastStep { get; private set; }

        void OnEnable() { _has = false; LastStep = 0f; }   // 다시 켜질 때 옛 위치로 순간이동 처리되지 않게

        void LateUpdate()
        {
            var p = transform.position;
            if (!_has) { _last = p; _has = true; return; }

            var delta = p - _last;          // ⚠️ `_last` 를 덮기 **전에** 방향까지 뽑아야 한다
            _last = p;
            float step = delta.magnitude;

            // 순간이동·정지는 굴리지 않는다
            if (step >= TeleportStep || step < MoveEpsilon) { LastStep = 0f; return; }
            LastStep = step;

            if (Wheels.Count == 0 || WheelRadius <= 0.01f) return;

            // 앞으로 가면 앞으로 구른다 — 뒤로 물러나면 반대로 돈다(차체 정면과의 내적으로 본다).
            // 오르내림(y)은 빼고 본다 — 비탈을 내려갈 때 바퀴가 거꾸로 도는 것처럼 보이지 않게.
            var flat = new Vector3(delta.x, 0f, delta.z);
            float dir = Vector3.Dot(flat, transform.forward) >= 0f ? 1f : -1f;
            _spin += dir * step / WheelRadius * Mathf.Rad2Deg;
            for (int i = 0; i < Wheels.Count; i++)
                if (Wheels[i] != null) Wheels[i].localRotation = Quaternion.Euler(_spin, 0f, 0f);
        }
    }
}
