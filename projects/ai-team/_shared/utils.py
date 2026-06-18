"""Unified utilities - path, resources, ffmpeg, image upload."""
import os
import shutil
import subprocess
from pathlib import Path


# ==================== PATH UTILITIES ====================

def find_root(start: str | None = None) -> Path:
    """Find project root (D:/ai_lab)."""
    root = Path(start or __file__).resolve()
    for _ in range(10):
        if (root / "ENV_MANIFEST.json").exists() or (root / ".env.encrypted").exists():
            return root
        if root.parent == root:
            break
        root = root.parent
    return Path(__file__).resolve().parent


def ensure_dir(path: str | Path) -> Path:
    """Create directory if missing."""
    p = Path(path)
    p.mkdir(parents=True, exist_ok=True)
    return p


def safe_filename(name: str) -> str:
    """Sanitize filename for cross-platform compatibility."""
    return "".join(c if c.isalnum() or c in "._- " else "_" for c in name).strip()


# ==================== RESOURCE UTILITIES ====================

def check_command(cmd: str) -> bool:
    """Check if command exists."""
    return shutil.which(cmd) is not None


def run_silent(cmd: list[str], timeout: int = 30) -> bool:
    """Run command silently, return success."""
    try:
        subprocess.run(cmd, capture_output=True, timeout=timeout, check=True)
        return True
    except Exception:
        return False


# ==================== FFMPEG ====================

def ffmpeg_installed() -> bool:
    """Check if ffmpeg is available."""
    return check_command("ffmpeg")


def convert_video(input_path: str | Path, output_path: str | Path, codec: str = "libx264") -> bool:
    """Convert video using ffmpeg."""
    if not ffmpeg_installed():
        print("❌ ffmpeg not installed")
        return False
    try:
        cmd = ["ffmpeg", "-i", str(input_path), "-c:v", codec, "-y", str(output_path)]
        subprocess.run(cmd, capture_output=True, check=True, timeout=120)
        print(f"✅ Converted: {output_path}")
        return True
    except Exception as e:
        print(f"❌ ffmpeg failed: {e}")
        return False


# ==================== IMAGE UPLOADER (Imgur fallback) ====================

def upload_image(image_path: str | Path) -> str | None:
    """
    Upload image to Imgur (public, anonymous).
    Returns URL or None.
    """
    import json
    import urllib.request

    try:
        with open(image_path, "rb") as f:
            import base64
            img_b64 = base64.b64encode(f.read()).decode()

        payload = json.dumps({"image": img_b64}).encode()
        req = urllib.request.Request(
            "https://api.imgur.com/3/image",
            data=payload,
            headers={
                "Authorization": "Client-ID 546c25a59c58ad7",  # Public Imgur client
                "Content-Type": "application/json",
            },
        )

        with urllib.request.urlopen(req, timeout=30) as r:
            res = json.loads(r.read())

        if res.get("success"):
            url = res["data"]["link"]
            print(f"✅ Image uploaded: {url}")
            return url
        else:
            print(f"❌ Imgur upload failed: {res}")
            return None
    except Exception as e:
        print(f"❌ Image upload error: {e}")
        return None
