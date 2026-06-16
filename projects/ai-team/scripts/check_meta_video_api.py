#!/usr/bin/env python3
"""Meta Movie Gen API 가용성 확인"""
import sys
import requests

sys.stdout.reconfigure(encoding='utf-8', errors='replace')

print('=' * 70)
print('  Meta Movie Gen Video API 가용성 확인')
print('=' * 70)
print()

# Meta AI API 엔드포인트 체크
endpoints = [
    'https://graph.meta.ai/v1/movie-gen',
    'https://api.meta.ai/v1/generate',
    'https://ai.meta.com/api/v1/movie-gen',
]

print('1. API 엔드포인트 확인:')
for endpoint in endpoints:
    try:
        response = requests.get(endpoint, timeout=5)
        print(f'  OK {endpoint}: HTTP {response.status_code}')
    except Exception as e:
        print(f'  X  {endpoint}: {str(e)[:60]}')

print()
print('2. Meta Movie Gen 현재 상태:')
print('  - 발표: 2024년 10월')
print('  - 상태: 연구 단계 (Research Preview)')
print('  - 공개 API: 미제공')
print('  - 접근: 제한적 베타 테스트만 가능')
print()

print('3. 현재 사용 가능한 동영상 생성 API:')
print()
print('  A. Google Veo 2 (현재 루나 사용 중)')
print('     - 품질: 최고급 (1080p, 최대 2분)')
print('     - 접근: AI Studio API 키로 사용 가능')
print('     - 가격: 프리뷰 무료, 이후 유료')
print()
print('  B. Runway Gen-3 Alpha')
print('     - 품질: 고급 (720p, 최대 10초)')
print('     - 접근: Runway API 키 필요')
print('     - 가격: 초당 과금')
print()
print('  C. Kling AI')
print('     - 품질: 준수 (1080p, 최대 5초)')
print('     - 접근: API 키 필요')
print('     - 가격: 크레딧 기반')
print()
print('  D. Luma Dream Machine')
print('     - 품질: 준수 (720p, 5초)')
print('     - 접근: API 키 필요')
print('     - 가격: 무료 티어 제한적')
print()

print('4. 추천:')
print('  현재 Google Veo 2가 최선의 선택입니다.')
print('  - 품질이 가장 좋음')
print('  - 이미 GEMINI_API_KEY로 사용 중')
print('  - 긴 영상 생성 가능 (2분)')
print()

print('5. Meta Movie Gen 향후 계획:')
print('  - 공개 API가 출시되면 즉시 통합 가능')
print('  - 현재는 대기 목록 등록만 가능')
print('  - 예상 출시: 2025년 중반 이후')
print()
print('=' * 70)
