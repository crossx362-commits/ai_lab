# 할로우 나이트 화풍 — 단일 소스 (오너 2026-08-18 「흑백 느낌」)

생성 프롬프트는 아래 영문 블록을 그대로 앞에 붙인다. `aigen.py`의 `HOLLOW_STYLE`이 강제한다.

## 이 게임이 따라갈 것 (공식 스프라이트·환경에서 읽은 것)

1. **값이 먼저다.** 기사는  practically 흑백이다. 홀로우네스트 화면의 절반 가까이가 무채색이다. 색은 예외(영혼 청록, 감염 주황) 한 점만.
2. **실루엣을 검으로 채운 뒤** 안에 빛을 판다. 몸은 거의 검정·차콜, 얼굴만 뼈색 가면.
3. **가면:** 아이보리/백골, 눈은 **빈 검정 구멍**(눈동자·홍채 금지). 표정을 가면 구멍으로만.
4. **선 + 칠:** 두껍고 매끄러운 먹선(카툰) + 그 안은 부드러운 페인터 음영. 픽셀 도트 금지, 3D·사진 금지.
5. **벌레이되 단순.** 머리(가면)가 크고 몸은 작다. 귀여운 치비가 아니라 쓸쓸한 우화.
6. **액센트는 하나.** 직업당 빛 한 점까지. 이끼 초록 + 피 빨강 + 금빛 지팡이를 한 장에 넣지 마라.
7. **스프라이트에 땅·그림자 판을 깔지 마라.** 마젠타 위에 캐릭터만.
8. **포즈는 실루엣이 달라야** 애니다. 같은 자세를 살짝 밀면 안 된다.

## 우리가 틀린 것 (실측)

| | 맞는 쪽 | 틀린 쪽 |
|---|---|---|
| 딜러 idle | 검정 몸 + 흰 가면 + 빈 눈 | — |
| 탱 idle | 대체로 맞음, 이끼 초록이 조금 셈 | — |
| 수호기사·광전사 idle | — | 초록 이끼·빨간 균열·땅바닥·눈동자. 일러스트지 할로우 나이트가 아님 |

## 영문 강제 문단 (`HOLLOW_STYLE`)

```
STYLE LOCK — Hollow Knight official sprite language, NOT generic cartoon bugs:
Near-monochrome ink-and-wash. Body is VOID BLACK / charcoal. Face is a bone-white mask
with EMPTY black eye-sockets (no pupils, no iris, no angry cartoon eyes).
Thick smooth dark outlines + soft painterly shading inside. One accent color at most,
used as a tiny glow. NO saturated green moss, NO crimson rage, NO gold shine, NO grass
or ground shadow under the feet. NOT pixel art, NOT photoreal, NOT 3D, NOT chibi-cute.
Read as a dark silhouette first, then a pale mask.
```
