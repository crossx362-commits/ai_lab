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
    r"|신규\s*테이블|테이블\s*추가|테이블.{0,4}신설|스키마\s*(변경|추가|마이그)"
    r"|\bschema\b"
    r"|api[_\s-]?key|시크릿|\bsecret\b",
    re.IGNORECASE,
)


def touches_db_auth(title: str, detail: str = "") -> bool:
    """DB/인증 계층을 요구해 수리 자동 루프가 병합할 수 없는 과제인가."""
    return bool(DB_AUTH_PATTERN.search(f"{title}\n{detail}"))


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
    나열을 이 함수 하나로 단일화해 두 호출부가 항상 같은 판정을 보게 한다."""
    return (owner not in AUTO_OWNERS
            or owner_type_mismatch(owner, item_type)
            or touches_db_auth(title, detail))


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
INFRA_FAILURE_KEYWORDS = ("미발견", "타임아웃", "rate limit", "429", "529", "verloaded")


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
