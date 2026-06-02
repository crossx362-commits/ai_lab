# Tool: Lyria 3 음악 생성기

Google의 차세대 고품질 음악 생성 모델인 Lyria 3를 호출하여 유튜브용 완곡 BGM(최대 3분) 또는 쇼츠/릴스용 30초 음악 클립을 자동으로 작곡합니다.

## 설정 파라미터 (JSON)

- `PROMPT`: 어떤 음악을 생성할지 상세하게 묘사하는 텍스트 프롬프트입니다.
  - *추천 템플릿*: `장르/시대 + 무드 + 특정 악기 + 보컬 스타일 + 가사/주제`
- `OUTPUT_FILENAME`: 저장할 음악 파일의 이름입니다. (예: `my_bgm.mp3`)
- `IS_PRO`: `true`로 설정하면 최대 3분 분량의 완곡(`lyria-3-pro-preview`)을 생성하며, `false`로 설정하면 30초 분량의 클립(`lyria-3-clip-preview`)을 생성합니다.

## 실행 방법

에이전트가 본 도구를 로드하여 아래와 같이 명령을 호출합니다.

```bash
# 3분 완곡 생성 (기본값)
python lyria_music_gen.py "Vibrant Lo-fi beats, warm synth, peaceful mood"

# 30초 클립 생성
python lyria_music_gen.py "Vibrant Lo-fi beats, warm synth, peaceful mood" --clip
```
