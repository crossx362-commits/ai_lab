"""Orch 검증 장치 — Blender 배치 판정 (수정 금지, protected_globs).

    blender -b --factory-startup --python orch_check.py -- --report <json>

하는 일:
  1) 빈 씬(팩토리)에서 이 저장소의 build.py를 읽어 build()를 부른다.
  2) 씬에 실제로 메시가 생겼는지 잰다 — "돌아갔다"가 아니라 "만들었다"를 본다.
  3) tests/test_*.py 의 test_* 함수를 전부 부른다(AI가 쓴 테스트, 지우면 게이트가 잡는다).
  4) 결과를 JSON으로 쓰고 stdout에 마커를 찍는다: ORCH_BLENDER_OK / ORCH_BLENDER_FAIL
마커가 없으면 오케스트레이터는 UNKNOWN으로 본다 — 초록을 지어내지 않는다.
"""
import importlib.util
import json
import os
import sys
import traceback

import bpy

ROOT = os.path.dirname(os.path.abspath(__file__))
OK, FAIL = "ORCH_BLENDER_OK", "ORCH_BLENDER_FAIL"


def _arg(name, default=None):
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return argv[argv.index(name) + 1] if name in argv and argv.index(name) + 1 < len(argv) else default


def _load(path, modname):
    spec = importlib.util.spec_from_file_location(modname, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def main():
    report_path = _arg("--report")
    rep = {"verdict": "FAILED", "errors": [], "meshes": 0, "polys": 0, "tests": 0, "failures": []}
    try:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        build_path = os.path.join(ROOT, "build.py")
        if not os.path.isfile(build_path):
            raise RuntimeError("build.py 가 없다")
        mod = _load(build_path, "orch_build")
        if not hasattr(mod, "build"):
            raise RuntimeError("build.py 에 build() 가 없다")
        mod.build()

        meshes = [o for o in bpy.data.objects if o.type == "MESH"]
        rep["meshes"] = len(meshes)
        rep["polys"] = sum(len(o.data.polygons) for o in meshes)
        if not meshes:
            rep["errors"].append("메시 오브젝트가 하나도 없다 — 아무것도 만들지 않았다")
        if meshes and rep["polys"] == 0:
            rep["errors"].append("메시는 있는데 면이 0개다")
        for o in meshes:
            if any(abs(c) > 1000 for c in o.location):
                rep["errors"].append(f"{o.name} 위치가 1000m 밖이다: {tuple(round(c, 1) for c in o.location)}")

        tests_dir = os.path.join(ROOT, "tests")
        if os.path.isdir(tests_dir):
            for fn in sorted(os.listdir(tests_dir)):
                if not (fn.startswith("test_") and fn.endswith(".py")):
                    continue
                tm = _load(os.path.join(tests_dir, fn), "orch_" + fn[:-3])
                for name in dir(tm):
                    if name.startswith("test_") and callable(getattr(tm, name)):
                        rep["tests"] += 1
                        try:
                            getattr(tm, name)()
                        except Exception as e:  # noqa: BLE001 — 실패 사유를 전부 보고한다
                            rep["failures"].append(f"{fn}::{name}: {type(e).__name__}: {e}")
        if rep["failures"]:
            rep["errors"].append(f"테스트 {len(rep['failures'])}건 실패")
        if not rep["errors"]:
            rep["verdict"] = "PASS"
    except Exception:  # noqa: BLE001
        rep["errors"].append(traceback.format_exc().strip().splitlines()[-1])
        rep["traceback"] = traceback.format_exc()

    if report_path:
        with open(report_path, "w", encoding="utf-8") as f:
            json.dump(rep, f, ensure_ascii=False, indent=2)
    print(OK if rep["verdict"] == "PASS" else FAIL, flush=True)
    for e in rep["errors"] + rep["failures"]:
        print("  " + e, flush=True)
    sys.exit(0 if rep["verdict"] == "PASS" else 1)


main()
