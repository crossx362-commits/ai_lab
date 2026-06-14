import os
import sys
import time
import io
import datetime
import subprocess

from google import genai
from google.genai import types
from PIL import Image

# Set paths
_here = os.path.dirname(os.path.abspath(__file__))
AI_TEAM_ROOT = os.path.abspath(os.path.join(_here, "projects", "ai-team"))
sys.path.insert(0, AI_TEAM_ROOT)

from _shared.env_loader import load_env
load_env()

# Initialize Client
client = genai.Client(api_key=os.getenv("GEMINI_API_KEY"))
VEO_MODEL_ID = "veo-3.1-generate-preview"
IMAGE_PATH = "/Users/junholee/.gemini/antigravity-ide/brain/4fd2a4c0-c51e-4cca-9173-1271b4837bdf/swiftcart_promo_bg_1781439599101.png"
AUDIO_PATH = "/Users/junholee/ai_lab/output/luna/full_track.mp3"
OUTPUT_DIR = os.path.join(_here, "output")
os.makedirs(OUTPUT_DIR, exist_ok=True)

def wait_for_active(vid_data):
    file_name = f"files/{vid_data.uri.split('files/')[-1].split(':')[0]}"
    print(f"⏳ [Veo] 후처리(ACTIVE) 상태 대기 중: {file_name}")
    while True:
        try:
            f = client.files.get(name=file_name)
            if hasattr(f, "state") and "ACTIVE" in str(f.state).upper():
                print("✅ [Veo] 비디오 활성화 완료.")
                break
        except Exception:
            pass
        time.sleep(5)
    return vid_data

def generate_veo_ad():
    print("=== [Veo 3.1] 30초 광고 영상 생성 시작 ===")
    
    # 1. Base Image 준비
    img = Image.open(IMAGE_PATH).convert("RGB")
    buf = io.BytesIO()
    img.save(buf, format="JPEG")
    val_image = types.Image(image_bytes=buf.getvalue(), mime_type="image/jpeg")
    
    base_prompt = "1980s retro anime style. A cute Shiba Inu dog sitting by the window in a cozy room, looking at the pet health spray and supplements on the table."
    extend_prompts = [
        "The Shiba Inu wags its tail happily and looks at the Furry Wellness Boost Spray bottle.",
        "A close-up of the natural deer antler chew on the table, shimmering slightly in the warm sunset light.",
        "The scene shows the probiotics bottle and joint chews being gently nudged by the dog's paw.",
        "Warm sunlight fills the room as the Shiba Inu dog lies down comfortably on a rug next to the products.",
        "Slow zoom out to reveal the entire room, showing the dog and the SwiftCart pet products in beautiful golden hour light."
    ]
    
    # Generate Phase 1 (5s)
    print("\n🎬 [Phase 1/6] 초기 5초 영상 생성 중...")
    op = client.models.generate_videos(
        model=VEO_MODEL_ID,
        prompt=base_prompt,
        image=val_image,
        config=types.GenerateVideosConfig(aspect_ratio="9:16", resolution="720p"),
    )

    while not op.done:
        print("  ⏳ 대기 중...")
        time.sleep(15)
        op = client.operations.get(operation=op)
        
    current_video = wait_for_active(op.result.generated_videos[0].video)
    
    # Extensions (5 * 5 = 25s)
    for idx, prompt in enumerate(extend_prompts):
        print(f"\n🎬 [Phase {idx+2}/6] 비디오 연장 중 (+5초 추가) -> Prompt: {prompt}")
        ext_op = client.models.generate_videos(
            model=VEO_MODEL_ID,
            prompt=prompt,
            video=current_video,
            config=types.GenerateVideosConfig(number_of_videos=1, resolution="720p"),
        )
        while not ext_op.done:
            print("  ⏳ 대기 중...")
            time.sleep(15)
            ext_op = client.operations.get(operation=ext_op)
        current_video = wait_for_active(ext_op.result.generated_videos[0].video)
        
    print("\n✅ 모든 비디오 연장 시퀀스 완료! 다운로드 시작...")
    file_name_part = "files/" + current_video.uri.split("files/")[-1].split(":")[0]
    
    temp_video_path = os.path.join(OUTPUT_DIR, "veo_temp_ad.mp4")
    video_bytes = client.files.download(file=file_name_part)
    with open(temp_video_path, "wb") as f:
        f.write(video_bytes)
    print(f"💾 임시 비디오 다운로드 성공: {temp_video_path}")
    
    # 3. Audio와 합성
    final_video_path = os.path.join(OUTPUT_DIR, "swiftcart_veo_ad.mp4")
    print("\n🎵 3단계: 비디오와 오디오 합성 중...")
    
    cmd = [
        "ffmpeg", "-y",
        "-i", temp_video_path,
        "-i", AUDIO_PATH,
        "-c:v", "copy",
        "-c:a", "aac", "-b:a", "192k",
        "-t", "30",
        "-map", "0:v:0", "-map", "1:a:0",
        "-shortest",
        final_video_path
    ]
    
    r = subprocess.run(cmd, capture_output=True, text=True)
    if r.returncode == 0 and os.path.exists(final_video_path):
        print(f"🎉 최종 Veo 광고영상 합성 완료!")
        print(f"📍 최종 경로: {final_video_path}")
    else:
        print(f"❌ 영상 합성 실패: {r.stderr}")

if __name__ == "__main__":
    generate_veo_ad()
