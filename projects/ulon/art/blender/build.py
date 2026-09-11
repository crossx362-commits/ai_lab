"""울온 소품 — Blender로 만드는 CC0 대체 불가 물건(절구통·모루·화덕 등, docs/ASSET_WANTLIST.md 「없는 물건」).

build()는 **빈 씬**에서 불린다. bpy로 오브젝트를 만들고 이름은 WANTLIST의 자리 이름을 따른다
(예: Mortar, Anvil, Forge). 파일 저장은 하지 않는다 — 검증 장치(orch_check.py)가 씬을 직접 잰다.
크기 기준은 GAME_DESIGN §6.1(사람 키 1.8m): 절구통 높이 0.6~0.9m, 모루 높이 0.6~0.8m.
"""
import bpy


def build():
    # 첫 판: 자리표시 절구통(원통 몸통 + 얕은 홈). 다음 판이 이것을 진짜 형태로 키운다.
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=0.28, depth=0.7, location=(0.0, 0.0, 0.35))
    body = bpy.context.active_object
    body.name = "Mortar"
