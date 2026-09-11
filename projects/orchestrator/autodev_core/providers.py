"""Provider 독립성 — 특정 AI 업체를 시스템의 필수 의존성으로 만들지 않는다.

세 가지를 지킨다.
  ① **설치 ≠ 사용 가능.** `which codex`가 성공해도 로그아웃·구독 만료면 못 쓴다.
     그래서 각 Provider는 **실제 인증 상태를 묻는 명령**으로 검사한다.
  ② **한 Provider가 죽어도 오케스트레이터는 안 죽는다.** 상태를 기록하고 다른 곳으로 넘긴다.
  ③ **배정은 모델 이름이 아니라 능력(capability)으로 한다.** 이름을 박아두면 그 업체가
     막히는 날 시스템이 같이 막힌다.

상태는 일곱이다. 사용 가능/불가를 2진으로 접지 않는 이유는, 「지금 안 되는 것」과
「영영 안 되는 것」과 「조금 있다 되는 것」이 서로 다른 처방이기 때문이다.
"""

from __future__ import annotations

import json
import os
import re
import shutil
import subprocess
import time
from dataclasses import dataclass, field
from pathlib import Path

from .config import STATE_DIR

STORE = STATE_DIR / "providers.json"
TTL_SEC = 600.0            # 검사 결과를 이만큼 믿는다(매번 물으면 시작이 느려진다)
RATE_COOLDOWN = 900.0      # 사용량 제한을 만나면 이 시간 동안 건너뛴다

AVAILABLE = "AVAILABLE"
UNAVAILABLE = "UNAVAILABLE"          # CLI 자체가 없다
AUTH_REQUIRED = "AUTH_REQUIRED"      # 설치는 됐으나 로그인/인증이 없다
LIMITED = "LIMITED"                  # 쓸 수는 있으나 제한적(모델 없음·저사양 등)
RATE_LIMITED = "RATE_LIMITED"        # 사용량 한도 — 시간이 지나면 풀린다
ERROR = "ERROR"                      # 검사 자체가 실패(네트워크·타임아웃)
DISABLED = "DISABLED"                # 사람이 꺼둠

# 지금 일을 맡길 수 있는 상태. LIMITED는 「되긴 된다」라 포함한다.
USABLE = (AVAILABLE, LIMITED)

# 능력. 작업은 이 이름으로 요구하고, Provider는 이 이름으로 자기를 소개한다.
PLANNING, CODING, DEBUGGING, REVIEW = "PLANNING", "CODING", "DEBUGGING", "REVIEW"
RESEARCH, LONG_CONTEXT, LOCAL, LOW_COST = "RESEARCH", "LONG_CONTEXT", "LOCAL", "LOW_COST"
ALL_CAPS = (PLANNING, CODING, DEBUGGING, REVIEW, RESEARCH, LONG_CONTEXT, LOCAL, LOW_COST)

# type별 기본 능력. config.json에 `capabilities`를 적으면 그것이 이긴다.
DEFAULT_CAPS = {
    "codex":  [CODING, DEBUGGING, PLANNING, REVIEW],
    "claude": [CODING, DEBUGGING, LONG_CONTEXT, REVIEW, PLANNING],
    "grok":   [RESEARCH, CODING],
    "ollama": [LOCAL, LOW_COST, RESEARCH],
    "script": [CODING, PLANNING, REVIEW, RESEARCH, DEBUGGING, LOCAL, LOW_COST],
}

# 클라우드가 전부 막혔을 때만 가는 길. 로컬 모델은 대신할 수 있는 일이 제한적이다.
LOCAL_TYPES = ("ollama",)


@dataclass
class Status:
    name: str
    state: str
    detail: str = ""
    checked_at: float = 0.0
    cooldown_until: float = 0.0
    caps: list[str] = field(default_factory=list)

    @property
    def usable(self) -> bool:
        if self.cooldown_until and time.time() < self.cooldown_until:
            return False
        return self.state in USABLE

    def line(self) -> str:
        s = f"{self.name:9s} {self.state:13s} {self.detail}"
        if self.cooldown_until and time.time() < self.cooldown_until:
            s += f" (해제까지 {int(self.cooldown_until - time.time())}s)"
        return s


# --- 저장소 -------------------------------------------------------------

def _load() -> dict:
    if not STORE.is_file():
        return {}
    try:
        return json.loads(STORE.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {}


def _save(d: dict) -> None:
    STORE.parent.mkdir(parents=True, exist_ok=True)
    try:
        STORE.write_text(json.dumps(d, ensure_ascii=False, indent=2), encoding="utf-8")
    except OSError:
        pass


def mark(name: str, state: str, detail: str = "", cooldown_sec: float = 0.0) -> Status:
    """실행 중에 알게 된 사실을 기록한다(예: 429를 맞았다). 다음 배정이 이것을 본다."""
    d = _load()
    row = d.get(name, {})
    row.update({"state": state, "detail": detail, "checked_at": time.time(),
                "cooldown_until": time.time() + cooldown_sec if cooldown_sec else 0.0})
    d[name] = row
    _save(d)
    return Status(name=name, state=state, detail=detail, checked_at=row["checked_at"],
                  cooldown_until=row["cooldown_until"], caps=row.get("caps", []))


# --- 실제 검사 ----------------------------------------------------------

def _sh(argv: list[str], timeout: int = 30) -> tuple[int, str]:
    try:
        p = subprocess.run(argv, capture_output=True, text=True, timeout=timeout)
        return p.returncode, (p.stdout or "") + (p.stderr or "")
    except subprocess.TimeoutExpired:
        return -9, "__TIMEOUT__"
    except OSError as e:
        return -1, f"__OSERROR__ {e}"


_AUTH_NO = re.compile(r"(not logged in|logged out|please (log|sign) ?in|no credentials|"
                      r"unauthori[sz]ed|authentication (required|failed)|401)", re.I)
_AUTH_YES = re.compile(r"(logged in|authenticated|\"loggedIn\"\s*:\s*true)", re.I)

# 실행 중 출력에서 읽어내는 신호. 「무슨 실패인지」가 처방을 가른다.
_RATE = re.compile(r"(rate[ _-]?limit|429|too many requests|usage limit|quota exceeded|"
                   r"limit reached|사용량 한도|한도에 도달)", re.I)
_AUTH_ERR = re.compile(r"(401|unauthori[sz]ed|authentication (failed|required)|"
                       r"invalid api key|expired token|로그인이 필요)", re.I)
_BILLING = re.compile(r"(insufficient (credit|quota|funds)|billing|subscription (expired|required)|"
                      r"payment required|402|구독)", re.I)


def classify_failure(text: str) -> tuple[str, float] | None:
    """에이전트 출력에서 Provider 쪽 장애를 읽어낸다. (상태, 냉각초) 또는 None.

    **인프라 실패와 코드 실패를 섞지 않는다**(옛 교훈). 여기서 잡히면 그 시도는
    「이 목표가 어렵다」가 아니라 「이 Provider가 지금 안 된다」이다.
    """
    t = text or ""
    if _RATE.search(t):
        return RATE_LIMITED, RATE_COOLDOWN
    if _AUTH_ERR.search(t):
        return AUTH_REQUIRED, 0.0
    if _BILLING.search(t):
        return LIMITED, RATE_COOLDOWN
    return None


def _probe_cli(bin_: str, argv: list[str], timeout: int = 30) -> tuple[str, str]:
    rc, out = _sh(argv, timeout)
    head = " ".join(out.split())[:120]
    if out == "__TIMEOUT__":
        return ERROR, "인증 확인이 시간 초과"
    if out.startswith("__OSERROR__"):
        return UNAVAILABLE, out
    hit = classify_failure(out)
    if hit:
        return hit[0], head
    if _AUTH_NO.search(out):
        return AUTH_REQUIRED, head or "로그인 필요"
    if rc == 0 and _AUTH_YES.search(out):
        return AVAILABLE, head
    if rc != 0:
        return ERROR, head or f"rc={rc}"
    # rc=0인데 로그인 여부를 말해주지 않았다 — 모른다. 초록으로 세지 않는다.
    return LIMITED, head or "인증 상태를 확인하지 못했다(설치는 확인)"


def probe(name: str, agent_cfg, *, ollama_model: str | None = None) -> Status:
    """**실제로 물어본다.** 설치 여부만으로 AVAILABLE이라고 하지 않는다."""
    caps = list(getattr(agent_cfg, "capabilities", None) or DEFAULT_CAPS.get(agent_cfg.type, []))
    now = time.time()

    def st(state, detail, cooldown=0.0):
        return Status(name=name, state=state, detail=detail, checked_at=now,
                      cooldown_until=(now + cooldown) if cooldown else 0.0, caps=caps)

    if os.getenv(f"AUTODEV_DISABLE_{name.upper()}"):
        return st(DISABLED, "AUTODEV_DISABLE_* 환경변수로 꺼둠")
    if getattr(agent_cfg, "enabled", True) is False:
        return st(DISABLED, "config에서 꺼둠")

    typ = agent_cfg.type
    if typ == "script":
        return st(AVAILABLE, "시험용 script 에이전트")

    if typ == "ollama":
        from .agents import ollama as oll
        if not oll.available():
            return st(UNAVAILABLE, "ollama 서버에 연결하지 못했다")
        model = ollama_model or agent_cfg.model
        names = oll.list_models()
        if model and not any(model.split(":")[0] in m for m in names):
            return st(LIMITED, f"서버는 살아있지만 모델 {model} 미설치")
        return st(AVAILABLE, f"모델 {len(names)}개")

    if not shutil.which(agent_cfg.bin):
        return st(UNAVAILABLE, f"CLI 없음: {agent_cfg.bin}")

    probes = {
        "codex":  [agent_cfg.bin, "login", "status"],
        "claude": [agent_cfg.bin, "auth", "status"],
        "grok":   [agent_cfg.bin, "models"],
    }
    argv = probes.get(typ)
    if not argv:
        # 검사 방법을 모르는 type — 설치만 확인됐다는 뜻으로 LIMITED.
        return st(LIMITED, "인증 검사 방법이 없다(설치만 확인)")
    state, detail = _probe_cli(agent_cfg.bin, argv)
    return st(state, detail)


def probe_all(cfg, *, force: bool = False, names: list[str] | None = None) -> dict[str, Status]:
    """설정에 있는 Provider 전부를 본다. 캐시가 신선하면 다시 묻지 않는다."""
    store = _load()
    out: dict[str, Status] = {}
    for name in (names or list(cfg.raw.get("agents", {}))):
        acfg = cfg.agent(name)
        # 환경·설정으로 꺼둔 것과 시험용 script는 **캐시에 남기지 않는다**.
        # 이것들은 「지금 이 실행의 사정」이지 Provider의 상태가 아니다 —
        # 캐시에 남기면 다음 실행이 남의 환경변수 결정을 물려받는다(실제로 그렇게 새어 나갔다).
        if os.getenv(f"AUTODEV_DISABLE_{name.upper()}") or acfg.enabled is False:
            out[name] = probe(name, acfg)
            store.pop(name, None)
            continue
        if acfg.type == "script":
            out[name] = probe(name, acfg)
            continue
        row = store.get(name)
        fresh = row and (time.time() - row.get("checked_at", 0)) < TTL_SEC
        cooling = row and row.get("cooldown_until", 0) > time.time()
        if row and (fresh or cooling) and not force:
            out[name] = Status(name=name, state=row["state"], detail=row.get("detail", ""),
                               checked_at=row.get("checked_at", 0),
                               cooldown_until=row.get("cooldown_until", 0),
                               caps=row.get("caps", []))
            continue
        try:
            s = probe(name, acfg, ollama_model=cfg.log_summarizer.get("model"))
        except Exception as e:                      # 검사가 터져도 전체가 죽으면 안 된다(§독립성)
            s = Status(name=name, state=ERROR, detail=f"검사 중 예외: {e}", checked_at=time.time())
        out[name] = s
        store[name] = {"state": s.state, "detail": s.detail, "checked_at": s.checked_at,
                       "cooldown_until": s.cooldown_until, "caps": s.caps}
    _save(store)
    return out


# --- 배정 ---------------------------------------------------------------

def caps_of(cfg, name: str) -> list[str]:
    a = cfg.raw.get("agents", {}).get(name, {})
    return list(a.get("capabilities") or DEFAULT_CAPS.get(a.get("type", name), []))


def is_local(cfg, name: str) -> bool:
    return cfg.raw.get("agents", {}).get(name, {}).get("type") in LOCAL_TYPES


def pick(cfg, statuses: dict[str, Status], *, capability: str,
         prefer: list[str] | None = None, exclude: tuple[str, ...] = ()) -> list[str]:
    """능력을 가진 **쓸 수 있는** Provider를 선호 순서대로. 이름이 아니라 능력으로 고른다.

    `prefer`를 주면 **그 목록 안에서만** 고른다. 여기서 설정에 있는 모든 에이전트로
    번지게 두었더니 리뷰어가 구현자로 뽑혀 나갔다(2026-09-11) — 후보 명단을 만드는 일은
    부르는 쪽의 책임이고, 이 함수는 명단 밖으로 나가지 않는다.
    """
    order = list(prefer) if prefer else list(cfg.raw.get("agents", {}))
    out = []
    for n in order:
        if n in exclude or n in out:
            continue
        s = statuses.get(n)
        if not s or not s.usable:
            continue
        if capability not in caps_of(cfg, n):
            continue
        out.append(n)
    return out


def cloud_all_down(cfg, statuses: dict[str, Status]) -> bool:
    """클라우드가 전부 막혔는가. 로컬 모드로 내려갈지를 이걸로 정한다."""
    cloud = [n for n in cfg.raw.get("agents", {})
             if not is_local(cfg, n) and cfg.raw["agents"][n].get("type") != "script"]
    if not cloud:
        return False
    return all(not (statuses.get(n) and statuses[n].usable) for n in cloud)
