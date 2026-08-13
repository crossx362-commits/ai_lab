from PIL import Image

im = Image.open("source_sheets/sheet4_dash.png")
print(f"크기: {im.size}")

gray = im.convert("L")
p = gray.load()
w, h = im.size

print("\n가로선 후보 (밝기 45-130, 80% 이상):")
h_lines = []
for y in range(h):
    match = sum(1 for x in range(w) if 45 <= p[x, y] <= 130) / w
    if match >= 0.75:
        h_lines.append((y, match))
        print(f"  y={y}: {match:.1%}")

print(f"\n세로선 후보 (밝기 45-130, 80% 이상):")
v_lines = []
for x in range(w):
    match = sum(1 for y in range(h) if 45 <= p[x, y] <= 130) / h
    if match >= 0.75:
        v_lines.append((x, match))
        print(f"  x={x}: {match:.1%}")

print("\n추출된 가로선 Y 좌표:", [y for y, _ in h_lines])
print("추출된 세로선 X 좌표:", [x for x, _ in v_lines])
