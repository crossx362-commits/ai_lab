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
import sys
import time
from contextlib import contextmanager
from datetime import datetime
from pathlib import Path

# 회의가 명시한 범위 — DB 스키마·RLS·supabase 직접 접촉·마이그레이션.
#
# 'supabase'를 맨몸으로 매칭하면 **코드 식별자를 언급만 해도** 보류로 샌다.
# 2026-07-27 실사고: 3차 회의의 프론트 수정 3건(피드 재진입 가드·체중 입력 검증·
# 오프라인 표기)이 설명문에 `supabase.js:385`·`SupabaseService.isConnected`·
# `updatePetInSupabase`를 인용했다는 이유만으로 전부 자동 보류돼, 수리가 못 집고
# 하루를 넘겼다. 셋 다 스키마 무접촉이다.
# 다만 'supabase.js'를 통째로 빼면 안 된다 — 'supabase.js 쿼리 추가'는 진짜 데이터
# 계층 작업이고 기존 테스트가 이를 지킨다(제외 범위를 넓혔다가 그 테스트가 잡아냈다).
# 구분 기준은 **인용이냐 행위냐**다:
#   · 제외: `supabase.js:385` 같은 파일:줄 인용, SupabaseService/SupabaseClient/
#           ...InSupabase 같은 클라이언트 식별자 → 코드를 가리키는 말
#   · 유지: 그 밖의 모든 'supabase' 언급 → 데이터 계층을 건드리겠다는 말
def touches_db_auth(title: str, detail: str = "") -> bool:
    """이 과제가 DB/인증을 건드리는가 — 건드리면 자동 루프 밖(사람 검토)으로 보낸다.

    신호를 강/약으로 나눈다(2026-08-04). 순수 키워드 매칭이던 시절엔 다음을 구분하지
    못해 멀쩡한 과제가 영구 보류로 갔다:
      · "localStorage 로컬 완결, Supabase 동기화 경로 미접촉" — 작성자가 안 건드린다고
        못 박았는데 'Supabase' 한 단어에 걸렸다.
      · "기존 저장펫은 빈 배열 폴백 마이그레이션" — DB 마이그레이션이 아니라 로컬
        자료구조 변경인데 '마이그레이션'에 걸렸다.

    강 신호(신규 테이블·스키마 변경·RLS·시크릿)는 부정문이 있어도 그대로 막는다 —
    지나치게 넓게 제외하면 진짜 DB 작업이 자동 루프로 새어 들어간다(2026-07-11 교훈).
    약 신호(supabase/마이그레이션 단어만)는 명시적 '미접촉' 선언이 있으면 무시한다.
    """
    text = f"{title}\n{detail}"
    if _DB_STRONG.search(text):
        return True
    if _DB_NO_CONTACT.search(text):
        return False
    return bool(_DB_WEAK.search(text))


# 강 신호 — 부정문이 있어도 사람 검토로 보낸다.
_DB_STRONG = re.compile(
    r"신규\s*테이블|테이블\s*추가|테이블.{0,4}신설|스키마\s*(변경|추가|마이그)"
    r"|\bRLS\b|row level security|\bschema\b"
    r"|api[_\s-]?key|시크릿|\bsecret\b",
    re.IGNORECASE,
)

# 약 신호 — 단어만으로는 DB 변경인지 알 수 없다(조회일 수도, 로컬 마이그레이션일 수도).
_DB_WEAK = re.compile(
    r"(?<![A-Za-z])supabase(?!\.js:\d|Service|Client)|migration|마이그레이션",
    re.IGNORECASE,
)

# "DB는 안 건드린다"는 명시적 선언 — 약 신호를 무력화한다.
_DB_NO_CONTACT = re.compile(
    r"(?:supabase|db|디비|서버|원격)[^.\n]{0,24}(?:미접촉|무접촉|접촉\s*없|건드리지\s*않|연동\s*없)"
    r"|localstorage[^.\n]{0,20}(?:로컬\s*)?완결",
    re.IGNORECASE,
)


# 백로그 정규 상태 어휘 — 이 넷만 소비자(수리·예원·council)가 읽고 옮긴다.
# 어휘 밖 값(예: '진행'·'완료(부분)')은 어떤 도구도 안 읽어 항목이 영구 정지한다
# ("'진행' 상태는 아무도 안 읽는 무덤" 교훈, CLAUDE.md). 코드는 이 값을 만들지 않지만
# 수동 편집으로 새면 조용히 방치되므로, 신선도 감사가 noncanonical_items()로 감지·경보한다
# (비변경 — 데이터를 바꾸지 않고 유령만 드러낸다).
CANONICAL_STATUSES = ("대기", "보류", "PR대기", "완료")


def noncanonical_items(items: list) -> list:
    """정규 상태 어휘 밖 status를 가진 백로그 항목 — 소비자가 없어 정지한 유령."""
    return [it for it in (items or [])
            if isinstance(it, dict) and it.get("status") not in CANONICAL_STATUSES]


def canonical_target(status) -> str | None:
    """비어휘 상태를 안전하게 정규화할 대상 정규 상태. 확신 없으면 None(사람 확인 필요).

    '완료' 변형('완료(부분)'·'완료됨' 등)만 자동 정규화한다 — 종결이라 자동 루프에 다시
    진입하지 않아 안전하고, 이미 정지한 항목이라 잃을 작업도 없다. '진행'처럼 활성 루프로
    되돌려야 할 수 있는 값은 owner 트랙을 잘못 바꿀 위험이 있어 자동화하지 않고 경보한다."""
    if isinstance(status, str) and status.startswith("완료"):
        return "완료"
    return None


# owner가 실제로 소비하는 type 제약(자동 파이프라인 감사 도구가 발견, 2026-07-11).
# 테오 _backlog_task()는 type=='테스트'만, 미오 _assigned_tasks()는 type=='디자인'만 본다 —
# 그런데 council.needs_human()은 owner만 보고 type은 안 봐서, owner=테오인데 type이
# '테스트'가 아닌 항목이 '대기'로 적재되면 아무도 못 집는 좀비가 된다(현재 실사례는 없지만
# 재현 가능한 잠재 결함). 여기 없는 owner(백호·수리)는 type 필터가 없어 제약 없음.
OWNER_ALLOWED_TYPES = {
    "테오": {"테스트"},
    "미오": {"디자인"},
}


def owner_type_mismatch(owner: str, item_type: str) -> bool:
    """이 owner가 실제로 소비할 수 없는 type으로 배정됐는가(적재/라우팅 판정용).
    item_type이 비어있으면(호출부가 아직 type을 안 정한 시점) 판단 보류 — 있는데
    안 맞을 때만 True. 관대한 쪽으로 기본값을 둔 이유: needs_human()이 이걸 쓰는데,
    type을 아직 모른다고 무조건 사람 트랙으로 보내면 정상 항목까지 새어나간다."""
    if not item_type:
        return False
    allowed = OWNER_ALLOWED_TYPES.get(owner)
    return bool(allowed) and item_type not in allowed


# 미오는 리뷰·시안 산출물만 만든다 — petnna_design_review.py의 run_claude 호출은
# allowed_tools="Read,WebSearch,WebFetch"뿐이라 코드/템플릿을 절대 못 고친다. owner=미오
# +type=디자인은 owner_type_mismatch() 기준으론 "정상 배정"이지만, 배정 과제의 내용
# 자체가 이미 결정된 구현 착수를 요구하면(시안·검토 요청이 아니라) 미오가 리뷰 한 번
# 돌리고 완료 처리해도 실제 코드 변경은 영영 일어나지 않는다(2026-07-16 사고: 회의가
# "커밋 61505944와 동일 패턴으로 착수하라"고 배정한 케어위젯 재그룹화 과제가 미오 손에서
# 완료 처리됐지만 커밋도 dev_state 항목도 없었다 — 결국 사람이 수동 구현). "착수"·"구현"은
# 실행 지시 언어라 리뷰 요청 문장에는 쓰이지 않는다 — 실제 백로그 74건의 미오 자체 제안
# (source=미오, owner 없음)을 전수 확인해 오탐 0건 확인(2026-07-16).
MIO_EXECUTION_PATTERN = re.compile(r"착수|구현하|구현할|구현해")


def mio_implementation_task(title: str, detail: str = "") -> bool:
    """미오에게 배정된 과제가 리뷰·시안이 아니라 실제 구현 착수를 요구하는가."""
    return bool(MIO_EXECUTION_PATTERN.search(f"{title}\n{detail}"))


# 백로그를 실제로 소비하는 owner만. 여기 없는 owner(예: 나무 — 적재만 하고 안 읽음)로
# 배정된 항목은 '대기'로 만들어봤자 아무도 안 집는다. petnna_council.py가 이 상수를
# import해 쓴다(단일 소스 — 두 곳에 따로 정의하면 한쪽만 갱신돼 어긋난다, 2026-07-11 교훈).
# 새 에이전트를 추가하려면 먼저 그 도구에 백로그 소비 코드를 넣고 여기 추가할 것.
#   수리 select_backlog · 테오 _backlog_task · 미오 _assigned_tasks · 백호 investigate_assigned_tasks
AUTO_OWNERS = ("", "수리", "테오", "미오", "백호")


def structurally_blocked(owner: str, item_type: str, title: str, detail: str = "") -> bool:
    """오너 승인 여부와 무관하게 항상 사람 검토가 필요한 사유(owner 소비자 없음·owner+type
    불일치·DB/인증 접촉). '[승인필요]' 태그는 여기 포함하지 않는다 — 그건 승인되면 정상적으로
    풀려야 하는 사유라 needs_human()에서만 별도로 본다.

    이 함수를 만든 이유(2026-07-11, 파이프라인 감사 도구가 2번째로 발견): needs_human()과
    promote_approved_holds()가 "보류 사유"를 각자 따로 나열하다 보니, needs_human()에
    owner_type_mismatch를 추가했을 때 promote_approved_holds()는 갱신을 까먹어 또 같은
    계열의 좀비(owner=테오+type=디자인 같은 배정이 승인만 받으면 승격되지만 여전히 아무도
    못 집는 상태)가 재발했다 — 2026-07-10 owner-불일치 사고와 동일한 실수의 반복. 사유
    나열을 이 함수 하나로 단일화해 두 호출부가 항상 같은 판정을 보게 한다.

    2026-07-16 추가: owner=미오+type=디자인은 카테고리상 정상 배정이어도, 내용이 실제
    구현 착수를 요구하면 미오가 못 해낼 일을 떠맡는 것이다(mio_implementation_task)."""
    return (owner not in AUTO_OWNERS
            or owner_type_mismatch(owner, item_type)
            or touches_db_auth(title, detail)
            or (owner == "미오" and mio_implementation_task(title, detail)))


def needs_human(title: str, owner: str, detail: str = "", item_type: str = "") -> bool:
    """자동 루프가 집으면 안 되는 항목인가(적재 시점 판정 — petnna_council.py가 씀).

    ①승인 필요 태그 ②구조적 차단(structurally_blocked: owner 소비자 없음·type 불일치·
    DB/인증). item_type을 안 넘기면 owner_type_mismatch는 건너뛴다(하위호환)."""
    return "[승인필요]" in title or structurally_blocked(owner, item_type, title, detail)


# ==================== 최근 결정 참고(디자인 진자 방지) ====================
# 배경(2026-07-14): 미오가 매주기 스크린샷만 보고 새로 진단하다 보니, 바로 며칠 전
# 예원이 승인·병합한 결정("회원가입 버튼을 인라인으로 합침")을 기억 없이 정반대로
# 되돌리는 제안("다시 분리형 버튼으로")을 내는 일이 반복됐다(오너 발견). 예원의 리뷰도
# diff 하나만 보고 과거 맥락 없이 판단해 이런 반전을 그대로 승인해버린다. 근본 원인은
# 두 에이전트 다 "최근 무엇이 왜 결정됐는지"에 접근할 방법이 없었다는 것 — 이 함수로
# 백로그의 완료·보류 이력을 양쪽(제안 생성 시 미오, 최종 판단 시 예원)에 공유한다.

def recent_reviewed_items(backlog_path, limit: int = 8, item_type: str = "",
                           exclude_id: str = "") -> list[dict]:
    """최근 검토 완료(완료·보류)된 백로그 항목 — updated 내림차순.

    item_type을 넘기면 그 타입만("디자인"/"기획") 필터링. exclude_id는 지금 판단 중인
    항목 자신을 결과에서 빼기 위함(자기 자신과 비교하는 무의미한 경우 방지)."""
    try:
        data = json.loads(Path(backlog_path).read_text(encoding="utf-8"))
    except (FileNotFoundError, json.JSONDecodeError):
        return []
    items = [it for it in data.get("items", [])
             if it.get("status") in ("완료", "보류") and it.get("id") != exclude_id]
    if item_type:
        items = [it for it in items if it.get("type") == item_type]
    items.sort(key=lambda it: it.get("updated", ""), reverse=True)
    return items[:limit]


def format_recent_decisions(items: list[dict]) -> str:
    """recent_reviewed_items() 결과를 LLM 프롬프트에 넣을 텍스트 블록으로 변환.
    항목이 없으면 빈 문자열(프롬프트에 불필요한 섹션 추가 안 함)."""
    if not items:
        return ""
    lines = ["[최근 검토된 관련 결정 — 이미 정해진 방향을 기억 없이 뒤집는 제안·승인 금지]"]
    for it in items:
        status = it.get("status", "")
        reason = it.get("review_reason", "")
        line = f"- [{status}] {it.get('title', '')}"
        if reason:
            line += f" (사유: {reason})"
        lines.append(line)
    return "\n".join(lines)


# ==================== 배정 과제 재시도 상한(공용) ====================
# 배경(2026-07-11, 자동 파이프라인 감사 도구): 테오에 이 로직을 처음 넣을 때는
# petnna_test_engineer.py 안에 직접 구현했는데, 곧바로 백호도 investigate_assigned_tasks()에
# 동일한 문제(재시도 상한 없이 300초마다 무한 재조사)가 있다는 게 드러났다. 에이전트마다
# 각자 구현하면 이번처럼 하나씩 순서대로 발견되고, 그사이 구현도 조금씩 어긋난다
# (수리는 attempts 감소, 테오는 별도 함수) — 여기 하나로 모아 새 에이전트가 배정 과제를
# 소비할 때 이 함수만 재사용하면 되게 한다.
TASK_MAX_ATTEMPTS = 3
# 과제 탓이 아닌 실패 사유 — attempts에 반영하면 안 된다.
#
# 2026-08-04에 두 종류가 빠져 있는 걸 확인했다. 둘 다 실제로 과제를 죽였다:
#  · 구독 클로드 토큰 만료 — 2026-07-27~28 36시간 동안 수리가 27회 실패했고, 그 실패가
#    미오 과제 2건의 시도 3회를 통째로 소진해 영구 보류로 밀어냈다. 어제 '되살리는 쪽'
#    (select_backlog의 attempts 리셋)만 고치고 '애초에 안 깎이게' 하는 쪽은 안 고쳤다.
#  · 포트 충돌(Errno 48) — 사람이 임시로 E2E/QA를 돌리면 데몬의 정시 사이클과 포트가
#    겹쳐 사이클 하나가 통째로 죽는다(2026-08-04 20:01 실측). 과제 품질과 무관하다.
INFRA_FAILURE_KEYWORDS = (
    "미발견", "타임아웃", "rate limit", "429", "529", "verloaded",
    "Address already in use", "Errno 48",
    "token has expired", "Failed to authenticate", "Re-authenticate",
)


def is_infra_failure(text: str) -> bool:
    """CLI 부재·타임아웃·레이트리밋 같은 인프라 사유인가 — 과제 자체의 잘못이 아니므로
    attempts에 반영하면 안 된다(수리 _improve_cycle이 확립한 원칙과 동일)."""
    return any(k in (text or "") for k in INFRA_FAILURE_KEYWORDS)


def apply_task_failure(item: dict, max_attempts: int = TASK_MAX_ATTEMPTS) -> None:
    """배정 과제 딕셔너리(백로그 items 리스트의 원소, in-place)에 실패 1회를 반영 —
    파일 I/O 없음. 호출자가 이미 백로그 전체를 메모리에 들고 있다가 한 번에 저장하는
    구조(예: 백호 investigate_assigned_tasks의 todo 순회 후 일괄 저장)에 쓴다. 여기서
    파일을 직접 읽고 써버리면 호출자의 뒤이은 일괄 저장이 이 변경을 덮어써 버린다
    (2026-07-11 백호에 이 로직을 넣으며 실제로 마주친 함정 — record_backlog_task_failure를
    그대로 재사용하려다 발견)."""
    item["attempts"] = item.get("attempts", 0) + 1
    if item["attempts"] >= max_attempts:
        item["status"] = "보류"
        item["gate"] = f"{item.get('owner', '')} 반복 실패(구조적 원인 필요)"


def record_backlog_task_failure(backlog_path, task_id: str,
                                max_attempts: int = TASK_MAX_ATTEMPTS) -> None:
    """단일 과제 실패를 파일에서 직접 읽어 반영하고 즉시 저장한다 — 호출부가 과제 1개만
    다루고 그 자리에서 바로 저장하는 구조(예: 테오 generate_test, 과제 1개/사이클)에 쓴다.
    여러 과제를 순회하며 나중에 한 번만 저장하는 호출부는 apply_task_failure()를 직접
    쓰고 저장은 호출자가 책임져라(안 그러면 이중 저장·덮어쓰기 경합이 생긴다)."""
    path = Path(backlog_path)
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except Exception:
        return
    for i in data.get("items", []):
        if i.get("id") == task_id:
            apply_task_failure(i, max_attempts)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")


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

    버그 발견·수정(2026-07-11 1차, 파이프라인 재검토 중): council.py가 '보류'로 라우팅하는
    이유는 세 가지(승인필요·owner 불일치·DB/인증)인데 `gate` 필드는 DB/인증일 때만
    붙는다 — owner 불일치(예: 나무처럼 백로그를 안 읽는 owner)로 보류된 항목은 gate가
    없어 이 함수가 그냥 통과시켰다. 그러면 '대기'로 승격은 되는데 owner가 여전히
    소비자 없는 값이라 아무도 안 집는 좀비 상태가 된다 — 방치를 없앤 게 아니라 모양만
    바꾼 것. owner가 AUTO_OWNERS(백로그를 실제로 읽는 owner)에 속할 때만 승격한다.

    버그 발견·수정(2026-07-11 2차, 자동 파이프라인 감사 도구가 재발견): 1차 수정 이후
    needs_human()에 owner_type_mismatch 사유가 추가됐는데 이 함수는 갱신을 안 해 owner는
    맞아도 type이 안 맞는(예: 테오에게 type=디자인) 항목이 또 같은 계열의 좀비로 승격될
    수 있었다 — "사유 나열을 두 곳에 따로 두면 한쪽만 갱신돼 어긋난다"는 교훈을 지키려고
    만든 함수 자체가 지켜지지 않은 것. structurally_blocked() 하나로 단일화해, 앞으로
    사유가 추가돼도 이 함수를 따로 갱신할 필요가 없게 한다."""
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
        if structurally_blocked(it.get("owner", ""), it.get("type", ""),
                               it.get("title", ""), it.get("detail", "")):
            continue  # owner 소비자 없음·type 불일치·DB/인증 — 승인과 무관하게 보류 유지
        it["status"] = "대기"
        if not it.get("owner"):
            it["owner"] = "수리"
        it["updated"] = datetime.now().isoformat()
        it["promotion_note"] = "오너 승인 + 재검토(DB/인증 미접촉·소비자 있는 owner) 자동 승격"
        promoted.append(it.get("id", "?"))
    if promoted:
        path.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")
    return promoted


# ── backlog.json 동시쓰기 방지 (2026-08-02) ────────────────────────────────
#
# 배경: backlog.json에 쓰는 도구가 7곳(나무·미오·백호·테오·수리·회의·예원감사)인데
# 저마다 자기 이름의 advisory_lock만 쓴다 — 즉 **서로를 전혀 배제하지 못한다**.
# 각 도구는 파일 전체를 읽어 고친 뒤 통째로 덮어쓰므로, 두 도구의 읽기·쓰기가 겹치면
# 나중에 쓴 쪽이 먼저 쓴 쪽의 변경을 통째로 지운다. 파이프라인 감사가 2026-07-11에
# 지적했고 "여러 파일을 한 번에 바꿔야 한다"는 이유로 미뤄져 있었다.
#
# advisory_lock을 그대로 쓸 수 없는 이유: 그건 비차단(LOCK_NB)이라 충돌 시 **건너뛴다**.
# 건너뛰기는 '이번 사이클 스킵'엔 맞지만 백로그 쓰기엔 곧 데이터 유실이다 — 여기선
# 잠깐 기다렸다가 쓰는 게 맞다. 그래서 짧은 타임아웃을 둔 차단 락을 따로 둔다.
#
# 타임아웃을 넘기면 락 없이 진행한다(예외를 던지지 않는다) — 락 획득 실패로 적재 자체가
# 사라지는 것보다, 드문 경합을 감수하고 쓰는 편이 낫다. 대신 경고를 남겨 추적 가능하게 한다.
_BACKLOG_LOCK_PATH = "/tmp/petnna_backlog_json.lock"


@contextmanager
def backlog_lock(timeout: float = 20.0):
    """backlog.json read-modify-write 구간 전용 전역 락. 모든 도구가 이 하나를 공유한다.

    사용:
        with backlog_lock():
            data = json.loads(BACKLOG.read_text(...))
            ...수정...
            BACKLOG.write_text(...)

    반드시 **읽기부터** 감싸야 한다 — 쓰기만 감싸면 읽은 뒤 남이 바꾼 걸 덮어쓰는
    read-modify-write 경합이 그대로 남는다.
    """
    if sys.platform == "win32":  # pragma: no cover - 운영 기계는 darwin
        try:
            yield True
        finally:
            pass
        return
    try:
        import fcntl
    except ImportError:  # pragma: no cover
        yield True
        return
    fd = open(_BACKLOG_LOCK_PATH, "w")
    deadline = time.time() + timeout
    got = False
    try:
        while True:
            try:
                fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
                got = True
                break
            except OSError:
                if time.time() >= deadline:
                    print(f"[backlog] 락 대기 {timeout}s 초과 — 락 없이 진행(경합 가능)")
                    break
                time.sleep(0.2)
        yield got
    finally:
        if got:
            try:
                fcntl.flock(fd, fcntl.LOCK_UN)
            except Exception:
                pass
        fd.close()


# ── 액션아이템 owner 정규화 (2026-08-04) ──────────────────────────────────
#
# 회의(petnna_council)가 owner를 "수리|테오|백호|미오|나무|사람" 중에서 고르게 프롬프트에
# 하드코딩해 뒀는데, 실제 소비자 목록인 AUTO_OWNERS엔 나무·사람이 없다. 그래서 회의가
# 나무나 사람을 고를 때마다 **아무도 읽지 않는 항목**이 하나씩 생겼다 — 2026-08-04
# 시점에 그렇게 쌓인 게 10건(사람 5·봄이 3·나무 2). 2026-07-10에 같은 사고를 한 번
# 겪고 structurally_blocked()로 '걸러내기'는 했지만, **애초에 안 만들게** 하지는
# 않아서 계속 재생산됐다.
#
# 여기서 두 가지를 한다:
#  1) 프롬프트에 넣을 owner 목록을 이 파일에서 만들어 준다(OWNER_CHOICES) — 하드코딩된
#     목록이 AUTO_OWNERS와 어긋나는 일이 구조적으로 불가능해진다.
#  2) LLM이 그래도 목록 밖 owner를 뱉으면 적재 시점에 정규화한다(normalize_owner).
#     '사람'은 유효한 트랙이라 남기되 gate로 명시해 '소비자 없음'과 구분한다.
HUMAN_OWNER = "사람"

# 회의 프롬프트에 제시할 선택지 — 소비자가 있는 owner + 사람(오너 판단 트랙).
# ""(무지정)은 LLM에 제시하지 않는다(빈 문자열을 고르라고 하면 혼란만 준다).
OWNER_CHOICES = tuple(o for o in AUTO_OWNERS if o) + (HUMAN_OWNER,)


def normalize_owner(owner: str) -> tuple[str, str]:
    """(정규화된 owner, 사유). 사유가 비어 있으면 변경 없음.

    · AUTO_OWNERS에 있으면 그대로
    · '사람'이면 그대로 — 오너 판단 트랙(소비자가 없는 게 정상)
    · 그 외(나무·봄이 등 백로그를 읽지 않는 에이전트)는 ""로 되돌린다.
      ""는 수리가 집는다 — 아무도 안 집는 것보다 낫고, 부적합하면 수리 게이트가 거른다.
    """
    o = (owner or "").strip()
    if o in AUTO_OWNERS or o == HUMAN_OWNER:
        return o, ""
    return "", (f"owner '{o}'는 백로그를 읽지 않아 아무도 집지 못한다 — "
                f"무지정으로 되돌려 수리가 집게 한다(소비자: {', '.join(x or '(무지정)' for x in AUTO_OWNERS)})")
