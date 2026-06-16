#!/usr/bin/env python3
"""
petnna_monitor.py — 케빈: Petnna PWA 가용성 실시간 모니터링
SKILL.md Mission 3 반영:
  🚨 Critical → 즉시 텔레그램 알림 (서버 다운, 로그인 실패, DB 단절)
  ⚠️ Warning  → 데일리 요약 리포트에 포함 (에셋 누락, 레이턴시 3000ms 초과)
  ✅ Normal   → 백그라운드 로그만 적재
"""
import os
import sys
import time
import urllib.request
import urllib.error
import datetime
from pathlib import Path

# UTF-8 인코딩 설정
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
    sys.stderr.reconfigure(encoding='utf-8', errors='replace')

_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", "..", ".."))
_project_root = os.path.abspath(os.path.join(_ai_team_root, "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)

from _shared.env_loader import load_env
from _shared.telegram_notifier import send_telegram_message

load_env()

PETNNA_DIR = Path(_project_root) / "projects" / "petnna"
PETNNA_URL = os.getenv("PETNNA_URL", "https://petnna.vercel.app")
LATENCY_WARNING_MS = 3000  # SKILL.md Mission 3 — Warning 임계치


# ── 체크 함수들 ───────────────────────────────────────────────────────────────

def check_vercel_deployment() -> dict:
    """Vercel 배포 상태 + 응답 레이턴시 측정."""
    try:
        start = time.time()
        req = urllib.request.Request(PETNNA_URL)
        with urllib.request.urlopen(req, timeout=10) as r:
            latency_ms = int((time.time() - start) * 1000)
            code = r.status
        severity = "normal" if code == 200 and latency_ms < LATENCY_WARNING_MS else (
            "warning" if code == 200 else "critical"
        )
        return {"severity": severity, "code": code, "latency_ms": latency_ms, "url": PETNNA_URL}
    except urllib.error.HTTPError as e:
        return {"severity": "critical", "code": e.code, "message": f"HTTP {e.code}: {e.reason}"}
    except Exception as e:
        return {"severity": "critical", "message": f"연결 실패: {e}"}


def check_pwa_assets() -> dict:
    """PWA 핵심 자산 존재 여부 확인."""
    critical_files = ["index.html", "manifest.json", "sw.js", "js/app.js", "js/supabase.js", "css/style.css"]
    missing = [f for f in critical_files if not (PETNNA_DIR / f).exists()]
    severity = "normal" if not missing else ("critical" if "manifest.json" in missing or "sw.js" in missing else "warning")
    return {"severity": severity, "total": len(critical_files), "missing": missing}


def check_service_worker() -> dict:
    """Service Worker 파일 및 캐시 설정 확인."""
    sw_file = PETNNA_DIR / "sw.js"
    if not sw_file.exists():
        return {"severity": "critical", "message": "sw.js 없음 — PWA 오프라인 캐싱 불가"}
    try:
        content = sw_file.read_text(encoding="utf-8")
        cache_ok = any("CACHE" in line.upper() and "=" in line for line in content.splitlines())
        return {"severity": "normal", "size": len(content), "cache_config": cache_ok}
    except Exception as e:
        return {"severity": "warning", "message": f"sw.js 읽기 실패: {e}"}


def check_supabase_integration() -> dict:
    """Supabase 클라이언트 파일 + Auth 설정 확인."""
    f = PETNNA_DIR / "js" / "supabase.js"
    if not f.exists():
        return {"severity": "critical", "message": "supabase.js 없음 — DB 연결 불가"}
    try:
        content = f.read_text(encoding="utf-8")
        checks = {
            "url":    "SUPABASE_URL" in content or "supabaseUrl" in content,
            "key":    "SUPABASE_ANON_KEY" in content or "supabaseKey" in content,
            "client": "createClient" in content,
        }
        if not all(checks.values()):
            return {"severity": "critical", "checks": checks}
    except Exception as e:
        return {"severity": "warning", "message": f"읽기 실패: {e}"}

    # index.html의 window._env_ 주입 여부 확인
    idx = PETNNA_DIR / "index.html"
    try:
        html = idx.read_text(encoding="utf-8")
        import re
        m = re.search(r'"SUPABASE_URL":\s*"([^"]+)"', html)
        url_injected = bool(m and m.group(1).startswith("https://"))
        if not url_injected:
            return {"severity": "critical", "message": "index.html window._env_ SUPABASE_URL 미주입 — 로그인 불가"}
    except Exception:
        pass

    # Note: Site URL 설정은 Dashboard에서 수동 확인 필요
    # /auth/v1/settings 엔드포인트가 site_url 필드를 반환하지 않을 수 있음
    # Dashboard → Authentication → URL Configuration에서 설정됨

    return {"severity": "normal", "checks": checks}


# ── 보고 함수 ─────────────────────────────────────────────────────────────────

def generate_health_report() -> tuple[str, str]:
    """
    전체 헬스 체크 보고서 생성.
    Returns (report_text, overall_severity: 'critical'|'warning'|'normal')
    """
    now = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    results = {
        "deployment": check_vercel_deployment(),
        "assets":     check_pwa_assets(),
        "sw":         check_service_worker(),
        "supabase":   check_supabase_integration(),
    }

    severities = [r["severity"] for r in results.values()]
    if "critical" in severities:
        overall = "critical"
    elif "warning" in severities:
        overall = "warning"
    else:
        overall = "normal"

    d, a, sw, sb = results["deployment"], results["assets"], results["sw"], results["supabase"]

    lines = [
        f"🏥 <b>[케빈] Petnna PWA 헬스 체크</b>",
        f"📅 {now}",
        "",
        "1. Vercel 배포:",
        f"  {'✅' if d['severity']=='normal' else ('⚠️' if d['severity']=='warning' else '🚨')} HTTP {d.get('code','?')} | {d.get('latency_ms','?')}ms",
        "",
        "2. PWA 핵심 파일:",
        f"  {'✅' if not a['missing'] else '🚨'} {a['total'] - len(a['missing'])}/{a['total']} 존재",
    ]
    if a["missing"]:
        lines.append(f"  누락: {', '.join(a['missing'])}")

    sw_detail = sw.get("message") or f"{sw.get('size','?')}bytes, 캐시={'✅' if sw.get('cache_config') else '❌'}"
    sb_detail = sb.get("message") or str(sb.get("checks", ""))
    lines += [
        "",
        "3. Service Worker:",
        f"  {'✅' if sw['severity']=='normal' else '🚨'} {sw_detail}",
        "",
        "4. Supabase 통합:",
        f"  {'✅' if sb['severity']=='normal' else '🚨'} {sb_detail}",
        "",
        f"종합: {'🚨 긴급 조치 필요' if overall=='critical' else ('⚠️ 점검 권장' if overall=='warning' else '🎉 모든 시스템 정상')}",
    ]

    return "\n".join(lines), overall


def quick_test():
    """빠른 헬스 테스트 — CLI 출력 전용."""
    d  = check_vercel_deployment()
    a  = check_pwa_assets()
    sw = check_service_worker()
    sb = check_supabase_integration()
    print(f"배포: {'✅' if d['severity']=='normal' else '❌'} {d.get('code','?')} ({d.get('latency_ms','?')}ms)")
    print(f"파일: {'✅' if not a['missing'] else '⚠️'} {a['total']-len(a['missing'])}/{a['total']}")
    print(f"PWA:  {'✅' if sw['severity']=='normal' else '❌'}")
    print(f"DB:   {'✅' if sb['severity']=='normal' else '❌'}")


# ── CLI ───────────────────────────────────────────────────────────────────────

def main():
    if len(sys.argv) < 2:
        print("사용법:")
        print("  python petnna_monitor.py health   - 전체 헬스 체크 + 텔레그램 보고")
        print("  python petnna_monitor.py test     - 빠른 테스트 (콘솔만)")
        print("  python petnna_monitor.py deploy   - 배포 상태만 확인")
        return

    cmd = sys.argv[1].lower()

    if cmd == "health":
        report, severity = generate_health_report()
        print(report.replace("<b>", "").replace("</b>", ""))
        # SKILL.md: Critical → 즉시 전송, Warning → 데일리 포함, Normal → 로그만
        if severity == "critical":
            send_telegram_message(f"🚨 <b>[케빈] CRITICAL</b>\n{report}")
        elif severity == "warning":
            send_telegram_message(f"⚠️ <b>[케빈] WARNING</b>\n{report}")
        else:
            print("✅ 정상 — 텔레그램 알림 생략 (SKILL.md: Normal = 로그만)")

    elif cmd == "test":
        quick_test()

    elif cmd == "deploy":
        d = check_vercel_deployment()
        icon = "✅" if d["severity"] == "normal" else ("⚠️" if d["severity"] == "warning" else "🚨")
        print(f"{icon} {d.get('code','?')} | {d.get('latency_ms','?')}ms | {d.get('url', d.get('message',''))}")

    else:
        print(f"❌ 알 수 없는 명령어: {cmd}")


if __name__ == "__main__":
    main()
