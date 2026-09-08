"""애니메이션 슬라이더 (Tween Machine, Blend to Neighbor, Ease, Scale Keys, Push/Pull,
Noise/Wave, Time Offset(+Stagger), Blend to Default, Connect to Neighbour).

공통 구조
    session = SliderSession(op_name, tracks, mode="current"|"range", range_=(s,e))
    session.begin()         # 베이스라인 캡처 + undo 시작
    session.update(w)       # w in [-1, 1], 드래그마다 호출 (항상 베이스라인 기준 재계산)
    session.end() / cancel()

모든 연산은 월드 포즈(core.Pose) 기준이라 Biped와 일반 노드에 똑같이 적용된다.
"""
import math
from pymxs import runtime as rt

from . import core
from .core import Pose


# ----------------------------------------------------------------- 베이스라인
class Baseline:
    """슬라이더 시작 시점의 포즈 스냅샷."""

    def __init__(self, tracks, times, dense=None):
        self.tracks = tracks
        self.times = {}       # track.name -> [키 시간들(대상)]
        self.poses = {}       # track.name -> {t: Pose}
        self.neigh = {}       # track.name -> (prev_t, next_t)  (대상 밖 이웃)
        self.dense = {}       # track.name -> {t: Pose} 연속 샘플 (time offset 용)
        for tr in tracks:
            ks = tr.key_times()
            tgt = sorted(t for t in times if (t in ks) or (len(times) == 1))
            self.times[tr.name] = tgt
            lo, hi = (min(tgt), max(tgt)) if tgt else (None, None)
            prev = max((k for k in ks if lo is not None and k < lo), default=None)
            nxt = min((k for k in ks if hi is not None and k > hi), default=None)
            self.neigh[tr.name] = (prev, nxt)
            need = set(tgt)
            if prev is not None:
                need.add(prev)
            if nxt is not None:
                need.add(nxt)
            self.poses[tr.name] = {t: tr.sample(t) for t in need}
            if dense is not None:
                a, b = dense
                self.dense[tr.name] = {t: tr.sample(t) for t in range(a, b + 1)}

    def restore(self):
        for tr in self.tracks:
            for t in self.times[tr.name]:
                tr.write(t, self.poses[tr.name][t], True)


def _sample_dense(dense, t):
    """dense 딕셔너리에서 t(실수 가능) 보간 샘플."""
    if not dense:
        return None
    ks = sorted(dense)
    if t <= ks[0]:
        return dense[ks[0]]
    if t >= ks[-1]:
        return dense[ks[-1]]
    lo = int(math.floor(t))
    hi = lo + 1
    if lo == t or hi not in dense:
        return dense[lo]
    return Pose.lerp(dense[lo], dense[hi], t - lo)


# ----------------------------------------------------------------- 이징 프로파일
def ease_profile(u, w):
    """Ease 슬라이더의 시간 왜곡 함수.

    u: 구간 내 정규화 시간 0..1, w: 슬라이더 값 -1..1.
    w<0 = ease in (도착 직전이 느려짐), w>0 = ease out (출발 직후가 느려짐).
    반환값은 0..1 (해당 프레임에서 시작 포즈->끝 포즈 블렌드 비율).

    기본값은 거듭제곱 곡선. 애니메이터 취향에 따라 sin/cubic 등으로 바꿔도 된다.
    """
    k = 1.0 + 3.0 * abs(w)
    if w < 0:
        return 1.0 - (1.0 - u) ** k
    return u ** k


# ----------------------------------------------------------------- 연산들
class Ops:
    """각 연산: (baseline, track, w, ctx) -> {t: Pose} (기록할 포즈)."""

    @staticmethod
    def tween(bl, tr, w, ctx):
        prev, nxt = bl.neigh[tr.name]
        out = {}
        for t in bl.times[tr.name]:
            P = bl.poses[tr.name].get(prev)
            Nx = bl.poses[tr.name].get(nxt)
            if P is None and Nx is None:
                continue
            if P is None:
                P = bl.poses[tr.name][t]
            if Nx is None:
                Nx = bl.poses[tr.name][t]
            out[t] = Pose.lerp(P, Nx, (w + 1.0) / 2.0)
        return out

    @staticmethod
    def blend_neighbor(bl, tr, w, ctx):
        prev, nxt = bl.neigh[tr.name]
        out = {}
        for t in bl.times[tr.name]:
            C = bl.poses[tr.name][t]
            tgt_t = prev if w < 0 else nxt
            if tgt_t is None:
                continue
            out[t] = Pose.lerp(C, bl.poses[tr.name][tgt_t], abs(w))
        return out

    @staticmethod
    def blend_default(bl, tr, w, ctx):
        default = ctx.get("defaults", {}).get(tr.name)
        if default is None:
            return {}
        return {t: Pose.lerp(bl.poses[tr.name][t], default, w) for t in bl.times[tr.name]}

    @staticmethod
    def scale_keys(bl, tr, w, ctx):
        pivot_mode = ctx.get("pivot", "left")
        prev, nxt = bl.neigh[tr.name]
        tgt = bl.times[tr.name]
        if not tgt:
            return {}
        poses = bl.poses[tr.name]
        if pivot_mode == "left":
            pivot = poses[prev] if prev is not None else poses[tgt[0]]
        elif pivot_mode == "right":
            pivot = poses[nxt] if nxt is not None else poses[tgt[-1]]
        else:
            pivot = Pose.average([poses[t] for t in tgt])
        return {t: Pose.lerp(pivot, poses[t], 1.0 + w) for t in tgt}

    @staticmethod
    def push_pull(bl, tr, w, ctx):
        tgt = bl.times[tr.name]
        prev, nxt = bl.neigh[tr.name]
        poses = bl.poses[tr.name]
        if prev is None or nxt is None or not tgt:
            return {}
        out = {}
        for t in tgt:
            u = (t - prev) / float(nxt - prev)
            lin = Pose.lerp(poses[prev], poses[nxt], u)
            out[t] = Pose.lerp(lin, poses[t], 1.0 + w)
        return out

    @staticmethod
    def ease(bl, tr, w, ctx):
        """현재 키 주변 프레임의 타이밍을 부드럽게. 키 사이 프레임에 키를 굽는다."""
        tgt = bl.times[tr.name]
        prev, nxt = bl.neigh[tr.name]
        poses = bl.poses[tr.name]
        if not tgt:
            return {}
        t0 = tgt[0]
        out = {}
        if w < 0 and prev is not None:
            a, b = prev, t0
        elif w > 0 and nxt is not None:
            a, b = t0, nxt
        else:
            return {}
        for f in range(a + 1, b):
            u = (f - a) / float(b - a)
            out[f] = Pose.lerp(poses[a], poses[b], ease_profile(u, w))
        return out

    @staticmethod
    def noise_wave(bl, tr, w, ctx):
        amp_pos = ctx.get("amp_pos", 5.0) * abs(w)
        amp_rot = ctx.get("amp_rot", 10.0) * abs(w)
        rng = core.seeded_random(ctx.get("seed", 7) + hash(tr.name) % 1000)
        tgt = bl.times[tr.name]
        poses = bl.poses[tr.name]
        out = {}
        for i, t in enumerate(tgt):
            if w < 0:
                dp = rt.Point3(*(rng.uniform(-1, 1) * amp_pos for _ in range(3)))
                ang = rng.uniform(-1, 1) * amp_rot
                axis = rt.normalize(rt.Point3(*(rng.uniform(-1, 1) for _ in range(3))))
            else:
                s = math.sin(2.0 * math.pi * i / max(1.0, ctx.get("wave_period", 4.0)))
                dp = rt.Point3(0, 0, s * amp_pos)
                ang = s * amp_rot
                axis = rt.Point3(0, 1, 0)
            p = poses[t]
            out[t] = Pose(p.pos + dp, rt.quat(ang, axis) * p.rot, p.scale)
        return out

    @staticmethod
    def time_offset(bl, tr, w, ctx):
        """키 위치는 그대로 두고 값만 시간축으로 밀어 넣는다."""
        max_shift = ctx.get("max_shift", 10)
        factor = ctx.get("stagger", {}).get(tr.name, 1.0)
        shift = w * max_shift * factor
        dense = bl.dense.get(tr.name)
        out = {}
        for t in bl.times[tr.name]:
            s = _sample_dense(dense, t - shift)
            if s is not None:
                out[t] = s
        return out

    @staticmethod
    def connect_neighbor(bl, tr, w, ctx):
        tgt = bl.times[tr.name]
        prev, nxt = bl.neigh[tr.name]
        poses = bl.poses[tr.name]
        if not tgt:
            return {}
        if w < 0 and prev is not None:
            src, dst = poses[tgt[0]], poses[prev]
        elif w > 0 and nxt is not None:
            src, dst = poses[tgt[-1]], poses[nxt]
        else:
            return {}
        dpos = (dst.pos - src.pos) * abs(w)
        drot = core.slerp(rt.quat(0, 0, 0, 1), dst.rot * rt.inverse(src.rot), abs(w))
        return {t: Pose(poses[t].pos + dpos, drot * poses[t].rot, poses[t].scale) for t in tgt}


OPS = {
    "tween": Ops.tween,
    "blend_neighbor": Ops.blend_neighbor,
    "blend_default": Ops.blend_default,
    "scale_keys": Ops.scale_keys,
    "push_pull": Ops.push_pull,
    "ease": Ops.ease,
    "noise_wave": Ops.noise_wave,
    "time_offset": Ops.time_offset,
    "connect_neighbor": Ops.connect_neighbor,
}

LABELS = {
    "tween": "Tween Machine",
    "blend_neighbor": "Blend to Neighbor",
    "blend_default": "Blend to Default",
    "scale_keys": "Scale Keys",
    "push_pull": "Push / Pull",
    "ease": "Ease",
    "noise_wave": "Noise / Wave",
    "time_offset": "Time Offset",
    "connect_neighbor": "Connect to Neighbour",
}


# ----------------------------------------------------------------- 세션
class SliderSession:
    def __init__(self, op, tracks, mode="current", range_=None, ctx=None):
        self.op = OPS[op]
        self.op_name = op
        self.tracks = tracks
        self.mode = mode
        self.range_ = range_
        self.ctx = dict(ctx or {})
        self.baseline = None
        self.active = False
        self._written = {}

    def _target_times(self):
        if self.mode == "current":
            return [core.current_frame()]
        s, e = self.range_ if self.range_ else core.anim_range()
        return list(range(s, e + 1))

    def begin(self):
        times = self._target_times()
        dense = None
        if self.op_name == "time_offset":
            s, e = (min(times), max(times))
            m = int(self.ctx.get("max_shift", 10))
            dense = (s - m, e + m)
            n = len(self.tracks)
            self.ctx["stagger"] = {tr.name: (1.0 - i / float(n) if self.ctx.get("stagger_on") else 1.0)
                                   for i, tr in enumerate(self.tracks)}
        if self.op_name == "blend_default" and "defaults" not in self.ctx:
            from . import pose as posemod
            self.ctx["defaults"] = {tr.name: posemod.default_pose(tr) for tr in self.tracks}
        rt.theHold.Begin()
        self.baseline = Baseline(self.tracks, times, dense)
        self.active = True
        self._written = {}

    def update(self, w):
        if not self.active:
            return
        w = max(-1.0, min(1.0, float(w)))
        rt.disableSceneRedraw()
        try:
            for tr in self.tracks:
                targets = self.op(self.baseline, tr, w, self.ctx)
                prev_written = self._written.get(tr.name, set())
                # 이전 update 에서 굽혔던 중간 프레임(ease) 중 이번엔 없는 것 복원
                stale = prev_written - set(targets)
                if stale:
                    tr.delete_keys(stale - set(self.baseline.poses[tr.name]))
                for t, p in targets.items():
                    tr.write(t, p, True)
                self._written[tr.name] = set(targets)
        finally:
            rt.enableSceneRedraw()
            rt.redrawViews()

    def end(self):
        if not self.active:
            return
        self.active = False
        rt.theHold.Accept(LABELS.get(self.op_name, self.op_name))

    def cancel(self):
        if not self.active:
            return
        self.active = False
        rt.theHold.Cancel()
        rt.redrawViews()


def apply_once(op, w, tracks=None, mode="current", range_=None, ctx=None):
    """드래그 없이 값 하나로 적용 (핫키/테스트용)."""
    tracks = tracks if tracks is not None else core.tracks_for()
    s = SliderSession(op, tracks, mode, range_, ctx)
    s.begin()
    try:
        s.update(w)
    except Exception:
        s.cancel()
        raise
    s.end()
    return s
