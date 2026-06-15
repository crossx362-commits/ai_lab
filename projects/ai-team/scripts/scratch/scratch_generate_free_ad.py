import os
import subprocess

# Paths
_here = os.path.dirname(os.path.abspath(__file__))
OUTPUT_DIR = os.path.join(_here, "output")
os.makedirs(OUTPUT_DIR, exist_ok=True)

# Image Paths
images = [
    "/Users/junholee/.gemini/antigravity-ide/brain/4fd2a4c0-c51e-4cca-9173-1271b4837bdf/swiftcart_eat_scene1_1781440867183.png",
    "/Users/junholee/.gemini/antigravity-ide/brain/4fd2a4c0-c51e-4cca-9173-1271b4837bdf/swiftcart_eat_scene2_1781440886086.png",
    "/Users/junholee/.gemini/antigravity-ide/brain/4fd2a4c0-c51e-4cca-9173-1271b4837bdf/swiftcart_eat_scene3_1781440905096.png",
    "/Users/junholee/.gemini/antigravity-ide/brain/4fd2a4c0-c51e-4cca-9173-1271b4837bdf/swiftcart_eat_scene4_1781440922529.png"
]
audio_path = "/Users/junholee/ai_lab/output/luna/full_track.mp3"
final_video_path = os.path.join(OUTPUT_DIR, "swiftcart_promo_video.mp4")

def check_files():
    for idx, path in enumerate(images):
        if not os.path.exists(path):
            print(f"❌ Image {idx+1} not found at: {path}")
            return False
    if not os.path.exists(audio_path):
        print(f"❌ Audio not found at: {audio_path}")
        return False
    return True

def compile_video():
    print("=== [Free Video Generator] Compiling 30s Video Ad using FFmpeg ===")
    
    # We have 4 images. To get 30 seconds, each image runs for 7.5 seconds.
    # We can create a vertical video by scaling each image to 1080x1920 using a blurred background.
    # FFmpeg filter:
    # 1. Scale image to 1080x1920 (stretching it), apply heavy boxblur.
    # 2. Scale image to 1080x1080 (maintaining aspect ratio), overlay it on top of the blurred background in the center.
    
    # Let's build a complex filtergraph or process each image into a temporary video file, then concat them.
    # Processing each image into a 7.5s video clip is much simpler and robust.
    temp_clips = []
    for idx, img_path in enumerate(images):
        temp_clip = os.path.join(OUTPUT_DIR, f"temp_clip_{idx}.mp4")
        print(f"🎬 Creating clip {idx+1} from {os.path.basename(img_path)}...")
        
        # FFmpeg command to turn static image into vertical video with blurred background
        cmd = [
            "ffmpeg", "-y",
            "-loop", "1", "-i", img_path,
            "-t", "7.5",
            "-filter_complex", (
                "[0:v]scale=1080:1920,boxblur=40:5[bg]; "
                "[0:v]scale=1080:1080[fg]; "
                "[bg][fg]overlay=(W-w)/2:(H-h)/2[outv]"
            ),
            "-map", "[outv]",
            "-c:v", "libx264", "-pix_fmt", "yuv420p", "-r", "30",
            temp_clip
        ]
        
        r = subprocess.run(cmd, capture_output=True, text=True)
        if r.returncode != 0:
            print(f"❌ Failed to create clip {idx+1}: {r.stderr}")
            return
        temp_clips.append(temp_clip)
        
    # Concat the 4 clips
    concat_txt_path = os.path.join(OUTPUT_DIR, "concat_list.txt")
    with open(concat_txt_path, "w") as f:
        for clip in temp_clips:
            f.write(f"file '{clip}'\n")
            
    temp_concat_video = os.path.join(OUTPUT_DIR, "temp_concat.mp4")
    print("🎬 Concatenating clips...")
    cmd_concat = [
        "ffmpeg", "-y",
        "-f", "concat", "-safe", "0", "-i", concat_txt_path,
        "-c", "copy",
        temp_concat_video
    ]
    r = subprocess.run(cmd_concat, capture_output=True, text=True)
    if r.returncode != 0:
        print(f"❌ Concat failed: {r.stderr}")
        return
        
    # Merge with audio
    print("🎵 Merging with audio track...")
    cmd_merge = [
        "ffmpeg", "-y",
        "-i", temp_concat_video,
        "-i", audio_path,
        "-c:v", "copy",
        "-c:a", "aac", "-b:a", "192k",
        "-t", "30",
        "-map", "0:v:0", "-map", "1:a:0",
        "-shortest",
        final_video_path
    ]
    r = subprocess.run(cmd_merge, capture_output=True, text=True)
    if r.returncode == 0 and os.path.exists(final_video_path):
        print(f"🎉 SUCCESS! Final video compiled at: {final_video_path}")
        
        # Clean up temporary files
        for clip in temp_clips:
            try:
                os.remove(clip)
            except Exception:
                pass
        try:
            os.remove(concat_txt_path)
            os.remove(temp_concat_video)
        except Exception:
            pass
    else:
        print(f"❌ Merge failed: {r.stderr}")

if __name__ == "__main__":
    if check_files():
        compile_video()
