"""펫나 E2E — 건강기록(health_logs) Supabase 실배선 회귀 검증.

2026-07-16 개선회의(회의_202607162302_6) 결정 — medical_records만 커버되고
health_logs 실배선(커밋 1d847dcd)엔 대응 E2E가 없다는 지적에 따른 보완.

saveHealthHistoryToday()가 오늘 컨디션을 healthLogs.history에 반영하고, 캘린더
히트맵이 갱신되며, 동기화 훅(window.uploadHealthLogToSupabase)이 예외 없이
호출되는지를 화면 구조·가시성 기준으로 검증한다. 실 Supabase 계정이 없는
환경(auth.getUser()가 세션 없음을 반환)에서는 조용히 로컬 전용으로 남는 것이
정상 동작이므로, 실제 네트워크 성공에는 의존하지 않는다(flaky 방지).
"""
import json

NAME = "건강기록(health_logs) 동기화 훅 회귀"

_PET = {
    "id": 990717, "name": "동기화견", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "2살", "weight": "9", "gender": "여아",
    "personality": "차분", "hunger": 70, "happy": 80,
}


def run(page, base_url):
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_healthlogs@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','health');"
        "localStorage.setItem('petna_pets', %s);" % json.dumps(json.dumps([_PET]))
    )
    page.goto(base_url)

    page.wait_for_selector("#health-calendar-main", state="attached", timeout=15000)

    fns_ready = page.evaluate(
        "() => typeof window.saveHealthHistoryToday === 'function'"
        " && typeof window.uploadHealthLogToSupabase === 'function'"
    )
    assert fns_ready, "saveHealthHistoryToday/uploadHealthLogToSupabase 전역 함수가 없음"

    # === 오늘 컨디션 기록 → 히스토리 반영 + 동기화 훅 호출(예외 없이) ===
    result = page.evaluate(
        """() => {
            window.healthLogs = window.healthLogs || {};
            window.healthLogs.today = { food: 80, water: 200, poop: 'normal', condition: 'good' };
            let threw = null;
            try { window.saveHealthHistoryToday(); } catch (e) { threw = e.message; }
            const today = new Date().toISOString().split('T')[0];
            const entry = (window.healthLogs.history || []).find(h => h.date === today);
            return { threw, entryExists: !!entry, hasRemoteIdField: entry ? ('_remoteId' in entry) : false };
        }"""
    )
    assert result["threw"] is None, f"saveHealthHistoryToday()가 예외를 던짐: {result['threw']}"
    assert result["entryExists"], "오늘 컨디션이 healthLogs.history에 반영되지 않음"

    # 동기화 훅을 직접 호출해도(실 세션 없어 조용히 no-op) 예외 없이 끝나야 한다.
    upload_result = page.evaluate(
        """async () => {
            const today = new Date().toISOString().split('T')[0];
            const entry = window.healthLogs.history.find(h => h.date === today);
            try { await window.uploadHealthLogToSupabase(entry); return { threw: null }; }
            catch (e) { return { threw: e.message }; }
        }"""
    )
    assert upload_result["threw"] is None, \
        f"uploadHealthLogToSupabase()가 실 세션 없는 환경에서도 예외 없이 끝나야 함: {upload_result['threw']}"

    # === 캘린더 히트맵이 오늘 기록을 반영해 렌더되는지(화면 구조 기준) ===
    page.evaluate("() => window.renderHealthCalendarMain && window.renderHealthCalendarMain()")
    cal = page.locator("#health-calendar-main")
    cal_html = cal.inner_html()
    assert "bg-emerald-400" in cal_html, "캘린더에 기록 완료(초록) 셀이 없음 — 오늘 기록 반영 안 됨"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
