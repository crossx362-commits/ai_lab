import os
import subprocess
import sys

_here_vg = os.path.dirname(os.path.abspath(__file__))
_ai_team_vg = os.path.abspath(os.path.join(_here_vg, "..", "..", "..", ".."))
if _ai_team_vg not in sys.path:
    sys.path.insert(0, _ai_team_vg)
from _shared.ffmpeg_utils import get_ffmpeg_path

class VideoGenerator:
    """
    ffmpeg을 사용하여 이미지와 WAV 오디오를 결합하여 고품질 MP4 동영상을 생성합니다.
    """
    def __init__(self, ffmpeg_path: str = "ffmpeg"):
        self.ffmpeg_path = get_ffmpeg_path() if ffmpeg_path == "ffmpeg" else ffmpeg_path


    def generate_video(self, image_path: str, audio_path: str, output_path: str, duration: int = None) -> bool:
        """
        이미지와 오디오를 결합하여 유튜브 규격의 MP4 비디오를 렌더링합니다.
        """
        if not os.path.exists(image_path):
            print(f"[Error] 이미지 파일이 존재하지 않습니다: {image_path}")
            return False
        if not os.path.exists(audio_path):
            print(f"[Error] 오디오 파일이 존재하지 않습니다: {audio_path}")
            return False

        # ffmpeg 명령어 조율
        # -loop 1: 이미지 반복
        # -tune stillimage: 정지 이미지 인코딩 최적화
        # -pix_fmt yuv420p: 유튜브 및 일반 플레이어 호환 포맷
        cmd = [
            self.ffmpeg_path,
            "-y",
            "-loop", "1",
            "-i", image_path,
            "-i", audio_path,
            "-c:v", "libx264",
            "-tune", "stillimage",
            "-c:a", "aac",
            "-b:a", "192k",
            "-pix_fmt", "yuv420p"
        ]

        if duration:
            cmd.extend(["-t", str(duration)])
        else:
            cmd.append("-shortest")

        cmd.append(output_path)

        print(f"[Info] 비디오 생성 시작... (ffmpeg 실행 중, 목표 길이: {duration if duration else '오디오 원본'})")
        print(f"Command: {' '.join(cmd)}")
        
        try:
            result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, check=True)
            print(f"[Success] 비디오 렌더링 완료: {output_path}")
            return True
        except subprocess.CalledProcessError as e:
            print(f"[Error] ffmpeg 실행 실패. 에러 메시지:")
            print(e.stderr)
            return False
        except Exception as e:
            print(f"[Error] 비디오 생성 오류: {e}")
            return False

    def merge_video_audio(self, video_path: str, audio_path: str, output_path: str) -> bool:
        """
        Veo 무음 영상 + Lyria 오디오 합성.
        영상을 루프하여 오디오 길이에 맞추고, 오디오 종료 시점에 영상도 종료.
        """
        if not os.path.exists(video_path):
            print(f"[Error] 영상 파일 없음: {video_path}")
            return False
        if not os.path.exists(audio_path):
            print(f"[Error] 오디오 파일 없음: {audio_path}")
            return False

        cmd = [
            self.ffmpeg_path, "-y",
            "-stream_loop", "-1", "-i", video_path,
            "-i", audio_path,
            "-map", "0:v:0",
            "-map", "1:a:0",
            "-c:v", "libx264",
            "-c:a", "aac", "-b:a", "192k",
            "-pix_fmt", "yuv420p",
            "-shortest",
            output_path,
        ]

        print(f"[Info] Veo+Lyria 합성 시작... (오디오 길이 기준)")
        try:
            result = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, check=True)
            print(f"[Success] 뮤직비디오 합성 완료: {output_path}")
            return True
        except subprocess.CalledProcessError as e:
            print(f"[Error] ffmpeg 합성 실패:\n{e.stderr}")
            return False
        except Exception as e:
            print(f"[Error] 합성 오류: {e}")
            return False


if __name__ == "__main__":
    if len(sys.argv) >= 4:
        gen = VideoGenerator()
        gen.generate_video(sys.argv[1], sys.argv[2], sys.argv[3])
    else:
        print("Usage: python video_generator.py <image_path> <audio_path> <output_path>")
