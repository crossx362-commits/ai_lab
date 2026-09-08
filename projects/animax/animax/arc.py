"""Arc Tracker - 선택 파트의 궤적을 스플라인으로 그린다. 카메라 스페이스 옵션 포함.

무거운 리그에서도 가볍도록 뷰포트 드로잉 콜백 대신 SplineShape 를 쓴다.
시간 변경 시 자동 갱신(registerTimeCallback), 실패하면 refresh() 수동 호출.
"""
from pymxs import runtime as rt

from . import core
from .core import Track

_TRACKERS = {}
_CB_REGISTERED = [False]
PREFIX = "AXARC_"


class ArcTracker:
    def __init__(self, node, before=12, after=12, camera=None, show_keys=True):
        self.node = node
        self.track = Track(node)
        self.before = before
        self.after = after
        self.camera = camera
        self.show_keys = show_keys
        self.shape = None
        self.markers = None

    def _positions(self, t):
        s, e = core.anim_range()
        a, b = max(s, t - self.before), min(e, t + self.after)
        pts = {}
        cam_now = None
        if self.camera is not None:
            import pymxs
            with pymxs.attime(t):
                cam_now = self.camera.transform
        for f in range(a, b + 1):
            p = self.track.sample(f).pos
            if self.camera is not None:
                import pymxs
                with pymxs.attime(f):
                    cm = self.camera.transform
                # 카메라 공간 좌표 -> 현재 카메라 위치 기준 월드로 재배치
                p = (p * rt.inverse(cm)) * cam_now
            pts[f] = p
        return pts

    def build(self):
        if self.shape is None or not rt.isValidNode(self.shape):
            self.shape = rt.SplineShape(name=PREFIX + self.node.name)
            self.shape.wirecolor = rt.color(255, 220, 0)
            self.shape.renderable = False
            self.shape.isFrozen = True
            self.shape.isHidden = False
        if self.show_keys and (self.markers is None or not rt.isValidNode(self.markers)):
            self.markers = rt.SplineShape(name=PREFIX + "keys_" + self.node.name)
            self.markers.wirecolor = rt.color(255, 60, 60)
            self.markers.renderable = False
            self.markers.isFrozen = True
        self.refresh()
        return self

    def refresh(self):
        t = core.current_frame()
        pts = self._positions(t)
        sh = self.shape
        rt.deleteSpline(sh, 1) if rt.numSplines(sh) > 0 else None
        while rt.numSplines(sh) > 0:
            rt.deleteSpline(sh, 1)
        idx = rt.addNewSpline(sh)
        for f in sorted(pts):
            rt.addKnot(sh, idx, N_CORNER, N_LINE, pts[f])
        rt.updateShape(sh)
        if self.markers is not None:
            mk = self.markers
            while rt.numSplines(mk) > 0:
                rt.deleteSpline(mk, 1)
            keys = set(self.track.key_times())
            size = 1.5
            for f in sorted(pts):
                if f in keys or f == t:
                    p = pts[f]
                    r = size * (2.0 if f == t else 1.0)
                    i = rt.addNewSpline(mk)
                    rt.addKnot(mk, i, N_CORNER, N_LINE, p + rt.Point3(-r, 0, 0))
                    rt.addKnot(mk, i, N_CORNER, N_LINE, p + rt.Point3(r, 0, 0))
                    i = rt.addNewSpline(mk)
                    rt.addKnot(mk, i, N_CORNER, N_LINE, p + rt.Point3(0, 0, -r))
                    rt.addKnot(mk, i, N_CORNER, N_LINE, p + rt.Point3(0, 0, r))
            rt.updateShape(mk)

    def remove(self):
        for s in (self.shape, self.markers):
            if s is not None and rt.isValidNode(s):
                rt.delete(s)
        self.shape = self.markers = None


N_CORNER = rt.Name("corner")
N_LINE = rt.Name("line")


def _on_time_change():
    for tr in list(_TRACKERS.values()):
        try:
            tr.refresh()
        except Exception:
            pass


def start(nodes=None, before=12, after=12, camera=None):
    nodes = core.selected_nodes() if nodes is None else nodes
    with core.undo_block("Arc Tracker"):
        for n in nodes:
            if n.name in _TRACKERS:
                _TRACKERS[n.name].remove()
            _TRACKERS[n.name] = ArcTracker(n, before, after, camera).build()
    if not _CB_REGISTERED[0]:
        try:
            rt.registerTimeCallback(_on_time_change)
            _CB_REGISTERED[0] = True
        except Exception:
            pass
    return list(_TRACKERS)


def stop(nodes=None):
    names = [n.name for n in nodes] if nodes else list(_TRACKERS)
    with core.undo_block("Arc Tracker Off"):
        for nm in names:
            tr = _TRACKERS.pop(nm, None)
            if tr:
                tr.remove()
        # 씬에 남은 잔재
        if not nodes:
            for n in list(rt.objects):
                if n.name.startswith(PREFIX):
                    rt.delete(n)
    if not _TRACKERS and _CB_REGISTERED[0]:
        try:
            rt.unRegisterTimeCallback(_on_time_change)
        except Exception:
            pass
        _CB_REGISTERED[0] = False


def refresh():
    _on_time_change()


def set_range(before, after):
    for tr in _TRACKERS.values():
        tr.before, tr.after = before, after
    refresh()
