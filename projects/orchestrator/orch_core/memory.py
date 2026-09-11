"""메모리 관리자 — 16GB 통합 메모리에서 자율 루프를 오래 돌리기 위한 것.

**RAM 퍼센트 하나로 판단하지 않는다**(§11). macOS는 압축·스왑으로 부족을 숨기기 때문에
"여유 몇 %"만 보면 이미 늦는다. 여기서는 셋을 같이 본다:
  ① 시스템 여유 비율(memory_pressure) ② 스왑 사용량과 **증가 속도** ③ 지금 무엇이 돌고 있는가

판정은 순수 함수(`assess`)로 분리했다 — 진짜 압박을 만들지 않고도 규칙을 시험할 수 있어야 한다.
"""

from __future__ import annotations

import json
import re
import subprocess
import time
from dataclasses import dataclass, field
from pathlib import Path

from .config import STATE_DIR

SAMPLES = STATE_DIR / "mem_samples.json"
MAX_SAMPLES = 60

# 임계값은 한 곳에만 둔다. 바꾸려면 여기만 고친다.
FREE_PCT_RED = 10.0
FREE_PCT_YELLOW = 25.0
SWAP_RATIO_RED = 0.90
SWAP_RATIO_YELLOW = 0.60
SWAP_RATE_RED = 200.0     # MB/분
SWAP_RATE_YELLOW = 50.0


@dataclass
class Snapshot:
    ts: float
    free_pct: float | None
    swap_used_mb: float
    swap_total_mb: float
    compressed_mb: float
    wired_mb: float
    procs: dict = field(default_factory=dict)   # {이름: RSS MB}
    swap_rate_mb_min: float | None = None

    @property
    def swap_ratio(self) -> float:
        return (self.swap_used_mb / self.swap_total_mb) if self.swap_total_mb else 0.0

    def line(self) -> str:
        rate = f"{self.swap_rate_mb_min:+.0f}MB/분" if self.swap_rate_mb_min is not None else "추세 없음"
        free = f"{self.free_pct:.0f}%" if self.free_pct is not None else "?"
        return (f"여유 {free} · 스왑 {self.swap_used_mb:.0f}/{self.swap_total_mb:.0f}MB"
                f"({self.swap_ratio * 100:.0f}%, {rate}) · 압축 {self.compressed_mb:.0f}MB")


@dataclass
class Assessment:
    state: str              # GREEN / YELLOW / RED
    reasons: list[str] = field(default_factory=list)

    @property
    def ok(self) -> bool:
        return self.state == "GREEN"

    def summary(self) -> str:
        return self.state + (" — " + ", ".join(self.reasons) if self.reasons else "")


def _sh(cmd: list[str], timeout: int = 10) -> str:
    try:
        p = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout)
        return p.stdout
    except (OSError, subprocess.SubprocessError):
        return ""


def _free_pct() -> float | None:
    out = _sh(["memory_pressure"], timeout=15)
    m = re.search(r"System-wide memory free percentage:\s*(\d+)%", out)
    return float(m.group(1)) if m else None


def _swap() -> tuple[float, float]:
    out = _sh(["sysctl", "vm.swapusage"])
    tot = re.search(r"total\s*=\s*([\d.]+)M", out)
    used = re.search(r"used\s*=\s*([\d.]+)M", out)
    return (float(used.group(1)) if used else 0.0, float(tot.group(1)) if tot else 0.0)


def _vm_stat() -> tuple[float, float]:
    out = _sh(["vm_stat"])
    page = re.search(r"page size of (\d+) bytes", out)
    psz = int(page.group(1)) if page else 4096
    def pages(label):
        m = re.search(rf"{label}:\s+(\d+)", out)
        return int(m.group(1)) * psz / (1024 ** 2) if m else 0.0
    return pages("Pages occupied by compressor"), pages("Pages wired down")


def _procs(patterns=("Unity", "ollama", "codex", "claude", "grok")) -> dict:
    out = _sh(["ps", "-Ao", "rss,comm", "-r"])
    found: dict[str, float] = {}
    for ln in out.splitlines()[1:]:
        parts = ln.strip().split(None, 1)
        if len(parts) != 2:
            continue
        rss_kb, comm = parts
        if not rss_kb.isdigit():
            continue
        for pat in patterns:
            if pat.lower() in comm.lower():
                found[pat] = found.get(pat, 0.0) + int(rss_kb) / 1024
                break
    return found


def _load_samples() -> list[dict]:
    if not SAMPLES.is_file():
        return []
    try:
        return json.loads(SAMPLES.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return []


def _save_samples(rows: list[dict]) -> None:
    SAMPLES.parent.mkdir(parents=True, exist_ok=True)
    try:
        SAMPLES.write_text(json.dumps(rows[-MAX_SAMPLES:]), encoding="utf-8")
    except OSError:
        pass


def sample(record: bool = True) -> Snapshot:
    """지금 상태를 잰다. 추세(스왑 증가 속도)는 이전 표본과 비교해 낸다."""
    comp, wired = _vm_stat()
    used, total = _swap()
    snap = Snapshot(ts=time.time(), free_pct=_free_pct(), swap_used_mb=used,
                    swap_total_mb=total, compressed_mb=comp, wired_mb=wired, procs=_procs())

    rows = _load_samples()
    if rows:
        prev = rows[-1]
        dt_min = (snap.ts - prev.get("ts", snap.ts)) / 60.0
        # 표본이 너무 오래됐으면 추세로 쓰지 않는다 — 옛 값으로 지금을 판정하지 않는다.
        if 0.05 <= dt_min <= 30.0:
            snap.swap_rate_mb_min = (snap.swap_used_mb - prev.get("swap_used_mb", 0.0)) / dt_min
    if record:
        rows.append({"ts": snap.ts, "swap_used_mb": snap.swap_used_mb, "free_pct": snap.free_pct})
        _save_samples(rows)
    return snap


def assess(snap: Snapshot) -> Assessment:
    """순수 판정. 실제 압박을 만들지 않고도 규칙을 시험할 수 있게 입력만 본다."""
    reasons: list[str] = []
    state = "GREEN"

    def raise_to(level: str, why: str):
        nonlocal state
        order = {"GREEN": 0, "YELLOW": 1, "RED": 2}
        if order[level] > order[state]:
            state = level
        reasons.append(why)

    if snap.free_pct is not None:
        if snap.free_pct <= FREE_PCT_RED:
            raise_to("RED", f"시스템 여유 {snap.free_pct:.0f}%")
        elif snap.free_pct <= FREE_PCT_YELLOW:
            raise_to("YELLOW", f"시스템 여유 {snap.free_pct:.0f}%")
    else:
        # 못 쟀으면 초록이라고 하지 않는다 — 모르는 것은 모른다고 표시하고 한 단계 조심한다.
        raise_to("YELLOW", "여유 비율을 재지 못했다")

    # 스왑 **사용률만으로는 RED를 만들지 않는다.** macOS의 스왑 사용량은 "지금 부족하다"가
    # 아니라 "예전에 한 번이라도 밀려났다"의 누적이라, 회수되지 않은 채 오래 남는다.
    # 2026-09-11 실측: 여유 71% · 증가 0MB/분인데 스왑 91%라는 이유로 RED가 떠서
    # 매 작업이 300초씩 기다리다 실패했다(NC 스위트 전멸). 오탐이었다.
    # 지금 밀려나는 중인지는 **증가 속도**가 말해준다 — 높은 비율 + 증가 중일 때만 RED다.
    if snap.swap_total_mb:
        growing = (snap.swap_rate_mb_min or 0.0) >= SWAP_RATE_YELLOW
        if snap.swap_ratio >= SWAP_RATIO_RED and growing:
            raise_to("RED", f"스왑 {snap.swap_ratio * 100:.0f}%인데 계속 늘고 있다")
        elif snap.swap_ratio >= SWAP_RATIO_YELLOW:
            raise_to("YELLOW", f"스왑 {snap.swap_ratio * 100:.0f}% 사용"
                               + ("(정체)" if not growing else ""))

    if snap.swap_rate_mb_min is not None:
        if snap.swap_rate_mb_min >= SWAP_RATE_RED:
            raise_to("RED", f"스왑이 {snap.swap_rate_mb_min:.0f}MB/분으로 늘고 있다")
        elif snap.swap_rate_mb_min >= SWAP_RATE_YELLOW:
            raise_to("YELLOW", f"스왑이 {snap.swap_rate_mb_min:.0f}MB/분으로 늘고 있다")

    return Assessment(state, reasons)


def relieve(state: str, summarizer_model: str | None = None) -> list[str]:
    """압박을 실제로 덜어낸다. 지금 할 수 있는 것은 로컬 모델을 내리는 것뿐이다 —
    Unity와 클라우드 CLI는 남의 일을 하는 중이라 함부로 죽이지 않는다."""
    notes = []
    if state not in ("YELLOW", "RED"):
        return notes
    from .agents import ollama

    if not ollama.available():
        return notes
    loaded = ollama.loaded_models()
    for m in loaded:
        if ollama.unload(m):
            notes.append(f"ollama 모델 내림: {m}")
    if summarizer_model and summarizer_model not in loaded:
        ollama.unload(summarizer_model)
    return notes


def effective_unity_slots(state: str, configured: int) -> int:
    """압박 상태에서는 Unity를 여러 개 띄우지 않는다."""
    if state == "RED":
        return 1
    if state == "YELLOW":
        return min(configured, 1)
    return configured


def wait_for_room(max_wait: int = 300, poll: int = 15) -> tuple[str, Snapshot, list[str]]:
    """RED면 잠시 기다린다. 무한정 기다리지 않는다 — 안 풀리면 사실대로 세운다."""
    notes: list[str] = []
    deadline = time.time() + max_wait
    snap = sample()
    a = assess(snap)
    while a.state == "RED" and time.time() < deadline:
        notes += relieve(a.state)
        time.sleep(poll)
        snap = sample()
        a = assess(snap)
    return a.state, snap, notes
