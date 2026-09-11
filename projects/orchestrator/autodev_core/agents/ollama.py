"""Ollama(Gemma) — 개발 에이전트가 아니라 **보조**다.

로컬 모델은 파일을 고치지 않는다. 여기서 맡는 것은 긴 로그 압축·요약뿐이고, 그 결과는
클라우드 에이전트에 넘길 Context를 줄이는 데 쓴다. 개발 사다리에 올리지 않는 이유는
품질을 희생하면서까지 로컬을 강제하지 않는다는 원칙(§15) 때문이다.

16GB 기계라 모델을 상주시키지 않는다 — 부를 때 올리고, 끝나면 내린다(`unload`).
"""

from __future__ import annotations

import json
import urllib.error
import urllib.request

HOST = "http://127.0.0.1:11434"


class OllamaError(RuntimeError):
    pass


def _post(path: str, payload: dict, timeout: int) -> dict:
    req = urllib.request.Request(
        HOST + path, data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.loads(r.read().decode("utf-8", "replace"))


def available(timeout: int = 3) -> bool:
    try:
        urllib.request.urlopen(HOST + "/api/tags", timeout=timeout).read()
        return True
    except Exception:
        return False


def loaded_models(timeout: int = 3) -> list[str]:
    try:
        with urllib.request.urlopen(HOST + "/api/ps", timeout=timeout) as r:
            data = json.loads(r.read().decode("utf-8", "replace"))
        return [m.get("name", "") for m in data.get("models", [])]
    except Exception:
        return []


def unload(model: str, timeout: int = 10) -> bool:
    """모델을 즉시 내린다(keep_alive=0). Unity 배치·메모리 압박 전에 부른다."""
    try:
        _post("/api/generate", {"model": model, "prompt": "", "keep_alive": 0}, timeout)
        return True
    except Exception:
        return False


def summarize(text: str, model: str, *, instruction: str, timeout: int = 180,
              keep_alive: str = "60s", max_chars: int = 24000) -> str | None:
    """긴 텍스트를 줄인다. 실패하면 **None을 돌려준다** — 요약 실패를 빈 요약으로 둔갑시키지 않는다."""
    if not text.strip():
        return None
    body = text[-max_chars:]
    prompt = f"{instruction}\n\n----\n{body}\n----\n"
    # think=False가 없으면 gemma4 같은 사고형 모델은 예산을 전부 내부 사고에 쓰고
    # **본문을 0자로 돌려준다**(실측: done_reason=length, response=""). 요약이 조용히
    # 사라지는 자리라 여기서 못을 박는다.
    try:
        out = _post("/api/generate", {
            "model": model, "prompt": prompt, "stream": False,
            "keep_alive": keep_alive, "think": False,
            "options": {"temperature": 0.1, "num_predict": 800},
        }, timeout)
    except (urllib.error.URLError, TimeoutError, OSError, json.JSONDecodeError):
        return None
    res = (out or {}).get("response", "").strip()
    if not res:
        return None  # 빈 응답을 요약으로 내밀지 않는다
    return res
