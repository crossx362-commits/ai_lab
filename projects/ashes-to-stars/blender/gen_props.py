"""
재와 별 — 배경 프랍 생성기 (블렌더 3D → 픽셀아트 후처리)

    blender --background --factory-startup --python gen_props.py

왜 프랍만 블렌더인가:
  캐릭터는 픽셀아트로 손수 그린다(오너 기준 시트). 3D 렌더를 축소하면
  프레임마다 도트가 미묘하게 달라져 애니메이션이 지글거리기 때문이다.
  **프랍은 정지 상태라 그 문제가 없다.** 그리고 바위·덤불·잔해를
  바이옴 10티어 × 변형 여러 개씩 손으로 그리면 물량이 폭발한다.
  → 정지 + 대량 + 시선의 중심이 아님 = 절차 생성이 정확히 맞는 영역.

픽셀아트로 보이게 하는 후처리 3단계 (이게 없으면 3D 티가 난다):
  ① 저해상도로 렌더 (64~96px) — 애초에 도트 크기로 뽑는다
  ② 팔레트 양자화(posterize) — 색 단계를 8~12개로 줄여 그라데이션을 없앤다
  ③ 1px 외곽선 — 픽셀아트 특유의 윤곽을 넣어 배경에서 분리되게 한다

출력: ./out_props/<biome>_<name>_<variant>.png
"""
import bpy, bmesh, math, os, random

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out_props")
SIZE = 96            # 렌더 해상도 = 도트 해상도
CAM_PITCH = 30.0     # 쿼터뷰 하강각 — 캐릭터 스프라이트와 같아야 한다
PALETTE_STEPS = 6    # 채널당 색 단계 (낮을수록 픽셀아트 느낌)
OUTLINE = (0.04, 0.03, 0.05, 1.0)

# (바이옴, 프랍명, 종류, 기본색, 변형 수)
PROPS = [
    ("field",   "rock",    "rock",    (0.42, 0.42, 0.45), 3),
    ("field",   "bush",    "bush",    (0.20, 0.34, 0.18), 3),
    ("field",   "stump",   "stump",   (0.32, 0.23, 0.14), 2),
    ("ash",     "charred", "stump",   (0.16, 0.14, 0.14), 3),  # 불탄 그루터기 (세계관: 한 번 불탄 땅)
    ("ash",     "bone",    "spike",   (0.72, 0.70, 0.62), 2),
    ("dungeon", "pillar",  "pillar",  (0.34, 0.33, 0.36), 3),
    ("dungeon", "crystal", "spike",   (0.35, 0.55, 0.75), 3),
    ("dungeon", "rubble",  "rock",    (0.28, 0.27, 0.29), 3),
    ("estate",  "crate",   "crate",   (0.40, 0.29, 0.17), 2),
    ("estate",  "barrel",  "barrel",  (0.36, 0.26, 0.16), 2),
]


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def rough_mat(name, rgb):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = 0.9
    return m


def jitter_mesh(obj, amount, seed):
    """정점을 흔들어 프리미티브 티를 없앤다 — 같은 큐브도 변형마다 달라 보이게"""
    rnd = random.Random(seed)
    me = obj.data
    bm = bmesh.new()
    bm.from_mesh(me)
    for v in bm.verts:
        v.co.x += (rnd.random() - 0.5) * amount
        v.co.y += (rnd.random() - 0.5) * amount
        v.co.z += (rnd.random() - 0.5) * amount * 0.6
    bm.to_mesh(me)
    bm.free()
    me.update()


def build(kind, rgb, seed):
    rnd = random.Random(seed)
    parts = []

    if kind == "rock":
        for i in range(rnd.randint(2, 4)):
            bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,
                                                  radius=rnd.uniform(0.25, 0.5),
                                                  location=(rnd.uniform(-.3, .3), rnd.uniform(-.3, .3),
                                                            rnd.uniform(0.15, 0.4)))
            o = bpy.context.object
            jitter_mesh(o, 0.18, seed + i)
            parts.append(o)

    elif kind == "bush":
        for i in range(rnd.randint(3, 5)):
            bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,
                                                  radius=rnd.uniform(0.22, 0.38),
                                                  location=(rnd.uniform(-.35, .35), rnd.uniform(-.35, .35),
                                                            rnd.uniform(0.2, 0.5)))
            o = bpy.context.object
            jitter_mesh(o, 0.22, seed + i)
            parts.append(o)

    elif kind == "stump":
        bpy.ops.mesh.primitive_cylinder_add(radius=0.34, depth=0.75, vertices=8, location=(0, 0, 0.37))
        o = bpy.context.object
        jitter_mesh(o, 0.08, seed)
        parts.append(o)

    elif kind == "spike":
        for i in range(rnd.randint(2, 3)):
            bpy.ops.mesh.primitive_cone_add(radius1=rnd.uniform(0.14, 0.22),
                                            depth=rnd.uniform(0.7, 1.2), vertices=6,
                                            location=(rnd.uniform(-.25, .25), rnd.uniform(-.25, .25), 0.5),
                                            rotation=(rnd.uniform(-.25, .25), rnd.uniform(-.25, .25), 0))
            parts.append(bpy.context.object)

    elif kind == "pillar":
        bpy.ops.mesh.primitive_cylinder_add(radius=0.3, depth=rnd.uniform(1.2, 1.9), vertices=8,
                                            location=(0, 0, 0.8))
        o = bpy.context.object
        jitter_mesh(o, 0.06, seed)
        parts.append(o)

    elif kind == "crate":
        bpy.ops.mesh.primitive_cube_add(size=0.8, location=(0, 0, 0.4),
                                        rotation=(0, 0, rnd.uniform(0, 0.6)))
        parts.append(bpy.context.object)

    else:  # barrel
        bpy.ops.mesh.primitive_cylinder_add(radius=0.32, depth=0.85, vertices=10, location=(0, 0, 0.43))
        parts.append(bpy.context.object)

    mat = rough_mat("prop", rgb)
    for p in parts:
        p.data.materials.append(mat)
        bpy.ops.object.select_all(action="DESELECT")
        p.select_set(True)
        bpy.context.view_layer.objects.active = p
        bpy.ops.object.shade_flat()      # 스무스 셰이딩은 그라데이션을 만들어 도트를 흐린다
    return parts


def setup_scene():
    sc = bpy.context.scene
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    for c in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "BLENDER_WORKBENCH"):
        if c in engines:
            sc.render.engine = c
            break
    sc.render.resolution_x = SIZE
    sc.render.resolution_y = SIZE
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    sc.render.filter_size = 0.0          # 안티에일리어싱 끄기 — 도트 경계가 흐려지면 안 된다
    try:
        sc.eevee.taa_render_samples = 4
    except AttributeError:
        pass
    sc.view_settings.view_transform = "Standard"

    bpy.ops.object.camera_add()
    cam = bpy.context.object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.4
    d, pitch, yaw = 6.0, math.radians(CAM_PITCH), math.radians(45)
    cam.location = (d * math.cos(pitch) * math.sin(yaw),
                    -d * math.cos(pitch) * math.cos(yaw),
                    d * math.sin(pitch) + 0.4)
    cam.rotation_euler = (math.radians(90 - CAM_PITCH), 0, yaw)
    sc.camera = cam

    for loc, e in [((3, -3, 5), 700), ((-4, -1, 3), 220)]:
        bpy.ops.object.light_add(type="AREA", location=loc)
        L = bpy.context.object
        L.data.energy = e
        L.data.size = 6


def posterize_and_outline(path):
    """
    렌더 결과를 픽셀아트로 후처리.
    이 단계가 없으면 아무리 저해상도로 뽑아도 3D 렌더 티가 난다.
    """
    img = bpy.data.images.load(path)
    w, h = img.size
    px = list(img.pixels)          # RGBA float, 아래에서 위로

    def idx(x, y): return (y * w + x) * 4

    # ① 팔레트 양자화 — 색 단계를 줄여 그라데이션 제거
    step = 1.0 / (PALETTE_STEPS - 1)
    for i in range(0, len(px), 4):
        if px[i + 3] < 0.5:
            px[i + 3] = 0.0        # 반투명 가장자리 제거 — 픽셀아트는 알파가 0 아니면 1
            continue
        px[i + 3] = 1.0
        for c in range(3):
            px[i + c] = round(px[i + c] / step) * step

    # ② 1px 외곽선 — 불투명 픽셀 중 투명과 맞닿은 것을 어둡게
    edge = []
    for y in range(h):
        for x in range(w):
            i = idx(x, y)
            if px[i + 3] < 0.5:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if nx < 0 or ny < 0 or nx >= w or ny >= h or px[idx(nx, ny) + 3] < 0.5:
                    edge.append(i)
                    break
    for i in edge:
        px[i] = OUTLINE[0]; px[i + 1] = OUTLINE[1]; px[i + 2] = OUTLINE[2]

    img.pixels = px
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()

    opaque = sum(1 for i in range(3, len(px), 4) if px[i] > 0.5)
    bpy.data.images.remove(img)
    return opaque, len(edge)


def main():
    os.makedirs(OUT, exist_ok=True)
    total, fails = 0, 0
    for biome, name, kind, rgb, variants in PROPS:
        for v in range(variants):
            reset()
            build(kind, rgb, seed=hash((name, v)) & 0xFFFF)
            setup_scene()
            path = os.path.join(OUT, f"{biome}_{name}_{v}.png")
            bpy.context.scene.render.filepath = path
            bpy.ops.render.render(write_still=True)

            opaque, edges = posterize_and_outline(path)
            ok = opaque > 60
            if not ok:
                fails += 1
            print(f"[props] {'OK  ' if ok else 'FAIL'} {biome}_{name}_{v} — "
                  f"불투명 {opaque}px / 외곽선 {edges}px")
            total += 1

    print(f"[props] 총 {total}장 (실패 {fails}) → {OUT}")


main()
