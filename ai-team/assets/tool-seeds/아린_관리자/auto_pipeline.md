# Tool: Instagram 자동화 포스팅 파이프라인

구글 트렌드 검색어 감지, Ollama를 활용한 AI 콘텐츠/이미지 프롬프트 기획, Pollinations.ai 이미지 렌더링, Catbox.moe 이미지 업로드 및 최종 Instagram Graph API 발행까지의 모든 자동화 흐름을 통합 제어합니다.

## 설정 파라미터 (JSON)

- `DRY_RUN`: `true`로 설정하면 실제 인스타그램 업로드를 제외하고 기획 및 이미지 호스팅 단계까지만 드라이 런으로 진행합니다.

## 실행 방법

```bash
# 기본 파이프라인 가동
python auto_pipeline.py

# 드라이 런 모드 (테스트용)
python auto_pipeline.py --dry-run
```
