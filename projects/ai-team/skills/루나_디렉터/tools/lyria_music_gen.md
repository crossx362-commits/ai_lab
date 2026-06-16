# Tool: Lyria 3 음악 생성기

Google Lyria 3를 호출하여 유튜브용 완곡 BGM(최대 3분) 또는 쇼츠용 30초 클립을 자동 작곡합니다.

## 설정 파라미터 (JSON)

- `PROMPT`: 음악을 묘사하는 텍스트 프롬프트. 아래 6단 구조를 따른다.
- `OUTPUT_FILENAME`: 저장할 파일명 (예: `my_bgm.mp3`)
- `IS_PRO`: `true` → 완곡 최소 2분 (`lyria-3-pro-preview`) / `false` → 30초 클립 (`lyria-3-clip-preview`)

## 음악 프롬프트 6단 구조 (SKILL.md Mission 2 준수)

```
[제목/키워드 연계 콘셉트] + [장르/시대] + [템포/무드] + [주요 악기] + [보컬 스타일] + [주제/가사(한국어)]
```

**장르 우선순위:**
1. Japanese City Pop × K-Pop Fusion — 110~150 BPM
2. Emotional Hip-Hop × R&B × Pop — 90~150 BPM
3. 기타 자유 장르 (K-Pop Dance 120~170 BPM)

**금지 키워드:** `Lofi / Lo-fi / Study Beats / Chill Beats / Sleep Music / White Noise / Ambient Study`

**프롬프트 예시:**
```
Connects with 'Golden Hour Drive' vibe,
Japanese City Pop × K-Pop Fusion (1980s Retro),
120 BPM Energetic & Nostalgic,
DX7 Piano + Slap Bass + Brass Synth,
Smooth powerful K-Pop female vocals,
황금빛 저녁 도시 드라이브, 설레는 청춘의 순간
```

## 실행 방법

```bash
# 완곡 생성 (기본값, 최소 2분)
python lyria_music_gen.py "Japanese City Pop × K-Pop Fusion, 120 BPM energetic, DX7 Piano + Slap Bass, Smooth K-Pop vocals, 서울 밤거리를 달리는 자유로운 감성"

# 30초 클립 생성
python lyria_music_gen.py "..." --clip
```
