// 격파 잔해 — 포탑이 튀어 올라 돌며 떨어진다(재미요소, 오너 지시 2026-09-17).
//
// ⚠️ 물리 엔진을 안 쓴다(§7-5: 지형에 콜라이더가 없다). 중력만 직접 적분하고 지면 아래로 내려가면 멈춘다.
// ⚠️ 연출 전용 — 판정·탄도에 아무 영향 없다. 수명이 끝나면 스스로 사라진다.
using UnityEngine;

namespace Tankfall.View
{
    public sealed class WreckPiece : MonoBehaviour
    {
        Vector3 _vel, _angVel; float _life; float _floorY;

        public void Launch(Vector3 velocity, Vector3 angularVelocityDeg, float lifeSec)
        {
            _vel = velocity; _angVel = angularVelocityDeg; _life = lifeSec;
            _floorY = transform.position.y - 1.5f;          // 대략 원래 지면. 정확한 접지는 필요 없다(잔해는 파묻혀도 된다)
            foreach (var c in GetComponentsInChildren<Collider>()) Destroy(c);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _life -= dt;
            if (_life <= 0f) { Destroy(gameObject); return; }
            if (transform.position.y > _floorY)
            {
                _vel += Vector3.down * 30f * dt;                  // Ballistics.Gravity 와 같은 30 — 세계의 무게가 같아야 한다
                transform.position += _vel * dt;
                transform.Rotate(_angVel * dt, Space.Self);
                if (transform.position.y <= _floorY) { _angVel = Vector3.zero; }
            }
        }
    }
}
