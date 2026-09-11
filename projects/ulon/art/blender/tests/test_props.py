"""울온 소품 자(§6.1 크기·§8.1 실루엣). 지우거나 약화시키면 게이트가 잡는다."""
import bpy


def _height(o):
    zs = [(o.matrix_world @ v.co).z for v in o.data.vertices]
    return max(zs) - min(zs), min(zs)


def test_mortar_exists_and_sized():
    o = bpy.data.objects.get("Mortar")
    assert o is not None and o.type == "MESH", "Mortar 메시가 없다"
    h, low = _height(o)
    assert 0.5 <= h <= 1.0, f"절구통 높이 {h:.2f}m — 사람 키 1.8m 기준 0.5~1.0m 이어야 한다"
    assert abs(low) < 1e-3, f"바닥이 z=0 이 아니다: {low:.3f}"
