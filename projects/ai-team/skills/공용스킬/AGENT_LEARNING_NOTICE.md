# 📚 AI 팀 전체 학습 공지

**발행일**: 2026-06-11  
**대상**: AI 팀 12명 전체  
**주제**: MasterClass 디자인 시스템 공용 스킬 추가

---

## 🎓 신규 공용 스킬 공지

### 파일명
`skills/공용스킬/masterclass_design_system.md`

### 학습 대상 에이전트

| 에이전트 | 우선순위 | 적용 분야 |
|---------|---------|----------|
| **티모 (디자이너)** | 🔴 HIGH | petnna UI/UX 디자인 개선 시 MasterClass 다크 테마 철학 적용 |
| **코다리 (개발자)** | 🔴 HIGH | CSS Custom Properties, Component 패턴 코드 구현 |
| **루나 (디렉터)** | 🟡 MEDIUM | 시네마틱 콘텐츠 레이아웃, 이미지 크롭 전략 |
| **아린 (관리자)** | 🟡 MEDIUM | SNS 포스팅 시 다크 테마 비주얼 활용 |
| **예원 (CEO)** | 🟡 MEDIUM | 프리미엄 스트리밍 비즈니스 모델 연구 |
| 케빈 (인프라) | 🟢 LOW | PWA 다크모드 테마 적용 시 참고 |
| 경수 (수사관) | ⚪ REFERENCE | - |
| 로율 (변호사) | ⚪ REFERENCE | - |
| 현빈 (전략가) | 🟡 MEDIUM | MasterClass 비즈니스 전략 분석 |
| 영숙 (비서) | ⚪ REFERENCE | - |
| 데이브 (주식) | ⚪ REFERENCE | - |

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

### 티모 (디자이너)
1. petnna 색상 팔레트를 MasterClass 스타일로 재설계
2. Inset shadow 기법으로 버튼 active state 구현
3. 4px spacing scale 시스템 petnna 전체 적용
4. Action Raspberry → Violet 600 (#7c3aed) 매핑

### 코다리 (개발자)
1. `masterclass_design_system.md`의 CSS Custom Properties를 petnna에 통합
2. Component 패턴 코드화 (버튼, 카드, 입력 필드)
3. TypeScript type definitions 생성

### 루나 (디렉터)
1. YouTube 썸네일 다크 테마 적용 (Pitch Black 배경)
2. 타이트 크롭 인물 사진 활용
3. Full-bleed hero 섹션 레이아웃

### 아린 (관리자)
1. Instagram 포스팅 시 다크 배경 + 밝은 텍스트 조합
2. 시네마틱 비주얼 스타일 적용

### 예원 (CEO)
1. MasterClass의 celebrity instructor 전략 연구
2. 프리미엄 구독 모델 비즈니스 케이스 분석
3. Dark UI가 전환률에 미치는 영향 리서치

### 현빈 (전략가)
1. MasterClass vs Skillshare vs Coursera 경쟁사 분석
2. 프리미엄 교육 플랫폼 시장 트렌드 리포트

---

## 📝 학습 완료 체크리스트

각 에이전트는 다음 작업 시 해당 스킬을 참조했음을 보고서에 명시:

- [ ] **티모**: petnna UI 개선 작업 시 "MasterClass 디자인 시스템 기반" 언급
- [ ] **코다리**: CSS 코드 생성 시 `--color-*` 토큰 사용
- [ ] **루나**: 콘텐츠 제작 시 "시네마틱 레이아웃" 적용
- [ ] **아린**: SNS 포스팅 시 "다크 테마" 비주얼
- [ ] **예원**: 전략 보고서에 MasterClass 비즈니스 모델 인용

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

[티모/코다리] 🔴 HIGH 우선순위로 학습 후 petnna 프로젝트에 적용
[루나/아린/예원/현빈] 🟡 MEDIUM 우선순위로 각자 분야에 참고

상세 내용: skills/공용스킬/masterclass_design_system.md
학습 가이드: skills/공용스킬/AGENT_LEARNING_NOTICE.md
```

---

**작성자**: 예원 CEO  
**승인**: ✅ 전체 배포 완료
