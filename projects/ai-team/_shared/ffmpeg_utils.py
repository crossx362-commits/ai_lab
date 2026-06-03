"""
ffmpeg_utils.py — FFmpeg/FFprobe 경로 감지 및 썸네일 보정 공통 유틸
"""
import os

_WINGET_BASE = (
    r"C:\Users\cross\AppData\Local\Microsoft\WinGet\Packages"
    r"\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe"
    r"\ffmpeg-8.1.1-full_build\bin"
)

def get_ffmpeg_path() -> str:
    win = os.path.join(_WINGET_BASE, "ffmpeg.exe")
    return win if os.path.exists(win) else "ffmpeg"

def get_ffprobe_path() -> str:
    win = os.path.join(_WINGET_BASE, "ffprobe.exe")
    return win if os.path.exists(win) else "ffprobe"

def enhance_thumbnail(img_path: str, color: float = 1.3, contrast: float = 1.2) -> bool:
    """PIL로 썸네일 채도·대비 보정. 성공 시 True."""
    try:
        from PIL import Image, ImageEnhance
        img = Image.open(img_path)
        img = ImageEnhance.Color(img).enhance(color)
        img = ImageEnhance.Contrast(img).enhance(contrast)
        img.save(img_path)
        return True
    except Exception as e:
        print(f"⚠️ 썸네일 보정 실패: {e}")
        return False
