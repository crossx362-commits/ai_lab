"""
nanobanana_client.py — 나노바나나2 이미지 생성 클라이언트

Banana.dev Serverless GPU 기반 이미지 생성 서비스
Flux, Stable Diffusion 등 다양한 모델 지원
"""
import os
import json
import base64
import urllib.request
import urllib.error


def _get_api_key() -> str:
    """나노바나나 API 키 가져오기."""
    return os.getenv("NANOBANANA_API_KEY", "") or os.getenv("BANANA_API_KEY", "")


def _get_model_key() -> str:
    """나노바나나 모델 키 가져오기 (배포된 모델 ID)."""
    return os.getenv("NANOBANANA_MODEL_KEY", "") or os.getenv("BANANA_MODEL_KEY", "")


def generate_image(
    prompt: str,
    output_path: str,
    width: int = 1280,
    height: int = 720,
    model: str = "flux-schnell",
    negative_prompt: str = "",
    num_inference_steps: int = 4,
    guidance_scale: float = 7.5,
) -> str | None:
    """나노바나나2로 이미지 생성.

    Args:
        prompt: 이미지 생성 프롬프트
        output_path: 저장할 파일 경로
        width: 이미지 너비 (기본 1280)
        height: 이미지 높이 (기본 720)
        model: "flux-schnell" (기본, 빠름) 또는 "flux-dev" (고품질)
        negative_prompt: 제외할 요소
        num_inference_steps: 추론 스텝 수 (4-50, flux-schnell은 4 권장)
        guidance_scale: 가이던스 스케일 (1.0-20.0)

    Returns:
        성공 시 output_path, 실패 시 None
    """
    api_key = _get_api_key()
    model_key = _get_model_key()

    if not api_key or not model_key:
        print("  [나노바나나] API 키 또는 모델 키 없음")
        return None

    try:
        # Banana.dev API 엔드포인트
        url = f"https://api.banana.dev/start/v4"

        # 요청 페이로드
        payload = {
            "apiKey": api_key,
            "modelKey": model_key,
            "modelInputs": {
                "prompt": prompt,
                "negative_prompt": negative_prompt,
                "width": width,
                "height": height,
                "num_inference_steps": num_inference_steps,
                "guidance_scale": guidance_scale,
                "seed": -1,  # 랜덤 시드
            }
        }

        # API 요청
        request_data = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(
            url,
            data=request_data,
            headers={
                "Content-Type": "application/json",
                "Accept": "application/json",
            }
        )

        print(f"  [나노바나나] 이미지 생성 중 ({model}, {width}x{height})...")

        with urllib.request.urlopen(req, timeout=120) as response:
            result = json.loads(response.read().decode("utf-8"))

        # 응답 처리
        if result.get("finished") and result.get("modelOutputs"):
            outputs = result["modelOutputs"]

            # Base64 이미지 추출
            if isinstance(outputs, list) and len(outputs) > 0:
                img_b64 = outputs[0].get("image_base64", "")
            elif isinstance(outputs, dict):
                img_b64 = outputs.get("image_base64", "")
            else:
                print(f"  [나노바나나] 응답 형식 오류: {result}")
                return None

            if not img_b64:
                print(f"  [나노바나나] 이미지 데이터 없음")
                return None

            # Base64 디코딩 및 저장
            img_bytes = base64.b64decode(img_b64)

            os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)

            with open(output_path, "wb") as f:
                f.write(img_bytes)

            if os.path.exists(output_path):
                print(f"  [나노바나나] 완료: {output_path}")
                return output_path
            else:
                print(f"  [나노바나나] 파일 저장 실패: {output_path}")
                return None
        else:
            print(f"  [나노바나나] 생성 실패: {result.get('message', 'Unknown error')}")
            return None

    except urllib.error.HTTPError as e:
        error_body = e.read().decode('utf-8') if e.fp else "No details"
        print(f"  [나노바나나] HTTP 오류 {e.code}: {error_body[:200]}")
        return None
    except Exception as e:
        print(f"  [나노바나나] 실패: {e}")
        return None


# ── Replicate API 폴백 (나노바나나 대안) ──────────────────────────────────

def generate_image_replicate(
    prompt: str,
    output_path: str,
    width: int = 1280,
    height: int = 720,
) -> str | None:
    """Replicate API로 Flux 이미지 생성 (나노바나나 대안).

    Args:
        prompt: 이미지 생성 프롬프트
        output_path: 저장할 파일 경로
        width: 이미지 너비
        height: 이미지 높이

    Returns:
        성공 시 output_path, 실패 시 None
    """
    api_token = os.getenv("REPLICATE_API_TOKEN", "")

    if not api_token:
        print("  [Replicate] API 토큰 없음")
        return None

    try:
        # Replicate Flux Schnell 모델
        url = "https://api.replicate.com/v1/predictions"

        payload = {
            "version": "black-forest-labs/flux-schnell",
            "input": {
                "prompt": prompt,
                "width": width,
                "height": height,
                "num_inference_steps": 4,
                "disable_safety_checker": True,
            }
        }

        request_data = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(
            url,
            data=request_data,
            headers={
                "Authorization": f"Token {api_token}",
                "Content-Type": "application/json",
            }
        )

        print(f"  [Replicate] 이미지 생성 중...")

        with urllib.request.urlopen(req, timeout=120) as response:
            result = json.loads(response.read().decode("utf-8"))

        # 결과 URL에서 이미지 다운로드
        if result.get("output") and len(result["output"]) > 0:
            image_url = result["output"][0]

            # 이미지 다운로드
            img_req = urllib.request.Request(image_url, headers={"User-Agent": "Mozilla/5.0"})
            with urllib.request.urlopen(img_req, timeout=30) as img_response:
                img_data = img_response.read()

            os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)

            with open(output_path, "wb") as f:
                f.write(img_data)

            if os.path.exists(output_path):
                print(f"  [Replicate] 완료: {output_path}")
                return output_path

        print(f"  [Replicate] 생성 실패")
        return None

    except Exception as e:
        print(f"  [Replicate] 실패: {e}")
        return None
