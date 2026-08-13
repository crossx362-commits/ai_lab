import os
import shutil

OUT = r"D:\ai_lab\projects\ashes-to-stars\unity\Assets\Resources\sprites"
roles = ['tank', 'dps', 'ranged', 'healer', 'buffer']

# 불필요한 _dash_03 파일 삭제 (스크립트는 dash 3개만 만들었음)
for role in roles:
    role_dir = os.path.join(OUT, role)
    dash_03 = os.path.join(role_dir, f"{role}_dash_03.png")
    if os.path.exists(dash_03):
        os.remove(dash_03)
        print(f"삭제: {role}_dash_03.png")

print("\n=== 최종 파일 현황 ===")
for role in roles:
    role_dir = os.path.join(OUT, role)
    if os.path.isdir(role_dir):
        files = sorted([f for f in os.listdir(role_dir) if f.endswith('.png')])

        # 상태별 분류
        stats = {}
        for f in files:
            state = f.split('_')[1]  # tank_STATE_nn.png
            if state not in stats:
                stats[state] = 0
            stats[state] += 1

        print(f"\n{role}: 총 {len(files)}장")
        for state in sorted(stats.keys()):
            print(f"  {state}: {stats[state]}장")
