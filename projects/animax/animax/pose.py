"""포즈 유틸: Default/Reset Pose, Mirror, Select Opposite, Align, Xform(copy/paste),
Xform Relationship, Global Offset, 씬 간 애니/포즈 전송(JSON).
"""
import json
import re
from pymxs import runtime as rt

from . import core
from .core import Pose, N, Track

USERPROP_DEFAULT = "animax_default_pose"


# ----------------------------------------------------------------- Default / Reset
def _pose_to_list(p):
    return [p.pos.x, p.pos.y, p.pos.z, p.rot.x, p.rot.y, p.rot.z, p.rot.w, p.scale.x, p.scale.y, p.scale.z]


def _pose_from_list(v):
    return Pose(rt.Point3(v[0], v[1], v[2]), rt.quat(v[3], v[4], v[5], v[6]), rt.Point3(v[7], v[8], v[9]))


def store_default(tracks, t=None):
    """현재(또는 t) 포즈를 노드의 기본 포즈로 저장(노드 user prop). Biped 는 이걸로 Reset 한다."""
    t = core.current_frame() if t is None else t
    for tr in tracks:
        rt.setUserProp(tr.node, USERPROP_DEFAULT, json.dumps(_pose_to_list(tr.sample(t))))
    return len(tracks)


def default_pose(tr):
    """저장된 기본 포즈. 없으면 일반 노드는 부모 기준 단위 변환, Biped 는 None."""
    raw = rt.getUserProp(tr.node, USERPROP_DEFAULT)
    if raw:
        try:
            return _pose_from_list(json.loads(str(raw)))
        except Exception:
            pass
    if tr.biped:
        return None
    parent = tr.node.parent
    m = parent.transform if parent is not None else rt.matrix3(1)
    return Pose.from_matrix(m)


def reset_pose(tracks, channels=("pos", "rot", "scale"), t=None):
    t = core.current_frame() if t is None else t
    done, skipped = [], []
    with core.undo_block("Reset Pose"):
        for tr in tracks:
            d = default_pose(tr)
            if d is None:
                skipped.append(tr.name)
                continue
            tr.write(t, d, True, channels)
            done.append(tr.name)
    return done, skipped


# ----------------------------------------------------------------- Opposite 이름
_PAIRS = [
    (re.compile(r"(\b|_| )L(\b|_| )"), r"\1R\2"), (re.compile(r"(\b|_| )R(\b|_| )"), r"\1L\2"),
    (re.compile(r"Left"), "Right"), (re.compile(r"Right"), "Left"),
    (re.compile(r"left"), "right"), (re.compile(r"right"), "left"),
    (re.compile(r"_l$"), "_r"), (re.compile(r"_r$"), "_l"),
    (re.compile(r"\.l$"), ".r"), (re.compile(r"\.r$"), ".l"),
]


def opposite_name(name):
    """이름의 좌우 토큰을 바꾼 이름. 못 찾으면 None."""
    for i in range(0, len(_PAIRS), 2):
        pat_a, rep_a = _PAIRS[i]
        pat_b, rep_b = _PAIRS[i + 1]
        if pat_a.search(name):
            cand = pat_a.sub(rep_a, name, count=1)
            if cand != name:
                return cand
        if pat_b.search(name):
            cand = pat_b.sub(rep_b, name, count=1)
            if cand != name:
                return cand
    return None


def opposite_node(node):
    n = opposite_name(node.name)
    return rt.getNodeByName(n) if n else None


def select_opposite(nodes=None, add=False):
    nodes = core.selected_nodes() if nodes is None else nodes
    out = []
    for n in nodes:
        o = opposite_node(n)
        out.append(o if o is not None else n)  # 중앙 파트는 그대로 유지
    if add:
        out = list(nodes) + [o for o in out if o not in nodes]
    rt.select(out)
    return out


# ----------------------------------------------------------------- Mirror
def _mirror_quat(q, axis="x"):
    if axis == "x":
        return rt.quat(q.x, -q.y, -q.z, q.w)
    if axis == "y":
        return rt.quat(-q.x, q.y, -q.z, q.w)
    return rt.quat(-q.x, -q.y, q.z, q.w)


def _mirror_pos(p, axis="x"):
    return rt.Point3(-p.x if axis == "x" else p.x, -p.y if axis == "y" else p.y, -p.z if axis == "z" else p.z)


def mirror(nodes=None, range_=None, axis="x"):
    """선택 파트의 포즈(또는 범위 내 모든 키)를 좌우 미러.

    Biped: 내장 copy/paste(opposite) 사용 - 정확한 리그 규칙을 따른다.
    일반 노드: 이름 규칙으로 반대편을 찾고 월드 평면(axis) 대칭으로 계산.
    """
    nodes = core.selected_nodes() if nodes is None else list(nodes)
    if not nodes:
        return 0
    # 반대편 포함해 대상 확정
    targets = []
    seen = set()
    for n in nodes + [opposite_node(n) for n in nodes]:
        if n is not None and n.name not in seen:
            seen.add(n.name)
            targets.append(n)
    bip_nodes = [n for n in targets if core.is_biped(n)]
    std_nodes = [n for n in targets if not core.is_biped(n)]

    # 대상 시간
    if range_ is None:
        times = [core.current_frame()]
    else:
        s, e = range_
        union = set()
        for n in targets:
            union.update(k for k in Track(n).key_times() if s <= k <= e)
        times = sorted(union) or [core.current_frame()]

    count = 0
    with core.undo_block("Mirror Pose"):
        if bip_nodes:
            ctrl = core.biped_ctrl(bip_nodes[0])
            cc = rt.biped.createCopyCollection(ctrl, "animax_mirror")
            try:
                for t in times:
                    with pymxs_attime(t):
                        cp = rt.biped.copyBipPosture(ctrl, cc, bip_nodes, N("snapNone"))
                    with pymxs_animate_at(t):
                        rt.biped.pasteBipPosture(ctrl, cp, True, N("pstdefault"), False, False, False, False)
                    count += 1
            finally:
                try:
                    rt.biped.deleteCopyCollection(ctrl, cc)
                except Exception:
                    pass
        if std_nodes:
            trs = {n.name: Track(n) for n in std_nodes}
            for t in times:
                snap = {name: tr.sample(t) for name, tr in trs.items()}
                for name, tr in trs.items():
                    src_name = opposite_name(name) if opposite_name(name) in snap else name
                    p = snap[src_name]
                    tr.write(t, Pose(_mirror_pos(p.pos, axis), _mirror_quat(p.rot, axis), p.scale), True)
                count += 1
    return count


def pymxs_attime(t):
    import pymxs
    return pymxs.attime(t)


class pymxs_animate_at:
    def __init__(self, t):
        import pymxs
        self.a = pymxs.animate(True)
        self.t = pymxs.attime(t)

    def __enter__(self):
        self.a.__enter__()
        self.t.__enter__()
        return self

    def __exit__(self, *exc):
        self.t.__exit__(*exc)
        self.a.__exit__(*exc)
        return False


# ----------------------------------------------------------------- Align
def align(nodes=None, range_=None, channels=("pos", "rot")):
    """마지막 선택 노드(타깃)에 나머지를 맞춘다. range_ 주면 범위 내 매 프레임 키."""
    nodes = core.selected_nodes() if nodes is None else list(nodes)
    if len(nodes) < 2:
        return 0
    target = Track(nodes[-1])
    others = [Track(n) for n in nodes[:-1]]
    times = [core.current_frame()] if range_ is None else list(range(range_[0], range_[1] + 1))
    with core.undo_block("Align"):
        samples = {t: target.sample(t) for t in times}
        for tr in others:
            for t in times:
                tr.write(t, samples[t], True, channels)
    return len(others)


# ----------------------------------------------------------------- Xform copy / paste
_XFORM = {"poses": [], "range": None}


def xform_copy(tracks, range_=None):
    """월드 포즈 저장. range_=None 이면 현재 프레임 하나."""
    times = [core.current_frame()] if range_ is None else list(range(range_[0], range_[1] + 1))
    _XFORM["poses"] = [{t: tr.sample(t) for t in times} for tr in tracks]
    _XFORM["range"] = (times[0], times[-1])
    return len(tracks), len(times)


def xform_paste(tracks, at=None, channels=("pos", "rot")):
    """저장된 포즈를 순서대로 붙인다. at 주면 그 프레임부터(오프셋), 아니면 원래 시간."""
    if not _XFORM["poses"]:
        return 0
    s0 = _XFORM["range"][0]
    with core.undo_block("Xform Paste"):
        for i, tr in enumerate(tracks):
            src = _XFORM["poses"][min(i, len(_XFORM["poses"]) - 1)]
            for t, p in src.items():
                dst_t = t if at is None else at + (t - s0)
                tr.write(dst_t, p, True, channels)
    return len(tracks)


# ----------------------------------------------------------------- Xform Relationship
class Relationship:
    """드라이버 하나에 팔로워들의 상대 오프셋을 기억. apply/bake 시 팔로워가 따라간다."""

    def __init__(self, driver, followers, t=None):
        t = core.current_frame() if t is None else t
        self.driver = Track(driver)
        self.followers = [Track(f) for f in followers]
        dm = self.driver.sample(t).matrix()
        self.offsets = [tr.sample(t).matrix() * rt.inverse(dm) for tr in self.followers]

    def apply(self, times=None, channels=("pos", "rot")):
        times = [core.current_frame()] if times is None else times
        with core.undo_block("Xform Relationship"):
            dm = {t: self.driver.sample(t).matrix() for t in times}
            for tr, off in zip(self.followers, self.offsets):
                for t in times:
                    tr.write(t, Pose.from_matrix(off * dm[t]), True, channels)
        return len(times)

    def bake(self, range_=None):
        s, e = range_ if range_ else core.anim_range()
        return self.apply(list(range(s, e + 1)))


# ----------------------------------------------------------------- Global Offset
class GlobalOffset:
    """begin() 으로 현재 포즈를 기억 -> 사용자가 뷰포트에서 파트를 옮김 -> apply() 가
    그 델타를 범위 내 모든 키에 동일하게 적용한다(레이어 없이 포즈 오프셋)."""

    def __init__(self, tracks):
        self.tracks = tracks
        self.t0 = core.current_frame()
        self.start = {tr.name: tr.sample(self.t0) for tr in tracks}

    def apply(self, range_=None, channels=("pos", "rot")):
        s, e = range_ if range_ else core.anim_range()
        n = 0
        with core.undo_block("Global Offset"):
            for tr in self.tracks:
                now = tr.sample(self.t0)
                a = self.start[tr.name]
                dpos = now.pos - a.pos
                drot = now.rot * rt.inverse(a.rot)
                ks = [k for k in tr.key_times() if s <= k <= e and k != self.t0]
                poses = {k: tr.sample(k) for k in ks}
                for k in ks:
                    p = poses[k]
                    tr.write(k, Pose(p.pos + dpos, drot * p.rot, p.scale), True, channels)
                    n += 1
        return n


# ----------------------------------------------------------------- 씬 간 전송 (JSON)
def export_animation(tracks, path, range_=None, per_frame=False):
    """키(또는 매 프레임) 월드 포즈를 JSON 으로 저장. 다른 씬에서 import_animation."""
    s, e = range_ if range_ else core.anim_range()
    data = {"range": [s, e], "fps": float(rt.frameRate), "nodes": {}}
    for tr in tracks:
        times = list(range(s, e + 1)) if per_frame else [k for k in tr.key_times() if s <= k <= e]
        if not times:
            times = [core.current_frame()]
        data["nodes"][tr.name] = {str(t): _pose_to_list(tr.sample(t)) for t in times}
    from .keys import dump_json
    dump_json(path, data)
    return len(data["nodes"])


def import_animation(path, name_map=None, offset=0, nodes=None):
    """JSON 을 이름 매칭으로 적용. name_map={src:dst}, nodes 주면 순서 매칭."""
    from .keys import load_json
    data = load_json(path)
    name_map = name_map or {}
    applied = 0
    with core.undo_block("Import Animation"):
        items = list(data["nodes"].items())
        for i, (src, frames) in enumerate(items):
            if nodes is not None:
                if i >= len(nodes):
                    break
                node = nodes[i]
            else:
                node = rt.getNodeByName(name_map.get(src, src))
            if node is None:
                continue
            tr = Track(node)
            for t, v in sorted(frames.items(), key=lambda kv: int(kv[0])):
                tr.write(int(t) + offset, _pose_from_list(v), True)
            applied += 1
    return applied


def export_pose(tracks, path):
    return export_animation(tracks, path, range_=(core.current_frame(), core.current_frame()), per_frame=True)


def import_pose(path, at=None, nodes=None):
    from .keys import load_json
    data = load_json(path)
    s = data["range"][0]
    at = core.current_frame() if at is None else at
    return import_animation(path, offset=at - s, nodes=nodes)


def biped_save(root, path, range_=None):
    """Biped 전용 .bip 저장 (완전 보존)."""
    ctrl = root.controller
    if range_:
        return rt.biped.saveBipFileSegment(ctrl, path, range_[0], range_[1], N("keyPerFrame"))
    return rt.biped.saveBipFile(ctrl, path)


def biped_load(root, path):
    return rt.biped.loadBipFile(root.controller, path)
