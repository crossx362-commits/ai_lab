"""펫나 E2E — 건강기록 동기화가 로컬 전용 필드를 지우지 않는지 검증(2026-07-28 사고).

오너 신고 "기록이 반영이 안되 있는데"의 원인:
SupabaseService.syncHealthLogs()가 원격 행으로 로컬 history 항목을 **통째로 교체**해서,
health_logs 테이블에 컬럼이 없는 필드가 동기화 한 번에 조용히 사라졌다.

해당 필드는 원탭 컨디션(daily-condition.js)이 쓰는 poopColor·urine·appetite·activity다
— 테이블은 2026-07-10에 만들어져 네 컬럼이 없고 uploadHealthLog도 안 올린다.
이게 지워지면 wellness-anomaly의 혈변(analyzeStoolColor)·혈뇨(analyzeUrine)·
식욕/활력 저하(analyzeCondition) 감지가 입력을 통째로 잃는다 — 즉 화면에서 기록이
사라질 뿐 아니라 건강 이상 알림이 조용히 무력화된다.

실 Supabase에 의존하지 않는다 — client/_authUser만 스텁하고 **실제 syncHealthLogs를
그대로 호출**한다. 스텁이 돌려주는 행은 마이그레이션(add_health_logs.sql)의 실제
컬럼 구성 그대로다(네 필드가 없는 것이 정상이며, 그게 이 회귀의 전제다).
"""
import datetime
import json

NAME = "건강기록 동기화 로컬 필드 보존"

_PET = {
    "id": 970728, "name": "동기화보존", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "3살", "weight": "6.4", "gender": "남아",
    "personality": "온순", "hunger": 70, "happy": 80,
}

# 원탭 컨디션만 쓰는(=테이블에 컬럼이 없는) 필드들
_LOCAL_ONLY = ["poopColor", "urine", "appetite", "activity"]


def run(page, base_url):
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_syncmerge@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','health');"
        # 주입한 펫이 원격 동기화로 덮이지 않게(2026-07-25 교훈)
        "localStorage.setItem('petna_demo_mode','1');"
        "localStorage.setItem('petna_pets', %s);" % json.dumps(json.dumps([_PET]))
    )
    page.goto(base_url)
    page.wait_for_selector("#health-calendar-main", state="attached", timeout=15000)

    today = datetime.date.today().isoformat()

    result = page.evaluate(
        """
        async (today) => {
          // 원탭 컨디션으로 이상 소견이 잡히는 상태를 만든다(붉은 변 + 진한 소변)
          healthLogs.today = { date: today, food: 250, water: 400, poop: 'normal',
                               poopColor: 'red', urine: 'dark', appetite: 'low', activity: 'low' };
          healthLogs.history = [ { ...healthLogs.today } ];

          const before = {
            stoolColor: !!analyzeStoolColor(healthLogs.history),
            urine: !!analyzeUrine(healthLogs.history),
          };

          // 실제 syncHealthLogs 호출 — client/_authUser/_withRetry만 스텁.
          // 응답 행은 add_health_logs.sql의 실제 컬럼 구성(네 필드 없음).
          const savedDemo = SupabaseService.isDemoMode;
          const savedAuth = SupabaseService._authUser;
          const savedRetry = SupabaseService._withRetry;
          const savedClient = SupabaseService.client;
          try {
            SupabaseService.isDemoMode = () => false;
            SupabaseService._authUser = async () => ({ id: 'u-e2e', email: 'e2e@petna.co.kr' });
            SupabaseService._withRetry = async (fn) => fn();
            SupabaseService.client = { from: () => ({ select: () => ({ eq: async () => ({
                data: [ { id: 4242, log_date: today, water: 400, food: 250,
                          poop: 'normal', condition: null } ],
                error: null }) }) }) };
            await SupabaseService.syncHealthLogs();
          } finally {
            SupabaseService.isDemoMode = savedDemo;
            SupabaseService._authUser = savedAuth;
            SupabaseService._withRetry = savedRetry;
            SupabaseService.client = savedClient;
          }

          const entry = (healthLogs.history || []).find(h => h.date === today) || {};
          return {
            entry,
            remoteIdApplied: entry._remoteId === 4242,
            before,
            after: {
              stoolColor: !!analyzeStoolColor(healthLogs.history),
              urine: !!analyzeUrine(healthLogs.history),
            },
          };
        }
        """,
        today,
    )

    entry = result["entry"]
    assert entry, "동기화 후 오늘 항목이 사라졌다"

    # ① 로컬 전용 필드가 살아있다 — 이 테스트의 본체
    missing = [k for k in _LOCAL_ONLY if entry.get(k) in (None, "")]
    assert not missing, (
        "동기화가 로컬 전용 필드를 지웠다: %s — syncHealthLogs가 원격 행으로 "
        "항목을 교체하고 있지 않은지 확인하라(병합이어야 한다). entry=%s" % (missing, entry)
    )

    # ② 원격이 실제로 반영되긴 해야 한다(병합이 '원격 무시'로 퇴화하면 안 됨)
    assert result["remoteIdApplied"], "원격 _remoteId가 반영되지 않았다 — 병합이 아니라 무시하고 있다"

    # ③ 이상감지가 입력을 잃지 않았다 — 사용자에게 실제로 보이는 결과
    assert result["before"]["stoolColor"] and result["before"]["urine"], \
        "전제 실패: 동기화 전에 이미 이상 소견이 없다(테스트 데이터 확인 필요)"
    assert result["after"]["stoolColor"], "동기화 후 혈변 의심 소견이 사라졌다"
    assert result["after"]["urine"], "동기화 후 진한 소변 소견이 사라졌다"


# 직접 실행 금지 가드(2026-08-11 사고) — 이 파일은 NAME/run() 만 정의하는 계약이라
# `python3 이파일.py` 로 돌리면 run()이 호출되지 않아 **아무것도 안 하고 exit 0**이 된다.
# 그 거짓 통과를 실제로 믿고 넘어간 적이 있어, 조용히 성공하는 대신 시끄럽게 죽인다.
if __name__ == "__main__":
    raise SystemExit(
        "이 파일은 직접 실행하지 않는다(run()이 호출되지 않아 항상 성공한다). "
        "python3 projects/petnna/tests/e2e/run_e2e.py 로 실행하라."
    )
