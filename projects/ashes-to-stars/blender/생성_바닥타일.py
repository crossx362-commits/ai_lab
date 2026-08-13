"""
재와 별 — 바닥 타일 생성기 (노이즈맵 기반)
Blender 4.1 headless 전용.

    blender --background --factory-startup --python gen_ground.py

방식:
  Noise/Voronoi/Musgrave 프로시저럴 텍스처를 셰이더에서 조합해
  **타일링 가능한(seamless)** 바닥 텍스처를 베이크한다.
  카메라로 렌더하지 않고 이미지 텍스처 노드에 직접 베이크하므로
  쿼터뷰 각도와 무관하게 유니티에서 타일맵/평면에 그대로 깔 수 있다.

seamless 보장:
  노이즈 좌표를 4D로 두고, UV(0~1)를 원환면(torus)에 매핑해 샘플링한다.
  → 좌우·상하 어디로 이어 붙여도 경계가 안 보인다.

출력: ./ground/<biome>_albedo.png (512px)  ※ 프로토타입엔 albedo만으로 충분
"""
import bpy, os, math

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "출력_바닥")
SIZE = 512

# (이름, 바탕색, 얼룩색, 노이즈 스케일, 보로노이 세기, 거칠기)
BIOMES = [
    ("field_plain",  (0.18, 0.26, 0.16), (0.26, 0.36, 0.20),  6.0, 0.35),  # 필드 — 풀밭
    ("field_ash",    (0.16, 0.14, 0.15), (0.30, 0.26, 0.25),  8.0, 0.55),  # 잿벌판 (세계관: 한 번 불탄 땅)
    ("dungeon_rock", (0.13, 0.12, 0.14), (0.22, 0.21, 0.24), 10.0, 0.70),  # 던전 — 암반
    ("estate_soil",  (0.22, 0.17, 0.12), (0.32, 0.25, 0.17),  7.0, 0.30),  # 영지 — 흙
]


def clean():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for img in list(bpy.data.images):
        bpy.data.images.remove(img)


def make_seamless_ground(name, base, spot, scale, voro):
    """평면 + 셰이더 노드로 seamless 노이즈 바닥을 구성하고 베이크"""
    bpy.ops.mesh.primitive_plane_add(size=2)
    plane = bpy.context.object

    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    nodes, links = nt.nodes, nt.links
    nodes.clear()

    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfDiffuse")
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

    # ── UV → 원환면 좌표 (seamless 타일링의 핵심) ──────────────
    # u,v ∈ [0,1] 을 각도로 바꿔 (cos u, sin u, cos v, sin v) 4D 좌표로.
    # 경계에서 값이 정확히 이어지므로 이어붙여도 티가 안 난다.
    uv = nodes.new("ShaderNodeTexCoord")
    sep = nodes.new("ShaderNodeSeparateXYZ")
    links.new(uv.outputs["UV"], sep.inputs["Vector"])

    def circ(idx):
        """스칼라 → (cos, sin) 2성분"""
        mul = nodes.new("ShaderNodeMath"); mul.operation = "MULTIPLY"
        mul.inputs[1].default_value = math.tau
        links.new(sep.outputs[idx], mul.inputs[0])
        c = nodes.new("ShaderNodeMath"); c.operation = "COSINE"
        s = nodes.new("ShaderNodeMath"); s.operation = "SINE"
        links.new(mul.outputs[0], c.inputs[0])
        links.new(mul.outputs[0], s.inputs[0])
        return c, s

    cu, su = circ(0)
    cv, sv = circ(1)

    comb = nodes.new("ShaderNodeCombineXYZ")
    links.new(cu.outputs[0], comb.inputs["X"])
    links.new(su.outputs[0], comb.inputs["Y"])
    links.new(cv.outputs[0], comb.inputs["Z"])

    # W 축에 sin(v)를 넣어 4D 노이즈로 (원환면 완성)
    noise = nodes.new("ShaderNodeTexNoise")
    noise.noise_dimensions = "4D"
    noise.inputs["Scale"].default_value = scale
    noise.inputs["Detail"].default_value = 8.0
    noise.inputs["Roughness"].default_value = 0.55
    links.new(comb.outputs["Vector"], noise.inputs["Vector"])
    links.new(sv.outputs[0], noise.inputs["W"])

    # 보로노이로 자갈·균열 느낌 추가 (같은 원환면 좌표 사용)
    vor = nodes.new("ShaderNodeTexVoronoi")
    vor.voronoi_dimensions = "4D"
    vor.feature = "DISTANCE_TO_EDGE"
    vor.inputs["Scale"].default_value = scale * 1.6
    links.new(comb.outputs["Vector"], vor.inputs["Vector"])
    links.new(sv.outputs[0], vor.inputs["W"])

    # 노이즈 × 보로노이 혼합 (스칼라 Mix — Float 모드가 인덱스 혼동이 없다)
    mix1 = nodes.new("ShaderNodeMix")
    mix1.data_type = "FLOAT"
    mix1.inputs["Factor"].default_value = voro
    links.new(noise.outputs["Fac"], mix1.inputs["A"])
    links.new(vor.outputs["Distance"], mix1.inputs["B"])

    # 대비 확장 — 노이즈는 대개 0.35~0.65에 몰려 있어 그대로 쓰면 평평하다
    stretch = nodes.new("ShaderNodeMapRange")
    stretch.inputs["From Min"].default_value = 0.30
    stretch.inputs["From Max"].default_value = 0.70
    stretch.inputs["To Min"].default_value = 0.0
    stretch.inputs["To Max"].default_value = 1.0
    stretch.clamp = True
    links.new(mix1.outputs["Result"], stretch.inputs["Value"])

    # 4단 컬러 램프 — 바탕 → 얼룩 사이를 층지게 (단색으로 뭉개지지 않게)
    ramp = nodes.new("ShaderNodeValToRGB")
    cr = ramp.color_ramp
    dark = tuple(c * 0.65 for c in base)
    lite = tuple(min(1.0, c * 1.35) for c in spot)
    cr.elements[0].position = 0.0;  cr.elements[0].color = (*dark, 1)
    cr.elements[1].position = 1.0;  cr.elements[1].color = (*lite, 1)
    e2 = cr.elements.new(0.38); e2.color = (*base, 1)
    e3 = cr.elements.new(0.68); e3.color = (*spot, 1)
    links.new(stretch.outputs["Result"], ramp.inputs["Fac"])
    links.new(ramp.outputs["Color"], bsdf.inputs["Color"])

    # ── 베이크 대상 이미지 ────────────────────────────────
    img = bpy.data.images.new(name, SIZE, SIZE, alpha=False)
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = img
    nodes.active = tex   # 베이크는 활성 이미지 노드에 기록된다

    plane.data.materials.append(m)
    return plane, img


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, base, spot, scale, voro in BIOMES:
        clean()
        sc = bpy.context.scene
        sc.render.engine = "CYCLES"      # 베이크는 Cycles만 지원
        sc.cycles.samples = 8            # 프로시저럴 텍스처라 적은 샘플로 충분
        sc.cycles.use_denoising = False
        try:
            sc.cycles.device = "CPU"
        except Exception:
            pass
        sc.render.bake.use_pass_direct = False
        sc.render.bake.use_pass_indirect = False
        sc.render.bake.margin = 16
        # AgX/Filmic이 대비를 눌러 평평하게 보이므로 표준 변환으로 베이크
        sc.view_settings.view_transform = "Standard"
        sc.view_settings.look = "None"

        plane, img = make_seamless_ground(name, base, spot, scale, voro)
        bpy.ops.object.select_all(action="DESELECT")
        plane.select_set(True)
        bpy.context.view_layer.objects.active = plane

        bpy.ops.object.bake(type="DIFFUSE")

        path = os.path.join(OUT, f"{name}_albedo.png")
        img.filepath_raw = path
        img.file_format = "PNG"
        img.save()

        # 검증: 실제로 무늬가 생겼는지 픽셀 통계로 확인 (눈대중 금지)
        px = list(img.pixels)
        lum = [(px[i] * 0.299 + px[i + 1] * 0.587 + px[i + 2] * 0.114)
               for i in range(0, len(px), 4 * 37)]  # 37칸 간격 표본
        lo, hi = min(lum), max(lum)
        avg = sum(lum) / len(lum)
        var = "OK " if (hi - lo) > 0.08 else "FLAT"
        print(f"[ground] {var} {name} 밝기 {lo:.3f}~{hi:.3f} (폭 {hi-lo:.3f}, 평균 {avg:.3f}) → {path}")

    print(f"[ground] 총 {len(BIOMES)}종 완료 (seamless {SIZE}px)")


main()
