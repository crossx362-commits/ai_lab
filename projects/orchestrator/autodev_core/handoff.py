"""Provider 승계 인수인계 — 다른 Provider로 넘어갈 때 **처음부터 다시 시키지 않는다**.

넘기는 것은 대화가 아니라 **상태**다. worktree는 그대로 두고(이미 고친 파일이 거기 있다),
그 위에 「무엇이 목표였고, 지금까지 뭐가 바뀌었고, 어디서 막혔고, 뭐가 남았는지」를 붙인다.

이 파일이 없으면 승계는 재시작이 된다 — 앞 Provider가 쓴 시간과 돈이 매번 버려진다.
"""

from __future__ import annotations

from pathlib import Path

from . import db, gitwt

TEMPLATE = """
[인수인계 — 앞 담당이 중간까지 해놓았다]
앞 담당: {prev} (교체 사유: {why})
이 worktree에는 **앞 담당의 작업이 이미 들어 있다**. 처음부터 다시 만들지 마라.
먼저 아래 변경을 읽고, 이어서 남은 것을 끝내라.

■ 목표
{goal}

■ 완료 조건
{done}

■ 지금까지의 변경 (merge-base 기준)
{diffstat}

변경된 파일:
{files}

■ Unity 판정 결과
{unity}

■ 마지막 오류
{errors}

■ 지난 시도들
{history}

■ 남은 일
{remaining}
"""


def _history(conn, task_id: int) -> str:
    rows = db.list_attempts(conn, task_id)
    if not rows:
        return "(없음)"
    out = []
    for r in rows:
        out.append(f"  시도 {r['n']} · {r['agent']} → {r['status']}"
                   + (f" ({r['reason']})" if r["reason"] else ""))
    return "\n".join(out)


def build(conn, *, task_id: int, goal: str, done_criteria: str, worktree: Path,
          base_commit: str, prev_agent: str, why: str,
          unity_summary: str = "", errors: str = "", remaining: str = "") -> str:
    """승계 문서를 만든다. 사실만 넣는다 — 없는 것은 '(없음)'이라고 적는다."""
    try:
        ch = gitwt.collect_changes(worktree, base_commit)
        diffstat = (f"파일 {len(ch.files)}개, +{ch.insertions} / -{ch.deletions}줄"
                    if ch.changed else "(변경 없음 — 앞 담당이 아무것도 남기지 못했다)")
        files = "\n".join(f"  {f}" for f in ch.files) or "  (없음)"
    except Exception as e:                       # git이 흔들려도 승계 자체가 죽으면 안 된다
        diffstat, files = f"(diff를 읽지 못했다: {e})", "  (미상)"
    return TEMPLATE.format(
        prev=prev_agent or "(미상)",
        why=why or "(미상)",
        goal=goal,
        done=done_criteria or "(명시 없음 — 목표 문구를 완료 조건으로 본다)",
        diffstat=diffstat,
        files=files,
        unity=unity_summary or "(아직 판정 없음)",
        errors=errors or "(없음)",
        history=_history(conn, task_id),
        remaining=remaining or "완료 조건을 만족시키는 나머지 전부.",
    )
