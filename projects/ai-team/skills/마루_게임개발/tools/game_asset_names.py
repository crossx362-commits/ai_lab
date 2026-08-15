#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""재와 별 — 에셋 네이밍·반영 검사기.

오너 지시(2026-08-15): "파일 생성시 네이밍 관리 철저히".

이 저장소에서 아트가 화면에 안 나온 사고는 전부 **이름**에서 왔다:
- `Resources.Load`는 문자열로 찾는다 — 이름이 하나만 달라도 그 자산은 **조용히 없는 것**이 된다
  (`[FieldDecor] 프랍 누락` 경고만 남고 게임은 그냥 안 그린다)
- 2026-08-15: `Resources/props` 32장이 `art/out_ai` 32장과 **파일명은 같은데 내용은 전부 달랐다.**
  목록·개수만 보면 정상이라 16시간 동안 아무도 몰랐고, 게임은 계속 어제 그림을 그렸다.
- 같은 날: 생성 폴더에 `compare_*`·`sheet_all32`·`_old_*` 같은 대조본·임시본이 섞여
  "39장 만들었는데 32장만 들어갔다"는 잘못된 개수 대조를 만들었다.

그래서 **이름의 권위는 문서가 아니라 코드**다 — 런타임이 실제로 `Resources.Load`에 넘기는
문자열을 소스에서 뽑아 그것과 대조한다. 문서에 적어두는 방식은 코드가 바뀌면 조용히 어긋난다.

판정 3종:
1. **누락** — 코드가 찾는데 파일이 없다 (게임에서 안 그려진다)
2. **낡음** — 반영본과 생성본이 같은 이름인데 내용이 다르다 (가장 안 보이는 사고)
3. **잡동사니** — 생성 폴더에 자산이 아닌 것이 섞였다 (개수 대조를 망친다)

사용: `python3 game_asset_names.py [--strict]`   (--strict면 문제 발견 시 exit 1)
"""

from __future__ import annotations

import argparse
import filecmp
import re
import sys
from pathlib import Path

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = Path(__file__).resolve().parents[5]
GAME = ROOT / "projects" / "ashes-to-stars"
ASSETS = GAME / "unity" / "Assets"
RES = ASSETS / "Resources"
GEN = GAME / "art" / "out_ai"

# 자산이 아닌 산출물 — 생성 폴더에 있어도 반입 대상이 아니다.
JUNK = re.compile(r"^(_|compare|sheet_all|.*_sheet_scaled)")


def _src(path: Path) -> str:
    try:
        return path.read_text(encoding="utf-8", errors="replace")
    except Exception:
        return ""


def _array(path: Path, name: str) -> list[str]:
    """`name = { "a", "b" }` 형태의 **선언**에서 리터럴을 뽑는다.

    ⚠️ `str.find(name)`으로 첫 언급을 잡으면 안 된다 — 대개 주석이나 호출부가 먼저 나와서
    엉뚱한 구간을 읽는다(첫 구현이 정확히 이 오류로 4개 중 2개를 빈 값으로 돌려줬다).
    고정 길이 창도 쓰지 않는다 — `JOB_DIRS`가 뒤따르는 `JOB_FRAMES`까지 먹었다.
    중괄호로 범위를 닫는다.
    """
    m = re.search(re.escape(name) + r"\s*=\s*\{([^}]*)\}", _src(path))
    return re.findall(r'"([a-z0-9_]+)"', m.group(1)) if m else []


def _method_literals(path: Path, name: str) -> list[str]:
    """메서드 **정의** 이후 본문에서 리터럴을 뽑는다(반환 배열이 여러 갈래인 경우)."""
    src = _src(path)
    m = re.search(r"\b(?:string\[\]|String\[\])\s+" + re.escape(name) + r"\s*\(", src)
    if not m:
        return []
    return re.findall(r'"([a-z0-9_]+)"', src[m.start():m.start() + 1600])


def expected() -> dict[str, list[str]]:
    """런타임이 실제로 Resources.Load에 넘기는 이름을 소스에서 뽑는다."""
    sb = ASSETS / "Scripts" / "SpriteBank.cs"
    fd = ASSETS / "_Game" / "Scripts" / "Runtime" / "FieldDecor.cs"

    jobs = _array(sb, "JOB_DIRS")
    job_frames = _array(sb, "JOB_FRAMES")
    mob_dirs = _array(sb, "MOB_DIRS")
    mob_frames = _array(sb, "MOB_FRAMES")

    out: dict[str, list[str]] = {}
    if jobs and job_frames:
        for j in jobs:
            out[f"sprites/{j}"] = [f"{j}_{f}.png" for f in job_frames]
    # 몹도 직업과 **같은 규칙**이다 — `SpriteBank.cs:141` 이 둘 다 `$"{d}/{d}_{f}"`로 조합한다.
    # 첫 구현이 몹만 접두사 없이 기대해서 실재하는 22장을 전부 '누락'으로 신고했다
    # (오탐은 없는 문제를 쫓게 만들어 진짜 문제를 덮는다 — 조합 규칙은 코드에서 확인할 것).
    if mob_dirs and mob_frames:
        for m in mob_dirs:
            out[f"sprites/{m}"] = [f"{m}_{f}.png" for f in mob_frames]
    props = _method_literals(fd, "GetPropNames")
    if props:
        out["props"] = sorted({f"{p}.png" for p in props})
    return out


def scale_table_problems() -> list[str]:
    """프랍 목표 크기표(`prop_scale.json`)의 세 가지 사고를 잡는다 — 전부 2026-08-15 실제 발생.

    ① **두 벌이 어긋난다.** 단일 소스는 `art/prop_scale.json`이고 유니티는
       `Assets/Resources/prop_scale.json`을 읽는다. 아트 쪽만 고치면 게임은 옛 크기를 쓴다.
    ② **같은 키가 두 번 들어간다.** 루프와 대화 세션이 동시에 나무를 반입해 실제로 났다.
       JSON은 뒤엣것이 이기지만 파서마다 달라 조용히 크기가 뒤바뀔 수 있다.
    ③ **표에 없는 프랍**은 기본 1유닛으로 그려진다 — 집이 사람만 해지는 그 사고다.
    """
    import json

    art = ROOT / "projects/ashes-to-stars/art/prop_scale.json"
    res = RES / "prop_scale.json"        # ⚠️ RES가 이미 Resources다. parent는 Assets라 한 단계 위다
    out: list[str] = []

    if not art.exists() or not res.exists():
        return ["prop_scale.json 없음 — 프랍 크기가 전부 기본값(1유닛)이 된다"]

    a_txt, r_txt = art.read_text(encoding="utf-8"), res.read_text(encoding="utf-8")
    if a_txt != r_txt:
        out.append("prop_scale.json 두 벌이 다르다 — art/ 것을 Assets/Resources/로 복사할 것")

    for label, txt in (("art", a_txt), ("Resources", r_txt)):
        keys = re.findall(r'"([A-Za-z0-9_]+)"\s*:\s*[0-9.]+', txt)
        dup = sorted({k for k in keys if keys.count(k) > 1})
        if dup:
            out.append(f"prop_scale.json({label}) 중복 키 {len(dup)}개 — {', '.join(dup[:5])}")
        try:
            json.loads(txt)
        except Exception as e:      # noqa: BLE001 — 어떤 파싱 실패든 크기표가 통째로 죽는다
            out.append(f"prop_scale.json({label}) 파싱 실패: {e}")

    try:
        table = json.loads(r_txt)
    except Exception:               # noqa: BLE001 — 위에서 이미 보고했다
        return out

    props_dir = RES / "props"
    have = {p.stem for p in props_dir.glob("*.png")} if props_dir.exists() else set()
    missing = sorted(have - set(table))
    if missing:
        out.append(f"크기표에 없는 프랍 {len(missing)}개 — {', '.join(missing[:5])}"
                   + (" …" if len(missing) > 5 else "")
                   + "  → 기본 1유닛으로 그려진다(바위가 사람만 해지는 그 사고)")
    return out


def untracked_asset_problems() -> list[str]:
    """`Assets/` 아래에 **git이 모르는 자산**이 있으면 보고한다.

    2026-08-15 사고: 전투 스타일 화면과 이펙트 8종을 만들어 코드까지 붙였는데
    `Style.unity`·`fx_*.png`·새 스크립트 3개의 `.meta`가 **전부 추적 밖**이었다.
    이 상태로 다른 기계가 받으면 코드만 있고 자산이 없어서, 씬 전환은 실패하고
    이펙트는 조용히 안 그려진다 — 그런데 **작업한 기계에서는 전부 정상으로 보인다.**
    로컬에서 잘 도는 것이 "커밋됐다"는 뜻이 아니다.

    `.meta`가 특히 위험하다: meta 없이 스크립트만 커밋하면 다른 기계에서 GUID가
    새로 생겨 **씬이 들고 있던 참조가 통째로 끊긴다.**
    """
    import subprocess

    try:
        out = subprocess.run(
            ["git", "ls-files", "--others", "--exclude-standard", "--", str(ASSETS)],
            cwd=ROOT, capture_output=True, text=True, timeout=30,
        )
    except Exception as e:                     # noqa: BLE001 — git이 없거나 저장소가 아니면 판정 불능
        return [f"추적 상태 확인 불가: {e}"]
    if out.returncode != 0:
        return [f"추적 상태 확인 불가(git 오류): {out.stderr.strip()[:80]}"]

    files = [f for f in out.stdout.splitlines() if f.strip()]
    if not files:
        return []

    # 씬·스크립트·meta는 없으면 **기능이 깨진다**. 그림은 안 보일 뿐이다 — 나눠서 말한다.
    critical = [f for f in files if f.endswith((".unity", ".cs", ".meta", ".asset", ".prefab"))]
    rest = [f for f in files if f not in critical]

    msgs = []
    if critical:
        msgs.append(f"⛔ 커밋 안 된 자산 {len(critical)}개(씬·스크립트·meta) — "
                    + ", ".join(Path(f).name for f in critical[:4])
                    + (" …" if len(critical) > 4 else "")
                    + "  → 다른 기계에서 참조가 끊기거나 화면이 안 뜬다")
    if rest:
        msgs.append(f"커밋 안 된 자산 {len(rest)}개(그림 등) — "
                    + ", ".join(Path(f).name for f in rest[:4])
                    + (" …" if len(rest) > 4 else ""))
    return msgs


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--strict", action="store_true")
    args = ap.parse_args()

    problems: list[str] = []
    exp = expected()
    if not exp:
        # 소스에서 하나도 못 뽑았으면 '이상 없음'이 아니라 **검사 불능**이다.
        # 빈 결과를 통과로 읽으면 검사기가 침묵장치가 된다.
        print("❌ 코드에서 기대 이름을 하나도 못 뽑았다 — 검사 불능(소스 구조가 바뀌었는지 확인)")
        sys.exit(1)

    # ── 1. 누락
    for folder, names in exp.items():
        d = RES / folder
        have = {p.name for p in d.glob("*.png")} if d.exists() else set()
        miss = [n for n in names if n not in have]
        if miss:
            problems.append(f"누락 {folder}: {len(miss)}개 — {', '.join(miss[:5])}"
                            + (" …" if len(miss) > 5 else ""))

    # ── 2. 낡음 (같은 이름, 다른 내용)
    stale = []
    if GEN.exists():
        for src in GEN.glob("*.png"):
            if JUNK.match(src.name):
                continue
            for d in RES.rglob(src.name):
                if not filecmp.cmp(src, d, shallow=False):
                    stale.append(src.name)
                break
    if stale:
        problems.append(f"낡음(이름 같고 내용 다름) {len(stale)}개 — {', '.join(sorted(stale)[:5])}"
                        + (" …" if len(stale) > 5 else "")
                        + "  → 생성본이 게임에 반영되지 않았다")

    # ── 3. 잡동사니
    junk = [p.name for p in GEN.glob("*.png") if JUNK.match(p.name)] if GEN.exists() else []
    if junk:
        problems.append(f"생성 폴더에 자산 아닌 파일 {len(junk)}개 — {', '.join(sorted(junk)[:5])}"
                        + (" …" if len(junk) > 5 else "")
                        + "  → 개수 대조가 틀어진다. 대조본·임시본은 별도 폴더로")

    # ── 4. meta 짝
    for folder in exp:
        d = RES / folder
        if not d.exists():
            continue
        png, meta = len(list(d.glob("*.png"))), len(list(d.glob("*.png.meta")))
        if png != meta:
            problems.append(f"meta 불일치 {folder}: png {png} vs meta {meta} — GUID 고아 위험")

    problems += scale_table_problems()
    problems += untracked_asset_problems()

    if not problems:
        print("✅ 네이밍·반영 이상 없음")
        return
    for p in problems:
        print(f"⚠️ {p}")
    if args.strict:
        sys.exit(1)


if __name__ == "__main__":
    main()
