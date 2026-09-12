"""작업 **난이도**에 따른 모델·추론 강도 선택. Provider 가용성 선택 이후 호출 직전에 적용한다.

오너 지시(2026-09-12): 「일 난이도에 따라 뭐로 하면 좋을지 생각해서 결정하는 거다」.
단계 이름만 보고 고정 표를 따르면, 한 줄 문서 수정에 최상위 모델을 쓰고 동시성 복구 설계에
싸구려를 쓰는 일이 같이 벌어진다. 그래서 **과제 자체를 재서** 등급을 매긴다.

재는 것(전부 이미 가진 값이다, 모델에게 묻지 않는다):
  · 계획자가 매긴 risk(low/medium/high)          · 지금까지의 실패 횟수(반려·게이트 실패)
  · 목표·완료조건의 길이와 요구 개수             · 어려운 일의 낱말(동시성·복구·마이그레이션…)
  · 쉬운 일의 낱말(문서·오타·상수…)              · 선행 의존 개수

등급은 1~5. 1은 문서 한 줄, 5는 설계가 필요한 것. 등급이 오르면 모델도 추론 강도도
**내려가지 않는다**(NC로 단조성을 지킨다). 왜 그 등급인지 사유를 반드시 함께 남긴다 —
사유 없는 선택은 나중에 규칙을 고칠 수도, 비용을 따질 수도 없다.
"""
from dataclasses import replace
import re


def task_kind(goal: str, *, failures=False) -> str:
    if failures or re.search(r'오류|버그|디버그|debug|bug\b|crash', goal, re.I):
        return 'debugging'
    if re.search(r'문서|readme|documentation|주석', goal, re.I):
        return 'documentation'
    if re.search(r'테스트|test\b|검증', goal, re.I):
        return 'testing'
    return 'coding'


def select(cfg, name: str, kind: str):
    agent = cfg.agent(name)
    policy = cfg.raw.get('model_policy', {}).get(agent.type, {})
    model = policy.get(kind)
    if not model:
        return agent
    return replace(agent, model=model)


def unavailable(text: str) -> bool:
    """CLI가 명시한 모델 부재/접근 거부만 대체 근거로 쓴다."""
    return bool(re.search(
        r"(?:model[^\n]{0,120}(?:not found|not available|not supported|does not exist|unavailable))"
        r"|(?:no access to[^\n]{0,60}model)", text or '', re.I))


# ── 난이도 판정 ───────────────────────────────────────────────────────────────

_HARD = [
    (r'동시성|경합|race|deadlock|락\b|lock\b', '동시성'),
    (r'복구|장애|failover|롤백|재해|정합성|일관성|무결성', '복구·정합성'),
    (r'마이그레이션|스키마 변경|migration|리팩터|아키텍처|설계|구조 변경', '구조 변경'),
    (r'네트워크|동기화|권위|authority|프로토콜|rpc\b', '네트워크·권위'),
    (r'성능|최적화|프로파일|메모리 누수|gc\b', '성능'),
    (r'보안|인증|권한|암호', '보안'),
]
_EASY = [
    (r'문서|readme|주석|오타|표기|맞춤법', '문서·표기'),
    (r'상수|값 변경|수치 조정|이름 변경|rename', '값·이름'),
    (r'로그 한 줄|줄 추가|한 줄', '한 줄'),
]


def difficulty(goal: str, *, done_criteria: str = '', risk: str = '',
               failures: int = 0, deps: int = 0) -> tuple[int, list[str]]:
    """1~5와 그 사유. 모델을 부르지 않는 순수 함수다(그래서 NC로 값싸게 지킬 수 있다)."""
    text = f'{goal}\n{done_criteria}'
    score, why = 2, []

    r = (risk or '').lower()
    if r == 'high':
        score += 2; why.append('계획자 risk=high')
    elif r == 'medium':
        score += 1; why.append('계획자 risk=medium')
    elif r == 'low':
        score -= 1; why.append('계획자 risk=low')

    hits = [name for pat, name in _HARD if re.search(pat, text, re.I)]
    if hits:
        score += min(2, len(hits)); why.append('어려운 축: ' + '·'.join(hits[:3]))
    easy = [name for pat, name in _EASY if re.search(pat, text, re.I)]
    if easy and not hits:
        score -= 1; why.append('가벼운 축: ' + '·'.join(easy[:2]))

    n = len(text)
    if n > 1200:
        score += 1; why.append(f'요구 {n}자')
    elif n < 160:
        score -= 1; why.append(f'요구 {n}자')

    if failures >= 2:
        score += 2; why.append(f'{failures}회 실패 — 쉬운 길로는 안 된다')
    elif failures == 1:
        score += 1; why.append('1회 실패')

    if deps >= 2:
        score += 1; why.append(f'선행 {deps}개 위에 쌓는다')

    score = max(1, min(5, score))
    return score, why


def tier(cfg, agent_type: str, level: int):
    """난이도 등급 → (모델, 추론 강도). config.model_tiers가 없으면 None."""
    tiers = (cfg.raw.get('model_tiers') or {}).get(agent_type)
    if not tiers:
        return None
    for row in sorted(tiers, key=lambda x: x.get('max_difficulty', 5)):
        if level <= int(row.get('max_difficulty', 5)):
            return row
    return tiers[-1]


def choose(cfg, name: str, *, goal: str, done_criteria: str = '', risk: str = '',
           failures: int = 0, deps: int = 0, kind: str = '', floor: int = 0):
    """난이도로 모델·추론 강도를 정한다. 등급표가 없으면 옛 단계별 표로 물러선다.

    돌려주는 것: (agent_cfg, 설명 한 줄). 설명은 로그·보드에 그대로 실린다.
    """
    agent = cfg.agent(name)
    level, why = difficulty(goal, done_criteria=done_criteria, risk=risk,
                            failures=failures, deps=deps)
    if floor and level < floor:
        # 설계·리뷰는 바닥을 둔다 — 쉬워 보이는 목표라도 **나눠 놓은 결과가 틀리면 전부가 틀린다**.
        level = floor
        why.append(f'{kind or "보조"}는 최소 {floor}등급')
    row = tier(cfg, agent.type, level)
    if not row:
        old = select(cfg, name, kind or task_kind(goal, failures=bool(failures)))
        return old, f'난이도 {level}/5 ({", ".join(why) or "기본"}) → {old.model} (단계별 표)'
    model = row.get('model') or agent.model
    effort = row.get('effort')
    extra = [x for x in (agent.extra_config or []) if 'model_reasoning_effort' not in str(x)]
    if effort:
        extra = extra + [f'model_reasoning_effort="{effort}"']
    picked = replace(agent, model=model, extra_config=extra)
    return picked, (f'난이도 {level}/5 ({", ".join(why) or "기본"}) → {model}'
                    + (f' · 추론 {effort}' if effort else ''))
