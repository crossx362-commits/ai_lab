using UnityEngine;

/// <summary>
/// 유닛끼리 겹치지 않게 밀어낸다 (오너 지시 2026-08-14 "캐릭터 몬스터 안 겹치게").
///
/// 왜 물리 엔진이 아닌가:
///   이 게임의 유닛은 전부 좌표 갱신으로 움직인다(Rigidbody 없음). 콜라이더를 붙여도
///   아무도 안 본다. 그리고 500체에 물리를 켜면 W1에서 확보한 성능 여유가 통째로 사라진다.
///
/// 왜 O(n²)이 아닌가:
///   500체면 12만 5천 쌍이다. 1/60 고정 스텝에서 매 프레임 그걸 도는 건 예산 밖이다.
///   **균일 격자**로 자기 칸과 이웃 8칸만 본다 — 밀도가 고른 판에서 사실상 O(n).
///
/// ⚠️ 완전한 비침투를 목표로 하지 않는다. 뱀서류는 무리가 뭉치는 그림이 정상이고,
///    한 번의 부드러운 밀어내기로 **실루엣이 서로 먹히지 않을 만큼만** 띄운다.
///    완전 분리를 하려면 반복 해석이 필요하고 그건 밀집 전투에서 유닛을 떨게 만든다.
/// </summary>
public static class UnitSeparation
{
    /// <summary>유닛 반지름(월드 단위). 스프라이트 폭의 절반보다 살짝 작게 잡아 뭉침을 허용한다.
    /// 값을 키우면 실루엣은 잘 읽히지만 무리가 흩어져 뱀서류의 압박감이 줄어든다.</summary>
    public const float Radius = 0.34f;

    const float CellSize = Radius * 2f;
    const int MaxUnits = 640;

    static readonly int[] _cellStart = new int[4096];
    static readonly int[] _next = new int[MaxUnits];
    static int _gridW, _gridH;
    static float _minX, _minY;

    /// <summary>
    /// 겹침을 푼다. pos는 제자리에서 수정된다.
    /// </summary>
    /// <param name="strength">0~1. 1이면 겹친 만큼 전부, 작을수록 부드럽게.</param>
    public static void Resolve(Vector2[] pos, bool[] alive, int count, float strength = 0.5f)
    {
        if (count <= 1) return;
        count = Mathf.Min(count, MaxUnits);

        // ── 격자 구성
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        for (int i = 0; i < count; i++)
        {
            if (!alive[i]) continue;
            var p = pos[i];
            if (p.x < minX) minX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.x > maxX) maxX = p.x;
            if (p.y > maxY) maxY = p.y;
        }
        if (minX > maxX) return;                       // 살아 있는 유닛이 없다

        _minX = minX; _minY = minY;
        _gridW = Mathf.Clamp(Mathf.CeilToInt((maxX - minX) / CellSize) + 1, 1, 63);
        _gridH = Mathf.Clamp(Mathf.CeilToInt((maxY - minY) / CellSize) + 1, 1, 63);

        int cells = _gridW * _gridH;
        for (int c = 0; c < cells; c++) _cellStart[c] = -1;

        for (int i = 0; i < count; i++)
        {
            if (!alive[i]) { _next[i] = -1; continue; }
            int c = CellOf(pos[i]);
            _next[i] = _cellStart[c];
            _cellStart[c] = i;
        }

        // ── 자기 칸 + 이웃 8칸만 본다
        float min = Radius * 2f;
        float minSq = min * min;
        for (int i = 0; i < count; i++)
        {
            if (!alive[i]) continue;
            int cx = Mathf.Clamp((int)((pos[i].x - _minX) / CellSize), 0, _gridW - 1);
            int cy = Mathf.Clamp((int)((pos[i].y - _minY) / CellSize), 0, _gridH - 1);

            for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int nx = cx + ox, ny = cy + oy;
                    if (nx < 0 || ny < 0 || nx >= _gridW || ny >= _gridH) continue;

                    for (int j = _cellStart[ny * _gridW + nx]; j >= 0; j = _next[j])
                    {
                        if (j <= i) continue;              // 각 쌍을 한 번만
                        Vector2 d = pos[j] - pos[i];
                        float sq = d.sqrMagnitude;
                        if (sq >= minSq || sq < 1e-6f) continue;

                        float dist = Mathf.Sqrt(sq);
                        float push = (min - dist) * 0.5f * strength;
                        Vector2 n = d / dist;
                        pos[i] -= n * push;
                        pos[j] += n * push;
                    }
                }
        }
    }

    static int CellOf(Vector2 p)
    {
        int cx = Mathf.Clamp((int)((p.x - _minX) / CellSize), 0, _gridW - 1);
        int cy = Mathf.Clamp((int)((p.y - _minY) / CellSize), 0, _gridH - 1);
        return cy * _gridW + cx;
    }
}
