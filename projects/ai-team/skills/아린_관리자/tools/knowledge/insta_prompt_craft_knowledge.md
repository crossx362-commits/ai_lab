# 📚 인스타 이미지 프롬프트 설계 지식서
# knowledge/insta_prompt_craft_knowledge.md
# 최종 수정: 2026-06-02

---

## 1. 개요 (Overview)

인스타그램 자동화 파이프라인에서 **Gemini**(`gemini-3.1-flash-image-preview`)가
최고 품질 이미지를 생성하려면, 단순 키워드가 아닌
**카테고리 + 스타일 가이드 + 상세 서사 묘사** 3단계 구조의 프롬프트가 필요합니다.

이 지식서는 `prompt_crafter.py` 모듈의 설계 근거와 원리를 기록합니다.

---

## 2. 핵심 설계 원리

### 원칙 1. "카테고리가 곧 렌즈다"
이미지 AI는 촬영 스타일 지시어(type prompt)를 먼저 받아야
올바른 조명·구도·화풍을 선택합니다.
키워드를 그대로 넣으면 AI가 스타일을 임의로 결정하게 됩니다.

### 원칙 2. "한 장에 스토리를 압축하라"
단순 묘사("여성이 서 있다")보다
구체적 서사("soft cinematic lighting + shallow depth of field + sophisticated city background")가
더 높은 인게이지먼트를 유도합니다.

### 원칙 3. "품질 수식어는 후미에 공통 적용"
`masterpiece, exquisite and intricate details, vibrant and rich tones, sharp focus,
perfect artistic composition, cinematic ambience`
→ 이 12개 단어는 모든 카테고리에 공통으로 붙여 Gemini의 퀄리티 기준선을 높입니다.

---

## 3. 카테고리 분류 체계

```
트렌드 키워드 입력
        │
        ▼
  키워드 소문자 변환
        │
        ▼
 우선순위 순 매칭:
 person → animal → landscape → tech (기본)
        │
        ▼
  카테고리 결정
```

### 카테고리별 판별 핵심 키워드

| 카테고리 | 판별 키워드 (일부) |
|----------|--------------------|
| person   | 패션, 스타일, 오오티디, 룩, ootd, 인플루언서, 패션위크, 셀카 |
| animal   | 고양이, 강아지, 동물, 반려, cat, dog, 펫 |
| landscape| 자연, 우주, 풍경, 오로라, 바다, 산, 도시, 하늘 |
| tech     | ai, 인공지능, 로봇, 자동화, gpt, 반도체, 코딩, 메타버스 |

---

## 4. Type Prompt (스타일 가이드) 매핑

카테고리마다 촬영 기법·렌즈·분위기를 지정합니다.

```python
"landscape" → "A high-end cinematic masterpiece photography,
               trend-setting conceptual scenery,
               breathtaking visual narrative"

"animal"    → "An ultra-photorealistic trending studio portrait,
               highly detailed character concept,
               expressive emotional depth"

"person"    → "A cutting-edge editorial fashion magazine cover look,
               authentic instagram aesthetic,
               sophisticated cinematic lighting"

"tech"      → "A premium hyper-realistic 3D tech concept render,
               award-winning innovative artwork,
               stunning volumetric lighting effects"
```

---

## 5. 서사 묘사 (Narrative) 설계

### person (고정 서사 — 범용성 최고)
```
A stylish individual capturing a candid moment in a trendy urban setting.
Dressed in a cutting-edge minimalist outfit reflecting the latest social media fashion trends.
The scene features soft, warm cinematic lighting, with a shallow depth of field
blurring a sophisticated city background. Every texture of the clothing and
the authentic expression of confidence tell a story of modern youth and creative lifestyle.
```
> **설계 이유**: 특정 인물 지정 시 인스타 저작권 이슈 방지. "stylish individual"로 추상화.

### tech (고정 서사 — AI·기술 트렌드 범용)
```
A profound visualization of next-generation AI seamlessly integrating into human life.
A close-up of a sleek, transparent holographic device floating above a warm wooden desk.
Vibrant data streams and gentle light particles interact with a steaming cup of coffee.
A harmonious blend of advanced technology and cozy, meaningful daily life.
```
> **설계 이유**: 차가운 기술 이미지에 "따뜻한 일상" 요소(나무 책상, 커피)를 배합해 친근함 유도.

### landscape / animal (동적 서사 — 주제 직접 반영)
```
A masterpiece capture of {today_topic},
telling a beautiful visual story with extraordinary detail and depth.
```
> **설계 이유**: 풍경·동물은 주제 자체가 충분히 구체적이므로 동적 생성이 더 효과적.

---

## 6. 최종 프롬프트 조립 공식

```
{type_prompt} of {narrative}, {QUALITY_SUFFIX}
```

### 예시 출력 (person 카테고리)
```
A cutting-edge editorial fashion magazine cover look, authentic instagram aesthetic,
sophisticated cinematic lighting of A stylish individual capturing a candid moment
in a trendy urban setting. Dressed in a cutting-edge minimalist outfit ...,
masterpiece, exquisite and intricate details, vibrant and rich tones, sharp focus,
perfect artistic composition, cinematic ambience
```

---

## 7. 파이프라인 연동 위치

```
auto_pipeline.py
  └── generate_content(trend_topic)
        └── returns { image_prompt: "..." }  ← 기본 키워드 수준
              │
              ▼
        craft_insta_prompt(image_prompt)      ← prompt_crafter.py 적용
              │
              ▼
        _generate_image_gemini(crafted_prompt) ← Gemini gemini-3.1-flash-image-preview 호출
```

---

## 8. 확장 가이드

### 새 카테고리 추가 방법 (`prompt_crafter.py`)
1. `CATEGORY_KEYWORDS["새카테고리"] = [...]` 추가
2. `TYPE_PROMPTS["새카테고리"] = "..."` 추가
3. 필요 시 `NARRATIVE_TEMPLATES["새카테고리"] = "..."` 추가
4. `detect_category()` 우선순위 순서에 삽입

### 커스텀 topic_type 지정
```python
# 자동 판별 대신 명시적 카테고리 지정
prompt = craft_insta_prompt("오늘의 핫 트렌드 키워드", topic_type="person")
```

---

## 9. 관련 파일

| 파일 | 역할 |
|------|------|
| `prompt_crafter.py` | 핵심 Python 모듈 |
| `auto_pipeline.py` | 파이프라인 (prompt_crafter 연동) |
| `SKILL.md` | 에이전트 실행 지시서 (`skills/아린_관리자/SKILL.md`) |
| `uploader.py` | Instagram Graph API 업로더 + 토큰 자동 갱신 |
