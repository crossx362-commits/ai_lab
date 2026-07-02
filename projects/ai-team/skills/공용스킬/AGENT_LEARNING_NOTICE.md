# 📚 AI 팀 전체 학습 공지

**발행일**: 2026-06-11 (2026-07-02 현 로스터 기준 갱신)  
**대상**: AI 팀 전체 (예원·영숙·소미 + 마켓데스크·행크·유나·레온·한별)  
**주제**: MasterClass 디자인 시스템 공용 스킬 추가

---

## 🎓 신규 공용 스킬 공지

### 파일명
`skills/공용스킬/masterclass_design_system.md`

### 학습 대상 에이전트

| 에이전트 | 우선순위 | 적용 분야 |
|---------|---------|----------|
| **예원 (CEO)** | 🔴 HIGH | petnna UI/UX 개선 디스패치 시 MasterClass 디자인 시스템 기준 적용 + 프리미엄 비즈니스 모델 연구 |
| 마켓데스크 | 🟡 MEDIUM | 프리미엄 구독 플랫폼 시장 트렌드 참고 |
| 영숙 (비서) | ⚪ REFERENCE | - |
| 소미 (분석가) | ⚪ REFERENCE | - |

> ※ 과거 담당(티모·코다리 등)은 에이전트 정리로 제거됨. petnna 디자인 작업은 예원 디스패치로 수행.

---

## 📖 핵심 학습 내용

### 1. 색상 시스템
- **Pitch Black (#000000)**: 주 배경
- **Charcoal Canvas (#222326)**: 카드 배경
- **Action Raspberry (#e32652)**: 주요 CTA (펫과나에서는 보라 계열로 변환)
- **Pure White (#ffffff)**: 주 텍스트

### 2. Typography
- **Sohne (Inter 대체)**: Body text, 버튼, 헤딩
- **Sohne Schmal (Oswald 대체)**: 거대 헤드라인 (64-80px)
- **Line height**: 1.3 (헤딩), 1.45 (본문)

### 3. Spacing
- **Base unit**: 4px
- **Scale**: 4, 8, 12, 16, 20, 24, 32, 48, 64, 80, 96, 112px
- **Section gap**: 64px

### 4. Border Radius
- **Cards**: 8px
- **Buttons**: 8px
- **Badges**: 20px
- **Inputs**: 0px (stark look)

### 5. Components
```css
/* Primary Action Button */
background: #e32652;
color: #ffffff;
border-radius: 8px;
padding: 12px 16px;

/* Card */
background: #272c33;
border-radius: 8px;
padding: 48px;

/* Input */
background: transparent;
border: 1px solid #ffffff;
border-radius: 0px;
```

---

## 🎯 에이전트별 학습 액션 아이템

### 예원 (CEO) — petnna 디자인 디스패치 겸임
1. petnna UI 작업 지시 시 색상 팔레트·4px spacing scale·Component 패턴을 MasterClass 기준으로 명시 (Action Raspberry → Violet 600 `#7c3aed` 매핑)
2. MasterClass의 celebrity instructor 전략·프리미엄 구독 모델 비즈니스 케이스 분석
3. Dark UI가 전환률에 미치는 영향 리서치

### 마켓데스크
1. MasterClass vs Skillshare vs Coursera 경쟁사 분석
2. 프리미엄 교육 플랫폼 시장 트렌드 리포트

---

## 📝 학습 완료 체크리스트

각 에이전트는 다음 작업 시 해당 스킬을 참조했음을 보고서에 명시:

- [ ] **예원**: petnna UI 개선 지시·전략 보고서에 "MasterClass 디자인 시스템 기반" 명시, CSS 산출물은 `--color-*` 토큰 사용

---

## 🔗 참고 자료

- **원본 파일**: `projects/ai-team/skills/공용스킬/masterclass_design_system.md`
- **출처**: Refero Styles (https://styles.refero.design/)
- **공식 사이트**: https://www.masterclass.com

---

## 📢 영숙 비서 전달 사항

**메시지 템플릿**:
```
📚 AI 팀 전체 학습 공지

MasterClass 디자인 시스템이 공용 스킬로 추가되었습니다.

[예원] 🔴 HIGH 우선순위 — petnna 프로젝트 디스패치 시 적용
[마켓데스크] 🟡 MEDIUM 우선순위 — 시장 트렌드 참고

상세 내용: skills/공용스킬/masterclass_design_system.md
학습 가이드: skills/공용스킬/AGENT_LEARNING_NOTICE.md
```

---

**작성자**: 예원 CEO  
**승인**: ✅ 전체 배포 완료
