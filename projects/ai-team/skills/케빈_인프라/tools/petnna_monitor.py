#!/usr/bin/env python3
"""
Petnna PWA 모니터링 도구
케빈이 Petnna 웹앱의 상태를 체크합니다.
"""

import os
import sys
import json
import urllib.request
import urllib.error
from pathlib import Path
from datetime import datetime

# 경로 설정
ROOT_DIR = Path(__file__).parent.parent.parent.parent.parent
PETNNA_DIR = ROOT_DIR / "projects" / "petnna"


def check_vercel_deployment():
    """Vercel 배포 상태 확인"""
    try:
        # Vercel 프로젝트 URL (환경변수 또는 설정에서 가져오기)
        # 일단 프로덕션 URL 체크
        url = "https://petnna.vercel.app"

        req = urllib.request.Request(url)
        with urllib.request.urlopen(req, timeout=10) as response:
            status_code = response.status
            content_length = response.headers.get('Content-Length', 'N/A')

            return {
                'status': 'ok' if status_code == 200 else 'warning',
                'code': status_code,
                'url': url,
                'content_length': content_length
            }

    except urllib.error.HTTPError as e:
        return {
            'status': 'error',
            'code': e.code,
            'message': f'HTTP Error: {e.reason}'
        }
    except urllib.error.URLError as e:
        return {
            'status': 'error',
            'message': f'URL Error: {str(e.reason)}'
        }
    except Exception as e:
        return {
            'status': 'error',
            'message': f'Error: {str(e)}'
        }


def check_local_files():
    """로컬 Petnna 파일 확인"""
    critical_files = [
        'index.html',
        'manifest.json',
        'sw.js',
        'js/app.js',
        'js/supabase.js',
        'css/style.css'
    ]

    results = {
        'total': len(critical_files),
        'found': 0,
        'missing': []
    }

    for file in critical_files:
        filepath = PETNNA_DIR / file
        if filepath.exists():
            results['found'] += 1
        else:
            results['missing'].append(file)

    results['status'] = 'ok' if results['found'] == results['total'] else 'warning'
    return results


def check_dependencies():
    """package.json 의존성 확인"""
    package_json = PETNNA_DIR / "package.json"

    if not package_json.exists():
        return {
            'status': 'warning',
            'message': 'package.json 없음'
        }

    try:
        with open(package_json, 'r', encoding='utf-8') as f:
            data = json.load(f)

        dependencies = data.get('dependencies', {})
        dev_dependencies = data.get('devDependencies', {})

        return {
            'status': 'ok',
            'dependencies': len(dependencies),
            'devDependencies': len(dev_dependencies),
            'total': len(dependencies) + len(dev_dependencies)
        }

    except Exception as e:
        return {
            'status': 'error',
            'message': f'package.json 읽기 실패: {str(e)}'
        }


def check_service_worker():
    """Service Worker 파일 확인"""
    sw_file = PETNNA_DIR / "sw.js"

    if not sw_file.exists():
        return {
            'status': 'error',
            'message': 'Service Worker 없음 (PWA 필수)'
        }

    try:
        content = sw_file.read_text(encoding='utf-8')
        lines = content.split('\n')

        # 캐시 이름 확인
        cache_names = [line for line in lines if 'CACHE' in line.upper() and '=' in line]

        return {
            'status': 'ok',
            'size': len(content),
            'lines': len(lines),
            'cache_config': len(cache_names) > 0
        }

    except Exception as e:
        return {
            'status': 'error',
            'message': f'Service Worker 읽기 실패: {str(e)}'
        }


def check_supabase_integration():
    """Supabase 통합 확인"""
    supabase_js = PETNNA_DIR / "js" / "supabase.js"

    if not supabase_js.exists():
        return {
            'status': 'error',
            'message': 'Supabase 클라이언트 파일 없음'
        }

    try:
        content = supabase_js.read_text(encoding='utf-8')

        checks = {
            'supabase_url': 'SUPABASE_URL' in content or 'supabaseUrl' in content,
            'supabase_key': 'SUPABASE_ANON_KEY' in content or 'supabaseKey' in content,
            'create_client': 'createClient' in content or 'supabase.createClient' in content
        }

        all_ok = all(checks.values())

        return {
            'status': 'ok' if all_ok else 'warning',
            'checks': checks,
            'size': len(content)
        }

    except Exception as e:
        return {
            'status': 'error',
            'message': f'Supabase.js 읽기 실패: {str(e)}'
        }


def generate_health_report():
    """Petnna 헬스 체크 보고서"""
    report = []
    report.append("🏥 **Petnna PWA 헬스 체크**\n")
    report.append(f"체크 시간: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}\n")

    # 1. Vercel 배포 상태
    report.append("**1. Vercel 배포 상태:**")
    deployment = check_vercel_deployment()
    if deployment['status'] == 'ok':
        report.append(f"  ✅ 배포 정상 (HTTP {deployment['code']})")
        report.append(f"  📍 URL: {deployment.get('url', 'N/A')}")
    else:
        report.append(f"  ❌ 배포 문제: {deployment.get('message', 'Unknown')}")

    # 2. 로컬 파일 확인
    report.append("\n**2. 핵심 파일 체크:**")
    files = check_local_files()
    report.append(f"  {'✅' if files['status'] == 'ok' else '⚠️'} {files['found']}/{files['total']} 파일 존재")
    if files['missing']:
        report.append("  누락된 파일:")
        for file in files['missing']:
            report.append(f"    - {file}")

    # 3. 의존성
    report.append("\n**3. 의존성:**")
    deps = check_dependencies()
    if deps['status'] == 'ok':
        report.append(f"  ✅ {deps['total']} 패키지")
        report.append(f"     - 일반: {deps['dependencies']}")
        report.append(f"     - 개발: {deps['devDependencies']}")
    else:
        report.append(f"  ⚠️ {deps.get('message', 'N/A')}")

    # 4. Service Worker (PWA 필수)
    report.append("\n**4. Service Worker (PWA):**")
    sw = check_service_worker()
    if sw['status'] == 'ok':
        report.append(f"  ✅ Service Worker 정상")
        report.append(f"     - 크기: {sw['size']} bytes")
        report.append(f"     - 라인 수: {sw['lines']}")
        report.append(f"     - 캐시 설정: {'✅' if sw['cache_config'] else '❌'}")
    else:
        report.append(f"  ❌ {sw.get('message', 'N/A')}")

    # 5. Supabase 통합
    report.append("\n**5. Supabase 통합:**")
    supabase = check_supabase_integration()
    if supabase['status'] == 'ok':
        checks = supabase.get('checks', {})
        report.append(f"  ✅ Supabase 클라이언트 설정 완료")
        report.append(f"     - URL 설정: {'✅' if checks.get('supabase_url') else '❌'}")
        report.append(f"     - Key 설정: {'✅' if checks.get('supabase_key') else '❌'}")
        report.append(f"     - Client 초기화: {'✅' if checks.get('create_client') else '❌'}")
    else:
        report.append(f"  ❌ {supabase.get('message', 'N/A')}")

    # 종합 판정
    report.append("\n**종합 판정:**")
    all_statuses = [deployment['status'], files['status'], deps['status'], sw['status'], supabase['status']]
    if all(s == 'ok' for s in all_statuses):
        report.append("  🎉 모든 시스템 정상!")
    elif 'error' in all_statuses:
        report.append("  🚨 긴급 조치 필요")
    else:
        report.append("  ⚠️ 일부 시스템 점검 필요")

    return "\n".join(report)


def quick_test():
    """빠른 테스트"""
    print("🔍 Petnna 빠른 체크...")

    # Vercel 배포
    deployment = check_vercel_deployment()
    print(f"\n배포: {'✅' if deployment['status'] == 'ok' else '❌'} {deployment.get('code', 'N/A')}")

    # 파일
    files = check_local_files()
    print(f"파일: {'✅' if files['status'] == 'ok' else '⚠️'} {files['found']}/{files['total']}")

    # Service Worker
    sw = check_service_worker()
    print(f"PWA: {'✅' if sw['status'] == 'ok' else '❌'}")

    # Supabase
    supabase = check_supabase_integration()
    print(f"DB: {'✅' if supabase['status'] == 'ok' else '❌'}")


def main():
    """CLI 인터페이스"""
    if len(sys.argv) < 2:
        print("사용법:")
        print("  python petnna_monitor.py health   - 전체 헬스 체크")
        print("  python petnna_monitor.py test     - 빠른 테스트")
        print("  python petnna_monitor.py deploy   - 배포 상태만 확인")
        return

    command = sys.argv[1].lower()

    if command == "health":
        print(generate_health_report())

    elif command == "test":
        quick_test()

    elif command == "deploy":
        deployment = check_vercel_deployment()
        if deployment['status'] == 'ok':
            print(f"✅ 배포 정상")
            print(f"   코드: {deployment['code']}")
            print(f"   URL: {deployment.get('url', 'N/A')}")
        else:
            print(f"❌ 배포 문제")
            print(f"   {deployment.get('message', 'Unknown')}")

    else:
        print(f"❌ 알 수 없는 명령어: {command}")


if __name__ == "__main__":
    main()
