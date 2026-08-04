"""펫나 E2E — 투약 이행 로그의 날짜 키가 **로컬** 기준인지.

2026-08-04 검토에서 잡은 버그: medication-log.js의 `_todayStr()`이
`new Date().toISOString().split('T')[0]`, 즉 **UTC** 날짜를 쓰고 있었다.
반면 연속일 계산(`_streak`)은 로컬 날짜(`_dateStr`)로 비교한다. KST(UTC+9)에서
두 값은 자정~오전 9시 사이에 하루 어긋난다.

그래서 아침 8시에 약을 먹이고 체크하면 그 기록이 **어제 날짜**로 저장됐다.
(petId, scheduleId, date)가 로컬 findIndex의 키이자 서버 UNIQUE 제약이므로,
어제 저녁에 남긴 기록을 그대로 덮어썼다 — 하루치가 통째로 사라지고 연속일·
이행률이 함께 틀어진다. 자동 검증을 전부 통과한다(테스트는 UTC 오후에 돌면
두 값이 같아서 아무 일도 안 일어난다).

이 테스트가 지키는 계약: 기록된 date가 **브라우저 로컬 날짜**와 같다.

시각을 고정하지 않으면 이 테스트는 무의미하다 — 낮에 돌리면 UTC와 로컬 날짜가
같아서 버그가 있어도 통과한다(처음 쓴 버전이 정확히 그랬다). 그래서 `Date`를
"로컬 날짜와 UTC 날짜가 서로 다른 순간"으로 고정한 뒤 기록한다. 브라우저 시간대가
UTC라 그런 순간이 없으면 검증이 성립하지 않으므로 명시적으로 건너뛴다.
"""
import json

NAME = "투약 로그 로컬 날짜 키"

_PET = {"id": 9911, "name": "메디", "type": "강아지", "breed": "믹스", "age": 3}
_SCHEDULE = {"id": "sch-e2e-med", "petId": 9911, "type": "medicine", "title": "심장사상충"}


def run(page, base_url):
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_med@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','health');"
        "localStorage.removeItem('petna_medication_logs');"
        "localStorage.setItem('petna_pets', %s);" % json.dumps(json.dumps([_PET]))
    )
    page.goto(base_url)
    page.wait_for_function("typeof MedicationLog !== 'undefined'", timeout=15000)

    result = page.evaluate(
        """(sch) => {
            const fmt = d => `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
            // 로컬 날짜와 UTC 날짜가 어긋나는 순간을 찾는다. 하루를 시간 단위로 훑어
            // 첫 불일치를 쓴다(UTC+9면 새벽, UTC-5면 저녁 — 시간대 부호와 무관하게 잡힌다).
            const base = new Date();
            let target = null;
            for (let h = 0; h < 24 && !target; h++) {
                const c = new Date(base.getFullYear(), base.getMonth(), base.getDate(), h, 30, 0);
                if (fmt(c) !== c.toISOString().split('T')[0]) target = c;
            }
            if (!target) return { skipped: true };

            const Real = Date;
            const fixed = target.getTime();
            // new Date() = 고정 시각, new Date(x) = 평소대로. Date.now()도 고정.
            function FakeDate(...a) { return a.length ? new Real(...a) : new Real(fixed); }
            FakeDate.prototype = Real.prototype;
            FakeDate.now = () => fixed;
            FakeDate.parse = Real.parse;
            FakeDate.UTC = Real.UTC;

            localStorage.removeItem('petna_medication_logs');
            const svc = window.SupabaseService;
            const origUpload = svc && svc.uploadMedicationLog;
            if (svc) svc.uploadMedicationLog = () => {};   // 실 세션 없음 — 업로드는 관심사 밖
            window.Date = FakeDate;
            let stored, streak;
            try {
                MedicationLog.record(sch, true);
                const logs = MedicationLog.getLogs();
                stored = logs.length ? logs[0].date : null;
                streak = MedicationLog.stats(sch.petId).streak;   // 연속일도 고정 시각 기준
            } finally {
                window.Date = Real;
                if (svc && origUpload) svc.uploadMedicationLog = origUpload;
            }
            return {
                skipped: false, stored, streak,
                local: fmt(target),
                utc: target.toISOString().split('T')[0],
                at: target.toString().slice(0, 24),
            };
        }""",
        _SCHEDULE,
    )

    if result.get("skipped"):
        raise AssertionError(
            "브라우저 시간대가 UTC라 로컬/UTC 날짜가 어긋나는 순간이 없다 — "
            "이 상태에선 검증이 성립하지 않으므로 통과로 처리하지 않는다"
        )

    assert result["stored"] == result["local"], (
        f"{result['at']} 기준으로 기록했더니 날짜가 로컬({result['local']})이 아니라 "
        f"{result['stored']}로 저장됐다 — UTC({result['utc']})를 쓰면 그 시간대의 "
        f"아침 투약이 전날 기록과 같은 키를 써서 덮어쓴다"
    )
    assert result["streak"] >= 1, (
        f"방금 기록했는데 연속일이 {result['streak']}일 — "
        f"기록 날짜와 연속일 계산의 기준이 어긋났다"
    )

    page.evaluate("() => localStorage.removeItem('petna_medication_logs')")
    return f"{result['at']} → 로컬 {result['local']}로 저장(UTC {result['utc']}), 연속일 {result['streak']}일"
