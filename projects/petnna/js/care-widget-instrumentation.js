// care-widget-instrumentation.js — 건강 탭 케어위젯 노출/클릭 최소 계측.
// 2026-07-16 개선회의(회의_202607162027_3) 결정 — 케어위젯 7종의 실사용 데이터가
// 없어 재구성 우선순위를 못 정한다는 지적에 따라, 이미 승인된 error_logs 파이프라인
// (AppLogger.addErrorLog, 로컬 50건 캡·원격 세션당 20건 캡·실패 무시)을 그대로 재사용해
// type만 'widget_view'/'widget_click'로 구분한다 — 새 테이블/마이그레이션 없음.
// 봄이 QA 순찰의 AppLogger.getErrorLogs() 흡수 루프가 이 타입들까지 P2 이슈로
// 오탐하지 않도록 petnna_qa_patrol.py에서 명시 제외 처리됨(같은 커밋).

(function () {
    const CARE_WIDGET_IDS = [
        'preventive-care-dashboard', 'med-adherence-tracker', 'qol-checkin-widget',
        'bcs-wizard-widget', 'calorie-tracker-widget', 'diet-recommend-widget',
        'daily-care-tip-widget', 'vet-cost-board-widget',
    ];

    function seenKey(kind, id) { return `petna_widget_${kind}_seen_${id}`; }

    function logOncePerSession(kind, id) {
        try {
            const key = seenKey(kind, id);
            if (sessionStorage.getItem(key)) return;
            sessionStorage.setItem(key, '1');
        } catch (e) { /* sessionStorage 불가 환경 — 계측 생략, 앱 동작에는 무해 */ return; }
        if (typeof AppLogger !== 'undefined' && AppLogger.addErrorLog) {
            AppLogger.addErrorLog(`widget_${kind}`, id, null);
        }
    }

    let observer = null;
    function observeCareWidgets() {
        const group = document.getElementById('care-widgets-group');
        if (!group) return;

        if (!observer) {
            observer = new IntersectionObserver((entries) => {
                entries.forEach((entry) => {
                    if (entry.isIntersecting) {
                        logOncePerSession('view', entry.target.id);
                        observer.unobserve(entry.target);
                    }
                });
            }, { threshold: 0.4 });
        }
        CARE_WIDGET_IDS.forEach((id) => {
            const el = document.getElementById(id);
            if (el) observer.observe(el);
        });

        // 클릭은 위젯마다 내부 마크업이 달라 위임 리스너 하나로 처리 —
        // 각 렌더 함수 내부를 건드리지 않는 wrapper-only 계측(오늘 UI 정리와 동일 원칙).
        // 위임은 탭 전체에 바인딩: 칼로리·식단·병원비 위젯이 우측 레일 과밀 해소로
        // 왼쪽 컬럼으로 이동(2026-07-21)해 그룹 밖에 있어도 계측이 유지되도록.
        const tab = document.getElementById('tab-health') || group;
        if (!tab._petnaClickBound) {
            tab._petnaClickBound = true;
            tab.addEventListener('click', (ev) => {
                const host = CARE_WIDGET_IDS
                    .map((id) => document.getElementById(id))
                    .find((el) => el && el.contains(ev.target));
                if (host) logOncePerSession('click', host.id);
            });
        }
    }

    // 건강 탭은 SPA 탭 전환이라 매번 renderHealthTab()이 다시 불린다 — 그 끝에서
    // 이 함수를 호출(health.js)해 재관측한다(IntersectionObserver.observe는 이미
    // 관측 중인 엘리먼트에 다시 호출해도 안전 — 스펙상 무시됨).
    window.observeCareWidgetsForInstrumentation = observeCareWidgets;
})();
