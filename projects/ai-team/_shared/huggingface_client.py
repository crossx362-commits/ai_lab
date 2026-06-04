"""
huggingface_client.py — Hugging Face Inference API 이미지 생성

Flux, Stable Diffusion 등 무료 이미지 생성 모델 지원
"""
import os
import urllib.request
import urllib.error
import json
import time


def _get_api_key() -> str:
    """Hugging Face API 키 가져오기."""
    return os.getenv("HUGGINGFACE_API_KEY", "") or os.getenv("HF_TOKEN", "")


def generate_image(
    prompt: str,
    output_path: str,
    model: str = "black-forest-labs/FLUX.1-schnell",
    width: int = 1280,
    height: int = 720,
    num_inference_steps: int = 4,
    guidance_scale: float = 0.0,
) -> str | None:
    """Hugging Face Inference API로 이미지 생성.

    Args:
        prompt: 이미지 생성 프롬프트
        output_path: 저장할 파일 경로
        model: 사용할 모델 (기본: FLUX.1-schnell - 빠르고 무료)
        width: 이미지 너비
        height: 이미지 높이
        num_inference_steps: 추론 스텝 수 (FLUX schnell은 4 권장)
        guidance_scale: 가이던스 스케일 (FLUX schnell은 0.0 권장)

    Returns:
        성공 시 output_path, 실패 시 None
    """
    api_key = _get_api_key()
    if not api_key:
        print("  [HuggingFace] API 키 없음 - 환경 변수 HUGGINGFACE_API_KEY 또는 HF_TOKEN 설정 필요")
        return None

    try:
        # API 엔드포인트
        api_url = f"https://api-inference.huggingface.co/models/{model}"

        # 요청 데이터
        payload = {
            "inputs": prompt,
            "parameters": {
                "width": width,
                "height": height,
                "num_inference_steps": num_inference_steps,
            }
        }

        # guidance_scale은 FLUX schnell에서는 사용하지 않음
        if guidance_scale > 0:
            payload["parameters"]["guidance_scale"] = guidance_scale

        headers = {
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
        }

        request_data = json.dumps(payload).encode("utf-8")
        req = urllib.request.Request(api_url, data=request_data, headers=headers)

        # 모델 로딩 대기 (최대 3번 재시도)
        for attempt in range(3):
            try:
                with urllib.request.urlopen(req, timeout=60) as response:
                    img_bytes = response.read()

                # JSON 오류 응답 체크
                if img_bytes.startswith(b'{'):
                    error_data = json.loads(img_bytes.decode('utf-8'))
                    if "error" in error_data:
                        error_msg = error_data["error"]
                        if "loading" in error_msg.lower():
                            wait_time = error_data.get("estimated_time", 20)
                            print(f"  [HuggingFace] 모델 로딩 중... {wait_time}초 대기 (시도 {attempt + 1}/3)")
                            time.sleep(wait_time)
                            continue
                        else:
                            print(f"  [HuggingFace] API 오류: {error_msg}")
                            return None

                # 이미지 데이터가 너무 작으면 오류
                if len(img_bytes) < 5000:
                    print(f"  [HuggingFace] 이미지 데이터가 너무 작음 ({len(img_bytes)} bytes)")
                    return None

                # 파일 저장
                os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
                with open(output_path, "wb") as f:
                    f.write(img_bytes)

                if os.path.exists(output_path) and os.path.getsize(output_path) > 5000:
                    print(f"  [HuggingFace FLUX.1] 이미지 생성 완료: {output_path} ({len(img_bytes):,} bytes)")
                    return output_path
                else:
                    print(f"  [HuggingFace] 파일 저장 실패: {output_path}")
                    return None

            except urllib.error.HTTPError as e:
                error_body = e.read().decode('utf-8')
                print(f"  [HuggingFace] HTTP {e.code}: {error_body[:200]}")
                return None

        print(f"  [HuggingFace] 모델 로딩 타임아웃 (3회 시도 실패)")
        return None

    except Exception as e:
        print(f"  [HuggingFace] 실패: {e}")
        return None
