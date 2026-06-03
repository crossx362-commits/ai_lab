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
import subprocess
import argparse

_here = os.path.dirname(os.path.abspath(__file__))
_ai_team_root = os.path.abspath(os.path.join(_here, "..", "..", ".."))
if _ai_team_root not in sys.path:
    sys.path.insert(0, _ai_team_root)

from _shared.env_loader import load_env, find_project_root
_root = find_project_root(_here)

try:
    from google import genai
    from google.genai import types
except ImportError:
    print("❌ 패키지 에러: 터미널에서 'pip install google-genai'를 실행해주세요.")
    sys.exit(1)

_BANNED = ["lofi", "lo-fi", "study beats", "chill beats", "sleep music", "white noise"]


def _get_duration(file_path: str) -> float | None:
    """ffprobe로 오디오 길이(초) 반환. 실패 시 None."""
    try:
        out = subprocess.check_output(
            ["ffprobe", "-v", "error", "-show_entries", "format=duration",
             "-of", "default=noprint_wrappers=1:nokey=1", file_path],
            stderr=subprocess.STDOUT,
        ).decode().strip()
        return float(out)
    except Exception:
        return None


def generate_music_with_lyria(
    prompt: str,
    output_filename: str = "result_music.mp3",
    is_pro: bool = True,
    max_retries: int = 3,
    output_dir: str | None = None,
) -> str | None:
    """
    Lyria 3 Pro로 최대 3분 완곡 생성 후 파일 경로 반환.

    Args:
        prompt:          음악 설명 프롬프트 (장르/시대+템포/무드+악기+보컬+가사)
        output_filename: 저장 파일명 (.mp3 / .wav)
        is_pro:          True = lyria-3-pro-preview (최대 3분), False = lyria-3-clip-preview (30초)
        max_retries:     길이 미달 시 최대 재시도 횟수
        output_dir:      저장 디렉토리 (None이면 output/ 폴더)
    Returns:
        생성된 파일의 전체 경로, 실패 시 None
    """
    load_env()

    if any(k in prompt.lower() for k in _BANNED):
        print(f"❌ [루나] 금지 장르 포함: {prompt}")
        return None

    api_key = (
        os.getenv("GEMINI_MUSIC_KEY") or
        os.getenv("GEMINI_API_KEY") or
        os.getenv("GEMINI_API_KEYS") or ""
    ).split(",")[0].strip()

    if not api_key:
        print("❌ 인증 에러: .env 파일에 GEMINI_API_KEY를 설정해주세요!")
        return None

    model_id = "lyria-3-pro-preview" if is_pro else "lyria-3-clip-preview"
    contents = (
        f"{prompt.strip()} Ensure this is a full-length track, approximately 3 minutes (180 seconds) long. Do not generate a short clip or intro."
        if is_pro else prompt.strip()
    )
    min_sec = 120 if is_pro else 25

    print(f"\n{'='*54}")
    print(f"  🎵 [루나] AI 음악 생성 — {model_id}")
    print(f"{'='*54}")
    print(f"  프롬프트: {prompt[:100]}")
    print(f"  ⏳ 생성 중... (1~3분 소요될 수 있습니다)")

    client = genai.Client(api_key=api_key)

    for attempt in range(1, max_retries + 1):
        if attempt > 1:
            print(f"\n[시도 {attempt}/{max_retries}]")
        try:
            response = client.models.generate_content(
                model=model_id,
                contents=contents,
                config=types.GenerateContentConfig(
                    response_modalities=["AUDIO"]
                ),
            )

            if not getattr(response, "candidates", None):
                print("❌ 응답 없음")
                continue

            for part in response.candidates[0].content.parts:
                if not (hasattr(part, "inline_data") and part.inline_data):
                    continue
                mime = getattr(part.inline_data, "mime_type", "")
                if not mime.startswith("audio/"):
                    continue

                ext = ".mp3" if "mpeg" in mime else ".wav"
                base = os.path.splitext(output_filename)[0]
                final_name = base + ext

                if output_dir:
                    os.makedirs(output_dir, exist_ok=True)
                    final_path = os.path.join(output_dir, final_name)
                else:
                    final_path = os.path.join(_here, "output", final_name)
                    os.makedirs(os.path.dirname(final_path), exist_ok=True)

                with open(final_path, "wb") as f:
                    f.write(part.inline_data.data)

                duration = _get_duration(final_path)
                if duration is not None and duration < min_sec:
                    print(f"⚠️ 길이 미달 ({duration:.1f}초 < {min_sec}초) — 재시도")
                    os.remove(final_path)
                    continue

                size_kb = os.path.getsize(final_path) / 1024
                dur_str = f"{duration:.1f}초" if duration else "알 수 없음"
                print(f"\n✅ 음악 생성 완료!")
                print(f"  파일: {final_path}")
                print(f"  길이: {dur_str} | 크기: {size_kb:.1f} KB")
                return final_path

            print("❌ 오디오 데이터가 응답에 없습니다.")

        except Exception as e:
            print(f"[ERROR] API 오류: {e}")

    return None


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Lyria 3 음악 생성 도구")
    parser.add_argument("prompt", nargs="?", default="", help="음악 프롬프트")
    parser.add_argument("-o", "--output", default="my_ai_music.mp3", help="출력 파일명")
    parser.add_argument("--clip", action="store_true", help="30초 클립 모드 (lyria-3-clip-preview)")
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
