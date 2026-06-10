#!/usr/bin/env python3
"""
seed_demo_data.py - Petnna 일주일치 데모 데이터 주입

사용법: python seed_demo_data.py
"""
import os
import sys
import json
import random
from datetime import datetime, timedelta
import urllib.request
import urllib.parse

# UTF-8 인코딩 설정
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')

# 프로젝트 루트에서 .env 로드
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', '..'))
from projects.ai_team._shared.env_loader import load_env
load_env()

SUPABASE_URL = os.getenv('SUPABASE_URL')
SUPABASE_ANON_KEY = os.getenv('SUPABASE_ANON_KEY')

if not SUPABASE_URL or not SUPABASE_ANON_KEY:
    print("❌ SUPABASE_URL 또는 SUPABASE_ANON_KEY가 설정되지 않았습니다.")
    sys.exit(1)

# API 헬퍼 함수
def supabase_insert(table: str, data: dict):
    """Supabase 테이블에 데이터 삽입"""
    url = f"{SUPABASE_URL}/rest/v1/{table}"
    headers = {
        "apikey": SUPABASE_ANON_KEY,
        "Authorization": f"Bearer {SUPABASE_ANON_KEY}",
        "Content-Type": "application/json",
        "Prefer": "return=representation"
    }

    payload = json.dumps(data).encode('utf-8')
    req = urllib.request.Request(url, data=payload, headers=headers, method='POST')

    try:
        with urllib.request.urlopen(req, timeout=10) as response:
            result = json.loads(response.read().decode())
            return result
    except urllib.error.HTTPError as e:
        error_body = e.read().decode()
        print(f"❌ {table} 삽입 실패: HTTP {e.code}")
        print(f"   {error_body}")
        return None
    except Exception as e:
        print(f"❌ {table} 삽입 실패: {e}")
        return None


def generate_demo_data():
    """일주일치 데모 데이터 생성"""
    print("🐾 펫과나 데모 데이터 생성 중...")
    print()

    # 데모 사용자 ID (Supabase Auth에서 생성된 UUID 사용 - 실제로는 로그인한 사용자 ID)
    # 참고: RLS 정책 때문에 실제 인증된 사용자만 데이터 삽입 가능
    # 여기서는 anon key로 public 접근 가능한 샘플 데이터만 생성

    demo_user_id = "00000000-0000-0000-0000-000000000000"  # 플레이스홀더
    demo_email = "demo@petnna.app"

    # 1. 프로필 생성
    print("1️⃣ 사용자 프로필 생성...")
    profile = {
        "user_id": demo_user_id,
        "email": demo_email,
        "nickname": "데모유저",
        "avatar": "🐕",
        "theme": "auto",
        "unit": "metric",
        "notifications_enabled": True
    }
    # RLS 정책 때문에 anon key로는 삽입 불가 - 스키마만 표시
    print(f"   프로필: {profile['nickname']} ({profile['email']})")

    # 2. 반려동물 생성
    print("\n2️⃣ 반려동물 정보 생성...")
    pet_id = random.randint(100000, 999999)
    pet = {
        "id": pet_id,
        "user_id": demo_user_id,
        "name": "코코",
        "breed": "골든 리트리버",
        "type": "강아지",
        "imageUrl": "https://images.unsplash.com/photo-1633722715463-d30f4f325e24?w=400",
        "age": "3살 (청소년기)",
        "weight": 28.5,
        "gender": "남아",
        "personality": "활발하고 사교적",
        "hunger": 75,
        "happy": 85,
        "roomName": "코코의 방",
        "iqScore": 128,
        "iqTitle": "똑똑한 친구",
        "iqDesc": "학습 능력이 뛰어나고 명령을 잘 따릅니다",
        "mbtiCode": "ENFP"
    }
    print(f"   반려동물: {pet['name']} ({pet['breed']}, {pet['age']})")

    # 3. 일주일치 산책 데이터 생성
    print("\n3️⃣ 일주일치 산책 기록 생성...")
    now = datetime.now()
    posts = []

    walk_locations = [
        {"name": "한강공원", "lat": 37.5326, "lng": 127.0246},
        {"name": "올림픽공원", "lat": 37.5219, "lng": 127.1211},
        {"name": "서울숲", "lat": 37.5443, "lng": 127.0374},
        {"name": "남산공원", "lat": 37.5512, "lng": 126.9882},
    ]

    for i in range(7):  # 7일
        day = now - timedelta(days=i)

        # 하루에 1-2번 산책
        walk_count = random.randint(1, 2)

        for j in range(walk_count):
            location = random.choice(walk_locations)
            distance = round(random.uniform(1.5, 4.5), 2)  # 1.5 ~ 4.5 km
            duration = int(distance * 15 + random.randint(-5, 10))  # 대략 15분/km
            calories = int(distance * 80)  # 대략 80kcal/km

            walk_data = {
                "distance": distance,
                "duration": duration,
                "calories": calories,
                "startTime": (day - timedelta(minutes=duration)).strftime("%Y-%m-%d %H:%M:%S"),
                "endTime": day.strftime("%Y-%m-%d %H:%M:%S"),
                "location": location['name'],
                "coords": [
                    {"lat": location['lat'] + random.uniform(-0.01, 0.01),
                     "lng": location['lng'] + random.uniform(-0.01, 0.01)}
                    for _ in range(random.randint(10, 30))
                ]
            }

            # AI 건강 분석 데이터
            health_score = random.randint(75, 95)
            health_data = {
                "score": health_score,
                "status": "건강" if health_score >= 80 else "양호",
                "recommendations": [
                    f"{distance}km 산책으로 충분한 운동량을 확보했습니다.",
                    "수분 섭취를 꾸준히 해주세요.",
                    "날씨가 좋을 때 야외 활동을 추천합니다."
                ],
                "metrics": {
                    "activity": health_score,
                    "nutrition": random.randint(70, 90),
                    "rest": random.randint(75, 95)
                }
            }

            post = {
                "user_id": demo_user_id,
                "pet_name": pet['name'],
                "pet_avatar": pet['imageUrl'],
                "content": f"{location['name']}에서 {pet['name']}와 즐거운 산책! 🐕 {distance}km를 걸었어요.",
                "image": pet['imageUrl'],
                "likes": random.randint(5, 50),
                "attached_walk": walk_data,
                "attached_ai_health": health_data,
                "created_at": day.isoformat()
            }
            posts.append(post)

            print(f"   {day.strftime('%m/%d')} - {location['name']}: {distance}km, {duration}분")

    print(f"\n   총 {len(posts)}개 산책 기록 생성 완료")

    # 4. 산책 경로 저장
    print("\n4️⃣ 즐겨찾는 산책 경로 생성...")
    routes = []
    for location in walk_locations[:2]:  # 2개 경로만
        route_id = random.randint(100000, 999999)
        route = {
            "id": route_id,
            "user_id": demo_user_id,
            "email": demo_email,
            "name": f"{location['name']} 단골 코스",
            "coords": [
                {"lat": location['lat'] + random.uniform(-0.01, 0.01),
                 "lng": location['lng'] + random.uniform(-0.01, 0.01)}
                for _ in range(20)
            ],
            "distance": round(random.uniform(2.0, 3.5), 2)
        }
        routes.append(route)
        print(f"   {route['name']}: {route['distance']}km")

    # 5. 데이터 출력 (실제 삽입은 RLS 정책 때문에 auth된 사용자만 가능)
    print("\n" + "="*60)
    print("📋 생성된 데모 데이터 요약")
    print("="*60)
    print(f"반려동물: {pet['name']} ({pet['breed']})")
    print(f"산책 기록: {len(posts)}개 (일주일)")
    print(f"즐겨찾는 경로: {len(routes)}개")
    print()
    print("⚠️ 참고: Supabase RLS 정책으로 인해 실제 데이터 삽입은")
    print("   로그인한 사용자만 가능합니다.")
    print()
    print("📝 데이터 삽입 방법:")
    print("   1. https://petnna.vercel.app 에서 로그인")
    print("   2. 개발자 도구 콘솔에서 아래 코드 실행:")
    print()

    # 삽입용 JavaScript 코드 생성
    print("```javascript")
    print("// 반려동물 등록")
    print(f"const petData = {json.dumps(pet, indent=2, ensure_ascii=False)};")
    print("await window.supabase.from('pets').insert(petData);")
    print()
    print("// 산책 기록 등록")
    print(f"const posts = {json.dumps(posts[:3], indent=2, ensure_ascii=False)}; // 처음 3개만 예시")
    print("await window.supabase.from('posts').insert(posts);")
    print("```")
    print()

    # JSON 파일로 저장
    output_file = os.path.join(os.path.dirname(__file__), 'demo_data.json')
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump({
            "profile": profile,
            "pet": pet,
            "posts": posts,
            "routes": routes
        }, f, indent=2, ensure_ascii=False)

    print(f"✅ 데모 데이터가 {output_file} 에 저장되었습니다.")
    print()


if __name__ == "__main__":
    generate_demo_data()
