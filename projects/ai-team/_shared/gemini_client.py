"""
gemini_client.py — 공통 Gemini API 클라이언트

Vision(이미지→텍스트) / 웹서치(Google Search Grounding) / 이미지 생성(Imagen 3) 전용.
텍스트 생성은 Ollama만 사용 — Gemini 텍스트 폴백 제거됨.
"""
import os
import json
import base64
import urllib.request

_BASE = "https://generativelanguage.googleapis.com/v1beta/models"
_IMAGEN_BASE = "https://generativelanguage.googleapis.com/v1beta"


def _api_key() -> str:
    return os.getenv("GEMINI_API_KEY", "")


# ── 텍스트 생성 (Ollama 전용 래퍼) ───────────────────────────────────────────

def text(
    prompt: str,
    system: str = "",
    max_tokens: int = 2000,
    temperature: float = 0.7,
    json_mode: bool = False,
    task: str = "",
    lm_first: bool = True,  # 하위 호환성 유지 (무시됨)
) -> str | None:
    """Ollama 텍스트 생성, 실패 시 Gemini API 폴백."""
    try:
        from _shared.ollama_client import chat as lm_chat, is_available as lm_available
        if lm_available():
            res = lm_chat(
                prompt, system=system,
                max_tokens=max_tokens, temperature=temperature,
                json_mode=json_mode, task=task,
            )
            if res:
                return res.strip()
    except Exception:
        pass

    # Gemini API 폴백
    api_key = _api_key()
    if not api_key:
        return None
    try:
        parts = []
        if system:
            parts.append({"text": f"{system}\n\n{prompt}"})
        else:
            parts.append({"text": prompt})
        payload = json.dumps({
            "contents": [{"parts": parts}],
            "generationConfig": {
                "maxOutputTokens": max_tokens,
                "temperature": temperature,
                **({"responseMimeType": "application/json"} if json_mode else {}),
            },
        }).encode("utf-8")
        url = f"{_BASE}/gemini-2.5-flash:generateContent?key={api_key}"
        req = urllib.request.Request(url, data=payload, headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=60) as r:
            res = json.loads(r.read())
        result = res["candidates"][0]["content"]["parts"][0]["text"].strip()
        print(f"  [Gemini 폴백] 텍스트 생성 완료")
        return result
    except Exception as e:
        print(f"  [Gemini 폴백] 실패: {e}")
    return None


# ── Vision (이미지 → 텍스트) ─────────────────────────────────────────────────

def vision(img_bytes: bytes, prompt: str, max_tokens: int = 800) -> str | None:
    """Gemini Vision으로 이미지 분석 → 텍스트 반환."""
    api_key = _api_key()
    if not api_key:
        return None
    try:
        img_b64 = base64.b64encode(img_bytes).decode()
        payload = json.dumps({
            "contents": [{
                "parts": [
                    {"text": prompt},
                    {"inline_data": {"mime_type": "image/jpeg", "data": img_b64}},
                ]
            }],
            "generationConfig": {"maxOutputTokens": max_tokens},
        }).encode("utf-8")
        url = f"{_BASE}/gemini-2.5-flash:generateContent?key={api_key}"
        req = urllib.request.Request(url, data=payload, headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=30) as r:
            res = json.loads(r.read())
        return res["candidates"][0]["content"]["parts"][0]["text"].strip()
    except Exception as e:
        print(f"  [Gemini Vision] 실패: {e}")
    return None


# ── 웹 서치 (Google Search Grounding) ────────────────────────────────────────

def web_search(query: str, max_tokens: int = 1500) -> str | None:
    """Gemini Google Search Grounding으로 실시간 웹 검색 후 요약 반환."""
    api_key = _api_key()
    if not api_key:
        return None
    print(f"  [Gemini 웹서치] {query[:60]}")
    try:
        payload = json.dumps({
            "contents": [{"parts": [{"text": query}]}],
            "tools": [{"google_search": {}}],
            "generationConfig": {"maxOutputTokens": max_tokens},
        }).encode("utf-8")
        url = f"{_BASE}/gemini-2.5-flash:generateContent?key={api_key}"
        req = urllib.request.Request(url, data=payload, headers={"Content-Type": "application/json"})
        with urllib.request.urlopen(req, timeout=30) as r:
            res = json.loads(r.read())
        parts = res.get("candidates", [{}])[0].get("content", {}).get("parts", [])
        return " ".join(p.get("text", "") for p in parts).strip() or None
    except Exception as e:
        print(f"  [Gemini 웹서치] 실패: {e}")
    return None


# ── 이미지 생성 (Imagen 3) ───────────────────────────────────────────────────

def generate_image(
    prompt: str,
    output_path: str,
    aspect_ratio: str = "16:9",
    model: str = "imagen-3.0-generate-001",
    negative_prompt: str = "",
    num_images: int = 1,
) -> str | None:
    """Gemini Imagen 3으로 이미지 생성.

    Args:
        prompt: 이미지 생성 프롬프트
        output_path: 저장할 파일 경로
        aspect_ratio: "1:1", "3:4", "4:3", "9:16", "16:9" 중 하나
        model: imagen-3.0-generate-001 (기본) 또는 imagen-3.0-fast-generate-001
        negative_prompt: 제외할 요소 (선택)
        num_images: 생성할 이미지 수 (1-4, 기본 1)

    Returns:
        성공 시 output_path, 실패 시 None
    """
    api_key = _api_key()
    if not api_key:
        print("  [Imagen] API 키 없음")
        return None

    try:
        # API 요청 구성
        request_data = {
            "instances": [
                {
                    "prompt": prompt,
                }
            ],
            "parameters": {
                "sampleCount": num_images,
                "aspectRatio": aspect_ratio,
                "negativePrompt": negative_prompt if negative_prompt else None,
            }
        }

        # None 값 제거
        if not negative_prompt:
            del request_data["parameters"]["negativePrompt"]

        payload = json.dumps(request_data).encode("utf-8")
        url = f"{_IMAGEN_BASE}/publishers/google/models/{model}:predict?key={api_key}"

        req = urllib.request.Request(
            url,
            data=payload,
            headers={"Content-Type": "application/json"}
        )

        with urllib.request.urlopen(req, timeout=120) as r:
            res = json.loads(r.read())

        # 응답에서 이미지 데이터 추출
        if "predictions" in res and len(res["predictions"]) > 0:
            # 첫 번째 이미지 사용
            img_data = res["predictions"][0]

            # bytesBase64Encoded 또는 images 필드에서 이미지 추출
            if "bytesBase64Encoded" in img_data:
                img_b64 = img_data["bytesBase64Encoded"]
            elif "images" in img_data and len(img_data["images"]) > 0:
                img_b64 = img_data["images"][0]
            else:
                print("  [Imagen] 응답에 이미지 데이터 없음")
                return None

            # Base64 디코딩 및 저장
            img_bytes = base64.b64decode(img_b64)

            # 디렉토리 생성
            os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)

            with open(output_path, "wb") as f:
                f.write(img_bytes)

            if os.path.exists(output_path):
                return output_path
            else:
                print(f"  [Imagen] 파일 저장 실패: {output_path}")
                return None
        else:
            print(f"  [Imagen] API 응답 형식 오류: {res}")
            return None

    except urllib.error.HTTPError as e:
        error_body = e.read().decode('utf-8') if e.fp else "No details"
        print(f"  [Imagen] HTTP 오류 {e.code}: {error_body[:200]}")
        return None
    except Exception as e:
        print(f"  [Imagen] 실패: {e}")
        return None
