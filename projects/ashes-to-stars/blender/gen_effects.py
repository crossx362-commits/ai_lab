"""
재와 별 — 이펙트·표식 스프라이트 생성기
Blender 4.1 headless 전용.

    blender --background --factory-startup --python gen_effects.py

무엇을 만드는가:
  장판 예고 표식, 폭발, 대시 잔상, 투사체, 피격 표시 등
  프로토타입 W2(탄막 회피 조작감) 필요 에셋을 단순 도형으로 렌더한다.

출력: ./out_effects/<name>.png  (투명 배경)
"""
import bpy, math, os, sys

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out_effects")
RENDER_ENGINE = "BLENDER_EEVEE"  # EEVEE 우선, 없으면 WORKBENCH


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def set_render_engine():
    """EEVEE 또는 WORKBENCH 설정"""
    sc = bpy.context.scene
    engines = [e.identifier for e in
               bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    for cand in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE", "BLENDER_WORKBENCH"):
        if cand in engines:
            sc.render.engine = cand
            return
    print(f"[warning] no render engine found")


def setup_render(width, height, transparent=True):
    """렌더 설정 (해상도, 투명성, 포맷)"""
    sc = bpy.context.scene
    set_render_engine()
    sc.render.resolution_x = width
    sc.render.resolution_y = height
    sc.render.film_transparent = transparent
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"
    try:
        sc.eevee.taa_render_samples = 16
    except AttributeError:
        pass


def setup_camera_topdown(distance=8.0):
    """위에서 내려다본 직교 카메라 (투영각도 0°)"""
    bpy.ops.object.camera_add(location=(0, 0, distance))
    cam = bpy.context.object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 5.0
    bpy.context.scene.camera = cam
    return cam


def setup_camera_quarterish(distance=6.0, pitch=15.0):
    """약간 숙인 쿼터뷰 카메라 (투영각도 15°, 더 곡선의 느낌)"""
    bpy.ops.object.camera_add()
    cam = bpy.context.object
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 4.0
    pitch_rad = math.radians(pitch)
    yaw_rad = math.radians(45)
    cam.location = (distance * math.cos(pitch_rad) * math.sin(yaw_rad),
                    -distance * math.cos(pitch_rad) * math.cos(yaw_rad),
                    distance * math.sin(pitch_rad) + 0.3)
    cam.rotation_euler = (math.radians(90 - pitch), 0, yaw_rad)
    bpy.context.scene.camera = cam
    return cam


def setup_lights():
    """간단한 조명 3점"""
    for loc, e in [((4, -4, 8), 1000), ((-5, -2, 4), 300), ((0, 5, 5), 250)]:
        bpy.ops.object.light_add(type="AREA", location=loc)
        L = bpy.context.object
        L.data.energy = e
        L.data.size = 4


def mat_emissive(name, rgb, emit_strength=1.0):
    """자체발광 재질 (밝은 이펙트용)"""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    for key in ("Emission Color", "Emission"):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = (*rgb, 1)
            break
    if "Emission Strength" in bsdf.inputs:
        bsdf.inputs["Emission Strength"].default_value = emit_strength
    return m


def mat_opaque(name, rgb):
    """불투명 재질 (일반 도형용)"""
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Roughness"].default_value = 0.8
    return m


def render_to_file(filepath, size=(256, 256)):
    """현재 장면을 PNG로 렌더"""
    sc = bpy.context.scene
    sc.render.resolution_x = size[0]
    sc.render.resolution_y = size[1]
    sc.render.filepath = filepath
    bpy.ops.render.render(write_still=True)


def validate_image(filepath):
    """PNG 파일 검증: 불투명 픽셀 수, 알파 범위"""
    try:
        import struct
        with open(filepath, 'rb') as f:
            data = f.read()

        # 극도로 간단한 PNG 검증: IHDR 헤더 확인
        if not data.startswith(b'\x89PNG'):
            return 0, "INVALID_PNG"

        # 작은 파일은 실패 (빈 프레임)
        if len(data) < 100:
            return 0, "TINY_FILE"

        # PIL/Pillow가 있으면 정확한 계산
        try:
            from PIL import Image
            img = Image.open(filepath)
            px = img.load()
            w, h = img.size
            opaque_count = 0
            alpha_vals = []
            for y in range(h):
                for x in range(w):
                    p = px[x, y]
                    if len(p) >= 4:  # RGBA
                        alpha = p[3]
                        alpha_vals.append(alpha)
                        if alpha > 127:
                            opaque_count += 1
            return opaque_count, min(alpha_vals) if alpha_vals else 0
        except ImportError:
            # PIL 없을 땐 파일 크기만 체크
            return len(data), "PIL_UNAVAIL"
    except Exception as e:
        return 0, f"ERROR: {e}"


def effect_warn_circle():
    """
    장판 예고 표식 — 바닥에 깔리는 붉은 원형 테두리
    위에서 내려다본 타원(쿼터뷰 시뮬레이션)
    3단계 강도: 옅음, 중간, 진함
    """
    reset()
    setup_render(256, 256)
    setup_camera_quarterish(distance=6.0, pitch=25.0)
    setup_lights()

    # 바닥 평면 (레퍼런스)
    bpy.ops.mesh.primitive_plane_add(size=3, location=(0, 0, 0.01))

    # 원 테두리 (높이 무시, 쿼터뷰면 찌그러짐)
    # 도넛 모양으로 렌더하되, 쿼터뷰 카메라로 타원화
    bpy.ops.mesh.primitive_torus_add(major_radius=1.0, minor_radius=0.08,
                                      location=(0, 0, 0.1))
    circle = bpy.context.object

    names = []
    for i, (intensity, strength) in enumerate([
        ("light",  0.4),   # 옅음
        ("normal", 0.7),   # 중간
        ("intense", 1.0)   # 진함
    ]):
        reset()
        setup_render(256, 256)
        setup_camera_quarterish(distance=6.0, pitch=25.0)
        setup_lights()

        bpy.ops.mesh.primitive_plane_add(size=3, location=(0, 0, 0.01))
        bpy.ops.mesh.primitive_torus_add(major_radius=1.0, minor_radius=0.08,
                                          location=(0, 0, 0.1))
        circle = bpy.context.object

        # 붉은색, 강도별 발광
        red = (1.0, 0.1, 0.1)
        m = mat_emissive(f"warn_{intensity}", red, emit_strength=strength * 0.8)
        circle.data.materials.append(m)

        fname = os.path.join(OUT, f"warn_circle_{intensity}.png")
        render_to_file(fname, (256, 256))
        names.append((f"warn_circle_{intensity}", fname))

    return names


def effect_burst():
    """
    폭발 이펙트 — 방사형 스파이크
    4프레임 시퀀스: 작게 → 크게 → 흐리게 → 소멸
    """
    names = []

    for frame, (scale, opacity) in enumerate([
        (1.3, 1.0),    # 프레임 0: 작음, 밝음
        (1.5, 1.0),    # 프레임 1: 중간, 밝음
        (1.8, 0.6),    # 프레임 2: 크고, 흐려짐
        (2.2, 0.2),    # 프레임 3: 가장 크고, 거의 소멸
    ]):
        reset()
        setup_render(128, 128)
        setup_camera_topdown(distance=3.5)
        setup_lights()

        # 방사형 스파이크 8개 (윗쪽 보기)
        spike_color = (1.0, 0.6, 0.2)
        m = mat_emissive("burst_spike", spike_color, emit_strength=opacity * 0.9)

        for dir_idx in range(8):
            angle = math.radians(45 * dir_idx)
            x = math.cos(angle) * 0.8 * scale
            y = math.sin(angle) * 0.8 * scale

            # 원뿔 스파이크 (위쪽 가리키기)
            bpy.ops.mesh.primitive_cone_add(
                radius1=0.15 * scale,
                depth=0.5 * scale,
                vertices=8,
                location=(x, y, 0.3 * scale)
            )
            spike = bpy.context.object
            spike.data.materials.append(m)

        fname = os.path.join(OUT, f"fx_burst_{frame}.png")
        render_to_file(fname, (128, 128))
        names.append((f"fx_burst_{frame}", fname))

    return names


def effect_dash_trail():
    """
    대시 잔상 — 반투명 타원
    3단계: 진함, 중간, 옅음
    """
    names = []

    for stage, (alpha, emit) in enumerate([
        (1.0, 0.7),    # 진함
        (0.6, 0.45),   # 중간
        (0.2, 0.15),   # 옅음
    ]):
        reset()
        setup_render(128, 128)
        setup_camera_topdown(distance=4.0)
        setup_lights()

        # 수평 타원 (쿼터뷰 하강 시뮬레이션)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=1.0, location=(0, 0, 0.2))
        trail = bpy.context.object
        trail.scale = (2.0, 0.6, 0.3)  # 길쭉한 타원

        # 밝은 파란색, 반투명
        blue = (0.3, 0.7, 1.0)
        m = mat_emissive("dash_trail", blue, emit_strength=emit)
        trail.data.materials.append(m)

        fname = os.path.join(OUT, f"fx_dash_trail_{stage}.png")
        render_to_file(fname, (128, 128))
        names.append((f"fx_dash_trail_{stage}", fname))

    return names


def effect_projectile():
    """
    투사체 — 발광하는 구체 + 꼬리
    2장: orb 본체 1장, trail 1장
    """
    names = []

    # 구체 (투사체 본체)
    reset()
    setup_render(64, 64)
    setup_camera_topdown(distance=2.0)  # 더 가까이
    setup_lights()

    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.8, location=(0, 0, 0.3))  # 더 크게
    orb = bpy.context.object

    purple = (0.8, 0.3, 1.0)
    m = mat_emissive("proj_orb", purple, emit_strength=1.0)
    orb.data.materials.append(m)

    fname = os.path.join(OUT, "proj_orb.png")
    render_to_file(fname, (64, 64))
    names.append(("proj_orb", fname))

    # 꼬리 (도형)
    reset()
    setup_render(64, 64)
    setup_camera_topdown(distance=1.2)  # 더 가까이
    setup_lights()

    bpy.ops.mesh.primitive_cone_add(
        radius1=0.7,  # 더 크게
        depth=2.2,    # 더 길게
        vertices=12,
        location=(0, -0.5, 0.1),
        rotation=(math.radians(90), 0, 0)
    )
    tail = bpy.context.object

    trail_color = (0.6, 0.2, 0.8)
    m2 = mat_emissive("proj_tail", trail_color, emit_strength=0.6)
    tail.data.materials.append(m2)

    fname = os.path.join(OUT, "proj_tail.png")
    render_to_file(fname, (64, 64))
    names.append(("proj_tail", fname))

    return names


def effect_hit():
    """
    피격 표시 — 짧은 흰색 섬광
    2프레임: 밝음, 희미함
    """
    names = []

    for frame, intensity in enumerate([1.0, 0.3]):
        reset()
        setup_render(128, 128)
        setup_camera_topdown(distance=4.0)
        setup_lights()

        # 중앙 구체 (섬광)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.5, location=(0, 0, 0.2))
        hit = bpy.context.object

        white = (1.0, 1.0, 1.0)
        m = mat_emissive("hit_flash", white, emit_strength=intensity)
        hit.data.materials.append(m)

        # 사방 스파이크 (작은 십자)
        for dx, dy in [(0.8, 0), (-0.8, 0), (0, 0.8), (0, -0.8)]:
            bpy.ops.mesh.primitive_cube_add(size=0.2,
                                             location=(dx, dy, 0.2))
            spike = bpy.context.object
            spike.data.materials.append(m)

        fname = os.path.join(OUT, f"fx_hit_{frame}.png")
        render_to_file(fname, (128, 128))
        names.append((f"fx_hit_{frame}", fname))

    return names


def effect_taunt():
    """
    도발의 함성 — 충격파 원형 확산
    4프레임: 작음 → 중간 → 크게 → 소멸
    """
    names = []

    for frame, (radius, opacity) in enumerate([
        (0.5, 1.0),    # 프레임 0: 작은 고리
        (1.2, 0.9),    # 프레임 1: 중간
        (1.8, 0.5),    # 프레임 2: 크게
        (2.3, 0.1),    # 프레임 3: 거의 소멸
    ]):
        reset()
        setup_render(256, 256)
        setup_camera_quarterish(distance=6.0, pitch=25.0)
        setup_lights()

        # 바닥 평면
        bpy.ops.mesh.primitive_plane_add(size=3, location=(0, 0, 0.01))

        # 충격파 링 (도넛 모양)
        bpy.ops.mesh.primitive_torus_add(major_radius=radius, minor_radius=radius*0.15,
                                          location=(0, 0, 0.15))
        wave = bpy.context.object

        # 노란색 충격파
        yellow = (1.0, 0.9, 0.2)
        m = mat_emissive("taunt_wave", yellow, emit_strength=opacity * 0.8)
        wave.data.materials.append(m)

        fname = os.path.join(OUT, f"fx_taunt_{frame:02d}.png")
        render_to_file(fname, (256, 256))
        names.append((f"fx_taunt_{frame:02d}", fname))

    return names


def effect_firestorm():
    """
    화염폭풍 — 불길 장판
    4프레임: 불이 피어오르고 소멸
    """
    names = []

    for frame, (scale, opacity) in enumerate([
        (0.8, 1.0),    # 프레임 0: 첫 불길
        (1.2, 0.85),   # 프레임 1: 성장
        (1.3, 0.5),    # 프레임 2: 절정, 흐려짐
        (0.9, 0.15),   # 프레임 3: 소멸
    ]):
        reset()
        setup_render(256, 256)
        setup_camera_quarterish(distance=6.0, pitch=25.0)
        setup_lights()

        # 바닥 평면 (불이 깔림)
        bpy.ops.mesh.primitive_plane_add(size=3, location=(0, 0, 0.01))
        floor = bpy.context.object
        floor_mat = mat_opaque("floor_dark", (0.3, 0.2, 0.1))
        floor.data.materials.append(floor_mat)

        # 불길 원형 분사 (여러 조각으로 표현)
        fire_color = (1.0, 0.6, 0.0)
        m = mat_emissive("firestorm", fire_color, emit_strength=opacity * 0.9)

        for i in range(6):
            angle = math.radians(60 * i)
            x = math.cos(angle) * 0.7 * scale
            y = math.sin(angle) * 0.7 * scale

            # 불길 뭉치 (구체들)
            for j, height_factor in enumerate([0.3, 0.7, 1.0]):
                bpy.ops.mesh.primitive_uv_sphere_add(
                    radius=0.25 * (1.0 - j*0.25),
                    location=(x + math.cos(angle)*j*0.15,
                             y + math.sin(angle)*j*0.15,
                             0.1 + height_factor * 0.4)
                )
                flame = bpy.context.object
                flame.data.materials.append(m)

        fname = os.path.join(OUT, f"fx_firestorm_{frame:02d}.png")
        render_to_file(fname, (256, 256))
        names.append((f"fx_firestorm_{frame:02d}", fname))

    return names


def effect_heal_wave():
    """
    치유의 파동 — 위로 솟는 초록 파동
    3프레임: 아래에서 위로
    """
    names = []

    for frame, (y_pos, opacity) in enumerate([
        (0.2, 1.0),    # 프레임 0: 아래에서 시작
        (0.5, 0.7),    # 프레임 1: 중간 상승
        (0.9, 0.2),    # 프레임 2: 위로 소멸
    ]):
        reset()
        setup_render(256, 256)
        setup_camera_quarterish(distance=6.0, pitch=25.0)
        setup_lights()

        # 바닥 평면
        bpy.ops.mesh.primitive_plane_add(size=3, location=(0, 0, 0.01))

        # 회복 파동 (고리)
        bpy.ops.mesh.primitive_torus_add(major_radius=1.5, minor_radius=0.2,
                                          location=(0, 0, y_pos))
        wave = bpy.context.object

        # 밝은 초록색
        green = (0.3, 1.0, 0.5)
        m = mat_emissive("heal_wave", green, emit_strength=opacity * 0.8)
        wave.data.materials.append(m)

        # 상승 효과를 위해 추가 구체
        for z_offset in [0.1, 0.3, 0.5]:
            bpy.ops.mesh.primitive_uv_sphere_add(
                radius=0.3,
                location=(0, 0, y_pos + z_offset * 0.3)
            )
            sphere = bpy.context.object
            sphere.data.materials.append(m)

        fname = os.path.join(OUT, f"fx_heal_wave_{frame:02d}.png")
        render_to_file(fname, (256, 256))
        names.append((f"fx_heal_wave_{frame:02d}", fname))

    return names


def effect_miracle_light():
    """
    기적 — 금색 대형 광휘
    단일 프레임: 밝은 금색 구체 + 고리
    """
    names = []

    reset()
    setup_render(256, 256)
    setup_camera_quarterish(distance=6.0, pitch=20.0)
    setup_lights()

    # 바닥 평면
    bpy.ops.mesh.primitive_plane_add(size=3, location=(0, 0, 0.01))

    # 큰 중앙 구체
    bpy.ops.mesh.primitive_uv_sphere_add(radius=1.2, location=(0, 0, 0.5))
    center = bpy.context.object

    # 밝은 금색
    gold = (1.0, 0.9, 0.2)
    m = mat_emissive("miracle_center", gold, emit_strength=1.2)
    center.data.materials.append(m)

    # 외부 고리
    bpy.ops.mesh.primitive_torus_add(major_radius=1.5, minor_radius=0.25,
                                      location=(0, 0, 0.5))
    ring = bpy.context.object
    ring.data.materials.append(m)

    fname = os.path.join(OUT, "fx_miracle_light.png")
    render_to_file(fname, (256, 256))
    names.append(("fx_miracle_light", fname))

    return names


def effect_bardic_aura():
    """
    음유시인 악장 — 음표 오라 링
    2종류: 진군가(노란색) / 수호가(파란색)
    """
    names = []

    for theme, (color, name) in [
        ("attack", ((1.0, 0.9, 0.2), "진군가")),   # 노란색
        ("defense", ((0.3, 0.7, 1.0), "수호가")),  # 파란색
    ]:
        reset()
        setup_render(256, 256)
        setup_camera_quarterish(distance=6.0, pitch=25.0)
        setup_lights()

        # 바닥 평면
        bpy.ops.mesh.primitive_plane_add(size=3, location=(0, 0, 0.01))

        # 오라 링 (도넛)
        bpy.ops.mesh.primitive_torus_add(major_radius=1.3, minor_radius=0.18,
                                          location=(0, 0, 0.2))
        aura = bpy.context.object

        m = mat_emissive(f"aura_{theme}", color, emit_strength=0.7)
        aura.data.materials.append(m)

        # 음표 표현 (작은 구체 위아래)
        for z_offset in [0.4, -0.1]:
            for angle_mult in [0, 1, 2, 3]:
                angle = math.radians(90 * angle_mult)
                x = math.cos(angle) * 1.3
                y = math.sin(angle) * 1.3

                bpy.ops.mesh.primitive_uv_sphere_add(
                    radius=0.15,
                    location=(x, y, z_offset)
                )
                note = bpy.context.object
                note.data.materials.append(m)

        fname = os.path.join(OUT, f"fx_bardic_{theme}.png")
        render_to_file(fname, (256, 256))
        names.append((f"fx_bardic_{theme}", fname))

    return names


def main():
    os.makedirs(OUT, exist_ok=True)

    all_results = []
    total = 0

    print("[effects] 경고 원형 표식...")
    all_results.extend(effect_warn_circle())
    total += 3

    print("[effects] 폭발 이펙트...")
    all_results.extend(effect_burst())
    total += 4

    print("[effects] 대시 잔상...")
    all_results.extend(effect_dash_trail())
    total += 3

    print("[effects] 투사체...")
    all_results.extend(effect_projectile())
    total += 2

    print("[effects] 피격 표시...")
    all_results.extend(effect_hit())
    total += 2

    # ──── 스킬 이펙트 (신규) ────────────────────────────
    print("[effects] 도발의 함성 (충격파)...")
    all_results.extend(effect_taunt())
    total += 4

    print("[effects] 화염폭풍 (불길)...")
    all_results.extend(effect_firestorm())
    total += 4

    print("[effects] 치유의 파동...")
    all_results.extend(effect_heal_wave())
    total += 3

    print("[effects] 기적 (금색 광휘)...")
    all_results.extend(effect_miracle_light())
    total += 1

    print("[effects] 음유시인 악장...")
    all_results.extend(effect_bardic_aura())
    total += 2

    # ──── 검증 테이블 ────────────────────────────
    print("\n" + "="*70)
    print("검증 결과 (픽셀 통계)")
    print("="*70)
    print(f"{'파일명':<30} {'불투명픽셀':<15} {'최소α':<10} {'상태':<10}")
    print("-"*70)

    failed = []
    for name, fpath in all_results:
        opaque, min_alpha = validate_image(fpath)

        # 판정: 해상도 × 해상도 대비 일정 비율 이상이어야 함
        # warn_circle: 256×256 = 65536, 최소 1000 (1.5%)
        # fx_burst: 128×128 = 16384, 최소 500 (3%)
        # fx_dash_trail: 128×128 = 16384, 최소 400 (2.4%)
        # proj_*: 64×64 = 4096, 최소 200 (4.8%)
        # fx_hit: 128×128 = 16384, 최소 300 (1.8%)

        thresholds = {
            "warn": 1000,
            "fx_burst": 500,
            "fx_dash_trail": 400,
            "proj": 200,
            "fx_hit": 300,
            "fx_taunt": 1200,    # 256×256, 충격파 링
            "fx_firestorm": 1500, # 256×256, 불길
            "fx_heal_wave": 1000, # 256×256, 파동
            "fx_miracle": 1500,   # 256×256, 광휘
            "fx_bardic": 1200,    # 256×256, 오라
        }
        threshold = next((v for k, v in thresholds.items() if k in name), 200)

        status = "OK" if (opaque >= threshold and min_alpha >= 0) else "FAIL"
        if status == "FAIL":
            failed.append(name)

        print(f"{name:<30} {opaque:<15} {min_alpha:<10} {status:<10}")

    print("-"*70)
    print(f"총 {total}장 렌더 완료 → {OUT}")

    if failed:
        print(f"\n[WARNING] 검증 실패: {failed}")
        return False
    else:
        print("\n✓ 전체 검증 통과")
        return True


if __name__ == "__main__":
    success = main()
    sys.exit(0 if success else 1)
