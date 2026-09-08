"""Spacify - 임시 컨트롤 기반 스페이스 스위칭 (Biped 호환).

Biped 파트에는 컨스트레인트를 걸 수 없으므로 모든 모드를 하나의 원리로 통일한다.

    1. create(): 선택 파트마다 Point 헬퍼를 만들고, 범위 내 매 프레임 파트의 월드 포즈를
       헬퍼에 굽는다. 모드에 따라 헬퍼를 임시 부모(피벗/드라이버/카메라/그룹) 밑에 넣는다.
       -> 헬퍼의 로컬 애니가 "그 공간에서의 애니"가 된다. 원본 파트는 손대지 않는다.
    2. 사용자가 헬퍼(또는 임시 부모)를 자유롭게 애니메이션한다.
    3. apply(): 매 프레임 헬퍼의 월드 포즈를 원본 파트로 굽고 헬퍼를 지운다.
       discard(): 헬퍼만 지운다(원본 그대로).

모드: world | pivot | relative | world_orient | camera | group | aim
"""
from pymxs import runtime as rt

from . import core
from .core import Pose, Track, N

HELPER_PREFIX = "AX_"
PARENT_PREFIX = "AXP_"
_SESSIONS = {}
_NEXT_ID = [1]


def _make_point(name, size=8.0, color=(255, 170, 0), box=False, cross=True):
    p = rt.Point(name=name, size=size, box=box, cross=cross, axistripod=True, centermarker=False)
    p.wirecolor = rt.color(*color)
    return p


class SpacifySession:
    def __init__(self, mode, nodes, driver=None, range_=None, pivot_pos=None, target=None):
        self.id = _NEXT_ID[0]
        _NEXT_ID[0] += 1
        self.mode = mode
        self.nodes = list(nodes)
        self.tracks = [Track(n) for n in self.nodes]
        self.driver = driver
        self.target = target
        self.range_ = range_ if range_ else core.anim_range()
        self.pivot_pos = pivot_pos
        self.helpers = []
        self.parent = None
        self.key_times = {tr.name: tr.key_times() for tr in self.tracks}

    # ---- 생성
    def create(self):
        s, e = self.range_
        frames = list(range(s, e + 1))
        t0 = core.current_frame()
        with core.undo_block("Spacify %s" % self.mode):
            # 임시 부모
            if self.mode in ("pivot", "relative", "world_orient", "camera", "group"):
                self.parent = _make_point(PARENT_PREFIX + "%s_%d" % (self.mode, self.id), size=14.0, color=(0, 200, 255), box=True)
                self._animate_parent(frames, t0)
            # 파트별 헬퍼
            for tr in self.tracks:
                h = _make_point(HELPER_PREFIX + "%s_%d_%s" % (self.mode, self.id, tr.name))
                if self.parent is not None:
                    h.parent = self.parent
                samples = {f: tr.sample(f) for f in frames}
                import pymxs
                with pymxs.animate(True):
                    for f in frames:
                        with pymxs.attime(f):
                            h.transform = samples[f].matrix()
                if self.mode == "aim" and self.target is not None:
                    self._add_lookat(h)
                self.helpers.append(h)
        _SESSIONS[self.id] = self
        rt.select(self.helpers if self.parent is None else [self.parent])
        return self

    def _animate_parent(self, frames, t0):
        import pymxs
        P = self.parent
        if self.mode == "pivot":
            pos = self.pivot_pos if self.pivot_pos is not None else Pose.average([tr.sample(t0) for tr in self.tracks]).pos
            P.transform = rt.transMatrix(pos)
            return
        if self.mode == "group":
            P.transform = rt.transMatrix(Pose.average([tr.sample(t0) for tr in self.tracks]).pos)
            return
        if self.driver is None:
            return
        d = Track(self.driver)
        with pymxs.animate(True):
            for f in frames:
                with pymxs.attime(f):
                    m = d.sample(f).matrix()
                    if self.mode == "world_orient":
                        m = rt.transMatrix(m.translationpart)
                    P.transform = m

    def _add_lookat(self, h):
        rc = rt.LookAt_Constraint()
        rc.appendTarget(self.target, 100.0)
        rc.lookat_vector_length = 0
        rt.setPropertyController(h.controller, "rotation", rc)

    # ---- 적용/폐기
    def apply(self, restore_keys=False, channels=("pos", "rot")):
        s, e = self.range_
        frames = list(range(s, e + 1))
        with core.undo_block("Spacify Apply"):
            for tr, h in zip(self.tracks, self.helpers):
                samples = {}
                import pymxs
                for f in frames:
                    with pymxs.attime(f):
                        samples[f] = Pose.from_matrix(h.transform)
                for f in frames:
                    tr.write(f, samples[f], True, channels)
                if restore_keys:
                    keep = set(self.key_times.get(tr.name, []))
                    if keep:
                        tr.delete_keys(set(tr.key_times()) - keep)
            self._cleanup()
        _SESSIONS.pop(self.id, None)
        rt.select(self.nodes)
        return len(frames)

    def discard(self):
        with core.undo_block("Spacify Discard"):
            self._cleanup()
        _SESSIONS.pop(self.id, None)
        rt.select(self.nodes)

    def _cleanup(self):
        for h in self.helpers:
            if rt.isValidNode(h):
                rt.delete(h)
        if self.parent is not None and rt.isValidNode(self.parent):
            rt.delete(self.parent)
        self.helpers = []
        self.parent = None


# ----------------------------------------------------------------- 편의 함수
def create(mode, nodes=None, driver=None, range_=None, pivot_pos=None, target=None):
    nodes = core.selected_nodes() if nodes is None else nodes
    if mode in ("relative", "world_orient") and driver is None:
        # 마지막 선택을 드라이버로
        if len(nodes) < 2:
            raise ValueError("relative/world_orient 는 드라이버(마지막 선택)가 필요하다")
        driver, nodes = nodes[-1], nodes[:-1]
    if mode == "aim" and target is None:
        if len(nodes) < 2:
            raise ValueError("aim 은 타깃(마지막 선택)이 필요하다")
        target, nodes = nodes[-1], nodes[:-1]
    if mode == "camera":
        if driver is None:
            cam = rt.getActiveCamera()
            if cam is None:
                raise ValueError("활성 카메라 뷰가 아니다")
            driver = cam
    return SpacifySession(mode, nodes, driver, range_, pivot_pos, target).create()


def sessions():
    return dict(_SESSIONS)


def latest():
    if not _SESSIONS:
        return None
    return _SESSIONS[max(_SESSIONS)]


def recover_from_scene():
    """세션 메모리가 날아갔을 때(스크립트 리로드) 씬의 헬퍼 이름으로 세션 복원."""
    found = {}
    for n in rt.objects:
        nm = n.name
        if not nm.startswith(HELPER_PREFIX):
            continue
        try:
            _, mode, sid, part = nm.split("_", 3)
            sid = int(sid)
        except ValueError:
            continue
        node = rt.getNodeByName(part)
        if node is None:
            continue
        sess = found.get(sid)
        if sess is None:
            sess = SpacifySession(mode, [])
            sess.id = sid
            found[sid] = sess
        sess.nodes.append(node)
        sess.tracks.append(Track(node))
        sess.helpers.append(n)
        sess.parent = n.parent if (n.parent is not None and n.parent.name.startswith(PARENT_PREFIX)) else sess.parent
    _SESSIONS.update(found)
    _NEXT_ID[0] = max([_NEXT_ID[0]] + [s + 1 for s in found])
    return list(found.values())
