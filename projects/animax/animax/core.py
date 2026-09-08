"""공통 레이어: Biped/일반 노드를 같은 Track 인터페이스로 감싼다.

설계 원칙
- 포즈는 항상 **월드 공간** (pos Point3, rot Quat, scale Point3)으로 다룬다.
  Biped는 biped.setTransform 이 월드 기준이고, 일반 노드는 node.transform 이 월드라
  두 계열을 같은 수식으로 블렌딩할 수 있다.
- 키 시간은 int 프레임으로 통일한다.
"""
import math
import random
import pymxs
from pymxs import runtime as rt

N = rt.Name
POS, ROT, SCALE = N("pos"), N("rotation"), N("scale")


# ----------------------------------------------------------------- 시간 유틸
def frame_of(t):
    """pymxs Time -> int 프레임."""
    try:
        return int(round(float(t)))
    except Exception:
        pass
    s = str(t)
    for tok in ("Time<", "<", ">", "f"):
        s = s.replace(tok, "")
    return int(round(float(s.strip())))


def current_frame():
    return frame_of(rt.sliderTime)


def anim_range():
    r = rt.animationRange
    return frame_of(r.start), frame_of(r.end)


# ----------------------------------------------------------------- 포즈
def qdot(a, b):
    return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w


def slerp(a, b, w):
    """MaxScript slerp 은 q/-q 이중 표현을 안 잡는다(먼 길로 회전). 짧은 길 강제."""
    if qdot(a, b) < 0.0:
        b = rt.quat(-b.x, -b.y, -b.z, -b.w)
    return rt.slerp(a, b, w)


class Pose:
    __slots__ = ("pos", "rot", "scale")

    def __init__(self, pos, rot, scale=None):
        self.pos = rt.Point3(pos.x, pos.y, pos.z)
        self.rot = rt.quat(rot.x, rot.y, rot.z, rot.w)
        self.scale = rt.Point3(scale.x, scale.y, scale.z) if scale is not None else rt.Point3(1, 1, 1)

    def copy(self):
        return Pose(self.pos, self.rot, self.scale)

    @staticmethod
    def lerp(a, b, w):
        """a->b 로 w 만큼 블렌드. 0..1 밖이면 외삽."""
        pos = a.pos + (b.pos - a.pos) * w
        if 0.0 <= w <= 1.0:
            q = slerp(a.rot, b.rot, w)
        else:
            brot = b.rot
            if qdot(a.rot, brot) < 0.0:
                brot = rt.quat(-brot.x, -brot.y, -brot.z, -brot.w)
            delta = brot * rt.inverse(a.rot)
            ang = math.degrees(math.acos(max(-1.0, min(1.0, delta.w)))) * 2.0
            if abs(ang) < 1e-6:
                q = rt.quat(a.rot.x, a.rot.y, a.rot.z, a.rot.w)
            else:
                axis = rt.normalize(rt.Point3(delta.x, delta.y, delta.z))
                q = rt.quat(ang * w, axis) * a.rot
        sc = a.scale + (b.scale - a.scale) * w
        return Pose(pos, q, sc)

    @staticmethod
    def average(poses):
        if not poses:
            return None
        acc = poses[0].copy()
        for i, p in enumerate(poses[1:], start=2):
            acc = Pose.lerp(acc, p, 1.0 / i)
        return acc

    def distance(self, other):
        """(위치 거리, 회전 각도 deg)."""
        d = rt.distance(self.pos, other.pos)
        dq = other.rot * rt.inverse(self.rot)
        ang = math.degrees(math.acos(max(-1.0, min(1.0, abs(dq.w))))) * 2.0
        return d, ang

    def matrix(self):
        rm = rt.rotate(rt.matrix3(1), self.rot)
        return rt.scaleMatrix(self.scale) * rm * rt.transMatrix(self.pos)

    @staticmethod
    def from_matrix(m):
        return Pose(m.translationpart, m.rotationpart, m.scalepart)


# ----------------------------------------------------------------- 노드 분류
def is_biped(node):
    try:
        return rt.classOf(node) == rt.Biped_Object and rt.classOf(node.controller) != rt.Footsteps
    except Exception:
        return False


def is_biped_com(node):
    return is_biped(node) and rt.classOf(node.controller) == rt.Vertical_Horizontal_Turn


def biped_root(node):
    """이 바이패드 파트가 속한 COM 노드(Bip001)."""
    if not is_biped(node):
        return None
    n = node
    while n is not None and not is_biped_com(n):
        n = n.parent
    return n


def biped_ctrl(node):
    r = biped_root(node)
    return r.controller if r is not None else None


def biped_parts(root):
    """COM 아래 전체 바이패드 노드(발자국 제외)."""
    out = []
    stack = [root]
    while stack:
        n = stack.pop()
        if is_biped(n):
            out.append(n)
        for i in range(1, n.children.count + 1):
            stack.append(n.children[i - 1])
    return out


# ----------------------------------------------------------------- Track
class Track:
    """노드 하나의 애니메이션 채널 집합. Biped/일반 공통 인터페이스."""

    # Biped 손·발에 월드 위치를 쓰면 IK 로 팔·다리 체인이 풀린다(탐침 확인). 기본은 회전만 쓰고
    # COM 만 위치+회전. 발 고정 등 IK 위치 블렌드가 필요할 때만 pos_ik=True.
    pos_ik = False

    def __init__(self, node):
        self.node = node
        self.biped = is_biped(node)
        self.com = is_biped_com(node)

    @property
    def name(self):
        return self.node.name

    # ---- 컨트롤러
    def controllers(self):
        n = self.node
        if self.biped:
            if self.com:
                c = n.controller
                return [rt.biped.getVerticalControl(c), rt.biped.getHorizontalControl(c), rt.biped.getTurnControl(c)]
            return [n.controller]
        out = []
        tc = n.controller
        for sub in ("position", "rotation", "scale"):
            try:
                c = rt.getPropertyController(tc, sub)
            except Exception:
                c = None
            if c is None:
                continue
            out.append(c)
            try:
                for i in range(1, c.numSubs + 1):
                    sc = rt.getSubAnim(c, i).controller
                    if sc is not None:
                        out.append(sc)
            except Exception:
                pass
        return out

    def float_controllers(self):
        """그래프 에디터식 값 조작이 가능한 float 서브 컨트롤러(일반 노드만)."""
        if self.biped:
            return []
        out = []
        for c in self.controllers():
            try:
                if rt.superClassOf(c) == rt.FloatController:
                    out.append(c)
            except Exception:
                continue
        return out

    # ---- 키 시간
    def key_times(self):
        ts = set()
        for c in self.controllers():
            try:
                for i in range(1, rt.numKeys(c) + 1):
                    ts.add(frame_of(rt.getKeyTime(c, i)))
            except Exception:
                continue
        return sorted(ts)

    def neighbors(self, t):
        """t 기준 (이전 키, 다음 키). 없으면 None."""
        ks = self.key_times()
        prev = max((k for k in ks if k < t), default=None)
        nxt = min((k for k in ks if k > t), default=None)
        return prev, nxt

    def has_key(self, t):
        return t in self.key_times()

    # ---- 포즈 샘플/기록
    def sample(self, t):
        with pymxs.attime(t):
            if self.biped:
                return Pose(rt.biped.getTransform(self.node, POS), rt.biped.getTransform(self.node, ROT))
            return Pose.from_matrix(self.node.transform)

    def write(self, t, pose, set_key=True, channels=("pos", "rot", "scale")):
        """t 프레임에 월드 포즈 기록. set_key=True면 키 생성(animate on)."""
        with pymxs.animate(set_key):
            with pymxs.attime(t):
                if self.biped:
                    if "pos" in channels and (self.com or self.pos_ik):
                        rt.biped.setTransform(self.node, POS, pose.pos, set_key)
                    if "rot" in channels:
                        rt.biped.setTransform(self.node, ROT, pose.rot, set_key)
                    return
                cur = Pose.from_matrix(self.node.transform)
                merged = Pose(pose.pos if "pos" in channels else cur.pos,
                              pose.rot if "rot" in channels else cur.rot,
                              pose.scale if "scale" in channels else cur.scale)
                self.node.transform = merged.matrix()

    # ---- 키 추가/삭제/이동
    def add_key(self, t):
        """값 변화 없이 t에 키 추가 (Share Keys)."""
        self.write(t, self.sample(t), True)

    def delete_keys(self, times=None):
        """times=None 이면 전부 삭제."""
        for c in self.controllers():
            try:
                if times is None:
                    if self.biped:
                        rt.biped.deleteKeys(c, N("allKeys"))
                    else:
                        rt.deleteKeys(c, N("allKeys"))
                    continue
                # 표준 deleteKey(인덱스) 가 Biped 컨트롤러에도 통한다(탐침 확인). 역순 삭제.
                for i in range(rt.numKeys(c), 0, -1):
                    if frame_of(rt.getKeyTime(c, i)) in times:
                        rt.deleteKey(c, i)
            except Exception:
                continue

    def move_keys(self, delta, times=None):
        """키를 delta 프레임 이동. times 지정 시 그 키만."""
        if delta == 0:
            return
        for c in self.controllers():
            try:
                if times is None:
                    rt.moveKeys(c, delta)
                else:
                    rt.deselectKeys(c)
                    for i in range(1, rt.numKeys(c) + 1):
                        if frame_of(rt.getKeyTime(c, i)) in times:
                            rt.selectKeys(c, rt.getKeyTime(c, i))
                    rt.moveKeys(c, delta, N("selection"))
                    rt.deselectKeys(c)
            except Exception:
                continue


# ----------------------------------------------------------------- 선택
def selected_nodes():
    return [n for n in rt.selection]


def tracks_for(nodes=None):
    nodes = selected_nodes() if nodes is None else nodes
    return [Track(n) for n in nodes if n is not None]


def expand_biped_selection(nodes):
    """선택에 바이패드 파트가 하나라도 있으면 그 바이패드 전체 파트로 확장."""
    out, seen = [], set()
    for n in nodes:
        root = biped_root(n)
        parts = biped_parts(root) if root is not None else [n]
        for p in parts:
            if p.name not in seen:
                seen.add(p.name)
                out.append(p)
    return out


# ----------------------------------------------------------------- undo/redraw
class undo_block:
    """with undo_block("이름"): ... -> Ctrl+Z 한 번으로 되돌아간다."""

    def __init__(self, name):
        self.name = name

    def __enter__(self):
        rt.theHold.Begin()
        rt.disableSceneRedraw()
        return self

    def __exit__(self, et, ev, tb):
        rt.enableSceneRedraw()
        if et is None:
            rt.theHold.Accept(self.name)
        else:
            rt.theHold.Cancel()
        rt.redrawViews()
        return False


def seeded_random(seed):
    return random.Random(seed)
