#!/usr/bin/env python3
"""agent_loop.py — 에이전트 판단 루프 (단계 A 고도화).

영숙의 Function-Calling 패턴을 일반화. 에이전트는 자기 registry tools 만 보고,
LLM이 '어떤 tool들을 어떤 순서로 쓸지' 정해 실행 → 결과 요약/판단 → 종합 보고.

가드(폭주 방지):
  - max_steps 상한
  - tool 화이트리스트(자기 registry tools 만)
  - 스크립트별 timeout
  - LLM 미가용/파싱 실패 시 키워드 폴백(첫 매칭 tool 1개 실행)
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

_here = Path(__file__).resolve().parent
AI_TEAM = _here.parent
PROJECT_ROOT = AI_TEAM.parents[1]
sys.path.insert(0, str(AI_TEAM))

from _shared.registry import get_agent, tools_for          # noqa: E402
from _shared.llm import text as llm_text, is_available as llm_available  # noqa: E402

MAX_STEPS_DEFAULT = 4


def _run_tool(tool: dict, message: str) -> tuple[int, str]:
    """registry tool 1개 실행 (스크립트 + args, {message} 치환)."""
    script = AI_TEAM / tool["script"]
    if not script.exists():
        return 1, f"(스크립트 없음: {tool['script']})"
    args = [str(a).replace("{message}", message) for a in tool.get("args", [])]
    env = {**os.environ, "PYTHONUTF8": "1", **(tool.get("env") or {})}
    nowin = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {}
    try:
        r = subprocess.run(
            [sys.executable, str(script), *args],
            cwd=str(PROJECT_ROOT), capture_output=True, text=True,
            encoding="utf-8", errors="replace",
            timeout=int(tool.get("timeout", 120)), env=env, **nowin,
        )
        return r.returncode, (r.stdout or r.stderr or "").strip()
    except subprocess.TimeoutExpired:
        return 1, f"(타임아웃: {tool['name']})"
    except Exception as e:
        return 1, f"(실행 오류: {e})"


def _plan_prompt(agent_meta: dict, tools: list[dict], message: str) -> str:
    lines = [
        f"당신은 에이전트 [{agent_meta.get('display')}] — {agent_meta.get('role')}.",
        "아래 도구만 사용할 수 있습니다:",
    ]
    for t in tools:
        lines.append(f"  - {t['name']}: {t.get('desc','')}")
    lines += [
        "",
        f'사용자 요청: "{message}"',
        "",
        "요청 해결에 필요한 도구를 순서대로 고르세요. JSON 배열만 반환:",
        '["tool_name", ...]  (필요한 것만, 1~3개. 해당 없으면 빈 배열 [])',
    ]
    return "\n".join(lines)


def _parse_tool_list(raw: str | None, valid: set[str]) -> list[str]:
    if not raw:
        return []
    s = raw.strip()
    a, b = s.find("["), s.rfind("]")
    cand = s[a:b + 1] if a >= 0 and b > a else s
    try:
        arr = json.loads(cand)
        return [x for x in arr if isinstance(x, str) and x in valid]
    except Exception:
        return [name for name in valid if name in s]


def run_agent(agent_id: str, message: str, max_steps: int = MAX_STEPS_DEFAULT) -> str:
    """에이전트 판단 루프 실행 → 종합 보고 문자열."""
    meta = get_agent(agent_id)
    if not meta:
        return f"⚠️ 알 수 없는 에이전트: {agent_id}"
    tools = tools_for(agent_id)
    if not tools:
        return f"⚠️ [{meta.get('display')}] 등록된 도구가 없습니다."

    by_name = {t["name"]: t for t in tools}
    valid = set(by_name)

    # 1) 도구 선택 (LLM → 키워드 폴백)
    chosen: list[str] = []
    if llm_available():
        raw = llm_text(_plan_prompt(meta, tools, message), json_mode=True,
                       max_tokens=120, temperature=0.3, lm_first=True)
        chosen = _parse_tool_list(raw, valid)
    if not chosen:
        low = message.lower()
        for t in tools:
            if t["name"] in low or any(w in low for w in t.get("desc", "").split()):
                chosen = [t["name"]]
                break
    if not chosen:
        chosen = [tools[0]["name"]]      # 최종 폴백: 대표 도구 1개

    chosen = chosen[:max_steps]

    # 2) 순차 실행
    blocks = [f"🤖 [{meta.get('display')}] 작업 처리 — 선택 도구: {', '.join(chosen)}"]
    results = []
    for name in chosen:
        code, out = _run_tool(by_name[name], message)
        icon = "✅" if code == 0 else "❌"
        snippet = out[:1500]
        blocks.append(f"\n{icon} [{name}] {by_name[name].get('desc','')}\n{snippet}")
        results.append((name, code, out))

    # 3) 다중 도구면 LLM 종합 (선택)
    if len(results) > 1 and llm_available():
        joined = "\n\n".join(f"[{n}]\n{o[:800]}" for n, _, o in results)
        summary = llm_text(
            f"다음은 '{message}' 요청에 대한 도구 실행 결과들이다. 3~4줄로 종합 결론만:\n\n{joined}",
            max_tokens=300, temperature=0.4, lm_first=True)
        if summary:
            blocks.append(f"\n📌 종합: {summary.strip()}")

    return "\n".join(blocks)


def _cli() -> None:
    import argparse
    p = argparse.ArgumentParser(description="에이전트 판단 루프")
    p.add_argument("agent", help="에이전트 id (somi/youngsuk/ceo)")
    p.add_argument("message", help="요청 메시지")
    p.add_argument("--max-steps", type=int, default=MAX_STEPS_DEFAULT)
    args = p.parse_args()
    print(run_agent(args.agent, args.message, args.max_steps))


if __name__ == "__main__":
    _cli()
