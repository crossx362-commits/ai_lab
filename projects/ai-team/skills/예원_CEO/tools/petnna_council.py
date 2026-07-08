#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""펫나 긴급 회의 — 큰 이슈 발생 시 전 에이전트 소집 (의장: 예원).

트리거(자동): 봄이 신규 P0/P1 발견 · 수리 3회 실패 보류 · 백호 신규 P1 계약 불일치.
수동: --topic "안건" [--context ...] [--priority P0|P1|P2]

진행:
1. 6인 전문가(봄이·수리·테오·백호·미오·나무)가 각자 헌장(SKILL.md)과 실데이터
   (QA 상태·감사 보고·테스트 결과·코드)를 읽고 독립 의견 제출 — 병렬, plan 모드(수정 불가)
2. 의장 예원이 의견을 종합해 결론·결정·액션아이템 도출
3. 액션아이템은 공유 백로그(source=회의)로 적재 → 수리가 실행(고위험은 [승인필요] 표시)
4. 회의록 output/qa/petnna/council/ 저장 + 텔레그램 요약

안전선: 회의는 조언·과제 생성만 한다(코드/DB 무수정). 같은 안건은 24시간 내 재소집 안 함.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import sys
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parents[4]
AI_TEAM_ROOT = PROJECT_ROOT / "projects" / "ai-team"
sys.path.insert(0, str(AI_TEAM_ROOT))

from _shared.env import load_env  # noqa: E402
from _shared.notify import send  # noqa: E402
from _shared.process import ProcessLock  # noqa: E402
from _shared.cc import run_claude, extract_json  # noqa: E402

load_env(str(PROJECT_ROOT))

OUT_DIR = PROJECT_ROOT / "output" / "qa" / "petnna" / "council"
STATE = OUT_DIR / "state.json"
BACKLOG = PROJECT_ROOT / "output" / "qa" / "petnna" / "backlog.json"
COOLDOWN_H = 24

PERSONAS = [
    ("봄이", "QA 검수관", "봄이_QA"),
    ("수리", "자동 개선 개발자", "수리_개발자"),
    ("테오", "E2E 테스트 엔지니어", "테오_테스트"),
    ("백호", "백엔드 지킴이", "백호_백엔드"),
    ("미오", "디자인 리뷰어", "미오_디자인"),
    ("나무", "기획 PM", "나무_기획"),
]


def _recent_meeting(topic: str) -> bool:
    key = hashlib.md5(topic.encode("utf-8")).hexdigest()[:10]
    try:
        state = json.loads(STATE.read_text(encoding="utf-8"))
    except Exception:
        state = {}
    last = state.get(key)
    if last:
        from datetime import datetime as dt
        if (dt.now() - dt.fromisoformat(last)).total_seconds() < COOLDOWN_H * 3600:
            return True
    state[key] = datetime.now().isoformat()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    STATE.write_text(json.dumps(state, ensure_ascii=False, indent=1), encoding="utf-8")
    return False


def _opinion(name: str, role: str, folder: str, topic: str, context: str, priority: str) -> tuple[str, str]:
    skill = AI_TEAM_ROOT / "skills" / folder / "SKILL.md"
    prompt = (
        f"너는 펫나 개발팀의 {name}({role})다. 너의 헌장은 {skill} 에 있다 — Read로 읽고 그 관점을 유지하라.\n\n"
        f"[긴급 회의 안건] (우선순위 {priority})\n{topic}\n상세: {context or '(없음)'}\n\n"
        "[참고 자료 — 판단에 필요한 것만 Read로 확인]\n"
        "- QA 상태: output/qa/petnna/qa_state.json / 보고서·감사·테스트 결과·백로그: output/qa/petnna/ 아래\n"
        "- 코드: projects/petnna/ / 대기 중 브랜치: git branch --list 'fix/petnna-*' 등은 회의록 컨텍스트 참고\n"
        "모르는 기술적 사실은 웹서치로 확인하라. 추측을 사실처럼 말하지 마라. 코드는 수정하지 마라.\n\n"
        "[출력 — 이 형식 그대로, 총 7줄 이내]\n"
        "입장: <한 줄 — 예: 즉시 수정 / 병합 권고 / 롤백 / 보류 / 구조 개선 필요>\n"
        "근거: <최대 3줄, 실제 확인한 데이터 기반>\n"
        "제안 액션: <구체적 1~2개, 없으면 '없음'>\n"
        "리스크: <한 줄>"
    )
    ok, out = run_claude(prompt, PROJECT_ROOT, timeout=420,
                         allowed_tools="Read,WebSearch,WebFetch", permission_mode="plan")
    return name, (out.strip()[:1200] if ok and out else "(의견 수집 실패)")


def convene(topic: str, context: str, priority: str) -> None:
    if _recent_meeting(topic):
        print(f"[회의] 동일 안건 {COOLDOWN_H}시간 내 개최됨 — 생략: {topic[:60]}")
        return
    with ProcessLock("petnna_council"):
        print(f"[{datetime.now()}] 🏛️ 긴급 회의 소집 — {topic[:80]}")
        with ThreadPoolExecutor(max_workers=3) as ex:
            futures = [ex.submit(_opinion, n, r, f, topic, context, priority)
                       for n, r, f in PERSONAS]
            opinions = [fu.result() for fu in futures]

        opinion_text = "\n\n".join(f"### {name}\n{op}" for name, op in opinions)
        ok, out = run_claude(
            "너는 펫나 개발팀 CEO 예원, 이 긴급 회의의 의장이다. 군더더기 없이 핵심만.\n\n"
            f"[안건] (우선순위 {priority})\n{topic}\n상세: {context or '(없음)'}\n\n"
            f"[전문가 의견]\n{opinion_text}\n\n"
            "[할 일]\n"
            "1. 의견을 종합해 회의 결론을 5줄 이내로 내라(의견 충돌은 명시하고 판정하라).\n"
            "2. 결정 사항을 번호 목록으로.\n"
            "3. 마지막에 실행 액션아이템을 JSON 배열로: "
            "[{\"title\": \"...\", \"detail\": \"...\", \"priority\": \"P1|P2|P3\", "
            "\"type\": \"기능|디자인|기획|테스트|백엔드\", \"owner\": \"수리|테오|백호|미오|나무|사람\"}] "
            "— 자동 실행이 위험한 항목은 title 앞에 [승인필요]를 붙여라. 액션이 없으면 빈 배열.",
            PROJECT_ROOT, timeout=420, allowed_tools="Read", permission_mode="plan")
        verdict = out.strip() if ok and out else "(의장 종합 실패)"

        # 액션아이템 → 백로그(source=회의). [승인필요]·owner=사람 항목은 적재만 하고 수리가 안 집도록 보류 상태.
        actions = extract_json(verdict) or []
        added = 0
        try:
            data = json.loads(BACKLOG.read_text(encoding="utf-8"))
        except Exception:
            data = {"items": []}
        existing = {i.get("title") for i in data["items"]}
        for a in actions if isinstance(actions, list) else []:
            title = (a.get("title") or "").strip()
            if not title or title in existing:
                continue
            needs_human = "[승인필요]" in title or a.get("owner") == "사람"
            data["items"].append({
                "id": f"회의_{datetime.now():%Y%m%d%H%M}_{added}",
                "title": title[:120], "detail": (a.get("detail") or "")[:500],
                "priority": a.get("priority") if a.get("priority") in ("P1", "P2", "P3") else "P2",
                "type": a.get("type", "기획"), "source": "회의",
                "status": "보류" if needs_human else "대기",
                "owner": a.get("owner", ""), "created": datetime.now().isoformat(),
            })
            added += 1
        BACKLOG.parent.mkdir(parents=True, exist_ok=True)
        BACKLOG.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")

        OUT_DIR.mkdir(parents=True, exist_ok=True)
        minutes = OUT_DIR / f"minutes_{datetime.now():%Y%m%d_%H%M}.md"
        minutes.write_text(
            f"# 펫나 긴급 회의록 — {datetime.now():%Y-%m-%d %H:%M}\n\n"
            f"## 안건 ({priority})\n{topic}\n\n상세: {context or '(없음)'}\n\n"
            f"## 전문가 의견\n{opinion_text}\n\n## 의장(예원) 종합\n{verdict}\n\n"
            f"## 백로그 적재: {added}건 (source=회의, 승인필요 항목은 보류 상태)\n",
            encoding="utf-8")
        print(f"[{datetime.now()}] 회의 종료 — 회의록 {minutes}, 액션 {added}건 적재")
        # 결론 첫 부분만 텔레그램 (전문은 회의록)
        head = verdict.split("```")[0].strip()[:1200]
        send(f"🏛️ 펫나 긴급 회의 결과 — {topic[:70]}\n\n{head}\n\n"
             f"액션 {added}건 백로그 적재 · 📄 {minutes}")


def main() -> None:
    ap = argparse.ArgumentParser(description="펫나 긴급 회의 (전 에이전트 소집)")
    ap.add_argument("--topic", required=True, help="회의 안건")
    ap.add_argument("--context", default="", help="상세 컨텍스트")
    ap.add_argument("--priority", default="P1", choices=["P0", "P1", "P2"])
    args = ap.parse_args()
    convene(args.topic, args.context, args.priority)


if __name__ == "__main__":
    main()
