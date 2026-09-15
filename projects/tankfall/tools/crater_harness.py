#!/usr/bin/env python3
"""크레이터 프로파일 참조 구현 + 검증 하네스.

명세: docs/GAME_SPEC_TANK_ARTILLERY.md §7-3
오너 결정(2026-09-15): A(구 불리언) + C(흙 보존) + 안식각 릴랙세이션.

이 파일의 목적은 두 가지다.
  1) Sim/TerrainDeformer.cs 의 **참조 구현**. C# 포팅이 같은 수치를 내는지 대조한다.
  2) 상수를 바꿀 때(안식각·반복·보존율·링) 42° 등반 한계를 깨지 않는지 재는 자.

⚠️ 네거티브 컨트롤이 이 하네스의 핵심이다.
   --no-repose 로 돌리면 최대 경사가 72°가 나와야 한다. 그게 안 나오면 측정이 고장 난 것이다.

사용:
    python crater_harness.py            # 전체 검증
    python crater_harness.py --no-repose  # 네거티브 컨트롤 (빨간불 확인)
"""
import math
import sys

CELL = 200.0 / 256
AREA = CELL * CELL
N = 256

# --- Sim/TerrainDeformer.cs 와 동일해야 하는 상수 ---
REPOSE_ANGLE_DEG = 35.0
REPOSE_ITERATIONS = 40
DEPOSIT_RATIO = 0.8
DEPOSIT_RING_MUL = 2.0

# 축 2 + 대각 2. 축 4방향만 쓰면 대각 경사가 47.8°로 남아 42° 한계를 넘는다.
NEIGHBOURS = ((1, 0, 1.0), (0, 1, 1.0), (1, 1, 1.4142135), (1, -1, 1.4142135))

TANK_CLIMB_LIMIT_DEG = 42.0  # 명세 §2-7


def make(base=None):
    return [[(base(i, j) if base else 0.0) for i in range(N)] for j in range(N)]


def carve(h, cx, cz, rc):
    """A: 구 불리언. 반환 = 파낸 부피(m³)."""
    cy = h[int(cz / CELL)][int(cx / CELL)]
    dug = 0.0
    for j in range(int((cz - rc) / CELL) - 1, int((cz + rc) / CELL) + 2):
        for i in range(int((cx - rc) / CELL) - 1, int((cx + rc) / CELL) + 2):
            if not (0 <= i < N and 0 <= j < N):
                continue
            dh = math.hypot(i * CELL - cx, j * CELL - cz)
            if dh >= rc:
                continue
            half = math.sqrt(rc * rc - dh * dh)
            bottom, top = cy - half, cy + half
            # top 위쪽은 건드리지 않는다 — 건드리면 오버행이 되는데 Heightfield는 표현 못 한다.
            if bottom < h[j][i] <= top:
                dug += (h[j][i] - bottom) * AREA
                h[j][i] = bottom
    return dug


def deposit(h, cx, cz, rc, dug, ratio=DEPOSIT_RATIO, ring_mul=DEPOSIT_RING_MUL):
    """C: 파낸 흙을 Rc~Rc*mul 링에 cos² 가중으로 쌓는다."""
    if dug <= 0:
        return
    ro = rc * ring_mul
    ring = []
    for j in range(int((cz - ro) / CELL) - 1, int((cz + ro) / CELL) + 2):
        for i in range(int((cx - ro) / CELL) - 1, int((cx + ro) / CELL) + 2):
            if not (0 <= i < N and 0 <= j < N):
                continue
            dh = math.hypot(i * CELL - cx, j * CELL - cz)
            if rc <= dh < ro:
                ring.append((i, j, math.cos(math.pi * 0.5 * (dh - rc) / (ro - rc)) ** 2))
    wsum = sum(w for _, _, w in ring)
    if wsum <= 0:
        return
    k = dug * ratio / (wsum * AREA)
    for i, j, w in ring:
        h[j][i] += k * w


def repose(h, cx, cz, r, deg=REPOSE_ANGLE_DEG, iters=REPOSE_ITERATIONS):
    """안식각 릴랙세이션(Gauss-Seidel, 부피 보존). 순회 순서가 결과를 바꾼다 — 바꾸지 말 것."""
    lim = math.tan(math.radians(deg)) * CELL
    i0, i1 = max(1, int((cx - r) / CELL)), min(N - 1, int((cx + r) / CELL) + 1)
    j0, j1 = max(1, int((cz - r) / CELL)), min(N - 1, int((cz + r) / CELL) + 1)
    for _ in range(iters):
        moved = 0.0
        for j in range(j0, j1):
            for i in range(i0, i1):
                for di, dj, length in NEIGHBOURS:
                    ii, jj = i + di, j + dj
                    if not (i0 <= ii < i1 and j0 <= jj < j1):
                        continue
                    d = h[j][i] - h[jj][ii]
                    lm = lim * length
                    if abs(d) > lm:
                        t = (abs(d) - lm) * 0.5 * (1 if d > 0 else -1)
                        h[j][i] -= t
                        h[jj][ii] += t
                        moved += abs(t)
        if moved < 1e-4:
            break


def shot(h, cx, cz, rc, use_repose=True, iters=REPOSE_ITERATIONS):
    dug = carve(h, cx, cz, rc)
    deposit(h, cx, cz, rc, dug)
    if use_repose:
        repose(h, cx, cz, rc * DEPOSIT_RING_MUL + 4, iters=iters)


def stats(h, cx, cz, r, base=None):
    """(최대경사°, 최저높이, 최고높이) — base 대비."""
    ms, lo, hi = 0.0, 1e9, -1e9
    for j in range(max(1, int((cz - r) / CELL)), min(N - 1, int((cz + r) / CELL) + 1)):
        for i in range(max(1, int((cx - r) / CELL)), min(N - 1, int((cx + r) / CELL) + 1)):
            gx = (h[j][i + 1] - h[j][i - 1]) / (2 * CELL)
            gz = (h[j + 1][i] - h[j - 1][i]) / (2 * CELL)
            ms = max(ms, math.degrees(math.atan(math.hypot(gx, gz))))
            b = base(i, j) if base else 0.0
            lo, hi = min(lo, h[j][i] - b), max(hi, h[j][i] - b)
    return ms, lo, hi


def main():
    use_repose = "--no-repose" not in sys.argv
    tag = "안식각 OFF (네거티브 컨트롤)" if not use_repose else f"안식각 {REPOSE_ANGLE_DEG}°"
    print(f"=== 크레이터 검증 · {tag} ===\n")

    print("[1] 릴랙세이션 반복 수렴 (Rc=7m 평지 1발)")
    for it in (10, 20, 40, 80):
        h = make()
        shot(h, 100, 100, 7.0, use_repose, iters=it)
        s, lo, hi = stats(h, 100, 100, 22)
        flag = "OK" if s < TANK_CLIMB_LIMIT_DEG else "갇힘!"
        print(f"    {it:>3}회: 최대경사 {s:5.1f}°  깊이 {-lo:4.1f}m  둔덕 {hi:4.2f}m  [{flag}]")

    print("\n[2] 같은 지점 10발 연타 — 누적 톱니/접시 검사")
    h = make()
    for n in range(1, 11):
        shot(h, 100, 100, 7.0, use_repose)
        if n in (1, 3, 5, 10):
            s, lo, hi = stats(h, 100, 100, 25)
            flag = "OK" if s < TANK_CLIMB_LIMIT_DEG else "갇힘!"
            print(f"    {n:>2}발: 최대경사 {s:5.1f}°  깊이 {-lo:4.1f}m  둔덕 {hi:4.2f}m  [{flag}]")

    print("\n[3] 25° 비탈 타격 — A안이 옆면을 깎는가")
    slope = lambda i, j: i * CELL * math.tan(math.radians(25))
    h = make(slope)
    shot(h, 100, 100, 7.0, use_repose)
    s, lo, hi = stats(h, 100, 100, 22, slope)
    flag = "OK" if s < TANK_CLIMB_LIMIT_DEG else "갇힘!"
    print(f"    비탈 깎임 {-lo:4.1f}m  하단 퇴적 {hi:4.2f}m  최대경사 {s:5.1f}° (원래 25°)  [{flag}]")


if __name__ == "__main__":
    main()
