#!/usr/bin/env python3
"""
kodari_ollama.py — 공용 ollama_client 사용 펫과나 자율 개발
Usage: python3 projects/ai-team/scripts/kodari_ollama.py
"""
import os
import re
import subprocess
import sys
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]   # scripts/→ai-team/→projects/→ai_lab/
PETNNA = ROOT / "projects/petnna"
AI_TEAM = ROOT / "projects/ai-team"

sys.path.insert(0, str(AI_TEAM))
from _shared.env_loader import load_env
from _shared import ollama_client as lm

load_env()


def read_file(path: Path, lines: int = None) -> str:
    try:
        text = path.read_text(encoding="utf-8")
        return "\n".join(text.splitlines()[:lines]) if lines else text
    except Exception:
        return ""


def get_context() -> str:
    meeting = read_file(ROOT / "reports/meetings/2026-06-03-meeting.md", 200)
    progress = read_file(AI_TEAM / "docs/progress.md")
    return f"=== 회의록 ===\n{meeting[:4000]}\n\n=== progress.md ===\n{progress}"


def apply_changes(result: str) -> list:
    changed = []

    # APPEND 패턴: APPEND js/파일.js\n```js\n코드\n```
    for m in re.finditer(r'APPEND\s+([\w/._-]+)\n```[^\n]*\n([\s\S]+?)```', result):
        fpath = PETNNA / m.group(1).strip()
        code = "\n\n" + m.group(2).strip()
        if fpath.exists():
            with open(fpath, "a", encoding="utf-8") as f:
                f.write(code)
            changed.append(str(fpath.relative_to(ROOT)))
            print(f"  ✅ APPEND → {fpath.name}")

    # REPLACE 패턴: REPLACE js/파일.js\nOLD:\n```\n...\n```\nNEW:\n```\n...\n```
    for m in re.finditer(
        r'REPLACE\s+([\w/._-]+)\nOLD:\n```[^\n]*\n([\s\S]+?)```\nNEW:\n```[^\n]*\n([\s\S]+?)```',
        result
    ):
        fpath = PETNNA / m.group(1).strip()
        old, new = m.group(2).strip(), m.group(3).strip()
        if fpath.exists():
            content = fpath.read_text(encoding="utf-8")
            if old in content:
                fpath.write_text(content.replace(old, new, 1), encoding="utf-8")
                changed.append(str(fpath.relative_to(ROOT)))
                print(f"  ✅ REPLACE → {fpath.name}")
            else:
                print(f"  ⚠️  OLD 코드 미발견: {fpath.name}")

    return changed


def main():
    if not lm.is_available():
        print("❌ Ollama 서버 미실행 (http://localhost:11434)")
        sys.exit(1)

    model = lm._pick_model(lm._list_models(), "coding") or "auto"
    print(f"🤖 코다리(Ollama/{model}) 시작 — {datetime.now().strftime('%H:%M:%S')}")

    context = get_context()

    # Step 1: 미구현 항목 선정
    print("\n📋 구현 항목 선택 중...")
    plan = lm.chat(
        prompt=f"""{context}

회의록·progress.md 기준 미구현 기능 중 난이도 낮음~중간 2개를 고르세요.

출력 형식 (정확히):
TASK | js/파일명.js | 구현 방법 한 줄
TASK | js/파일명.js | 구현 방법 한 줄""",
        system="Vanilla JS 전문 개발자. 출력 형식 정확히 준수.",
        task="coding",
        temperature=0.1,
    )
    print(f"\n{plan}\n")

    tasks = [l.strip() for l in plan.splitlines() if "|" in l and l.strip().startswith(("TASK", "- ", "1.", "2.")) or "|" in l]
    tasks = [t for t in tasks if "|" in t][:2]

    if not tasks:
        print("⚠️  파싱 실패 — 응답:", plan[:300])
        return

    all_changed = []

    # Step 2: 구현
    for task_line in tasks:
        parts = [p.strip() for p in task_line.split("|")]
        feature  = re.sub(r'^(TASK|[-\d.]+)\s*', '', parts[0]).strip()
        file_hint = parts[1].strip("/ ") if len(parts) > 1 else ""
        method   = parts[2] if len(parts) > 2 else ""

        print(f"\n🔨 구현: {feature} ({file_hint})")

        target_path = PETNNA / file_hint
        file_content = read_file(target_path, 300) if target_path.exists() else "파일 없음"

        impl = lm.chat(
            prompt=f"""펫과나 앱 기능 구현:

기능: {feature}
방법: {method}
파일: {file_hint}

현재 파일 (앞 300줄):
```
{file_content}
```

출력 형식 중 하나만 사용:

(1) 코드 추가:
APPEND {file_hint}
```js
추가 코드
```

(2) 코드 교체:
REPLACE {file_hint}
OLD:
```js
기존 코드 (정확히 일치)
```
NEW:
```js
새 코드
```

규칙: Vanilla JS, 기존 스타일, escapeHtml, localStorage fallback""",
            system="Vanilla JS 시니어. 출력 형식 정확히.",
            task="coding",
            temperature=0.1,
        )

        changed = apply_changes(impl)
        all_changed.extend(changed)
        if not changed:
            print(f"  ℹ️  변경 없음 — {impl[:100]}...")

    # Step 3: progress.md
    progress_path = AI_TEAM / "docs/progress.md"
    with open(progress_path, "a", encoding="utf-8") as f:
        f.write(f"\n\n## {datetime.now().strftime('%Y-%m-%d %H:%M')} — 코다리(Ollama/{model})\n")
        for t in tasks:
            f.write(f"- {t}\n")
        if all_changed:
            f.write(f"- 변경: {', '.join(all_changed)}\n")
    all_changed.append(str(progress_path.relative_to(ROOT)))

    # Step 4: git
    if all_changed:
        names = " / ".join([re.sub(r'^(TASK|[-\d.]+)\s*', '', t.split("|")[0]).strip() for t in tasks])
        subprocess.run(["git", "add"] + all_changed, cwd=ROOT)
        r = subprocess.run(
            ["git", "commit", "-m",
             f"feat(petnna): 코다리(Ollama) — {names}\n\nCo-Authored-By: Ollama/{model} <noreply@ollama.ai>"],
            cwd=ROOT, capture_output=True, text=True,
        )
        if r.returncode == 0:
            push = subprocess.run(["git", "push", "origin", "master"],
                                   cwd=ROOT, capture_output=True, text=True)
            print(f"\n{'✅ push 완료' if push.returncode == 0 else '❌ push 실패: ' + push.stderr}")
        else:
            print(f"\n⚠️  커밋: {r.stdout.strip() or r.stderr.strip()}")
    else:
        print("\n⚠️  변경 없음")

    print("\n🎉 완료!")


if __name__ == "__main__":
    main()
