"""
image_uploader.py — 공통 이미지 업로드 모듈

ImgBB(우선) → Catbox.moe(폴백) → Pollinations.ai(URL 직접)
"""
import os
import json
import base64
import urllib.request
import urllib.parse


def upload_imgbb(image_bytes: bytes, filename: str, api_key: str) -> str | None:
    """ImgBB에 이미지 업로드 → 영구 공개 URL 반환."""
    try:
        b64 = base64.b64encode(image_bytes).decode("utf-8")
        name = os.path.splitext(filename)[0]
        data = (
            f"key={urllib.parse.quote(api_key)}"
            f"&image={urllib.parse.quote(b64)}"
            f"&name={urllib.parse.quote(name)}"
        )
        req = urllib.request.Request(
            "https://api.imgbb.com/1/upload",
            data=data.encode("utf-8"),
            headers={
                "Content-Type": "application/x-www-form-urlencoded",
                "User-Agent": "ConnectAIBot/1.0",
            },
        )
        with urllib.request.urlopen(req, timeout=30) as r:
            result = json.loads(r.read())
        url = result.get("data", {}).get("url", "")
        if url:
            print(f"    [ImgBB] 업로드 완료: {url}")
            return url
    except Exception as e:
        print(f"    [ImgBB] 실패: {e}")
    return None


def upload_catbox(image_bytes: bytes, filename: str) -> str | None:
    """Catbox.moe에 이미지 업로드 → 영구 공개 URL 반환."""
    try:
        boundary = "CAIBoundary8f3e"
        body = (
            f"--{boundary}\r\n"
            f'Content-Disposition: form-data; name="reqtype"\r\n\r\nfileupload\r\n'
            f"--{boundary}\r\n"
            f'Content-Disposition: form-data; name="fileToUpload"; filename="{filename}"\r\n'
            f"Content-Type: image/png\r\n\r\n"
        ).encode() + image_bytes + f"\r\n--{boundary}--\r\n".encode()

        req = urllib.request.Request(
            "https://catbox.moe/user/api.php",
            data=body,
            headers={
                "Content-Type": f"multipart/form-data; boundary={boundary}",
                "User-Agent": "ConnectAIBot/1.0",
            },
        )
        with urllib.request.urlopen(req, timeout=30) as r:
            url = r.read().decode().strip()
        if url.startswith("https://files.catbox.moe/"):
            print(f"    [Catbox] 업로드 완료: {url}")
            return url
    except Exception as e:
        print(f"    [Catbox] 실패: {e}")
    return None


def upload_image(image_bytes: bytes, filename: str) -> str | None:
    """ImgBB(우선) → Catbox(폴백) 순서로 업로드. 환경변수 IMGBB_API_KEY 사용."""
    imgbb_key = os.getenv("IMGBB_API_KEY", "")
    if imgbb_key:
        url = upload_imgbb(image_bytes, filename, imgbb_key)
        if url:
            return url
    print("    [업로드] ImgBB 실패 → Catbox 폴백...")
    return upload_catbox(image_bytes, filename)


def pollinations_url(prompt: str, width: int = 1280, height: int = 720) -> str:
    """Pollinations.ai 이미지 직접 URL (업로드 불필요, 공개 URL)."""
    encoded = urllib.parse.quote(prompt)
    seed = abs(hash(prompt)) % 99999
    return (
        f"https://image.pollinations.ai/prompt/{encoded}"
        f"?width={width}&height={height}&model=flux&nologo=true&seed={seed}"
    )
