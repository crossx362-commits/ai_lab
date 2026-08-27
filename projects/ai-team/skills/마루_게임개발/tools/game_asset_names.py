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

판정 4종:
1. **누락** — 코드가 찾는데 파일이 없다 (게임에서 안 그려진다)
2. **낡음** — 반영본과 생성본이 같은 이름인데 내용이 다르다 (가장 안 보이는 사고)
3. **잡동사니** — 생성 폴더에 자산이 아닌 것이 섞였다 (개수 대조를 망친다)
4. **미사용** — Resources에 있는데 코드가 안 읽는다 (빌드에 실리고 검사기는 침묵했다)

사용: `python3 game_asset_names.py [--strict] [--png-load] [--zip-load] [--self-test] [--zip-self-test]`
  --strict        문제 발견 시 exit 1
  --png-load      보간 `$"..."` 접두 PNG 실로드만 (커밋 가드)
  --zip-load      리터럴·보간 `.zip` 실로드/unzip만 (커밋 가드)
  --self-test     접두 추출 + 네거티브(없는 보간 로드) + live
  --zip-self-test zip 접두 + 네거티브(없는 zip·빈 unzip) + live
"""

from __future__ import annotations

import argparse
import filecmp
import os
import re
import sys
import zipfile
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

# 보간 `$"FX/fx_dash_trail_{i}"` · `$"props/{id}_0"` (회의 20260827-081437 채택 #1)
_RES_HEAD = re.compile(r"^(props|sprites|fx|ui|ground|bg)(/|$)", re.I)
_INTERP_LOAD = re.compile(
    r'Resources\.Load(?:<(?P<type>[^>]+)>)?\s*\(\s*\$"(?P<raw>[^"]*)"',
    re.DOTALL,
)
_INTERP_RES = re.compile(
    r'\$"((?:props|sprites|fx|FX|ui|ground|bg)/[^"]*)"',
)



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
    """메서드 **정의** 이후 본문에서 리터럴을 뽑는다(반환 배열이 여러 갈래인 경우).

    고정 글자 창은 쓰지 않는다 — `GetPropNames` 주석이 늘자 1600자가 던전 프랍
    앞에서 잘려, 코드가 읽는 그림을 미사용으로 오탐했다(2026-08-16).
    """
    src = _src(path)
    m = re.search(r"\b(?:string\[\]|String\[\])\s+" + re.escape(name) + r"\s*\(", src)
    if not m:
        return []
    brace = src.find("{", m.start())
    if brace < 0:
        return []
    depth = 0
    end = brace
    for i, ch in enumerate(src[brace:], brace):
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                end = i + 1
                break
    return re.findall(r'"([a-z0-9_]+)"', src[m.start():end])


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
    boss_dirs = _array(sb, "BOSS_DIRS")
    boss_frames = _array(sb, "BOSS_FRAMES")
    if boss_dirs and boss_frames:
        for d in boss_dirs:
            out[f"sprites/{d}"] = [f"{d}_{f}.png" for f in boss_frames]
    props = set(_method_literals(fd, "GetPropNames"))
    # 영지 전용 건물(대장간·영묘·탑)은 필드 산포에 안 넣는다 — EstateBuildings가 읽는다.
    eb = ASSETS / "_Game" / "Scripts" / "Runtime" / "EstateBuildings.cs"
    if eb.exists():
        props.update(re.findall(r'"(estate_[a-z0-9_]+|village_[a-z0-9_]+)"', _src(eb)))
    # ArenaLayout은 GetPropNames에 없는 접두(cover·wall)로 던전 장애물을 세운다.
    al = ASSETS / "_Game" / "Scripts" / "Runtime" / "ArenaLayout.cs"
    for prefix in re.findall(r'"(dungeon_[a-z]+_)"', _src(al)):
        for i in range(3):
            props.add(f"{prefix}{i}")
    if props:
        out["props"] = sorted({f"{p}.png" for p in props})

    # SpriteBank 루트 폴백·보스 실루엣 — 폴더가 아니라 sprites/ 바로 아래.
    roots = _array(sb, "MOB_KEYS") + _array(sb, "BOSS_KEYS")
    roots += re.findall(r'"(player_knight_0|elite_healer_0|boss_0)"', _src(sb))
    if roots:
        out["sprites"] = sorted({f"{n}.png" for n in roots})

    rt = ASSETS / "_Game" / "Scripts" / "Runtime"
    fx_names = (
        _array(rt / "FxPool.cs", "FILES")
        + _array(rt / "JobVfxSheets.cs", "Keys")
        + _array(rt / "StatusVfxSheets.cs", "Files")
    )
    # CombatVfxAtlas.Keys는 아틀라스 *칸* 이름이다. 파일은 combat_vfx_atlas 한 장뿐.
    if "fx/combat_vfx_atlas" in _src(rt / "CombatVfxAtlas.cs"):
        fx_names.append("combat_vfx_atlas")
    fx_folder = "FX" if (RES / "FX").is_dir() else "fx"
    if fx_names:
        out[fx_folder] = sorted({f"{n}.png" for n in fx_names})

    bgs: list[str] = []
    uis: list[str] = []
    chrome_names: list[str] = []
    for p in rt.glob("*.cs"):
        src = _src(p)
        bgs += re.findall(r'BackgroundArt\s*=>\s*"(bg_[a-z]+)"', src)
        uis += re.findall(r'ResourceKey\s*=\s*"ui/([^"]+)"', src)
        chrome_names += re.findall(r'"ui/chrome/([^"]+)"', src)
    if bgs:
        out["bg"] = sorted({f"{n}.png" for n in bgs})
    if chrome_names:
        out["ui/chrome"] = sorted({f"{n}.png" for n in chrome_names})
    if uis:
        out["ui"] = sorted({f"{n}.png" for n in uis})

    grounds: list[str] = []
    for p in (
        ASSETS / "Scripts" / "GroundBuilder.cs",
        ASSETS / "Scripts" / "W3Party.cs",
        ASSETS / "Scripts" / "SpriteBank.cs",
        rt / "NoiseTerrain.cs",
    ):
        grounds += re.findall(r'"ground/([a-z0-9_]+)"', _src(p))
    if grounds:
        out["ground"] = sorted({f"{n}.png" for n in grounds})
    return out


def _norm_res_rel(rel: str) -> str:
    """Resources 상대경로를 소문자로 정규화. Unity는 fx/FX를 같은 폴더로 본다."""
    rel = rel.replace("\\", "/").lower()
    if rel.startswith("fx/"):
        return rel
    return rel


def consumed_relpaths() -> set[str]:
    """코드가 읽는 PNG의 Resources 상대경로(소문자, 확장자 포함)."""
    out: set[str] = set()
    for folder, names in expected().items():
        folder_n = _norm_res_rel(folder)
        for n in names:
            out.add(f"{folder_n}/{n.lower()}")
    # `$"FX/fx_dash_trail_{i}"` 처럼 배열에 없는 보간 로드도 소비로 친다.
    # 폴더만(`sprites/` + 빈 접미)은 너무 넓어 미사용 고아를 가린다 — 접두 줄기나 접미가 있을 때만.
    for pat in interpolated_resource_patterns():
        if not _interp_specific_enough(pat["prefix"], pat["suffix"]):
            continue
        for rel in match_interp_pngs(pat["prefix"], pat["suffix"]):
            out.add(_norm_res_rel(rel))
    return out


def unused_resource_problems(res: Path | None = None) -> list[str]:
    """Resources에 있는데 코드가 안 읽는 PNG.

    2026-08-16: 미사용 FX 28장·estate 통·battle_background_atlas가 Resources에
    남아 있었는데, 검사기는 누락만 봐서 침묵했다. 목록에 있으면 쓰는 줄 안다.
    """
    root = res or RES
    if not root.is_dir():
        return []
    consumed = consumed_relpaths()
    unused: list[str] = []
    for p in sorted(root.rglob("*.png")):
        rel = p.relative_to(root).as_posix()
        if _norm_res_rel(rel) not in consumed:
            unused.append(rel)
    if not unused:
        return []
    return [
        f"미사용 Resources {len(unused)}개 — {', '.join(unused[:5])}"
        + (" …" if len(unused) > 5 else "")
        + "  → 코드가 안 읽는다. 생성 원본은 art/out_* 에 두고 Resources에 두지 마라"
    ]


def art_trap_problems() -> list[str]:
    """`Assets/_Game/Art`는 Resources.Load가 못 읽는 함정이다(2026-08-13 실측).

    파일이 거기 있으면 화면엔 플레이스홀더만 나온다. 런타임 아트는
    `Assets/Resources/`만 쓴다. 빈 폴더를 남겨 두면 같은 함정에 다시 넣게 된다.
    """
    out: list[str] = []
    trap = ASSETS / "_Game" / "Art"
    if trap.exists():
        pngs = sorted(p.name for p in trap.rglob("*.png"))
        if pngs:
            out.append(
                f"_Game/Art 함정에 PNG {len(pngs)}장 — Resources.Load 불가. "
                f"Resources/로 옮겨라 ({', '.join(pngs[:5])}"
                + (" …" if len(pngs) > 5 else "")
                + ")"
            )
        else:
            out.append("_Game/Art 빈 함정 폴더가 남아 있다 — 지워라. 런타임 아트는 Resources/")
    stray_root = GAME / "out_p2_gem"
    if stray_root.exists() and any(stray_root.iterdir()):
        out.append("프로젝트 루트 out_p2_gem — 생성 원본은 art/out_* 아래만")
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



def parse_interp(raw: str) -> tuple[str, str]:
    """C# `$"props/{id}_0"` → (`props/`, `_0`). 첫 `{` 앞 리터럴 접두 + 마지막 `}` 뒤 접미.

    회의 20260827-081437 채택 #1: 보간 접두를 뽑아 새 PNG `Resources.Load` 가
    null 이 될 경로를 커밋 전에 차단한다. `{id}` 구멍은 glob 와일드카드다.
    """
    raw = raw.replace("\\", "/")
    first = raw.find("{")
    if first < 0:
        return raw, ""
    last = raw.rfind("}")
    prefix = raw[:first]
    suffix = raw[last + 1 :] if last >= 0 else ""
    return prefix, suffix


def _looks_like_res_prefix(prefix: str) -> bool:
    return bool(prefix) and bool(_RES_HEAD.match(prefix.replace("\\", "/")))


def _interp_specific_enough(prefix: str, suffix: str) -> bool:
    """폴더만(`sprites/` + 빈 접미)은 미사용 대조에 쓰면 전부를 삼킨다."""
    stem = prefix.replace("\\", "/").split("/")[-1]
    return bool(stem) or bool(suffix)


def interpolated_resource_patterns(
    assets: Path | None = None, extra_src: str = "",
) -> list[dict]:
    """소스에서 `$"..."` 보간 리소스 경로를 뽑는다.

    `Resources.Load($"...")` 를 우선하고, `props/`·`FX/` 로 시작하는 보간
    문자열도 포함한다(경로를 변수에 담아 나중에 Load 하는 경우).
    """
    root = assets or ASSETS
    blobs: list[str] = [extra_src] if extra_src else []
    if root.is_dir():
        for p in root.rglob("*.cs"):
            blobs.append(_src(p))
    out: list[dict] = []
    seen: set[tuple] = set()
    for src in blobs:
        for m in _INTERP_LOAD.finditer(src):
            raw = m.group("raw")
            prefix, suffix = parse_interp(raw)
            if not _looks_like_res_prefix(prefix):
                continue
            lt = (m.group("type") or "").strip()
            key = (prefix, suffix, lt, raw)
            if key in seen:
                continue
            seen.add(key)
            out.append({
                "raw": raw, "prefix": prefix, "suffix": suffix, "load_type": lt,
            })
        for m in _INTERP_RES.finditer(src):
            raw = m.group(1)
            prefix, suffix = parse_interp(raw)
            if not _looks_like_res_prefix(prefix):
                continue
            if any(
                x["prefix"] == prefix and x["suffix"] == suffix and x["raw"] == raw
                for x in out
            ):
                continue
            key = (prefix, suffix, "", raw)
            if key in seen:
                continue
            seen.add(key)
            out.append({
                "raw": raw, "prefix": prefix, "suffix": suffix, "load_type": "",
            })
    return out


def match_interp_pngs(
    prefix: str, suffix: str, res: Path | None = None,
) -> list[str]:
    """Resources 아래에서 접두·접미에 맞는 PNG 상대경로 (확장자 포함)."""
    root = res or RES
    if not root.is_dir():
        return []
    pre = prefix.replace("\\", "/").lower()
    suf = suffix.replace("\\", "/").lower()
    hits: list[str] = []
    for p in root.rglob("*.png"):
        rel = p.relative_to(root).as_posix()
        stem = rel[:-4] if rel.lower().endswith(".png") else rel
        low = stem.lower()
        if low.startswith(pre) and low.endswith(suf):
            hits.append(rel)
    return sorted(hits)


def _sprite_null_reason(png_rel: str, res: Path) -> str | None:
    """Load<Sprite> 가 null 이 될 .meta 사유. 파일 실존은 호출 쪽에서 본다."""
    meta = res / (png_rel + ".meta")
    if not meta.exists():
        return "meta 없음"
    txt = meta.read_text(encoding="utf-8", errors="replace")
    if not re.search(r"textureType:\s*8\b", txt):
        return "textureType이 Sprite 아님(Load<Sprite> null)"
    m = re.search(r"spriteMode:\s*(\d+)", txt)
    if m and m.group(1) == "2":
        return "spriteMode=Multiple(Load<Sprite> null 실측)"
    return None


def png_load_problems(
    res: Path | None = None,
    assets: Path | None = None,
    extra_src: str = "",
) -> list[str]:
    """보간 Resources.Load 가 가리키는 PNG 가 없거나 Sprite 로드가 null 이면 보고.

    파이썬은 Unity `Resources.Load` 를 못 부르니 파일 존재(+ `Load<Sprite>` 면
    .meta textureType/spriteMode)로 실로드 null 을 근사한다.
    `QA_NO_PNG_LOAD=1` 이면 커밋 가드·live 검사를 건너뛴다(네거티브 픽스처·응급).
    extra_src 네거티브는 env 와 무관하게 항상 본다.
    """
    skip_live = os.environ.get("QA_NO_PNG_LOAD") == "1" and not extra_src
    if skip_live:
        return []
    root = res or RES
    out: list[str] = []
    pats = interpolated_resource_patterns(assets, extra_src=extra_src)
    for pat in pats:
        hits = match_interp_pngs(pat["prefix"], pat["suffix"], root)
        if not hits:
            out.append(
                f"보간 로드 누락: $\"{pat['raw']}\" "
                f"(접두 {pat['prefix']} 접미 {pat['suffix']}) "
                "→ Resources.Load null"
            )
            continue
        if "Sprite" in pat["load_type"] and _interp_specific_enough(
            pat["prefix"], pat["suffix"]
        ):
            for rel in hits:
                why = _sprite_null_reason(rel, root)
                if why:
                    out.append(f"보간 Load<Sprite> null: {rel} — {why}")
    return out



# zip 실로드 (회의 20260827-081437 보류 #5, png `d30a135c` 후속)
# 런타임 zip 파이프라인은 아직 없다. C# 의 `.zip` 리터럴·보간만 뽑아
# Resources/StreamingAssets 존재 + unzip 목록 비어있지 않음을 본다.
_ZIP_LOAD = re.compile(
    r'Resources\.Load(?:<(?P<type>[^>]+)>)?\s*\(\s*(?:\$)?"(?P<raw>[^"]*)"',
    re.DOTALL | re.IGNORECASE,
)
_ZIP_QUOTED = re.compile(r'(?:\$)?"(?P<raw>[^"]+\.zip)"', re.IGNORECASE)
_ZIP_API = re.compile(
    r'(?:ZipFile|ZipArchive)\.\w+\s*\(\s*(?:\$)?"(?P<raw>[^"]*)"',
    re.IGNORECASE,
)


def _is_zip_raw(raw: str) -> bool:
    return ".zip" in raw.replace("\\", "/").lower()


def _zip_stem_affix(prefix: str, suffix: str) -> tuple[str, str]:
    """보간 접미·접두의 `.zip` 은 확장자라 stem 대조에서 뺀다."""
    pre = prefix.replace("\\", "/")
    suf = suffix.replace("\\", "/")
    if pre.lower().endswith(".zip"):
        pre = pre[:-4]
    if suf.lower().endswith(".zip"):
        suf = suf[:-4]
    return pre, suf


def _zip_roots(res: Path | None = None) -> list[Path]:
    if res is not None:
        return [res] if res.is_dir() else []
    out: list[Path] = []
    if RES.is_dir():
        out.append(RES)
    sa = ASSETS / "StreamingAssets"
    if sa.is_dir():
        out.append(sa)
    return out


def zip_resource_patterns(
    assets: Path | None = None, extra_src: str = "",
) -> list[dict]:
    """소스에서 리터럴·보간 `.zip` 경로를 뽑는다.

    런타임 zip 파이프라인이 없어도, 미래에 스테이징된 Load 경로가
    빠지면 커밋을 막기 위해 검사한다. png 실로드와 같은 parse_interp 접두.
    """
    root = assets or ASSETS
    blobs: list[str] = [extra_src] if extra_src else []
    if root.is_dir():
        for p in root.rglob("*.cs"):
            blobs.append(_src(p))
    out: list[dict] = []
    seen: set[tuple] = set()
    for src in blobs:
        found: list[tuple[str, str]] = []
        for m in _ZIP_LOAD.finditer(src):
            raw = m.group("raw")
            if _is_zip_raw(raw):
                found.append((raw, (m.group("type") or "").strip()))
        for m in _ZIP_API.finditer(src):
            raw = m.group("raw")
            if _is_zip_raw(raw):
                found.append((raw, ""))
        for m in _ZIP_QUOTED.finditer(src):
            found.append((m.group("raw"), ""))
        for raw, lt in found:
            prefix, suffix = parse_interp(raw)
            key = (prefix, suffix, raw)
            if key in seen:
                continue
            seen.add(key)
            out.append({
                "raw": raw, "prefix": prefix, "suffix": suffix, "load_type": lt,
            })
    return out


def match_interp_zips(
    prefix: str, suffix: str, res: Path | None = None,
) -> list[str]:
    """검색 루트 아래 접두·접미에 맞는 zip 상대경로."""
    pre, suf = _zip_stem_affix(prefix, suffix)
    pre = pre.lower()
    suf = suf.lower()
    hits: list[str] = []
    for root in _zip_roots(res):
        for p in root.rglob("*.zip"):
            rel = p.relative_to(root).as_posix()
            stem = rel[:-4] if rel.lower().endswith(".zip") else rel
            low = stem.lower()
            if low.startswith(pre) and low.endswith(suf):
                hits.append(rel)
    return sorted(hits)


def _zip_null_reason(zip_rel: str, root: Path) -> str | None:
    path = root / zip_rel
    if not path.is_file():
        return "파일 없음"
    try:
        with zipfile.ZipFile(path) as z:
            names = [n for n in z.namelist() if n and not n.endswith("/")]
    except zipfile.BadZipFile:
        return "깨진 zip(unzip null)"
    except OSError as e:
        return f"unzip 실패({e})"
    if not names:
        return "unzip 목록 비어 있음"
    return None


def zip_load_problems(
    res: Path | None = None,
    assets: Path | None = None,
    extra_src: str = "",
) -> list[str]:
    """C# zip 경로가 없거나 unzip 목록이 비면 보고.

    파이썬은 Unity `Resources.Load` 를 못 부르니 파일 존재 + zipfile namelist
    로 실로드/unzip null 을 근사한다.
    `QA_NO_ZIP_LOAD=1` 이면 커밋 가드·live 검사를 건너뛴다(네거티브 픽스처·응급).
    extra_src 네거티브는 env 와 무관하게 항상 본다.
    """
    skip_live = os.environ.get("QA_NO_ZIP_LOAD") == "1" and not extra_src
    if skip_live:
        return []
    roots = _zip_roots(res)
    out: list[str] = []
    pats = zip_resource_patterns(assets, extra_src=extra_src)
    for pat in pats:
        hits: list[tuple[str, Path]] = []
        for root in roots:
            for rel in match_interp_zips(pat["prefix"], pat["suffix"], root):
                hits.append((rel, root))
        if not hits:
            out.append(
                f"zip 로드 누락: $\"{pat['raw']}\" "
                f"(접두 {pat['prefix']} 접미 {pat['suffix']}) "
                "→ Resources.Load/unzip null"
            )
            continue
        for rel, root in hits:
            why = _zip_null_reason(rel, root)
            if why:
                out.append(f"zip unzip null: {rel} — {why}")
    seen_rel: set[str] = set()
    for root in roots:
        for p in root.rglob("*.zip"):
            rel = p.relative_to(root).as_posix()
            if rel in seen_rel:
                continue
            seen_rel.add(rel)
            why = _zip_null_reason(rel, root)
            if why:
                msg = f"zip unzip null: {rel} — {why}"
                if msg not in out:
                    out.append(msg)
    return out


def zip_self_test() -> tuple[bool, str]:
    """접두 추출 + temp 실파일 + 네거티브(없는 zip) + 빈 unzip + live."""
    import tempfile as _tf

    lines: list[str] = []
    ok = True

    cases = [
        ("packs/{id}.zip", "packs/", ".zip"),
        ("FX/pack_{i}.zip", "FX/pack_", ".zip"),
        ("data.zip", "data.zip", ""),
    ]
    for raw, wp, ws in cases:
        p, s = parse_interp(raw)
        if (p, s) != (wp, ws):
            ok = False
            lines.append(
                f"FAIL extract $\"{raw}\" → prefix={p!r} suffix={s!r} "
                f"(want {wp!r},{ws!r})"
            )
        else:
            lines.append(f"ok   extract $\"{raw}\" → prefix={p!r} suffix={s!r}")

    with _tf.TemporaryDirectory() as tmp:
        root = Path(tmp)
        (root / "packs").mkdir()
        good = root / "packs" / "data_0.zip"
        with zipfile.ZipFile(good, "w") as z:
            z.writestr("ok.txt", "x")
        hits = match_interp_zips("packs/data_", "", root)
        if hits != ["packs/data_0.zip"]:
            ok = False
            lines.append(f"FAIL temp prefix glob hits={hits}")
        else:
            lines.append(f"ok   temp packs/data_ → {hits}")
        empty = root / "packs" / "qa_empty.zip"
        with zipfile.ZipFile(empty, "w"):
            pass
        extra_empty = 'Resources.Load<TextAsset>("packs/qa_empty.zip");\n'
        msgs_e = zip_load_problems(res=root, extra_src=extra_empty, assets=root)
        blob_e = " ".join(msgs_e)
        if "qa_empty" not in blob_e or "비어" not in blob_e:
            ok = False
            lines.append(f"FAIL 빈 unzip 미탐지: {msgs_e}")
        else:
            lines.append("ok   네거티브 빈 unzip qa_empty 탐지")

    extra = 'Resources.Load<TextAsset>($"packs/qa_missing_zip_{i}.zip");\n'
    msgs = zip_load_problems(extra_src=extra)
    blob = " ".join(msgs)
    if "qa_missing_zip" not in blob:
        ok = False
        lines.append(f"FAIL 네거티브 qa_missing_zip 미탐지: {msgs}")
    else:
        lines.append(
            'ok   네거티브 $"packs/qa_missing_zip_{i}.zip" 탐지 (파일 없음 → null)'
        )

    live = zip_load_problems()
    if live:
        ok = False
        lines.append("FAIL live zip 실로드:\n  " + "\n  ".join(live))
    else:
        n = len(zip_resource_patterns())
        lines.append(
            f"ok   live zip Resources.Load/unzip {n}건 전부 존재·비어있지 않음"
        )

    return ok, "\n".join(lines)


def self_test() -> tuple[bool, str]:
    """접두 추출 + live 실파일 + 네거티브(없는 보간 로드가 잡히는지)."""
    lines: list[str] = []
    ok = True

    cases = [
        ("FX/fx_dash_trail_{i}", "FX/fx_dash_trail_", ""),
        ("props/{id}_0", "props/", "_0"),
        ("sprites/{d}/{d}_idle_00", "sprites/", "_idle_00"),
        (
            "sprites/{JOB_DIRS[j]}/{JOB_DIRS[j]}_{JOB_FRAMES[0]}",
            "sprites/",
            "",
        ),
    ]
    for raw, wp, ws in cases:
        p, s = parse_interp(raw)
        if (p, s) != (wp, ws):
            ok = False
            lines.append(
                f"FAIL extract $\"{raw}\" → prefix={p!r} suffix={s!r} "
                f"(want {wp!r},{ws!r})"
            )
        else:
            lines.append(f"ok   extract $\"{raw}\" → prefix={p!r} suffix={s!r}")

    hits = match_interp_pngs("FX/fx_dash_trail_", "")
    names = {Path(h).name for h in hits}
    need = {"fx_dash_trail_0.png", "fx_dash_trail_1.png", "fx_dash_trail_2.png"}
    if not need <= names:
        ok = False
        lines.append(f"FAIL live dash_trail hits={sorted(names)}")
    else:
        lines.append(
            f"ok   live FX/fx_dash_trail_ → {len(hits)} files {sorted(names)}"
        )

    extra = 'Resources.Load<Sprite>($"FX/qa_missing_interp_{i}");\n'
    msgs = png_load_problems(extra_src=extra)
    blob = " ".join(msgs)
    if "qa_missing_interp" not in blob:
        ok = False
        lines.append(f"FAIL 네거티브 qa_missing_interp 미탐지: {msgs}")
    else:
        lines.append(
            'ok   네거티브 $"FX/qa_missing_interp_{i}" 탐지 (파일 없음 → null)'
        )

    live = png_load_problems()
    if live:
        ok = False
        lines.append("FAIL live png 실로드:\n  " + "\n  ".join(live))
    else:
        n = len(interpolated_resource_patterns())
        lines.append(f"ok   live 보간 Resources.Load {n}건 전부 파일 존재")

    return ok, "\n".join(lines)


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--strict", action="store_true")
    ap.add_argument(
        "--png-load", action="store_true",
        help="보간 접두 png 실로드만 검사 (커밋 가드용)",
    )
    ap.add_argument(
        "--self-test", action="store_true",
        help="접두 추출 + 네거티브(없는 보간 로드) + live 실로드",
    )
    ap.add_argument(
        "--zip-load", action="store_true",
        help="리터럴·보간 zip 실로드/unzip만 검사 (커밋 가드용)",
    )
    ap.add_argument(
        "--zip-self-test", action="store_true",
        help="zip 접두 추출 + 네거티브(없는 zip·빈 unzip) + live",
    )
    args = ap.parse_args()

    if args.self_test:
        ok, note = self_test()
        print(note)
        if ok:
            print("판정: PASS — 보간 접두 추출 + png 실로드 (네거티브 qa_missing_interp 탐지)")
            return
        print("판정: FAIL — 보간 접두 png 실로드 검사가 네거티브/live 를 통과하지 못했다")
        sys.exit(1)

    if args.png_load:
        probs = png_load_problems()
        if not probs:
            n = len(interpolated_resource_patterns())
            print(f"✅ png 실로드 이상 없음 (보간 {n}건)")
            return
        for p in probs:
            print(f"⚠️ {p}")
        sys.exit(1)

    if args.zip_self_test:
        ok, note = zip_self_test()
        print(note)
        if ok:
            print("판정: PASS — zip 접두 추출 + 실로드/unzip (네거티브 qa_missing_zip 탐지)")
            return
        print("판정: FAIL — zip 실로드 검사가 네거티브/live 를 통과하지 못했다")
        sys.exit(1)

    if args.zip_load:
        probs = zip_load_problems()
        if not probs:
            n = len(zip_resource_patterns())
            print(f"✅ zip 실로드 이상 없음 (경로 {n}건)")
            return
        for p in probs:
            print(f"⚠️ {p}")
        sys.exit(1)

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
    problems += art_trap_problems()
    problems += unused_resource_problems()
    problems += png_load_problems()
    problems += zip_load_problems()

    if not problems:
        print("✅ 네이밍·반영 이상 없음")
        return
    for p in problems:
        print(f"⚠️ {p}")
    if args.strict:
        sys.exit(1)


if __name__ == "__main__":
    main()
