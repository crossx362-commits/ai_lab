"""Unified utilities - path, resources, ffmpeg, image upload."""
import json
import os
import shutil
import subprocess
import sys
from datetime import datetime
from pathlib import Path

_NOWIN = {"creationflags": subprocess.CREATE_NO_WINDOW} if sys.platform == "win32" else {}


# ==================== SCHEDULE SLOT GATE ====================
def due_slot(slots: list[str], state_path: str | Path, weekdays_only: bool = True) -> str | None:
    """정해진 시각(slots, 'HH:MM')이 도래했고 오늘 아직 안 보냈으면 그 슬롯을 반환, 아니면 None.
    데몬이 시작 즉시·매 틱마다 보고하는 것을 막고 '정해진 시간에 한 번만' 실행하게 한다.
    지난 슬롯은 재시작 시 catch-up(한 번)된다. 상태는 state_path(json)에 날짜로 기록."""
    now = datetime.now()
    if weekdays_only and now.weekday() >= 5:
        return None
    state_path = Path(state_path)
    try:
        sent = json.loads(state_path.read_text(encoding="utf-8")) if state_path.exists() else {}
    except Exception:
        sent = {}
    today = now.strftime("%Y-%m-%d")
    for s in slots:
        try:
            h, m = (int(x) for x in s.split(":"))
        except Exception:
            continue
        slot_dt = now.replace(hour=h, minute=m, second=0, microsecond=0)
        if now >= slot_dt and sent.get(s) != today:
            sent[s] = today
            try:
                state_path.parent.mkdir(parents=True, exist_ok=True)
                state_path.write_text(json.dumps(sent, ensure_ascii=False), encoding="utf-8")
            except Exception:
                pass
            return s
    return None


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
        subprocess.run(cmd, capture_output=True, timeout=timeout, check=True, **_NOWIN)
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
        subprocess.run(cmd, capture_output=True, check=True, timeout=120, **_NOWIN)
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
