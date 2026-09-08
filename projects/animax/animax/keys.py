"""키 유틸: Nudge, Share Keys, Crop, Delete Redundant, Smart Snap, Smooth,
Keys Time(save/restore), Fast Bake, Tangents.
"""
import json
import os
from pymxs import runtime as rt

from . import core
from .core import Pose, N


# ----------------------------------------------------------------- Nudge
def nudge(tracks, delta, times=None):
    """키를 delta 프레임 이동. times=None 이면 현재 프레임의 키만."""
    if times is None:
        times = {core.current_frame()}
    with core.undo_block("Nudge Keys"):
        for tr in tracks:
            tr.move_keys(delta, set(times))


# ----------------------------------------------------------------- Share Keys
def share_keys(tracks, range_=None):
    """선택 노드들의 모든 키 시간 합집합에 대해 값 변화 없이 키를 추가."""
    s, e = range_ if range_ else core.anim_range()
    union = set()
    for tr in tracks:
        union.update(k for k in tr.key_times() if s <= k <= e)
    with core.undo_block("Share Keys"):
        for tr in tracks:
            have = set(tr.key_times())
            # 먼저 전부 샘플하고 나서 기록 (기록이 보간을 바꾸므로)
            missing = sorted(union - have)
            samples = {t: tr.sample(t) for t in missing}
            for t in missing:
                tr.write(t, samples[t], True)
    return sorted(union)


# ----------------------------------------------------------------- Crop
def crop(tracks, range_=None):
    """범위 밖 키 삭제. 경계 프레임에는 키를 굽는다."""
    s, e = range_ if range_ else core.anim_range()
    with core.undo_block("Crop Animation"):
        for tr in tracks:
            ks = tr.key_times()
            if not ks:
                continue
            edge = {t: tr.sample(t) for t in (s, e)}
            for t, p in edge.items():
                tr.write(t, p, True)
            outside = {k for k in tr.key_times() if k < s or k > e}
            tr.delete_keys(outside)


# ----------------------------------------------------------------- Delete Redundant
def delete_redundant(tracks, pos_tol=0.01, rot_tol=0.05):
    """이웃과 값이 같은 키(정적 구간)를 제거. 양 끝 키는 유지."""
    removed = {}
    with core.undo_block("Delete Redundant Keys"):
        for tr in tracks:
            ks = tr.key_times()
            if len(ks) < 3:
                continue
            poses = {k: tr.sample(k) for k in ks}
            dead = set()
            for i in range(1, len(ks) - 1):
                a, b, c = ks[i - 1], ks[i], ks[i + 1]
                d1, r1 = poses[b].distance(poses[a])
                d2, r2 = poses[b].distance(poses[c])
                if d1 <= pos_tol and r1 <= rot_tol and d2 <= pos_tol and r2 <= rot_tol:
                    dead.add(b)
            if dead:
                tr.delete_keys(dead)
                removed[tr.name] = sorted(dead)
    return removed


# ----------------------------------------------------------------- Smart Snap
def smart_snap(tracks):
    """서브프레임 키를 정수 프레임으로. 충돌 시 포즈를 우선해 값은 원래 시간 샘플로 굽는다."""
    with core.undo_block("Smart Snap Keys"):
        for tr in tracks:
            for c in tr.controllers():
                try:
                    n = rt.numKeys(c)
                except Exception:
                    continue
                for i in range(n, 0, -1):
                    kt = rt.getKeyTime(c, i)
                    f = float(kt)
                    r = round(f)
                    if abs(f - r) > 1e-6:
                        rt.deselectKeys(c)
                        rt.selectKeys(c, kt)
                        rt.moveKeys(c, (r - f), N("selection"))
                        rt.deselectKeys(c)


# ----------------------------------------------------------------- Smooth
def smooth(tracks, iterations=1, strength=0.5, range_=None):
    """선택 키를 이웃 키 평균 쪽으로 당겨 지터 제거."""
    s, e = range_ if range_ else core.anim_range()
    with core.undo_block("Smooth Keys"):
        for tr in tracks:
            ks = [k for k in tr.key_times() if s <= k <= e]
            if len(ks) < 3:
                continue
            poses = {k: tr.sample(k) for k in ks}
            for _ in range(iterations):
                new = dict(poses)
                for i in range(1, len(ks) - 1):
                    a, b, c = ks[i - 1], ks[i], ks[i + 1]
                    u = (b - a) / float(c - a)
                    mid = Pose.lerp(poses[a], poses[c], u)
                    new[b] = Pose.lerp(poses[b], mid, strength)
                poses = new
            for k in ks[1:-1]:
                tr.write(k, poses[k], True)


# ----------------------------------------------------------------- Keys Time
_KEYS_TIME_STORE = {}


def save_keys_time(tracks):
    for tr in tracks:
        _KEYS_TIME_STORE[tr.name] = tr.key_times()
    return {tr.name: len(_KEYS_TIME_STORE[tr.name]) for tr in tracks}


def restore_keys_time(tracks):
    """베이크된 트랙에서 저장해둔 키 시간만 남기고 나머지 키 삭제."""
    with core.undo_block("Restore Keys Time"):
        for tr in tracks:
            keep = set(_KEYS_TIME_STORE.get(tr.name, []))
            if not keep:
                continue
            ks = set(tr.key_times())
            # 남길 키가 없는 시간에 키를 먼저 굽고 (값 보존), 나머지 삭제
            for t in sorted(keep - ks):
                tr.write(t, tr.sample(t), True)
            tr.delete_keys(set(tr.key_times()) - keep)


# ----------------------------------------------------------------- Fast Bake
def bake(tracks, step=1, range_=None, delete_existing=False):
    """월드 포즈를 step 간격으로 샘플해 키로 굽는다."""
    s, e = range_ if range_ else core.anim_range()
    frames = list(range(s, e + 1, max(1, int(step))))
    if frames[-1] != e:
        frames.append(e)
    with core.undo_block("Fast Bake"):
        for tr in tracks:
            samples = {f: tr.sample(f) for f in frames}
            if delete_existing:
                tr.delete_keys(None)
            for f in frames:
                tr.write(f, samples[f], True)
    return len(frames)


# ----------------------------------------------------------------- Tangents (일반 노드)
_TANGENT = {"auto": N("auto"), "linear": N("linear"), "step": N("step"), "smooth": N("smooth"), "fast": N("fast"), "slow": N("slow")}


def set_tangents(tracks, kind="auto", range_=None):
    """Bezier 키의 in/out 탄젠트 타입 변경. Biped(TCB)는 continuity 로 근사."""
    s, e = range_ if range_ else (None, None)
    changed = 0
    with core.undo_block("Set Tangents"):
        for tr in tracks:
            for c in tr.controllers():
                try:
                    n = rt.numKeys(c)
                except Exception:
                    continue
                for i in range(1, n + 1):
                    kt = core.frame_of(rt.getKeyTime(c, i))
                    if s is not None and not (s <= kt <= e):
                        continue
                    try:
                        if tr.biped:
                            # 표준 getKey 는 BipedKey 속성이 없다(탐침) -> biped.getKey 로 TCB 편집.
                            # step 은 불가. linear = continuity 0 / auto = 25 (Max 기본)
                            k = rt.biped.getKey(c, i)
                            k.continuity = 0.0 if kind == "linear" else 25.0
                        else:
                            k = rt.getKey(c, i)  # 부모(Position_XYZ 등)는 예외 -> 건너뜀
                            k.inTangentType = _TANGENT[kind]
                            k.outTangentType = _TANGENT[kind]
                        changed += 1
                    except Exception:
                        continue
    return changed


# ----------------------------------------------------------------- JSON 헬퍼
def dump_json(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)


def load_json(path):
    with open(path, "r", encoding="utf-8") as f:
        return json.load(f)
