"""
youtube_generator.py — YouTube 콘텐츠 자동 생성 모듈
이미지 + 포스팅 데이터 → 유튜브 영상(ffmpeg) + 메타데이터 생성
"""
import os
import sys
import json
import subprocess
import tempfile

# 경로 설정
_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(6):
    if os.path.isdir(os.path.join(_root, "reports")):
        break
    _root = os.path.dirname(_root)
_shared = os.path.join(_root, "_shared")
if _shared not in sys.path:
    sys.path.insert(0, _shared)
if _root not in sys.path:
    sys.path.insert(0, _root)

from ffmpeg_utils import get_ffmpeg_path, enhance_thumbnail


def _build_video_from_image(img_bytes: bytes, duration: int = 7, output_dir: str = None) -> str:
    """
    이미지 바이트 → MP4 (정지 화면 슬라이드).
    반환: 생성된 비디오 파일 경로
    """
    out_dir = output_dir or tempfile.gettempdir()
    img_path = os.path.join(out_dir, "yt_source.jpg")
    vid_path = os.path.join(out_dir, "yt_video.mp4")

    with open(img_path, "wb") as f:
        f.write(img_bytes)

    # 썸네일 색상·대비 보정
    enhance_thumbnail(img_path)

    ffmpeg = get_ffmpeg_path()
    cmd = [
        ffmpeg, "-y",
        "-loop", "1",
        "-i", img_path,
        "-c:v", "libx264",
        "-t", str(duration),
        "-pix_fmt", "yuv420p",
        "-vf", "scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2",
        "-r", "30",
        vid_path,
    ]
    result = subprocess.run(cmd, capture_output=True, text=True)
    if result.returncode != 0:
        raise RuntimeError(f"ffmpeg 영상 생성 실패: {result.stderr[-300:]}")

    # 임시 이미지 정리
    if os.path.exists(img_path):
        os.remove(img_path)

    return vid_path


def _generate_yt_metadata(post_data: dict) -> dict:
    """
    Ollama(gemma3) 또는 규칙 기반으로 YouTube 메타데이터 생성.
    반환: {"title": str, "description": str, "tags": [str]}
    """
    try:
        from ollama_client import chat, is_available
    except ImportError:
        from _shared.ollama_client import chat, is_available

    trend   = post_data.get("trend", post_data.get("topic", "일상"))
    caption = post_data.get("caption", "")
    season  = post_data.get("season", "여름")
    prompt_img = post_data.get("image_prompt", "")

    if is_available():
        prompt = (
            f"트렌드: {trend} / 계절: {season}\n"
            f"인스타 캡션: {caption}\n"
            f"이미지 프롬프트: {prompt_img[:100]}\n\n"
            "위 내용을 기반으로 YouTube 영상 메타데이터를 작성해 주세요.\n"
            "조건:\n"
            "- title: 60자 이하, 이모지 1개, 핵심 키워드 포함\n"
            "- description: 200자 이상, 해시태그 5~8개, AI·기술·인공지능 금지\n"
            "- tags: 한국어 태그 10개 배열 (# 포함)\n"
            "JSON만 반환: {\"title\":\"...\",\"description\":\"...\",\"tags\":[\"#...\"]}"
        )
        try:
            raw = chat(prompt, max_tokens=500, temperature=0.7, json_mode=True)
            if raw:
                data = json.loads(raw.strip())
                return {
                    "title": data.get("title", f"{trend} 감성 영상 🎬"),
                    "description": data.get("description", caption),
                    "tags": data.get("tags", ["#일상", "#감성", "#영상"]),
                }
        except Exception as e:
            print(f"  ⚠️ Ollama 메타데이터 생성 실패 (폴백 사용): {e}")

    # 규칙 기반 폴백
    return {
        "title": f"{trend} – {season} 감성 영상 🎬",
        "description": (
            f"{caption}\n\n"
            f"📍 {trend} | {season} 분위기\n"
            "#일상 #감성 #영상 #한국 #힐링 #여행 #자연 #풍경"
        ),
        "tags": ["#일상", "#감성", "#영상", "#한국", "#힐링", f"#{trend.replace(' ', '')}"],
    }


def generate_youtube_assets(post_data: dict, img_bytes: bytes, output_dir: str = None) -> dict:
    """
    메인 진입점: 이미지 + 포스팅 데이터 → YouTube 자산 전체 생성.
    반환:
        {
            "title": str,
            "description": str,
            "tags": [str],
            "video_path": str,        # 생성된 MP4 경로
            "decision": str,          # 경수 승인 요청 문자열
        }
    """
    print("🎬 YouTube 자산 생성 시작...")

    # 1. 메타데이터 생성
    metadata = _generate_yt_metadata(post_data)
    print(f"  📝 제목: {metadata['title']}")
    print(f"  🏷️  태그: {', '.join(metadata['tags'][:5])}...")

    # 2. 영상 생성 (이미지 → MP4)
    video_path = None
    if img_bytes:
        try:
            out_dir = output_dir or os.path.join(_root, "reports", "uploads", "youtube")
            os.makedirs(out_dir, exist_ok=True)
            video_path = _build_video_from_image(img_bytes, duration=7, output_dir=out_dir)
            print(f"  ✅ 영상 생성 완료: {video_path}")
        except Exception as e:
            print(f"  ⚠️ 영상 생성 실패 (이미지만 사용): {e}")
            video_path = None
    else:
        print("  ⚠️ img_bytes 없음 — 영상 생성 건너뜀")

    return {
        "title": metadata["title"],
        "description": metadata["description"],
        "tags": metadata["tags"],
        "video_path": video_path,
        "decision": f"[경수 승인 요청] YouTube 콘텐츠 검토\n제목: {metadata['title']}\n태그: {', '.join(metadata['tags'][:5])}",
    }
