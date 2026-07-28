"""펫나 E2E — 원탭 컨디션 4필드의 서버 왕복 (마이그레이션 유무 양쪽, 2026-07-28 오너 승인).

migrations/add_health_logs_condition_columns.sql이 health_logs에 poop_color·urine·
appetite·activity를 추가한다. 그런데 마이그레이션 실행 시점과 프론트 배포 시점은
서로 독립이라, **컬럼이 아직 없는 상태에서 새 코드가 도는 구간**이 반드시 생긴다.
그 구간에 업로드가 통째로 실패하면 식사·음수·배변까지 같이 못 올라간다.

세 가지를 본다:
  1) 컬럼이 있으면 네 필드가 그대로 실려 올라간다.
  2) 컬럼이 없으면(PGRST204) 네 필드만 빼고 재시도해 **나머지는 정상 업로드**된다.
  3) 원격 행에 네 컬럼이 실려 오면 로컬 history에 병합된다(기기 간 동기화의 목적).

실 Supabase에 붙지 않는다 — client/_authUser만 스텁하고 실제 uploadHealthLog·
syncHealthLogs를 호출한다. 스텁이 흉내내는 오류는 PostgREST의 실제 형태다.
"""
import json

NAME = "원탭 컨디션 컬럼 서버 왕복"

_PET = {
    "id": 970744, "name": "컬럼", "breed": "믹스", "type": "dog",
    "imageUrl": "", "age": "3살", "weight": "6.4", "gender": "남아",
    "personality": "온순", "hunger": 70, "happy": 80,
}

_ENTRY_JS = """
  (mode) => {
    const sent = [];
    const saved = {
      demo: SupabaseService.isDemoMode, auth: SupabaseService._authUser,
      client: SupabaseService.client, warned: SupabaseService._warnedHealthLogColumns,
    };
    SupabaseService.isDemoMode = () => false;
    SupabaseService._authUser = async () => ({ id: 'u-e2e', email: 'e2e@petna.co.kr' });
    SupabaseService._warnedHealthLogColumns = false;
    SupabaseService.client = { from: () => ({
      upsert: async (rows) => {
        sent.push(rows[0]);
        // 마이그레이션 전이면 PostgREST가 스키마 캐시에 없는 컬럼을 거절한다.
        if (mode === 'missing' && 'poop_color' in rows[0]) {
          return { error: { code: 'PGRST204',
            message: "Could not find the 'poop_color' column of 'health_logs' in the schema cache" } };
        }
        return { error: null };
      },
    }) };
    const entry = { date: '2026-07-28', food: 200, water: 300, poop: 'normal',
                    poopColor: 'red', urine: 'dark', appetite: 'low', activity: 'low' };
    return SupabaseService.uploadHealthLog(entry).then(() => {
      SupabaseService.isDemoMode = saved.demo;
      SupabaseService._authUser = saved.auth;
      SupabaseService.client = saved.client;
      SupabaseService._warnedHealthLogColumns = saved.warned;
      return sent;
    });
  }
"""


def run(page, base_url):
    page.add_init_script(
        "localStorage.setItem('petna_is_logged_in','true');"
        "localStorage.setItem('petna_user_email','e2e_cols@petna.co.kr');"
        "localStorage.setItem('petna_active_tab','health');"
        "localStorage.setItem('petna_demo_mode','1');"
        "localStorage.setItem('petna_pets', %s);" % json.dumps(json.dumps([_PET]))
    )
    page.goto(base_url)
    page.wait_for_selector("#health-calendar-main", state="attached", timeout=15000)

    # === 1) 컬럼이 있는 정상 경로 — 한 번에 네 필드까지 올라간다 ===
    sent = page.evaluate(_ENTRY_JS, "ok")
    assert len(sent) == 1, f"정상 경로인데 업로드를 {len(sent)}회 시도했다(재시도 불필요): {sent}"
    row = sent[0]
    for col, want in (("poop_color", "red"), ("urine", "dark"),
                      ("appetite", "low"), ("activity", "low")):
        assert row.get(col) == want, f"{col}이 안 실렸다: {row}"
    assert row.get("food") == 200 and row.get("water") == 300, f"기존 필드가 빠졌다: {row}"

    # === 2) 컬럼이 없는 구간 — 네 필드만 빼고 재시도해 나머지는 올라간다 ===
    sent = page.evaluate(_ENTRY_JS, "missing")
    assert len(sent) == 2, \
        f"컬럼 부재 시 폴백 재시도가 없었다(업로드 {len(sent)}회) — 나머지 필드까지 유실된다: {sent}"
    retry = sent[1]
    for col in ("poop_color", "urine", "appetite", "activity"):
        assert col not in retry, f"재시도에 {col}이 아직 남아 있다 — 또 거절된다: {retry}"
    assert retry.get("food") == 200 and retry.get("water") == 300 and retry.get("poop") == "normal", \
        f"폴백 재시도에서 기존 필드가 빠졌다: {retry}"

    # === 3) 원격이 네 컬럼을 실어 보내면 로컬 history에 병합된다 ===
    merged = page.evaluate(
        """
        async () => {
          healthLogs.today = { date: '2026-07-28' };
          healthLogs.history = [{ date: '2026-07-28', food: 10 }];
          const saved = { demo: SupabaseService.isDemoMode, auth: SupabaseService._authUser,
                          retry: SupabaseService._withRetry, client: SupabaseService.client };
          try {
            SupabaseService.isDemoMode = () => false;
            SupabaseService._authUser = async () => ({ id: 'u-e2e', email: 'e2e@petna.co.kr' });
            SupabaseService._withRetry = async (fn) => fn();
            SupabaseService.client = { from: () => ({ select: () => ({ eq: async () => ({
              data: [{ id: 77, log_date: '2026-07-28', water: 300, food: 200, poop: 'normal',
                       condition: null, poop_color: 'black', urine: 'red',
                       appetite: 'good', activity: 'high' }],
              error: null }) }) }) };
            await SupabaseService.syncHealthLogs();
          } finally {
            SupabaseService.isDemoMode = saved.demo;
            SupabaseService._authUser = saved.auth;
            SupabaseService._withRetry = saved.retry;
            SupabaseService.client = saved.client;
          }
          return (healthLogs.history || []).find(h => h.date === '2026-07-28') || null;
        }
        """
    )
    assert merged, "동기화 후 항목이 사라졌다"
    for field, want in (("poopColor", "black"), ("urine", "red"),
                        ("appetite", "good"), ("activity", "high")):
        assert merged.get(field) == want, \
            f"원격의 {field}가 로컬에 안 들어왔다 — 기기 간 동기화가 안 된다: {merged}"
    assert merged.get("food") == 200, f"원격 food가 반영되지 않았다: {merged}"
