"""
ffmpeg_utils.py — FFmpeg/FFprobe 경로 감지 및 썸네일 보정 공통 유틸
"""
import os
import glob

def _find_winget_ffmpeg_bin() -> str:
    local_app = os.environ.get("LOCALAPPDATA", "")
    if not local_app:
        return ""
    pattern = os.path.join(
        local_app, "Microsoft", "WinGet", "Packages",
        "Gyan.FFmpeg_*", "*", "bin"
    )
    matches = glob.glob(pattern)
    return matches[0] if matches else ""

_WINGET_BIN = _find_winget_ffmpeg_bin()

def get_ffmpeg_path() -> str:
    win = os.path.join(_WINGET_BIN, "ffmpeg.exe") if _WINGET_BIN else ""
    return win if win and os.path.exists(win) else "ffmpeg"

def get_ffprobe_path() -> str:
    win = os.path.join(_WINGET_BIN, "ffprobe.exe") if _WINGET_BIN else ""
    return win if win and os.path.exists(win) else "ffprobe"

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
