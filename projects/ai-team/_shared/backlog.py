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
import re

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
