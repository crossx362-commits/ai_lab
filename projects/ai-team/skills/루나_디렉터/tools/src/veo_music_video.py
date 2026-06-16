"""
veo_music_video.py — 루나 전용 Veo 뮤직비디오 생성기
음악 테마(mood, style, genre) → Veo 프롬프트 변환 → 무음 영상 생성
최종 합성은 music_pipeline.py의 ffmpeg merge 단계에서 수행
"""
import os
import time
import io

try:
    from google import genai
    from google.genai import types
    from PIL import Image
except ImportError:
    genai = None
    types = None
    Image = None

VEO_MODEL_ID = "veo-3.1-generate-preview"


def _build_veo_prompts(theme: dict) -> tuple:
    """음악 테마 메타데이터 → Veo 뮤직비디오 프롬프트 (base + extend 리스트)"""
    style  = theme.get("style", "cinematic lofi aesthetic")
    mood   = theme.get("mood", "nostalgic, dreamy")
    genre  = theme.get("genre_era", "Japanese City Pop")

    base = (
        f"Cinematic music video, {style}, "
        f"{mood} atmosphere, smooth slow camera movement, "
        f"high quality 4K, no text, no logos, no people, dreamlike visuals, "
        f"film grain, shallow depth of field"
    )
    extends = [
        f"Slow pan across the scene, {mood} warm lighting, cinematic depth of field, {genre} aesthetic",
        f"Close-up atmospheric details, soft bokeh, {style}, gentle motion",
        f"Wide angle reveal, beautiful {mood} color palette, {genre} visual mood",
        f"Gentle dolly pull-back, atmospheric {mood} twilight, seamless loop ending",
    ]
    return base, extends


class VeoMusicVideoGenerator:
    """루나 파이프라인 전용 — 음악 테마로 뮤직비디오 영상(무음) 생성"""

    def __init__(self, api_key: str = None):
        raise ValueError("❌ [루나] Veo 뮤직비디오 생성 기능이 정책에 의해 차단되었습니다.")

    def _wait_active(self, vid_data):
        file_name = "files/" + vid_data.uri.split("files/")[-1].split(":")[0]
        print(f"  - [루나/Veo] 후처리 대기: {file_name}")
        while True:
            try:
                f = self.client.files.get(name=file_name)
                if hasattr(f, "state") and "ACTIVE" in str(f.state).upper():
                    break
            except Exception:
                pass
            time.sleep(5)
        return vid_data

    def generate(self, theme: dict, image_path: str = None,
                 output_path: str = "output/veo_raw.mp4") -> str:
        """
        음악 테마로 Veo 뮤직비디오(무음) 생성.
        image_path가 있으면 image-to-video, 없으면 text-to-video.
        Returns: output_path (성공) or None (실패)
        """
        base_prompt, extend_prompts = _build_veo_prompts(theme)

        try:
            val_image = None
            if image_path and os.path.exists(image_path):
                img = Image.open(image_path).convert("RGB")
                buf = io.BytesIO()
                img.save(buf, format="JPEG")
                val_image = types.Image(image_bytes=buf.getvalue(), mime_type="image/jpeg")

            print(f"  - [루나/Veo] 뮤직비디오 생성 시작...")
            print(f"    프롬프트: {base_prompt[:80]}...")

            if val_image:
                op = self.client.models.generate_videos(
                    model=VEO_MODEL_ID,
                    prompt=base_prompt,
                    image=val_image,
                    config=types.GenerateVideosConfig(aspect_ratio="16:9", resolution="720p"),
                )
            else:
                op = self.client.models.generate_videos(
                    model=VEO_MODEL_ID,
                    prompt=base_prompt,
                    config=types.GenerateVideosConfig(aspect_ratio="16:9", resolution="720p"),
                )

            while not op.done:
                time.sleep(15)
                op = self.client.operations.get(operation=op)

            current_video = self._wait_active(op.result.generated_videos[0].video)

            for idx, prompt in enumerate(extend_prompts):
                print(f"  - [루나/Veo] 영상 연장 {idx + 1}/{len(extend_prompts)}...")
                ext_op = self.client.models.generate_videos(
                    model=VEO_MODEL_ID,
                    prompt=prompt,
                    video=current_video,
                    config=types.GenerateVideosConfig(number_of_videos=1, resolution="720p"),
                )
                while not ext_op.done:
                    time.sleep(15)
                    ext_op = self.client.operations.get(operation=ext_op)
                current_video = self._wait_active(ext_op.result.generated_videos[0].video)

            file_name_part = "files/" + current_video.uri.split("files/")[-1].split(":")[0]
            video_bytes = self.client.files.download(file=file_name_part)
            os.makedirs(os.path.dirname(output_path) or ".", exist_ok=True)
            with open(output_path, "wb") as f:
                f.write(video_bytes)

            print(f"  - [루나/Veo] 뮤직비디오 다운로드 완료: {output_path}")
            return output_path

        except Exception as e:
            print(f"  - [Warning] Veo 생성 실패: {e}")
            return None
