#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""재와 별 개발 회의 — 기획서를 기준으로 다음에 뭘 할지 정하고 작업지시서를 낸다 (의장: 마루).

왜 만들었나: 오너가 저녁 내내 "다음 뭐 해야 돼?"를 사람이 판정하고 세션에 지시해 왔다.
그 판정은 항상 같은 자료(기획서 ✅ 확정 · 최근 커밋 · 측정 결과 · 핸드오프의 남은 과제)를 보고
같은 질문에 답한다 — 「기획서가 말하는 것 중 코드에서 아직 성립 안 하는 게 뭔가」.
그걸 기계화한다.

진행:
1. 6인이 각자 렌즈로 기획서와 코드를 **직접 읽고** 독립 의견 제출 (병렬, plan 모드 = 수정 불가)
2. 의장 마루가 종합해 결론·다음 작업 3~5개 도출
3. 액션아이템 → backlog.json  +  **ORDERS.md(작업지시서)** 생성 → 개발 세션이 읽는다
4. 회의록 저장 + 텔레그램 요약

안전선 (이 저장소가 실제로 겪은 사고에서 온 것):
- 회의는 조언·과제 생성만 한다. 코드 무수정.
- **유니티·블렌더를 실행하지 않는다** — 에디터 락을 잡으면 개발 세션 빌드가 exit 21로 죽는다.
  (2026-08-06: 검토 서브에이전트가 `vercel dev`를 실행해 오너 계정에 프로젝트를 만든 사고와 같은 계열.
   문서 경고가 아니라 프롬프트로 금지해야 막힌다.)
- 결론은 **가설**이다. 회의 6인 전원이 코드를 오독한 전례가 있어(2026-07-25), 각 의견은
  자기가 실제로 읽은 근거(파일:줄)를 대야 하고 못 읽었으면 '미확인'으로 적는다.
- 같은 안건 24시간 내 재소집 안 함.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
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

from _shared.env import load_env        # noqa: E402
from _shared.telegram import send       # noqa: E402
from _shared.process import advisory_lock  # noqa: E402
from _shared.cc import run_claude, extract_json  # noqa: E402

load_env(str(PROJECT_ROOT))

OUT_DIR = PROJECT_ROOT / "output" / "qa" / "ashes-to-stars" / "council"
STATE = OUT_DIR / "state.json"
BACKLOG = PROJECT_ROOT / "output" / "qa" / "ashes-to-stars" / "backlog.json"
ORDERS = PROJECT_ROOT / "output" / "qa" / "ashes-to-stars" / "ORDERS.md"
COOLDOWN_H = 24
FAIL = "(의견 수집 실패)"

DESIGN = "docs/GAME_DESIGN_ASHES_TO_STARS.md"

# 렌즈가 겹치면 같은 말을 6번 듣는다. 서로 다른 실패 모드를 보게 나눈다.
PERSONAS = [
    ("정합성", "기획서 ✅ 확정과 코드가 어긋난 곳을 찾는다. 문서끼리 모순되면 기획서가 이긴다"),
    ("구현",   "클라이언트 코드의 실제 상태·부채·죽은 데이터(설정은 있는데 읽는 코드가 0곳)를 본다"),
    ("밸런스", "수치가 §18 앵커에서 유도된 것인지, 측정이 그걸 실제로 재고 있는지 본다"),
    ("연출",   "화풍 유지·가독성·타격감. 500체 화면에서 규칙이 눈에 읽히는지 본다"),
    ("검증",   "측정이 '측정하려던 것'을 쟀는지 본다. 네거티브 컨트롤이 없는 통과는 통과가 아니다"),
    ("우선순위", "다음 수직 슬라이스가 뭔지 정한다. 재미가 먼저 성립해야 할 것부터"),
]

# 이 저장소가 반복해서 낸 오답 4종 — 프롬프트에 박아 같은 실수를 막는다.
GROUND_RULES = """
[반드시 지킬 것]
1. **직접 읽고 말하라.** 주장마다 근거를 `파일:줄`로 대라. 못 읽었으면 '미확인'이라고 적어라.
   이 팀은 6인 전원이 코드를 오독해 멀쩡한 코드를 "고치자"고 결의한 전례가 있다(2026-07-25).
   템플릿의 정적 표시값을 실제 로직으로 착각한 것이었다.
2. **이미 있는 것을 다시 만들라고 하지 마라.** 제안 전에 Grep/Glob으로 그 기능·테스트가
   이미 있는지 확인하고, 있으면 '이미 있음(경로)'이라고 적어라. 보류 과제의 10%가 이 유형이었다.
3. **유니티·블렌더를 실행하지 마라.** 배치 빌드도 금지다. 에디터 락을 잡으면 지금 돌고 있는
   개발 세션의 빌드가 exit 21로 죽는다. 외부 계정·과금·게시에 영향을 주는 명령도 전부 금지.
4. **코드를 수정하지 마라.** 이 회의는 조언과 과제 생성만 한다.
"""


def _key(topic: str) -> str:
    return hashlib.md5(topic.encode("utf-8")).hexdigest()[:10]


def _recent(topic: str) -> bool:
    try:
        last = json.loads(STATE.read_text(encoding="utf-8")).get(_key(topic))
    except Exception:
        return False
    if not last:
        return False
    return (datetime.now() - datetime.fromisoformat(last)).total_seconds() < COOLDOWN_H * 3600


def _mark(topic: str, clear: bool = False) -> None:
    """락을 잡은 뒤에만 기록한다 — 안 그러면 열리지도 않은 회의가 24h 봉쇄한다."""
    try:
        state = json.loads(STATE.read_text(encoding="utf-8"))
    except Exception:
        state = {}
    if clear:
        state.pop(_key(topic), None)
    else:
        state[_key(topic)] = datetime.now().isoformat()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    STATE.write_text(json.dumps(state, ensure_ascii=False, indent=1), encoding="utf-8")


def _sh(cmd: str) -> str:
    try:
        return subprocess.run(cmd, shell=True, cwd=PROJECT_ROOT, capture_output=True,
                              text=True, timeout=30, encoding="utf-8").stdout.strip()
    except Exception as e:
        return f"(수집 실패: {e})"


def _pending_backlog() -> str:
    try:
        data = json.loads(BACKLOG.read_text(encoding="utf-8"))
    except Exception:
        return "(없음)"
    titles = [i.get("title", "") for i in data if i.get("status") == "대기"]
    return "\n".join(titles) or "(없음)"


def _situation() -> str:
    """회의가 볼 현재 상태. 사람이 매번 손으로 모으던 것."""
    results = PROJECT_ROOT / "projects" / "ashes-to-stars" / "results"
    qa = PROJECT_ROOT / "output" / "qa" / "ashes-to-stars"
    recent_qa = sorted(qa.glob("regression_*.md"))[-1].name if list(qa.glob("regression_*.md")) else "(없음)"
    # 미병합 브랜치·워크트리를 반드시 넣는다(2026-08-14 첫 실행에서 실제로 터진 결함):
    # C7 에이전트가 스타일 소비 클래스 3개를 이미 만들어 워크트리에 올려뒀는데, 회의가
    # 그걸 못 봐서 "신규 클래스 생성 없이 새로 배선하라"는 지시를 냈다 — 이 도구가
    # 막으려던 「이미 있는 것을 다시 만들라」를 회의 자신이 저지른 것이다.
    # git status는 워크트리 밖 변경을 보여주지 않으므로 별도로 물어야 한다.
    return (
        f"[최근 커밋 12개]\n{_sh('git log --oneline -12')}\n\n"
        f"[미커밋 변경]\n{_sh('git status --short') or '(없음)'}\n\n"
        f"[미병합 브랜치 — 여기 이미 구현돼 있을 수 있다]\n"
        f"{_sh('git branch --format=\"%(refname:short) %(committerdate:relative)\" | grep -v \"^master\"') or '(없음)'}\n\n"
        f"[격리 워크트리]\n{_sh('git worktree list') or '(없음)'}\n\n"
        f"[백로그 대기 과제]\n{_pending_backlog()}\n\n"
        f"[측정 결과 파일]\n{_sh(f'ls -t {results} 2>/dev/null | head -12')}\n\n"
        f"[최신 회귀 리포트] {recent_qa}\n"
    )


def _opinion(lens: str, focus: str, topic: str, situation: str) -> tuple[str, str]:
    prompt = (
        f"너는 '재와 별(Ashes to Stars)' 개발 회의의 **{lens}** 담당이다.\n"
        f"너의 렌즈: {focus}\n\n"
        f"[안건]\n{topic}\n\n"
        f"[현재 상태 — 이미 수집됨]\n{situation}\n\n"
        "[읽을 것 — 판단에 필요한 것만]\n"
        f"- 기획서(최상위 권위): {DESIGN} — ✅=오너 확정, 💡=제안, ⚠️=주의, 📌=폐기\n"
        "- 수치 근거: 같은 문서 §18 밸런싱 기준표 / 검증 결과: §21\n"
        "- 스펙: docs/GAME_SPEC_*.md · 아트: docs/GAME_ART_RESOURCES.md(§0-B가 물량 권위)\n"
        "- 남은 과제·함정 목록: docs/GAME_DEV_HANDOFF.md §4·§5\n"
        "- 코드: projects/ashes-to-stars/unity/Assets/\n"
        + GROUND_RULES +
        "\n[출력 — 이 형식 그대로, 8줄 이내]\n"
        "입장: <한 줄>\n"
        "근거: <최대 3줄, 각 줄 끝에 파일:줄 또는 '미확인'>\n"
        "제안: <구체적 1~2개. 이미 있으면 '이미 있음(경로)'>\n"
        "리스크: <한 줄>"
    )
    ok, out = run_claude(prompt, PROJECT_ROOT, timeout=420,
                         allowed_tools="Read,Grep,Glob,WebSearch",
                         permission_mode="plan")
    return lens, (out.strip()[:1500] if ok and out else FAIL)


def _chair(topic: str, situation: str, opinions: list[tuple[str, str]]) -> tuple[str, list]:
    text = "\n\n".join(f"### {n}\n{o}" for n, o in opinions)
    ok, out = run_claude(
        "너는 재와 별 개발팀의 마루, 이 회의의 의장이다. 헌장은 "
        "projects/ai-team/skills/마루_게임개발/SKILL.md 에 있다. 군더더기 없이 핵심만.\n\n"
        f"[안건]\n{topic}\n\n[현재 상태]\n{situation}\n\n[의견]\n{text}\n\n"
        "[할 일]\n"
        "1. 결론 5줄 이내. 의견이 충돌하면 명시하고 판정하라.\n"
        "2. 결정 사항을 번호 목록으로.\n"
        "3. 마지막에 다음 작업을 JSON 배열로 3~5개, 실행 순서대로:\n"
        '[{"title":"...","detail":"무엇을 어떻게 — 통과 기준까지","priority":"P1|P2|P3",'
        '"track":"개발|그래픽","verify":"이걸 어떻게 확인하는가 — 네거티브 컨트롤 포함",'
        '"needs_owner":true|false}]\n'
        "- track: 유니티 코드·밸런스·측정은 '개발', 아트·스프라이트·연출은 '그래픽'.\n"
        "- verify가 비면 안 된다. 이 팀은 '코드를 넣었다'와 '화면에 나왔다'를 구분한다.\n"
        "- 기획서에 없는 새 메커니즘을 추가하거나 ✅ 확정을 뒤집는 것은 needs_owner=true.",
        PROJECT_ROOT, timeout=420, allowed_tools="Read", permission_mode="plan")
    if not ok or not out:
        return "(의장 종합 실패)", []
    items = extract_json(out)
    return out.strip(), (items if isinstance(items, list) else [])


def _write_orders(topic: str, items: list, ts: str) -> None:
    """개발 세션이 읽는 단일 작업지시서. 사람이 매번 손으로 쓰던 것."""
    lines = [f"# 재와 별 — 작업지시서 ({ts})", "",
             f"> 안건: {topic}", "> 회의가 자동 생성했다. 위에서부터 순서대로 진행한다.",
             "> ⚠️ 결론은 **가설**이다 — 지목된 코드를 직접 열어 확인하고, 오진이면 그 사유를 적고 종결하라.", ""]
    for track in ("개발", "그래픽"):
        mine = [i for i in items if i.get("track") == track]
        if not mine:
            continue
        lines += [f"## {track} 세션", ""]
        for n, i in enumerate(mine, 1):
            owner = " **[오너 판단 필요]**" if i.get("needs_owner") else ""
            lines += [f"### {n}. {i.get('title','(제목 없음)')} ({i.get('priority','P2')}){owner}",
                      f"{i.get('detail','')}", "",
                      f"- **통과 기준·검증**: {i.get('verify','(미기재 — 회의 결함)')}", ""]
    ORDERS.parent.mkdir(parents=True, exist_ok=True)
    ORDERS.write_text("\n".join(lines), encoding="utf-8")


def _append_backlog(items: list, ts: str) -> None:
    try:
        data = json.loads(BACKLOG.read_text(encoding="utf-8"))
    except Exception:
        data = []
    known = {i.get("title") for i in data}
    for i in items:
        if i.get("title") in known:      # 같은 제안을 매 회의마다 다시 쌓지 않는다
            continue
        data.append({**i, "id": f"회의_{ts}_{len(data)}", "status": "대기",
                     "source": "회의", "created": datetime.now().isoformat()})
    BACKLOG.parent.mkdir(parents=True, exist_ok=True)
    BACKLOG.write_text(json.dumps(data, ensure_ascii=False, indent=1), encoding="utf-8")


def hold(topic: str) -> bool:
    _mark(topic)
    ts = datetime.now().strftime("%Y%m%d_%H%M")
    print(f"[{datetime.now()}] 🎮 개발 회의 — {topic[:80]}")
    situation = _situation()

    with ThreadPoolExecutor(max_workers=3) as ex:
        opinions = [f.result() for f in
                    [ex.submit(_opinion, n, d, topic, situation) for n, d in PERSONAS]]

    if all(o == FAIL for _, o in opinions):
        # 전원 실패 = 이 안건 탓이 아니라 클로드 호출 자체가 전멸. 쿨다운을 소진시키면
        # 24시간 재소집 불가로 방치된다.
        _mark(topic, clear=True)
        print("[회의] 전원 의견수집 실패(인프라) — 쿨다운 미소진")
        return False

    summary, items = _chair(topic, situation, opinions)
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    (OUT_DIR / f"minutes_{ts}.md").write_text(
        f"# 재와 별 개발 회의 — {ts}\n\n## 안건\n{topic}\n\n## 현재 상태\n```\n{situation}\n```\n\n"
        + "\n\n".join(f"## {n}\n{o}" for n, o in opinions)
        + f"\n\n## 의장 종합 (마루)\n{summary}\n", encoding="utf-8")

    if items:
        _append_backlog(items, ts)
        _write_orders(topic, items, ts)

    head = "\n".join(f"{n}. {i.get('title','')}" for n, i in enumerate(items[:5], 1)) or "(액션 없음)"
    send(f"🎮 재와 별 개발 회의 — {topic[:50]}\n\n{head}\n\n지시서: output/qa/ashes-to-stars/ORDERS.md")
    print(f"[회의] 완료 — 액션 {len(items)}건, 지시서 갱신")
    return True


DEFAULT_TOPIC = ("기획서 기준으로 지금 게임에서 가장 크게 어긋나 있거나 비어 있는 것은 무엇이고, "
                 "다음에 무엇을 만들어야 하는가")


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--topic", default=DEFAULT_TOPIC)
    ap.add_argument("--force", action="store_true", help="쿨다운 무시")
    args = ap.parse_args()

    if not args.force and _recent(args.topic):
        print(f"[회의] 동일 안건 {COOLDOWN_H}시간 내 개최됨 — 생략")
        return
    with advisory_lock("game_council") as got:
        if not got:
            print("[회의] 다른 회의 진행 중 — 생략")
            return
        hold(args.topic)


if __name__ == "__main__":
    main()
