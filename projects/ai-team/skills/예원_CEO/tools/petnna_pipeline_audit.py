#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""예원 — 펫나 파이프라인 딥 로직 감사 (주 1회, 읽기 전용).

배경(2026-07-11): 오너 "문제는 좀 계속 찾아서 고쳐 지식화 자동화해" — 그날 세션 하나에서
연쇄로 발견된 실제 사고 4건(미오 주간 슬롯이 배정과제를 최대 6일 방치·ProcessLock의
sys.exit이 상시데몬을 죽일 뻔함·디스패치 출력이 DEVNULL로 사라짐·MAX_TESTS 상한이
배정과제까지 막음)은 전부 "기능 하나를 다른 데 추가했는데 그게 건드리는 기존 경로를
안 훑어봐서 생긴" 같은 계열의 결함이었다. 사람이 매번 "점검해줘"라고 시켜야만 나오는
발견을 정기 자동 점검으로 옮긴다.

이 도구가 찾는 것(skill_auditor.py의 "문서 품질 점수"·code_auditor.py의 "미사용 함수"와
겹치지 않는, 실행 로직 결함):
  1. 배정된 백로그 과제가 캡·게이트·정기슬롯 때문에 영원히 처리 안 될 수 있는 경로
  2. 에러·예외가 조용히 삼켜지거나(DEVNULL, bare except: pass) 로그 없이 사라지는 곳
  3. 여러 에이전트가 같은 종류 기능(백로그 소비·락 안전성·재시도 카운터)을 갖는데
     한쪽만 최근 패턴을 갖고 나머지는 예전 버전에 머물러 있는 비대칭
  4. 락/동시성 — sys.exit·미처리 예외가 상시 데몬 루프를 죽일 수 있는 경로
  5. 재시도/쿨다운 상태가 프로세스 재시작으로 초기화돼 무한반복하거나 영구 차단되는 경로

읽기 전용(코드 수정 없음, plan 모드) — 발견은 보고서+텔레그램, 고확신 항목만 백로그에
`보류`(사람 검토)로 적재. skill_auditor(월)·code_auditor(토)와 겹치지 않게 일요일 배치.

실행:
  python petnna_pipeline_audit.py --check         # 감사만(텔레그램 전송 없음)
  python petnna_pipeline_audit.py --send          # + 텔레그램
"""
import json
import os
import sys
from datetime import datetime

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)

from _shared.env import find_root, load_env  # noqa: E402
from _shared.telegram import send  # noqa: E402
from _shared.cc import run_claude, extract_json  # noqa: E402

_root = find_root(_here)
load_env(_root)  # 누락 시 launchd 잡이 TELEGRAM_BOT_TOKEN 등을 못 읽어 전송 조용히 실패(2026-07-11 교훈)

PROJECT_ROOT = _root
AI_TEAM = os.path.join(PROJECT_ROOT, "projects", "ai-team")
OUT_DIR = os.path.join(PROJECT_ROOT, "output", "qa", "petnna", "pipeline_audit")
BACKLOG = os.path.join(PROJECT_ROOT, "output", "qa", "petnna", "backlog.json")
SEND_TG = "--send" in sys.argv

# 감사 대상 — 펫나 자동개발 파이프라인의 핵심 로직 파일. _shared는 전 에이전트 의존이라 포함.
TARGET_FILES = [
    "skills/수리_개발자/tools/petnna_dev_engine.py",
    "skills/테오_테스트/tools/petnna_test_engineer.py",
    "skills/미오_디자인/tools/petnna_design_review.py",
    "skills/백호_백엔드/tools/petnna_backend_guard.py",
    "skills/나무_기획/tools/petnna_product_manager.py",
    "skills/봄이_QA/tools/petnna_qa_patrol.py",
    "skills/예원_CEO/tools/harness_monitor.py",
    "skills/예원_CEO/tools/petnna_council.py",
    "_shared/backlog.py",
    "_shared/process.py",
]

CHECKLIST = """[점검 관점 — 실제로 이 저장소에서 터졌던 사고 계열이다. 비슷한 패턴을 찾아라]
1. 배정된 백로그 과제(owner 필드로 특정 에이전트에 배정된 '대기' 항목)가 캡·게이트·정기슬롯
   때문에 영원히 처리 안 될 수 있는 경로가 있는가? (예: 개수 상한 체크가 배정과제보다 먼저
   실행돼 조기 반환하는 경우, 주 1회/하루 1회 같은 낮은 빈도 슬롯에만 배정과제 확인이 묶인 경우)
2. 에러·예외가 조용히 삼켜지거나(`except: pass`, 출력이 DEVNULL로 버려짐, 실패해도 상태 파일에
   기록 안 됨) 로그·텔레그램 어디에도 흔적이 안 남는 경로가 있는가?
3. 비슷한 역할의 에이전트 파일들(수리·테오·백호·미오는 모두 "백로그 배정과제 소비" 기능이
   있어야 한다)을 서로 비교했을 때, 한쪽만 최근에 추가된 안전장치(예: advisory_lock 실행구간
   보호, 배정과제 소비, 재발 감지 리셋)를 갖고 다른 쪽은 옛 패턴에 머물러 있는 비대칭이 있는가?
4. 상시 데몬의 while-루프 안에서 `sys.exit`을 유발할 수 있는 호출(예: 실행구간 전체를 감싸는
   ProcessLock을 매 루프 반복마다 다시 얻으려 하는 경우)이 있는가?
5. 재시도 횟수·쿨다운 타임스탬프 같은 상태가 데몬 프로세스 메모리에만 있어서, 데몬이 재시작되면
   리셋돼 같은 실수가 무한 반복되거나 반대로 원래 있던 상한이 무력화되는 경로가 있는가?

[출력 지침]
실제로 코드를 Read로 열어 근거를 확인한 것만 보고하라. 추측이나 일반론(예: "에러 핸들링을
강화하세요")은 쓰지 마라 — 구체적 파일:줄, 구체적 트리거 조건, 구체적 실패 시나리오가 없으면
보고하지 마라. 이미 이 저장소에서 고쳐진 것으로 보이는 패턴은 다시 보고하지 마라."""


def _read_targets() -> str:
    blocks = []
    for rel in TARGET_FILES:
        path = os.path.join(AI_TEAM, rel)
        try:
            with open(path, encoding="utf-8") as f:
                blocks.append(f"### {rel}\n```python\n{f.read()}\n```")
        except Exception:
            continue
    return "\n\n".join(blocks)


def run_audit(do_send: bool = True) -> dict:
    code = _read_targets()
    prompt = (
        "너는 펫나(petnna) 자동개발 파이프라인의 실행 로직을 감사하는 시니어 엔지니어다. "
        "아래는 파이프라인 핵심 파일 전문이다.\n\n"
        f"{code}\n\n"
        f"{CHECKLIST}\n\n"
        "발견한 결함만 JSON 배열로 반환하라(없으면 빈 배열). 마크다운·설명문 없이 JSON만:\n"
        '[{"title": "짧은 제목", "file": "파일 상대경로", "severity": "P1|P2|P3", '
        '"evidence": "구체적 근거(줄 인용 가능)", "trigger": "언제 실제로 터지는가", '
        '"suggested_fix": "구체적 수정 방향"}]'
    )
    ok, out = run_claude(prompt, PROJECT_ROOT, timeout=600,
                         allowed_tools="Read", permission_mode="plan")
    findings = extract_json(out) if ok and out else None
    findings = findings if isinstance(findings, list) else []

    os.makedirs(OUT_DIR, exist_ok=True)
    report_path = os.path.join(OUT_DIR, f"report_{datetime.now():%Y%m%d}.md")
    lines = [f"# 펫나 파이프라인 딥 로직 감사 — {datetime.now():%Y-%m-%d}", ""]
    if not ok:
        lines.append(f"감사 실패(인프라): {out[:300]}")
    elif not findings:
        lines.append("발견 없음 — 점검 관점 5종 전부 이상 없음.")
    else:
        for f in findings:
            lines.append(f"## [{f.get('severity','P3')}] {f.get('title','')}")
            lines.append(f"- 파일: `{f.get('file','')}`")
            lines.append(f"- 근거: {f.get('evidence','')}")
            lines.append(f"- 트리거: {f.get('trigger','')}")
            lines.append(f"- 제안: {f.get('suggested_fix','')}")
            lines.append("")
    with open(report_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))

    added = _append_backlog(findings)

    if do_send:
        if not ok:
            msg = f"🔍 [예원] 펫나 파이프라인 감사 실패(인프라) — 다음 주기 재시도"
        elif not findings:
            msg = "🔍 [예원] 펫나 파이프라인 감사 — 발견 없음"
        else:
            msg = [f"🔍 [예원] 펫나 파이프라인 감사 — {len(findings)}건 발견(백로그 {added}건 적재, 사람 검토 대기)"]
            msg += [f"· [{f.get('severity','P3')}] {f.get('title','')} ({f.get('file','')})"
                    for f in findings[:5]]
            msg.append(f"📄 {report_path}")
            msg = "\n".join(msg)
        send(msg, silent=True)

    return {"ok": ok, "findings": findings, "report": report_path, "backlog_added": added}


def _append_backlog(findings: list[dict]) -> int:
    """고확신 발견만 백로그에 사람 검토 대기(`보류`)로 적재 — 자동 구현 대상 아님.
    LLM이 짐작한 로직 결함은 사람 확인 없이 수리가 바로 고치기엔 신뢰도가 낮다."""
    if not findings:
        return 0
    try:
        with open(BACKLOG, encoding="utf-8") as f:
            data = json.load(f)
    except Exception:
        data = {"items": []}
    existing_titles = {it.get("title") for it in data.get("items", [])}
    added = 0
    for i, f in enumerate(findings):
        title = f"[파이프라인감사] {f.get('title','')}"[:120]
        if not f.get("title") or title in existing_titles:
            continue
        detail = f"파일: {f.get('file','')}\n근거: {f.get('evidence','')}\n트리거: {f.get('trigger','')}\n제안: {f.get('suggested_fix','')}"
        data.setdefault("items", []).append({
            "id": f"파이프라인감사_{datetime.now():%Y%m%d}_{i}",
            "title": title, "detail": detail[:800],
            "priority": f.get("severity") if f.get("severity") in ("P1", "P2", "P3") else "P2",
            "type": "기능", "source": "파이프라인감사",
            "status": "보류", "gate": "사람검토(LLM 추정 결함)",
            "owner": "사람", "created": datetime.now().isoformat(),
        })
        added += 1
    if added:
        os.makedirs(os.path.dirname(BACKLOG), exist_ok=True)
        with open(BACKLOG, "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=1)
    return added


if __name__ == "__main__":
    result = run_audit(do_send=SEND_TG)
    print(f"감사 완료 — 발견 {len(result['findings'])}건, 백로그 적재 {result['backlog_added']}건, {result['report']}")
