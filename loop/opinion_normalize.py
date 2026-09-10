#!/usr/bin/env python3
"""CLI 원시 출력 → 지휘 보드 의견 형식(1줄 제목 + 본문).

사용: opinion_normalize.py <post> <raw파일> <출력파일>
  post = text | grok-json | file:<경로>
종료 코드 0=성공, 3=빈 결과.

그록 헤드리스는 조사 중 멘트("…읽고 정리하겠습니다.")를 최종 답 첫 줄 앞에 개행 없이 이어 붙인다
(2026-09-10 Grok 자신이 지적). 제목 줄에서 「…니다.」로 끝나는 서술 문장을 잘라내고 마지막 조각을 제목으로 쓴다.
"""
import io
import json
import re
import sys

NARRATION_SPLIT = re.compile(r"(?<=니다\.)\s*(?=\S)")
NARRATION_TAIL = re.compile(r"(니다|합니다|하겠습니다)\.$")


def read(path: str) -> str:
    try:
        return io.open(path, encoding="utf-8", errors="replace").read()
    except FileNotFoundError:
        return ""


def strip_narration(first: str) -> str:
    parts = [p for p in NARRATION_SPLIT.split(first) if p.strip()]
    if len(parts) <= 1:
        return first
    # 앞쪽이 전부 서술문이면 마지막 조각만 남긴다. 마지막 조각도 서술문이면 원문 유지(정보 손실 방지).
    head, last = parts[:-1], parts[-1]
    if all(NARRATION_TAIL.search(p.strip()) for p in head) and not NARRATION_TAIL.search(last.strip()):
        return last.strip()
    return first


def normalize(post: str, raw: str) -> str:
    text = ""
    if post == "grok-json":
        try:
            outer = json.loads(raw)
            inner = outer.get("text", "") if isinstance(outer, dict) else ""
            text = inner
            # 모델이 {"title","body"} JSON(코드펜스 포함)으로 답한 경우 — 그대로 제목이 되면 안 된다
            stripped = re.sub(r"^\s*```(?:json)?\s*|\s*```\s*$", "", inner.strip())
            if stripped.startswith("{"):
                try:
                    obj = json.loads(stripped)
                    text = (str(obj.get("title", "")).strip() + "\n" + str(obj.get("body", "")).strip()).strip()
                except Exception:
                    m = re.search(r'"title"\s*:\s*"((?:[^"\\]|\\.)*)"', stripped)
                    b = re.search(r'"body"\s*:\s*"((?:[^"\\]|\\.)*)"', stripped)
                    if m:
                        text = (m.group(1) + "\n" + (b.group(1) if b else "")).replace("\\n", "\n").strip()
        except Exception:
            text = raw
    elif post.startswith("file:"):
        text = read(post[5:]) or raw
    else:
        text = raw
    text = re.sub(r"\x1b\[[0-9;]*m", "", text)
    lines = [l.rstrip() for l in text.splitlines()]
    lines = [l for l in lines if l.strip()]
    if lines:
        for _ in range(3):  # "# 제목: **…**" 처럼 접두가 겹쳐도 다 벗긴다
            lines[0] = re.sub(r"^\s*(#+\s*|\*\*|제목\s*[:：]\s*)", "", lines[0]).strip().strip("*").strip()
        lines[0] = strip_narration(lines[0])
        if len(lines) > 1 and re.match(r"^\s*(본문\s*[:：])", lines[1]):
            lines[1] = re.sub(r"^\s*본문\s*[:：]\s*", "", lines[1])
    return "\n".join(lines).strip()


def main() -> int:
    post, raw_path, dest = sys.argv[1], sys.argv[2], sys.argv[3]
    out = normalize(post, read(raw_path))
    io.open(dest, "w", encoding="utf-8", newline="\n").write(out + ("\n" if out else ""))
    return 0 if out else 3


if __name__ == "__main__":
    sys.exit(main())
