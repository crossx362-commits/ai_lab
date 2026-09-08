"""리뷰/전송: Multi-Camera Playblast, Quick Export/Import(objects), Selection Sets.
"""
import json
import os
import re
from pymxs import runtime as rt

from . import core
from .core import N


# ----------------------------------------------------------------- Playblast
def _safe(name):
    return re.sub(r"[^\w\-]+", "_", name)


def playblast(cameras=None, out_dir=None, range_=None, percent=100, fps=None, frame_nums=True, prefix=None):
    """카메라마다 프리뷰 AVI 를 만든다. 반환: [(camera_name, path)]."""
    cams = cameras if cameras else [c for c in rt.cameras if rt.superClassOf(c) == rt.camera]
    if not cams:
        raise ValueError("씬에 카메라가 없다")
    s, e = range_ if range_ else core.anim_range()
    out_dir = out_dir or os.path.join(str(rt.getDir(N("preview"))), "animax")
    os.makedirs(out_dir, exist_ok=True)
    scene = os.path.splitext(str(rt.maxFileName) or "untitled")[0]
    prefix = prefix or scene
    fps = fps or int(rt.frameRate)
    results = []
    view = rt.viewport.activeViewport
    for cam in cams:
        rt.viewport.setCamera(cam)
        rt.redrawViews()
        path = os.path.join(out_dir, "%s_%s.avi" % (_safe(prefix), _safe(cam.name)))
        try:
            rt.createPreview(outputAVI=True, filename=path, percentSize=percent, start=s, end=e, skip=1,
                             fps=fps, dspGeometry=True, dspShapes=False, dspLights=False, dspCameras=False,
                             dspHelpers=False, dspGrid=False, dspFrameNums=frame_nums, dspSafeFrames=True,
                             rndLevel=N("smoothhighlights"), autoPlay=False)
        except Exception:
            # 구버전 시그니처(파일명 미지원) 폴백: 기본 _scene.avi 가 preview 폴더에 생김
            rt.createPreview(outputAVI=True, percentSize=percent, start=s, end=e, skip=1, fps=fps,
                             dspGeometry=True, dspFrameNums=frame_nums, rndLevel=N("smoothhighlights"))
            default = os.path.join(str(rt.getDir(N("preview"))), "_scene.avi")
            if os.path.exists(default):
                if os.path.exists(path):
                    os.remove(path)
                os.replace(default, path)
        results.append((cam.name, path))
    try:
        rt.viewport.activeViewport = view
    except Exception:
        pass
    return results


# ----------------------------------------------------------------- Quick Export / Import
def quick_export(nodes=None, path=None):
    nodes = core.selected_nodes() if nodes is None else nodes
    if not nodes:
        raise ValueError("선택된 오브젝트가 없다")
    path = path or os.path.join(str(rt.getDir(N("export"))), "animax_quick_export.max")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    ok = rt.saveNodes(nodes, path, quiet=True)
    return path if ok else None


def quick_import(path=None):
    path = path or os.path.join(str(rt.getDir(N("export"))), "animax_quick_export.max")
    if not os.path.exists(path):
        raise FileNotFoundError(path)
    before = {n.name for n in rt.objects}
    rt.mergeMAXFile(path, N("select"), N("autoRenameDups"), N("useSceneMtlDups"), quiet=True)
    return [n for n in rt.objects if n.name not in before]


# ----------------------------------------------------------------- Selection Sets (색상·내보내기)
def _sets_path():
    return os.path.join(str(rt.getDir(N("userScripts"))), "animax_selection_sets.json")


def load_sets(path=None):
    p = path or _sets_path()
    if not os.path.exists(p):
        return {}
    with open(p, "r", encoding="utf-8") as f:
        return json.load(f)


def save_sets(sets, path=None):
    p = path or _sets_path()
    os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, "w", encoding="utf-8") as f:
        json.dump(sets, f, ensure_ascii=False, indent=1)
    return p


def add_set(name, nodes=None, color="#4aa3ff", sets=None):
    nodes = core.selected_nodes() if nodes is None else nodes
    sets = load_sets() if sets is None else sets
    sets[name] = {"color": color, "nodes": [n.name for n in nodes]}
    save_sets(sets)
    return sets


def select_set(name, add=False, sets=None):
    sets = load_sets() if sets is None else sets
    entry = sets.get(name)
    if not entry:
        return []
    nodes = [rt.getNodeByName(n) for n in entry["nodes"]]
    nodes = [n for n in nodes if n is not None]
    if add:
        nodes = core.selected_nodes() + [n for n in nodes if n not in core.selected_nodes()]
    rt.select(nodes)
    return nodes


def remove_set(name, sets=None):
    sets = load_sets() if sets is None else sets
    sets.pop(name, None)
    save_sets(sets)
    return sets
