"""
재와 별 (Ashes to Stars) — 플레이스홀더 스프라이트 생성기
Blender 4.1 headless 실행 전용.

    blender --background --python gen_sprites.py

무엇을 만드는가:
  단순 프리미티브(캡슐/원뿔/큐브/구)로 캐릭터·몬스터를 만들고
  쿼터뷰(카메라 45° 회전 / 30° 하강, 직교 투영) 8방향 스프라이트를 렌더한다.
  프로토타입 W1(성능 검증)에는 사실 1방향이면 충분하지만,
  8방향 파이프라인이 실제로 도는지 함께 확인해두면 1단계에서 그대로 쓴다.

출력: ./sprites/<name>_<dir>.png  (투명 배경, 128px)
"""
import bpy, math, os, sys

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sprites")
SIZE = 128
DIRS = 8          # 8방향
CAM_PITCH = 30.0  # 쿼터뷰 하강각
ORTHO = 2.6

# (이름, 몸통 프리미티브, 색, 높이, 폭, 머리 유무)
UNITS = [
    ("player_knight",  "capsule", (0.30, 0.55, 0.95), 1.00, 0.34, True),   # 수호기사 — 파랑
    ("player_mage",    "capsule", (0.65, 0.35, 0.90), 0.95, 0.30, True),   # 마법사 — 보라
    ("player_priest",  "capsule", (0.95, 0.90, 0.55), 0.95, 0.30, True),   # 사제 — 노랑
    ("mob_chaser",     "cone",    (0.85, 0.25, 0.25), 0.55, 0.26, False),  # 추적형 — 빨강
    ("mob_swarmer",    "sphere",  (0.90, 0.50, 0.20), 0.45, 0.24, False),  # 포위형 — 주황
    ("mob_ranged",     "cube",    (0.55, 0.30, 0.75), 0.55, 0.24, False),  # 원거리형 — 자주
    ("elite_healer",   "capsule", (0.30, 0.85, 0.45), 0.80, 0.34, True),   # 정예 주술사 — 초록
    ("boss",           "cube",    (0.15, 0.15, 0.20), 1.60, 0.80, True),   # 보스 — 검정
]


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def mat(name, rgb, emit=0.35):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = 0.75
    # 살짝 자체발광 — 평면 조명에서도 실루엣이 또렷하게 (소켓명이 버전마다 다름)
    for key in ("Emission Color", "Emission"):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = (*rgb, 1)
            break
    if "Emission Strength" in bsdf.inputs:
        bsdf.inputs["Emission Strength"].default_value = emit
    return m


def body(kind, w, h):
    if kind == "capsule":
        bpy.ops.mesh.primitive_cylinder_add(radius=w, depth=h, vertices=16)
        o = bpy.context.object
        bpy.ops.mesh.primitive_uv_sphere_add(radius=w, segments=16, ring_count=8,
                                             location=(0, 0, h / 2))
        top = bpy.context.object
        top.select_set(True); o.select_set(True)
        bpy.context.view_layer.objects.active = o
        bpy.ops.object.join()
        return o
    if kind == "cone":
        bpy.ops.mesh.primitive_cone_add(radius1=w, depth=h, vertices=16)
    elif kind == "sphere":
        bpy.ops.mesh.primitive_uv_sphere_add(radius=w, segments=16, ring_count=10)
    else:  # cube
        bpy.ops.mesh.primitive_cube_add(size=1)
        bpy.context.object.scale = (w, w, h / 2)
    return bpy.context.object


def build(name, kind, rgb, h, w, head):
    reset()
    m = mat(name, rgb)

    b = body(kind, w, h)
    b.location.z = h / 2
    b.data.materials.append(m)

    parts = [b]
    if head:
        bpy.ops.mesh.primitive_uv_sphere_add(radius=w * 0.62, segments=16, ring_count=10,
                                             location=(0, 0, h + w * 0.35))
        hd = bpy.context.object
        hd.data.materials.append(mat(name + "_head", [c * 0.75 + 0.15 for c in rgb]))
        parts.append(hd)

    # 정면 표식 — 방향을 눈으로 구분할 수 있게 (-Y가 정면)
    bpy.ops.mesh.primitive_cone_add(radius1=w * 0.38, depth=w * 0.75,
                                    rotation=(math.radians(90), 0, 0),
                                    location=(0, -w * 0.95, h * 0.62))
    nose = bpy.context.object
    nose.data.materials.append(mat(name + "_nose", (1, 1, 1)))
    parts.append(nose)

    for p in parts:
        bpy.ops.object.select_all(action="DESELECT")
        p.select_set(True)
        bpy.context.view_layer.objects.active = p
        bpy.ops.object.shade_smooth()

    # 회전용 부모 — 이걸 돌려 8방향을 만든다
    bpy.ops.object.empty_add(location=(0, 0, 0))
    pivot = bpy.context.object
    for p in parts:
        p.parent = pivot
    return pivot


def setup_scene():
    sc = bpy.context.scene
    # EEVEE 식별자가 버전마다 다르다 (4.1=BLENDER_EEVEE, 4.2+=BLENDER_EEVEE_NEXT)
    engines = sc.bl_rna.properties["render"].fixed_type.properties["engine"].enum_items.keys() \
        if False else [e.identifier for e in
                       bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    for cand in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "BLENDER_WORKBENCH"):
        if cand in engines:
            sc.render.engine = cand
            break
    sc.render.resolution_x = SIZE
    sc.render.resolution_y = SIZE
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    try:
        sc.eevee.taa_render_samples = 16
    except AttributeError:
        pass

    # 쿼터뷰 카메라 (직교) — 45° 방위 / 30° 하강
    bpy.ops.object.camera_add()
    cam = bpy.context.object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = ORTHO
    d = 6.0
    pitch = math.radians(CAM_PITCH)
    yaw = math.radians(45)
    cam.location = (d * math.cos(pitch) * math.sin(yaw),
                    -d * math.cos(pitch) * math.cos(yaw),
                    d * math.sin(pitch) + 0.5)
    cam.rotation_euler = (math.radians(90 - CAM_PITCH), 0, yaw)
    sc.camera = cam

    # 조명 3점 — 실루엣 위주라 단순하게
    for loc, e in [((4, -4, 6), 900), ((-5, -2, 3), 300), ((0, 5, 4), 250)]:
        bpy.ops.object.light_add(type="AREA", location=loc)
        L = bpy.context.object
        L.data.energy = e
        L.data.size = 5
        L.rotation_euler = (math.radians(50), 0, math.radians(30))
    return cam


def main():
    os.makedirs(OUT, exist_ok=True)
    total = 0
    for name, kind, rgb, h, w, head in UNITS:
        pivot = build(name, kind, rgb, h, w, head)
        setup_scene()
        for i in range(DIRS):
            pivot.rotation_euler = (0, 0, math.radians(360 / DIRS * i))
            bpy.context.scene.render.filepath = os.path.join(OUT, f"{name}_{i}.png")
            bpy.ops.render.render(write_still=True)
            total += 1
        print(f"[gen] {name} — {DIRS}방향 완료")
    print(f"[gen] 총 {total}장 → {OUT}")


main()
