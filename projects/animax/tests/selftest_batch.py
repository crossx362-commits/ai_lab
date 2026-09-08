"""헤드리스 셀프테스트. 실행:
  "C:/Program Files/Autodesk/3ds Max 2025/3dsmaxbatch.exe" tests/selftest_batch.py
결과: tests/selftest_result.txt (마지막 줄 SUMMARY pass/fail 수)
"""
import os
import sys
import traceback

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, ROOT)
for m in [k for k in sys.modules if k.startswith("animax")]:
    del sys.modules[m]

import pymxs
from pymxs import runtime as rt
from animax import core, sliders, keys, pose, spacify, arc, review
from animax.core import Track, Pose, N

OUT = []
RESULTS = []


def log(*a):
    OUT.append(" ".join(str(x) for x in a))


def check(name, cond, detail=""):
    RESULTS.append((name, bool(cond)))
    log(("PASS" if cond else "FAIL"), name, detail)


def close(a, b, tol=1e-3):
    return abs(float(a) - float(b)) <= tol


def pose_close(p, q, ptol=0.05, rtol=0.5):
    d, ang = p.distance(q)
    return d <= ptol and ang <= rtol


def rot_close(p, q, rtol=0.5):
    """Biped FK 파트는 위치가 계층에 종속이라 회전만 비교."""
    return p.distance(q)[1] <= rtol


def setup_scene():
    rt.resetMaxFile(N("noPrompt"))
    rt.animationRange = rt.interval(0, 60)
    bip = rt.biped.createNew(100, -90, rt.Point3(0, 0, 0))
    box = rt.box(name="Box_L")
    box2 = rt.box(name="Box_R")
    box2.pos = rt.Point3(-30, 0, 0)
    box.pos = rt.Point3(30, 0, 0)
    return bip, box, box2


def key_rot(node, t, deg_z):
    tr = Track(node)
    p = tr.sample(t)
    tr.write(t, Pose(p.pos, rt.quat(rt.eulerAngles(0, 0, deg_z)) * p.rot, p.scale), True)


def run():
    bip, box, box2 = setup_scene()
    lth = rt.getNodeByName("Bip001 L Thigh")
    rth = rt.getNodeByName("Bip001 R Thigh")
    larm = rt.getNodeByName("Bip001 L UpperArm")

    # ---------------- core
    tr = Track(lth)
    check("core.is_biped", core.is_biped(lth) and not core.is_biped(box))
    check("core.biped_root", core.biped_root(lth).name == "Bip001")
    base = tr.sample(0)
    q10 = rt.quat(rt.eulerAngles(0, 0, 40)) * base.rot
    q30 = rt.quat(rt.eulerAngles(0, 0, -40)) * base.rot
    tr.write(10, Pose(base.pos, q10), True)
    tr.write(30, Pose(base.pos, q30), True)
    check("core.key_times biped", tr.key_times() == [10, 30], str(tr.key_times()))
    check("core.neighbors", tr.neighbors(20) == (10, 30))
    s10 = tr.sample(10)
    check("core.write/sample roundtrip", pose_close(s10, Pose(base.pos, q10)), str(s10.distance(Pose(base.pos, q10))))
    # 일반 노드
    tb = Track(box)
    tb.write(0, Pose(rt.Point3(30, 0, 0), rt.quat(0, 0, 0, 1)), True)
    tb.write(20, Pose(rt.Point3(80, 0, 0), rt.quat(rt.eulerAngles(0, 0, 90))), True)
    check("core.key_times std", tb.key_times() == [0, 20], str(tb.key_times()))
    mid = tb.sample(10)
    check("core.std interp pos", close(mid.pos.x, 55, 3.0), str(mid.pos))
    check("core.frame_of", core.frame_of(rt.getKeyTime(tb.controllers()[0], 2)) == 20)
    # COM
    tcom = Track(bip)
    p0 = tcom.sample(0)
    tcom.write(15, Pose(p0.pos + rt.Point3(20, 0, 10), p0.rot), True)
    check("core.COM keys", 15 in tcom.key_times(), str(tcom.key_times()))
    check("core.COM pos", close(tcom.sample(15).pos.x, p0.pos.x + 20, 0.5), str(tcom.sample(15).pos))

    # ---------------- sliders (biped)
    rt.sliderTime = 20
    rt.select([lth])
    sliders.apply_once("tween", -1.0)
    t20 = tr.sample(20)
    check("slider.tween w=-1 == prev key", pose_close(t20, tr.sample(10)), str(t20.distance(tr.sample(10))))
    check("slider.tween creates key", 20 in tr.key_times())
    sliders.apply_once("tween", 1.0)
    check("slider.tween w=+1 == next key", pose_close(tr.sample(20), tr.sample(30)))
    sliders.apply_once("tween", 0.0)
    midp = Pose.lerp(tr.sample(10), tr.sample(30), 0.5)
    midq = midp.rot
    check("slider.tween w=0 == midpoint", rot_close(tr.sample(20), midp), str(tr.sample(20).distance(midp)))
    # negative control: 키 없는 노드에는 아무 것도 안 함
    rt.select([larm])
    n_before = Track(larm).key_times()
    sliders.apply_once("tween", 0.5)
    check("slider.tween no-op without keys (negative)", Track(larm).key_times() == n_before, str(Track(larm).key_times()))
    # blend neighbor
    rt.select([lth])
    before = tr.sample(20)
    sliders.apply_once("blend_neighbor", 1.0)
    check("slider.blend_neighbor +1 == next", pose_close(tr.sample(20), tr.sample(30)))
    sliders.apply_once("blend_neighbor", -1.0)
    check("slider.blend_neighbor -1 == prev", pose_close(tr.sample(20), tr.sample(10)))
    # scale keys (range) with left pivot: w=-1 -> collapse to pivot(prev key)
    tr.write(20, Pose(base.pos, midq), True)
    sliders.apply_once("scale_keys", -1.0, mode="range", range_=(15, 25), ctx={"pivot": "left"})
    check("slider.scale_keys -1 collapses to left pivot", pose_close(tr.sample(20), tr.sample(10)))
    # push_pull -1 -> linear between neighbors
    tr.write(20, Pose(base.pos, q10), True)  # 이전 키와 같은 값 -> 비선형
    sliders.apply_once("push_pull", -1.0, mode="range", range_=(15, 25))
    check("slider.push_pull -1 == linear", rot_close(tr.sample(20), midp), str(tr.sample(20).distance(midp)))
    # ease: 중간 프레임에 키가 구워지고, w<0 (ease in) 은 도착 직전 느림 => 25f 가 이미 목표에 가까움
    rt.sliderTime = 30
    sliders.apply_once("ease", -1.0)
    ks = tr.key_times()
    check("slider.ease bakes in-between keys", all(f in ks for f in range(21, 30)), str(ks))
    d25 = tr.sample(25).distance(tr.sample(30))[1]
    d21 = tr.sample(21).distance(tr.sample(30))[1]
    check("slider.ease -1 approaches target early", d25 < d21 * 0.5, "%.2f vs %.2f" % (d25, d21))
    # time offset with dense sampling
    rt.select([box])
    x_before = tb.sample(20).pos.x
    sliders.apply_once("time_offset", 0.5, mode="range", range_=(0, 20), ctx={"max_shift": 10})
    check("slider.time_offset shifts values", not close(tb.sample(20).pos.x, x_before, 1.0), "%.1f -> %.1f" % (x_before, tb.sample(20).pos.x))
    check("slider.time_offset keeps key times", tb.key_times() == [0, 20], str(tb.key_times()))
    # undo: 배치 모드는 생성까지 한 덩어리로 묶여 검증 불가 -> 인터랙티브에서 확인 (tests/README)
    tb.write(20, Pose(rt.Point3(80, 0, 0), rt.quat(rt.eulerAngles(0, 0, 90))), True)
    # noise
    rt.select([lth])
    before = tr.sample(20)
    sliders.apply_once("noise_wave", -0.5, mode="range", range_=(0, 60), ctx={"amp_rot": 20})
    check("slider.noise changes pose", not pose_close(tr.sample(20), before))
    tr.write(20, before, True)
    # blend to default (biped: stored default)
    pose.store_default([tr], t=0)
    sliders.apply_once("blend_default", 1.0)
    check("slider.blend_default -> stored default", rot_close(tr.sample(20), base), str(tr.sample(20).distance(base)))
    tr.write(20, before, True)

    # ---------------- keys
    rt.select([lth, larm])
    keys.share_keys([tr, Track(larm)])
    check("keys.share_keys", set(Track(larm).key_times()) >= set(tr.key_times()), str(Track(larm).key_times()))
    kt_before = tr.key_times()
    keys.nudge([tr], 3, times={30})
    check("keys.nudge moves only that key", 33 in tr.key_times() and 30 not in tr.key_times() and 10 in tr.key_times(), str(tr.key_times()))
    keys.nudge([tr], -3, times={33})
    check("keys.nudge back", tr.key_times() == kt_before)
    # delete redundant: 같은 값 3연속 -> 가운데 삭제, 다른 값은 유지(negative)
    t2 = Track(box2)
    P = Pose(rt.Point3(-30, 0, 0), rt.quat(0, 0, 0, 1))
    for f in (0, 10, 20):
        t2.write(f, P, True)
    t2.write(30, Pose(rt.Point3(-60, 0, 0), rt.quat(0, 0, 0, 1)), True)
    removed = keys.delete_redundant([t2])
    check("keys.delete_redundant removes static", t2.key_times() == [0, 20, 30], str(t2.key_times()) + str(removed))
    # crop
    keys.crop([tr], (12, 28))
    check("keys.crop", tr.key_times() and min(tr.key_times()) >= 12 and max(tr.key_times()) <= 28, str(tr.key_times()))
    # bake + keys time restore (ease 가 남긴 21~27 중간키는 먼저 정리)
    tr.delete_keys(set(range(21, 28)))
    check("keys.delete_keys consecutive", tr.key_times() == [12, 20, 28], str(tr.key_times()))
    keys.save_keys_time([tr])
    n = keys.bake([tr], step=1, range_=(12, 28))
    check("keys.bake per frame", len(tr.key_times()) == 17, str(len(tr.key_times())))
    keys.restore_keys_time([tr])
    check("keys.restore_keys_time", tr.key_times() == [12, 20, 28], str(tr.key_times()))
    # smooth: 지터 넣고 줄어드는지
    for f, z in ((40, 5), (42, -5), (44, 5), (46, -5), (48, 5)):
        key_rot(box2, f, z)
    before_ang = Track(box2).sample(42).distance(Track(box2).sample(40))[1]
    keys.smooth([Track(box2)], iterations=2, strength=0.7, range_=(40, 48))
    after_ang = Track(box2).sample(42).distance(Track(box2).sample(40))[1]
    check("keys.smooth reduces jitter", after_ang < before_ang * 0.8, "%.2f -> %.2f" % (before_ang, after_ang))
    # tangents
    ch = keys.set_tangents([Track(box)], "step")
    k = rt.getKey(Track(box).float_controllers()[0], 1)
    check("keys.set_tangents step", str(k.outTangentType) == "step", str(k.outTangentType))
    ch = keys.set_tangents([tr], "linear")
    check("keys.set_tangents biped tcb", ch > 0 and close(rt.biped.getKey(lth.controller, 1).continuity, 0.0), str(ch))

    # ---------------- pose
    check("pose.opposite_name biped", pose.opposite_name("Bip001 L Thigh") == "Bip001 R Thigh", str(pose.opposite_name("Bip001 L Thigh")))
    check("pose.opposite_name std", pose.opposite_name("Box_L") == "Box_R" and pose.opposite_name("ctrl_left_hand") == "ctrl_right_hand" and pose.opposite_name("Bip001 Spine") is None)
    rt.select([lth])
    sel = pose.select_opposite()
    check("pose.select_opposite", [n.name for n in sel] == ["Bip001 R Thigh"])
    # mirror biped: L 에 회전 키 후 미러 -> R 이 바뀌어야 함
    rt.sliderTime = 20
    tr.write(20, Pose(base.pos, q10), True)
    r_before = Track(rth).sample(20)
    l_before = tr.sample(20)
    cnt = pose.mirror([lth])
    r_after = Track(rth).sample(20)
    check("pose.mirror biped changes R", cnt == 1 and not pose_close(r_after, r_before), str(r_after.distance(r_before)))
    check("pose.mirror biped keys R", 20 in Track(rth).key_times(), str(Track(rth).key_times()))
    # 두 번 미러하면 원래대로
    pose.mirror([lth])
    check("pose.mirror twice == identity", rot_close(tr.sample(20), l_before) and rot_close(Track(rth).sample(20), r_before))
    # mirror std
    tb.write(20, Pose(rt.Point3(80, 0, 0), rt.quat(rt.eulerAngles(0, 0, 90))), True)
    pose.mirror([box])
    p = t2.sample(20)
    check("pose.mirror std pos", close(p.pos.x, -80, 0.5), str(p.pos))
    # reset pose std
    pose.reset_pose([t2])
    check("pose.reset_pose std -> identity", pose_close(t2.sample(20), Pose(rt.Point3(0, 0, 0), rt.quat(0, 0, 0, 1))), str(t2.sample(20).pos))
    # align
    rt.select([box2, box])
    pose.align()
    check("pose.align", pose_close(t2.sample(20), tb.sample(20)))
    # xform copy/paste
    pose.xform_copy([tb], (0, 20))
    pose.xform_paste([t2], at=40)
    check("pose.xform_paste offset", close(t2.sample(60).pos.x, tb.sample(20).pos.x, 0.5) and 40 in t2.key_times(), str(t2.key_times()))
    # global offset
    go = pose.GlobalOffset([tb])
    tb.write(20, Pose(tb.sample(20).pos + rt.Point3(0, 0, 50), tb.sample(20).rot), True)
    go.apply((0, 20))
    check("pose.global_offset", close(tb.sample(0).pos.z, 50, 0.5), str(tb.sample(0).pos))
    # relationship
    rel = pose.Relationship(box, [box2], t=20)
    rel.bake((0, 20))
    d0 = rt.distance(tb.sample(0).pos, t2.sample(0).pos)
    d20 = rt.distance(tb.sample(20).pos, t2.sample(20).pos)
    check("pose.relationship keeps offset", close(d0, d20, 0.5), "%.2f vs %.2f" % (d0, d20))
    # transfer json
    path = os.path.join(HERE, "_tmp_anim.json")
    pose.export_animation([tr], path, (12, 28))
    sample_before = tr.sample(20)
    tr.delete_keys(None)
    n = pose.import_animation(path)
    check("pose.export/import json", n == 1 and rot_close(tr.sample(20), sample_before), str(tr.key_times()))
    os.remove(path)

    # ---------------- spacify
    rt.select([lth])
    kt = tr.key_times()
    sess = spacify.create("world", [lth], range_=(0, 30))
    check("spacify.create helper", len(sess.helpers) == 1 and rt.isValidNode(sess.helpers[0]))
    h = sess.helpers[0]
    with pymxs.attime(20):
        check("spacify.helper matches part", pose_close(Pose.from_matrix(h.transform), tr.sample(20)))
    # 헬퍼를 옮기고 apply -> 파트가 따라와야 함
    with pymxs.animate(True):
        with pymxs.attime(20):
            h.transform = rt.rotate(rt.matrix3(1), rt.quat(rt.eulerAngles(0, 25, 0))) * h.transform
    target = None
    with pymxs.attime(20):
        target = Pose.from_matrix(h.transform)
    sess.apply(restore_keys=True)
    check("spacify.apply moves part", rot_close(tr.sample(20), target), str(tr.sample(20).distance(target)))
    check("spacify.apply restore keys", tr.key_times() == kt, str(tr.key_times()))
    check("spacify.cleanup", not any(n.name.startswith("AX_") for n in rt.objects))
    # pivot mode
    sess = spacify.create("pivot", [lth, larm], range_=(0, 30), pivot_pos=rt.Point3(0, 0, 0))
    check("spacify.pivot parent", sess.parent is not None and all(h.parent == sess.parent for h in sess.helpers))
    sess.discard()
    check("spacify.discard", not any(n.name.startswith("AX") for n in rt.objects))
    # relative mode (driver=box)
    sess = spacify.create("relative", [lth, box], range_=(0, 30))
    check("spacify.relative driver", sess.driver == box and len(sess.helpers) == 1)
    sess.discard()

    # ---------------- arc
    names = arc.start([lth], before=5, after=5)
    sh = rt.getNodeByName(arc.PREFIX + lth.name)
    check("arc.start spline", sh is not None and rt.numKnots(sh, 1) == 11, str(rt.numKnots(sh, 1) if sh else None))
    arc.stop()
    check("arc.stop cleans", rt.getNodeByName(arc.PREFIX + lth.name) is None)

    # ---------------- review
    sets = review.add_set("legs", [lth, rth], "#ff0000", sets={})
    rt.select([])
    review.select_set("legs", sets=sets)
    check("review.selection_set", sorted(n.name for n in rt.selection) == ["Bip001 L Thigh", "Bip001 R Thigh"])
    p = review.quick_export([box], os.path.join(HERE, "_tmp_export.max"))
    rt.delete(box)
    new = review.quick_import(p)
    check("review.quick_export/import", any(n.name == "Box_L" for n in new), str([n.name for n in new]))
    os.remove(p)
    cam = rt.Freecamera(name="Cam_A")
    cam.pos = rt.Point3(0, -300, 100)
    try:
        res = review.playblast([cam], out_dir=os.path.join(HERE, "_tmp_pb"), range_=(0, 5), percent=25)
        ok = bool(res) and os.path.exists(res[0][1])
        check("review.playblast", ok, str(res))
    except Exception as e:
        check("review.playblast", False, str(e)[:200])


try:
    run()
except Exception:
    log("TRACEBACK", traceback.format_exc())
    RESULTS.append(("__crash__", False))

npass = sum(1 for _, ok in RESULTS if ok)
nfail = sum(1 for _, ok in RESULTS if not ok)
log("SUMMARY pass=%d fail=%d" % (npass, nfail))
with open(os.path.join(HERE, "selftest_result.txt"), "w", encoding="utf-8") as f:
    f.write("\n".join(OUT))
