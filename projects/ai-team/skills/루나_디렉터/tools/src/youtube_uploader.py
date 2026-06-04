import os
import pickle
from google_auth_oauthlib.flow import InstalledAppFlow
from google.auth.transport.requests import Request
from googleapiclient.discovery import build
from googleapiclient.http import MediaFileUpload
from googleapiclient.errors import HttpError

class YouTubeUploader:
    """
    YouTube Data API v3를 활용하여 생성된 비디오를 예약 업로드합니다.
    """
    def __init__(self, client_secrets_file: str = None, token_file: str = None):
        _tools_dir = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))

        # 프로젝트 루트 찾기 (find_project_root 사용)
        try:
            import sys as _sys
            _ai_team_root = os.path.abspath(os.path.join(_tools_dir, "..", "..", ".."))
            if _ai_team_root not in _sys.path:
                _sys.path.insert(0, _ai_team_root)
            from _shared.env_loader import find_project_root
            _root = find_project_root(_tools_dir)
        except:
            # 폴백: 수동 계산
            _root = os.path.abspath(os.path.join(_tools_dir, "..", "..", "..", ".."))

        # 1순위: 최상위 폴더 (환경 변수 관리 위치)
        _root_secret = os.path.join(_root, "client_secret.json")
        # 2순위: tools 폴더 (하위 호환성)
        _tools_secret = os.path.join(_tools_dir, "client_secret.json")

        self.client_secrets_file = client_secrets_file or (_root_secret if os.path.exists(_root_secret) else _tools_secret)
        self.token_file = token_file or os.path.join(_tools_dir, "youtube_token.pickle")
        # YouTube Data API 업로드 권한 범위 지정
        self.scopes = [
            "https://www.googleapis.com/auth/youtube",
            "https://www.googleapis.com/auth/youtube.upload",
        ]
        self.youtube = None

    def authenticate(self) -> bool:
        """
        OAuth 2.0 자격 증명을 획득 및 갱신합니다.
        """
        credentials = None

        # token.pickle에 저장된 캐시된 자격 증명 확인
        if os.path.exists(self.token_file):
            try:
                with open(self.token_file, "rb") as token:
                    credentials = pickle.load(token)
            except Exception as e:
                print(f"[Warning] 토큰 복원 실패: {e}")

        # 캐시된 자격 증명이 유효하지 않거나 없는 경우
        if not credentials or not credentials.valid:
            if credentials and credentials.expired and credentials.refresh_token:
                try:
                    credentials.refresh(Request())
                except Exception as e:
                    print(f"[Warning] 토큰 갱신 실패: {e}")
                    credentials = None
            
            if not credentials:
                # client_secret.json 파일 존재 여부 확인
                if not os.path.exists(self.client_secrets_file):
                    print(f"[Warning] 자격 증명 파일('{self.client_secrets_file}')이 없습니다. 드라이 런(Dry-run) 모드로 진행합니다.")
                    return False

                # 인증 흐름 시작
                try:
                    flow = InstalledAppFlow.from_client_secrets_file(self.client_secrets_file, self.scopes)
                    credentials = flow.run_local_server(port=0)
                except Exception as e:
                    print(f"[Error] OAuth 로컬 서버 실행 중 오류가 발생했습니다: {e}")
                    return False

            # 자격 증명 캐싱 저장
            try:
                with open(self.token_file, "wb") as token:
                    pickle.dump(credentials, token)
            except Exception as e:
                print(f"[Warning] 토큰 저장 실패: {e}")

        try:
            self.youtube = build("youtube", "v3", credentials=credentials)
            return True
        except Exception as e:
            print(f"[Error] YouTube API 클라이언트 빌드 실패: {e}")
            return False

    def upload_video(self, video_path: str, title: str, description: str, tags: list, privacy_status: str = "unlisted", publish_at: str = None) -> str:
        """
        유튜브 채널에 비디오를 업로드합니다. (예약 업로드 지원)
        """
        if not self.youtube:
            # 드라이 런 모드 (API 키가 연동되지 않은 상태)
            print("[Dry-Run] YouTube API가 연동되지 않았으므로 로컬 시뮬레이션을 진행합니다.")
            print(f"  - 동영상 경로: {video_path}")
            print(f"  - 제목: {title}")
            print(f"  - 설명:\n{description}")
            print(f"  - 태그: {', '.join(tags)}")
            print(f"  - 상태: {privacy_status}")
            if publish_at:
                print(f"  - 예약 업로드 일시: {publish_at}")
            return "DRY-RUN-SUCCESS-ID"

        if not os.path.exists(video_path):
            print(f"[Error] 업로드할 동영상이 존재하지 않습니다: {video_path}")
            return ""

        # 예약 업로드 설정 시 privacyStatus는 무조건 'private'이어야 함
        actual_privacy = privacy_status
        if publish_at:
            actual_privacy = "private"

        body = {
            "snippet": {
                "title": title,
                "description": description,
                "tags": tags,
                "categoryId": "10"  # Music 카테고리
            },
            "status": {
                "privacyStatus": actual_privacy,
                "selfDeclaredMadeForKids": False
            }
        }

        if publish_at:
            body["status"]["publishAt"] = publish_at

        # 1MB 단위 분할 업로드 설정
        media = MediaFileUpload(video_path, chunksize=1024*1024, resumable=True)

        try:
            print(f"[Info] 유튜브 서버로 동영상 업로드 시작: {video_path}")
            request = self.youtube.videos().insert(
                part="snippet,status",
                body=body,
                media_body=media
            )
            
            response = None
            while response is None:
                status, response = request.next_chunk()
                if status:
                    print(f"[Info] 업로드 진행률: {int(status.progress() * 100)}%")

            video_id = response.get("id")
            print(f"[Success] 업로드 완료. Video ID: {video_id}")
            print(f"유튜브 링크: https://youtu.be/{video_id}")
            return video_id

        except HttpError as e:
            print(f"[Error] HTTP 에러 발생: {e.resp.status} - {e.content}")
            return ""
        except Exception as e:
            print(f"[Error] 업로드 중 예기치 않은 오류 발생: {e}")
            return ""

    def upload_thumbnail(self, video_id: str, thumbnail_path: str) -> bool:
        """
        동영상에 커스텀 썸네일을 설정합니다.
        """
        if not self.youtube:
            print("[Dry-Run] YouTube API가 연동되지 않았으므로 썸네일 설정을 시뮬레이션합니다.")
            return True
            
        if not os.path.exists(thumbnail_path):
            print(f"[Error] 업로드할 썸네일 파일이 존재하지 않습니다: {thumbnail_path}")
            return False

        # YouTube 썸네일 최대 2MB — 초과 시 자동 압축
        _MAX_BYTES = 2 * 1024 * 1024
        upload_path = thumbnail_path
        if os.path.getsize(thumbnail_path) > _MAX_BYTES:
            try:
                from PIL import Image as _Img
                import io as _io
                img = _Img.open(thumbnail_path).convert("RGB")
                img.thumbnail((1280, 720), _Img.LANCZOS)
                compressed = thumbnail_path.replace(".png", "_thumb.jpg").replace(".jpg", "_thumb.jpg")
                quality = 90
                while quality >= 40:
                    img.save(compressed, "JPEG", quality=quality, optimize=True)
                    if os.path.getsize(compressed) <= _MAX_BYTES:
                        break
                    quality -= 10
                upload_path = compressed
                print(f"  [썸네일] {os.path.getsize(thumbnail_path)//1024}KB → {os.path.getsize(upload_path)//1024}KB 압축")
            except Exception as ce:
                print(f"  [썸네일 압축 실패] {ce} — 원본 사용")

        try:
            print(f"[Info] 유튜브 동영상(ID: {video_id})에 썸네일 설정 시작: {upload_path}")
            self.youtube.thumbnails().set(
                videoId=video_id,
                media_body=MediaFileUpload(upload_path)
            ).execute()
            print("[Success] 썸네일 업로드 완료!")
            return True
        except HttpError as e:
            print(f"[Error] 썸네일 업로드 중 HTTP 에러 발생: {e.resp.status} - {e.content}")
            return False
        except Exception as e:
            print(f"[Error] 썸네일 업로드 중 예기치 않은 오류 발생: {e}")
            return False

    def add_video_to_playlist(self, video_id: str, playlist_title: str) -> bool:
        """
        동영상을 지정된 이름의 재생목록에 자동으로 추가합니다. 재생목록이 없으면 생성합니다.
        """
        if not self.youtube:
            print(f"[Dry-Run] 유튜브 재생목록('{playlist_title}')에 동영상 추가 시뮬레이션")
            return True
            
        try:
            # 1. 기존 재생목록 검색
            playlist_id = None
            request = self.youtube.playlists().list(
                part="snippet",
                mine=True,
                maxResults=50
            )
            response = request.execute()
            for item in response.get("items", []):
                if item["snippet"]["title"] == playlist_title:
                    playlist_id = item["id"]
                    break
            
            # 2. 없으면 생성
            if not playlist_id:
                print(f"[Info] 재생목록 '{playlist_title}'이 없으므로 새로 생성합니다.")
                create_req = self.youtube.playlists().insert(
                    part="snippet,status",
                    body={
                        "snippet": {
                            "title": playlist_title,
                            "description": f"AI LUNA가 큐레이션한 {playlist_title} 플레이리스트입니다."
                        },
                        "status": {
                            "privacyStatus": "public"
                        }
                    }
                )
                create_res = create_req.execute()
                playlist_id = create_res["id"]
                print(f"[Success] 새 재생목록 생성 완료. ID: {playlist_id}")
                
            # 3. 재생목록에 동영상 추가
            self.youtube.playlistItems().insert(
                part="snippet",
                body={
                    "snippet": {
                        "playlistId": playlist_id,
                        "resourceId": {
                            "kind": "youtube#video",
                            "videoId": video_id
                        }
                    }
                }
            ).execute()
            print(f"[Success] 재생목록 '{playlist_title}'에 동영상(ID: {video_id}) 추가 완료!")
            return True
        except Exception as e:
            print(f"[Error] 재생목록 추가 중 오류 발생: {e}")
            return False


if __name__ == "__main__":
    uploader = YouTubeUploader()
    if uploader.authenticate():
        print("[Success] 유튜브 API 인증 성공")
    else:
        print("[Info] 드라이 런 모드로 동작 가능 여부 확인 완료")
