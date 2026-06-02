import os
import sys
import time
import io
import json
import datetime

try:
    from google import genai
    from google.genai import types
    from PIL import Image
except ImportError:
    print("❌ 패키지 없음: 'pip install google-genai pillow' 실행 요망")
    exit(1)

_here = os.path.dirname(os.path.abspath(__file__))
_root = _here
for _ in range(10):
    if os.path.exists(os.path.join(_root, ".agent")):
        break
    _root = os.path.dirname(_root)
if _root not in sys.path:
    sys.path.insert(0, _root)

from _shared.env_loader import load_env
load_env(start_path=_here)

client = genai.Client(api_key=os.getenv("GEMINI_API_KEY"))
VEO_MODEL_ID = "veo-3.1-generate-preview"


def _record_to_history(record: dict):
    """통합 에이전트 메모리(reports/history/upload_history.json)에 레코드 추가."""
    here = os.path.dirname(os.path.abspath(__file__))
    root = here
    for _ in range(6):
        if os.path.isdir(os.path.join(root, "reports")):
            break
        root = os.path.dirname(root)
    mem_path = os.path.join(root, "reports", "history", "upload_history.json")
    try:
        if os.path.exists(mem_path):
            with open(mem_path, "r", encoding="utf-8") as _f:
                history = json.load(_f)
        else:
            history = []
        history.append(record)
        with open(mem_path, "w", encoding="utf-8") as f:
            json.dump(history, f, indent=4, ensure_ascii=False)
    except Exception as e:
        print(f"  [Warning] 히스토리 기록 실패: {e}")


def wait_for_active(vid_data):
    """구글 클라우드 내부망에서 영상 후처리(ACTIVE)가 끝날 때까지 대기하여 연장 에러를 방지합니다."""
    file_name = f"files/{vid_data.uri.split('files/')[-1].split(':')[0]}"
    print(f"⏳ [서버 대기] 후처리(ACTIVE) 상태 전환을 기다립니다: {file_name}")
    while True:
        try:
            f = client.files.get(name=file_name)
            if hasattr(f, "state") and "ACTIVE" in str(f.state).upper():
                print("✅ [완료] 영상 활성화 완료. 다음 노드로 진입 가능합니다.")
                break
        except Exception:
            pass
        time.sleep(5)
    return vid_data


def generate_long_take(image_path, base_prompt, extend_prompts=None, output_filename="result.mp4"):
    """
    이미지를 기반으로 첫 5초를 만들고, extend_prompts 배열 길이만큼
    이전 프레임을 이어받아 롱테이크로 연속 연장 생성합니다.

    Args:
        image_path: 기준 이미지 경로
        base_prompt: 초기 5초 생성 프롬프트
        extend_prompts: 각 연장 구간 프롬프트 리스트 (1개당 +5초)
        output_filename: 최종 저장 파일명
    """
    if extend_prompts is None:
        extend_prompts = []

    img = Image.open(image_path).convert("RGB")
    buf = io.BytesIO()
    img.save(buf, format="JPEG")
    val_image = types.Image(image_bytes=buf.getvalue(), mime_type="image/jpeg")

    print("\n🎬 [1 Phase] 초기 5초 영상 렌더링 중...")
    op = client.models.generate_videos(
        model=VEO_MODEL_ID,
        prompt=base_prompt,
        image=val_image,
        config=types.GenerateVideosConfig(aspect_ratio="16:9", resolution="720p"),
    )
    while not op.done:
        time.sleep(15)
        op = client.operations.get(operation=op)

    current_video = wait_for_active(op.result.generated_videos[0].video)

    # 전달받은 프롬프트 리스트만큼 체이닝 연장
    for idx, prompt in enumerate(extend_prompts):
        print(f"\n🎬 [{idx + 2} Phase] 비디오 연장(Extending) 중... (목표: +5초 추가)")
        ext_op = client.models.generate_videos(
            model=VEO_MODEL_ID,
            prompt=prompt,
            video=current_video,
            config=types.GenerateVideosConfig(number_of_videos=1, resolution="720p"),
        )
        while not ext_op.done:
            time.sleep(15)
            ext_op = client.operations.get(operation=ext_op)
        current_video = wait_for_active(ext_op.result.generated_videos[0].video)

    print("\n✅ 모든 비디오 연장 시퀀스 완료! 다운로드 시작...")
    file_name_part = "files/" + current_video.uri.split("files/")[-1].split(":")[0]

    video_bytes = client.files.download(file=file_name_part)
    with open(output_filename, "wb") as f:
        f.write(video_bytes)
    print(f"💾 로컬 다운로드 성공: {output_filename}")

    _record_to_history({
        "agent": "루나",
        "status": "published",
        "uploaded_at": datetime.datetime.now().isoformat(),
        "metadata": {
            "veo_prompt": base_prompt,
            "output_file": output_filename,
            "phases": len(extend_prompts) + 1,
        },
    })
    return output_filename


if __name__ == "__main__":
    extend_prompts = [
        "The scene smoothly transitions as the subject takes a bite.",
        "A beautiful close-up showing the detail of the food.",
        "Pull back to reveal the full table setting in warm golden light.",
    ]
    # generate_long_take("sample.jpg", "Cinematic master shot of a premium product on a wooden table.", extend_prompts, "my_ad.mp4")
