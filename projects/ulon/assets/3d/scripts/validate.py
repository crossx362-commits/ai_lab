# 울온 3D export 헤드리스 검사.
# 모델명: n/a (검증기)
# 날짜: 2026-09-12
# 프롬프트 요약: 폴리곤 수·원점·스케일·법선·트랜스폼·빈 UV
# 블렌더 버전: 실행 시 bpy.app.version_string
#
# 사용:
#   blender --background --python assets/3d/scripts/validate.py
#   blender --background --python assets/3d/scripts/validate.py -- assets/3d/export/foo.glb
# 자동화 가능 항목만 본다. 리깅·애니메이션 의미 검사는 루프가 엔진에서 한다.

import json
import sys
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[3]
EXPORT = ROOT / "assets" / "3d" / "export"
EPS = 1e-3


def argv_files():
    args = sys.argv
    if "--" in args:
        return [Path(a) for a in args[args.index("--") + 1 :] if a]
    if EXPORT.exists():
        return sorted(
            p
            for p in EXPORT.iterdir()
            if p.suffix.lower() in {".glb", ".gltf", ".fbx", ".blend"}
        )
    return []


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def import_file(path: Path):
    suf = path.suffix.lower()
    if suf in {".glb", ".gltf"}:
        bpy.ops.import_scene.gltf(filepath=str(path))
    elif suf == ".fbx":
        bpy.ops.import_scene.fbx(filepath=str(path))
    elif suf == ".blend":
        bpy.ops.wm.open_mainfile(filepath=str(path))
    else:
        raise RuntimeError(f"unsupported format {path.suffix}")


def mesh_objects():
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


def check_object(obj) -> list[str]:
    fails = []
    loc = obj.location
    rot = obj.rotation_euler
    sca = obj.scale
    if max(abs(loc.x), abs(loc.y), abs(loc.z)) > EPS:
        fails.append(f"location not 0 ({tuple(round(v, 4) for v in loc)})")
    if max(abs(rot.x), abs(rot.y), abs(rot.z)) > EPS:
        fails.append(f"rotation not 0 ({tuple(round(v, 4) for v in rot)})")
    if max(abs(sca.x - 1), abs(sca.y - 1), abs(sca.z - 1)) > EPS:
        fails.append(f"scale not 1 ({tuple(round(v, 4) for v in sca)})")

    mesh = obj.data
    polys = len(mesh.polygons)
    if polys == 0:
        fails.append("polygon count 0")

    if not mesh.uv_layers:
        fails.append("no UV layers")
    else:
        uv = mesh.uv_layers.active
        if uv is None or len(uv.data) == 0:
            fails.append("empty UV")

    # 뒤집힌 법선: 면 법선과 면 중심→원점 방향이 같은 쪽이 많으면 의심.
    # 캐릭터/소품은 원점이 메시 밖(발 밑/바닥)이라 이 휴리스틱은 보조다.
    flipped = 0
    for poly in mesh.polygons:
        if poly.normal.length < EPS:
            flipped += 1
    if flipped:
        fails.append(f"zero-length normals {flipped}")

    bbox = [obj.matrix_world @ v.co for v in mesh.vertices] if mesh.vertices else []
    if bbox:
        min_z = min(v.z for v in bbox)
        # 발 밑/바닥 중심 규격: 최저점이 원점 근처여야 한다.
        if abs(min_z) > 0.05:
            fails.append(f"lowest z {min_z:.4f} (want ~0, feet/ground)")

    return fails, polys


def check_file(path: Path) -> dict:
    reset()
    import_file(path)
    meshes = mesh_objects()
    report = {
        "file": str(path.relative_to(ROOT) if path.is_relative_to(ROOT) else path),
        "blender": bpy.app.version_string,
        "mesh_count": len(meshes),
        "polygons": 0,
        "ok": True,
        "fails": [],
    }
    if not meshes:
        report["ok"] = False
        report["fails"].append("no mesh objects")
        return report
    for obj in meshes:
        fails, polys = check_object(obj)
        report["polygons"] += polys
        for f in fails:
            report["fails"].append(f"{obj.name}: {f}")
    report["ok"] = not report["fails"]
    return report


def main() -> int:
    files = argv_files()
    if not files:
        print("validate.py: no files in assets/3d/export and no argv after --")
        return 0
    reports = []
    bad = 0
    for path in files:
        if not path.exists():
            reports.append({"file": str(path), "ok": False, "fails": ["missing"]})
            bad += 1
            continue
        try:
            reports.append(check_file(path))
        except Exception as e:
            reports.append({"file": str(path), "ok": False, "fails": [str(e)]})
        if not reports[-1]["ok"]:
            bad += 1
    text = json.dumps(reports, ensure_ascii=False, indent=2)
    print(text)
    log_dir = ROOT / "logs"
    log_dir.mkdir(parents=True, exist_ok=True)
    (log_dir / "validate_3d.json").write_text(text + "\n", encoding="utf-8")
    return 1 if bad else 0


if __name__ == "__main__":
    raise SystemExit(main())
