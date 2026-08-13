from PIL import Image, ImageDraw

im = Image.open("source_sheets/sheet4_dash.png").convert("RGB")

# Tank 행만 추출 (y=83~298)
tank_crop = im.crop((0, 83, im.width, 298))
tank_crop.save("results/debug_tank_full.png")

print("Tank 행 추출 완료: results/debug_tank_full.png")

# 배경색 샘플링 (첫 프레임 영역 307~535)
p = im.load()
from collections import Counter

bg_colors = Counter()
for x in range(307, 320):
    for y in range(83, 100):
        bg_colors[p[x, y]] += 1

print("\nTank 첫 프레임 좌상단 배경색 샘플:")
for color, count in bg_colors.most_common(10):
    brightness = sum(color) // 3
    print(f"  {color}: {count}개 (밝기 {brightness})")

# 첫 dash 프레임 이미지만 추출 (307~535)
frame1 = im.crop((307, 83, 535, 298))
frame1.save("results/debug_tank_frame1.png")
print("\n첫 dash 프레임: results/debug_tank_frame1.png")

# 이미지의 모든 픽셀 색상 히스토그램
all_colors = Counter()
for y in range(83, 298):
    for x in range(307, 535):
        all_colors[p[x, y]] += 1

print(f"\nTank 첫 프레임 내 색상 분포:")
for color, count in all_colors.most_common(10):
    brightness = (color[0]*299 + color[1]*587 + color[2]*114) // 1000
    print(f"  {color}: {count}개 (밝기 {brightness})")
