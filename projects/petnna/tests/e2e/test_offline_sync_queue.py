"""펫나 E2E — 오프라인 기록 안전저장 + 재연결 자동 동기화 회귀 검증.

P2 기능(회의 제안): 통신 끊김 중 산책·건강기록을 localStorage 큐에 안전 저장하고,
재연결 시 자동으로 재전송한 뒤 진행/완료 상태를 배지에 표시한다(offline-sync.js).

과거 Supabase 동기화 유실 사고 이력이 있어, '오프라인에 저장된 기록이 재연결 시
큐에서 실제로 비워지는가'를 결정론적으로 확인한다. 실 Supabase 세션이 없어도 upload
함수는 조용히 no-op이고 flush는 온라인에서 큐를 비우므로 네트워크 성공에 의존하지 않는다.
"""

NAME = "오프라인 기록 큐 + 재연결 자동 동기화"


def run(page, base_url):
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_offline@petna.co.kr');"
        "localStorage.removeItem('petna_offline_queue');"
    )
    page.goto(base_url)

    page.wait_for_function(
        "() => typeof window.OfflineSync !== 'undefined'"
        " && typeof window.uploadHealthLogToSupabase === 'function'",
        timeout=15000,
    )

    # === 오프라인 전환 → 실제 업로드 경로(supabase.js 가드)가 큐로 우회하는지 ===
    page.context.set_offline(True)
    enq = page.evaluate(
        """async () => {
            await window.uploadHealthLogToSupabase(
                { date: '2026-08-05', water: 200, food: 80, condition: 'good' });
            return { count: window.OfflineSync.count() };
        }"""
    )
    assert enq["count"] == 1, \
        f"오프라인 건강기록이 큐에 안전 저장되지 않음 — count={enq['count']}"

    # 오프라인 상태 배지가 노출되는지(도달성 확인)
    badge = page.locator("#offline-sync-status")
    assert badge.count() == 1, "오프라인 상태 배지가 DOM에 없음"
    assert "저장" in badge.inner_text(), f"오프라인 배지 문구가 예상과 다름: {badge.inner_text()!r}"

    # === 재연결 → 자동 flush로 큐가 비워지는지 ===
    page.context.set_offline(False)
    # 'online' 이벤트로 자동 flush가 돌지만, 이벤트 타이밍에 의존하지 않도록 명시 대기
    page.wait_for_function(
        "() => window.OfflineSync.count() === 0",
        timeout=15000,
    )
    assert page.evaluate("() => window.OfflineSync.count()") == 0, \
        "재연결 후에도 큐가 비워지지 않음 — 자동 동기화 실패"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
