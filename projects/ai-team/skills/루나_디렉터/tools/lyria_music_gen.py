#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
🎵 Lyria 3 음악 생성 도구 — 루나 에이전트 (Sound Director & Composer)
구글 최신 음악 모델 Lyria 3 Pro를 사용하여 3분짜리 완곡을 자동 생성.

사용법:
  python lyria_music_gen.py "신나는 K-pop 시티팝"          # 프롬프트 직접 지정
  python lyria_music_gen.py "calm jazz" -o my_music.mp3    # 출력 파일명 지정
  python lyria_music_gen.py                                 # 대화형 입력

환경 변수 (.env):
  GEMINI_API_KEY=your_key_here

프롬프트 공식 (장르/시대 + 템포/무드 + 악기 + 보컬 + 가사/주제):
  예) J-Pop City Pop × K-Pop (1980s Retro), Medium-fast 118 BPM,
      DX7 piano + slap bass + brass synth,
      Powerful K-pop female vocals, Midnight Seoul drive
"""

import os
import sys
import time
import subprocess
import argparse

# 프로젝트 루트 경로 설정
_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)

from _shared.env_loader import load_env, find_project_root
_root = find_project_root(_here)

def _get_duration(file_path: str) -> float | None:
    """ffprobe를 사용하여 생성된 오디오 파일의 실제 길이를 확인"""
    try:
        cmd = ["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", file_path]
        output = subprocess.check_output(cmd, stderr=subprocess.STDOUT).decode().strip()
        return float(output)
    except Exception:
        return None

try:
    from google import genai
    from google.genai import types
except ImportError:
    print("❌ 패키지 에러: 터미널에서 'pip install google-genai'를 실행해주세요.")
    sys.exit(1)


def generate_music_with_lyria(
    prompt: str,
    output_filename: str = "result_music.mp3",
    is_pro: bool = True,          # 항상 Pro 고정 — 30초 clip 금지
    max_retries: int = 3,         # 3분 미달 시 재시도 횟수
    output_dir: str | None = None,
) -> str | None:
    """
    Lyria 3 Pro로 최대 3분 완곡 생성 후 파일 경로 반환.

    Args:
        prompt:          음악 설명 프롬프트 (장르/시대+템포/무드+악기+보컬+가사)
        output_filename: 저장 파일명 (.mp3 / .wav)
        is_pro:          무조건 True 고정 (30초 clip 모드 사용 금지)
        output_dir:      저장 디렉토리 (None이면 현재 폴더)
    Returns:
        생성된 파일의 전체 경로, 실패 시 None
    """
    load_env()

    # lofi 금지 규칙 적용
    banned_keywords = ["lofi", "lo-fi", "study beats", "chill beats", "sleep music", "white noise"]
    if any(k in prompt.lower() for k in banned_keywords):
        print(f"❌ [루나] 금지된 장르(lofi 등)가 프롬프트에 포함되어 생성할 수 없습니다: {prompt}")
        return None

    api_key = (os.getenv("GEMINI_API_KEY") or os.getenv("GEMINI_API_KEYS") or "").split(",")[0].strip()
    if not api_key:
        print("❌ 인증 에러: .env 파일에 GEMINI_API_KEY를 설정해주세요!")
        return None

    if is_pro:
        # 60초 미만 생성 방지를 위한 프롬프트 가이드 강제 추가
        enriched_prompt = f"{prompt.strip()} Ensure this is a full-length track, approximately 3 minutes (180 seconds) long. Do not generate a short clip or intro."
        model_id = "lyria-3-pro-preview"
        mode_label = "[Pro] 완곡 모드 (최대 3분)"
    else:
        enriched_prompt = prompt.strip()
        model_id = "lyria-3-clip-preview"
        mode_label = "[Clip] 30초 클립 모드"

    print()
    print("=" * 54)
    print(f"  [루나] AI 음악 생성 — {model_id}")
    print(f"  {mode_label}")
    print("=" * 54)

    target_sec = 180 if is_pro else 30
    for attempt in range(1, max_retries + 1):
        print(f"\n[시도 {attempt}/{max_retries}] 생성 중... ({target_sec}초 목표)")
        try:
            client = genai.Client(api_key=api_key)
            response = client.models.generate_content(
                model=model_id,
                contents=enriched_prompt,
                config=types.GenerateContentConfig(
                    response_modalities=["AUDIO"]
                )
            )

            if not getattr(response, "candidates", None):
                print("❌ 곡 생성 실패 — 응답 없음")
                continue

            for part in response.candidates[0].content.parts:
                if hasattr(part, "inline_data") and part.inline_data is not None:
                    mime = getattr(part.inline_data, "mime_type", "")
                    if not mime.startswith("audio/"):
                        continue

                    # 확장자 결정
                    ext = ".mp3" if "mpeg" in mime else ".wav"
                    base = os.path.splitext(output_filename)[0]
                    final_name = base + ext

                    # 저장 경로 결정
                    if output_dir:
                        os.makedirs(output_dir, exist_ok=True)
                        final_path = os.path.join(output_dir, final_name)
                    else:
                        final_path = final_name if os.path.isabs(final_name) else os.path.join(_here, "output", final_name)
                        os.makedirs(os.path.dirname(final_path), exist_ok=True)

                    with open(final_path, "wb") as f:
                        f.write(part.inline_data.data)

                    # [검토] 생성된 파일의 길이 확인 (완곡 120초 미만 재시도 / 클립 25초 미만 재시도)
                    _MIN_DURATION = 120 if is_pro else 25
                    duration = _get_duration(final_path)
                    if duration is not None and duration < _MIN_DURATION:
                        if is_pro and duration < 60:
                            print(f"🗑️ [루나] 60초 미만 쓰레기 파일 발견 ({duration:.1f}초) — 즉시 삭제.")
                        else:
                            print(f"⚠️ [루나] 생성된 곡이 기준 미달 ({duration:.1f}초 < {_MIN_DURATION}초) — 재시도합니다.")
                        if os.path.exists(final_path):
                            os.remove(final_path)
                        continue

                    size_kb = os.path.getsize(final_path) / 1024
                    print(f"\n[루나] 음악 생성 완료!")
                    print(f"  파일: {final_path}")
                    print(f"  크기: {size_kb:.1f} KB")
                    dur_str = f"{duration:.1f}초" if duration is not None else "알 수 없음"
                    print(f"  길이: {dur_str} | 크기: {size_kb:.1f} KB")
                    print(f"  MIME: {mime}")
                    return final_path

            print("❌ 오디오 데이터가 응답에 없습니다.")

        except Exception as e:
            print(f"[ERROR] API 오류: {e}")

    return None


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Lyria 3 음악 생성 도구")
    parser.add_argument("prompt", nargs="?", default="", help="음악 프롬프트")
    parser.add_argument("-o", "--output", default="my_ai_music.mp3", help="출력 파일명")
    parser.add_argument("--clip", action="store_true", help="30초 클립 생성 모드 (lyria-3-clip-preview)")
    args = parser.parse_args()

    prompt = args.prompt.strip()
    if not prompt:
        print("\n🚀 Lyria 3 음악 생성 도구")
        print("💡 프롬프트 공식: 장르/시대 + 템포/무드 + 악기 + 보컬 + 가사/주제")
        prompt = input("어떤 음악을 만들고 싶으신가요? > ").strip()

    if prompt:
        generate_music_with_lyria(prompt, output_filename=args.output, is_pro=not args.clip)
    else:
        print("❌ 프롬프트가 없습니다.")
