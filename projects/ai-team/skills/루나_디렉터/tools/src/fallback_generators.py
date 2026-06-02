"""
fallback_generators.py — 루나 파이프라인 폴백 생성기 모음

각 단계가 실패할 때 순서대로 시도하는 대안 생성기들.
BGM:   Lyria → Pollinations.ai → dummy.wav
Video: ffmpeg 정적 → VEO 3.1 → Ken Burns 슬라이드쇼
"""
import os
import subprocess
import shutil
import urllib.request
import urllib.parse
import json
import time


# ─── BGM 폴백: Pollinations.ai ──────────────────────────────────────────────

def generate_music_pollinations(prompt: str, output_path: str) -> str:
    """Pollinations.ai 오디오 API로 BGM 생성. 성공 시 output_path 반환."""
    try:
        encoded = urllib.parse.quote(prompt[:200])
        url = f"https://audio.pollinations.ai/{encoded}"
        print(f"    [Pollinations BGM] 요청 중: {prompt[:60]}...")
        req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(req, timeout=60) as r:
            data = r.read()
        if len(data) < 1000:
            print(f"    [Pollinations BGM] 응답 데이터 너무 작음 ({len(data)} bytes)")
            return ""
        os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
        with open(output_path, "wb") as f:
            f.write(data)
        print(f"    [Pollinations BGM] 완료: {output_path} ({len(data):,} bytes)")
        return output_path
    except Exception as e:
        print(f"    [Pollinations BGM] 실패: {e}")
        return ""


# ─── 영상 폴백 1: VEO 3.1 ────────────────────────────────────────────────────

def generate_video_veo(visual_prompt: str, output_path: str, api_key: str) -> str:
    """VEO 3.1로 AI 영상 생성. 성공 시 output_path 반환."""
    try:
        from google import genai
        from google.genai import types
        client = genai.Client(api_key=api_key)
        print(f"    [VEO] 생성 요청: {visual_prompt[:60]}...")
        op = client.models.generate_videos(
            model="veo-3.1-generate-preview",
            prompt=visual_prompt,
            config=types.GenerateVideoConfig(
                aspect_ratio="16:9",
                duration_seconds=8,
                number_of_videos=1,
            ),
        )
        # 완료 대기 (최대 5분)
        for _ in range(30):
            time.sleep(10)
            op = client.operations.get(op)
            if op.done:
                break
        if not op.done or not op.response:
            print("    [VEO] 타임아웃 또는 응답 없음")
            return ""
        video_obj = op.response.generated_videos[0].video
        os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)
        client.files.download(file=video_obj, download_path=output_path)
        print(f"    [VEO] 완료: {output_path}")
        return output_path
    except Exception as e:
        print(f"    [VEO] 실패: {e}")
        return ""


# ─── 영상 폴백 2: Ken Burns 애니메이션 슬라이드쇼 ────────────────────────────

def generate_video_ken_burns(image_path: str, audio_path: str, output_path: str,
                              ffmpeg_path: str = "ffmpeg") -> str:
    """
    ffmpeg zoompan 필터로 Ken Burns 효과(천천히 줌인) 영상 생성.
    정적 이미지보다 훨씬 역동적인 폴백.
    """
    if not os.path.exists(image_path):
        print(f"    [Ken Burns] 이미지 없음: {image_path}")
        return ""
    try:
        # 오디오 길이 측정
        duration = 30.0
        if os.path.exists(audio_path):
            probe = subprocess.run(
                [ffmpeg_path, "-i", audio_path],
                stderr=subprocess.PIPE, stdout=subprocess.PIPE
            )
            for line in probe.stderr.decode(errors="ignore").splitlines():
                if "Duration" in line:
                    parts = line.strip().split("Duration:")[1].split(",")[0].strip()
                    h, m, s = parts.split(":")
                    duration = int(h) * 3600 + int(m) * 60 + float(s)
                    break

        os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)

        # zoompan: 천천히 1.0→1.1 줌인, 중앙 기준
        zoom_expr = "zoom='min(zoom+0.0005,1.1)'"
        x_expr = "x='iw/2-(iw/zoom/2)'"
        y_expr = "y='ih/2-(ih/zoom/2)'"
        fps = 25
        total_frames = int(duration * fps)

        cmd = [
            ffmpeg_path, "-y",
            "-loop", "1", "-i", image_path,
        ]
        if os.path.exists(audio_path):
            cmd += ["-i", audio_path]
        cmd += [
            "-vf", f"scale=1280:720,zoompan={zoom_expr}:{x_expr}:{y_expr}:d={total_frames}:fps={fps},format=yuv420p",
            "-c:v", "libx264", "-preset", "fast", "-crf", "23",
        ]
        if os.path.exists(audio_path):
            cmd += ["-c:a", "aac", "-b:a", "192k", "-shortest"]
        cmd += ["-t", str(duration), output_path]

        print(f"    [Ken Burns] ffmpeg 렌더 시작 ({duration:.1f}초)...")
        subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
        print(f"    [Ken Burns] 완료: {output_path}")
        return output_path
    except Exception as e:
        print(f"    [Ken Burns] 실패: {e}")
        return ""


# ─── 병합 폴백: 단일 이미지 + 합산 오디오 ─────────────────────────────────────

def generate_simple_slideshow(image_paths: list, audio_paths: list,
                               output_path: str, ffmpeg_path: str = "ffmpeg") -> str:
    """
    ffmpeg으로 여러 이미지를 슬라이드쇼로 연결하고 오디오와 병합.
    concat 실패 시 최후의 폴백 영상 생성.
    """
    try:
        valid_images = [p for p in image_paths if p and os.path.exists(p)]
        valid_audios = [p for p in audio_paths if p and os.path.exists(p)]
        if not valid_images:
            print("    [Slideshow] 유효한 이미지 없음")
            return ""

        os.makedirs(os.path.dirname(os.path.abspath(output_path)), exist_ok=True)

        # 각 이미지를 30초 클립으로 만들어 concat
        part_clips = []
        for i, img in enumerate(valid_images):
            clip = output_path.replace(".mp4", f"_slide{i}.mp4")
            subprocess.run([
                ffmpeg_path, "-y",
                "-loop", "1", "-i", img,
                "-c:v", "libx264", "-t", "30", "-pix_fmt", "yuv420p",
                "-vf", "scale=1280:720",
                clip
            ], stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
            part_clips.append(clip)

        # 클립 concat
        list_file = output_path.replace(".mp4", "_list.txt")
        with open(list_file, "w") as f:
            for c in part_clips:
                f.write(f"file '{os.path.basename(c)}'\n")

        video_only = output_path.replace(".mp4", "_noaudio.mp4")
        subprocess.run([
            ffmpeg_path, "-y", "-f", "concat", "-safe", "0",
            "-i", list_file, "-c", "copy", video_only
        ], stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)

        # 오디오 병합 및 최종 합성
        if valid_audios:
            audio_inputs = []
            for a in valid_audios:
                audio_inputs += ["-i", a]
            n = len(valid_audios)
            merged_audio = output_path.replace(".mp4", "_audio.mp3")
            filter_str = "".join(f"[{i}:a]" for i in range(n))
            subprocess.run(
                [ffmpeg_path, "-y"] + audio_inputs + [
                    "-filter_complex", f"{filter_str}concat=n={n}:v=0:a=1[a]",
                    "-map", "[a]", "-c:a", "libmp3lame", "-b:a", "192k", merged_audio
                ],
                stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True
            )
            subprocess.run([
                ffmpeg_path, "-y",
                "-i", video_only, "-i", merged_audio,
                "-c:v", "copy", "-c:a", "aac", "-shortest", output_path
            ], stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
        else:
            shutil.copy(video_only, output_path)

        # 임시 파일 정리
        for f in part_clips + [list_file, video_only,
                                output_path.replace(".mp4", "_audio.mp3")]:
            if os.path.exists(f):
                try:
                    os.remove(f)
                except Exception:
                    pass

        print(f"    [Slideshow] 폴백 영상 완료: {output_path}")
        return output_path
    except Exception as e:
        print(f"    [Slideshow] 실패: {e}")
        return ""
