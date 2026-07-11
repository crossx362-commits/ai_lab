"""펫나 공유 백로그 — 적재 시점 분류 규칙.

회의 액션아이템 `회의_202607090345_3`("백로그 적재 규칙에 'DB/인증 접촉' 사전 분리 태그 도입")의 구현.

배경: 수리는 `supabase`·`migrations/`·`api/` 등 금지경로를 건드린 diff의 자동 병합을
거부한다(`petnna_dev_engine.FORBIDDEN_PATHS`). 그런데 신규 테이블·RLS를 요구하는 기획이
`대기`로 적재되면 수리는 그걸 집어 구현 → 병합 거부 → 재시도를 3회 반복한 뒤에야 보류한다.
실제로 `나무_20260708_1`(건강수첩)이 이 경로로 3회 실패했고, 그 기능은 결국 오너 승인
커밋으로 master에 들어갔다. 낭비된 사이클 3회 + 이틀 묵은 잔재 브랜치.

수리: 적재 시점에 판별해 사람 검토 트랙(`보류`)으로 보낸다. 수리는 `대기`만 집으므로
자동 루프에 아예 진입하지 않는다.

오탐 주의: 판별 범위는 회의가 명시한 것(신규 테이블·RLS·supabase.js·migration)으로만
좁힌다. "로그인"·"인증" 같은 흔한 UI 낱말을 넣으면 순수 디자인 과제까지 보류로 새어나간다.
"""
import json
import re
from datetime import datetime
from pathlib import Path

# 회의가 명시한 범위 — DB 스키마·RLS·supabase 직접 접촉·마이그레이션.
DB_AUTH_PATTERN = re.compile(
    r"supabase"
    r"|migration|마이그레이션"
    r"|\bRLS\b|row level security"
    r"|신규\s*테이블|테이블\s*추가|스키마\s*(변경|추가|마이그)"
    r"|\bschema\b"
    r"|api[_\s-]?key|시크릿|\bsecret\b",
    re.IGNORECASE,
)


def touches_db_auth(title: str, detail: str = "") -> bool:
    """DB/인증 계층을 요구해 수리 자동 루프가 병합할 수 없는 과제인가."""
    return bool(DB_AUTH_PATTERN.search(f"{title}\n{detail}"))


# 백로그를 실제로 소비하는 owner만. 여기 없는 owner(예: 나무 — 적재만 하고 안 읽음)로
# 배정된 항목은 '대기'로 만들어봤자 아무도 안 집는다. petnna_council.py가 이 상수를
# import해 쓴다(단일 소스 — 두 곳에 따로 정의하면 한쪽만 갱신돼 어긋난다, 2026-07-11 교훈).
# 새 에이전트를 추가하려면 먼저 그 도구에 백로그 소비 코드를 넣고 여기 추가할 것.
#   수리 select_backlog · 테오 _backlog_task · 미오 _assigned_tasks · 백호 investigate_assigned_tasks
AUTO_OWNERS = ("", "수리", "테오", "미오", "백호")


# ==================== 승인된 보류 항목 자동 승격 ====================
# 배경(2026-07-11): "개선 신규 아이디어는 누가 개발하냐" 질문 계기로 발견 — 나무_20260708_4
# (트리아지)·나무_20260709_3(QOL)이 오너 승인(approved_by)까지 났는데도 `보류`에 그대로
# 남아 수리 자동 루프(대기만 소비)가 절대 못 집었다. 실측 결과 touches_db_auth()는 둘 다
# False(DB/인증 무관 순수 프론트)였다 — 즉 정당한 이유 없이 보류에 갇힌 것. 반면 같은 시기
# 나무_20260709_0(웹 푸시, "Supabase reminders 테이블" 명시)은 True — 이건 계속 보류가 맞다.
# 오너 지시: "오너 승인된 건만 자동 재검토해 순수 프론트면 대기로 승격"(수동 라이브 세션
# 없이도 수리가 새 아이디어를 스스로 개발하게).
def promote_approved_holds(backlog_path) -> list[str]:
    """오너 승인(approved_by)됐지만 `보류`에 남은 항목을 재검토해, 재검토해도 여전히
    DB/인증을 접촉하지 않으면 `대기`로 승격(owner 미지정이면 수리로)한다.
    명시적 `gate` 필드(예: "DB/인증")가 있는 항목은 오너 승인 여부와 무관하게 절대
    건드리지 않는다 — 그런 항목은 사람이 물리적으로(SQL 콘솔 실행 등) 처리해야 한다.

    버그 발견·수정(2026-07-11, 파이프라인 재검토 중): council.py가 '보류'로 라우팅하는
    이유는 세 가지(승인필요·owner 불일치·DB/인증)인데 `gate` 필드는 DB/인증일 때만
    붙는다 — owner 불일치(예: 나무처럼 백로그를 안 읽는 owner)로 보류된 항목은 gate가
    없어 이 함수가 그냥 통과시켰다. 그러면 '대기'로 승격은 되는데 owner가 여전히
    소비자 없는 값이라 아무도 안 집는 좀비 상태가 된다 — 방치를 없앤 게 아니라 모양만
    바꾼 것. owner가 AUTO_OWNERS(백로그를 실제로 읽는 owner)에 속할 때만 승격한다."""
    path = Path(backlog_path)
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return []
    promoted: list[str] = []
    for it in data.get("items", []):
        if it.get("status") != "보류":
            continue
        if it.get("gate"):
            continue  # 하드 게이트 — 승인과 무관하게 사람 전용
        if not it.get("approved_by"):
            continue  # 오너 승인 없으면 승격 후보 아님
        if it.get("owner", "") not in AUTO_OWNERS:
            continue  # 소비자 없는 owner — 승격해도 아무도 안 집는다(좀비 방지)
        if touches_db_auth(it.get("title", ""), it.get("detail", "")):
            continue  # 재검토해도 DB/인증 접촉 — 보류 유지
        it["status"] = "대기"
        if not it.get("owner"):
            it["owner"] = "수리"
        it["updated"] = datetime.now().isoformat()
        it["promotion_note"] = "오너 승인 + 재검토(DB/인증 미접촉·소비자 있는 owner) 자동 승격"
        promoted.append(it.get("id", "?"))
    if promoted:
        path.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")
    return promoted
