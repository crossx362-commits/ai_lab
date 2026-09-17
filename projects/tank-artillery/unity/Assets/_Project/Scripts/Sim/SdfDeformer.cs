// 명세: docs/GAME_SPEC_TANK_ARTILLERY.md §7
//
// 폭발로 지형을 깎는다. Heightfield 때와 달리 **진짜 구형**으로 파인다 —
// 언덕 옆면을 때리면 위쪽 흙이 남아 오버행이 되고, 발밑을 파면 천장이 생긴다(기획서 §21·§28).

using System;

namespace Tankfall.Sim
{
    public struct BlastRequest
    {
        public float X, Y, Z;     // 착탄 월드 좌표
        public float Radius;      // Rc = weapon.ExplosionRadius * weapon.TerrainDamage

        /// <summary>
        /// 세로 배율. 1 이면 정확히 구(예전 동작 그대로), &lt;1 이면 넓고 얕게, &gt;1 이면 좁고 깊게 판다.
        ///
        /// 왜 필요한가: 원작은 탄마다 파이는 모양이 달랐다 — 캐논 1번탄 검콩은 "가장 넓고 약하다"(폭발 11.4m),
        /// 이온어태커 위성탄은 수직으로 내리꽂혀 "정타만 해도 지형 완전 파괴"다(§2-9 원작 조사).
        /// 전부 같은 구로 파면 그 차이가 화면에서 사라진다.
        ///
        /// ⚠️ 1 에서 너무 멀어지면 `SdfRaymarch` 의 스텝이 과대 추정돼 표면을 지나칠 수 있다(여기 값은 근사 거리다).
        ///    그래서 `CraterShape` 가 0.5~1.8 로 묶는다. 이 범위를 넓히려면 레이마칭부터 다시 재라.
        /// </summary>
        public float VScale;

        public BlastRequest(float x, float y, float z, float radius, float vscale = 1f)
        { X = x; Y = y; Z = z; Radius = radius; VScale = vscale <= 0f ? 1f : vscale; }
    }

    /// <summary>
    /// 탄종별 굴착 모양(세로 배율). 게임과 하네스가 **같은 표**를 봐야 지형 차이가 승률에 똑같이 반영된다
    /// (§2-9-1 의 교훈: 한쪽만 쓰는 규칙은 하네스 수치를 게임의 것이 아니게 만든다).
    ///
    /// 근거는 전부 원작 조사(§2-9)다. 근거가 없는 기종은 1.0(구)으로 둔다 — 여기서 발명하지 않는다.
    /// </summary>
    public static class CraterShape
    {
        public const float Min = 0.5f, Max = 1.8f;

        public static float VScaleOf(TankKind kind, ShellKind shell)
        {
            float v = 1.0f;
            switch (kind)
            {
                // 캐논 1번탄 검콩 — 원작 "가장 넓고(폭발 11.4m) 약하다(140)". 넓게 퍼지고 얕게 판다.
                case TankKind.Cannon: v = shell == ShellKind.Normal ? 0.58f : 1.25f; break;

                // 이온어태커 2번탄 위성탄 — 하늘에서 수직으로 꽂힌다. 좁고 깊게.
                case TankKind.IonAttacker: v = shell == ShellKind.Special ? 1.75f : 1.0f; break;

                // 레이저탱크 — 빔이라 지면을 꿰뚫는 느낌. 2번탄은 3연 회전 레이저.
                case TankKind.Laser: v = shell == ShellKind.Special ? 1.45f : 1.20f; break;

                // 마인랜더 2번탄 지뢰 — 설치물이라 지형을 크게 헤집지 않는다.
                case TankKind.MineLander: v = shell == ShellKind.Special ? 0.70f : 1.0f; break;

                // 캐터펄트 — 투석기. 위에서 떨어뜨려 얕고 넓게.
                case TankKind.Catapult: v = 0.75f; break;

                // 미사일/멀티미사일 — 탄두가 박혀 터진다. 약간 깊게.
                case TankKind.Missile:
                case TankKind.MultiMissile: v = 1.20f; break;
            }
            return v < Min ? Min : v > Max ? Max : v;
        }
    }

    /// <summary>폭발이 건드린 격자 범위. 이 범위와 겹치는 청크만 메시를 다시 만든다.</summary>
    public struct DirtyBounds
    {
        public int I0, I1, J0, J1, K0, K1;
        public bool Empty;
    }

    public static class SdfDeformer
    {
        /// <summary>
        /// 구를 빼낸다(CSG subtract): sdf' = max(sdf, R − |p−c|).
        /// 구 안쪽은 R−|p−c| > 0 이 되어 공기로 바뀐다.
        /// </summary>
        public static DirtyBounds SubtractSphere(SdfVolume vol, in BlastRequest req)
        {
            float r = req.Radius, vx = vol.Voxel;
            // SDF가 표면 근처에서만 의미가 있으면 되므로, 구 반경 + 여유 2복셀만 훑는다.
            int i0 = vol.GridX(req.X - r) - 2, i1 = vol.GridX(req.X + r) + 3;
            float ry = r * req.VScale;                                    // 세로로 늘어난 만큼 더 훑는다(안 그러면 바닥이 잘린다)
            int j0 = vol.GridY(req.Y - ry) - 2, j1 = vol.GridY(req.Y + ry) + 3;
            int k0 = vol.GridZ(req.Z - r) - 2, k1 = vol.GridZ(req.Z + r) + 3;

            var b = new DirtyBounds { I0 = int.MaxValue, J0 = int.MaxValue, K0 = int.MaxValue,
                                      I1 = int.MinValue, J1 = int.MinValue, K1 = int.MinValue, Empty = true };

            for (int j = j0; j < j1; j++)
                for (int k = k0; k < k1; k++)
                    for (int i = i0; i < i1; i++)
                    {
                        float dx = vol.WorldX(i) - req.X;
                        float dy = (vol.WorldY(j) - req.Y) / req.VScale;   // VScale=1 이면 예전과 **완전히 같은 식**이다
                        float dz = vol.WorldZ(k) - req.Z;
                        float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                        float carved = r - dist;            // 구 안쪽에서 양수
                        if (carved <= 0f) continue;         // 구 밖 — 건드릴 필요 없음

                        float old = vol.Sample(i, j, k);
                        if (carved <= old) continue;        // 이미 공기이거나 더 비어 있음

                        vol.Set(i, j, k, carved);
                        if (i < b.I0) b.I0 = i; if (i + 1 > b.I1) b.I1 = i + 1;
                        if (j < b.J0) b.J0 = j; if (j + 1 > b.J1) b.J1 = j + 1;
                        if (k < b.K0) b.K0 = k; if (k + 1 > b.K1) b.K1 = k + 1;
                        b.Empty = false;
                    }
            return b;
        }
    }
}
